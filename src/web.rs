mod app;
mod auth;
#[allow(dead_code)]
mod components;
mod theme;

pub use app::router;

#[derive(Clone)]
pub struct BotState {
    pub(crate) serenity: poise::serenity_prelude::Context,
    pub(crate) lavalink: Option<lavalink_rs::prelude::LavalinkClient>,
    pub(crate) voice_locks: crate::commands::VoiceLocks,
    pub(crate) voice_state: crate::commands::VoiceStates,
}

pub type SharedBotState = std::sync::Arc<tokio::sync::RwLock<Option<BotState>>>;

pub struct State {
    pub(crate) pool: sqlx::PgPool,
    pub(crate) http: reqwest::Client,
    pub(crate) bot: SharedBotState,
    pub(crate) oauth: auth::OAuthConfig,
}

pub async fn serve(pool: sqlx::PgPool, bot: SharedBotState) -> anyhow::Result<()> {
    let port: u16 = std::env::var("PORT")
        .ok()
        .and_then(|p| p.parse().ok())
        .unwrap_or(8080);
    let listener = tokio::net::TcpListener::bind(("0.0.0.0", port)).await?;
    tracing::info!("web server listening on port {port}");
    auth::start_cleanup(pool.clone());
    let state = State {
        pool,
        http: reqwest::Client::builder()
            .timeout(std::time::Duration::from_secs(10))
            .user_agent("Wasabi Bot (https://github.com/kyle/wasabi-bot, 0.1.0)")
            .build()?,
        bot,
        oauth: auth::OAuthConfig::from_env()?,
    };
    topcoat::serve_until(listener, router(state)?, std::future::pending::<()>()).await?;
    Ok(())
}
