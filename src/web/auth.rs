use base64::prelude::{BASE64_STANDARD, BASE64_URL_SAFE_NO_PAD, Engine as _};
use ring::{aead, digest, rand as ring_rand};
use serde::Deserialize;
use time::OffsetDateTime;
use topcoat::{
    Result,
    context::{Cx, app_context},
    cookie::{Cookie, Cookies, SameSite, cookies},
    router::{
        content::Form,
        error::{SeeOther, bad_request, forbidden, see_other},
    },
};

use super::State;

const SESSION_COOKIE: &str = "wasabi_session";
const CSRF_COOKIE: &str = "wasabi_csrf";
const OAUTH_STATE_COOKIE: &str = "wasabi_oauth_state";
const SESSION_DAYS: i64 = 30;

#[derive(Clone)]
pub(crate) struct OAuthConfig {
    client_id: String,
    client_secret: String,
    public_url: String,
    token_key: [u8; 32],
    secure_cookies: bool,
}

impl OAuthConfig {
    pub(crate) fn from_env() -> anyhow::Result<Self> {
        let public_url = required_env("PUBLIC_URL")?.trim_end_matches('/').to_owned();
        let key = BASE64_STANDARD
            .decode(required_env("WEB_TOKEN_KEY")?)
            .map_err(|_| anyhow::anyhow!("WEB_TOKEN_KEY must be valid base64"))?;
        let token_key = key
            .try_into()
            .map_err(|_| anyhow::anyhow!("WEB_TOKEN_KEY must decode to exactly 32 bytes"))?;

        Ok(Self {
            client_id: required_env("DISCORD_CLIENT_ID")?,
            client_secret: required_env("DISCORD_CLIENT_SECRET")?,
            secure_cookies: public_url.starts_with("https://"),
            public_url,
            token_key,
        })
    }

    fn redirect_uri(&self) -> String {
        format!("{}/auth/discord/callback", self.public_url)
    }
}

fn required_env(name: &str) -> anyhow::Result<String> {
    std::env::var(name).map_err(|_| anyhow::anyhow!("{name} is not set"))
}

#[derive(Clone)]
pub(crate) struct Session {
    pub(crate) id_hash: Vec<u8>,
    pub(crate) user_id: u64,
    pub(crate) username: String,
    pub(crate) global_name: Option<String>,
    pub(crate) discriminator: String,
    pub(crate) avatar: Option<String>,
    csrf_hash: Vec<u8>,
    pub(crate) csrf_token: String,
    pub(crate) guilds: Vec<DiscordGuild>,
    pub(crate) guilds_fetched_at: OffsetDateTime,
}

impl Session {
    pub(crate) fn display_name(&self) -> &str {
        self.global_name.as_deref().unwrap_or(&self.username)
    }

    pub(crate) fn avatar_url(&self) -> String {
        match &self.avatar {
            Some(hash) => format!(
                "https://cdn.discordapp.com/avatars/{}/{hash}.png?size=64",
                self.user_id
            ),
            None => {
                let index = if self.discriminator == "0" {
                    (self.user_id >> 22) % 6
                } else {
                    self.discriminator.parse::<u64>().unwrap_or_default() % 5
                };
                format!("https://cdn.discordapp.com/embed/avatars/{index}.png")
            }
        }
    }
}

#[derive(sqlx::FromRow)]
struct SessionRow {
    id_hash: Vec<u8>,
    user_id: i64,
    username: String,
    global_name: Option<String>,
    discriminator: String,
    avatar: Option<String>,
    csrf_hash: Vec<u8>,
    guilds: sqlx::types::Json<Vec<DiscordGuild>>,
    guilds_fetched_at: OffsetDateTime,
}

#[derive(Clone, sqlx::FromRow)]
struct TokenRow {
    id_hash: Vec<u8>,
    access_token: Vec<u8>,
    refresh_token: Vec<u8>,
    token_expires_at: OffsetDateTime,
}

#[derive(Deserialize)]
pub(crate) struct CallbackQuery {
    code: Option<String>,
    state: Option<String>,
    error: Option<String>,
}

#[derive(Deserialize)]
pub(crate) struct LogoutForm {
    csrf: String,
}

#[derive(Deserialize)]
struct TokenResponse {
    access_token: String,
    refresh_token: String,
    expires_in: i64,
}

#[derive(Deserialize)]
struct DiscordUser {
    id: String,
    username: String,
    global_name: Option<String>,
    discriminator: String,
    avatar: Option<String>,
}

#[derive(Clone, Deserialize, serde::Serialize)]
pub(crate) struct DiscordGuild {
    pub(crate) id: String,
    pub(crate) name: String,
}

