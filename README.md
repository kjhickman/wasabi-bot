# Discord bot

A Discord Gateway bot on Cloudflare Workers.

## Run and deploy

For local development, `npm run dev` uses `.dev.vars`, dev mode, and no cron. Set `.env`'s `GATEWAY_URL` to the local URL printed by Vite (normally `http://localhost:5173`). The Gateway stays off until explicitly started:

```sh
npm run gateway -- start
npm run gateway -- status
npm run gateway -- stop
```

For deployed dev, use `npm run deploy`, which builds `discord-bot-dev` and uploads runtime secrets from `.dev.vars`. Set `.env`'s `GATEWAY_URL` to `https://discord-bot-dev.<your-subdomain>.workers.dev`; the same dev CLI commands then control that deployment. Stop local dev before switching targets to avoid two connections for the dev bot.

For production, fill `.env.prod` and `.dev.vars.prod` with the **production application's** settings, set `GATEWAY_URL` to `https://discord-bot-prod.<your-subdomain>.workers.dev`, and deploy with `npm run deploy:prod`. Production defaults enabled on first creation, so be ready for it to connect automatically. Use:

```sh
npm run gateway:prod -- status
npm run gateway:prod -- stop
npm run gateway:prod -- start
```

## Commands

| Command                                       | Purpose                                           |
| --------------------------------------------- | ------------------------------------------------- |
| `npm install`                                 | Install dependencies                              |
| `npm run dev`                                 | Run locally                                       |
| `npm test`                                    | Run tests                                         |
| `npm run check`                               | Check formatting, lint, and types                 |
| `npm run format`                              | Format files                                      |
| `npm run build`                               | Build the Worker                                  |
| `npm run preview`                             | Preview the build locally                         |
| `npm run deploy`                              | Deploy dev with local Worker secrets              |
| `npm run deploy:prod`                         | Deploy prod with `.dev.vars.prod` secrets         |
| `npm run types`                               | Regenerate Worker types after config changes      |
| `npm run commands:register`                   | Sync global commands, deleting any not listed     |
| `npm run commands:register:prod`              | Sync the production application's global commands |
| `npm run gateway -- start\|stop\|status`      | Control local/deployed dev using `.env`           |
| `npm run gateway:prod -- start\|stop\|status` | Control prod using `.env.prod`                    |
