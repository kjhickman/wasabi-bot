# De-Slop Plan

This plan prepares Wasabi Bot for music dashboard development without changing the dashboard's current appearance. Work should be delivered in small, behavior-preserving phases.

## Guiding Rules

- Preserve dashboard markup, classes, component order, and appearance.
- Move existing code before redesigning it.
- Do not add service traits, repositories, factories, or one-file-per-command structures without a concrete need.
- Treat line count as a signal, not a violation. Declarative UI may legitimately be long.
- Keep ordinary compiler and Clippy warnings fatal while initially reporting `pedantic`, `nursery`, and `cargo` findings as warnings.
- Verify every phase independently before starting the next one.

## Current Findings

- `src/web/auth.rs` is the clearest god file and mixes routes, OAuth transport, persistence, crypto, and maintenance.
- Voice state and behavior span `commands`, `bot`, and `web`; `web` currently calls into `commands::music`.
- `src/web/app/dashboard.rs` mixes access policy, rendering, browser behavior, and route support.
- `src/commands/music.rs` mixes Poise handlers, Lavalink operations, voice coordination, and response formatting.
- CI only runs `cargo test` through deployment workflows, not directly on pull requests.
- The project has no committed Clippy policy, pinned Rust toolchain, dependency audit, coverage, or complexity reporting.
- Dashboard presentation components are already reasonably divided. Their markup should not be reorganized merely because the files are long.

## Phase 1: Establish Quality Gates

Update project tooling before structural work so later phases cannot silently regress behavior.

Files likely involved:

- `Cargo.toml`
- `.github/workflows/tests.yml`
- `.github/dependabot.yml`
- `Dockerfile`
- New `rust-toolchain.toml`
- Possibly new `deny.toml`

Initial lint policy:

```toml
[lints.rust]
unsafe_code = "forbid"

[lints.clippy]
pedantic = { level = "warn", priority = -1 }
nursery = { level = "warn", priority = -1 }
cargo = { level = "warn", priority = -1 }
multiple_crate_versions = "allow"
```

Actions:

1. Pin one stable Rust version locally, in CI, and in the Docker build.
2. Add `rust-version` and `publish = false` to `Cargo.toml`.
3. Run formatting, Clippy, and tests on pull requests and pushes, not only deployments.
4. Add `--locked` to CI, deployment, and Docker Cargo commands.
5. Resolve the existing `large_enum_variant` finding around `LoadedTracks` in `src/commands/music.rs`.
6. Add Cargo and GitHub Actions Dependabot entries.
7. Add scheduled `cargo audit`; introduce `cargo deny` only after inventorying actual licenses and duplicate dependencies.

Keep default warnings fatal while the extra lint groups remain advisory. This may require separate Clippy invocations:

```bash
cargo fmt --all -- --check
cargo clippy --locked --all-targets --all-features --no-deps -- -D warnings \
  -A clippy::pedantic -A clippy::nursery -A clippy::cargo
cargo clippy --locked --all-targets --all-features --no-deps -- \
  -W clippy::pedantic -W clippy::nursery -W clippy::cargo \
  -A clippy::multiple_crate_versions
cargo test --locked --all-targets --all-features
```

## Phase 2: Fix Neutral Ownership Boundaries

### UI events

Move `UiEvent` and `UiEvents` from `src/web.rs` into `src/ui_events.rs`. This removes the current `commands -> web` dependency.

### Voice primitives

Move shared voice ownership from `src/commands/mod.rs` into `src/voice.rs`:

- `VoiceLocks`
- `VoiceStates`
- `VoiceState`
- `lock_guild_voice`
- Guild ID conversion
- Shared state transition helpers
- Repeated bot voice-channel lookup where practical

Update `commands`, `bot`, and `web` to depend on the neutral voice module. Do not move all music behavior or introduce a `MusicService` trait in this phase.

Verification:

- Existing tests pass unchanged.
- No route or command behavior changes.
- `commands` no longer imports event types through `web`.
- `web` no longer imports voice state ownership through `commands`.

## Phase 3: Separate Dashboard Policy From Presentation

Move access resolution from `src/web/app/dashboard.rs` into:

```text
src/web/app/voice/access.rs
```

Move these items and their tests together:

- `Access`
- `ChannelAccess`
- `JoinTarget`
- `VoiceDecision`
- `resolve_access`
- `join_target`
- `ready_target`

Both the dashboard and `/voice/join` should use the voice access module.

Keep unchanged:

- `VOICE_UPDATES_SCRIPT`
- Dashboard layout markup
- Tailwind classes
- Status panel markup
- Existing component markup

Verification:

- Existing access tests pass.
- Rendered dashboard HTML remains equivalent.
- `/voice/join` no longer imports policy from the dashboard presentation module.

