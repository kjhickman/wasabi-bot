mod components;

use poise::serenity_prelude as serenity;
use topcoat::{
    Result,
    context::{Cx, app_context},
    router::page,
    view::{component, view},
};

use crate::web::{
    BotState, State,
    auth::{self, DiscordGuild},
};
use components::{dashboard_header, now_playing_panel, queue_panel, search_panel};

topcoat::router::segment!(kind = Group);

#[page]
#[tracing::instrument(name = "web.dashboard", skip(cx))]
async fn dashboard(cx: &Cx) -> Result {
    let state = app_context::<State>(cx);
    let session = auth::current_session(cx).await?;
    let bot = state.bot.read().await.clone();
    let access = match &session {
        Some(session) if bot.is_some() => {
            resolve_access(bot.as_ref().unwrap(), session.user_id, &session.guilds)
        }
        Some(_) => Access::Unavailable,
        None => Access::LoggedOut,
    };
    let content = match &access {
        Access::LoggedOut => view! {
            status_panel(
                title: "Your music, one click away",
                description: "Log in with Discord to find a server you share with Wasabi Bot.",
                link: Some(("Log in with Discord", "/auth/discord")),
                join_csrf: None,
            )
        }?,
        Access::NoSharedGuild => view! {
            status_panel(
                title: "No shared server yet",
                description: "Wasabi Bot must be a member of one of your Discord servers before this dashboard can connect.",
                link: None,
                join_csrf: None,
            )
        }?,
        Access::NotInVoice => view! {
            status_panel(
                title: "Join a voice channel",
                description: "Connect to a voice channel in a server you share with Wasabi Bot, then refresh this page.",
                link: Some(("Refresh", "/")),
                join_csrf: None,
            )
        }?,
        Access::Unavailable => view! {
            status_panel(
                title: "Wasabi Bot is connecting",
                description: "The dashboard is online, but Discord is not ready yet. Try again in a moment.",
                link: Some(("Refresh", "/")),
                join_csrf: None,
            )
        }?,
        Access::Joinable(channel) => view! {
            status_panel(
                title: "Ready to join",
                description: format!("You're in {} / {}. Bring Wasabi Bot into the channel to continue.", channel.channel_name, channel.guild_name),
                link: None,
                join_csrf: session.as_ref().map(|session| session.csrf_token.as_str()),
            )
        }?,
        Access::Conflict(channel) => view! {
            status_panel(
                title: "Wasabi Bot is busy",
                description: format!("Wasabi Bot is already connected to {} in {}. Join that channel or disconnect it first.", channel.bot_channel_name.as_deref().unwrap_or("another voice channel"), channel.guild_name),
                link: Some(("Refresh", "/")),
                join_csrf: None,
            )
        }?,
        Access::Ready(channel) => view! {
            <div class="grid items-start gap-6 xl:grid-cols-[minmax(0,1.3fr)_minmax(22rem,0.7fr)]">
                <section class="grid gap-6">
                    now_playing_panel(
                        guild_name: &channel.guild_name,
                        channel_name: &channel.channel_name,
                    )
                    queue_panel()
                </section>
                <aside>
                    search_panel()
                </aside>
            </div>
        }?,
    };

    view! {
        <div class="relative min-h-screen overflow-x-hidden">
            <div
                aria-hidden="true"
                class="pointer-events-none fixed -top-32 -right-32 size-96 rounded-full bg-primary/10 blur-3xl"
            ></div>
            dashboard_header(session: session.as_ref())
            <main
                class="relative mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-10 lg:px-8"
            >
                (content)
            </main>
        </div>
    }
}

#[derive(Clone, Debug)]
struct ChannelAccess {
    guild_id: serenity::GuildId,
    guild_name: String,
    channel_id: serenity::ChannelId,
    channel_name: String,
    bot_channel_name: Option<String>,
}

enum Access {
    LoggedOut,
    NoSharedGuild,
    NotInVoice,
    Unavailable,
    Joinable(ChannelAccess),
    Conflict(ChannelAccess),
    Ready(ChannelAccess),
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) struct JoinTarget {
    pub(super) guild_id: serenity::GuildId,
    pub(super) channel_id: serenity::ChannelId,
}

pub(super) fn join_target(
    bot: &BotState,
    user_id: u64,
    guilds: &[DiscordGuild],
) -> Option<JoinTarget> {
    match resolve_access(bot, user_id, guilds) {
        Access::Joinable(channel) => Some(JoinTarget {
            guild_id: channel.guild_id,
            channel_id: channel.channel_id,
        }),
        _ => None,
    }
}

pub(super) fn ready_target(
    bot: &BotState,
    user_id: u64,
    guilds: &[DiscordGuild],
) -> Option<JoinTarget> {
    match resolve_access(bot, user_id, guilds) {
        Access::Ready(channel) => Some(JoinTarget {
            guild_id: channel.guild_id,
            channel_id: channel.channel_id,
        }),
        _ => None,
    }
}

