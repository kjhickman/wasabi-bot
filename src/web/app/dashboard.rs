mod components;

use topcoat::{Result, router::page, view::view};

use components::{dashboard_header, now_playing_panel, queue_panel, search_panel};

topcoat::router::segment!(kind = Group);

#[page]
async fn dashboard() -> Result {
    view! {
        <div class="relative min-h-screen overflow-x-hidden">
            <div
                aria-hidden="true"
                class="pointer-events-none fixed -top-32 -right-32 size-96 rounded-full bg-primary/10 blur-3xl"
            ></div>
            dashboard_header()
            <main
                class="relative mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-10 lg:px-8"
            >
                <header
                    class="mb-6 flex flex-col gap-3 sm:mb-8 sm:flex-row sm:items-end sm:justify-between"
                >
                    <div>
                        <p
                            class="mb-2 text-xs font-semibold tracking-[0.2em] text-primary uppercase"
                        >
                            "Music dashboard"
                        </p>
                        <h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">
                            "The listening room"
                        </h1>
                    </div>
                    <p class="flex items-center gap-2 text-sm text-muted-foreground">
                        <span class="size-2 rounded-full bg-primary"></span>
                        "Interface preview · controls offline"
                    </p>
                </header>

                <div
                    class="grid items-start gap-6 xl:grid-cols-[minmax(0,1.3fr)_minmax(22rem,0.7fr)]"
                >
                    now_playing_panel()
                    <aside class="grid gap-6">
                        search_panel()
                        queue_panel()
                    </aside>
                </div>
            </main>
        </div>
    }
}
