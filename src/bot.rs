use poise::serenity_prelude as serenity;
use serenity::utils::MessageBuilder;
use songbird::SerenityInit;

use crate::commands::{self, Data, Error, PlayerChannel};
use crate::db;

pub async fn run(pool: sqlx::PgPool) -> anyhow::Result<()> {
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
            event_handler: |_ctx, event, _framework, data| Box::pin(handle_event(event, data)),
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
                Ok(Data {
                    pool,
                    http,
                    gemini_api_key,
                    lavalink,
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
    client: lavalink_rs::prelude::LavalinkClient,
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
        if let Some((channel_id, http)) = player_text_channel(&client, event.guild_id) {
            let _ = channel_id
                .say(
                    http,
                    format!(
                        "Could not play **{}**: {}",
                        safe_text(&event.track.info.title),
                        safe_text(&event.exception.message)
                    ),
                )
                .await;
        }
    })
}

fn track_stuck_event(
    client: lavalink_rs::prelude::LavalinkClient,
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
        if let Some((channel_id, http)) = player_text_channel(&client, event.guild_id) {
            let _ = channel_id
                .say(
                    http,
                    format!(
                        "Playback got stuck on **{}**.",
                        safe_text(&event.track.info.title)
                    ),
                )
                .await;
        }
    })
}

fn player_text_channel(
    client: &lavalink_rs::prelude::LavalinkClient,
    guild_id: lavalink_rs::model::GuildId,
) -> Option<PlayerChannel> {
    let player = client.get_player_context(guild_id)?;
    let data = player.data::<PlayerChannel>().ok()?;
    Some((data.0, data.1.clone()))
}

fn safe_text(value: &str) -> String {
    MessageBuilder::new().push_safe(value).build()
}

async fn handle_event(event: &serenity::FullEvent, data: &Data) -> Result<(), Error> {
    if let serenity::FullEvent::InteractionCreate {
        interaction: serenity::Interaction::Command(command),
    } = event
    {
        let record = to_record(command);
        if let Err(error) = db::insert_interaction(&data.pool, &record).await {
            tracing::error!("failed to persist interaction {}: {error:?}", record.id);
        }
    }
    Ok(())
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
