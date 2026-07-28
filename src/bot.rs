use poise::serenity_prelude as serenity;
use songbird::SerenityInit;

use crate::commands::{self, Data, Error, VoiceLocks, VoiceStates};
use crate::db;

const IDLE_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(30);
const PAUSED_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(60);
const VOICE_MAINTENANCE_INTERVAL: std::time::Duration = std::time::Duration::from_secs(5);

pub async fn run(
    pool: sqlx::PgPool,
    bot_state: crate::web::SharedBotState,
    ui_events: crate::web::UiEvents,
) -> anyhow::Result<()> {
    let token =
        std::env::var("DISCORD_TOKEN").map_err(|_| anyhow::anyhow!("DISCORD_TOKEN is not set"))?;
    let gemini_api_key = std::env::var("GEMINI_API_KEY").ok();
    if gemini_api_key.is_none() {
        tracing::warn!("GEMINI_API_KEY is not set; /conch will use weighted fallback only");
    }
    let lavalink_url = std::env::var("LAVALINK_URL").ok();
    if lavalink_url.is_none() {
        tracing::warn!("LAVALINK_URL is not set; music commands are disabled");
    }
    let http = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(10))
        .build()?;

    let framework = poise::Framework::builder()
        .options(poise::FrameworkOptions {
            commands: commands::all(),
            event_handler: |ctx, event, _framework, data| Box::pin(handle_event(ctx, event, data)),
            on_error: |error| Box::pin(on_error(error)),
            ..Default::default()
        })
        .setup(|ctx, ready, framework| {
            Box::pin(async move {
                tracing::info!("logged in as {}", ready.user.name);
                poise::builtins::register_globally(&ctx.http, &framework.options().commands)
                    .await?;
                let lavalink = match lavalink_url {
                    Some(url) => Some(build_lavalink_client(&url, ready.user.id).await?),
                    None => None,
                };
                let voice_locks = std::sync::Arc::new(tokio::sync::Mutex::default());
                let voice_state = std::sync::Arc::new(tokio::sync::Mutex::default());
                *bot_state.write().await = Some(crate::web::BotState {
                    serenity: ctx.clone(),
                    lavalink: lavalink.clone(),
                    voice_locks: voice_locks.clone(),
                    voice_state: voice_state.clone(),
                });
                start_voice_maintenance(
                    ctx.clone(),
                    lavalink.clone(),
                    voice_locks.clone(),
                    voice_state.clone(),
                );
                Ok(Data {
                    pool,
                    http,
                    gemini_api_key,
                    lavalink,
                    voice_locks,
                    voice_state,
                    ui_events,
                })
            })
        })
        .build();

    let intents = serenity::GatewayIntents::GUILDS | serenity::GatewayIntents::GUILD_VOICE_STATES;
    let mut client = serenity::ClientBuilder::new(&token, intents)
        .register_songbird()
        .framework(framework)
        .await?;
    client.start().await?;
    Ok(())
}

async fn build_lavalink_client(
    url: &str,
    user_id: serenity::UserId,
) -> anyhow::Result<lavalink_rs::prelude::LavalinkClient> {
    use lavalink_rs::{model::events, prelude::*};

    let events = events::Events {
        track_exception: Some(track_exception_event),
        track_stuck: Some(track_stuck_event),
        ..Default::default()
    };
    let password = std::env::var("LAVALINK_PASSWORD").unwrap_or_else(|_| "youshallnotpass".into());
    let (is_ssl, rest) = match url.strip_prefix("https://") {
        Some(rest) => (true, rest),
        None => (false, url.strip_prefix("http://").unwrap_or(url)),
    };
    let hostname = rest.trim_end_matches('/').to_string();

    let node = NodeBuilder {
        hostname,
        is_ssl,
        events: events::Events::default(),
        password,
        user_id: user_id.get().into(),
        session_id: None,
    };

    Ok(LavalinkClient::new(events, vec![node], NodeDistributionStrategy::round_robin()).await)
}

fn track_exception_event(
    _client: lavalink_rs::prelude::LavalinkClient,
    _session_id: String,
    event: &lavalink_rs::model::events::TrackException,
) -> lavalink_rs::model::BoxFuture<'_, ()> {
    Box::pin(async move {
        tracing::warn!(
            guild_id = %event.guild_id,
            title = %event.track.info.title,
            message = %event.exception.message,
            cause = %event.exception.cause,
            "Lavalink track exception"
        );
    })
}

