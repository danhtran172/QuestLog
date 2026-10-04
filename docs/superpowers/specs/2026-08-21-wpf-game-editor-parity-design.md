# WPF Game Editor Parity in the QuestLog Detail Dialog

## Goal

Restore the complete editing and supporting workflow of the legacy WPF `GameEditorControl` inside the current QuestLog Web Detail dialog. Functional parity means that every useful field, command, picker, preview, native file action, taxonomy-management action, and validation behavior from the WPF editor has an equivalent web interaction.

The visual result is not a pixel copy of WPF. It retains the current QuestLog glass surface, fixed and fading hero image, title/developer placement, icon-only action buttons with tooltips, responsive sizing, and two-tab Detail navigation.

The WPF implementation is a behavioral inventory. `docs/openapi.yaml`, `docs/DOMAIN_RULES.md`, and the current database model remain authoritative for domain behavior.

## Interaction structure

The Detail dialog has two top-level states:

1. **View mode** retains the current two-tab presentation.
   - Overview contains game metadata, scores, playtime, ownership, progress, finish history, external links, and ownership information.
   - Files contains process, launch, save, and backup locations with reveal actions.
2. **Edit mode** keeps the same hero and glass dialog shell, but the content beneath the hero becomes the complete editor. The editor uses grouped sections instead of adding a third Detail tab.

The fixed lower-right action cluster continues to contain Edit and Finish in View mode. Edit mode contains Save, Cancel, and Disable for an existing game. All actions use the established rounded QuestLog icon-button treatment and localized tooltips.

The narrow vertical Detail tab indicator remains available only in View mode so it cannot be confused with the editor's grouped sections.

## Editor sections

The editor is arranged in responsive sections that collapse to one column when vertical or horizontal space is constrained.

### Identity and relationships

- Game name and metadata sync action.
- DLC toggle.
- Base game selector when the game is a DLC.
- Series selector when the game is not a DLC.
- Publisher and developer selectors/fields as supported by the current API model.
- Genre and theme multi-select controls.

Changing DLC state follows current backend rules. The frontend does not infer or rewrite series data beyond values returned by the API.

### Ratings, status, and time

- User score.
- Metascore with an icon-only action that opens the backend-provided Metacritic URL.
- Excited value.
- Player playtime shown as hours and minutes, with the WPF-style increment/decrement commands routed through the playtime endpoint.
- Community average playtime with an icon-only action that opens the backend-provided HowLongToBeat URL.
- Ownership selector.
- Play Progress selector.
- Publish date with full day/month/year editing.
- Finish History with month/year editing according to the current Finish Time design; the deprecated WPF `FinishedIn` string is not restored.

Ownership and Progress options render the API-provided `iconData`, `displayName`, and `colorCode`. The selected control also retains the selected item's color treatment. No `systemKey` or numeric catalog ID is hardcoded to derive appearance or behavior in the frontend.

### Runtime and folders

- Tracked process name.
- Optional launch target/process.
- Save location with folder picker and reveal action.
- Backup location with folder picker, generated-backup action, and reveal action.

Native actions are visible only when the matching capability is available. A supported action that fails shows its API error; an unsupported hosted action is not presented as a working control.

### Images

- Cover image path/URL, sync action, and portrait preview.
- Gallery image path/URL, sync action, and landscape preview.
- Steam Library image path/URL, sync action, and landscape preview.

Each preview has an explicit empty state and updates after a successful field change or sync response. A broken remote/local image shows the empty state without clearing the stored value. Image-sync buttons expose their own pending state and cannot be submitted twice.

### Ownership information

Restore all WPF ownership fields supported by the current `GameOwnershipInfo` model:

- Steam account and password.
- Ubisoft account and password when present in the current model.
- Support document URL.
- Download URL.
- Ownership note.

Passwords are masked by default. Copy and open-link actions remain icon-only with tooltips. Field visibility follows the selected ownership information returned by the API rather than a frontend list of ownership IDs.

## Taxonomy picker and manager

Series uses a single-select picker. Genres and Themes use multi-select pickers. All three share the same QuestLog glass popup behavior:

- Search the server-provided list.
- Select and remove items.
- Create a new item from non-empty search text.
- Open an existing item in a nested edit popup.
- Rename an item.
- Delete an item after confirmation.
- Preserve the editor draft while any nested popup is open.

Create, rename, and delete call the existing taxonomy API. Successful mutations invalidate the matching taxonomy query and reconcile the current draft:

- A created item becomes selected immediately.
- A renamed selected item updates in place after the server response.
- Deleting a selected item removes it from the draft.
- Deleting an associated taxonomy item relies on the backend transaction defined by the API contract; the frontend never rewrites associations itself.

Mutation errors remain visible inside the active popup. The popup closes only after a successful mutation or explicit Cancel.

## Backend and API work

The OpenAPI contract already defines the required public routes. The Host must implement the routes that are currently missing rather than adding frontend-only behavior:

- `POST /api/games/{id}/disable`
- `POST /api/games/{id}/playtime`
- `POST /api/games/{id}/sync/{source}`
- `POST`, `PUT`, and `DELETE /api/taxonomy/...`
- `POST /api/fs/pick-file`
- `POST /api/fs/pick-folder`
- `POST /api/fs/reveal` (already present; retain and verify)
- `POST /api/games/{id}/backup-folder/generate`
- `GET /api/games/{id}/external-links` (already present; retain and verify)

