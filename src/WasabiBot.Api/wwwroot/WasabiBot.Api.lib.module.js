import { initializeTheme, syncTheme } from '/js/theme.js';
import { initializeAccountMenu } from '/js/account-menu.js';
import { initializeTokenGenerator } from '/js/token-generator.js';

let initialized = false;

export function beforeWebStart() {
    initializeTheme();
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeAccountMenu();
        initializeTokenGenerator();
        initialized = true;
    }

    syncTheme();
    blazor.addEventListener('enhancedload', function() {
        syncTheme();
    });
}
