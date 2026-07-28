use topcoat::{
    Result,
    icon::{icon, iconify::iconify_icon},
    view::{attributes, component, view},
};

use crate::web::{
    components::{
        button::{ButtonVariant, button},
        select::select,
    },
    theme::theme_toggle,
};

#[component]
pub async fn dashboard_header() -> Result {
    view! {
        <header
            class="relative z-10 border-b border-border bg-background/85 backdrop-blur-xl"
        >
            <div
                class="mx-auto flex min-h-16 w-full max-w-7xl flex-wrap items-center gap-3 px-4 py-3 sm:flex-nowrap sm:px-6 lg:px-8"
            >
                <a
                    href="/"
                    class="flex shrink-0 items-center gap-3"
                    aria-label="Wasabi Music home"
                >
                    <span
                        aria-hidden="true"
                        class="grid size-9 place-items-center rounded-full bg-primary text-sm font-bold text-primary-foreground shadow-xs"
                    >
                        "W"
                    </span>
                    <span class="hidden leading-none sm:block">
                        <strong class="block text-sm tracking-[0.16em] uppercase">
                            "Wasabi"
                        </strong>
                        <span
                            class="text-[0.65rem] tracking-[0.25em] text-muted-foreground uppercase"
                        >
                            "Music"
                        </span>
                    </span>
                </a>

                <div class="order-3 w-full sm:order-none sm:ml-auto sm:w-52">
                    select(
                        attrs: attributes! {
                            disabled=(true)
                            aria-label="Discord server"
                            title="Server selection is not connected"
                        },
                        <option>"Wasabi Test Server"</option>
                    )
                </div>

                <div class="ml-auto flex items-center gap-1 sm:ml-0 sm:gap-2">
                    theme_toggle()
                    button(
                        variant: ButtonVariant::Outline,
                        attrs: attributes! {
                            disabled=(true)
                            title="Discord sign-in is coming soon"
                        },
                        icon(
                            data: iconify_icon!("lucide:log-in"),
                            attrs: attributes! { class="size-4" }
                        )
                        <span class="hidden sm:inline">"Sign in with Discord"</span>
                        <span class="sr-only sm:hidden">"Sign in with Discord"</span>
                    )
                </div>
            </div>
        </header>
    }
}
