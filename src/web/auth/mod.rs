mod crypto;
mod discord;
mod maintenance;
mod session_store;

use base64::prelude::{BASE64_STANDARD, Engine as _};
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

use self::{
    crypto::{decrypt_string, encrypt, hash, random_token},
    discord::{discord_user, exchange_code, fetch_discord_guilds, revoke_token},
    session_store::NewSession,
};
use super::State;

pub(super) use discord::DiscordGuild;
pub(super) use maintenance::start_maintenance;

const SESSION_COOKIE: &str = "wasabi_session";
const CSRF_COOKIE: &str = "wasabi_csrf";
const OAUTH_STATE_COOKIE: &str = "wasabi_oauth_state";
const SESSION_DAYS: i64 = 30;

#[derive(Clone)]
pub(super) struct OAuthConfig {
    client_id: String,
    client_secret: String,
    public_url: String,
    token_key: [u8; 32],
    secure_cookies: bool,
}

impl OAuthConfig {
    pub(super) fn from_env() -> anyhow::Result<Self> {
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
pub(super) struct Session {
    pub(super) id_hash: Vec<u8>,
    pub(super) user_id: u64,
    pub(super) username: String,
    pub(super) global_name: Option<String>,
    pub(super) discriminator: String,
    pub(super) avatar: Option<String>,
    csrf_hash: Vec<u8>,
    pub(super) csrf_token: String,
    pub(super) guilds: Vec<DiscordGuild>,
    pub(super) guilds_fetched_at: OffsetDateTime,
}

impl Session {
    pub(super) fn display_name(&self) -> &str {
        self.global_name.as_deref().unwrap_or(&self.username)
    }

    pub(super) fn avatar_url(&self) -> String {
        self.avatar.as_ref().map_or_else(
            || {
                let index = if self.discriminator == "0" {
                    (self.user_id >> 22) % 6
                } else {
                    self.discriminator.parse::<u64>().unwrap_or_default() % 5
                };
                format!("https://cdn.discordapp.com/embed/avatars/{index}.png")
            },
            |hash| {
                format!(
                    "https://cdn.discordapp.com/avatars/{}/{hash}.png?size=64",
                    self.user_id
                )
            },
        )
    }
}

#[derive(Deserialize)]
pub(super) struct CallbackQuery {
    code: Option<String>,
    state: Option<String>,
    error: Option<String>,
}

#[derive(Deserialize)]
pub(super) struct LogoutForm {
    csrf: String,
}

pub(super) async fn login(cx: &Cx) -> Result<SeeOther> {
    let state = app_context::<State>(cx);
    let oauth_state = random_token()?;
    session_store::delete_expired_oauth_states(&state.pool).await?;
    session_store::delete_expired_sessions(&state.pool).await?;
    session_store::insert_oauth_state(&state.pool, hash(oauth_state.as_bytes())).await?;
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

pub(super) async fn callback(cx: &Cx, Form(query): Form<CallbackQuery>) -> Result<SeeOther> {
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
    if !session_store::consume_oauth_state(&state.pool, hash(supplied_state.as_bytes())).await? {
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

    session_store::insert_session(
        &state.pool,
        NewSession {
            id_hash: hash(session_token.as_bytes()),
            user_id: database_user_id,
            username: user.username,
            global_name: user.global_name,
            discriminator: user.discriminator,
            avatar: user.avatar,
            access_token: encrypt(&state.oauth.token_key, token.access_token.as_bytes())?,
            refresh_token: encrypt(&state.oauth.token_key, token.refresh_token.as_bytes())?,
            token_expires_at: now + time::Duration::seconds(token.expires_in),
            csrf_hash: hash(csrf_token.as_bytes()),
            expires_at: now + time::Duration::days(SESSION_DAYS),
            guilds,
            guilds_fetched_at: now,
        },
    )
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

pub(super) async fn logout(cx: &Cx, Form(form): Form<LogoutForm>) -> Result<SeeOther> {
    let state = app_context::<State>(cx);
    let Some(session) = current_session(cx).await? else {
        clear_session_cookies(cx);
        return Ok(see_other("/"));
    };
    verify_csrf(&session, &form.csrf)?;

    let mut transaction = state.pool.begin().await?;
    let encrypted_refresh =
        session_store::lock_refresh_token(&mut transaction, &session.id_hash).await?;
    session_store::delete_user_sessions(&mut transaction, session.user_id.cast_signed()).await?;
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
pub(super) async fn current_session(cx: &Cx) -> Result<Option<Session>> {
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
    let id_hash = hash(session_token.as_bytes());
    let row = session_store::load_session(&state.pool, &id_hash).await?;

    let Some(row) = row else {
        session_store::delete_expired_session(&state.pool, &id_hash).await?;
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

pub(super) fn verify_csrf(session: &Session, token: &str) -> Result<()> {
    if session.csrf_hash != hash(token.as_bytes()) {
        return Err(forbidden().into());
    }
    Ok(())
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
    use super::{DiscordGuild, Session, crypto::hash, verify_csrf};

    fn session(csrf: &str) -> Session {
        Session {
            id_hash: Vec::new(),
            user_id: 1,
            username: String::new(),
            global_name: None,
            discriminator: String::new(),
            avatar: None,
            csrf_hash: hash(csrf.as_bytes()),
            csrf_token: csrf.to_owned(),
            guilds: Vec::<DiscordGuild>::new(),
            guilds_fetched_at: time::OffsetDateTime::UNIX_EPOCH,
        }
    }

    #[test]
    fn csrf_verification_rejects_the_wrong_token() {
        let session = session("expected");

        assert!(verify_csrf(&session, "expected").is_ok());
        assert!(verify_csrf(&session, "wrong").is_err());
    }
}
