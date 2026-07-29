use time::OffsetDateTime;

use super::DiscordGuild;

#[derive(sqlx::FromRow)]
pub(super) struct SessionRow {
    pub(super) id_hash: Vec<u8>,
    pub(super) user_id: i64,
    pub(super) username: String,
    pub(super) global_name: Option<String>,
    pub(super) discriminator: String,
    pub(super) avatar: Option<String>,
    pub(super) csrf_hash: Vec<u8>,
    pub(super) guilds: sqlx::types::Json<Vec<DiscordGuild>>,
    pub(super) guilds_fetched_at: OffsetDateTime,
}

#[derive(Clone, sqlx::FromRow)]
pub(super) struct TokenRow {
    pub(super) id_hash: Vec<u8>,
    pub(super) access_token: Vec<u8>,
    pub(super) refresh_token: Vec<u8>,
    pub(super) token_expires_at: OffsetDateTime,
}

pub(super) struct NewSession {
    pub(super) id_hash: Vec<u8>,
    pub(super) user_id: i64,
    pub(super) username: String,
    pub(super) global_name: Option<String>,
    pub(super) discriminator: String,
    pub(super) avatar: Option<String>,
    pub(super) access_token: Vec<u8>,
    pub(super) refresh_token: Vec<u8>,
    pub(super) token_expires_at: OffsetDateTime,
    pub(super) csrf_hash: Vec<u8>,
    pub(super) expires_at: OffsetDateTime,
    pub(super) guilds: Vec<DiscordGuild>,
    pub(super) guilds_fetched_at: OffsetDateTime,
}

pub(super) async fn delete_expired_sessions(pool: &sqlx::PgPool) -> anyhow::Result<()> {
    sqlx::query("DELETE FROM web_sessions WHERE expires_at <= now()")
        .execute(pool)
        .await?;
    Ok(())
}

pub(super) async fn delete_expired_oauth_states(pool: &sqlx::PgPool) -> anyhow::Result<()> {
    sqlx::query("DELETE FROM oauth_states WHERE expires_at <= now()")
        .execute(pool)
        .await?;
    Ok(())
}

pub(super) async fn insert_oauth_state(
    pool: &sqlx::PgPool,
    id_hash: Vec<u8>,
) -> anyhow::Result<()> {
    sqlx::query("INSERT INTO oauth_states (id_hash, expires_at) VALUES ($1, $2)")
        .bind(id_hash)
        .bind(OffsetDateTime::now_utc() + time::Duration::minutes(10))
        .execute(pool)
        .await?;
    Ok(())
}

pub(super) async fn consume_oauth_state(
    pool: &sqlx::PgPool,
    id_hash: Vec<u8>,
) -> anyhow::Result<bool> {
    let consumed: Option<i32> = sqlx::query_scalar(
        "DELETE FROM oauth_states WHERE id_hash = $1 AND expires_at > now() RETURNING 1",
    )
    .bind(id_hash)
    .fetch_optional(pool)
    .await?;
    Ok(consumed.is_some())
}

pub(super) async fn insert_session(pool: &sqlx::PgPool, session: NewSession) -> anyhow::Result<()> {
    sqlx::query(
        r"INSERT INTO web_sessions
           (id_hash, user_id, username, global_name, discriminator, avatar,
             access_token, refresh_token, token_expires_at, csrf_hash, expires_at,
             guilds, guilds_fetched_at, guilds_refresh_attempted_at)
           VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)",
    )
    .bind(session.id_hash)
    .bind(session.user_id)
    .bind(session.username)
    .bind(session.global_name)
    .bind(session.discriminator)
    .bind(session.avatar)
    .bind(session.access_token)
    .bind(session.refresh_token)
    .bind(session.token_expires_at)
    .bind(session.csrf_hash)
    .bind(session.expires_at)
    .bind(sqlx::types::Json(session.guilds))
    .bind(session.guilds_fetched_at)
    .bind(session.guilds_fetched_at)
    .execute(pool)
    .await?;
    Ok(())
}

pub(super) async fn lock_refresh_token(
    connection: &mut sqlx::PgConnection,
    id_hash: &[u8],
) -> anyhow::Result<Option<Vec<u8>>> {
    Ok(
        sqlx::query_scalar("SELECT refresh_token FROM web_sessions WHERE id_hash = $1 FOR UPDATE")
            .bind(id_hash)
            .fetch_optional(connection)
            .await?,
    )
}

pub(super) async fn delete_user_sessions(
    connection: &mut sqlx::PgConnection,
    user_id: i64,
) -> anyhow::Result<()> {
    sqlx::query("DELETE FROM web_sessions WHERE user_id = $1")
        .bind(user_id)
        .execute(connection)
        .await?;
    Ok(())
}

