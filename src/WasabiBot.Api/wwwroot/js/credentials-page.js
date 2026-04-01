const NAME_MAX_LENGTH = 100;
const CONTROL_CHARACTER_PATTERN = /[\u0000-\u001F\u007F]/;

function getPage() {
    return document.querySelector('[data-credentials-page]');
}

function buildUrl(template, id) {
    return template.replace('__id__', String(id));
}

function isValidName(name) {
    const trimmed = name.trim();
    return trimmed.length > 0 && trimmed.length <= NAME_MAX_LENGTH && !CONTROL_CHARACTER_PATTERN.test(trimmed);
}

function getNameValidationMessage(name) {
    const trimmed = name.trim();

    if (trimmed.length === 0) {
        return 'Credential name is required.';
    }

    if (trimmed.length > NAME_MAX_LENGTH) {
        return 'Credential name must be 100 characters or fewer.';
    }

    if (CONTROL_CHARACTER_PATTERN.test(trimmed)) {
        return 'Credential name cannot contain control characters.';
    }

    return '';
}

function setText(element, text) {
    if (element) {
        element.textContent = text;
    }
}

function setHidden(element, hidden) {
    if (element) {
        element.hidden = hidden;
    }
}

function setFormError(page, message) {
    const errorElement = page.querySelector('[data-credentials-form-error]');
    if (!errorElement) {
        return;
    }

    setText(errorElement, message);
    setHidden(errorElement, message.length === 0);
}

function updateCreateFormState(page) {
    const input = page.querySelector('[data-credentials-name-input]');
    const button = page.querySelector('[data-credentials-create-button]');
    if (!(input instanceof HTMLInputElement) || !(button instanceof HTMLButtonElement)) {
        return;
    }

    button.disabled = !isValidName(input.value);
}

function formatTimestamp(value) {
    if (!value) {
        return 'Never';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return 'Unknown';
    }

    return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short'
    }).format(date);
}

function createActionButton(label, datasetKey, datasetValue, variant) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.dataset[datasetKey] = datasetValue;
    if (variant) {
        button.className = variant;
    }
    return button;
}

function renderCredentialCard(credential) {
    const card = document.createElement('article');
    card.className = 'credentials-card';

    const header = document.createElement('header');
    header.className = 'credentials-card__header';

    const title = document.createElement('h3');
    title.className = 'credentials-card__title';
    title.textContent = credential.name;
    header.appendChild(title);

    const status = document.createElement('p');
    status.className = 'credentials-card__status';
    status.textContent = credential.revoked_at ? 'Revoked' : 'Active';
    header.appendChild(status);

    const clientIdLabel = document.createElement('span');
    clientIdLabel.className = 'credentials-label';
    clientIdLabel.textContent = 'Client ID';

    const clientId = document.createElement('code');
    clientId.className = 'credentials-card__client-id';
    clientId.textContent = credential.client_id;

    const metadata = document.createElement('dl');
    metadata.className = 'credentials-card__meta';
    metadata.appendChild(createMetaItem('Created', formatTimestamp(credential.created_at)));
    metadata.appendChild(createMetaItem('Last used', formatTimestamp(credential.last_used_at)));

    if (credential.revoked_at) {
        metadata.appendChild(createMetaItem('Revoked', formatTimestamp(credential.revoked_at)));
    }

    const actions = document.createElement('div');
    actions.className = 'credentials-card__actions';
    actions.appendChild(createActionButton('Copy client ID', 'copyClientId', credential.client_id, 'secondary outline'));

    if (!credential.revoked_at) {
        actions.appendChild(createActionButton('Regenerate secret', 'regenerateCredentialId', String(credential.id), 'secondary'));
        actions.appendChild(createActionButton('Delete credential', 'deleteCredentialId', String(credential.id), 'contrast outline'));
    }

    card.appendChild(header);
    card.appendChild(clientIdLabel);
    card.appendChild(clientId);
    card.appendChild(metadata);
    card.appendChild(actions);
    return card;
}

