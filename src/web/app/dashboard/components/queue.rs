use topcoat::{
    Result,
    icon::{icon, iconify::iconify_icon},
    view::{attributes, component, view},
};

use crate::web::components::{
    button::{ButtonSize, ButtonVariant, button},
    card::{card, card_content, card_description, card_header, card_title},
};

#[component]
pub async fn queue_panel() -> Result {
    view! {
        card(
            attrs: attributes! {
                class = "bg-surface"
            },
            card_header(
                attrs: attributes! {
                    class = "flex-row items-start justify-between"
                },
                <div>
                    card_title("Up next")
                    card_description("4 tracks · 14 minutes")
                </div>
                button(
                    variant: ButtonVariant::Ghost,
                    size: ButtonSize::Icon,
                    attrs: attributes! {
                        disabled=(true)
                        aria-label="Clear queue"
                        title="Queue controls are not connected"
                    },
                    icon(
                        data: iconify_icon!("lucide:trash-2"),
                        attrs: attributes! {
                            class = "size-4"
                        }
                    )
                )
            )
            card_content(
                <ol class="space-y-1">
                    queue_item(
                        position: "01",
                        title: "Chrome Garden",
                        artist: "Signal Path",
                        duration: "3:44",
                        up_next: true
                    )
                    queue_item(
                        position: "02",
                        title: "Low Battery",
                        artist: "Night Shift",
                        duration: "2:51",
                        up_next: false
                    )
                    queue_item(
                        position: "03",
                        title: "Window Seat",
                        artist: "City Sleep",
                        duration: "4:18",
                        up_next: false
                    )
                    queue_item(
                        position: "04",
                        title: "Neon Receipt",
                        artist: "Corner Store",
                        duration: "3:12",
                        up_next: false
                    )
                </ol>
            )
        )
    }
}

#[component]
async fn queue_item(
    position: &str,
    title: &str,
    artist: &str,
    duration: &str,
    up_next: bool,
) -> Result {
    let row_class = if up_next {
        "border-primary/25 bg-primary/10"
    } else {
        "border-transparent"
    };

    view! {
        <li
            class=(format!(
                "flex items-center gap-3 rounded-lg border p-2.5 {row_class}"
            ))
        >
            <span
                class="w-5 shrink-0 text-center font-mono text-[0.65rem] text-muted-foreground"
            >
                (position)
            </span>
            <span class="min-w-0 flex-1">
                <span class="flex items-center gap-2">
                    <strong class="truncate text-sm font-medium">(title)</strong>
                    if up_next {
                        <span
                            class="rounded-full bg-primary px-1.5 py-0.5 text-[0.6rem] font-bold tracking-wide text-primary-foreground uppercase"
                        >
                            "Next"
                        </span>
                    }
                </span>
                <span class="block truncate text-xs text-muted-foreground">
                    (artist)
                </span>
            </span>
            <span class="font-mono text-xs text-muted-foreground">(duration)</span>
            button(
                variant: ButtonVariant::Ghost,
                size: ButtonSize::Icon,
                attrs: attributes! {
                    disabled=(true)
                    aria-label=(format!("Remove {title} from queue"))
                    title="Queue controls are not connected"
                    class="size-8"
                },
                icon(
                    data: iconify_icon!("lucide:x"),
                    attrs: attributes! {
                        class = "size-3.5"
                    }
                )
            )
        </li>
    }
}
