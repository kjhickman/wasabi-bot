import { initializeTheme, initializeThemeControls, syncTheme } from '/js/theme.js';

let initialized = false;

export function beforeWebStart() {
    initializeTheme();
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeThemeControls();
        initialized = true;
    }

    syncTheme();
    blazor.addEventListener('enhancedload', function() {
        syncTheme();
    });
}
