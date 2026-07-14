use sqlx::PgPool;
use time::OffsetDateTime;

pub fn database_url_from_env() -> anyhow::Result<String> {
    std::env::var("DATABASE_URL").map_err(|_| anyhow::anyhow!("DATABASE_URL is not set"))
}

pub struct InteractionRecord {
    pub id: i64,
    pub channel_id: i64,
    pub application_id: i64,
    pub user_id: i64,
    pub guild_id: Option<i64>,
    pub username: String,
    pub global_name: Option<String>,
    pub nickname: Option<String>,
    pub data: Option<serde_json::Value>,
    pub created_at: OffsetDateTime,
}

#[tracing::instrument(name = "db.insert_interaction", skip(pool, r), fields(interaction_id = r.id, channel_id = r.channel_id, guild_id = ?r.guild_id, user_id = r.user_id))]
pub async fn insert_interaction(pool: &PgPool, r: &InteractionRecord) -> sqlx::Result<()> {
    sqlx::query(
        r#"INSERT INTO interactions
           (id, channel_id, application_id, user_id, guild_id, username, global_name, nickname, data, created_at)
           VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
           ON CONFLICT (id) DO NOTHING"#,
    )
    .bind(r.id)
    .bind(r.channel_id)
    .bind(r.application_id)
    .bind(r.user_id)
    .bind(r.guild_id)
    .bind(&r.username)
    .bind(&r.global_name)
    .bind(&r.nickname)
    .bind(&r.data)
    .bind(r.created_at)
    .execute(pool)
    .await?;
    Ok(())
}

#[derive(Debug, Default)]
pub struct Stats {
    pub total: i64,
    pub channel: i64,
    pub most_used_command: Option<(String, i64)>,
    pub top_user: Option<(String, i64)>,
}

#[tracing::instrument(name = "db.get_stats", skip(pool))]
pub async fn get_stats(
    pool: &PgPool,
    channel_id: i64,
    exclude_interaction_id: i64,
) -> sqlx::Result<Stats> {
    let (total, channel): (i64, i64) = sqlx::query_as(
        r#"SELECT COUNT(*), COUNT(*) FILTER (WHERE channel_id = $1)
           FROM interactions WHERE id <> $2"#,
    )
    .bind(channel_id)
    .bind(exclude_interaction_id)
    .fetch_one(pool)
    .await?;

    let most_used_command: Option<(String, i64)> = sqlx::query_as(
        r#"SELECT data->>'name', COUNT(*)
           FROM interactions
           WHERE id <> $1 AND data->>'name' IS NOT NULL
           GROUP BY 1 ORDER BY 2 DESC LIMIT 1"#,
    )
    .bind(exclude_interaction_id)
    .fetch_optional(pool)
    .await?;

    let top_user: Option<(String, i64)> = sqlx::query_as(
        r#"SELECT MAX(COALESCE(global_name, username)), COUNT(*)
           FROM interactions
           WHERE id <> $1
           GROUP BY user_id ORDER BY 2 DESC LIMIT 1"#,
    )
    .bind(exclude_interaction_id)
    .fetch_optional(pool)
    .await?;

    Ok(Stats {
        total,
        channel,
        most_used_command,
        top_user,
    })
}
