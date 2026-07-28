use topcoat::{
    Result,
    icon::{icon, iconify::iconify_icon},
    view::{attributes, component, view},
};

use super::components::button::{ButtonSize, ButtonVariant, button};

#[component]
pub async fn theme_script() -> Result {
    view! {
        <script>
            "(() => {
                const key = 'wasabi-theme';
                const root = document.documentElement;
                const media = matchMedia('(prefers-color-scheme: dark)');
                let saved = null;
                try { saved = localStorage.getItem(key); } catch {}
                root.classList.toggle(
                    'dark',
                    saved ? saved === 'dark' : media.matches,
                );

                const syncButton = () => {
                    const button = document.getElementById('theme-toggle');
                    if (!button) return;
                    const dark = root.classList.contains('dark');
                    button.setAttribute('aria-pressed', String(dark));
                    button.setAttribute('aria-label', dark ? 'Use light theme' : 'Use dark theme');
                    button.title = dark ? 'Use light theme' : 'Use dark theme';
                };

                document.addEventListener('DOMContentLoaded', () => {
                    const button = document.getElementById('theme-toggle');
                    syncButton();
                    button?.addEventListener('click', () => {
                        const dark = !root.classList.contains('dark');
                        root.classList.toggle('dark', dark);
                        saved = dark ? 'dark' : 'light';
                        try { localStorage.setItem(key, dark ? 'dark' : 'light'); } catch {}
                        syncButton();
                    });
                });

                media.addEventListener('change', (event) => {
                    if (saved) return;
                    root.classList.toggle('dark', event.matches);
                    syncButton();
                });
            })();"
        </script>
    }
}

#[component]
pub async fn theme_toggle() -> Result {
    view! {
        button(
            variant: ButtonVariant::Ghost,
            size: ButtonSize::Icon,
            attrs: attributes! {
                id="theme-toggle"
                type="button"
                aria-label="Use dark theme"
                aria-pressed="false"
                title="Use dark theme"
            },
            icon(
                data: iconify_icon!("lucide:sun"),
                attrs: attributes! { class="size-4 dark:hidden" }
            )
            icon(
                data: iconify_icon!("lucide:moon"),
                attrs: attributes! { class="hidden size-4 dark:block" }
            )
        )
    }
}
