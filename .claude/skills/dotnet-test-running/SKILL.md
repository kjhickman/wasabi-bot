---
name: dotnet-test-running
description: "Runs Wasabi Bot automated tests through the TUnit test executables with dotnet run. USE FOR: running unit or integration tests, discovering test names, filtering by class or test name, reproducing failures locally, CI-like test runs. DO NOT USE FOR: browser smoke tests, Aspire app orchestration, Discord/manual frontend verification. INVOKES: bash."
---

# Dotnet Test Running Skill

Use this skill to run automated tests in this repository.

This repo uses TUnit test executables. Prefer `dotnet run --project ...`, not `dotnet test --filter ...`.

## Commands

Full unit suite:

```bash
dotnet run --project test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj -- --no-ansi
```

Full integration suite:

```bash
dotnet run --project test/WasabiBot.IntegrationTests/WasabiBot.IntegrationTests.csproj -- --no-ansi
```

CI-style run:

```bash
dotnet run --project test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj -c Release --no-restore -- --no-ansi
```

Discover tests:

```bash
dotnet run --project test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj -- --list-tests --no-ansi
```

Run one class:

```bash
dotnet run --project test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj -- --treenode-filter "/*/*/DiscordGuildRequirementHandlerTests/*" --minimum-expected-tests 3 --no-ansi
```

Run one test:

```bash
dotnet run --project test/WasabiBot.UnitTests/WasabiBot.UnitTests.csproj -- --treenode-filter "/*/*/*/HandleAsync_ShouldCacheGuildListAndMembershipChecksAcrossRequests" --minimum-expected-tests 1 --no-ansi
```

## Rules

- Pass runner args after `--`.
- Use `--treenode-filter`, not `--filter`.
- Class pattern: `/*/*/ClassName/*`
- Test pattern: `/*/*/*/TestName`
- Use `*` for wildcards.
- Use `--list-tests` if the exact name is unclear.
- Add `--minimum-expected-tests N` when filtering so zero-match filters fail.