pub(crate) async fn login(cx: &Cx) -> Result<SeeOther> {
    let state = app_context::<State>(cx);
    let oauth_state = random_token()?;
    sqlx::query("DELETE FROM oauth_states WHERE expires_at <= now()")
        .execute(&state.pool)
        .await?;
    sqlx::query("DELETE FROM web_sessions WHERE expires_at <= now()")
        .execute(&state.pool)
        .await?;
    sqlx::query("INSERT INTO oauth_states (id_hash, expires_at) VALUES ($1, $2)")
        .bind(hash(oauth_state.as_bytes()))
        .bind(OffsetDateTime::now_utc() + time::Duration::minutes(10))
        .execute(&state.pool)
        .await?;
    cookies(cx).add(web_cookie(
        OAUTH_STATE_COOKIE,
        &oauth_state,
        topcoat::cookie::time::Duration::minutes(10),
        state.oauth.secure_cookies,
    ));

    let mut url = reqwest::Url::parse("https://discord.com/oauth2/authorize")?;
    url.query_pairs_mut()
        .append_pair("response_type", "code")
        .append_pair("client_id", &state.oauth.client_id)
        .append_pair("scope", "identify guilds")
        .append_pair("redirect_uri", &state.oauth.redirect_uri())
        .append_pair("state", &oauth_state);
    Ok(see_other(url.as_str()))
}

pub(crate) async fn callback(cx: &Cx, Form(query): Form<CallbackQuery>) -> Result<SeeOther> {
    let state = app_context::<State>(cx);
    let jar = cookies(cx);
    let expected_state = jar
        .get(OAUTH_STATE_COOKIE)
        .map(|cookie| cookie.value().to_owned());
    jar.remove(Cookie::build((OAUTH_STATE_COOKIE, "")).path("/").build());

    if let Some(error) = query.error {
        return Err(bad_request(format!("Discord authorization failed: {error}")).into());
    }
    let supplied_state = query
        .state
        .ok_or_else(|| bad_request("missing OAuth state"))?;
    let expected_state = expected_state.ok_or_else(|| bad_request("missing OAuth state cookie"))?;
    if hash(supplied_state.as_bytes()) != hash(expected_state.as_bytes()) {
        return Err(bad_request("invalid OAuth state").into());
    }
    let consumed: Option<i32> = sqlx::query_scalar(
        "DELETE FROM oauth_states WHERE id_hash = $1 AND expires_at > now() RETURNING 1",
    )
    .bind(hash(supplied_state.as_bytes()))
    .fetch_optional(&state.pool)
    .await?;
    if consumed.is_none() {
        return Err(bad_request("expired or reused OAuth state").into());
    }
    let code = query
        .code
        .ok_or_else(|| bad_request("missing authorization code"))?;

    let token = exchange_code(state, &code).await?;
    let (user, guilds) = tokio::try_join!(
        discord_user(state, &token.access_token),
        fetch_discord_guilds(state, &token.access_token),
    )?;
    let user_id: u64 = user
        .id
        .parse()
        .map_err(|_| anyhow::anyhow!("Discord returned an invalid user ID"))?;
    let database_user_id = i64::try_from(user_id)
        .map_err(|_| anyhow::anyhow!("Discord user ID exceeds PostgreSQL bigint"))?;
    let session_token = random_token()?;
    let csrf_token = random_token()?;
    let now = OffsetDateTime::now_utc();

    sqlx::query(
        r#"INSERT INTO web_sessions
           (id_hash, user_id, username, global_name, discriminator, avatar,
             access_token, refresh_token, token_expires_at, csrf_hash, expires_at,
             guilds, guilds_fetched_at, guilds_refresh_attempted_at)
           VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)"#,
    )
    .bind(hash(session_token.as_bytes()))
    .bind(database_user_id)
    .bind(user.username)
    .bind(user.global_name)
    .bind(user.discriminator)
    .bind(user.avatar)
    .bind(encrypt(
        &state.oauth.token_key,
        token.access_token.as_bytes(),
    )?)
    .bind(encrypt(
        &state.oauth.token_key,
        token.refresh_token.as_bytes(),
    )?)
    .bind(now + time::Duration::seconds(token.expires_in))
    .bind(hash(csrf_token.as_bytes()))
    .bind(now + time::Duration::days(SESSION_DAYS))
    .bind(sqlx::types::Json(guilds))
    .bind(now)
    .bind(now)
    .execute(&state.pool)
    .await?;

    let max_age = topcoat::cookie::time::Duration::days(SESSION_DAYS);
    jar.add(web_cookie(
        SESSION_COOKIE,
        &session_token,
        max_age,
        state.oauth.secure_cookies,
    ));
    jar.add(web_cookie(
        CSRF_COOKIE,
        &csrf_token,
        max_age,
        state.oauth.secure_cookies,
    ));
    Ok(see_other("/"))
}

