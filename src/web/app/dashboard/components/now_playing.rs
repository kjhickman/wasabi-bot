use topcoat::{
    Result,
    icon::{icon, iconify::iconify_icon},
    view::{attributes, component, view},
};

use crate::web::app::dashboard::models::PlaybackView;
use crate::web::components::{
    button::{ButtonSize, ButtonVariant, button},
    card::{card, card_content, card_header},
    progress::progress,
};

#[component]
pub async fn now_playing_panel(playback: &PlaybackView<'_>) -> Result {
    view! {
        card(
            attrs: attributes! {
                class = "bg-surface"
            },
            card_header(
                attrs: attributes! {
                    class = "flex-row items-center justify-between"
                },
                <div>
                    <p
                        class="text-xs font-semibold tracking-[0.18em] text-primary uppercase"
                    >
                        "Now playing"
                    </p>
                    <p class="mt-1 text-sm text-muted-foreground">
                        (format!(
                            "{} · {}",
                            playback.channel_name, playback.guild_name
                        ))
                    </p>
                </div>
                <span
                    class="flex items-center gap-2 rounded-full border border-border px-3 py-1 text-xs font-medium"
                >
                    <span class="size-1.5 rounded-full bg-primary"></span>
                    "Connected"
                </span>
            )
            card_content(
                attrs: attributes! {
                    class =
                    "grid gap-6 md:grid-cols-[minmax(15rem,0.9fr)_minmax(0,1.1fr)] md:items-center"
                },
                album_art(playback: playback)
                <div class="flex min-w-0 flex-col">
                    <p
                        class="mb-2 text-xs font-medium tracking-[0.14em] text-muted-foreground uppercase"
                    >
                        (playback.show)
                    </p>
                    <h2
                        class="truncate text-3xl font-semibold tracking-tight sm:text-4xl"
                    >
                        (playback.title)
                    </h2>
                    <p class="mt-2 text-lg text-muted-foreground">(playback.artist)</p>

                    <div class="mt-8">
                        progress(
                            value: playback.progress,
                            attrs: attributes! {
                                aria-label="Playback progress"
                                class="h-1.5"
                            }
                        )
                        <div
                            class="mt-2 flex justify-between font-mono text-xs text-muted-foreground"
                        >
                            <span>(playback.elapsed)</span>
                            <span>(playback.duration)</span>
                        </div>
                    </div>

                    <div class="mt-6 flex flex-wrap items-center justify-center gap-2 sm:gap-3">
                        button(
                            size: ButtonSize::Icon,
                            attrs: attributes! {
                                disabled=(true)
                                aria-label="Pause playback"
                                title="Playback controls are not connected"
                                class="size-12 rounded-full"
                            },
                            icon(
                                data: iconify_icon!("lucide:pause"),
                                attrs: attributes! { class="size-5" }
                            )
                        )
                        button(
                            variant: ButtonVariant::Secondary,
                            size: ButtonSize::Icon,
                            attrs: attributes! {
                                disabled=(true)
                                aria-label="Skip track"
                                title="Playback controls are not connected"
                            },
                            icon(
                                data: iconify_icon!("lucide:skip-forward"),
                                attrs: attributes! { class="size-4" }
                            )
                        )
                        button(
                            variant: ButtonVariant::Outline,
                            size: ButtonSize::Icon,
                            attrs: attributes! {
                                disabled=(true)
                                aria-label="Stop playback"
                                title="Playback controls are not connected"
                            },
                            icon(
                                data: iconify_icon!("lucide:square"),
                                attrs: attributes! { class="size-3.5" }
                            )
                        )
                    </div>

                    <div
                        class="mt-7 flex items-center gap-3 border-t border-border pt-5 text-sm text-muted-foreground"
                    >
                        icon(
                            data: iconify_icon!("lucide:volume-2"),
                            attrs: attributes! {
                                class = "size-4"
                            }
                        )
                        <div
                            class="h-1 flex-1 overflow-hidden rounded-full bg-foreground/10"
                        >
                            <div class="h-full w-2/3 rounded-full bg-foreground/40"></div>
                        </div>
                        <span class="font-mono text-xs">(playback.volume)</span>
                    </div>
                </div>
            )
        )
    }
}

#[component]
async fn album_art(playback: &PlaybackView<'_>) -> Result {
    view! {
        <div
            aria-label=(playback.artwork_label)
            role="img"
            class="relative aspect-square overflow-hidden rounded-xl border border-primary/20 bg-primary shadow-sm"
        >
            <div
                class="absolute inset-[9%] rounded-full border-[18px] border-primary-foreground/10"
            ></div>
            <div
                class="absolute inset-[24%] rounded-full border border-primary-foreground/25"
            ></div>
            <div
                class="absolute inset-[38%] rounded-full bg-primary-foreground/85 shadow-sm"
            ></div>
            <div
                class="absolute top-[8%] right-[8%] text-right text-[0.65rem] font-semibold tracking-[0.2em] text-primary-foreground/70 uppercase"
            >
                (playback.artist)
                <br>
                (playback.artwork_code)
            </div>
            <p
                class="absolute bottom-[8%] left-[8%] max-w-[8rem] text-2xl leading-[0.9] font-bold tracking-tight text-primary-foreground uppercase sm:text-3xl"
            >
                (playback.title)
            </p>
        </div>
    }
}
