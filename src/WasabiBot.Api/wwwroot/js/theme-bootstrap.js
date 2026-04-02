(function() {
    const storageKey = 'wasabi-theme';
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    function getStoredTheme() {
        try {
            return localStorage.getItem(storageKey) || 'system';
        } catch {
            return 'system';
        }
    }

    function normalizeTheme(theme) {
        return theme === 'light' || theme === 'dark' ? theme : 'system';
    }

    function resolveTheme(theme) {
        const normalizedTheme = normalizeTheme(theme);
        if (normalizedTheme !== 'system') {
            return normalizedTheme;
        }

        return mediaQuery.matches ? 'dark' : 'light';
    }

    function applyResolvedTheme(theme) {
        const resolvedTheme = resolveTheme(theme);
        document.documentElement.setAttribute('data-theme', resolvedTheme);
        document.documentElement.style.colorScheme = resolvedTheme;
    }

    function setStoredTheme(theme) {
        const normalizedTheme = normalizeTheme(theme);

        try {
            localStorage.setItem(storageKey, normalizedTheme);
        } catch {
        }

        return normalizedTheme;
    }

    window.WasabiTheme = {
        mediaQuery,
        getStoredTheme,
        normalizeTheme,
        resolveTheme,
        applyResolvedTheme,
        setStoredTheme,
        syncDocumentTheme() {
            applyResolvedTheme(getStoredTheme());
        }
    };

    window.WasabiTheme.syncDocumentTheme();
})();