pub(crate) async fn logout(cx: &Cx, Form(form): Form<LogoutForm>) -> Result<SeeOther> {
    let state = app_context::<State>(cx);
    let Some(session) = current_session(cx).await? else {
        clear_session_cookies(cx);
        return Ok(see_other("/"));
    };
    verify_csrf(&session, &form.csrf)?;

    let mut transaction = state.pool.begin().await?;
    let encrypted_refresh: Option<Vec<u8>> =
        sqlx::query_scalar("SELECT refresh_token FROM web_sessions WHERE id_hash = $1 FOR UPDATE")
            .bind(&session.id_hash)
            .fetch_optional(&mut *transaction)
            .await?;
    sqlx::query("DELETE FROM web_sessions WHERE user_id = $1")
        .bind(session.user_id as i64)
        .execute(&mut *transaction)
        .await?;
    transaction.commit().await?;
    clear_session_cookies(cx);

    if let Some(encrypted_refresh) = encrypted_refresh {
        let refresh = decrypt_string(&state.oauth.token_key, &encrypted_refresh)?;
        if let Err(error) = revoke_token(state, &refresh).await {
            tracing::warn!(?error, "failed to revoke Discord OAuth token during logout");
        }
    }
    Ok(see_other("/"))
}

#[tracing::instrument(name = "web.session.load", skip(cx))]
pub(crate) async fn current_session(cx: &Cx) -> Result<Option<Session>> {
    let jar = cookies(cx);
    let Some(session_token) = jar
        .get(SESSION_COOKIE)
        .map(|cookie| cookie.value().to_owned())
    else {
        return Ok(None);
    };
    let Some(csrf_token) = jar.get(CSRF_COOKIE).map(|cookie| cookie.value().to_owned()) else {
        return Ok(None);
    };
    let state = app_context::<State>(cx);
    let row = sqlx::query_as::<_, SessionRow>(
        r#"SELECT id_hash, user_id, username, global_name, discriminator, avatar, csrf_hash,
                  guilds, guilds_fetched_at
           FROM web_sessions WHERE id_hash = $1 AND expires_at > now()"#,
    )
    .bind(hash(session_token.as_bytes()))
    .fetch_optional(&state.pool)
    .await?;

    let Some(row) = row else {
        sqlx::query("DELETE FROM web_sessions WHERE id_hash = $1 AND expires_at <= now()")
            .bind(hash(session_token.as_bytes()))
            .execute(&state.pool)
            .await?;
        clear_session_cookies(cx);
        return Ok(None);
    };
    let user_id = u64::try_from(row.user_id)
        .map_err(|_| anyhow::anyhow!("stored Discord user ID is invalid"))?;
    Ok(Some(Session {
        id_hash: row.id_hash,
        user_id,
        username: row.username,
        global_name: row.global_name,
        discriminator: row.discriminator,
        avatar: row.avatar,
        csrf_hash: row.csrf_hash,
        csrf_token,
        guilds: row.guilds.0,
        guilds_fetched_at: row.guilds_fetched_at,
    }))
}

pub(crate) fn verify_csrf(session: &Session, token: &str) -> Result<()> {
    if session.csrf_hash != hash(token.as_bytes()) {
        return Err(forbidden().into());
    }
    Ok(())
}

pub(crate) fn start_maintenance(state: State) {
    tokio::spawn(async move {
        let mut interval = tokio::time::interval(std::time::Duration::from_secs(60));
        loop {
            interval.tick().await;
            if let Err(error) = maintain_sessions(&state).await {
                tracing::warn!(?error, "web session maintenance failed");
            }
        }
    });
}

#[tracing::instrument(name = "web.session.maintain", skip(state))]
async fn maintain_sessions(state: &State) -> anyhow::Result<()> {
    sqlx::query("DELETE FROM web_sessions WHERE expires_at <= now()")
        .execute(&state.pool)
        .await?;
    sqlx::query("DELETE FROM oauth_states WHERE expires_at <= now()")
        .execute(&state.pool)
        .await?;

    let sessions = sqlx::query_as::<_, TokenRow>(
        r#"WITH candidates AS (
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
                     session.token_expires_at"#,
    )
    .fetch_all(&state.pool)
    .await?;

    let mut tasks = tokio::task::JoinSet::new();
    for session in sessions {
        while tasks.len() >= 8 {
            log_refresh_result(tasks.join_next().await);
        }
        let state = state.clone();
        tasks.spawn(async move { refresh_cached_guilds(&state, &session).await });
    }
    while !tasks.is_empty() {
        log_refresh_result(tasks.join_next().await);
    }
    Ok(())
}

