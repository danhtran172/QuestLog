import assert from "node:assert/strict";
import test from "node:test";
import {
  INITIAL_CONTROLLER_STATE,
  isControllerActionAvailable,
  processControllerReading,
} from "../src/lib/controllerInput.ts";

function reading(pressedButtons = [], axes = [0, 0, 0, 0]) {
  const buttons = Array.from({ length: 16 }, (_, index) => pressedButtons.includes(index));
  return { buttons, axes };
}

test("emits a digital action only on the pressed edge", () => {
  const first = processControllerReading(INITIAL_CONTROLLER_STATE, reading([0]), 0);
  const held = processControllerReading(first.state, reading([0]), 16);
  assert.deepEqual(first.actions, ["activate"]);
  assert.deepEqual(held.actions, []);
});

test("exposes the B-button hold state until release", () => {
  const first = processControllerReading(INITIAL_CONTROLLER_STATE, reading([1]), 0);
  const held = processControllerReading(first.state, reading([1]), 450);
  const released = processControllerReading(held.state, reading([]), 500);
  assert.equal(first.backPressed, true);
  assert.equal(held.backPressed, true);
  assert.equal(released.backPressed, false);
  assert.deepEqual(first.actions, ["back"]);
  assert.deepEqual(held.actions, []);
});

test("activation chord emits once and consumes its component buttons", () => {
  const first = processControllerReading(INITIAL_CONTROLLER_STATE, reading([0, 4, 5]), 0);
  const held = processControllerReading(first.state, reading([0, 4, 5]), 16);
  assert.deepEqual(first.actions, ["toggleConsole"]);
  assert.deepEqual(held.actions, []);
});

test("direction fires immediately, waits, then repeats", () => {
  const first = processControllerReading(INITIAL_CONTROLLER_STATE, reading([15]), 100);
  const waiting = processControllerReading(first.state, reading([15]), 300);
  const repeated = processControllerReading(waiting.state, reading([15]), 420);
  assert.deepEqual(first.actions, ["moveRight"]);
  assert.deepEqual(waiting.actions, []);
  assert.deepEqual(repeated.actions, ["moveRight"]);
});

test("right stick produces contextual scroll actions", () => {
  const result = processControllerReading(INITIAL_CONTROLLER_STATE, reading([], [0, 0, 0, 0.8]), 0);
  assert.deepEqual(result.actions, ["scrollDown"]);
});

test("reserves vertical d-pad for the console prompt popup", () => {
  const opened = processControllerReading(
    INITIAL_CONTROLLER_STATE,
    reading([12]),
    0,
    { promptPopupControls: true },
  );
  const closed = processControllerReading(
    opened.state,
    reading([13]),
    16,
    { promptPopupControls: true },
  );

  assert.deepEqual(opened.actions, ["openPrompts"]);
  assert.deepEqual(closed.actions, ["closePrompts"]);
});

test("keeps left-stick vertical navigation while d-pad controls prompts", () => {
  const result = processControllerReading(
    INITIAL_CONTROLLER_STATE,
    reading([], [0, -0.8, 0, 0]),
    0,
    { promptPopupControls: true },
  );
  assert.deepEqual(result.actions, ["moveUp"]);
});

test("L3 and R3 map to layout and add game", () => {
  const result = processControllerReading(INITIAL_CONTROLLER_STATE, reading([10, 11]), 0);
  assert.deepEqual(result.actions, ["toggleLayout", "addGame"]);
});

test("console ignores desktop layout and surface buttons while retaining console commands", () => {
  const result = processControllerReading(INITIAL_CONTROLLER_STATE, reading([8, 9, 10, 11]), 0);
  assert.deepEqual(
    result.actions.filter((action) => isControllerActionAvailable(action, true)),
    ["openSettings", "addGame"],
  );
  assert.deepEqual(
    result.actions.filter((action) => isControllerActionAvailable(action, false)),
    ["openSettings", "toggleSurface", "toggleLayout", "addGame"],
  );
  assert.equal(isControllerActionAvailable("toggleConsole", true), true);
});
