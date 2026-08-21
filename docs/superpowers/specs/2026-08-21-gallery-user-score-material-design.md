# Gallery User Score Material Design

## Goal

Replace the Gallery Card's star-based User Score with a compact material score chip. The chip uses the text and surface palettes from `lovable-project-497a77b8.zip`, while keeping the ZIP strictly as a visual reference rather than a source of application code or domain behavior.

This change applies only to Gallery Cards. Detail and Play Queue keep the existing `ScoreBadge` and its star icon.

## Domain ownership and API contract

The frontend must not derive score thresholds. The backend will expose a nullable `scoreMaterialBand` on `GameListItem` and therefore on `GameDetail` through inheritance.

Add an OpenAPI enum named `ScoreMaterialBand` with these values:

- `Rust`
- `Copper`
- `Silver`
- `Gold`
- `Platinum`
- `Diamond`
- `Opal`

Backend mapping is the source of truth:

| Score | Band |
|---|---|
| `null`, `0`, or negative | `null` |
| `> 0` and `< 3` | `Rust` |
| `>= 3` and `< 6` | `Copper` |
| `>= 6` and `< 8` | `Silver` |
| `>= 8` and `< 9` | `Gold` |
| `>= 9` and `< 10` | `Platinum` |
| `10` | `Diamond` |
| sentinel `999` | `Opal` |

The mapping is implemented in `MasterPlay.Host/LibraryQuery.cs`, included in host DTO mapping, documented in `docs/DOMAIN_RULES.md`, and represented in `docs/openapi.yaml`. Generated TypeScript API files are regenerated with `npm run gen:api`; they are never edited by hand.

Mocks provide explicit score/band fixture pairs. They do not implement a second threshold function in the client.

## Gallery presentation

Create a Gallery-specific material score chip and use it in `GameCard` in place of `ScoreBadge`.

- No star or other leading icon.
- Height matches the current Metascore chip (`24px`).
- Minimum width remains content-safe for half-step values.
- Normal values use the existing score formatter.
- Sentinel `999` displays `10` with the `Opal` band and no flame.
- Missing/zero scores remain hidden by the existing Gallery condition.
- Detail and Play Queue retain their existing score display, including their current sentinel formatting.

The component accepts the backend-provided band and maps enum values directly to CSS material classes. It never compares numeric score thresholds.

## Material styling

Port the ZIP's material palette into `MasterPlay.Web/src/index.css`:

- Rust: cold iron, brown-orange oxide
- Copper: warm orange copper
- Silver: cool neutral metal
- Gold: deep warm gold
- Platinum: crisp blue-white metal
- Diamond: bright cyan-white refraction
- Opal: moving multicolor spectrum

Each chip combines:

- a material surface using the ZIP's dark/base/light colors;
- a clipped material text gradient using the same palette;
- a subtle border derived from the material light color.

The reference animation is intentionally softened without changing the palette:

- animation duration is increased by roughly 25–30%;
- text and surface glow opacity/blur are reduced;
- no separate sparkle dot is added;
- `prefers-reduced-motion: reduce` disables the moving shine.

## Accessibility

Color is not the only carrier of information: the numeric score remains visible text. The chip title exposes `User score <value>` using the existing localized score label. Decorative gradients and animation are CSS-only and do not add duplicate screen-reader content.

## Files in scope

- `docs/DOMAIN_RULES.md`
- `docs/openapi.yaml`
- `MasterPlay.Host/LibraryQuery.cs`
- `MasterPlay.Host/Dtos.cs`
- `MasterPlay.Host/Program.cs`
- generated `MasterPlay.Web/src/lib/api/types.gen.ts` via `npm run gen:api`
- `MasterPlay.Web/src/lib/api/domain.ts` for the generated enum re-export
- `MasterPlay.Web/src/mocks/data.ts`
- `MasterPlay.Web/src/components/Pills.tsx`
- `MasterPlay.Web/src/components/GameCard.tsx`
- `MasterPlay.Web/src/index.css`
- the matching row in `docs/PARITY_CHECKLIST.md`

No database schema or migration is required because the band is derived from the existing score at response time.

## Verification

- Verify backend mapping at all boundaries: `2.5`, `3`, `5.5`, `6`, `7.5`, `8`, `8.5`, `9`, `9.5`, `10`, and `999`.
- Confirm Gallery renders no star and renders `999` as Opal `10`.
- Confirm Detail and Play Queue still use their existing score badge.
- Run `dotnet build MasterPlay.Host/MasterPlay.Host.csproj`.
- Run `npm run gen:api`, then ensure no manual generated-file edits remain.
- Run `npm run typecheck` and `npm run build` in `MasterPlay.Web`.
- The web package currently defines no lint or test script; verification uses the available scripts above.

