import type { ControllerAction } from "./controllerInput";

export type ConsoleKeyboardReading = Readonly<{
  key: string;
  consoleSession: boolean;
  consoleVisible: boolean;
  overlayOpen: boolean;
  editing: boolean;
  repeat?: boolean;
  altKey?: boolean;
  ctrlKey?: boolean;
  metaKey?: boolean;
}>;

const SPATIAL_KEY_ACTIONS: Readonly<Record<string, ControllerAction>> = {
  ArrowLeft: "moveLeft",
  ArrowRight: "moveRight",
  ArrowUp: "moveUp",
  ArrowDown: "moveDown",
  a: "moveLeft",
  d: "moveRight",
  w: "moveUp",
  s: "moveDown",
};

const CONSOLE_KEY_ACTIONS: Readonly<Record<string, ControllerAction>> = {
  Enter: "activate",
  " ": "activate",
  Escape: "back",
  q: "previousTab",
  e: "nextTab",
  v: "openSettings",
  l: "toggleLayout",
  n: "addGame",
  p: "play",
  Tab: "toggleSurface",
};

export function getConsoleKeyboardAction(
  reading: ConsoleKeyboardReading,
): ControllerAction | null {
  const normalizedKey = reading.key.length === 1 ? reading.key.toLowerCase() : reading.key;

  if (normalizedKey === "F10") return reading.repeat ? null : "toggleConsole";
  if (!reading.consoleSession) return null;
  if (reading.altKey || reading.ctrlKey || reading.metaKey) return null;
  if (reading.editing && normalizedKey !== "Escape") return null;

  const spatialAction = SPATIAL_KEY_ACTIONS[normalizedKey] ?? null;
  const carouselAction = reading.consoleVisible && !reading.overlayOpen
    ? spatialAction === "moveUp"
      ? "moveLeft"
      : spatialAction === "moveDown"
        ? "moveRight"
        : spatialAction
    : spatialAction;
  const commandAction = CONSOLE_KEY_ACTIONS[normalizedKey] ?? null;
  const action = carouselAction ?? commandAction;
  if (reading.overlayOpen && action != null && ![
    "activate",
    "back",
    "moveUp",
    "moveDown",
    "moveLeft",
    "moveRight",
  ].includes(action)) return null;
  if (reading.repeat && ![
    "moveUp",
    "moveDown",
    "moveLeft",
    "moveRight",
  ].includes(action ?? "")) return null;
  return action;
}
