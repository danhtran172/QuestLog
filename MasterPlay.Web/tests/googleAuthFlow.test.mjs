import assert from "node:assert/strict";
import test from "node:test";
import {
  GOOGLE_MAX_TRANSIENT_FAILURES,
  GoogleFlowExpiredError,
  isTransientError,
  runGoogleFlow,
} from "../src/lib/googleAuthFlow.ts";

function harness({ polls, opened = true, expiresInMs = 600_000 }) {
  let clock = 0;
  const calls = { completes: 0, opened: [] };
  const deps = {
    start: async () => ({
      flowId: "flow-1",
      authorizeUrl: "https://project.supabase.test/auth/v1/authorize",
      expiresAt: new Date(expiresInMs).toISOString(),
    }),
    complete: async (flowId) => {
      assert.equal(flowId, "flow-1");
      const next = polls[calls.completes++];
      if (next instanceof Error) throw next;
      return next;
    },
    open: (url) => {
      calls.opened.push(url);
      return opened;
    },
    sleep: async (ms) => {
      clock += ms;
    },
    now: () => clock,
  };
  return { deps, calls };
}

const httpError = (status, code = "X") => Object.assign(new Error(code), { status, code });

test("polls until the Host reports a completed session", async () => {
  const { deps, calls } = harness({
    polls: [{ status: "pending" }, { status: "pending" }, { status: "completed", session: { accessToken: "a" } }],
  });
  const events = [];

  const session = await runGoogleFlow(deps, new AbortController().signal, (event) => events.push(event));

  assert.deepEqual(session, { accessToken: "a" });
  assert.equal(calls.completes, 3);
  assert.deepEqual(events, [{ type: "opened", authorizeUrl: "https://project.supabase.test/auth/v1/authorize" }]);
});

test("reports a blocked popup but keeps waiting", async () => {
  const { deps } = harness({ opened: false, polls: [{ status: "completed", session: { accessToken: "a" } }] });
  const events = [];

  await runGoogleFlow(deps, new AbortController().signal, (event) => events.push(event));

  assert.equal(events[0].type, "blocked");
});

test("retries transient failures, then gives up", async () => {
  const transient = httpError(503);
  const recovered = harness({
    polls: [transient, transient, { status: "completed", session: { accessToken: "ok" } }],
  });
  assert.deepEqual(await runGoogleFlow(recovered.deps, new AbortController().signal), { accessToken: "ok" });

  const failing = harness({ polls: Array(GOOGLE_MAX_TRANSIENT_FAILURES).fill(transient) });
  await assert.rejects(runGoogleFlow(failing.deps, new AbortController().signal), transient);
});

test("a final API error stops the flow immediately", async () => {
  const locked = httpError(423, "ACCOUNT_LOCKED");
  const { deps, calls } = harness({ polls: [locked, { status: "pending" }] });

  await assert.rejects(runGoogleFlow(deps, new AbortController().signal), locked);
  assert.equal(calls.completes, 1);
});

test("expires with the server-side flow", async () => {
  const { deps } = harness({ polls: Array(10).fill({ status: "pending" }), expiresInMs: 4_000 });

  await assert.rejects(runGoogleFlow(deps, new AbortController().signal), GoogleFlowExpiredError);
});

test("cancel stops polling", async () => {
  const controller = new AbortController();
  const { deps, calls } = harness({ polls: [{ status: "pending" }, { status: "pending" }] });
  const complete = deps.complete;
  deps.complete = async (flowId) => {
    const result = await complete(flowId);
    controller.abort();
    return result;
  };

  await assert.rejects(runGoogleFlow(deps, controller.signal), { name: "AbortError" });
  assert.equal(calls.completes, 1);
});

test("only network errors and 5xx are transient", () => {
  assert.equal(isTransientError(new TypeError("Failed to fetch")), true);
  assert.equal(isTransientError(httpError(502)), true);
  assert.equal(isTransientError(httpError(410)), false);
  assert.equal(isTransientError(new Error("other")), false);
});
