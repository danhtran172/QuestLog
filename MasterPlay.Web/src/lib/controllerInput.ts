export type ControllerAction =
  | "toggleConsole"
  | "toggleSurface"
  | "openSettings"
  | "previousTab"
  | "nextTab"
  | "moveUp"
  | "moveDown"
  | "moveLeft"
  | "moveRight"
  | "activate"
  | "back"
  | "play"
  | "refresh"
  | "toggleLayout"
  | "addGame"
  | "scrollUp"
  | "scrollDown"
  | "openPrompts"
  | "closePrompts"
  | "togglePrompts";

export type ControllerReading = Readonly<{
  buttons: readonly boolean[];
  axes: readonly number[];
}>;

export function isControllerActionAvailable(action: ControllerAction, consoleSession: boolean): boolean {
  return !consoleSession || (action !== "toggleLayout" && action !== "toggleSurface");
}

type RepeatDirection = "up" | "down" | "left" | "right" | "scrollUp" | "scrollDown";

export type ControllerInputState = Readonly<{
  buttons: readonly boolean[];
  chordPressed: boolean;
  direction: RepeatDirection | null;
  nextRepeatAt: number;
}>;

export const INITIAL_CONTROLLER_STATE: ControllerInputState = {
  buttons: [],
  chordPressed: false,
  direction: null,
  nextRepeatAt: 0,
};

const DEAD_ZONE = 0.55;
const INITIAL_REPEAT_MS = 320;
const HELD_REPEAT_MS = 110;

function pressed(reading: ControllerReading, index: number): boolean {
  return reading.buttons[index] === true;
}

function justPressed(state: ControllerInputState, reading: ControllerReading, index: number): boolean {
  return pressed(reading, index) && state.buttons[index] !== true;
}

function readDirection(reading: ControllerReading, reserveVerticalDpad: boolean): RepeatDirection | null {
  if ((!reserveVerticalDpad && pressed(reading, 12)) || (reading.axes[1] ?? 0) <= -DEAD_ZONE) return "up";
  if ((!reserveVerticalDpad && pressed(reading, 13)) || (reading.axes[1] ?? 0) >= DEAD_ZONE) return "down";
  if (pressed(reading, 14) || (reading.axes[0] ?? 0) <= -DEAD_ZONE) return "left";
  if (pressed(reading, 15) || (reading.axes[0] ?? 0) >= DEAD_ZONE) return "right";
  if ((reading.axes[3] ?? 0) <= -DEAD_ZONE) return "scrollUp";
  if ((reading.axes[3] ?? 0) >= DEAD_ZONE) return "scrollDown";
  return null;
}

const DIRECTION_ACTION: Record<RepeatDirection, ControllerAction> = {
  up: "moveUp",
  down: "moveDown",
  left: "moveLeft",
  right: "moveRight",
  scrollUp: "scrollUp",
  scrollDown: "scrollDown",
};

export function processControllerReading(
  previous: ControllerInputState,
  reading: ControllerReading,
  now: number,
  options: Readonly<{ promptPopupControls?: boolean }> = {},
): { state: ControllerInputState; actions: ControllerAction[]; backPressed: boolean } {
  const actions: ControllerAction[] = [];
  const chordPressed = pressed(reading, 0) && pressed(reading, 4) && pressed(reading, 5);
  const backPressed = pressed(reading, 1);

  if (chordPressed) {
    if (!previous.chordPressed) actions.push("toggleConsole");
    return {
      actions,
      backPressed,
      state: { buttons: [...reading.buttons], chordPressed: true, direction: null, nextRepeatAt: 0 },
    };
  }

  const shouldersTogether = pressed(reading, 4) && pressed(reading, 5);
  const buttonActions: readonly [number, ControllerAction][] = [
    [0, "activate"],
    [1, "back"],
    [2, "play"],
    [3, "refresh"],
    [8, "openSettings"],
    [9, "toggleSurface"],
    [10, "toggleLayout"],
    [11, "addGame"],
  ];

  for (const [index, action] of buttonActions) {
    if (justPressed(previous, reading, index)) actions.push(action);
  }
  if (!shouldersTogether && justPressed(previous, reading, 4)) actions.push("previousTab");
  if (!shouldersTogether && justPressed(previous, reading, 5)) actions.push("nextTab");
  if (options.promptPopupControls && justPressed(previous, reading, 12)) actions.push("openPrompts");
  if (options.promptPopupControls && justPressed(previous, reading, 13)) actions.push("closePrompts");

  const direction = readDirection(reading, options.promptPopupControls === true);
  let nextRepeatAt = 0;
  if (direction) {
    if (direction !== previous.direction) {
      actions.push(DIRECTION_ACTION[direction]);
      nextRepeatAt = now + INITIAL_REPEAT_MS;
    } else if (now >= previous.nextRepeatAt) {
      actions.push(DIRECTION_ACTION[direction]);
      nextRepeatAt = now + HELD_REPEAT_MS;
    } else {
      nextRepeatAt = previous.nextRepeatAt;
    }
  }

  return {
    actions,
    backPressed,
    state: { buttons: [...reading.buttons], chordPressed: false, direction, nextRepeatAt },
  };
}
