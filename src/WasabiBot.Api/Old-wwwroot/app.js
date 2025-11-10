'use strict';

const unauthenticatedSection = document.getElementById('unauthenticated');
const authenticatedSection = document.getElementById('authenticated');
const statusMessage = document.getElementById('status-message');
const userGreetingElement = document.getElementById('user-greeting');
const tokenPanel = document.getElementById('token-panel');
const tokenOutput = document.getElementById('token-output');
const generateTokenButton = document.getElementById('generate-token');
const logoutButton = document.getElementById('logout-button');
const tooltipTarget = tokenPanel || tokenOutput;
const defaultTokenCopyTooltip = tooltipTarget?.dataset?.tooltip?.trim() || 'Click to copy';
let tokenCopyFeedbackTimeoutId;
let currentTokenValue = '';

const show = (element) => {
    element.hidden = false;
};

const hide = (element) => {
    element.hidden = true;
};

const setStatus = (message) => {
    if (!message) {
        statusMessage.textContent = '';
        hide(statusMessage);
        return;
    }

    statusMessage.textContent = message;
    show(statusMessage);
};

const parseProblem = async (response) => {
    try {
        const data = await response.json();
        return data?.error || data?.detail || data?.title || response.statusText;
    } catch (error) {
        return response.statusText;
    }
};

const updateGreeting = (displayName) => {
    if (userGreetingElement) {
        userGreetingElement.textContent = displayName || 'there';
    }
};

const setAuthenticatedView = (isAuthenticated) => {
    if (isAuthenticated) {
        hide(unauthenticatedSection);
        show(authenticatedSection);
        return;
    }

    hide(authenticatedSection);
    show(unauthenticatedSection);
    setStatus('');
};

const setTooltipText = (text) => {
    if (!tooltipTarget) {
        return;
    }

    if (text) {
        tooltipTarget.setAttribute('data-tooltip', text);
    } else {
        tooltipTarget.removeAttribute('data-tooltip');
    }
};

const resetTokenCopyState = () => {
    clearTimeout(tokenCopyFeedbackTimeoutId);
    tooltipTarget?.setAttribute('data-copy-state', 'ready');
    setTooltipText(defaultTokenCopyTooltip);
};

const indicateTokenCopied = () => {
    clearTimeout(tokenCopyFeedbackTimeoutId);
    tooltipTarget?.setAttribute('data-copy-state', 'copied');
    setTooltipText('Copied!');
    tokenCopyFeedbackTimeoutId = setTimeout(() => {
        resetTokenCopyState();
    }, 1500);
};

const hideTokenPanel = () => {
    currentTokenValue = '';

    if (!tokenPanel) {
        return;
    }

    hide(tokenPanel);
    resetTokenCopyState();
};

const showTokenPanel = () => {
    if (!tokenPanel) {
        return;
    }

    show(tokenPanel);
};

const copyTextToClipboard = async (text) => {
    if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
        return true;
    }

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
};

const loadProfile = async () => {
    try {
        const response = await fetch('auth/me', {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });

        if (response.status === 401) {
            hide(statusMessage);
            hideTokenPanel();
            setAuthenticatedView(false);
            return;
        }

        if (!response.ok) {
            const message = await parseProblem(response);
            setStatus(`Unable to load profile: ${message}`);
            hideTokenPanel();
            setAuthenticatedView(false);
            return;
        }

        const profile = await response.json();
        const displayName = profile.globalName || profile.username || '';
        updateGreeting(displayName);

        setAuthenticatedView(true);
        hideTokenPanel();
        setStatus('');
    } catch (error) {
        setStatus('Unable to reach the server. Please try again.');
        hideTokenPanel();
        setAuthenticatedView(false);
    }
};

const handleGenerateToken = async () => {
    setStatus('');
    hideTokenPanel();
    generateTokenButton.disabled = true;

    try {
        const response = await fetch('api/v1/token', {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });

        if (!response.ok) {
            if (response.status === 403) {
                setStatus('You need to share at least one Discord server with the Wasabi Bot before generating a token.');
            } else {
                const message = await parseProblem(response);
                setStatus(`Unable to generate token: ${message}`);
            }
            return;
        }

        const token = await response.json();
        const accessToken = token.token ?? '';
        if (!accessToken) {
            setStatus('Token was generated but the response is missing the token value.');
            return;
        }

        const tokenValue = accessToken.trim();

        if (!tokenOutput) {
            console.warn('Missing token output element; cannot display generated token.');
            return;
        }

        currentTokenValue = tokenValue;
        tokenOutput.textContent = tokenValue;

        showTokenPanel();
    } catch (error) {
        setStatus('Unable to reach the server. Please try again.');
    } finally {
        generateTokenButton.disabled = false;
    }
};

const handleCopyToken = async () => {
    if (!currentTokenValue) {
        setStatus('Generate a token before copying.');
        return;
    }

    try {
        const copied = await copyTextToClipboard(currentTokenValue);
        if (!copied) {
            throw new Error('copy-failed');
        }

        indicateTokenCopied();
    } catch (error) {
        setStatus('Unable to copy automatically. Please copy the token manually.');
    }
};

const handleTokenOutputKeydown = (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        handleCopyToken();
    }
};

const handleLogout = async () => {
    logoutButton.disabled = true;
    setStatus('');

    try {
        await fetch('auth/logout', {
            method: 'POST',
            headers: { 'X-Requested-With': 'fetch' },
            credentials: 'same-origin'
        });
    } catch (error) {
        // Ignore errors and fall back to a reload.
    } finally {
        window.location.href = './';
    }
};

const initialize = () => {
    generateTokenButton?.addEventListener('click', handleGenerateToken);
    tokenOutput?.addEventListener('click', handleCopyToken);
    tokenOutput?.addEventListener('keydown', handleTokenOutputKeydown);
    logoutButton?.addEventListener('click', handleLogout);
    loadProfile();
};

initialize();