function createMetaItem(label, value) {
    const wrapper = document.createElement('div');
    wrapper.className = 'credentials-card__meta-item';

    const term = document.createElement('dt');
    term.textContent = label;

    const description = document.createElement('dd');
    description.textContent = value;

    wrapper.appendChild(term);
    wrapper.appendChild(description);
    return wrapper;
}

function renderCredentials(page, credentials) {
    const container = page.querySelector('[data-credentials-list]');
    const feedback = page.querySelector('[data-credentials-list-feedback]');
    if (!(container instanceof HTMLElement) || !(feedback instanceof HTMLElement)) {
        return;
    }

    container.replaceChildren();

    if (!Array.isArray(credentials) || credentials.length === 0) {
        feedback.textContent = 'No credentials yet. Create one to start exchanging for API tokens.';
        return;
    }

    feedback.textContent = credentials.length === 1
        ? '1 credential'
        : `${credentials.length} credentials`;

    for (const credential of credentials) {
        container.appendChild(renderCredentialCard(credential));
    }
}

async function parseJson(response) {
    return response.json().catch(function() {
        return null;
    });
}

async function loadCredentials(page) {
    const endpoint = page.dataset.listEndpoint;
    const feedback = page.querySelector('[data-credentials-list-feedback]');
    if (!endpoint || !(feedback instanceof HTMLElement)) {
        return;
    }

    feedback.textContent = 'Loading credentials...';

    try {
        const response = await fetch(endpoint, {
            method: 'GET',
            cache: 'no-store',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('credentials_load_failed');
        }

        const payload = await parseJson(response);
        renderCredentials(page, payload);
    } catch (error) {
        console.error('Failed to load credentials:', error);
        feedback.textContent = 'Could not load credentials right now.';
    }
}

function showIssuedSecret(page, payload) {
    const panel = page.querySelector('[data-credentials-secret-panel]');
    const clientId = page.querySelector('[data-credentials-issued-client-id]');
    const clientSecret = page.querySelector('[data-credentials-issued-client-secret]');
    if (!payload || !payload.credential || !panel || !clientId || !clientSecret) {
        return;
    }

    setText(clientId, payload.credential.client_id || '');
    setText(clientSecret, payload.client_secret || '');
    setHidden(panel, false);
}

async function copyText(text, button, successText) {
    if (!text) {
        return;
    }

    const original = button.textContent || '';

    try {
        if (!navigator.clipboard || !navigator.clipboard.writeText) {
            throw new Error('clipboard_unavailable');
        }

        await navigator.clipboard.writeText(text);
        button.textContent = successText;
        window.setTimeout(function() {
            button.textContent = original;
        }, 1500);
    } catch (error) {
        console.error('Clipboard write failed:', error);
        button.textContent = 'Copy failed';
        window.setTimeout(function() {
            button.textContent = original;
        }, 1800);
    }
}

async function createCredential(page) {
    const endpoint = page.dataset.createEndpoint;
    const input = page.querySelector('[data-credentials-name-input]');
    const button = page.querySelector('[data-credentials-create-button]');
    if (!endpoint || !(input instanceof HTMLInputElement) || !(button instanceof HTMLButtonElement)) {
        return;
    }

    const validationMessage = getNameValidationMessage(input.value);
    if (validationMessage) {
        setFormError(page, validationMessage);
        updateCreateFormState(page);
        return;
    }

    setFormError(page, '');
    const originalText = button.textContent || 'Create credential';
    button.disabled = true;
    button.textContent = 'Creating...';

    try {
        const response = await fetch(endpoint, {
            method: 'POST',
            cache: 'no-store',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ name: input.value.trim() })
        });

        const payload = await parseJson(response);
        if (!response.ok || !payload) {
            throw new Error(payload && payload.error ? payload.error : 'credential_create_failed');
        }

        showIssuedSecret(page, payload);
        input.value = '';
        setFormError(page, '');
        await loadCredentials(page);
    } catch (error) {
        console.error('Failed to create credential:', error);
        setFormError(page, error instanceof Error ? error.message : 'Could not create credential.');
    } finally {
        button.textContent = originalText;
        updateCreateFormState(page);
    }
}

