import assert from "node:assert/strict";
import test from "node:test";
import {
  INITIAL_CONTROLLER_STATE,
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

test("L3 and R3 map to layout and add game", () => {
  const result = processControllerReading(INITIAL_CONTROLLER_STATE, reading([10, 11]), 0);
  assert.deepEqual(result.actions, ["toggleLayout", "addGame"]);
});
