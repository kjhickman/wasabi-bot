use serde::Deserialize;
use topcoat::{
    Result,
    context::{Cx, app_context},
    router::{content::Form, error::SeeOther, error::forbidden, error::see_other, route},
};

use crate::{
    music, voice,
    web::{State, auth},
};

use super::access::{join_target, ready_target};

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
    let target = join_target(&bot, session.user_id, &session.guilds).ok_or_else(forbidden)?;
    let _guard = voice::lock_guild(&bot.voice_locks, target.guild_id).await;

    if ready_target(&bot, session.user_id, &session.guilds) == Some(target) {
        return Ok(see_other("/"));
    }
    let target = join_target(&bot, session.user_id, &session.guilds)
        .filter(|current| current == &target)
        .ok_or_else(forbidden)?;
    let lavalink = bot.lavalink.as_ref().ok_or_else(forbidden)?;
    music::join(
        &bot.serenity,
        lavalink,
        &bot.voice_state,
        target.guild_id,
        target.channel_id,
    )
    .await?;

    Ok(see_other("/"))
}