fn resolve_access(bot: &BotState, user_id: u64, guilds: &[DiscordGuild]) -> Access {
    let user_id = serenity::UserId::new(user_id);
    let bot_id = bot.serenity.cache.current_user().id;
    let mut shares_guild = false;

    for oauth_guild in guilds {
        let Ok(guild_id) = oauth_guild.id.parse::<u64>() else {
            continue;
        };
        let guild_id = serenity::GuildId::new(guild_id);
        let Some(guild) = bot.serenity.cache.guild(guild_id) else {
            continue;
        };
        shares_guild = true;
        let Some(channel_id) = guild
            .voice_states
            .get(&user_id)
            .and_then(|voice| voice.channel_id)
        else {
            continue;
        };
        let bot_channel_id = guild
            .voice_states
            .get(&bot_id)
            .and_then(|voice| voice.channel_id);
        let channel_name = guild
            .channels
            .get(&channel_id)
            .map(|channel| channel.name.clone())
            .unwrap_or_else(|| "Voice channel".to_owned());
        let bot_channel_name = bot_channel_id
            .and_then(|id| guild.channels.get(&id).map(|channel| channel.name.clone()));
        let channel = ChannelAccess {
            guild_id,
            guild_name: oauth_guild.name.clone(),
            channel_id,
            channel_name,
            bot_channel_name,
        };
        return match voice_decision(Some(channel_id), bot_channel_id) {
            VoiceDecision::Joinable => Access::Joinable(channel),
            VoiceDecision::Ready => Access::Ready(channel),
            VoiceDecision::Conflict => Access::Conflict(channel),
            VoiceDecision::NotInVoice => unreachable!(),
        };
    }

    if shares_guild {
        Access::NotInVoice
    } else {
        Access::NoSharedGuild
    }
}

#[derive(Debug, PartialEq, Eq)]
enum VoiceDecision {
    NotInVoice,
    Joinable,
    Conflict,
    Ready,
}

fn voice_decision(
    user_channel: Option<serenity::ChannelId>,
    bot_channel: Option<serenity::ChannelId>,
) -> VoiceDecision {
    match (user_channel, bot_channel) {
        (None, _) => VoiceDecision::NotInVoice,
        (Some(_), None) => VoiceDecision::Joinable,
        (Some(user), Some(bot)) if user == bot => VoiceDecision::Ready,
        (Some(_), Some(_)) => VoiceDecision::Conflict,
    }
}

#[component]
async fn status_panel(
    title: &str,
    #[into] description: String,
    link: Option<(&str, &str)>,
    join_csrf: Option<&str>,
) -> Result<topcoat::view::View> {
    let action = match (link, join_csrf) {
        (Some((label, href)), _) => view! {
            <a
                href=(href)
                class="mt-6 inline-flex h-10 items-center rounded-lg bg-primary px-5 text-sm font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-primary/30"
            >
                (label)
            </a>
        }?,
        (_, Some(csrf)) => view! {
            <form method="post" action="/voice/join" class="mt-6">
                <input type="hidden" name="csrf" value=(csrf)>
                <button
                    type="submit"
                    class="inline-flex h-10 cursor-pointer items-center rounded-lg bg-primary px-5 text-sm font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-primary/30"
                >
                    "Join voice channel"
                </button>
            </form>
        }?,
        _ => view! {}?,
    };

    view! {
        <section class="mx-auto max-w-2xl rounded-2xl border border-border bg-surface px-6 py-12 text-center shadow-sm sm:px-10 sm:py-16">
            <img
                src=(crate::web::FAVICON_URL)
                alt="Wasabi Bot"
                class="mx-auto mb-5 size-12 rounded-full border border-border object-cover shadow-xs"
                width="48"
                height="48"
            >
            <h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">(title)</h1>
            <p class="mx-auto mt-4 max-w-lg text-base leading-7 text-muted-foreground">
                (description)
            </p>
            (action)
        </section>
    }
}

#[cfg(test)]
mod tests {
    use super::{VoiceDecision, voice_decision};
    use poise::serenity_prelude::ChannelId;

    #[test]
    fn voice_access_requires_the_same_channel() {
        let lounge = ChannelId::new(1);
        let kitchen = ChannelId::new(2);

        assert_eq!(voice_decision(None, None), VoiceDecision::NotInVoice);
        assert_eq!(voice_decision(Some(lounge), None), VoiceDecision::Joinable);
        assert_eq!(
            voice_decision(Some(lounge), Some(kitchen)),
            VoiceDecision::Conflict
        );
        assert_eq!(
            voice_decision(Some(lounge), Some(lounge)),
            VoiceDecision::Ready
        );
    }
}
