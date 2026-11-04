# Discord bot

A Discord Gateway bot on Cloudflare Workers.

## Run and deploy

Test the bot locally; there is no deployed dev environment. Fill `.dev.vars` (from `.dev.vars.example`) with the **dev application's** settings, then run `npm run dev`. It starts the Worker, connects to Discord, and syncs the dev application's slash commands when they've changed. Stop it with Ctrl-C.

`/radio` runs each guild's voice connection in a container, so `npm run dev` and `npm run deploy` need Docker running (Docker Desktop or Colima). The first run builds the image, which takes a minute. For a few minutes after a deploy that changes the image, `/radio play` may say the radio couldn't start while Cloudflare rolls the image out.

## Commands

| Command          | Purpose                                                        |
| ---------------- | -------------------------------------------------------------- |
| `npm install`    | Install dependencies and generate Worker types                 |
| `npm run dev`    | Run locally and connect                                        |
| `npm run check`  | Check formatting, lint, and types (`npx vp check --fix` fixes) |
| `npm test`       | Run tests                                                      |
| `npm run deploy` | Deploy prod                                                    |

Rerun `npm install` after changing `wrangler.jsonc` to regenerate Worker types.
