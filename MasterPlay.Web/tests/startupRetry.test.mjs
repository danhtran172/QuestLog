import assert from "node:assert/strict";
import test from "node:test";
import { withStartupRetry } from "../src/lib/startupRetry.ts";

const httpError = (status) => Object.assign(new Error(`HTTP ${status}`), { status });
const noWait = async () => {};

test("retries while the Local API is still starting", async () => {
  let calls = 0;
  const result = await withStartupRetry(async () => {
    calls++;
    if (calls === 1) throw new TypeError("Failed to fetch");
    if (calls < 4) throw httpError(500);
    return "config";
  }, noWait);

  assert.equal(result, "config");
  assert.equal(calls, 4);
});

test("client errors are returned immediately", async () => {
  let calls = 0;
  await assert.rejects(
    withStartupRetry(async () => {
      calls++;
      throw httpError(401);
    }, noWait),
    { status: 401 },
  );
  assert.equal(calls, 1);
});

test("gives up after the last delay", async () => {
  let calls = 0;
  const waits = [];
  await assert.rejects(
    withStartupRetry(
      async () => {
        calls++;
        throw httpError(502);
      },
      async (ms) => {
        waits.push(ms);
      },
      [10, 20],
    ),
    { status: 502 },
  );
  assert.equal(calls, 3);
  assert.deepEqual(waits, [10, 20]);
});
