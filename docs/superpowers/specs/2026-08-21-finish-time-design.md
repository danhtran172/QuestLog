# Finish Time and Full Publish Date Design

## Goal

Replace the Gallery and Detail presentation of a finish year with a finish time that includes month and year. Publish dates display the complete day, month, and year. The Questory Web editor must support editing every finish-history entry by month and year only, while the Finish action records the current time automatically.

This design applies to the current Questory Web application (`MasterPlay.Web` hosted by `MasterPlay.Shell`) and its local API (`MasterPlay.Host`). The legacy `MasterPlay.UI` WPF surface is outside this change.

## Canonical data

`GameFinish.FinishedAt` remains the source of truth. It already stores a full `DateTime`, so this change requires no database migration and does not restore the deprecated `Game.FinishedIn` field to any new API or UI flow.

- An automatic Finish action stores the backend's current local timestamp.
- A manual month/year edit stores the first day of the selected month at `00:00:00` because the UI intentionally does not expose a day.
- Existing finish timestamps are not rewritten until the user edits that specific history entry.
- A game may keep multiple finish entries. Editing one entry never replaces, deletes, or recreates the others.

## API contract

### Read model

Replace the list/detail field `lastFinishedYear` with nullable `lastFinishedAt`, containing the latest `GameFinish.FinishedAt` as an ISO date-time. The frontend formats this value as `MM/yyyy`; it does not derive the latest record itself.

Rename the public sort key from `finishedYear` to `finishTime`. It sorts by `MAX(GameFinish.FinishedAt)` descending, places games without a finish last, and uses game name ascending as the tie-breaker. The backend may accept `finishedYear` as a temporary compatibility alias, but OpenAPI and the frontend expose only `finishTime`.

The existing finished-year filter remains a year filter and keeps its current query parameters and semantics. It matches when any finish entry has the requested year. Its label remains “Finished year / Năm hoàn thành” because the control still accepts a year rather than a month.

Rename the card hint `showFinishedIn` to `showFinishTime` for consistency with the new read model.

### Finish action

Add:

```text
POST /api/games/{gameId}/finishes
```

The request contains the user's score. Score validation follows the canonical game-score contract: half-point values from `0` through `10`, plus sentinel `999`. The endpoint executes one backend transaction:

1. Load the game and the catalog entry whose system key is `Done`; never hardcode a catalog numeric ID.
2. Update the game's current score.
3. Set its progress to `Done`.
4. Insert `GameFinish { FinishedAt = backend now, Score = submitted score }`.
5. Commit and return the refreshed `GameDetail`.

If the game or Done catalog entry is unavailable, nothing is written.

### Edit one history entry

Add:

```text
PUT /api/games/{gameId}/finishes/{finishId}
```

The request contains `finishedMonth` in strict `YYYY-MM` form. The backend verifies that the finish belongs to the route game, validates the year and month, normalizes it to the first day of that month, updates only that entry, and returns the refreshed `GameDetail`. The entry's score is unchanged.

Malformed months return `400`; missing games or finish entries return `404`. API errors use the existing structured error envelope.

## Date presentation

Add frontend formatting helpers rather than slicing ISO strings directly:

- Publish date: `dd/MM/yyyy`, for example `21/08/2026`.
- Finish time: `MM/yyyy`, for example `08/2026`.

Gallery Card uses the full Publish date and the latest Finish time. Detail Overview uses the same formats. Finish History lists every entry as `MM/yyyy` and retains its score display. Null values remain hidden under the existing card/detail visibility rules.

The publish-date editor remains a normal date input because publish date still includes a day.

## Detail actions

Move Edit into a fixed action group at the lower-right corner of the Detail popup and place Finish beside it. Both actions are icon-only buttons with localized tooltips, matching the existing Questory action-button system.

- Edit uses the existing pencil action.
- Finish uses a trophy/finish icon.
- The popup reserves bottom/right padding so the fixed actions never cover overview content, finish history, or page navigation.
- The action group stays visible while the user scrolls Tab 1.
- While the editor or Finish dialog is active, action availability follows the current dialog state so duplicate mutations cannot be submitted.

## Finish interaction

Selecting Finish opens a compact dialog that asks for the user score. The date is not shown as an editable control because the backend records the current time. The dialog remains open if the request fails and closes only after a successful response.

On success, invalidate and refresh:

- the current game detail;
- gallery list queries, including Finish time sorting;
- dashboard/count queries affected by the progress change;
- queue queries affected by moving the game to Done.

## Editing all finish history

In Game Edit mode, add a Finish History section when existing entries are present. Every history row contains:

- its score as read-only context;
- an `<input type="month">` initialized from `finishedAt`;
- an icon-only Save button with a localized tooltip.

Each Save button updates only its own finish entry through the dedicated endpoint. A row shows its own pending state, prevents duplicate submission, preserves the selected month after an error, and displays the API error. No delete action and no day selector are added.

Successful updates refresh both the editor data and every list/detail query that can display or sort Finish time.

## Accessibility

- Edit, Finish, and per-row Save buttons have localized accessible names and tooltips.
- The Finish dialog has a title, description, labeled score input, validation message, and predictable focus return.
- Month inputs have labels that distinguish individual history entries.
- Dates remain visible text; icons do not carry the information alone.
- Pending mutations disable only the relevant action and expose a busy state.

## Files in scope

- `docs/DOMAIN_RULES.md`
- `docs/API_CONTRACT.md`
- `docs/UI_MAP.md`
- `docs/openapi.yaml`
- `MasterPlay.Host/Dtos.cs`
- `MasterPlay.Host/LibraryQuery.cs`
- `MasterPlay.Host/Program.cs`
- generated `MasterPlay.Web/src/lib/api/types.gen.ts` through `npm run gen:api`
- `MasterPlay.Web/src/lib/api/domain.ts`
- `MasterPlay.Web/src/lib/api/client.ts`
- `MasterPlay.Web/src/lib/format.ts`
- `MasterPlay.Web/src/lib/i18n.tsx`
- `MasterPlay.Web/src/mocks/data.ts`
- `MasterPlay.Web/src/mocks/library.ts`
- `MasterPlay.Web/src/components/GameCard.tsx`
- `MasterPlay.Web/src/components/GameDetailDialog.tsx`
- `MasterPlay.Web/src/components/GameEditorForm.tsx`
- the matching entry in `docs/PARITY_CHECKLIST.md`

No schema migration and no changes to generated API files by hand are allowed.

## Verification

- Verify Publish date formatting with `2026-08-21` → `21/08/2026`.
- Verify Finish time formatting with an ISO timestamp in August 2026 → `08/2026`.
- Verify the Gallery and Detail hide missing dates and show the new formats when values exist.
- Verify `finishTime` sorts by the complete latest timestamp, not year alone, with unfinished games last.
- Verify Finish writes the game score, Done progress, and one finish entry atomically using the backend clock.
- Verify an invalid Finish score rolls back all changes.
- Verify editing one of several finish entries changes only that entry and normalizes it to the first day of the selected month.
- Verify invalid `YYYY-MM`, cross-game finish IDs, missing games, and missing Done catalog state do not mutate data.
- Run mutation tests against a temporary database, never the user's library.
- Run `npm run gen:api`, `npm run typecheck`, and `npm run build` in `MasterPlay.Web`.
- Run backend build/tests for `MasterPlay.Host` and the solution.
- The web package currently has no lint or test script; use the available verification commands above.
