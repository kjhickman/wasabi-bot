# Wasabi bot

## Development

### Dependencies

- .NET 9 SDK
- Docker
- Ngrok account with static subdomain
- [Taskfile](https://github.com/go-task/task)

### Creating a Discord application
TODO: guide on creating discord application

### Environment variables
Using `env.sample` as a guide, create a `.env` file with all of the variables set.

### .NET User Secrets
You'll need to add a few secrets to the WasabiBot.Web project for it to run locally properly:

Run these from the WasabiBot.Web directory:
```
dotnet user-secrets set "Discord:Token" *your-bot-token*
dotnet user-secrets set "Discord:PublicKey" *your-bot-public-key*
dotnet user-secrets set "Discord:TestGuildId" *your-test-guild-id*
```

### ngrok
You'll need to setup a [static ngrok domain](https://ngrok.com/blog-post/free-static-domains-ngrok-users) and authenticate with it locally.

### Running locally

You can simply run `task up` or `docker compose up` to run everything locally, including ngrok, postgres and the aspire dashboard.

If you want to debug, you can run `task debug` which will start everything except the web server, which you can then debug locally.

Once you're running, you'll need to go to the Discord developer portal and set you bot's webhook url to the static ngrok domain. Discord will send some test pings, so if you're not running this will fail.

### Run tests

From the root of the solution:
```bash
dotnet test
```