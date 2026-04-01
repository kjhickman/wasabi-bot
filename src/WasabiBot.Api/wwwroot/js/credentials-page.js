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

function getCreateModal(page) {
    const modal = page.querySelector('[data-credentials-create-modal]');
    return modal instanceof HTMLDialogElement ? modal : null;
}

function getConfirmModal(page) {
    const modal = page.querySelector('[data-credentials-confirm-modal]');
    return modal instanceof HTMLDialogElement ? modal : null;
}

function getSecretModal(page) {
    const modal = page.querySelector('[data-credentials-secret-modal]');
    return modal instanceof HTMLDialogElement ? modal : null;
}

function getNameInput(page) {
    const input = page.querySelector('[data-credentials-name-input]');
    return input instanceof HTMLInputElement ? input : null;
}

function getCreateButton(page) {
    const button = page.querySelector('[data-credentials-create-button]');
    return button instanceof HTMLButtonElement ? button : null;
}

function resetCreateForm(page) {
    const input = getNameInput(page);
    if (input) {
        input.value = '';
    }

    setFormError(page, '');
    updateCreateFormState(page);
}

function openCreateModal(page) {
    const modal = getCreateModal(page);
    const input = getNameInput(page);
    if (!modal) {
        return;
    }

    resetCreateForm(page);

    if (typeof modal.showModal === 'function' && !modal.open) {
        modal.showModal();
    }

    window.setTimeout(function() {
        input?.focus();
    }, 0);
}

function closeCreateModal(page) {
    const modal = getCreateModal(page);
    if (!modal) {
        return;
    }

    if (modal.open) {
        modal.close();
    }

    resetCreateForm(page);
}

function closeConfirmModal(page) {
    const modal = getConfirmModal(page);
    if (!modal) {
        return;
    }

    if (modal.open) {
        modal.close();
    }

    delete page.dataset.confirmAction;
    delete page.dataset.confirmCredentialId;
}

function closeSecretModal(page) {
    const modal = getSecretModal(page);
    if (!modal) {
        return;
    }

    if (modal.open) {
        modal.close();
    }
}

