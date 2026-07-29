mod interactions;
mod voice_maintenance;

use poise::serenity_prelude as serenity;
use songbird::SerenityInit;

use crate::{
    commands::{self, Data, Error},
    db,
    ui_events::{UiEvent, UiEvents},
};

pub async fn run(
    pool: sqlx::PgPool,
    bot_state: crate::web::SharedBotState,
    ui_events: UiEvents,
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
                voice_maintenance::start(
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
    let (is_ssl, rest) = url.strip_prefix("https://").map_or_else(
        || (false, url.strip_prefix("http://").unwrap_or(url)),
        |rest| (true, rest),
    );
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
            let record = interactions::to_record(command);
            if let Err(error) = db::insert_interaction(&data.pool, &record).await {
                tracing::error!("failed to persist interaction {}: {error:?}", record.id);
            }
        }
        serenity::FullEvent::VoiceStateUpdate { old, new } => {
            if let Some(guild_id) = new
                .guild_id
                .or_else(|| old.as_ref().and_then(|old| old.guild_id))
            {
                let _ = data.ui_events.send(UiEvent::VoiceStateChanged {
                    guild_id,
                    user_id: new.user_id,
                    is_bot: new.user_id == ctx.cache.current_user().id,
                });
                voice_maintenance::disconnect_if_alone(ctx, data, guild_id).await?;
            }
        }
        _ => {}
    }
    Ok(())
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
