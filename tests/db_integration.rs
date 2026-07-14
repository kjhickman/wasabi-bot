use testcontainers_modules::postgres::Postgres;
use testcontainers_modules::testcontainers::runners::AsyncRunner;
use time::OffsetDateTime;
use wasabi_bot::db::{self, InteractionRecord};

fn record(id: i64, channel_id: i64, user: &str, command: &str) -> InteractionRecord {
    InteractionRecord {
        id,
        channel_id,
        application_id: 100,
        user_id: user.len() as i64,
        guild_id: Some(500),
        username: user.to_string(),
        global_name: None,
        nickname: None,
        data: Some(serde_json::json!({ "name": command })),
        created_at: OffsetDateTime::now_utc(),
    }
}

#[tokio::test]
async fn insert_interaction_and_compute_stats() -> anyhow::Result<()> {
    let container = Postgres::default().start().await?;
    let url = format!(
        "postgres://postgres:postgres@127.0.0.1:{}/postgres",
        container.get_host_port_ipv4(5432).await?
    );
    let pool = sqlx::PgPool::connect(&url).await?;
    sqlx::migrate!().run(&pool).await?;

    db::insert_interaction(&pool, &record(1, 10, "kyle", "flip")).await?;
    db::insert_interaction(&pool, &record(2, 10, "kyle", "flip")).await?;
    db::insert_interaction(&pool, &record(3, 20, "sam", "choose")).await?;
    // The interaction being excluded (simulates the in-flight /stats interaction).
    db::insert_interaction(&pool, &record(4, 10, "sam", "stats")).await?;

    let stats = db::get_stats(&pool, 10, 4).await?;

    assert_eq!(stats.total, 3);
    assert_eq!(stats.channel, 2);
    assert_eq!(stats.most_used_command, Some(("flip".to_string(), 2)));
    assert_eq!(stats.top_user, Some(("kyle".to_string(), 2)));
    Ok(())
}