function openConfirmModal(page, action, credentialId) {
    const modal = getConfirmModal(page);
    const title = page.querySelector('[data-credentials-confirm-title]');
    const message = page.querySelector('[data-credentials-confirm-message]');
    const confirmButton = page.querySelector('[data-credentials-confirm-button]');
    if (!modal || !(title instanceof HTMLElement) || !(message instanceof HTMLElement) || !(confirmButton instanceof HTMLButtonElement)) {
        return;
    }

    page.dataset.confirmAction = action;
    page.dataset.confirmCredentialId = String(credentialId);

    if (action === 'delete') {
        title.textContent = 'Delete credential';
        message.textContent = 'Delete this credential? Existing access tokens will keep working until they expire.';
        confirmButton.textContent = 'Delete';
        confirmButton.className = 'contrast';
    } else {
        title.textContent = 'Regenerate secret';
        message.textContent = 'Regenerate this secret? The current secret will stop working immediately.';
        confirmButton.textContent = 'Regenerate';
        confirmButton.className = '';
    }

    if (typeof modal.showModal === 'function' && !modal.open) {
        modal.showModal();
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
    const input = getNameInput(page);
    const button = getCreateButton(page);
    if (!input || !button) {
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
    button.className = variant || '';
    button.setAttribute('aria-label', label);
    return button;
}

function createTableCell(content) {
    const cell = document.createElement('td');
    if (content instanceof Node) {
        cell.appendChild(content);
    } else {
        cell.textContent = content;
    }

    return cell;
}

function renderCredentialRow(credential) {
    const row = document.createElement('tr');

    row.appendChild(createTableCell(credential.name));

    const clientId = document.createElement('code');
    clientId.className = 'credentials-table__client-id';
    clientId.textContent = credential.client_id;
    row.appendChild(createTableCell(clientId));

    row.appendChild(createTableCell(formatTimestamp(credential.created_at)));
    row.appendChild(createTableCell(formatTimestamp(credential.last_used_at)));

    const actions = document.createElement('div');
    actions.className = 'credentials-table__actions';

    const regenerateButton = createActionButton('Regenerate', 'regenerateCredentialId', String(credential.id), 'secondary outline');
    actions.appendChild(regenerateButton);

    const deleteButton = createActionButton('Delete', 'deleteCredentialId', String(credential.id), 'contrast outline');
    actions.appendChild(deleteButton);

    row.appendChild(createTableCell(actions));

    return row;
}

function renderEmptyState(container, message) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 5;
    cell.className = 'credentials-table__empty';
    cell.textContent = message;
    row.appendChild(cell);
    container.appendChild(row);
}

function renderCredentials(page, credentials) {
    const container = page.querySelector('[data-credentials-list]');
    const feedback = page.querySelector('[data-credentials-list-feedback]');
    if (!(container instanceof HTMLTableSectionElement) || !(feedback instanceof HTMLElement)) {
        return;
    }

    container.replaceChildren();

    if (!Array.isArray(credentials) || credentials.length === 0) {
        feedback.textContent = 'No credentials yet.';
        renderEmptyState(container, 'Create credentials to start exchanging for API tokens.');
        return;
    }

    feedback.textContent = credentials.length === 1
        ? '1 active credential'
        : `${credentials.length} active credentials`;

    for (const credential of credentials) {
        container.appendChild(renderCredentialRow(credential));
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
                Accept: 'application/json'
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
    const modal = getSecretModal(page);
    const clientId = page.querySelector('[data-credentials-issued-client-id]');
    const clientSecret = page.querySelector('[data-credentials-issued-client-secret]');
    if (!payload || !payload.credential || !modal || !clientId || !clientSecret) {
        return;
    }

    setText(clientId, payload.credential.client_id || '');
    setText(clientSecret, payload.client_secret || '');

    if (typeof modal.showModal === 'function' && !modal.open) {
        modal.showModal();
    }
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
    const input = getNameInput(page);
    const button = getCreateButton(page);
    if (!endpoint || !input || !button) {
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
                Accept: 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ name: input.value.trim() })
        });

        const payload = await parseJson(response);
        if (!response.ok || !payload) {
            throw new Error(payload && payload.error ? payload.error : 'credential_create_failed');
        }

        showIssuedSecret(page, payload);
        closeCreateModal(page);
        await loadCredentials(page);
    } catch (error) {
        console.error('Failed to create credential:', error);
        setFormError(page, error instanceof Error ? error.message : 'Could not create credential.');
        updateCreateFormState(page);
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
                Accept: 'application/json'
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

    try {
        const response = await fetch(buildUrl(template, credentialId), {
            method: 'DELETE',
            cache: 'no-store',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json'
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

async function runConfirmedAction(page) {
    const action = page.dataset.confirmAction;
    const credentialId = page.dataset.confirmCredentialId;
    if (!action || !credentialId) {
        return;
    }

    closeConfirmModal(page);

    if (action === 'delete') {
        await deleteCredential(page, credentialId);
        return;
    }

    if (action === 'regenerate') {
        await regenerateSecret(page, credentialId);
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

    const modal = getCreateModal(page);
    modal?.addEventListener('close', function() {
        resetCreateForm(page);
    });

    const confirmModal = getConfirmModal(page);
    confirmModal?.addEventListener('close', function() {
        delete page.dataset.confirmAction;
        delete page.dataset.confirmCredentialId;
    });

    const secretModal = getSecretModal(page);
    secretModal?.addEventListener('close', function() {
        const clientId = page.querySelector('[data-credentials-issued-client-id]');
        const clientSecret = page.querySelector('[data-credentials-issued-client-secret]');
        setText(clientId, '');
        setText(clientSecret, '');
    });

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

        const openModalButton = target.closest('[data-credentials-open-modal]');
        if (openModalButton instanceof HTMLButtonElement) {
            event.preventDefault();
            openCreateModal(page);
            return;
        }

        const closeModalButton = target.closest('[data-credentials-close-modal]');
        if (closeModalButton instanceof HTMLButtonElement) {
            event.preventDefault();
            closeCreateModal(page);
            return;
        }

        const closeConfirmButton = target.closest('[data-credentials-close-confirm-modal]');
        if (closeConfirmButton instanceof HTMLButtonElement) {
            event.preventDefault();
            closeConfirmModal(page);
            return;
        }

        const closeSecretButton = target.closest('[data-credentials-close-secret-modal]');
        if (closeSecretButton instanceof HTMLButtonElement) {
            event.preventDefault();
            closeSecretModal(page);
            return;
        }

        const confirmButton = target.closest('[data-credentials-confirm-button]');
        if (confirmButton instanceof HTMLButtonElement) {
            event.preventDefault();
            void runConfirmedAction(page);
            return;
        }

        const refreshButton = target.closest('[data-credentials-refresh-button]');
        if (refreshButton instanceof HTMLButtonElement) {
            event.preventDefault();
            void loadCredentials(page);
            return;
        }

        const regenerateButton = target.closest('[data-regenerate-credential-id]');
        if (regenerateButton instanceof HTMLButtonElement) {
            event.preventDefault();
            openConfirmModal(page, 'regenerate', regenerateButton.dataset.regenerateCredentialId);
            return;
        }

        const deleteButton = target.closest('[data-delete-credential-id]');
        if (deleteButton instanceof HTMLButtonElement) {
            event.preventDefault();
            openConfirmModal(page, 'delete', deleteButton.dataset.deleteCredentialId);
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