async function regenerateSecret(page, credentialId) {
    const template = page.dataset.regenerateTemplate;
    if (!template) {
        return;
    }

    try {
        const response = await fetch(buildUrl(template, credentialId), {
            method: 'POST',
            cache: 'no-store',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json'
            }
        });

        const payload = await parseJson(response);
        if (!response.ok || !payload) {
            throw new Error('credential_regenerate_failed');
        }

        showIssuedSecret(page, payload);
        await loadCredentials(page);
    } catch (error) {
        console.error('Failed to regenerate credential secret:', error);
    }
}

async function deleteCredential(page, credentialId) {
    const template = page.dataset.deleteTemplate;
    if (!template) {
        return;
    }

    const confirmed = window.confirm('Delete this credential? Existing access tokens will keep working until they expire.');
    if (!confirmed) {
        return;
    }

    try {
        const response = await fetch(buildUrl(template, credentialId), {
            method: 'DELETE',
            cache: 'no-store',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('credential_delete_failed');
        }

        await loadCredentials(page);
    } catch (error) {
        console.error('Failed to delete credential:', error);
    }
}

export function initializeCredentialsPage() {
    const page = getPage();
    if (!(page instanceof HTMLElement)) {
        return;
    }

    if (page.dataset.initialized === 'true') {
        return;
    }

    page.dataset.initialized = 'true';
    updateCreateFormState(page);
    void loadCredentials(page);

    page.addEventListener('input', function(event) {
        const target = event.target;
        if (!(target instanceof HTMLInputElement) || !target.matches('[data-credentials-name-input]')) {
            return;
        }

        setFormError(page, getNameValidationMessage(target.value));
        updateCreateFormState(page);
    });

    page.addEventListener('submit', function(event) {
        const target = event.target;
        if (!(target instanceof HTMLFormElement) || !target.matches('[data-credentials-create-form]')) {
            return;
        }

        event.preventDefault();
        void createCredential(page);
    });

    page.addEventListener('click', function(event) {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const refreshButton = target.closest('[data-credentials-refresh-button]');
        if (refreshButton instanceof HTMLButtonElement) {
            event.preventDefault();
            void loadCredentials(page);
            return;
        }

        const copyClientIdButton = target.closest('[data-copy-client-id]');
        if (copyClientIdButton instanceof HTMLButtonElement) {
            event.preventDefault();
            void copyText(copyClientIdButton.dataset.copyClientId || '', copyClientIdButton, 'Copied client ID');
            return;
        }

        const regenerateButton = target.closest('[data-regenerate-credential-id]');
        if (regenerateButton instanceof HTMLButtonElement) {
            event.preventDefault();
            void regenerateSecret(page, regenerateButton.dataset.regenerateCredentialId);
            return;
        }

        const deleteButton = target.closest('[data-delete-credential-id]');
        if (deleteButton instanceof HTMLButtonElement) {
            event.preventDefault();
            void deleteCredential(page, deleteButton.dataset.deleteCredentialId);
            return;
        }

        const issuedClientIdButton = target.closest('[data-copy-issued-client-id]');
        if (issuedClientIdButton instanceof HTMLButtonElement) {
            event.preventDefault();
            const text = page.querySelector('[data-credentials-issued-client-id]')?.textContent || '';
            void copyText(text, issuedClientIdButton, 'Copied client ID');
            return;
        }

        const issuedClientSecretButton = target.closest('[data-copy-issued-client-secret]');
        if (issuedClientSecretButton instanceof HTMLButtonElement) {
            event.preventDefault();
            const text = page.querySelector('[data-credentials-issued-client-secret]')?.textContent || '';
            void copyText(text, issuedClientSecretButton, 'Copied client secret');
        }
    });
}
