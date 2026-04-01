const storageKey = 'wasabi-theme';
const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

function getStoredTheme() {
    try {
        return localStorage.getItem(storageKey) || 'system';
    } catch {
        return 'system';
    }
}

function resolveTheme(theme) {
    if (theme === 'light' || theme === 'dark') {
        return theme;
    }

    return mediaQuery.matches ? 'dark' : 'light';
}

export function applyResolvedTheme(theme) {
    const resolvedTheme = resolveTheme(theme);
    document.documentElement.setAttribute('data-theme', resolvedTheme);
    document.documentElement.style.colorScheme = resolvedTheme;
}

export function syncThemeControls(root = document) {
    const selectedTheme = getStoredTheme();
    root.querySelectorAll('[data-theme-select]').forEach(function(select) {
        select.value = selectedTheme;
    });
}

export function syncTheme(root = document) {
    applyResolvedTheme(getStoredTheme());
    syncThemeControls(root);
}

export function applyTheme(theme) {
    const selectedTheme = theme === 'light' || theme === 'dark' ? theme : 'system';

    try {
        localStorage.setItem(storageKey, selectedTheme);
    } catch {
    }

    applyResolvedTheme(selectedTheme);
}

export function initializeTheme() {
    syncTheme();

    if (typeof mediaQuery.addEventListener === 'function') {
        mediaQuery.addEventListener('change', handleThemeMediaChange);
    }
}

function handleThemeMediaChange() {
    if (getStoredTheme() === 'system') {
        applyResolvedTheme('system');
    }
}
