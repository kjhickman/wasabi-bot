(function() {
    const storageKey = 'wasabi-theme';
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    const getStoredTheme = function() {
        try {
            return localStorage.getItem(storageKey) || 'system';
        } catch {
            return 'system';
        }
    };

    const resolveTheme = function(theme) {
        if (theme === 'light' || theme === 'dark') {
            return theme;
        }

        return mediaQuery.matches ? 'dark' : 'light';
    };

    const applyResolvedTheme = function(theme) {
        const resolvedTheme = resolveTheme(theme);
        document.documentElement.setAttribute('data-theme', resolvedTheme);
        document.documentElement.style.colorScheme = resolvedTheme;
    };

    window.wasabiThemeSyncControls = function() {
        const selectedTheme = getStoredTheme();
        document.querySelectorAll('[data-theme-select]').forEach(function(select) {
            select.value = selectedTheme;
        });
    };

    window.wasabiCloseAccountMenus = function() {
        document.querySelectorAll('[data-account-menu-panel]').forEach(function(panel) {
            panel.hidden = true;
        });

        document.querySelectorAll('#account-menu-button[aria-expanded="true"]').forEach(function(button) {
            button.setAttribute('aria-expanded', 'false');
        });
    };

    window.wasabiThemeApply = function(theme) {
        const selectedTheme = theme === 'light' || theme === 'dark' ? theme : 'system';

        try {
            localStorage.setItem(storageKey, selectedTheme);
        } catch {
        }

        applyResolvedTheme(selectedTheme);
        window.wasabiThemeSyncControls();
        window.wasabiCloseAccountMenus();
    };

    window.wasabiToggleAccountMenu = function(button) {
        if (!button) {
            return;
        }

        const panel = document.getElementById(button.getAttribute('aria-controls'));
        if (!panel) {
            return;
        }

        const shouldOpen = panel.hidden;
        window.wasabiCloseAccountMenus();

        if (!shouldOpen) {
            return;
        }

        panel.hidden = false;
        button.setAttribute('aria-expanded', 'true');
        window.wasabiThemeSyncControls();
    };

    document.addEventListener('click', function(event) {
        if (event.target instanceof Element && event.target.closest('.account-menu')) {
            return;
        }

        window.wasabiCloseAccountMenus();
    });

    document.addEventListener('keydown', function(event) {
        if (event.key === 'Escape') {
            window.wasabiCloseAccountMenus();
        }
    });

    applyResolvedTheme(getStoredTheme());

    if (typeof mediaQuery.addEventListener === 'function') {
        mediaQuery.addEventListener('change', function() {
            if (getStoredTheme() === 'system') {
                applyResolvedTheme('system');
            }
        });
    }

    if (!window.generateAndCopyToken) {
        window.generateAndCopyToken = async function(button) {
            if (!button) {
                return;
            }

            const endpoint = button.getAttribute('data-token-endpoint');
            const originalTooltip = button.getAttribute('data-tooltip') || 'Generate and copy';
            const originalText = button.textContent || 'Generate and copy token';

            const restoreTooltip = function(delay) {
                window.setTimeout(function() {
                    button.setAttribute('data-tooltip', originalTooltip);
                }, delay);
            };

            const getToken = function() {
                return fetch(endpoint, {
                    method: 'POST',
                    cache: 'no-store',
                    credentials: 'same-origin',
                    headers: {
                        'Accept': 'application/json'
                    }
                }).then(async function(response) {
                    const payload = await response.json().catch(function() {
                        return null;
                    });

                    if (!response.ok || !payload || !payload.token) {
                        throw new Error(payload && payload.error ? payload.error : 'token_request_failed');
                    }

                    return payload.token;
                });
            };

            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
            button.textContent = 'Generating...';
            button.setAttribute('data-tooltip', 'Generating...');

            try {
                if (navigator.clipboard && navigator.clipboard.write && window.ClipboardItem) {
                    await navigator.clipboard.write([
                        new ClipboardItem({
                            'text/plain': getToken().then(function(token) {
                                return new Blob([token], { type: 'text/plain' });
                            })
                        })
                    ]);
                } else if (navigator.clipboard && navigator.clipboard.writeText) {
                    const token = await getToken();
                    await navigator.clipboard.writeText(token);
                } else {
                    throw new Error('clipboard_unavailable');
                }

                button.setAttribute('data-tooltip', 'Copied!');
                restoreTooltip(1600);
            } catch (error) {
                console.error('Failed to generate token:', error);
                button.setAttribute('data-tooltip', 'Copy failed');
                restoreTooltip(2200);
            } finally {
                button.disabled = false;
                button.removeAttribute('aria-busy');
                button.textContent = originalText;
            }
        };
    }
})();
