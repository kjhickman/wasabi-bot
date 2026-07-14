use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    wasabi_bot::telemetry::init();

    let pool = sqlx::PgPool::connect(&db::database_url_from_env()?).await?;

    tokio::select! {
        result = wasabi_bot::web::serve() => result,
        result = wasabi_bot::bot::run(pool) => result,
    }
}
