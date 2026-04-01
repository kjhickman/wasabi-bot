---
name: frontend-playwright-debugging
description: "Uses playwright-cli to open the local Wasabi Bot frontend, reuse a repo-local authenticated browser profile, and debug UI/auth flows with snapshots, screenshots, and DOM inspection. USE FOR: manual smoke testing of the local frontend, validating Discord-backed sign-in flows, reproducing UI bugs in a visible browser, inspecting rendered state after form submissions. DO NOT USE FOR: unit/integration testing, repeated scripted credential entry, broad unattended browser automation, API-only debugging. INVOKES: playwright-cli, bash, question."
---

# Skill: frontend-playwright-debugging

# Frontend Playwright Debugging Skill

Use this skill when you need to exercise the local Wasabi Bot frontend through a real browser.

This repository has a Blazor frontend in `src/WasabiBot.Api` that uses Discord OAuth for sign-in, and it works well with `playwright-cli` for manual debugging.

## Required configuration

This skill expects the local app to be running under Aspire so the frontend URL can be discovered from the running AppHost.

- First, check that Aspire is running.
- Discover the `Frontend` URL from `aspire describe`.
- If Aspire is not running, start it with `aspire start` and wait for the app resource to become healthy.
- If the frontend URL still cannot be determined from Aspire, ask the user before continuing.

Use the repository root for all local Playwright artifacts:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
PLAYWRIGHT_DIR="$REPO_ROOT/.playwright-cli"
PLAYWRIGHT_PROFILE_DIR="$PLAYWRIGHT_DIR/profile"
PLAYWRIGHT_STATE_PATH="$PLAYWRIGHT_DIR/frontend-state.json"
```

## What this skill is for

- Opening the local frontend in a visible browser window
- Reusing an authenticated browser/profile instead of logging in every run
- Following the Discord OAuth login flow back into the local app
- Reproducing UI bugs on `/`, `/creds`, and other frontend routes
- Verifying rendered state with snapshots, screenshots, and targeted DOM inspection

## Commands that worked here

Open the local frontend in a visible persistent browser after resolving the `Frontend` URL from Aspire:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
FRONTEND_URL="$(aspire describe --format json | jq -r '.resources[] | .urls[]? | select(.displayText == "Frontend") | .url' | head -n 1)"
playwright-cli open "$FRONTEND_URL" --persistent --profile "$REPO_ROOT/.playwright-cli/profile"
```

Open the credentials page directly with the same persistent profile:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
FRONTEND_URL="$(aspire describe --format json | jq -r '.resources[] | .urls[]? | select(.displayText == "Frontend") | .url' | head -n 1)"
playwright-cli open "$FRONTEND_URL/creds" --persistent --profile "$REPO_ROOT/.playwright-cli/profile"
```

Save storage state inside the workspace after login succeeds:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
playwright-cli state-save "$REPO_ROOT/.playwright-cli/frontend-state.json"
```

Inspect current page state:

```bash
playwright-cli snapshot
playwright-cli screenshot
playwright-cli eval "() => ({ title: document.title, url: location.href })"
```

## Recommended workflow

1. Ensure the local app is already running and reachable. If you need the full local topology, start it with `aspire start`.
2. Resolve the `Frontend` URL from `aspire describe`.
3. Open the frontend with `playwright-cli open ... --persistent --profile ...` so the existing browser session is reused first.
4. If the app redirects to Discord and the browser is not authenticated, have the user log in manually.
5. Once the app returns to the local site in an authenticated state, save storage state to `.playwright-cli/frontend-state.json`.
6. Confirm the page title and URL match the expected route before reproducing the issue.
7. Prefer stable page ids like `#login-link`, `#creds-link`, `#credentials-page`, and `#credentials-create-open-button` when driving the UI.
8. Use `playwright-cli snapshot`, `playwright-cli screenshot`, and `playwright-cli eval` after each meaningful interaction.
9. If the issue involves forms or dialogs, inspect the resulting DOM and any visible error text before retrying.

## Important details

- Always use `--headed` when the user needs to interact with the Discord login flow manually.
- Always start by opening the app with the repo-local persistent profile so any saved Discord and local app session is reused before attempting a new login.
- Prefer `--persistent` with a repo-local `--profile` directory so both Discord and local app auth survive across sessions.
- Save auth state inside the workspace, not in a home-directory path.
- Keep all Playwright artifacts under `$REPO_ROOT/.playwright-cli/`.
- Frontend auth depends on Discord OAuth, so a valid Discord session in the persistent profile is usually the critical prerequisite.
- Save frontend state after the app finishes its own auth callback so local cookies are captured too.
- The saved `frontend-state.json` is a workspace artifact for inspection and reuse in tooling, but the primary reuse path in this skill is the persistent Playwright profile.
- Prefer stable ids and accessible labels from the Razor pages over brittle CSS selectors.

## Verification patterns

Use these checks after navigation or form interactions:

- `playwright-cli snapshot` to inspect the latest rendered UI
- `playwright-cli screenshot` for visual confirmation
- `playwright-cli eval` for targeted DOM inspection when the snapshot is too large

Example auth-state inspection:

```bash
playwright-cli eval "() => ({ title: document.title, url: location.href, loginVisible: !!document.querySelector('#login-link, #credentials-login-link'), greeting: document.querySelector('#user-greeting')?.textContent?.trim() || null, credentialsPage: !!document.querySelector('#credentials-page') })"
```

Example credentials-page inspection:

```bash
playwright-cli eval "() => ({ createModalOpen: !!document.querySelector('#credentials-create-modal[open]'), confirmModalOpen: !!document.querySelector('#credentials-confirm-modal[open]'), pageError: document.querySelector('#credentials-page-error')?.textContent?.trim() || null, formError: document.querySelector('#credentials-form-error')?.textContent?.trim() || null })"
```

## Guardrails

- Use this only for smoke tests and interactive debugging of real frontend behavior.
- Do not automate Discord credential entry unless the user explicitly asks; prefer manual login.
- Do not rely on browser automation as the primary frontend test suite.
- Keep the automation scoped to the local Wasabi Bot frontend.
- Do not commit `.playwright-cli/` artifacts.

## Example prompt

```text
Use playwright-cli to discover the Frontend URL from Aspire, open the local frontend with the saved persistent profile, sign in through Discord if needed, reproduce the /creds create flow, and inspect the rendered UI state.
```
