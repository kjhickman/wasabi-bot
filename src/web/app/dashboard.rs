mod components;
mod models;

use topcoat::{
    Result,
    context::{Cx, app_context},
    router::page,
    view::{Unescaped, component, view},
};

use super::voice::access::{Access, resolve_access};
use crate::web::{BotState, State, auth};
use components::{dashboard_header, now_playing_panel, queue_panel, search_panel};
use models::DashboardView;

topcoat::router::segment!(kind = Group);

const VOICE_UPDATES_SCRIPT: &str = r"(() => {
    const events = new EventSource('/events');
    let timer;
    let refreshing = false;
    let pending = false;

    const refresh = async () => {
        if (refreshing) {
            pending = true;
            return;
        }
        refreshing = true;
        do {
            pending = false;
            try {
                const response = await fetch('/content', { cache: 'no-store' });
                if (response.status === 401 || response.status === 403) {
                    events.close();
                    location.reload();
                    return;
                }
                if (!response.ok) break;
                const template = document.createElement('template');
                template.innerHTML = await response.text();
                const current = document.getElementById('dashboard-content');
                const next = template.content.querySelector('#dashboard-content');
                if (current && next) current.replaceWith(next);
            } catch {
                break;
            }
        } while (pending);
        refreshing = false;
    };

    events.addEventListener('voice', () => {
        clearTimeout(timer);
        timer = setTimeout(refresh, 100);
    });
    events.addEventListener('error', () => {
        clearTimeout(timer);
        timer = setTimeout(refresh, 100);
    });
})();";

#[page]
#[tracing::instrument(name = "web.dashboard", skip(cx))]
async fn dashboard(cx: &Cx) -> Result {
    let (session, bot) = dashboard_state(cx).await?;
    let updates = if session.is_some() {
        view! { <script>(Unescaped::new_unchecked(VOICE_UPDATES_SCRIPT))</script> }?
    } else {
        view! {}?
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
                <div id="dashboard-content">
                    dashboard_content(session: session.as_ref(), bot: bot.as_ref())
                </div>
            </main>
            (updates)
        </div>
    }
}

pub(super) async fn dashboard_state(cx: &Cx) -> Result<(Option<auth::Session>, Option<BotState>)> {
    let state = app_context::<State>(cx);
    let session = auth::current_session(cx).await?;
    let bot = state.bot.read().await.clone();
    Ok((session, bot))
}

#[component]
pub(super) async fn dashboard_content(
    session: Option<&auth::Session>,
    bot: Option<&BotState>,
) -> Result {
    let access = match session {
        Some(session) if bot.is_some() => {
            resolve_access(bot.unwrap(), session.user_id, &session.guilds)
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
                description: "Connect to a voice channel in a server you share with Wasabi Bot. This page will update automatically.",
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
                join_csrf: session.map(|session| session.csrf_token.as_str()),
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
        Access::Ready(channel) => {
            let dashboard_view = DashboardView::fixture(&channel.guild_name, &channel.channel_name);
            view! {
                <div class="grid items-start gap-6 xl:grid-cols-[minmax(0,1.3fr)_minmax(22rem,0.7fr)]">
                    <section class="grid gap-6">
                        now_playing_panel(playback: &dashboard_view.playback)
                        queue_panel(
                            summary: dashboard_view.queue_summary,
                            items: &dashboard_view.queue,
                        )
                    </section>
                    <aside>
                        search_panel(recommendations: &dashboard_view.recommendations)
                    </aside>
                </div>
            }?
        }
    };

    Ok(content)
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
