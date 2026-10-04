import assert from "node:assert/strict";
import test from "node:test";
import { setConsoleFullscreen, toggleFullscreen } from "../src/lib/desktopBridge.ts";

test("console entry and F11 use native window fullscreen inside WebView", async () => {
  const messages = [];
  const originalWindow = globalThis.window;
  globalThis.window = { chrome: { webview: { postMessage: (message) => messages.push(message) } } };
  try {
    // No document fullscreen API is available: native commands must handle this path.
    await setConsoleFullscreen(true);
    await toggleFullscreen();
    await setConsoleFullscreen(false);
    assert.deepEqual(messages, [
      { type: "questlog.console-fullscreen", enabled: true },
      { type: "questlog.fullscreen-toggle" },
      { type: "questlog.console-fullscreen", enabled: false },
    ]);
  } finally {
    globalThis.window = originalWindow;
  }
});

test("browser console entry is idempotent and F11 toggles document fullscreen", async () => {
  const originalWindow = globalThis.window;
  const originalDocument = globalThis.document;
  let entries = 0;
  let exits = 0;
  globalThis.window = {};
  globalThis.document = {
    fullscreenElement: null,
    documentElement: {
      async requestFullscreen() {
        entries++;
        document.fullscreenElement = document.documentElement;
      },
    },
    async exitFullscreen() {
      exits++;
      document.fullscreenElement = null;
    },
  };
  try {
    await setConsoleFullscreen(true);
    await setConsoleFullscreen(true);
    assert.equal(entries, 1);
    await toggleFullscreen();
    assert.equal(exits, 1);
    await toggleFullscreen();
    assert.equal(entries, 2);
    await setConsoleFullscreen(false);
    await setConsoleFullscreen(false);
    assert.equal(exits, 2);
  } finally {
    globalThis.window = originalWindow;
    globalThis.document = originalDocument;
  }
});

test("browser fullscreen failures propagate so the caller can display an error", async () => {
  const originalWindow = globalThis.window;
  const originalDocument = globalThis.document;
  globalThis.window = {};
  globalThis.document = {
    fullscreenElement: null,
    documentElement: { requestFullscreen: async () => { throw new Error("Fullscreen denied"); } },
  };
  try {
    await assert.rejects(toggleFullscreen(), /Fullscreen denied/);
    await assert.rejects(setConsoleFullscreen(true), /Fullscreen denied/);
  } finally {
    globalThis.window = originalWindow;
    globalThis.document = originalDocument;
  }
});
