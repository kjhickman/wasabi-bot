use topcoat::{
    Result,
    view::{component, view},
};

use crate::web::theme::theme_selector;

#[component]
pub async fn dashboard_header() -> Result {
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
                    <span
                        role="img"
                        aria-label="Discord user avatar"
                        title="Discord profile"
                        class="grid size-9 shrink-0 place-items-center rounded-full border border-border bg-foreground text-xs font-semibold text-background shadow-xs"
                    >
                        "K"
                    </span>
                </div>
            </div>
        </header>
    }
}
