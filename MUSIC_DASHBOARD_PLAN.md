# Music Dashboard Plan

## Goal

Build a frontend music dashboard that starts as a practical live queue view and evolves into the primary music UX for Wasabi Bot.

The dashboard should first solve visibility and usability problems around current music playback. UI polish comes last. Early phases should prioritize useful functionality over presentation.

## Product Decisions

- Favorites are saved per Discord user.
- Favorites should likely be split between songs and radio stations.
- Most played should be tracked by guild.
- Most played should show all-time top tracks only for now.
- Search should support both SoundCloud tracks and radio stations.
- The dashboard should continue using the rule that the user must be in the same voice channel as the bot to view and control the active session.

## Current State

Already implemented:

- `/music` page exists.
- The music page is the only `InteractiveServer` page.
- The page shows the current voice session when the signed-in user is in the same voice channel as the bot.
- The page shows now playing and queue.
- The page polls for updates.

Current gaps relative to the long-term vision:

- No progress bar.
- No artwork.
- No source link.
- No web playback controls.
- No search UX.
- No persistence for favorites.
- No persistence for most played.
- No queue management beyond read-only display.

## Guiding Principles

- Keep the rest of the site static unless there is a clear need for more interactive pages.
- Prefer server-side service reuse over duplicating music logic specifically for the web dashboard.
- Keep early UI simple and even ugly if needed.
- Separate read models from mutation actions.
- Avoid coupling the UI too tightly to SoundCloud-specific behavior, even if SoundCloud is the first track search implementation.
- Introduce music-specific persistence only when favorites and most-played are ready.

## Roadmap

## Phase 1: Richer Read-Only Playback

Goal: make the dashboard genuinely useful as a live monitoring surface.

Features:

- Progress bar (view-only)
- Elapsed time and total duration
- Album artwork if available
- Source link
- Richer now playing metadata
- Richer queue metadata
- Playback state display such as playing, paused, idle

Backend work:

- Extend the dashboard snapshot model with:
  - current playback position
  - total duration
  - artwork URL
  - source URL
  - source name
  - playback state
- Use Lavalink/player state for playback position and current state.
- Surface metadata only when available so the UI degrades gracefully.

Exit criteria:

- The page can show a live progress bar for the active track.
- The page can show artwork and a source link when track metadata provides them.
- The page clearly shows when playback is paused or idle.

## Phase 2: Basic Web Controls

Goal: make the dashboard useful for common playback actions.

Features:

- Pause / resume
- Skip
- Stop

Backend work:

- Add server-side control actions for pause/resume, skip, and stop.
- Reuse existing music services where practical.
- Enforce that the user is currently in the same voice channel as the bot before allowing actions.

Exit criteria:

- The user can perform the core playback actions from the dashboard.
- The queue and now playing state refresh correctly after each action.

## Phase 3: Search and Queueing

Goal: make the dashboard better than slash commands for discovery and queueing.

Features:

- Search songs
- Show top X search results
- Add to queue
- Play next

Backend work:

- Introduce a music search service for web use.
- Return structured search result models with fields such as:
  - title
  - artist or station name
  - duration
  - artwork URL
  - source URL
  - source type
  - playability hints when useful
- Add queue mutation actions for add to queue and play next.

UI direction:

- Search should support both tracks and radio stations.
- Recommended MVP UI for mixed search:
  - one search box
  - two result sections when applicable:
    - Songs
    - Radio stations
- Each result row should have actions appropriate to the result type.

Why separate sections is the recommended starting point:

- SoundCloud tracks and radio stations are different content types.
- Radio results likely need different metadata and labels.
- This keeps the implementation simpler than building a fully unified ranking model immediately.

Later alternative if needed:

- one unified result list with badges like `Song` and `Radio`

Exit criteria:

- A user can search from the dashboard and add either a song or radio station to playback.
- A user can choose to add a result to queue or play it next.

## Phase 4: Favorites

Goal: let users build a reusable personal music library.

Features:

- Favorite songs
- Favorite radio stations
- View favorites list
- Add favorite to queue
- Play favorite next
- Remove favorite

Data model direction:

