# Adding Discord Commands

Read this when adding or modifying Discord slash commands.

## Command Structure

Commands live in `src/WasabiBot.Api/Features/<FeatureName>/`. Each command is a class with:

1. `[CommandHandler("name", "description")]` attribute
2. Constructor injection for dependencies
3. `ExecuteAsync` method with `ICommandContext` as the first parameter

## Example: Simple Command

See `src/WasabiBot.Api/Features/Choose/ChooseCommand.cs`:

- Lines 7-8: `[CommandHandler]` attribute defines the slash command name and description
- Lines 10-17: Constructor injects `Tracer` and `ILogger<T>`
- Lines 19-27: `ExecuteAsync` receives `ICommandContext` first, then command parameters as method arguments
- Optional parameters (nullable with defaults) become optional slash command options

## Key Interfaces

- `ICommandContext` — abstraction for Discord interaction context
  - `ctx.RespondAsync(message)` — public response
  - `ctx.SendEphemeralAsync(message)` — private response (only user sees it)
  - `ctx.UserDisplayName`, `ctx.ChannelId`, `ctx.UserId` — interaction metadata

## Source Generator

The `CommandHandlerGenerator` in `src/WasabiBot.Api.Generators/` automatically:

1. Discovers all `[CommandHandler]` classes
2. Generates DI registration code
3. Generates NetCord slash command definitions from method signatures

You don't need to manually register commands — just add the attribute.

## Testing Commands

Commands are testable via the `ICommandContext` abstraction:

1. Create a `FakeCommandContext` (see `test/WasabiBot.UnitTests/Infrastructure/Discord/FakeCommandContext.cs`)
2. Instantiate the command with mocked dependencies
3. Call `ExecuteAsync` and assert on `context.Messages`

See `test/WasabiBot.UnitTests/Features/MagicConch/MagicConchCommandTests.cs` for examples.

## Checklist

- [ ] Create feature folder under `src/WasabiBot.Api/Features/`
- [ ] Add command class with `[CommandHandler]` attribute
- [ ] Implement `ExecuteAsync` with `ICommandContext` as first parameter
- [ ] Add unit tests using `FakeCommandContext`
