import { initializeTheme, initializeThemeControls, syncTheme } from '/js/theme.js';
import { initializeCredentialsPage } from '/js/credentials-page.js';

let initialized = false;

export function beforeWebStart() {
    initializeTheme();
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeThemeControls();
        initialized = true;
    }

    initializeCredentialsPage();
    syncTheme();
    blazor.addEventListener('enhancedload', function() {
        initializeCredentialsPage();
        syncTheme();
    });
}
