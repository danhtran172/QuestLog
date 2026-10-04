# Controller Console Mode Design

**Date:** 2026-08-29

**Status:** Approved design, pending implementation plan

**Scope:** `MasterPlay.Web` controller interaction and console presentation, plus the minimum `MasterPlay.Shell` bridge needed for native fullscreen

## 1. Goal

Add a controller-first way to use QuestLog without removing or replacing the existing gallery UI.

The app must accept controller input whenever a standard gamepad is available. A dedicated console presentation can be entered with `F10` or `LB + RB + A`. Console presentation is fullscreen, cover-focused, and suitable for use from a couch or TV. The existing gallery, list, dialogs, and settings remain usable with the same controller.

This feature is presentation and input orchestration only. It must not define domain values, change generated API types, filter or sort games on the client, infer launchability, or persist settings outside the existing settings API.

## 2. Chosen Approach

Use the browser Gamepad API in the React client for polling and input mapping. Use a small WebView2 message bridge only to request native fullscreen from `MasterPlay.Shell`.

This approach is preferred because it:

- gives React direct access to the UI context needed for navigation and prompts;
- works in both WebView2 and compatible standalone browsers;
- avoids a Windows-only XInput event protocol between the shell and the web app;
- keeps the shell responsible only for window chrome and fullscreen state;
- does not require a new backend endpoint or an OpenAPI change.

The existing `capabilities.features.controller` value may advertise native desktop availability, but controller input detection in the web client is based on the Gamepad API and actual button input. No controller-only control is shown merely because the server says the feature exists.

## 3. State Model

Controller behavior is split into three independent concepts:

1. **Controller availability**: at least one standard-mapped gamepad is connected and readable.
2. **Active input modality**: the most recent meaningful input came from a gamepad, keyboard, or pointer.
3. **Console session**: the console presentation and fullscreen request are active.

Controller navigation is always enabled when controller input is readable. A user never has to activate console presentation before using the controller.

Console session state is UI-only session state. It is not written to `localStorage` or the settings API. The selected game is tracked by game ID where possible so list refreshes do not silently move selection to a different game.

Updated 2026-09-04: Console sessions always show the Console presentation. Menu and L3 cannot switch to desktop Gallery/List, the former L keyboard shortcut is inactive, and prompts omit these desktop actions. Leaving the console session with `F10` or `LB + RB + A` restores the fullscreen and window state that existed before the session. Entering Console automatically requests native fullscreen using full monitor bounds, including the taskbar area; F11 uses the same native window path.

## 4. Input Mapping

Standard Gamepad API button indices are used. Input is edge-triggered, with controlled repeat only for directional navigation and scrolling.

| Input | Global behavior |
|---|---|
| `F10` | Enter or leave console session |
| `LB + RB + A` | Enter or leave console session once per chord press |
| Menu, right system button | Inactive in Console |
| View, left system button | Open Settings |
| `LB` / `RB` | Move to the previous or next library tab |
| D-pad / left stick | Move focus or move the selected game; at the Console root, D-pad Up opens the controls popup and D-pad Down closes it |
| `A` | Activate the focused control or open the selected game |
| `B` | Close the topmost dialog immediately; at the Console root, hold for 900 ms to leave the console session |
| `X` | Launch the selected game only when the API returns `canLaunch: true` |
| `Y` | Refresh the current server-backed view |
| `L3` | Toggle Card and List in desktop Gallery; inactive in Console |
| `R3` | Open the existing Add Game flow |
| Right stick | Scroll the active scroll container |

Console keyboard controls use `Tab` for Detail, `Enter` for Play when `canLaunch` is true, `Shift` to toggle the controls popup, and hold `Escape` for 900 ms to leave the console session. Releasing `Escape` or `B` early cancels the exit. While a dialog is open, `Escape` and `B` retain their immediate Back behavior.

When the user opens Settings, Detail, Add Game, or another dialog, contextual navigation takes precedence over actions targeting the library behind the dialog. Closing the dialog restores focus to the previously selected game or control.

## 5. Input Processing

The controller hook polls `navigator.getGamepads()` on animation frames only while the document is visible and focused. It does not write continuous axis values into React state.

The hook emits semantic actions only when:

