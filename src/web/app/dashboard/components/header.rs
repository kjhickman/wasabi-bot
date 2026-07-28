use topcoat::{
    Result,
    view::{component, view},
};

use crate::web::auth::Session;
use crate::web::theme::theme_selector;

#[component]
pub async fn dashboard_header(session: Option<&Session>) -> Result {
    let account = match session {
        Some(session) => view! {
            <details class="group relative">
                <summary
                    class="list-none cursor-pointer rounded-full outline-none ring-primary/30 focus-visible:ring-3"
                    aria-label=(format!("Open account menu for {}", session.display_name()))
                >
                    <img
                        src=(session.avatar_url())
                        alt="Discord user avatar"
                        class="size-9 rounded-full border border-border bg-muted object-cover shadow-xs"
                        width="36"
                        height="36"
                    >
                </summary>
                <div
                    class="absolute right-0 mt-2 w-56 rounded-xl border border-border bg-background p-2 shadow-lg"
                >
                    <p class="truncate px-2 py-1 text-sm font-semibold">
                        (session.display_name())
                    </p>
                    <p class="truncate px-2 pb-2 text-xs text-muted-foreground">
                        (format!("@{}", session.username))
                    </p>
                    <form method="post" action="/auth/logout">
                        <input type="hidden" name="csrf" value=(&session.csrf_token)>
                        <button
                            type="submit"
                            class="w-full rounded-lg px-2 py-2 text-left text-sm font-medium hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                        >
                            "Log out"
                        </button>
                    </form>
                </div>
            </details>
        }?,
        None => view! {
            <a
                href="/auth/discord"
                class="inline-flex h-9 items-center rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-primary/30"
            >
                "Log in with Discord"
            </a>
        }?,
    };

    view! {
        <header
            class="relative z-10 border-b border-border bg-background/85 backdrop-blur-xl"
        >
            <div
                class="mx-auto flex min-h-16 w-full max-w-7xl items-center gap-2 px-4 py-3 sm:gap-3 sm:px-6 lg:px-8"
            >
                <a
                    href="/"
                    class="flex shrink-0 items-center gap-3"
                    aria-label="Wasabi Bot home"
                >
                    <span
                        aria-hidden="true"
                        class="grid size-9 place-items-center rounded-full bg-primary text-sm font-bold text-primary-foreground shadow-xs"
                    >
                        "W"
                    </span>
                    <strong class="text-sm font-semibold tracking-tight sm:text-base">
                        "Wasabi Bot"
                    </strong>
                </a>

                <div class="ml-auto flex items-center gap-2">
                    theme_selector()
                    (account)
                </div>
            </div>
        </header>
    }
}
