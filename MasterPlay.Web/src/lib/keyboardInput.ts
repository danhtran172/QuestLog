import type { ControllerAction } from "./controllerInput";

export type ConsoleKeyboardReading = Readonly<{
  key: string;
  consoleSession: boolean;
  editing: boolean;
  repeat?: boolean;
  altKey?: boolean;
  ctrlKey?: boolean;
  metaKey?: boolean;
}>;

const CONSOLE_KEY_ACTIONS: Readonly<Record<string, ControllerAction>> = {
  ArrowLeft: "moveLeft",
  ArrowRight: "moveRight",
  ArrowUp: "moveLeft",
  ArrowDown: "moveRight",
  a: "moveLeft",
  d: "moveRight",
  w: "moveLeft",
  s: "moveRight",
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

  const action = CONSOLE_KEY_ACTIONS[normalizedKey] ?? null;
  if (reading.repeat && action !== "moveLeft" && action !== "moveRight") return null;
  return action;
}
