# Wasabi Bot

Wasabi Bot is a Rust Discord bot built with poise and serenity. Keep this file short and use it as onboarding context; read the referenced files only when they are relevant to the task.

## What This Repo Contains

- `src/`: Rust bot source, plus `src/bin` one-shot binaries for migrations
- `migrations/`: sqlx migrations
- `tests/`: Rust integration tests
- `apphost.cs`: Aspire orchestration for PostgreSQL, migrations, and the bot
- `lavalink/`: legacy Lavalink deployment assets kept for future music work

## How To Work In This Repo

- Prerequisites: .NET SDK for Aspire, Aspire CLI 13, Rust toolchain, and Docker
- Run Rust checks with `cargo test` from the repo root
- Start the app from the repo root with `aspire start`
- The `main` branch worktree can be kept beside this checkout for reference while migrating old features

## Where To Look Next

- `README.md`: local setup and developer expectations
- `apphost.cs`: Aspire resources, parameters, and local orchestration
- `RUST_REWRITE.md`: migration scope and backlog
- `.github/workflows/tests.yml`: CI validation commands
