use serde::Deserialize;

use super::State;

#[derive(Deserialize)]
pub(super) struct TokenResponse {
    pub(super) access_token: String,
    pub(super) refresh_token: String,
    pub(super) expires_in: i64,
}

#[derive(Deserialize)]
pub(super) struct DiscordUser {
    pub(super) id: String,
    pub(super) username: String,
    pub(super) global_name: Option<String>,
    pub(super) discriminator: String,
    pub(super) avatar: Option<String>,
}

#[derive(Clone, Deserialize, serde::Serialize)]
pub(in crate::web) struct DiscordGuild {
    pub(in crate::web) id: String,
    pub(in crate::web) name: String,
}

pub(super) async fn exchange_code(state: &State, code: &str) -> anyhow::Result<TokenResponse> {
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

pub(super) async fn refresh_token(
    state: &State,
    refresh_token: &str,
) -> anyhow::Result<TokenResponse> {
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
pub(super) async fn fetch_discord_guilds(
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

pub(super) async fn discord_user(state: &State, access_token: &str) -> anyhow::Result<DiscordUser> {
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

pub(super) async fn revoke_token(state: &State, token: &str) -> anyhow::Result<()> {
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
