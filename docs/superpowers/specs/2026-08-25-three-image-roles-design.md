# Three image roles — design

## Goal

Replace the ambiguous image fields with three explicit image roles:

| API field | Source of truth | Shape | Consumer |
|---|---|---|---|
| `cardCoverPath` | Steam `header.jpg` | landscape 460×215 | Gallery cards, list rows, queue |
| `coverPath` | RAWG `background_image` | landscape | Game detail hero |
| `portraitCoverPath` | IGDB `t_cover_big_2x` | portrait 528×748 | Console mode (future); editable/previewable now |

The old Steam Library portrait image has no consumer under this model and is retired.

## Scope and non-goals

- This changes the local game schema, backend API contract, Web editor and WPF editor/library bindings.
- Console mode is not implemented in this change; it will consume `portraitCoverPath` when it exists.
- Existing legacy image columns remain physically in SQLite for one release. They are not returned by the API, written by new syncs, uploaded by Cloudinary, or displayed in either UI.
- This change does not download image bytes during sync. It stores source URLs; Cloudinary retains its existing opt-in upload workflow.

## Migration

Add nullable `CardCoverPath`, `CoverPath`, and `PortraitCoverPath` columns to `Games`.

On first startup after migration:

1. Copy `GImagePath` to `CardCoverPath` only when the new field is empty.
2. Do not infer a role for legacy `ImagePath`: prior databases may contain either a RAWG landscape image or an IGDB portrait image. Leave it in the legacy column.
3. Do not map `SteamLibraryImagePath`; it is retired.
4. The detail hero falls back to `cardCoverPath`, so a game does not become visually empty while awaiting the next sync.

No image value is deleted by this migration. A later, separately approved cleanup migration may remove the three legacy columns after the library has been re-synced.

## API contract

`GameListItem` exposes `cardCoverPath`; `GameDetail` exposes all three fields. Create/update payloads use the same three field names. The old `gImagePath`, `imagePath`, and `steamLibraryImagePath` properties are removed from the Host API and regenerated client types.

The sync source enum becomes:

```text
metadata | card-cover | cover | portrait-cover
```

Each sync returns the refreshed `GameDetail` on success. Failures use the standard error envelope and a source-specific code:

- `STEAM_CARD_COVER_NOT_FOUND`
- `RAWG_COVER_NOT_FOUND`
- `IGDB_PORTRAIT_NOT_FOUND`

## Sync behaviour

`metadata` queries all three sources and persists every successfully resolved role:

- IGDB is required for canonical metadata and `portraitCoverPath`; an unavailable IGDB configuration or no IGDB match returns an error.
- RAWG supplies `coverPath`, slug, Metacritic score and average playtime. A missing RAWG result leaves those values unchanged.
- Steam supplies `cardCoverPath`. A missing Steam result leaves it unchanged.
- Gemini remains best-effort for `exeName`.

The dedicated image sync actions update only their own role:

| Source | Provider | Updated field |
|---|---|---|
| `card-cover` | Steam | `cardCoverPath` |
| `cover` | RAWG | `coverPath` |
| `portrait-cover` | IGDB | `portraitCoverPath` and IGDB id |

## UI and Cloudinary

- Gallery card, table row and play queue render `cardCoverPath`.
- Game detail hero renders `coverPath`, with `cardCoverPath` as safe fallback.
- The Editor contains three accurately named image fields and source-specific sync buttons. Portrait Cover is available there but is not rendered elsewhere until Console mode.
- The old Steam Library image field and button are removed from both editors.
- Cloudinary processes the three new fields using distinct asset role names (`card-cover`, `cover`, `portrait-cover`), never the retired legacy columns.

## Verification

1. Migration preserves the old card image as `cardCoverPath` and never deletes an old URL.
2. Endpoint tests verify each dedicated sync mutates only its intended field and that metadata sync accepts a missing optional RAWG/Steam result.
3. Contract generation succeeds after `docs/openapi.yaml` is updated.
4. `dotnet build`, `dotnet test`, Web typecheck/lint/build, and the WPF solution build succeed.
5. Manual smoke test: a synced game shows Steam art in the gallery, RAWG art in detail, and the IGDB portrait only in the editor.
