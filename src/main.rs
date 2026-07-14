use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    wasabi_bot::telemetry::init();

    let pool = sqlx::PgPool::connect(&db::database_url_from_env()?).await?;

    tokio::spawn(async {
        if let Err(error) = wasabi_bot::web::serve().await {
            tracing::error!("web server failed: {error:?}");
        }
    });

    wasabi_bot::bot::run(pool).await
}
