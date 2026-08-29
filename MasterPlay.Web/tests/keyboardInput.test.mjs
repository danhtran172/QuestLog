import assert from "node:assert/strict";
import test from "node:test";
import { getConsoleKeyboardAction } from "../src/lib/keyboardInput.ts";

const reading = (key, overrides = {}) => ({
  key,
  consoleSession: true,
  editing: false,
  ...overrides,
});

test("maps arrows and WASD to the horizontal console carousel", () => {
  assert.equal(getConsoleKeyboardAction(reading("ArrowLeft")), "moveLeft");
  assert.equal(getConsoleKeyboardAction(reading("w")), "moveLeft");
  assert.equal(getConsoleKeyboardAction(reading("ArrowDown")), "moveRight");
  assert.equal(getConsoleKeyboardAction(reading("D")), "moveRight");
});

test("maps console commands to keyboard actions", () => {
  assert.equal(getConsoleKeyboardAction(reading("Enter")), "activate");
  assert.equal(getConsoleKeyboardAction(reading(" ")), "activate");
  assert.equal(getConsoleKeyboardAction(reading("Escape")), "back");
  assert.equal(getConsoleKeyboardAction(reading("q")), "previousTab");
  assert.equal(getConsoleKeyboardAction(reading("e")), "nextTab");
  assert.equal(getConsoleKeyboardAction(reading("v")), "openSettings");
  assert.equal(getConsoleKeyboardAction(reading("l")), "toggleLayout");
  assert.equal(getConsoleKeyboardAction(reading("n")), "addGame");
  assert.equal(getConsoleKeyboardAction(reading("p")), "play");
  assert.equal(getConsoleKeyboardAction(reading("Tab")), "toggleSurface");
});

test("keeps console shortcuts inactive outside a console session", () => {
  assert.equal(getConsoleKeyboardAction(reading("ArrowRight", { consoleSession: false })), null);
  assert.equal(getConsoleKeyboardAction(reading("n", { consoleSession: false })), null);
});

test("protects text editing and modified browser shortcuts", () => {
  assert.equal(getConsoleKeyboardAction(reading("a", { editing: true })), null);
  assert.equal(getConsoleKeyboardAction(reading("e", { ctrlKey: true })), null);
  assert.equal(getConsoleKeyboardAction(reading("Escape", { editing: true })), "back");
});

test("allows held navigation but suppresses repeated commands", () => {
  assert.equal(getConsoleKeyboardAction(reading("ArrowRight", { repeat: true })), "moveRight");
  assert.equal(getConsoleKeyboardAction(reading("Enter", { repeat: true })), null);
  assert.equal(getConsoleKeyboardAction(reading("F10", { repeat: true })), null);
});

test("keeps F10 available with or without an active console session", () => {
  assert.equal(getConsoleKeyboardAction(reading("F10")), "toggleConsole");
  assert.equal(getConsoleKeyboardAction(reading("F10", { consoleSession: false })), "toggleConsole");
});
