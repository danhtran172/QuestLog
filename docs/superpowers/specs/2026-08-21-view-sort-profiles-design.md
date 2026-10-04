# View Sort Profiles

Date: 2026-08-21
Status: Approved for implementation

## Goal

Let users right-click the `All`, `Planning`, or `Finished` view tab to inspect and customize that view's persistent multi-key default sort. `Play Queue` keeps its existing `PlayOrder` behavior and disables filtering and sorting controls.

## Product behavior

- Right-clicking `All`, `Planning`, or `Finished` opens a compact editor anchored to that tab.
- The editor lists sort rules in priority order. Each row has a drag handle, a direction toggle, and a remove action.
- The add action lists only unused sort keys. Duplicate keys are invalid.
- Reset removes the custom profile and restores the built-in profile for that view.
- An empty custom profile is valid and sorts alphabetically by game name.
- Changes persist across UI sessions through backend configuration.
- The existing toolbar sort remains a temporary override. It stays active across view changes until the user changes it, presses F6, or closes the UI session.
- When a toolbar override is active, the profile editor shows that fact because edits do not affect the current list until toolbar sort returns to `Default`.
- F6 resets temporary search, filter, sort, and reverse-sort state. It does not delete saved view profiles.
- In Queue, Sort and Filter are disabled without clearing their temporary state. Right-clicking Queue does not open the profile editor.

## Architecture

Frontend never sorts game data. Profiles are stored in backend `config.json`, exposed through a dedicated API, and applied by `GET /api/games` only when the toolbar sort query is `default`.

Configuration stores overrides only, keyed by `all`, `planning`, and `finished`. A missing entry means the built-in profile. An explicitly stored empty array means alphabetical name sorting.

### Data model

```text
SortRule
  key: SortKey excluding default
  direction: asc | desc

ViewSortProfile
  view: all | planning | finished
  rules: SortRule[]
  customized: boolean
```

### API

- `GET /api/library/sort-profiles` returns effective profiles for all three configurable views and whether each is customized.
- `PUT /api/library/sort-profiles/{view}` validates and stores the complete rule array for one view.
- `DELETE /api/library/sort-profiles/{view}` removes the override and returns the restored built-in profile.
- Queue is not a valid profile route value.

The API rejects unknown keys, `default`, duplicate keys, invalid directions, and unsupported views with `400`.

## Sort semantics

Built-in profiles remain:

- All: `ready desc`, `publish desc`, `excited desc`, `name asc`, `progress asc`, `ownership asc`.
- Planning: `progress asc`, `excited desc`, `avgPlaytime asc`, `name asc`.
- Finished: `progress asc`, `score desc`, `finishTime desc`, `publish desc`, `metaScore desc`.

For custom profiles:

- Missing values always sort last in either direction.
- Backend appends `name asc` as a hidden deterministic tie-breaker when name is not already present.
- An empty rule array evaluates as `name asc`.
- A non-default toolbar sort continues to use the existing single-key behavior and whole-list `reverseSort` behavior.
- Queue ignores library filters and sort profiles and continues using `PlayOrder`.

## UI design

The editor is a small glass surface matching QuestLog. It uses text only for rule labels; all actions are icon-only with tooltips.

- Rows are vertically draggable and expose a visible grip.
- Direction is an icon toggle showing ascending or descending state.
- Remove is an icon action.
- Add opens a compact picker of unused keys.
- Reset is an icon action in the footer.
- Empty state says that alphabetical game-name sorting is active.
- Saving happens on drag end or immediately after add/remove/direction changes.
- While saving, affected controls are disabled. On failure, the optimistic change is rolled back and the API error is shown in the editor.
- Keyboard focus, Escape, outside-click dismissal, and accessible labels are preserved.

## State ownership

- Saved profiles: backend config and query cache.
- Open editor, dragged row, add picker: local UI state.
- Toolbar sort/reverse sort: existing session-only App state.
- Game order, validation, null ordering, tie-breakers: backend only.

## Verification

- Backend tests cover built-in, custom, empty, reset, duplicate rejection, null-last ordering, deterministic tie-breaking, toolbar override, and Queue invariants.
- API smoke tests cover GET/PUT/DELETE without touching game records.
- Frontend typecheck and production build must pass.
- UI verification covers context-menu anchoring, drag order, direction toggle, add/remove/reset, empty state, error rollback, toolbar-override notice, and disabled Queue controls.

## Out of scope

- User accounts or cloud synchronization of profiles.
- Custom sort profiles for Queue.
- Client-side sorting.
- Changing the existing list of sort keys.
