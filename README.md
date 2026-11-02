# Discord bot

A Discord Gateway bot on Cloudflare Workers.

## Run and deploy

Test the bot locally; there is no deployed dev environment. Fill `.env` (from `.env.example`) and `.dev.vars` (from `.dev.vars.example`) with the **dev application's** settings, then run `npm run dev`. It syncs the dev application's slash commands, starts the Worker, and connects to Discord. Stop it with Ctrl-C.

For production, fill `.env.prod` and `.dev.vars.prod` with the **production application's** settings. `.env.prod` also needs `GATEWAY_URL=https://discord-bot-prod.<your-subdomain>.workers.dev` and the same `GATEWAY_CONTROL_TOKEN` as `.dev.vars.prod`. `npm run deploy` builds and deploys the Worker with those secrets, then syncs the production slash commands. Production connects automatically on first creation. Control it with:

```sh
npm run gateway -- status
npm run gateway -- stop
npm run gateway -- start
```

## Commands

| Command                                  | Purpose                                                        |
| ---------------------------------------- | -------------------------------------------------------------- |
| `npm install`                            | Install dependencies and generate Worker types                 |
| `npm run dev`                            | Sync dev commands, run locally, and connect                    |
| `npm run check`                          | Check formatting, lint, and types (`npx vp check --fix` fixes) |
| `npm test`                               | Run tests                                                      |
| `npm run deploy`                         | Deploy prod and sync its commands                              |
| `npm run gateway -- start\|stop\|status` | Control prod                                                   |

Rerun `npm install` after changing `wrangler.jsonc` to regenerate Worker types.
