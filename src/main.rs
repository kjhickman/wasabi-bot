use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    rustls::crypto::ring::default_provider()
        .install_default()
        .expect("failed to install rustls ring crypto provider");
    wasabi_bot::telemetry::init();

    let pool = sqlx::PgPool::connect(&db::database_url_from_env()?).await?;

    let bot_state = std::sync::Arc::new(tokio::sync::RwLock::new(None));

    tokio::select! {
        result = wasabi_bot::bot::run(pool.clone(), bot_state.clone()) => result,
        result = wasabi_bot::web::serve(pool, bot_state) => result,
    }
}
