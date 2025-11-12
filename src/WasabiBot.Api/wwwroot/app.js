'use strict';

window.WasabiBot = {
    copyToClipboard: async (text, elementId) => {
        try {
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(text);
                return true;
            }

            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'fixed';
            textArea.style.left = '-9999px';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();

            try {
                return document.execCommand('copy');
            } finally {
                document.body.removeChild(textArea);
            }
        } catch (error) {
            console.error('Failed to copy to clipboard:', error);
            return false;
        }
    },

    setupTokenCopy: (tokenOutputId) => {
        const tokenOutput = document.getElementById(tokenOutputId);
        if (!tokenOutput) return;

        const tokenPanel = tokenOutput.closest('#token-panel');
        const defaultTooltip = tokenPanel?.dataset?.tooltip || 'Click to copy';
        let feedbackTimeoutId;

        const resetTooltip = () => {
            clearTimeout(feedbackTimeoutId);
            if (tokenPanel) {
                tokenPanel.setAttribute('data-tooltip', defaultTooltip);
                tokenPanel.setAttribute('data-copy-state', 'ready');
            }
        };

        const showCopiedFeedback = () => {
            clearTimeout(feedbackTimeoutId);
            if (tokenPanel) {
                tokenPanel.setAttribute('data-tooltip', 'Copied!');
                tokenPanel.setAttribute('data-copy-state', 'copied');
            }
            feedbackTimeoutId = setTimeout(() => {
                resetTooltip();
            }, 1500);
        };

        const handleCopy = async () => {
            const text = tokenOutput.textContent?.trim();
            if (!text) return;

            const success = await WasabiBot.copyToClipboard(text, tokenOutputId);
            if (success) {
                showCopiedFeedback();
            } else {
                if (tokenPanel) {
                    tokenPanel.setAttribute('data-tooltip', 'Failed to copy. Please copy manually.');
                }
                setTimeout(resetTooltip, 2000);
            }
        };

        tokenOutput.addEventListener('click', handleCopy);
        tokenOutput.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                handleCopy();
            }
        });

        resetTooltip();
    }
};
