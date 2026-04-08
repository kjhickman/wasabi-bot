let initialized = false;
const themePreferenceStorageKey = 'wasabi-theme-preference';

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

function initializeCopyButtons() {
    document.addEventListener('click', handleCopyButtonClick);
}

function initializeThemePreference() {
    document.addEventListener('change', handleThemePreferenceChange);
    document.addEventListener('click', handleThemePreferenceMenuOpen);
    applyThemePreference(getThemePreference());
    syncThemePreferenceInputs();
    queueThemePreferenceSync();
}

function getThemePreference() {
    try {
        const preference = window.localStorage.getItem(themePreferenceStorageKey);
        if (preference === 'light' || preference === 'dark') {
            return preference;
        }
    } catch {
    }

    return 'system';
}

function applyThemePreference(preference) {
    if (preference === 'light' || preference === 'dark') {
        document.documentElement.setAttribute('data-theme', preference);
        return;
    }

    document.documentElement.removeAttribute('data-theme');
}

function syncThemePreferenceInputs(root = document) {
    const preference = getThemePreference();
    const inputs = root.querySelectorAll('input[data-theme-preference]');

    for (const input of inputs) {
        if (!(input instanceof HTMLInputElement)) {
            continue;
        }

        const isChecked = input.value === preference;
        input.checked = isChecked;
        input.defaultChecked = isChecked;

        if (isChecked) {
            input.setAttribute('checked', 'checked');
        } else {
            input.removeAttribute('checked');
        }
    }
}

function queueThemePreferenceSync() {
    window.requestAnimationFrame(function() {
        syncThemePreferenceInputs();

        window.setTimeout(function() {
            syncThemePreferenceInputs();
        }, 0);
    });
}

function handleThemePreferenceMenuOpen(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    if (!target.closest('#account-menu-button')) {
        return;
    }

    queueThemePreferenceSync();
}

function handleThemePreferenceChange(event) {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || !target.matches('input[data-theme-preference]')) {
        return;
    }

    const preference = target.value === 'light' || target.value === 'dark'
        ? target.value
        : 'system';

    try {
        if (preference === 'system') {
            window.localStorage.removeItem(themePreferenceStorageKey);
        } else {
            window.localStorage.setItem(themePreferenceStorageKey, preference);
        }
    } catch {
    }

    applyThemePreference(preference);
    syncThemePreferenceInputs();
}

async function handleCopyButtonClick(event) {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
        return;
    }

    const button = target.closest('[data-copy-button]');
    if (!(button instanceof HTMLButtonElement)) {
        return;
    }

    const copyText = button.getAttribute('data-copy-text');
    if (!copyText) {
        return;
    }

    const originalLabel = button.getAttribute('data-copy-label') || button.textContent || 'Copy';

    try {
        await navigator.clipboard.writeText(copyText);
        button.textContent = 'Copied';
    } catch {
        button.textContent = 'Copy failed';
    }

    window.setTimeout(function() {
        button.textContent = originalLabel;
    }, 1500);
}

export function beforeWebStart() {
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeCopyButtons();
        initializeThemePreference();
        initialized = true;
    }

    replaceUrlIfNeeded();
    applyThemePreference(getThemePreference());
    syncThemePreferenceInputs();
    queueThemePreferenceSync();

    blazor.addEventListener('enhancedload', function() {
        replaceUrlIfNeeded();
        applyThemePreference(getThemePreference());
        syncThemePreferenceInputs();
        queueThemePreferenceSync();
    });
}