The implementation also exposes the matching capability flags from `/api/capabilities`. Native folder/file dialogs and Explorer launch remain Host responsibilities.

Metadata and image synchronization remains backend-owned. The frontend sends only the documented sync source and consumes the returned `GameDetail`; it does not contact RAWG, Steam, Metacritic, HowLongToBeat, Cloudinary, or another external service directly.

All contract changes, if an implementation gap proves that one is required, are made in `docs/openapi.yaml` first and generated through `npm run gen:api`. Generated files under `MasterPlay.Web/src/lib/api/**` are never edited manually.

## Save and mutation flow

The editor keeps a local UI draft typed from the generated `GameDetail` model. It may format seconds as hours/minutes for display, but it does not redefine storage units or domain fields.

Save performs one game upsert using the existing API contract. Auxiliary commands such as playtime adjustment, taxonomy mutation, sync, folder generation, and Finish History editing call their dedicated routes immediately because each has backend-owned transactional behavior.

After a successful mutation, the application refreshes:

- the active game Detail query;
- affected Gallery queries;
- taxonomy/catalog queries when applicable;
- queue/dashboard queries when progress, disable state, or Finish changes them.

The editor remains open when Save fails and retains the draft. Error messages are localized and visible near the relevant section or in the editor-level error region. No `catch` path silently discards an API failure.

## Disable behavior

Disable is available only for an existing active game in Edit mode. It requires confirmation, calls the soft-delete endpoint, and closes the Detail dialog only after success. It does not issue a hard delete. Gallery, Queue, and Dashboard data are refreshed after the mutation.

## Responsive behavior

The dialog remains within the current window on smaller Full HD displays and when Windows display scaling is above 100%.

- Height is constrained to the usable viewport and editor content scrolls inside the dialog.
- The hero remains fixed within the dialog and retains its bottom fade.
- Footer actions remain reachable without covering fields.
- Multi-column groups collapse before controls overflow.
- Popups are collision-aware and remain inside the dialog/viewport.
- Image previews shrink proportionally instead of forcing a minimum dialog width.

## Accessibility

- Every icon-only button has a localized tooltip and accessible name.
- Ownership and Progress do not communicate state by color alone; icon and text remain present.
- Pickers support keyboard navigation, Enter selection, Escape close, and visible focus.
- Nested taxonomy dialogs trap focus and return it to the invoking control.
- Destructive Disable/Delete actions require confirmation and expose pending state.
- Password controls identify their masked state and copy action.
- Error text is associated with the relevant field or dialog.

## Files expected in scope

- `MasterPlay.UI/Features/GameEditor/**` as read-only behavioral reference.
- `docs/openapi.yaml` only if implementation reveals a genuine contract gap.
- `MasterPlay.Host/Program.cs`
- `MasterPlay.Host/Dtos.cs`
- Host services required to implement native and sync commands without placing domain logic in the web client.
- Generated API artifacts only through `npm run gen:api`.
- `MasterPlay.Web/src/lib/gameDetailApi.ts` or the appropriate non-generated API wrapper.
- `MasterPlay.Web/src/lib/i18n.tsx`
- `MasterPlay.Web/src/components/GameDetailDialog.tsx`
- `MasterPlay.Web/src/components/GameEditorForm.tsx`
- Small reusable picker/preview/editor components extracted from the form as needed.
- `docs/PARITY_CHECKLIST.md`

## Parity acceptance checklist

The implementation is not complete until all of the following are verified against the WPF editor inventory:

- View mode contains Metacritic, HowLongToBeat, ownership details, process, save, backup, and reveal actions.
- Edit mode exposes every identity, relationship, rating, status, runtime, folder, image, and ownership field supported by the current domain model.
- Cover, Gallery, and Steam Library previews render real game data and have empty/broken states.
- Ownership and Progress selectors render distinct API-provided icons and colors in both their lists and selected state.
- Series/Genre/Theme each support search, select, create, rename, delete, cancel, and error handling.
- Native folder/file actions work through the Host and respect capability flags.
- Metadata and each image source can be synchronized through the backend.
- Player playtime can be incremented/decremented through its backend route.
- Existing games can be disabled only after confirmation.
- Finish Time remains month/year-based and Finish uses the current backend time.
- All controls fit and remain reachable on the smaller Full HD window configuration.
- No frontend domain inference, external metadata URL construction, direct Supabase domain access, mock game data, or swallowed API error is introduced.

## Verification

- Build the Host and run route-level tests against a temporary database and temporary filesystem paths.
- Verify taxonomy create/rename/delete transaction behavior, including deletion of an associated item.
- Verify sync success and upstream-error presentation for each supported source.
- Verify folder picker cancellation does not mutate the draft.
- Verify reveal/generate actions reject invalid paths cleanly.
- Verify playtime cannot become negative and the backend remains the unit-conversion authority.
- Verify disable is a soft delete and the game disappears from active queries.
- Run `npm run gen:api` only when OpenAPI changes.
- Run `npm run typecheck` and `npm run build` in `MasterPlay.Web`.
- Run `npm run lint` and `npm run test` if those scripts exist; otherwise report that they are unavailable.
- Run `dotnet build` and the relevant Host tests.
- Perform a browser smoke test of View/Edit, both Detail tabs, all nested taxonomy popups, colored catalog selects, the three image previews, native actions, and responsive behavior.
- Mark the corresponding `docs/PARITY_CHECKLIST.md` entry only after the full checklist passes.
