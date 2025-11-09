'use strict';

const unauthenticatedSection = document.getElementById('unauthenticated');
const authenticatedSection = document.getElementById('authenticated');
const statusMessage = document.getElementById('status-message');
const userGreetingElement = document.getElementById('user-greeting');
const tokenOutput = document.getElementById('token-output');
const generateTokenButton = document.getElementById('generate-token');
const logoutButton = document.getElementById('logout-button');

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
    if (!userGreetingElement) {
        console.warn('Missing user greeting element; skipping greeting update.');
        return;
    }

    userGreetingElement.textContent = displayName || 'there';
};

const loadProfile = async () => {
    try {
        const response = await fetch('auth/me', {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });

        if (response.status === 401) {
            hide(authenticatedSection);
            hide(statusMessage);
            show(unauthenticatedSection);
            return;
        }

        if (!response.ok) {
            const message = await parseProblem(response);
            setStatus(`Unable to load profile: ${message}`);
            hide(authenticatedSection);
            show(unauthenticatedSection);
            return;
        }

        const profile = await response.json();
        const displayName = profile.globalName || profile.username || '';
        updateGreeting(displayName);

        hide(unauthenticatedSection);
        show(authenticatedSection);
        hide(tokenOutput);
        setStatus('');
    } catch (error) {
        setStatus('Unable to reach the server. Please try again.');
        hide(authenticatedSection);
        show(unauthenticatedSection);
    }
};

const handleGenerateToken = async () => {
    setStatus('');
    hide(tokenOutput);
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
        const tokenType = token.token_type ?? token.tokenType ?? 'Bearer';
        const accessToken = token.access_token ?? token.accessToken ?? '';
        const expiresIn = token.expires_in ?? token.expiresIn;

        if (!accessToken) {
            setStatus('Token was generated but the response is missing the token value.');
            return;
        }

        const tokenValue = `${tokenType} ${accessToken}`.trim();
        const expiresLine = typeof expiresIn === 'number'
            ? `\n\nExpires In: ${expiresIn} seconds`
            : '';

        tokenOutput.textContent = `${tokenValue}${expiresLine}`;
        show(tokenOutput);
    } catch (error) {
        setStatus('Unable to reach the server. Please try again.');
    } finally {
        generateTokenButton.disabled = false;
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
    logoutButton?.addEventListener('click', handleLogout);
    loadProfile();
};

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
} else {
    initialize();
}