## Phase 4: Split Bot Voice Maintenance

Convert `src/bot.rs` into a small module directory without changing behavior:

```text
src/bot/
|-- mod.rs
|-- voice_maintenance.rs
`-- interactions.rs
```

Move voice maintenance and disconnect behavior into `voice_maintenance.rs`, and interaction persistence mapping into `interactions.rs`.

Keep in `bot/mod.rs`:

- Bot startup
- Framework construction
- Event dispatch
- Error handling

Do not add a generalized event-handler framework or traits.

## Phase 5: Split Authentication by Concrete Responsibility

Refactor `src/web/auth.rs` incrementally:

```text
src/web/auth/
|-- mod.rs
|-- crypto.rs
|-- discord.rs
|-- session_store.rs
`-- maintenance.rs
```

Responsibilities:

- `mod.rs`: login, callback, logout, and session orchestration.
- `crypto.rs`: token encryption/decryption and token utilities.
- `discord.rs`: OAuth exchange and Discord HTTP models/calls.
- `session_store.rs`: OAuth-state and session SQL.
- `maintenance.rs`: cleanup, guild refresh, and token refresh tasks.

Use plain feature-local functions rather than repository traits.

Special review target: token refresh currently appears to hold a database transaction and row lock during a remote request. Preserve behavior during the mechanical split, then evaluate narrowing the transaction separately with concurrency tests.

Verification:

- Encryption round-trip and tamper tests remain.
- Add focused tests for OAuth-state consumption, expired sessions, CSRF rejection, and refresh coordination.
- Route wrappers continue exposing the same paths.

## Phase 6: Create the Dashboard Data Seam

Parameterize the existing static panels without changing their generated structure or styles.

Use minimal concrete view models, such as:

```rust
struct DashboardView { /* access, playback, queue, recommendations */ }
struct PlaybackView { /* existing displayed fields */ }
struct QueueItemView { /* existing queue fields */ }
struct SearchResultView { /* existing recommendation fields */ }
```

Initially populate them with the exact current fake values. Components should only render supplied values; do not add a generic component framework.

Verification:

- Compare connected-state HTML before and after.
- Check desktop/mobile and light/dark rendering.
- All controls remain disabled.
- No real Lavalink data is introduced in this phase.

## Phase 7: Extract Shared Music Operations

Once the rendering seam exists, extract concrete Lavalink and Songbird operations from `commands/music.rs`:

```text
src/music/
|-- mod.rs
|-- tracks.rs
`-- snapshot.rs
```

The shared music module should own:

- Player lookup
- Search/load policy
- Enqueue operations
- Pause/resume
- Skip/stop
- Queue mutations
- Join/leave
- Snapshot reads
- Voice-state updates
- Music invalidation publication

Poise handlers become thin adapters that resolve Discord context, authorize the voice channel, call concrete music functions, and format Discord responses. Web handlers later use the same functions with session and CSRF authorization.

Do not create one module per command or a trait with one implementation.

Verification:

- Existing slash-command behavior and responses remain.
- `web` no longer calls `commands::music::join_voice_channel`.
- Track query tests move with track-loading code.
- Add one focused test per nontrivial shared operation where mocking is unnecessary.

## Phase 8: Introduce Complexity Reporting

After boundaries and tests stabilize, generate coverage and CRAP reports:

```bash
cargo llvm-cov --locked --all-features --lcov --output-path lcov.info
cargo crap --lcov lcov.info --top 20
cargo crap --lcov lcov.info --summary
```

Use `cargo-crap` as reporting first because Topcoat macros, async functions, and generated Poise handlers may distort results.

Rollout:

1. Generate and inspect an initial report.
2. Exclude or accept macro-generated noise.
3. Add tests for high-CRAP production policy functions.
4. Commit a stable baseline.
5. Gate regressions rather than an arbitrary absolute score.
6. Add an absolute threshold only if the report proves reliable.

Priority targets:

- OAuth callback and token refresh
- Voice maintenance
- Track loading
- Dashboard access resolution
- SSE event filtering

## Deferred Cleanup

Do not include these in the initial phases:

- Splitting `commands/fun.rs` unless one of those commands next changes.
- Rewriting the inline dashboard script.
- Adding a frontend framework or JSON API.
- Creating one component per small UI fragment.
- Eliminating all duplicate transitive crate versions.
- Refactoring `llm.rs` or `db.rs`, which are currently cohesive enough.
- Wiring real dashboard controls before shared music operations exist.

## Execution Order

1. Quality gates and toolchain.
2. Neutral UI event and voice ownership.
3. Dashboard access-policy extraction.
4. Bot voice-maintenance split.
5. Authentication split.
6. Dashboard view models with identical fake values.
7. Shared music operations.
8. CRAP and coverage regression reporting.
9. Begin functional dashboard work.