fn track_stuck_event(
    _client: lavalink_rs::prelude::LavalinkClient,
    _session_id: String,
    event: &lavalink_rs::model::events::TrackStuck,
) -> lavalink_rs::model::BoxFuture<'_, ()> {
    Box::pin(async move {
        tracing::warn!(
            guild_id = %event.guild_id,
            title = %event.track.info.title,
            threshold_ms = event.threshold_ms,
            "Lavalink track stuck"
        );
    })
}

async fn handle_event(
    ctx: &serenity::Context,
    event: &serenity::FullEvent,
    data: &Data,
) -> Result<(), Error> {
    match event {
        serenity::FullEvent::InteractionCreate {
            interaction: serenity::Interaction::Command(command),
        } => {
            let record = to_record(command);
            if let Err(error) = db::insert_interaction(&data.pool, &record).await {
                tracing::error!("failed to persist interaction {}: {error:?}", record.id);
            }
        }
        serenity::FullEvent::VoiceStateUpdate { old, new } => {
            if let Some(guild_id) = new
                .guild_id
                .or_else(|| old.as_ref().and_then(|old| old.guild_id))
            {
                let _ = data.ui_events.send(crate::web::UiEvent::VoiceStateChanged {
                    guild_id,
                    user_id: new.user_id,
                    is_bot: new.user_id == ctx.cache.current_user().id,
                });
                disconnect_if_alone(ctx, data, guild_id).await?;
            }
        }
        _ => {}
    }
    Ok(())
}

fn start_voice_maintenance(
    ctx: serenity::Context,
    lavalink: Option<lavalink_rs::prelude::LavalinkClient>,
    voice_locks: VoiceLocks,
    voice_state: VoiceStates,
) {
    tokio::spawn(async move {
        let mut interval = tokio::time::interval(VOICE_MAINTENANCE_INTERVAL);
        loop {
            interval.tick().await;
            if let Err(error) = maintain_voice(&ctx, &lavalink, &voice_locks, &voice_state).await {
                tracing::warn!(?error, "voice maintenance failed");
            }
        }
    });
}

async fn maintain_voice(
    ctx: &serenity::Context,
    lavalink: &Option<lavalink_rs::prelude::LavalinkClient>,
    voice_locks: &VoiceLocks,
    voice_state: &VoiceStates,
) -> Result<(), Error> {
    let Some(lavalink) = lavalink else {
        return Ok(());
    };
    let guild_ids: Vec<_> = {
        let voice_state = voice_state.lock().await;
        voice_state.keys().copied().collect()
    };

    for guild_id in guild_ids {
        let _guard = commands::lock_guild_voice(voice_locks, guild_id).await;
        if bot_voice_channel(&ctx.cache, guild_id).is_none() {
            clear_voice_state(voice_state, guild_id).await;
            continue;
        }
        let Some(player) = lavalink.get_player_context(lava_guild(guild_id)) else {
            clear_voice_state(voice_state, guild_id).await;
            continue;
        };

        let player_data = match player.get_player().await {
            Ok(player_data) => player_data,
            Err(error) => {
                tracing::warn!(?error, %guild_id, "failed to inspect voice player");
                continue;
            }
        };
        let queue_empty = match player.get_queue().get_count().await {
            Ok(count) => count == 0,
            Err(error) => {
                tracing::warn!(?error, %guild_id, "failed to inspect voice queue");
                continue;
            }
        };
        let idle = player_data.track.is_none() && queue_empty;
        let now = std::time::Instant::now();
        let should_disconnect = {
            let mut voice_state = voice_state.lock().await;
            let state = voice_state.entry(guild_id).or_default();
            if idle {
                state.idle_since.get_or_insert(now);
            } else {
                state.idle_since = None;
            }
            let idle_expired = state
                .idle_since
                .is_some_and(|since| now.duration_since(since) >= IDLE_TIMEOUT);
            let paused_expired = state
                .paused_since
                .is_some_and(|since| now.duration_since(since) >= PAUSED_TIMEOUT);
            idle_expired || paused_expired
        };
        if should_disconnect {
            disconnect_voice(ctx, lavalink, voice_state, guild_id).await?;
        }
    }
    Ok(())
}

async fn disconnect_if_alone(
    ctx: &serenity::Context,
    data: &Data,
    guild_id: serenity::GuildId,
) -> Result<(), Error> {
    let Some(bot_channel_id) = bot_voice_channel(&ctx.cache, guild_id) else {
        return Ok(());
    };
    if has_human_in_voice_channel(&ctx.cache, guild_id, bot_channel_id) {
        return Ok(());
    }
    let _guard = commands::lock_guild_voice(&data.voice_locks, guild_id).await;
    let Some(bot_channel_id) = bot_voice_channel(&ctx.cache, guild_id) else {
        return Ok(());
    };
    if !has_human_in_voice_channel(&ctx.cache, guild_id, bot_channel_id) {
        if let Some(lavalink) = &data.lavalink {
            disconnect_voice(ctx, lavalink, &data.voice_state, guild_id).await?;
        } else {
            remove_voice_connection(ctx, guild_id).await?;
            clear_voice_state(&data.voice_state, guild_id).await;
        }
    }
    Ok(())
}

