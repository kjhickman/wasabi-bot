export function replaceUrlIfNeeded(root = document) {
    const replacementUrl = root.querySelector('[data-replace-url]')?.getAttribute('data-replace-url');
    if (!replacementUrl) {
        return;
    }

    const currentUrl = window.location.pathname + window.location.search + window.location.hash;
    if (currentUrl !== replacementUrl) {
        window.history.replaceState(window.history.state, '', replacementUrl);
    }
}
