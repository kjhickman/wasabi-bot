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
                <div
                    class="grid items-start gap-6 xl:grid-cols-[minmax(0,1.3fr)_minmax(22rem,0.7fr)]"
                >
                    <section class="grid gap-6">
                        now_playing_panel()
                        queue_panel()
                    </section>
                    <aside>
                        search_panel()
                    </aside>
                </div>
            </main>
        </div>
    }
}
