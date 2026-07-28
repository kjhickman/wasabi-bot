use topcoat::{
    Result,
    icon::{icon, iconify::iconify_icon},
    view::{Unescaped, attributes, component, view},
};

use crate::web::auth::Session;
use crate::web::theme::theme_selector;

const ACCOUNT_MENU_SCRIPT: &str = r"(() => {
    const menu = document.querySelector('[data-account-menu]');
    document.addEventListener('click', (event) => {
        if (menu?.open && !menu.contains(event.target)) menu.removeAttribute('open');
    });
})();";

#[component]
pub async fn dashboard_header(session: Option<&Session>) -> Result {
    let avatar = match session {
        Some(session) => view! {
            <img
                src=(session.avatar_url())
                alt="Discord user avatar"
                class="size-9 rounded-full border border-border bg-foreground/5 object-cover shadow-xs"
                width="36"
                height="36"
            >
        }?,
        None => view! {
            <span
                aria-hidden="true"
                class="grid size-9 place-items-center rounded-full border border-border bg-foreground/5 text-muted-foreground shadow-xs"
            >
                icon(
                    data: iconify_icon!("lucide:user"),
                    attrs: attributes! { class="size-4" }
                )
            </span>
        }?,
    };

    let account_details = match session {
        Some(session) => view! {
            <div class="border-b border-border px-2 pb-3">
                <p class="truncate text-sm font-semibold">(session.display_name())</p>
                <p class="truncate text-xs text-muted-foreground">
                    (format!("@{}", session.username))
                </p>
            </div>
            <div class="py-3">theme_selector()</div>
            <form method="post" action="/auth/logout" class="border-t border-border pt-2">
                <input type="hidden" name="csrf" value=(&session.csrf_token)>
                <button
                    type="submit"
                    class="w-full cursor-pointer rounded-lg px-2 py-2 text-left text-sm font-medium hover:bg-foreground/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                >
                    "Log out"
                </button>
            </form>
        }?,
        None => view! {
            <div class="py-1">theme_selector()</div>
            <div class="mt-3 border-t border-border pt-3">
                <a
                    href="/auth/discord"
                    class="inline-flex h-9 w-full items-center justify-center rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground shadow-xs hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-primary/30"
                >
                    "Log in with Discord"
                </a>
            </div>
        }?,
    };

    let account_label = session
        .map(|session| format!("Open account menu for {}", session.display_name()))
        .unwrap_or_else(|| "Open settings menu".to_owned());

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
                    <img
                        src=(crate::web::FAVICON_URL)
                        alt=""
                        class="size-9 rounded-full border border-border object-cover shadow-xs"
                        width="36"
                        height="36"
                    >
                    <strong class="text-sm font-semibold tracking-tight sm:text-base">
                        "Wasabi Bot"
                    </strong>
                </a>

                <div class="ml-auto flex items-center gap-2">
                    <details data-account-menu="" class="group relative">
                        <summary
                            class="list-none cursor-pointer rounded-full outline-none ring-primary/30 focus-visible:ring-3"
                            aria-label=(account_label)
                        >
                            (avatar)
                        </summary>
                        <div
                            class="absolute right-0 mt-2 w-64 rounded-xl border border-border bg-background p-2 shadow-lg"
                        >
                            (account_details)
                        </div>
                    </details>
                    <script>(Unescaped::new_unchecked(ACCOUNT_MENU_SCRIPT))</script>
                </div>
            </div>
        </header>
    }
}