- a digital button transitions from released to pressed;
- a stick crosses a dead-zone threshold;
- a held directional input reaches its initial-delay and repeat interval;
- the full activation chord transitions from incomplete to complete.

The activation chord consumes its component button presses for that poll cycle. Pressing `LB + RB + A` must not also change a tab or open a game. Disconnecting a controller clears held-button, exit-hold, and repeat state so reconnecting cannot trigger stale actions. The Console root tracks the press and release edges of `Escape`/`B`; completing the 900 ms hold exits both the presentation and its fullscreen session rather than merely revealing Gallery underneath fullscreen.

Meaningful controller input changes the active input modality to gamepad. Pointer movement, pointer down, wheel, or non-synthetic keyboard input changes it away from gamepad. Merely connecting a controller does not cover the interface with prompts before the user touches it.

## 6. Spatial Navigation

Console presentation has explicit navigation because its content is a horizontal cover sequence.

Gallery, List, Settings, and dialogs use spatial focus navigation over eligible visible controls. The focus manager considers actual element rectangles and chooses the closest eligible target in the requested direction. Hidden, disabled, inert, and covered controls are excluded.

Game cards and rows expose a stable controller-focus target. The active target receives a primary-colored focus frame that remains visible over both light and dark image content. Native keyboard focus remains intact; controller focus must not remove existing accessibility outlines.

The `B` action targets the topmost dismissible surface first. It does not close multiple stacked surfaces in one press.

## 7. Console Presentation

`ConsoleLibraryView` occupies the full dynamic viewport and is visually isolated from the normal header and footer.

### 7.1 Backdrop

The selected game's `bannerPath` is preferred for the wide backdrop. `posterPath` is the fallback. The image uses cover cropping, strong blur, a dark scrim, and a subtle color wash derived from the existing theme tokens. When neither image exists, the current theme background is used.

Changing selection crossfades the backdrop. Only opacity and transform are animated. Reduced-motion users receive an immediate image change.

### 7.2 Cover sequence

The selected game uses its IGDB-only `posterPath` as the primary cover, then falls back to RAWG `backdropPath` and Steam `bannerPath`. The visible sequence shows the current game and nearby games, with the selected cover largest and fully opaque. Neighboring covers are progressively smaller and slightly muted. The selected cover has a clearly visible primary-colored frame and elevated shadow.

If `posterPath` is absent or fails to load, the cover displays a gamepad icon and the real game name. It does not fabricate artwork.

The selected game's name appears below the cover. Progress and ownership may also appear using only `displayName`, `iconData`, and `colorCode` returned by the API. No `systemKey` list is hardcoded to infer presentation or behavior.

### 7.3 Context and prompts

The current localized tab name is shown near the top. Contextual prompts are hidden by default inside one popup at the bottom-right corner. A low-emphasis `Shift` or `D-pad Up` badge remains as the dynamic input hint beside a localized `Open controls` label, so the shortcut's purpose is explicit. `Shift` toggles the popup, D-pad Up opens it, and D-pad Down closes it. Pointer input hides both the popup and hint.

The popup stacks its actions vertically and contains only actions valid in the current context. Examples:

- Keyboard Console: `Tab Details`, `Enter Play` when `canLaunch`, `Q/E Change tab`, `N Add game`.
- Gamepad Console: `A Details`, `X Play` when `canLaunch`, `LB/RB Change tab`, `R3 Add game`.
- Error: `A Retry`.

Any other action closes the popup before continuing that action. Opening a Detail view, Settings, Add Game, or another overlay also closes it. Exit is not listed in the popup. Starting an exit hold displays only the active `Esc` or `B` badge with a circular 900 ms progress indicator; releasing early removes it. Other prompts use compact button badges and localized text and switch automatically between keyboard and gamepad.

## 8. Server Data and Pagination

Console presentation consumes the same server response already used by the active library tab. It does not apply client-side filtering, sorting, thresholds, ranking, or inferred state.

When navigation moves beyond the last item of a paginated response, the app requests the next server page and selects its first item. Moving left from the first item requests the previous page and selects its last item. Queue continues to use its existing unpaginated response.

Changing tabs through `LB` or `RB` resets to the tab's valid page and selects the first available item after the server response arrives. Empty and failed tabs do not reuse a stale selected game from another tab.

