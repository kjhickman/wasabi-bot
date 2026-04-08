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

export function initializeCopyButtons() {
    document.addEventListener('click', handleCopyButtonClick);
}
