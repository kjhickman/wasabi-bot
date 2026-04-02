import { initializeTheme, initializeThemeControls, syncTheme } from '/js/theme.js';

let initialized = false;

function replaceUrlIfNeeded(root = document) {
    const replacementUrl = root.querySelector('[data-replace-url]')?.getAttribute('data-replace-url');
    if (!replacementUrl) {
        return;
    }

    const currentUrl = window.location.pathname + window.location.search + window.location.hash;
    if (currentUrl !== replacementUrl) {
        window.history.replaceState(window.history.state, '', replacementUrl);
    }
}

export function beforeWebStart() {
    initializeTheme();
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeThemeControls();
        initialized = true;
    }

    syncTheme();
    replaceUrlIfNeeded();

    blazor.addEventListener('enhancedload', function() {
        syncTheme();
        replaceUrlIfNeeded();
    });
}
