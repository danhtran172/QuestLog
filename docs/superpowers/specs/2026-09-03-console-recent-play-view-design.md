# Console Recent Play View Design

**Date:** 2026-09-03

**Status:** Implemented

**Scope:** Playtime tracking in `MasterPlay.TrayApp`, the shared game schema and API contract, server-side library querying, and Console Mode tab orchestration in `MasterPlay.Web`

## 1. Goal

Add a Console-only library view named `console`. It is the initial view every time a Console Mode session begins, contains only games that the backend considers playable, and orders games by their most recent tracked play session.

Leaving Console Mode restores the normal library tab that was active before the session. The regular library navigation and its `Ctrl+1..4` shortcuts remain unchanged.

## 2. Last-played source of truth

Add nullable `LastPlayedAt` to `Game`. The value is UTC and is owned by the playtime tracking pipeline; clients cannot write it.

`PlayTimeService` updates `LastPlayedAt` once when a tracked game changes from not running to running. The existing `_runningGameIds` set provides the session edge, so the 5-second sampler must not write the timestamp again while the same process remains running.

The update is persisted immediately through `DataService`, independently of the 30-second accumulated-playtime flush. This deliberately records short sessions and preserves the last-played value if the Tray process stops before the next playtime flush. A failed write is logged without stopping playtime sampling; a later new running edge may retry.

Starting a game through the Host launch endpoint does not write `LastPlayedAt`. The tracked process is the source of truth, so failed launches and launchers that never reach the configured game process do not count as played.

## 3. Database evolution

- Add `DateTime? LastPlayedAt` to the shared `Game` entity and EF mapping.
- Add the nullable `LastPlayedAt` column through the existing idempotent `EnsureColumn` startup path so existing user databases upgrade in place.
- Update the template SQLite database schema for new installations.
- Existing rows remain null. No timestamp is inferred from total playtime or day-resolution diary records.

No PlaySession table is introduced. The feature needs only the latest timestamp, and the existing PlayDiary remains unchanged.

## 4. API contract

Extend the generated contract at its backend-owned source:

- `Tab` gains `console`.
- `GameListItem.lastPlayedAt` is nullable, read-only, and formatted as `date-time`.
- `GameDetail` inherits the same field.

After updating `docs/openapi.yaml`, regenerate `MasterPlay.Web/src/lib/api/types.gen.ts`; generated API files must not be edited manually.

The `console` view is fixed and does not gain a configurable sort profile. Existing sort-profile endpoints continue to accept only `all`, `planning`, and `finished`.

## 5. Server-side Console query

For `GET /api/games?tab=console`, the Host applies these rules before pagination:

1. Start from the authenticated user's enabled, non-DLC library rows already loaded by the endpoint.
2. Keep only rows satisfying the same shared predicate used to populate `GameListItem.canLaunch`.
3. Order by `LastPlayedAt DESC`, with null values after every non-null value.
4. Use `Name ASC` as the deterministic tie-breaker, including among never-played games.

The playable predicate must be extracted or shared so filtering and the returned `canLaunch` flag cannot drift apart. Frontend code must not reproduce that predicate.

The Console view ignores toolbar search, filter, sort, and reverse-sort state. Those controls are not visible in Console Mode, and stale Gallery state must not silently alter the fixed Console view. Pagination remains server-owned and uses the standard library page size.

## 6. Frontend tab model

Keep two explicit tab collections:

- regular library tabs: `all`, `planning`, `finished`, `queue`;
- Console Mode tabs: `console`, followed by the four regular tabs.

The regular header renders only the regular collection and keeps `Ctrl+1..4`. Console Mode shoulder/Q-E navigation uses the Console collection.

When entering Console Mode:

1. remember the current regular tab;
2. switch the active query to `console` and page 1;
3. keep the remembered tab unchanged while the user browses other tabs inside that Console session.

When leaving Console Mode by hold-to-exit or the existing session toggle:

1. restore the remembered regular tab and page 1;
2. clear the remembered value;
3. release fullscreen through the existing bridge.

Updated 2026-09-04: an active Console session always uses Console presentation. Desktop-only layout and Gallery switching are disabled, including L3, Menu, and the former L shortcut; their controller prompts are omitted. Returning to Gallery requires leaving the Console session and restores the pre-session tab. The `console` tab is not rendered in the normal Gallery header.

## 7. Presentation and localization

Add localized `nav.console` strings in English and Vietnamese. The Console cover view uses that label exactly like the existing tab labels; no new game metadata is inferred or fabricated in the UI.

Empty state text explains that no playable games are available. API and playtime persistence errors continue through the existing visible/logged error paths; they are not swallowed.

## 8. Tests and verification

Automated coverage must include:

- Playtime Service records one timestamp on a not-running to running edge and does not repeat it on later samples in the same session.
- A stopped then restarted process records a newer timestamp.
- The server Console view excludes every row whose returned `canLaunch` would be false.
- Console ordering is recent non-null timestamps first, then never-played games, with `Name ASC` tie-breaking.
- Console ignores search/filter/sort overrides and paginates after its fixed filter and ordering.
- Entering Console Mode selects `console`; leaving restores the pre-session regular tab.
- Q/E and LB/RB include `console` only during Console Mode; `Ctrl+1..4` and the normal header remain unchanged.
- OpenAPI generation and TypeScript typecheck pass.

Final verification follows the repository gate: frontend typecheck/test/build, relevant .NET builds/tests, and a focused manual Console Mode smoke test with the Tray tracker running.

## 9. Out of scope

- Full play-session history or a new PlaySession entity.
- User-configurable filtering or sorting for the Console view.
- Backfilling `LastPlayedAt` from existing playtime totals or PlayDiary dates.
- Writing last-played timestamps from browser or launch-button actions.
