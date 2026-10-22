import assert from "node:assert/strict";
import test from "node:test";
import { applyRunningGames, createSseParser } from "../src/lib/runningGames.ts";

function collect() {
  const events = [];
  const push = createSseParser((event, data) => events.push([event, data]));
  return { events, push };
}

test("reads an event split across chunks and skips ping comments", () => {
  const { events, push } = collect();
  push(": ping\n\nevent: runningPro");
  push('cesses\ndata: {"gameIds":["a"]}\n');
  assert.deepEqual(events, []);
  push("\n");
  assert.deepEqual(events, [["runningProcesses", '{"gameIds":["a"]}']]);
});

test("accepts CRLF line endings and joins multi-line data", () => {
  const { events, push } = collect();
  push("event: x\r\ndata: one\r\ndata: two\r\n\r\n");
  assert.deepEqual(events, [["x", "one\ntwo"]]);
});

test("an event without a name falls back to message, the next one resets", () => {
  const { events, push } = collect();
  push("event: runningProcesses\ndata: 1\n\ndata: 2\n\n");
  assert.deepEqual(events, [["runningProcesses", "1"], ["message", "2"]]);
});

test("marks only the listed games as running in a library page", () => {
  const page = {
    items: [{ id: "a", isRunning: false }, { id: "b", isRunning: true }],
    total: 2,
  };
  const next = applyRunningGames(page, new Set(["a"]));
  assert.deepEqual(next.items, [{ id: "a", isRunning: true }, { id: "b", isRunning: false }]);
  assert.equal(next.total, 2);
});

test("keeps the same reference when nothing changes", () => {
  const queue = [{ id: "a", isRunning: true }, { id: "b", isRunning: false }];
  assert.equal(applyRunningGames(queue, new Set(["a"])), queue);

  const page = { items: queue };
  assert.equal(applyRunningGames(page, new Set(["a"])), page);
});

test("updates a single game detail and leaves unknown shapes alone", () => {
  assert.deepEqual(
    applyRunningGames({ id: "a", isRunning: true, name: "A" }, new Set()),
    { id: "a", isRunning: false, name: "A" },
  );
  const other = { total: 3 };
  assert.equal(applyRunningGames(other, new Set(["a"])), other);
});
