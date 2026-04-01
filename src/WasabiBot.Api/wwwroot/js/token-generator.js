function restoreTooltip(button, originalTooltip, delay) {
    window.setTimeout(function() {
        button.setAttribute('data-tooltip', originalTooltip);
    }, delay);
}

function getToken(endpoint) {
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
}

async function generateAndCopyToken(button) {
    const endpoint = button.getAttribute('data-token-endpoint');
    const originalTooltip = button.getAttribute('data-tooltip') || 'Generate and copy';
    const originalText = button.textContent || 'Generate and copy token';

    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    button.textContent = 'Generating...';
    button.setAttribute('data-tooltip', 'Generating...');

    try {
        if (navigator.clipboard && navigator.clipboard.write && window.ClipboardItem) {
            await navigator.clipboard.write([
                new ClipboardItem({
                    'text/plain': getToken(endpoint).then(function(token) {
                        return new Blob([token], { type: 'text/plain' });
                    })
                })
            ]);
        } else if (navigator.clipboard && navigator.clipboard.writeText) {
            const token = await getToken(endpoint);
            await navigator.clipboard.writeText(token);
        } else {
            throw new Error('clipboard_unavailable');
        }

        button.setAttribute('data-tooltip', 'Copied!');
        restoreTooltip(button, originalTooltip, 1600);
    } catch (error) {
        console.error('Failed to generate token:', error);
        button.setAttribute('data-tooltip', 'Copy failed');
        restoreTooltip(button, originalTooltip, 2200);
    } finally {
        button.disabled = false;
        button.removeAttribute('aria-busy');
        button.textContent = originalText;
    }
}

export function initializeTokenGenerator() {
    document.addEventListener('click', function(event) {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const button = target.closest('[data-generate-token-button]');
        if (!(button instanceof HTMLButtonElement) || button.disabled) {
            return;
        }

        event.preventDefault();
        void generateAndCopyToken(button);
    });
}
