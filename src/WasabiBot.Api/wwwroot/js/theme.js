const themeApi = window.WasabiTheme;
const mediaQuery = themeApi.mediaQuery;

export function applyResolvedTheme(selectedTheme) {
    themeApi.applyResolvedTheme(selectedTheme);
}

export function syncThemeControls(root = document) {
    const selectedTheme = themeApi.getStoredTheme();
    root.querySelectorAll('[data-theme-select]').forEach(function(select) {
        select.value = selectedTheme;
    });
}

export function syncTheme(root = document) {
    themeApi.syncDocumentTheme();
    syncThemeControls(root);
}

export function initializeThemeControls() {
    document.addEventListener('change', handleThemeControlChange);
    document.addEventListener('beforetoggle', handleAccountMenuBeforeToggle);
}

export function applyTheme(selectedTheme) {
    const normalizedTheme = themeApi.setStoredTheme(selectedTheme);
    themeApi.applyResolvedTheme(normalizedTheme);
}

export function initializeTheme() {
    syncTheme();

    if (typeof mediaQuery.addEventListener === 'function') {
        mediaQuery.addEventListener('change', handleThemeMediaChange);
    }
}

function handleThemeMediaChange() {
    if (themeApi.getStoredTheme() === 'system') {
        themeApi.applyResolvedTheme('system');
    }
}

function handleThemeControlChange(event) {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement) || !target.matches('[data-theme-select]')) {
        return;
    }

    applyTheme(target.value);
    syncThemeControls();
}

function handleAccountMenuBeforeToggle(event) {
    const target = event.target;
    if (!(target instanceof HTMLElement) || !target.matches('[data-account-menu-panel]')) {
        return;
    }

    const button = document.querySelector('[data-account-menu-toggle][aria-controls="' + target.id + '"]');
    if (!(button instanceof HTMLElement)) {
        return;
    }

    const isOpening = event.newState === 'open';
    button.setAttribute('aria-expanded', isOpening ? 'true' : 'false');

    if (isOpening) {
        syncThemeControls(target);
    }
}