fn log_refresh_result(result: Option<Result<anyhow::Result<()>, tokio::task::JoinError>>) {
    match result {
        Some(Ok(Err(error))) => tracing::warn!(?error, "failed to refresh cached Discord guilds"),
        Some(Err(error)) => tracing::warn!(?error, "Discord guild refresh task failed"),
        _ => {}
    }
}

#[tracing::instrument(name = "web.session.refresh_guilds", skip(state, session))]
async fn refresh_cached_guilds(state: &State, session: &TokenRow) -> anyhow::Result<()> {
    let access_token = access_token(state, session).await?;
    let guilds = fetch_discord_guilds(state, &access_token).await?;
    sqlx::query(
        "UPDATE web_sessions SET guilds = $1, guilds_fetched_at = now() WHERE id_hash = $2",
    )
    .bind(sqlx::types::Json(guilds))
    .bind(&session.id_hash)
    .execute(&state.pool)
    .await?;
    Ok(())
}

#[tracing::instrument(name = "discord.oauth.access_token", skip(state, session))]
async fn access_token(state: &State, session: &TokenRow) -> anyhow::Result<String> {
    if session.token_expires_at > OffsetDateTime::now_utc() + time::Duration::minutes(1) {
        return decrypt_string(&state.oauth.token_key, &session.access_token);
    }

    let mut transaction = state.pool.begin().await?;
    let row = sqlx::query_as::<_, TokenRow>(
        r#"SELECT id_hash, access_token, refresh_token, token_expires_at
           FROM web_sessions
           WHERE id_hash = $1 AND expires_at > now()
           FOR UPDATE"#,
    )
    .bind(&session.id_hash)
    .fetch_optional(&mut *transaction)
    .await?
    .ok_or_else(|| anyhow::anyhow!("session expired"))?;

    if row.token_expires_at > OffsetDateTime::now_utc() + time::Duration::minutes(1) {
        let access_token = decrypt_string(&state.oauth.token_key, &row.access_token)?;
        transaction.rollback().await?;
        return Ok(access_token);
    }

    let refresh = decrypt_string(&state.oauth.token_key, &row.refresh_token)?;
    let token = match refresh_token(state, &refresh).await {
        Ok(token) => token,
        Err(error) => {
            transaction.rollback().await?;
            return Err(error);
        }
    };
    let access_token = token.access_token.clone();
    sqlx::query(
        r#"UPDATE web_sessions
           SET access_token = $1, refresh_token = $2, token_expires_at = $3, updated_at = now()
           WHERE id_hash = $4"#,
    )
    .bind(encrypt(
        &state.oauth.token_key,
        token.access_token.as_bytes(),
    )?)
    .bind(encrypt(
        &state.oauth.token_key,
        token.refresh_token.as_bytes(),
    )?)
    .bind(OffsetDateTime::now_utc() + time::Duration::seconds(token.expires_in))
    .bind(&session.id_hash)
    .execute(&mut *transaction)
    .await?;
    transaction.commit().await?;
    Ok(access_token)
}

async fn exchange_code(state: &State, code: &str) -> anyhow::Result<TokenResponse> {
    token_request(
        state,
        &[
            ("grant_type", "authorization_code"),
            ("code", code),
            ("redirect_uri", &state.oauth.redirect_uri()),
        ],
    )
    .await
}

async fn refresh_token(state: &State, refresh_token: &str) -> anyhow::Result<TokenResponse> {
    token_request(
        state,
        &[
            ("grant_type", "refresh_token"),
            ("refresh_token", refresh_token),
        ],
    )
    .await
}

async fn token_request(state: &State, form: &[(&str, &str)]) -> anyhow::Result<TokenResponse> {
    Ok(state
        .http
        .post("https://discord.com/api/oauth2/token")
        .basic_auth(&state.oauth.client_id, Some(&state.oauth.client_secret))
        .form(form)
        .send()
        .await?
        .error_for_status()?
        .json()
        .await?)
}

