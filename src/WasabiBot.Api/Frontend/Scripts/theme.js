const themePreferenceStorageKey = 'wasabi-theme-preference';

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

export function initializeThemePreference() {
    document.addEventListener('change', handleThemePreferenceChange);
    document.addEventListener('click', handleThemePreferenceMenuOpen);
    syncThemePreferenceUi();
}

export function syncThemePreferenceUi() {
    applyThemePreference(getThemePreference());
    syncThemePreferenceInputs();
    queueThemePreferenceSync();
}
