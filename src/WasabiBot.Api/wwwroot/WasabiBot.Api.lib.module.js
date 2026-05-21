import { initializeCopyButtons } from './clipboard.js';
import { initializeDropdownCloseButtons, replaceCurrentUrlIfRequested } from './navigation.js';
import { initializeThemePreference, syncThemePreferenceUi } from './theme.js';

let initialized = false;

export function beforeWebStart() {
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeCopyButtons();
        initializeDropdownCloseButtons();
        initializeThemePreference();
        initialized = true;
    }

    replaceCurrentUrlIfRequested();
    syncThemePreferenceUi();

    blazor.addEventListener('enhancedload', function() {
        replaceCurrentUrlIfRequested();
        syncThemePreferenceUi();
    });
}