- Keep favorites per Discord user.
- Split favorites into two categories:
  - song favorites
  - radio favorites

Recommended implementation shape:

- One table with a `FavoriteKind` discriminator, or two separate tables if that proves simpler.
- Persist enough display metadata to render favorites even if live lookups later fail.

Suggested stored fields for song favorites:

- Discord user ID
- source type
- source track identifier if available
- source URL
- title
- artist
- artwork URL
- duration when available

Suggested stored fields for radio favorites:

- Discord user ID
- station identifier
- station name
- stream URL if stable and safe to persist
- homepage URL if available
- favicon/artwork URL if available
- tags/genre if useful

Important note:

- Do not rely on Lavalink encoded track strings as the persistent identity for favorites.
- Persist a stable source identity plus enough fallback display data.

Exit criteria:

- A user can save both songs and radio stations as favorites.
- A user can revisit favorites and start playback without re-searching.

## Phase 5: Most Played

Goal: provide useful shared discovery at the guild level.

Features:

- Most played tracks by guild
- All-time ranking only

Data model direction:

- Track counts should be stored per guild.
- Only songs should be included initially unless radio counting proves equally valuable.

Recommended tracking rule:

- Increment play counts when a track meaningfully starts or completes.
- Do not increment on queue alone.

Implementation notes:

- This likely needs player event handling rather than dashboard polling.
- Reuse the same normalized track identity strategy chosen for favorites.

Exit criteria:

- Each guild can view its all-time most played tracks.
- The ranking is based on actual playback, not queue events.

## Phase 6: Queue Management

Goal: make the dashboard the primary control surface.

Features:

- Remove queued track
- Move queued track up/down
- Clear queue
- Optional later drag-and-drop reorder

Notes:

- This phase should come after basic controls and search because it introduces more concurrency concerns.
- Slash commands and dashboard actions may mutate the same queue, so race conditions should be expected.

Exit criteria:

- The user can manage the active queue without falling back to slash commands for common tasks.

## Phase 7: UI Polish

Goal: make the dashboard feel cohesive and pleasant after behavior is stable.

Features:

- Better visual hierarchy
- Better artwork-driven layout
- Responsive search and queue layout
- Clearer playback controls
- Better mobile usability
- General fit and finish

This phase is intentionally last.

## Cross-Cutting Technical Decisions

## 1. Track Identity

This is the most important long-term design decision.

We need a normalized identity for tracks that supports:

- favorites
- most played
- future history/recently played features

Recommended identity fields:

- source type
- source track ID when available
- source URL
- title
- artist

Do not depend on transient or source-specific runtime-only values if they are not stable across sessions.

## 2. Search Result Modeling

Search results should be modeled as structured content rather than raw Lavalink results.

Recommended result shape:

- result kind: song or radio
- display title
- subtitle
- source label
- artwork URL
- source URL
- duration when applicable
- action payload needed to queue or play next

## 3. Playback Authorization

For all dashboard actions, keep the same rule:

- the user must currently be in the same voice channel as the bot

This keeps behavior consistent with the current dashboard and reduces ambiguity about which guild/session the dashboard is controlling.

## 4. Persistence Scope

- Favorites are per Discord user.
- Most played is per guild.

## 5. Radio Support in the UI

Recommended first UI approach:

- one shared search box
- separate results sections for songs and radio stations
- distinct favorite lists for songs and radio stations

This keeps the implementation clear and avoids muddying the interaction model.

## Suggested Implementation Order

1. Extend the read-only dashboard snapshot with progress, artwork, source link, and playback state.
2. Add pause/resume, skip, and stop actions to the dashboard.
3. Add mixed search with separate song and radio result sections.
4. Add add-to-queue and play-next actions from search results.
5. Add persistence for user favorites.
6. Add song favorites UI.
7. Add radio favorites UI.
8. Add guild-scoped most played tracking and display.
9. Add queue management actions.
10. Polish the UI.

## Near-Term Next Steps

The next most useful implementation slice is:

1. progress bar
2. elapsed / total time
3. artwork when available
4. source link
5. playback state display

After that:

1. pause / resume
2. skip
3. stop

That keeps momentum on the existing dashboard before expanding into search and persistence work.
