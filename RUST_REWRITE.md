# Rust Rewrite Plan

Rewrite Wasabi Bot (currently .NET 10 / NetCord) in Rust. Start with a small MVP, then rebuild features incrementally. The Rust crate now lives at the repo root. Aspire stays as the local orchestrator via the Community Toolkit Rust integration.

## Stack

| Concern | Choice |
|---|---|
| Discord | `serenity` + `poise` (slash commands, context menu commands, gateway) |
| Async runtime | `tokio` |
| Database | `sqlx` (postgres, compile-time checked queries), `sqlx migrate` for migrations |
| HTTP server (health checks) | `axum` |
| Telemetry | `tracing` + `opentelemetry-otlp` (reads `OTEL_EXPORTER_OTLP_ENDPOINT` injected by Aspire) |
| Orchestration | Aspire AppHost (`apphost.cs`) + `CommunityToolkit.Aspire.Hosting.Rust` (`AddRustApp`) |

## Layout

```
Cargo.toml
apphost.cs                  # Aspire AppHost
migrations/                 # sqlx migrations (0001_interactions.sql)
src/
  main.rs                   # process wiring
  bot.rs                    # Discord gateway + command registration
  web.rs                    # axum routes + server
  bin/migrate.rs            # standalone migration runner (Aspire resource, replaces WasabiBot.Migrations)
  commands/                 # command definitions
  db.rs                     # pool + interaction persistence
tests/                      # Rust integration tests
```

Existing leftover scaffolding in `wasabi-bot-api/` moves into this layout.

## MVP Scope

Explicitly out: reminders, music/radio/Lavalink, all LLM commands (`/ask`, `/caption`, `/conch` LLM path), web frontend, auth/OAuth/JWT, REST API, `ApiCredentials`/`MusicFavorites`/`GuildTrackPlays`/`DataProtectionKeys` tables.

In:

1. **Bot skeleton** — serenity/poise gateway connection, `DISCORD_TOKEN` env var, guild command registration.
2. **DB-free commands** — `/flip`, `/choose` (2–7 distinct options, case-insensitive dedupe), `/help`, `Mock` (message context-menu, SpOnGeBoB-casing), `/conch` (weighted-random fallback table only: Yes 44, No 32, "I don't think so" 12, Maybe 9, "Try asking again" 3 — no LLM).
3. **Interactions table + logging** — single sqlx migration reproducing the current schema (`Id` bigint PK not generated, `ChannelId`, `ApplicationId`, `UserId`, `GuildId` nullable, `Username`, `GlobalName`, `Nickname`, `Data` jsonb, `CreatedAt` timestamptz). Persist every interaction from the gateway handler, same as `InteractionCreatedEventHandler` does today. Snowflakes stored as signed bigint (matches existing data).
4. **`/stats`** — total interactions, per-channel count, most-used command (parsed from `Data` jsonb), top user. First DB-read command; proves sqlx queries against jsonb.
5. **Health endpoint** — tiny axum server on `PORT` (injected by `WithHttpEndpoint(env: "PORT")`) serving `/health` and `/alive` so Aspire shows the resource healthy.
6. **Telemetry basics** — `tracing` subscriber with OTLP export so logs/traces show up in the Aspire dashboard.
7. **Aspire wiring** — use `apphost.cs`; add `#:package CommunityToolkit.Aspire.Hosting.Rust@*`; replace the API + migrations project resources with `AddRustApp("wasabi-bot", ".")` and a migrate resource; keep PostgreSQL + PgWeb; drop Lavalink, frontend bun steps, Google/Discord OAuth params from the apphost for now (keep `discord-bot-token`).
8. **Tests** — unit tests inline (`#[cfg(test)]`) for choose/mock/conch-weights logic; one integration test hitting Postgres via `testcontainers` for interaction insert + stats query. Update `.github/workflows/tests.yml` to run `cargo test`.

### MVP build order

1. Cargo workspace + `apphost.cs` rewrite → verify: `aspire start` shows postgres + migrate + rust app, health check green.
2. Migration + interactions insert on ready/interaction events → verify: row appears in PgWeb after any interaction.
3. `/flip`, `/help`, `/choose`, `Mock`, `/conch` → verify: smoke test via Discord web (discord-playwright-testing skill).
4. `/stats` → verify: returns counts matching DB.
5. Telemetry → verify: logs/traces visible in Aspire dashboard.
6. Tests + CI → verify: `cargo test` passes locally and in CI.

