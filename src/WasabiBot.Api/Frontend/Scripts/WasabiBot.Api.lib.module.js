import { initializeCopyButtons } from './clipboard.js';
import { replaceUrlIfNeeded } from './navigation.js';
import { initializeThemePreference, syncThemePreferenceUi } from './theme.js';

let initialized = false;

export function beforeWebStart() {
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeCopyButtons();
        initializeThemePreference();
        initialized = true;
    }

    replaceUrlIfNeeded();
    syncThemePreferenceUi();

    blazor.addEventListener('enhancedload', function() {
        replaceUrlIfNeeded();
        syncThemePreferenceUi();
    });
}
