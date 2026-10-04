import assert from "node:assert/strict";
import test from "node:test";
import { getConsoleKeyboardAction } from "../src/lib/keyboardInput.ts";

const reading = (key, overrides = {}) => ({
  key,
  consoleSession: true,
  consoleVisible: true,
  overlayOpen: false,
  editing: false,
  ...overrides,
});

test("maps arrows and WASD to the horizontal console carousel", () => {
  assert.equal(getConsoleKeyboardAction(reading("ArrowLeft")), "moveLeft");
  assert.equal(getConsoleKeyboardAction(reading("w")), "moveLeft");
  assert.equal(getConsoleKeyboardAction(reading("ArrowDown")), "moveRight");
  assert.equal(getConsoleKeyboardAction(reading("D")), "moveRight");
});

test("uses spatial directions in gallery and overlays", () => {
  assert.equal(
    getConsoleKeyboardAction(reading("ArrowUp", { consoleVisible: false })),
    "moveUp",
  );
  assert.equal(
    getConsoleKeyboardAction(reading("s", { overlayOpen: true })),
    "moveDown",
  );
  assert.equal(
    getConsoleKeyboardAction(reading("Tab", { overlayOpen: true })),
    null,
  );
  assert.equal(
    getConsoleKeyboardAction(reading("Enter", { overlayOpen: true })),
    "activate",
  );
});

test("maps console commands to keyboard actions", () => {
  assert.equal(getConsoleKeyboardAction(reading("Enter")), "play");
  assert.equal(getConsoleKeyboardAction(reading(" ")), "activate");
  assert.equal(getConsoleKeyboardAction(reading("Escape")), "back");
  assert.equal(getConsoleKeyboardAction(reading("q")), "previousTab");
  assert.equal(getConsoleKeyboardAction(reading("e")), "nextTab");
  assert.equal(getConsoleKeyboardAction(reading("v")), "openSettings");
  assert.equal(getConsoleKeyboardAction(reading("l")), null);
  assert.equal(getConsoleKeyboardAction(reading("n")), "addGame");
  assert.equal(getConsoleKeyboardAction(reading("p")), null);
  assert.equal(getConsoleKeyboardAction(reading("Tab")), "activate");
  assert.equal(getConsoleKeyboardAction(reading("Shift")), "togglePrompts");
});

test("keeps Tab available for normal focus navigation outside the console surface", () => {
  assert.equal(getConsoleKeyboardAction(reading("Tab", { consoleVisible: false })), null);
  assert.equal(getConsoleKeyboardAction(reading("Shift", { consoleVisible: false })), null);
  assert.equal(getConsoleKeyboardAction(reading("Shift", { overlayOpen: true })), null);
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
  assert.equal(getConsoleKeyboardAction(reading("Shift", { repeat: true })), null);
});

test("keeps F10 available with or without an active console session", () => {
  assert.equal(getConsoleKeyboardAction(reading("F10")), "toggleConsole");
  assert.equal(getConsoleKeyboardAction(reading("F10", { consoleSession: false })), "toggleConsole");
});
