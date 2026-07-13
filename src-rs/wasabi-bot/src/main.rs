use poise::serenity_prelude as serenity;
use wasabi_bot::commands::{self, Data, Error};
use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    wasabi_bot::telemetry::init();

    let pool = sqlx::PgPool::connect_with(db::connect_options_from_env()?).await?;

    tokio::spawn(async {
        if let Err(error) = run_health_server().await {
            tracing::error!("health server failed: {error:?}");
        }
    });

    let token = std::env::var("Discord__Token")
        .or_else(|_| std::env::var("DISCORD_TOKEN"))
        .map_err(|_| anyhow::anyhow!("neither Discord__Token nor DISCORD_TOKEN is set"))?;

    let framework = poise::Framework::builder()
        .options(poise::FrameworkOptions {
            commands: commands::all(),
            event_handler: |_ctx, event, _framework, data| {
                Box::pin(handle_event(event, data))
            },
            on_error: |error| Box::pin(on_error(error)),
            ..Default::default()
        })
        .setup(|_ctx, ready, _framework| {
            Box::pin(async move {
                tracing::info!("logged in as {}", ready.user.name);
                Ok(Data { pool })
            })
        })
        .build();

    let mut client = serenity::ClientBuilder::new(&token, serenity::GatewayIntents::empty())
        .framework(framework)
        .await?;
    client.start().await?;
    Ok(())
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

async fn run_health_server() -> anyhow::Result<()> {
    let port: u16 = std::env::var("PORT")
        .ok()
        .and_then(|p| p.parse().ok())
        .unwrap_or(8080);
    let app = axum::Router::new()
        .route("/health", axum::routing::get(|| async { "ok" }))
        .route("/alive", axum::routing::get(|| async { "ok" }));
    let listener = tokio::net::TcpListener::bind(("0.0.0.0", port)).await?;
    tracing::info!("health server listening on port {port}");
    axum::serve(listener, app).await?;
    Ok(())
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
