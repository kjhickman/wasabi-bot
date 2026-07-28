use topcoat::{
    Result,
    view::{Unescaped, component, view},
};

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
        document.querySelectorAll('[data-theme-option]').forEach((option) => {
            option.checked = option.value === theme;
        });
    };
    apply();

    document.addEventListener('DOMContentLoaded', () => {
        apply();
        document.querySelectorAll('[data-theme-option]').forEach((option) => {
            option.addEventListener('change', (event) => {
                if (!event.target.checked) return;
                theme = event.target.value;
                try { localStorage.setItem(key, theme); } catch {}
                apply();
            });
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
        <fieldset>
            <legend class="mb-2 px-2 text-xs font-medium text-muted-foreground">
                "Theme"
            </legend>
            <div class="grid grid-cols-3 overflow-hidden rounded-lg border border-border shadow-xs">
                theme_option(value: "system", label: "System")
                theme_option(value: "light", label: "Light")
                theme_option(value: "dark", label: "Dark")
            </div>
        </fieldset>
    }
}

#[component]
async fn theme_option(value: &str, label: &str) -> Result {
    view! {
        <label class="relative cursor-pointer border-l border-border first:border-l-0">
            <input
                type="radio"
                name="theme"
                value=(value)
                data-theme-option=""
                class="peer sr-only"
            >
            <span
                class="grid h-9 place-items-center px-2 text-xs font-medium text-muted-foreground transition-colors hover:bg-foreground/5 peer-checked:bg-primary peer-checked:text-primary-foreground peer-checked:hover:bg-primary/90 peer-focus-visible:ring-2 peer-focus-visible:ring-primary peer-focus-visible:ring-inset"
            >
                (label)
            </span>
        </label>
    }
}
