use sqlx::PgPool;
use sqlx::postgres::PgConnectOptions;
use time::OffsetDateTime;

/// Builds connection options from `DATABASE_URL` (URI) or the Aspire-injected
/// `ConnectionStrings__wasabi_db` (ADO.NET key=value format).
pub fn connect_options_from_env() -> anyhow::Result<PgConnectOptions> {
    if let Ok(url) = std::env::var("DATABASE_URL") {
        return Ok(url.parse()?);
    }
    let raw = std::env::var("ConnectionStrings__wasabi_db").map_err(|_| {
        anyhow::anyhow!("neither DATABASE_URL nor ConnectionStrings__wasabi_db is set")
    })?;
    parse_connection_string(&raw)
}

pub fn parse_connection_string(raw: &str) -> anyhow::Result<PgConnectOptions> {
    if raw.starts_with("postgres://") || raw.starts_with("postgresql://") {
        return Ok(raw.parse()?);
    }
    let mut opts = PgConnectOptions::new();
    for pair in raw.split(';').map(str::trim).filter(|s| !s.is_empty()) {
        let (key, value) = pair
            .split_once('=')
            .ok_or_else(|| anyhow::anyhow!("invalid connection string segment: {pair}"))?;
        let value = value.trim();
        match key.trim().to_ascii_lowercase().as_str() {
            "host" | "server" => opts = opts.host(value),
            "port" => opts = opts.port(value.parse()?),
            "username" | "user id" | "userid" | "user" => opts = opts.username(value),
            "password" => opts = opts.password(value),
            "database" => opts = opts.database(value),
            _ => {}
        }
    }
    Ok(opts)
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

pub async fn insert_interaction(pool: &PgPool, r: &InteractionRecord) -> sqlx::Result<()> {
    sqlx::query(
        r#"INSERT INTO interactions
           (id, channel_id, application_id, user_id, guild_id, username, global_name, nickname, data, created_at)
           VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"#,
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_ado_net_connection_string() {
        let opts = parse_connection_string(
            "Host=localhost;Port=54320;Username=postgres;Password=p w;Database=wasabi_db",
        )
        .unwrap();
        assert_eq!(opts.get_host(), "localhost");
        assert_eq!(opts.get_port(), 54320);
        assert_eq!(opts.get_username(), "postgres");
        assert_eq!(opts.get_database(), Some("wasabi_db"));
    }

    #[test]
    fn parses_uri_connection_string() {
        let opts = parse_connection_string("postgres://u:p@db.example:5433/mydb").unwrap();
        assert_eq!(opts.get_host(), "db.example");
        assert_eq!(opts.get_port(), 5433);
        assert_eq!(opts.get_database(), Some("mydb"));
    }
}