The selected cover and two neighboring images are preloaded. The app does not preload the entire library.

## 9. Fullscreen Integration

In `MasterPlay.Shell`, the web client sends a narrowly scoped message requesting console fullscreen on or off. The shell accepts only the recognized message shape and calls its existing fullscreen implementation. This hides native title chrome and restores the previous `WindowState`, resize mode, and window bounds when leaving the session.

The shell reports the resulting fullscreen state back to the web client so React does not assume the request succeeded.

In a standalone browser, the client uses the Fullscreen API. If the browser denies the request, console presentation still opens and a localized non-blocking message explains that fullscreen was unavailable. The error is not swallowed.

Existing `F11` fullscreen remains independent. Leaving a console session restores the fullscreen state that existed before the session rather than blindly forcing a windowed state.

## 10. Loading, Empty, and Error States

- **Loading:** cover-shaped skeletons preserve the final layout and selected position.
- **Empty:** localized empty-state text is shown; `R3` still opens the existing Add Game flow.
- **API error:** the localized API error is visible, with `A Retry` when gamepad modality is active.
- **Image error:** only the failed image is replaced by its fallback; the game remains navigable.
- **Launch error:** the existing launch mutation error toast remains visible and focus stays on the selected game.
- **Fullscreen error:** console presentation remains usable and the failure is shown to the user.

No error branch catches and discards an error.

## 11. Accessibility and Motion

- All prompt text is localized in `i18n.tsx`.
- Controller focus is synchronized with DOM focus where an interactive element exists.
- Selected game and active tab are exposed with appropriate ARIA state.
- Text over artwork uses a contrast-preserving scrim.
- Image fallbacks include the game name; decorative backdrops use empty alt text.
- Motion honors `prefers-reduced-motion`.
- Focus frames remain visible without relying only on color or opacity.
- Console uses `100dvh` behavior rather than a fixed screen-height assumption.

## 12. Component Boundaries

The implementation should keep the following responsibilities separate:

- `controllerInput`: standard button constants, dead zones, edge detection, repeat timing, and semantic actions.
- `useGamepadController`: lifecycle, polling, connection changes, and action dispatch.
- `ControllerPrompts`: context-sensitive visible guidance.
- `ConsoleLibraryView`: console layout and explicit game navigation.
- `spatialNavigation`: visible-control discovery and directional focus selection.
- `desktopBridge`: typed fullscreen request and state messages.
- `App`: owns existing library queries and mutations, coordinates visible surface and dialogs.

No component in this feature may import from or edit `src/lib/api/types.gen.ts` except through the existing read-only domain re-exports.

## 13. Verification

Implementation verification must cover:

1. Button edge detection, directional repeat, dead-zone behavior, and chord consumption.
2. Always-on navigation before, during, and after a console session.
3. Input-modality switching, the faded `Shift`/D-pad hint, and contextual popup open/close behavior.
4. All mappings, including `L3` Card/List and `R3` Add Game.
5. Tab and server-page boundaries without client-side reordering.
6. Console loading, empty, API error, image failure, and launch failure states.
7. Dialog priority and focus restoration.
8. Reduced-motion behavior.
9. Native WebView2 fullscreen entry and restoration.
10. Browser fullscreen denial with a visible error.
11. React typecheck and production build.
12. Desktop Shell build and the repository's existing test command.

## 14. Definition of Done

The feature is complete when:

- a connected controller can operate the existing app without entering console presentation;
- `Shift` or D-pad Up reveals the correct contextual prompt popup, D-pad Down closes it, and other actions close it without being swallowed;
- `F10` and `LB + RB + A` enter and leave fullscreen console presentation exactly once per press;
- every approved input mapping works in its intended context;
- console covers, backdrop, selection frame, motion, loading, empty, and error states are present;
- all displayed game data traces to generated API types;
- `Tab`/`A` open Detail, while `Enter`/`X` and the visible Play button launch only games with `canLaunch: true`;
- holding `Escape`/`B` for 900 ms leaves the Console fullscreen session without leaking its viewport state into Gallery;
- no client-side domain filtering, sorting, thresholds, ranking, or capability guessing is introduced;
- the appropriate controller-mode line in `docs/PARITY_CHECKLIST.md` is updated;
- typecheck, build, tests, and focused manual controller verification pass.
