use topcoat::{
    Result,
    view::{Unescaped, attributes, component, view},
};

use super::components::select::select;

const THEME_SCRIPT: &str = r"(() => {
    const key = 'wasabi-theme';
    const root = document.documentElement;
    const media = matchMedia('(prefers-color-scheme: dark)');
    let theme = 'system';
    try { theme = localStorage.getItem(key) || 'system'; } catch {}
    if (!['system', 'light', 'dark'].includes(theme)) theme = 'system';

    const apply = () => {
        root.classList.toggle(
            'dark',
            theme === 'dark' || (theme === 'system' && media.matches),
        );
        root.dataset.theme = theme;
        const select = document.getElementById('theme-select');
        if (select) select.value = theme;
    };
    apply();

    document.addEventListener('DOMContentLoaded', () => {
        const select = document.getElementById('theme-select');
        apply();
        select?.addEventListener('change', (event) => {
            theme = event.target.value;
            try { localStorage.setItem(key, theme); } catch {}
            apply();
        });
    });

    media.addEventListener('change', () => {
        if (theme === 'system') apply();
    });
})();";

#[component]
pub async fn theme_script() -> Result {
    view! {
        <script>(Unescaped::new_unchecked(THEME_SCRIPT))</script>
    }
}

#[component]
pub async fn theme_selector() -> Result {
    view! {
        select(
            attrs: attributes! {
                id="theme-select"
                aria-label="Color theme"
                class="w-28"
            },
            <option value="system">"System"</option>
            <option value="light">"Light"</option>
            <option value="dark">"Dark"</option>
        )
    }
}
