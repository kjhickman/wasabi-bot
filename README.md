# Wasabi Bot

Wasabi Bot is a Rust Discord bot built with [poise](https://github.com/serenity-rs/poise) and [serenity](https://github.com/serenity-rs/serenity).

## Setting up local dev

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) for the Aspire AppHost
- [Aspire CLI](https://aspire.dev/get-started/install-cli/)
- Rust toolchain
- Docker

### Required parameters

The Aspire app prompts for these parameters on first run:

- `discord-bot-token`

You can enter them into the prompt when Aspire starts, or store them with Aspire user secrets for reuse.

### Getting Discord credentials

1. Create a Discord application and bot in the Discord developer portal.
2. Open the `Bot` page and reset or copy the bot token.
3. Invite the bot to a server you control so you can test commands locally.

Use these values for:

- `discord-bot-token`: bot token from the `Bot` page

The Rust app reads these runtime environment variables:

- `DATABASE_URL`: PostgreSQL URL
- `DISCORD_TOKEN`: Discord bot token, injected from the `discord-bot-token` Aspire parameter locally

### Running the bot

```bash
aspire start
```

After you provide the required parameters, Aspire will start PostgreSQL, run the Rust migrations and Discord command registration binaries, then start the bot. Once it connects to Discord, you can test it in any server where the bot has been added.
