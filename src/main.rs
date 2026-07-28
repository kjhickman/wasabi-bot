use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    rustls::crypto::ring::default_provider()
        .install_default()
        .expect("failed to install rustls ring crypto provider");
    wasabi_bot::telemetry::init();

    let pool = sqlx::PgPool::connect(&db::database_url_from_env()?).await?;

    tokio::select! {
        result = wasabi_bot::web::serve() => result,
        result = wasabi_bot::bot::run(pool) => result,
    }
}
