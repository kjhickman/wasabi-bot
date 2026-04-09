# Wasabi Bot

Wasabi Bot is a .NET Discord bot built using [NetCord](https://github.com/NetCordDev/NetCord).

## Setting up local dev

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Aspire CLI](https://aspire.dev/get-started/install-cli/)
- [Bun](https://bun.sh/)
- Docker

### Required parameters

The Aspire app prompts for these parameters on first run:

- `discord-client-id`
- `discord-client-secret`
- `discord-bot-token`
- `openrouter-api-key`

You can enter them into the prompt when Aspire starts, or store them with Aspire user secrets for reuse.

### Getting Discord credentials

1. Create a Discord application and bot by following the NetCord guide: [Making a Bot](https://netcord.dev/guides/getting-started/making-a-bot.html?tabs=generic-host).
2. In the Discord developer portal, copy the application's `Client ID`.
3. Open the app's `OAuth2` settings and generate or copy the `Client Secret`.
4. Open the `Bot` page and reset or copy the bot token.
5. Invite the bot to a server you control so you can test commands locally.

Use these values for:

- `discord-client-id`: Discord application `Client ID`
- `discord-client-secret`: Discord `Client Secret`
- `discord-bot-token`: bot token from the `Bot` page

### Getting an OpenRouter API key

1. Create or sign in to an account at [openrouter.ai](https://openrouter.ai/).
2. Create an API key from the OpenRouter dashboard.
3. Use that value for `openrouter-api-key`.

### Running the bot

```bash
aspire start
```

After you provide the required parameters, Aspire will start PostgreSQL, the local Lavalink container, run the migrations project, and then start the bot API. Once it connects to Discord, you can test it in any server where the bot has been added.
