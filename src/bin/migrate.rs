use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let pool = sqlx::PgPool::connect(&db::database_url_from_env()?).await?;
    sqlx::migrate!().run(&pool).await?;
    Ok(())
}
