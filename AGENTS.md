# AGENTS.md

A Discord Gateway bot running on Cloudflare Workers. See `README.md` for setup and commands.

## Stack

- TypeScript on Cloudflare Workers (`wrangler.jsonc`), with Hono for HTTP routes
- A Durable Object (`DiscordGateway`, SQLite storage) holds the single Discord Gateway WebSocket. In prod, a once-a-minute cron makes sure it stays connected.
- OpenAI handles chat replies when someone mentions the bot, and some slash commands use it too
- `discord-api-types` for Discord types
- Vite+ (`vp`) for dev, build, format, lint, and tests (Vitest). `tsrs` for type checking.

## Layout

- `src/index.ts`: Worker entry (fetch and scheduled handlers, DO export)
- `src/app.ts`: `/gateway/{status,start,stop}` control routes, protected by a bearer token
- `src/gateway/`: the Durable Object lifecycle, the WebSocket connection, and interaction handling
- `src/chat.ts`: replies to mentions using OpenAI, with slash commands available as tools
- `src/commands/`: slash commands, registered in `index.ts`
- `scripts/`: syncs slash commands with Discord and runs the prod gateway control CLI
- `test/`: Vitest tests and fixtures

Before committing, run `npm run check` and `npm test`. After editing `wrangler.jsonc`, rerun `npm install` to regenerate the Worker types.

## Investigating issues

Production is the `prod` env (Worker `discord-bot-prod`). Use `cf` or `npx wrangler` to inspect it:

- `npm run gateway -- status`: shows the gateway's lifecycle state and its last error
- `npx wrangler tail --env prod`: streams live logs. Workers Logs keeps history; browse it in the Cloudflare dashboard for `discord-bot-prod`.
- `npx wrangler deployments list --env prod` / `npx wrangler secret list --env prod`
- `cf cli search "<task>"`: finds the right `cf` command. Then run `<command> --help`.
- `cf docs search "<question>"`: searches the Cloudflare docs
