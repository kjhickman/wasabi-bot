---
name: discord-playwright-testing
description: "Uses playwright-cli to open Discord web, reuse an authenticated session, navigate to the configured test guild/channel, and run slash commands against the local bot. USE FOR: manual smoke testing of Discord slash commands through the real web client, validating local bot responses in a test guild, saving and reusing Discord auth state. DO NOT USE FOR: unit/integration testing, repeated scripted login flows, broad unattended Discord automation. INVOKES: playwright-cli, bash, question."
---

# Discord Playwright Testing Skill

Use this skill when you need to exercise the local bot through the real Discord web client.

This repository has a working Discord smoke-test flow with `playwright-cli`.

## Required configuration

This skill expects a Discord channel URL in `DISCORD_TEST_SERVER_URL`.

- First, check whether `DISCORD_TEST_SERVER_URL` is set.
- If it is not set, ask the user for the Discord channel URL.
- Then set `DISCORD_TEST_SERVER_URL` before continuing.

For the current shell session:

```bash
export DISCORD_TEST_SERVER_URL="https://discord.com/channels/<guild-id>/<channel-id>"
```

Use the repository root for all local Playwright artifacts:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
PLAYWRIGHT_DIR="$REPO_ROOT/.playwright-cli"
PLAYWRIGHT_PROFILE_DIR="$PLAYWRIGHT_DIR/profile"
PLAYWRIGHT_STATE_PATH="$PLAYWRIGHT_DIR/discord-state.json"
```

## What this skill is for

- Opening Discord in a visible browser window
- Reusing an authenticated browser/profile instead of logging in every run
- Navigating directly to the test guild and channel
- Running slash commands against the locally running bot
- Verifying the rendered bot response in Discord

## Commands that worked here

Open the configured channel in a visible persistent browser:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
playwright-cli open "$DISCORD_TEST_SERVER_URL" --persistent --profile "$REPO_ROOT/.playwright-cli/profile"
```

Save storage state inside the workspace after login:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
playwright-cli state-save "$REPO_ROOT/.playwright-cli/discord-state.json"
```

Inspect current page state:

```bash
playwright-cli snapshot
playwright-cli screenshot
playwright-cli eval "() => ({ title: document.title, url: location.href })"
```

Type and submit a slash command:

```bash
playwright-cli type "/help"
playwright-cli press ArrowDown
playwright-cli press Enter
playwright-cli press Enter
```

## Recommended workflow

1. Ensure the local bot is already running and connected to Discord.
2. Check `DISCORD_TEST_SERVER_URL`.
3. If it is missing, ask the user for the target Discord channel URL and set the variable.
4. Open Discord with `playwright-cli open ... --persistent --profile ...`.
5. If Discord is not authenticated, have the user log in manually. You'll need to open the window with `--headed` to allow interaction.
6. Save storage state to `.playwright-cli/discord-state.json` after authentication.
7. Confirm the page title and URL match the expected Discord guild/channel before issuing commands.
8. Type the slash command into the message composer for the current channel.
9. If multiple apps expose the same slash command, use your judgment to select the locally running bot rather than another environment.
10. Verify the rendered response from the latest page snapshot or screenshot.

## Important details

- Always use `--headed` when the user needs to interact with Discord login.
- Prefer `--persistent` with a repo-local `--profile` directory so Discord auth survives across sessions.
- Save auth state inside the workspace, not in an arbitrary home-directory path, or file access may be denied.
- Keep all Playwright artifacts under `$REPO_ROOT/.playwright-cli/`.
- Discord may show multiple apps for the same slash command. Prefer the option that appears to target the locally running bot.
- `playwright-cli` key names are case-sensitive: use `Enter`, not `enter`.
- A cleared composer after submission is a good sign that Discord accepted the command.

## Verification patterns

Use these checks after sending a command:

- `playwright-cli snapshot` to inspect the latest rendered message
- `playwright-cli screenshot` for visual confirmation
- `playwright-cli eval` for targeted DOM inspection when the snapshot is too large

Example DOM inspection:

```bash
playwright-cli eval "() => { const input = document.querySelector('[contenteditable=\"true\"][role=\"textbox\"][aria-label^=\"Message\"]'); return { inputText: input?.textContent || '' }; }"
```

If the composer still contains the slash command text, the interaction likely stopped before full submission.

## Guardrails

- Use this only for smoke tests of real Discord behavior.
- Do not automate Discord credential entry unless the user explicitly asks; prefer manual login.
- Do not rely on Discord browser automation as the primary test suite.
- Keep the automation scoped to the configured test guild and channel.
- Do not commit `.playwright-cli/` artifacts.

## Example prompt

```text
Use playwright-cli to open the Discord channel from DISCORD_TEST_SERVER_URL, run /help against the locally running bot, and verify the help message renders.
```
