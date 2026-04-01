import { applyTheme, syncThemeControls } from '/js/theme.js';

function closeAccountMenus(root = document) {
    root.querySelectorAll('[data-account-menu-panel]').forEach(function(panel) {
        panel.hidden = true;
    });

    root.querySelectorAll('[data-account-menu-toggle][aria-expanded="true"]').forEach(function(button) {
        button.setAttribute('aria-expanded', 'false');
    });
}

function toggleAccountMenu(button) {
    if (!button) {
        return;
    }

    const panel = document.getElementById(button.getAttribute('aria-controls'));
    if (!panel) {
        return;
    }

    const shouldOpen = panel.hidden;
    closeAccountMenus();

    if (!shouldOpen) {
        return;
    }

    panel.hidden = false;
    button.setAttribute('aria-expanded', 'true');
    syncThemeControls(panel);
}

export function initializeAccountMenu() {
    document.addEventListener('click', handleDocumentClick);
    document.addEventListener('keydown', handleDocumentKeydown);
    document.addEventListener('change', handleDocumentChange);
}

function handleDocumentClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        closeAccountMenus();
        return;
    }

    const toggleButton = target.closest('[data-account-menu-toggle]');
    if (toggleButton instanceof HTMLButtonElement) {
        toggleAccountMenu(toggleButton);
        return;
    }

    if (target.closest('.account-menu')) {
        return;
    }

    closeAccountMenus();
}

function handleDocumentKeydown(event) {
    if (event.key === 'Escape') {
        closeAccountMenus();
    }
}

function handleDocumentChange(event) {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement) || !target.matches('[data-theme-select]')) {
        return;
    }

    applyTheme(target.value);
    syncThemeControls();
    closeAccountMenus();
}
