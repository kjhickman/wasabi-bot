use std::collections::HashSet;

use poise::serenity_prelude as serenity;
use tokio_stream::{Stream, StreamExt, wrappers::BroadcastStream};
use topcoat::{
    Result,
    context::{Cx, app_context},
    router::{
        content::sse::{Event, KeepAlive, Sse},
        error::forbidden,
        route,
    },
};

use crate::{
    ui_events::UiEvent,
    web::{State, auth},
};

#[route(GET)]
async fn events(cx: &Cx) -> Result<Sse<impl Stream<Item = Result<Event>> + use<>>> {
    let state = app_context::<State>(cx);
    let receiver = state.ui_events.subscribe();
    let session = auth::current_session(cx).await?.ok_or_else(forbidden)?;
    let user_id = serenity::UserId::new(session.user_id);
    let mut guilds: HashSet<_> = session
        .guilds
        .iter()
        .filter_map(|guild| guild.id.parse().ok())
        .map(serenity::GuildId::new)
        .collect();
    if let Some(bot) = state.bot.read().await.as_ref() {
        for guild_id in bot.serenity.cache.guilds() {
            if bot
                .serenity
                .cache
                .guild(guild_id)
                .is_some_and(|guild| guild.voice_states.contains_key(&user_id))
            {
                guilds.insert(guild_id);
            }
        }
    }
    let mut accept_all_bot_events = false;
    let updates = BroadcastStream::new(receiver).filter_map(move |result| match result {
        Ok(event) if is_relevant(event, user_id, &mut guilds, accept_all_bot_events) => {
            Some(Ok(voice_event()))
        }
        Err(_) => {
            accept_all_bot_events = true;
            Some(Ok(voice_event()))
        }
        _ => None,
    });
    let initial = tokio_stream::once(Ok(voice_event()));
    let stream = tokio_stream::StreamExt::chain(initial, updates);
    let stream = futures_util::StreamExt::take_until(
        stream,
        tokio::time::sleep(std::time::Duration::from_secs(60)),
    );

    Ok(Sse::new(stream).keep_alive(KeepAlive::new()))
}

fn voice_event() -> Event {
    Event::new().event("voice").data("refresh")
}

fn is_relevant(
    event: UiEvent,
    user_id: serenity::UserId,
    guilds: &mut HashSet<serenity::GuildId>,
    accept_all_bot_events: bool,
) -> bool {
    match event {
        UiEvent::VoiceStateChanged {
            guild_id,
            user_id: changed_user,
            is_bot,
        } => {
            if changed_user == user_id {
                guilds.insert(guild_id);
                true
            } else {
                is_bot && (accept_all_bot_events || guilds.contains(&guild_id))
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashSet;

    use poise::serenity_prelude::{GuildId, UserId};

    use super::is_relevant;
    use crate::ui_events::UiEvent;

    #[test]
    fn filters_voice_events_to_the_user_and_shared_bot_guilds() {
        let guild = GuildId::new(1);
        let other_guild = GuildId::new(2);
        let user = UserId::new(10);
        let other_user = UserId::new(11);
        let mut guilds = HashSet::from([guild]);

        assert!(is_relevant(
            UiEvent::VoiceStateChanged {
                guild_id: guild,
                user_id: user,
                is_bot: false,
            },
            user,
            &mut guilds,
            false,
        ));
        assert!(is_relevant(
            UiEvent::VoiceStateChanged {
                guild_id: guild,
                user_id: other_user,
                is_bot: true,
            },
            user,
            &mut guilds,
            false,
        ));
        assert!(!is_relevant(
            UiEvent::VoiceStateChanged {
                guild_id: guild,
                user_id: other_user,
                is_bot: false,
            },
            user,
            &mut guilds,
            false,
        ));
        assert!(!is_relevant(
            UiEvent::VoiceStateChanged {
                guild_id: other_guild,
                user_id: other_user,
                is_bot: true,
            },
            user,
            &mut guilds,
            false,
        ));
        assert!(is_relevant(
            UiEvent::VoiceStateChanged {
                guild_id: other_guild,
                user_id: other_user,
                is_bot: true,
            },
            user,
            &mut guilds,
            true,
        ));
        assert!(is_relevant(
            UiEvent::VoiceStateChanged {
                guild_id: other_guild,
                user_id: user,
                is_bot: false,
            },
            user,
            &mut guilds,
            false,
        ));
        assert!(guilds.contains(&other_guild));
    }
}
