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

function initializeCopyButtons() {
    document.addEventListener('click', handleCopyButtonClick);
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
    initializeTheme();
}

export function afterWebStarted(blazor) {
    if (!initialized) {
        initializeThemeControls();
        initializeCopyButtons();
        initialized = true;
    }

    syncTheme();
    replaceUrlIfNeeded();

    blazor.addEventListener('enhancedload', function() {
        syncTheme();
        replaceUrlIfNeeded();
    });
}
