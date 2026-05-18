export function replaceCurrentUrlIfRequested(root = document) {
    const replacementUrl = root.querySelector('[data-wasabi-replace-current-url]')?.getAttribute('data-wasabi-replace-current-url');
    if (!replacementUrl) {
        return;
    }

    const currentUrl = window.location.pathname + window.location.search + window.location.hash;
    if (currentUrl !== replacementUrl) {
        window.history.replaceState(window.history.state, '', replacementUrl);
    }
}
