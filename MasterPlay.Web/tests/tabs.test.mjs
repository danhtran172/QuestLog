import assert from "node:assert/strict";
import test from "node:test";
import { CONSOLE_TABS, TABS } from "../src/lib/api/domain.ts";

test("keeps the regular four-tab navigation unchanged", () => {
  assert.deepEqual(TABS, ["all", "planning", "finished", "queue"]);
});

test("places the fixed Console view first only in Console Mode navigation", () => {
  assert.deepEqual(CONSOLE_TABS, ["console", "all", "planning", "finished", "queue"]);
});