pub(super) async fn load_session(
    pool: &sqlx::PgPool,
    id_hash: &[u8],
) -> anyhow::Result<Option<SessionRow>> {
    Ok(sqlx::query_as::<_, SessionRow>(
        r"SELECT id_hash, user_id, username, global_name, discriminator, avatar, csrf_hash,
                   guilds, guilds_fetched_at
           FROM web_sessions WHERE id_hash = $1 AND expires_at > now()",
    )
    .bind(id_hash)
    .fetch_optional(pool)
    .await?)
}

pub(super) async fn delete_expired_session(
    pool: &sqlx::PgPool,
    id_hash: &[u8],
) -> anyhow::Result<()> {
    sqlx::query("DELETE FROM web_sessions WHERE id_hash = $1 AND expires_at <= now()")
        .bind(id_hash)
        .execute(pool)
        .await?;
    Ok(())
}

pub(super) async fn claim_guild_refreshes(pool: &sqlx::PgPool) -> anyhow::Result<Vec<TokenRow>> {
    Ok(sqlx::query_as::<_, TokenRow>(
        r"WITH candidates AS (
               SELECT id_hash
               FROM web_sessions
               WHERE guilds_fetched_at <= now() - interval '5 minutes'
                 AND guilds_refresh_attempted_at <= now() - interval '1 minute'
                 AND expires_at > now()
               ORDER BY guilds_refresh_attempted_at, guilds_fetched_at
               FOR UPDATE SKIP LOCKED
               LIMIT 100
           )
           UPDATE web_sessions AS session
           SET guilds_refresh_attempted_at = now()
           FROM candidates
           WHERE session.id_hash = candidates.id_hash
           RETURNING session.id_hash, session.access_token, session.refresh_token,
                      session.token_expires_at",
    )
    .fetch_all(pool)
    .await?)
}

pub(super) async fn update_guilds(
    pool: &sqlx::PgPool,
    id_hash: &[u8],
    guilds: Vec<DiscordGuild>,
) -> anyhow::Result<()> {
    sqlx::query(
        "UPDATE web_sessions SET guilds = $1, guilds_fetched_at = now() WHERE id_hash = $2",
    )
    .bind(sqlx::types::Json(guilds))
    .bind(id_hash)
    .execute(pool)
    .await?;
    Ok(())
}

pub(super) async fn lock_token_row(
    connection: &mut sqlx::PgConnection,
    id_hash: &[u8],
) -> anyhow::Result<Option<TokenRow>> {
    Ok(sqlx::query_as::<_, TokenRow>(
        r"SELECT id_hash, access_token, refresh_token, token_expires_at
           FROM web_sessions
           WHERE id_hash = $1 AND expires_at > now()
           FOR UPDATE",
    )
    .bind(id_hash)
    .fetch_optional(connection)
    .await?)
}

pub(super) async fn update_tokens(
    connection: &mut sqlx::PgConnection,
    id_hash: &[u8],
    access_token: Vec<u8>,
    refresh_token: Vec<u8>,
    token_expires_at: OffsetDateTime,
) -> anyhow::Result<()> {
    sqlx::query(
        r"UPDATE web_sessions
           SET access_token = $1, refresh_token = $2, token_expires_at = $3, updated_at = now()
           WHERE id_hash = $4",
    )
    .bind(access_token)
    .bind(refresh_token)
    .bind(token_expires_at)
    .bind(id_hash)
    .execute(connection)
    .await?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use testcontainers_modules::{postgres::Postgres, testcontainers::runners::AsyncRunner};

    use super::{consume_oauth_state, delete_expired_oauth_states, insert_oauth_state};

    #[tokio::test]
    async fn oauth_state_is_single_use_and_expired_states_are_rejected() -> anyhow::Result<()> {
        let container = Postgres::default().start().await?;
        let url = format!(
            "postgres://postgres:postgres@127.0.0.1:{}/postgres",
            container.get_host_port_ipv4(5432).await?
        );
        let pool = sqlx::PgPool::connect(&url).await?;
        sqlx::migrate!().run(&pool).await?;

        let active = vec![1];
        insert_oauth_state(&pool, active.clone()).await?;
        assert!(consume_oauth_state(&pool, active.clone()).await?);
        assert!(!consume_oauth_state(&pool, active).await?);

        let expired = vec![2];
        sqlx::query("INSERT INTO oauth_states (id_hash, expires_at) VALUES ($1, now() - interval '1 minute')")
            .bind(&expired)
            .execute(&pool)
            .await?;
        assert!(!consume_oauth_state(&pool, expired).await?);

        let survivor = vec![3];
        insert_oauth_state(&pool, survivor.clone()).await?;
        delete_expired_oauth_states(&pool).await?;
        let remaining: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM oauth_states")
            .fetch_one(&pool)
            .await?;
        assert_eq!(remaining, 1);
        assert!(consume_oauth_state(&pool, survivor).await?);
        Ok(())
    }
}
