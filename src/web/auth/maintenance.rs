use time::OffsetDateTime;

use super::{
    State,
    crypto::{decrypt_string, encrypt},
    discord::{fetch_discord_guilds, refresh_token},
    session_store::{self, TokenRow},
};

pub(in crate::web) fn start_maintenance(state: State) {
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
    session_store::delete_expired_sessions(&state.pool).await?;
    session_store::delete_expired_oauth_states(&state.pool).await?;

    let sessions = session_store::claim_guild_refreshes(&state.pool).await?;
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
    session_store::update_guilds(&state.pool, &session.id_hash, guilds).await?;
    Ok(())
}

#[tracing::instrument(name = "discord.oauth.access_token", skip(state, session))]
async fn access_token(state: &State, session: &TokenRow) -> anyhow::Result<String> {
    if session.token_expires_at > OffsetDateTime::now_utc() + time::Duration::minutes(1) {
        return decrypt_string(&state.oauth.token_key, &session.access_token);
    }

    let mut transaction = state.pool.begin().await?;
    let row = session_store::lock_token_row(&mut transaction, &session.id_hash)
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
    session_store::update_tokens(
        &mut transaction,
        &session.id_hash,
        encrypt(&state.oauth.token_key, token.access_token.as_bytes())?,
        encrypt(&state.oauth.token_key, token.refresh_token.as_bytes())?,
        OffsetDateTime::now_utc() + time::Duration::seconds(token.expires_in),
    )
    .await?;
    transaction.commit().await?;
    Ok(access_token)
}
