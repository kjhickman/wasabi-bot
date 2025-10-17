# Wasabi Bot

Wasabi Bot is a .NET Discord bot built using [NetCord](https://github.com/NetCordDev/NetCord).

### Setting up local dev

Requires .NET 9.0 SDK

```
dotnet user-secrets set "Discord:Token" "your-bot-token"
dotnet user-secrets set "Gemini:ApiKey" "your-openai-apikey"
```

And that's it! You can run the bot locally using the Aspire AppHost:

```
dotnet run --project Wasabi.Bot/Wasabi.Bot.AppHost.csproj --launch-profile http
```