#[tracing::instrument(name = "discord.oauth.guilds", skip(state, access_token))]
async fn fetch_discord_guilds(
    state: &State,
    access_token: &str,
) -> anyhow::Result<Vec<DiscordGuild>> {
    Ok(state
        .http
        .get("https://discord.com/api/v10/users/@me/guilds?limit=200")
        .bearer_auth(access_token)
        .send()
        .await?
        .error_for_status()?
        .json()
        .await?)
}

async fn discord_user(state: &State, access_token: &str) -> anyhow::Result<DiscordUser> {
    Ok(state
        .http
        .get("https://discord.com/api/v10/users/@me")
        .bearer_auth(access_token)
        .send()
        .await?
        .error_for_status()?
        .json()
        .await?)
}

async fn revoke_token(state: &State, token: &str) -> anyhow::Result<()> {
    state
        .http
        .post("https://discord.com/api/oauth2/token/revoke")
        .basic_auth(&state.oauth.client_id, Some(&state.oauth.client_secret))
        .form(&[("token", token), ("token_type_hint", "refresh_token")])
        .send()
        .await?
        .error_for_status()?;
    Ok(())
}

fn random_token() -> anyhow::Result<String> {
    use ring_rand::SecureRandom as _;
    let mut bytes = [0_u8; 32];
    ring_rand::SystemRandom::new()
        .fill(&mut bytes)
        .map_err(|_| anyhow::anyhow!("secure random generation failed"))?;
    Ok(BASE64_URL_SAFE_NO_PAD.encode(bytes))
}

fn hash(value: &[u8]) -> Vec<u8> {
    digest::digest(&digest::SHA256, value).as_ref().to_vec()
}

fn encrypt(key: &[u8; 32], plaintext: &[u8]) -> anyhow::Result<Vec<u8>> {
    use ring_rand::SecureRandom as _;
    let key = aead::LessSafeKey::new(
        aead::UnboundKey::new(&aead::AES_256_GCM, key)
            .map_err(|_| anyhow::anyhow!("invalid token encryption key"))?,
    );
    let mut nonce_bytes = [0_u8; 12];
    ring_rand::SystemRandom::new()
        .fill(&mut nonce_bytes)
        .map_err(|_| anyhow::anyhow!("secure random generation failed"))?;
    let nonce = aead::Nonce::assume_unique_for_key(nonce_bytes);
    let mut output = plaintext.to_vec();
    key.seal_in_place_append_tag(nonce, aead::Aad::empty(), &mut output)
        .map_err(|_| anyhow::anyhow!("token encryption failed"))?;
    let mut encrypted = nonce_bytes.to_vec();
    encrypted.extend(output);
    Ok(encrypted)
}

fn decrypt_string(key: &[u8; 32], encrypted: &[u8]) -> anyhow::Result<String> {
    if encrypted.len() < 12 + aead::AES_256_GCM.tag_len() {
        anyhow::bail!("encrypted token is truncated");
    }
    let (nonce, ciphertext) = encrypted.split_at(12);
    let key = aead::LessSafeKey::new(
        aead::UnboundKey::new(&aead::AES_256_GCM, key)
            .map_err(|_| anyhow::anyhow!("invalid token encryption key"))?,
    );
    let mut plaintext = ciphertext.to_vec();
    let plaintext = key
        .open_in_place(
            aead::Nonce::try_assume_unique_for_key(nonce)
                .map_err(|_| anyhow::anyhow!("invalid token nonce"))?,
            aead::Aad::empty(),
            &mut plaintext,
        )
        .map_err(|_| anyhow::anyhow!("token decryption failed"))?;
    Ok(String::from_utf8(plaintext.to_vec())?)
}

fn web_cookie(
    name: &'static str,
    value: &str,
    max_age: topcoat::cookie::time::Duration,
    secure: bool,
) -> Cookie<'static> {
    Cookie::build((name, value.to_owned()))
        .path("/")
        .http_only(true)
        .same_site(SameSite::Lax)
        .secure(secure)
        .max_age(max_age)
        .build()
}

fn clear_session_cookies(cx: &Cx) {
    let jar = cookies(cx);
    jar.remove(Cookie::build((SESSION_COOKIE, "")).path("/").build());
    jar.remove(Cookie::build((CSRF_COOKIE, "")).path("/").build());
}

#[cfg(test)]
mod tests {
    use super::{decrypt_string, encrypt};

    #[test]
    fn encrypted_token_round_trips_and_rejects_tampering() {
        let key = [7; 32];
        let mut encrypted = encrypt(&key, b"secret").unwrap();
        assert_eq!(decrypt_string(&key, &encrypted).unwrap(), "secret");
        *encrypted.last_mut().unwrap() ^= 1;
        assert!(decrypt_string(&key, &encrypted).is_err());
    }
}
