use serde::Deserialize;
use topcoat::{
    Result,
    context::{Cx, app_context},
    router::{content::Form, error::SeeOther, error::forbidden, error::see_other, route},
};

use crate::{
    commands,
    web::{State, auth},
};

#[derive(Deserialize)]
struct JoinForm {
    csrf: String,
}

#[route(POST)]
async fn join(cx: &Cx, Form(form): Form<JoinForm>) -> Result<SeeOther> {
    let state = app_context::<State>(cx);
    let session = auth::current_session(cx).await?.ok_or_else(forbidden)?;
    auth::verify_csrf(&session, &form.csrf)?;
    if session.guilds_fetched_at <= time::OffsetDateTime::now_utc() - time::Duration::minutes(15) {
        return Err(forbidden().into());
    }
    let bot = state.bot.read().await.clone().ok_or_else(forbidden)?;
    let target = super::super::dashboard::join_target(&bot, session.user_id, &session.guilds)
        .ok_or_else(forbidden)?;
    let _guard = commands::lock_guild_voice(&bot.voice_locks, target.guild_id).await;

    if super::super::dashboard::ready_target(&bot, session.user_id, &session.guilds) == Some(target)
    {
        return Ok(see_other("/"));
    }
    let target = super::super::dashboard::join_target(&bot, session.user_id, &session.guilds)
        .filter(|current| current == &target)
        .ok_or_else(forbidden)?;
    let lavalink = bot.lavalink.as_ref().ok_or_else(forbidden)?;
    commands::join_voice_channel(&bot.serenity, lavalink, target.guild_id, target.channel_id)
        .await?;
    let mut voice_state = bot.voice_state.lock().await;
    let guild_voice_state = voice_state.entry(target.guild_id).or_default();
    guild_voice_state.idle_since = Some(std::time::Instant::now());
    guild_voice_state.paused_since = None;
    drop(voice_state);

    Ok(see_other("/"))
}