## Full Feature Inventory (post-MVP backlog)

Everything the .NET app does today, to be re-added (or consciously dropped) after the MVP.

### Bot commands
- [ ] `/ask <question>` — Gemini with Google Search grounding (native GenAI API, not OpenAI-compat)
- [x] `/caption <image>` — LLM meme caption; image content-type validation (jpeg/png/gif/webp), 10MB limit, HTTP image download
- [ ] `/conch` LLM path — LLM yes/no answer with weighted-random fallback tool (MVP ships fallback only)
- [ ] `/reminder <when> <message>` — LLM natural-language time parsing (America/Chicago anchor, ISO 8601), reject past times
- [ ] `/reminder-list` — pending/processing reminders, ephemeral, `<t:...:R>` timestamps
- [ ] `/reminder-rm <id>` — ownership check, set status Canceled
- [ ] `/play` — Lavalink playback, SoundCloud search (YouTube disabled), playlist support, preview-track filter
- [ ] `/radio` — radio-browser.info search with custom ranking, try up to 5 stations
- [ ] `/skip`, `/stop`, `/queue`, `/nowplaying`, `/leave`

### Bot infrastructure behaviors
- [ ] Auto-defer interaction responses (respond within ~2s or defer + follow-up — Discord 3s ack deadline). *poise handles defer explicitly; decide per-command.*
- [ ] Voice-state handler: auto-disconnect when bot is alone in a voice channel
- [ ] Music inactivity tracker (idle 15 min / paused 60 min disconnect)
- [ ] Per-guild music queue mutation locking
- [ ] Shared-voice-channel enforcement (user must be in bot's channel)

### Background services
- [ ] Reminder dispatcher — batch claim with `FOR UPDATE SKIP LOCKED`, Postgres `LISTEN/NOTIFY reminders_changed` wake signal, retry/attempt tracking, send via Discord REST

### Database tables (beyond `Interactions`)
- [ ] `Reminders` (status enum-as-text, `(Status, DueAt)` + `(UserId, Status, DueAt)` indexes)
- [ ] `ApiCredentials` (client-id unique, peppered secret hash)
- [ ] `MusicFavorites` (jsonb metadata, unique `(DiscordUserId, Kind, ExternalId)`)
- [ ] `GuildTrackPlays` (play-count stats, radio excluded)
- [ ] Data-protection/session key storage (only if the web frontend returns)

### HTTP API
- [ ] `POST /api/v1/oauth/token` — client_credentials → JWT (HMAC signing key config)
- [ ] `GET /api/v1/interactions` + `/{id}` — JWT-protected, cursor pagination (CreatedAt+Id, asc/desc)
- [ ] OpenAPI spec endpoint
- [ ] Discord OAuth login (`/login-discord`, `/logout`), cookie auth, guild-membership authorization policy

### Web frontend
- [ ] Music dashboard (live queue/control/search), library/favorites, API credentials management page. *Decide: rebuild (htmx/leptos/yew/SPA) or drop.*

### Cross-cutting
- [ ] Metrics: custom LLM response-duration histogram (`ai.llm.response.duration`), runtime/HTTP metrics
- [ ] Distributed tracing spans per command/service (partially in MVP)
- [ ] Structured JSON logging with trace correlation (partially in MVP)
- [ ] HTTP resilience (retries/timeouts) for outbound calls
- [ ] Health checks beyond liveness (DB connectivity)

### Ops / deployment
- [x] Fly.io deploy workflows (staging on push to main, prod manual), migrations via flyctl proxy step
- [x] Dockerfile for the Rust app (`--api-container` equivalent)
- [ ] Lavalink shared app deploy (when music returns)
- [x] Remove .NET projects/solution once parity reached; drop `src/`, `test/`, source generators, Bun/Tailwind toolchain (unless frontend returns)

### Testing parity
- [ ] Unit tests per command (ports of the TUnit suites)
- [ ] Integration tests: interactions service, reminders service, credentials persistence (testcontainers + real migrations, reset between tests)
- [x] CI matrix update: replace .NET SDK/Bun setup with Rust toolchain + cargo caching
