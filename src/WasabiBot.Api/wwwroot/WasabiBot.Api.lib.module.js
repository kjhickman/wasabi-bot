import { initializeTheme, initializeThemeControls, syncTheme } from '/js/theme.js';
import { initializeTokenGenerator } from '/js/token-generator.js';

let initialized = false;

export function beforeWebStart() {
    initializeTheme();
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeThemeControls();
        initializeTokenGenerator();
        initialized = true;
    }

    syncTheme();
    blazor.addEventListener('enhancedload', function() {
        syncTheme();
    });
}
