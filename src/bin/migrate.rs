use wasabi_bot::db;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let pool = sqlx::PgPool::connect_with(db::connect_options_from_env()?).await?;
    sqlx::migrate!().run(&pool).await?;
    Ok(())
}
