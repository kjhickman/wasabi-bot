use topcoat::{
    Result,
    icon::{icon, iconify::iconify_icon},
    view::{attributes, component, view},
};

use crate::web::app::dashboard::models::SearchResultView;
use crate::web::components::{
    button::{ButtonSize, ButtonVariant, button},
    card::{card, card_content, card_description, card_header, card_title},
    input::input,
};

#[component]
pub async fn search_panel(recommendations: &[SearchResultView<'_>]) -> Result {
    view! {
        card(
            attrs: attributes! {
                class = "bg-surface"
            },
            card_header(
                card_title("Find something next")
                card_description(
                    "Search will use Lavalink when the dashboard is connected."
                )
            )
            card_content(
                <div class="flex gap-2" role="search">
                    <div class="relative min-w-0 flex-1">
                        icon(
                            data: iconify_icon!("lucide:search"),
                            attrs: attributes! {
                                class =
                                "pointer-events-none absolute top-1/2 left-3 z-10 size-4 -translate-y-1/2 text-muted-foreground"
                            }
                        )
                        input(
                            attrs: attributes! {
                                disabled=(true)
                                type="search"
                                aria-label="Search songs"
                                placeholder="Song, artist, or URL"
                                class="pl-9"
                            }
                        )
                    </div>
                    button(
                        attrs: attributes! {
                            disabled=(true)
                            title="Search is not connected"
                        },
                        "Search"
                    )
                </div>

                <div class="mt-6">
                    <p
                        class="mb-2 text-xs font-semibold tracking-[0.16em] text-muted-foreground uppercase"
                    >
                        "Quick picks"
                    </p>
                    <ul class="divide-y divide-border">
                        for result in recommendations {
                            recommendation(result: result)
                        }
                    </ul>
                </div>
            )
        )
    }
}

#[component]
async fn recommendation(result: &SearchResultView<'_>) -> Result {
    view! {
        <li class="flex items-center gap-3 py-3 first:pt-1 last:pb-0">
            <span
                aria-hidden="true"
                class="grid size-9 shrink-0 place-items-center rounded-md bg-foreground/5 text-muted-foreground"
            >
                icon(
                    data: iconify_icon!("lucide:music"),
                    attrs: attributes! {
                        class = "size-4"
                    }
                )
            </span>
            <span class="min-w-0 flex-1">
                <strong class="block truncate text-sm font-medium">(result.title)</strong>
                <span class="block truncate text-xs text-muted-foreground">
                    (result.artist)
                </span>
            </span>
            <span class="font-mono text-xs text-muted-foreground">(result.duration)</span>
            button(
                variant: ButtonVariant::Ghost,
                size: ButtonSize::Icon,
                attrs: attributes! {
                    disabled=(true)
                    aria-label=(format!("Add {} to queue", result.title))
                    title="Queue controls are not connected"
                },
                icon(
                    data: iconify_icon!("lucide:plus"),
                    attrs: attributes! {
                        class = "size-4"
                    }
                )
            )
        </li>
    }
}