async fn disconnect_voice(
    ctx: &serenity::Context,
    lavalink: &lavalink_rs::prelude::LavalinkClient,
    voice_state: &VoiceStates,
    guild_id: serenity::GuildId,
) -> Result<(), Error> {
    let _ = lavalink.delete_player(lava_guild(guild_id)).await;
    remove_voice_connection(ctx, guild_id).await?;
    clear_voice_state(voice_state, guild_id).await;
    Ok(())
}

async fn remove_voice_connection(
    ctx: &serenity::Context,
    guild_id: serenity::GuildId,
) -> Result<(), Error> {
    let manager = songbird::get(ctx)
        .await
        .ok_or_else(|| anyhow::anyhow!("songbird is not registered"))?;
    if manager.get(guild_id).is_some() {
        manager.remove(guild_id).await?;
    }
    Ok(())
}

async fn clear_voice_state(voice_state: &VoiceStates, guild_id: serenity::GuildId) {
    voice_state.lock().await.remove(&guild_id);
}

fn bot_voice_channel(
    cache: &impl AsRef<serenity::Cache>,
    guild_id: serenity::GuildId,
) -> Option<serenity::ChannelId> {
    let cache = cache.as_ref();
    cache
        .guild(guild_id)?
        .voice_states
        .get(&cache.current_user().id)
        .and_then(|state| state.channel_id)
}

fn has_human_in_voice_channel(
    cache: &impl AsRef<serenity::Cache>,
    guild_id: serenity::GuildId,
    channel_id: serenity::ChannelId,
) -> bool {
    let cache = cache.as_ref();
    let current_user_id = cache.current_user().id;
    cache.guild(guild_id).is_some_and(|guild| {
        guild.voice_states.values().any(|state| {
            state.channel_id == Some(channel_id)
                && state.user_id != current_user_id
                && !state
                    .member
                    .as_ref()
                    .or_else(|| guild.members.get(&state.user_id))
                    .is_some_and(|member| member.user.bot)
        })
    })
}

fn lava_guild(guild_id: serenity::GuildId) -> lavalink_rs::model::GuildId {
    guild_id.get().into()
}

fn to_record(command: &serenity::CommandInteraction) -> db::InteractionRecord {
    db::InteractionRecord {
        id: command.id.get() as i64,
        channel_id: command.channel_id.get() as i64,
        application_id: command.application_id.get() as i64,
        user_id: command.user.id.get() as i64,
        guild_id: command.guild_id.map(|id| id.get() as i64),
        username: command.user.name.clone(),
        global_name: command.user.global_name.clone(),
        nickname: command.member.as_ref().and_then(|m| m.nick.clone()),
        data: serde_json::to_value(&command.data).ok(),
        created_at: snowflake_timestamp(command.id.get()),
    }
}

fn snowflake_timestamp(id: u64) -> time::OffsetDateTime {
    const DISCORD_EPOCH_MS: i128 = 1_420_070_400_000;
    let ms = (id >> 22) as i128 + DISCORD_EPOCH_MS;
    time::OffsetDateTime::from_unix_timestamp_nanos(ms * 1_000_000)
        .unwrap_or(time::OffsetDateTime::UNIX_EPOCH)
}

async fn on_error(error: poise::FrameworkError<'_, Data, Error>) {
    match error {
        poise::FrameworkError::Command { error, ctx, .. } => {
            tracing::error!(command = ctx.command().name, "command failed: {error:?}");
            let _ = ctx
                .send(
                    poise::CreateReply::default()
                        .content(
                            "Something went wrong while processing that command. Please try again later.",
                        )
                        .ephemeral(true),
                )
                .await;
        }
        other => {
            if let Err(error) = poise::builtins::on_error(other).await {
                tracing::error!("error while handling error: {error:?}");
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::snowflake_timestamp;

    #[test]
    fn snowflake_timestamp_decodes_discord_epoch() {
        // Snowflake 175928847299117063 => 2016-04-30 11:18:25.796 UTC (Discord docs example)
        let ts = snowflake_timestamp(175_928_847_299_117_063);
        assert_eq!(ts.unix_timestamp(), 1_462_015_105);
    }
}
