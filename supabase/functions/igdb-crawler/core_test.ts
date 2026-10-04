import { strict as assert } from "node:assert";
import {
  authorized,
  normalize,
  type Rpc,
  runCrawler,
  safeError,
} from "./core.ts";

const game = (id = 1) =>
  normalize({
    id,
    name: `Game ${id}`,
    updated_at: 100,
    game_type: { type: "Main Game" },
  });
const batch = (games = [game()]) => ({
  games,
  scannedThrough: games.at(-1)?.id ?? null,
  scannedReleaseAt: games.at(-1)?.release_at ?? null,
  rejectedIds: [] as number[],
});
function mockRpc(
  call: (name: string, body?: Record<string, unknown>) => unknown,
): Rpc {
  return <T>(name: string, body?: Record<string, unknown>) =>
    Promise.resolve(call(name, body) as T);
}

Deno.test("normalization keeps image roles, dates and company roles separate", () => {
  const g = normalize({
    id: 1,
    name: " DLC ",
    updated_at: 100,
    game_type: { type: "Expansion" },
    first_release_date: 0,
    cover: { image_id: "co123" },
    involved_companies: [{ developer: true, company: { name: "Dev" } }, {
      publisher: true,
      company: { name: "Pub" },
    }],
    collections: [{ id: 9, name: "B" }, { id: 2, name: "A" }],
    genres: [{ id: 2, name: "RPG" }],
    external_games: [{ category: 1, uid: "480" }],
  });
  assert.equal(g.is_dlc, true);
  assert.equal(g.publish_date, "1970-01-01");
  assert.equal(g.developer, "Dev");
  assert.equal(g.publisher, "Pub");
  assert.equal(g.series, "A");
  assert.match(g.poster_url!, /t_cover_big_2x\/co123.jpg$/);
  assert.equal(
    g.banner_url,
    "https://cdn.akamai.steamstatic.com/steam/apps/480/header.jpg",
  );
  assert.equal("meta_score" in g, false);
});
Deno.test("malformed source records fail closed", () => {
  assert.throws(() => normalize({ id: 1 }));
  assert.throws(() =>
    normalize({
      id: 1,
      name: "A",
      updated_at: 1,
      game_type: { type: "Main Game" },
      cover: { image_id: "../bad" },
    })
  );
});
Deno.test("auth rejects absent, weak and wrong secrets; errors do not reveal upstream text", async () => {
  const secret = "a".repeat(40);
  assert.equal(await authorized(secret, secret), true);
  assert.equal(await authorized("b".repeat(40), secret), false);
  assert.equal(await authorized(null, secret), false);
  assert.equal(await authorized("short", "short"), false);
  assert.equal(
    safeError(new Error("access_token=private")),
    "CRAWLER_REQUEST_FAILED",
  );
});
Deno.test("disabled and busy claims make zero provider calls", async () => {
  for (const skip of ["disabled", "busy", "not_due", "database_budget"]) {
    const result = await runCrawler(
      mockRpc(() => ({ skip })),
      {
        games: () => {
          throw Error("unexpected");
        },
      },
      false,
      "run",
    );
    assert.deepEqual(result, { skipped: skip });
  }
});
Deno.test("dry run reads only and caps discovery at one page", async () => {
  const calls: string[] = [];
  let queries = 0;
  const result = await runCrawler(
    mockRpc((name) => {
      calls.push(name);
      assert.equal(name, "crawler_targets");
      return { ids: [1], discovery_id: 0, release_cursor_at: 1_700_000_000 };
    }),
    {
      games: () => {
        queries++;
        return Promise.resolve(batch());
      },
    },
    true,
    "run",
  );
  assert.deepEqual(calls, ["crawler_targets"]);
  assert.equal(queries, 2);
  assert.deepEqual(result, {
    dryRun: true,
    checked: 1,
    observed: 1,
    discoverySample: 1,
    sourceRejected: 0,
  });
});
Deno.test("queued changes and observations run before discovery; insertion stops at 100", async () => {
  const calls: string[] = [];
  let discoveryQuery = "";
  const rpc = mockRpc((name) => {
    calls.push(name);
    if (name === "crawler_claim") return { ids: [1], discovery_id: 0, release_cursor_at: 1_700_000_000 };
    if (name === "crawler_consume") {
      return { refreshed: 10, pending: 0, errors: 0 };
    }
    if (name === "crawler_discover_release") {
      return { inserted: 100, cursor: 100, releaseCursorAt: 1_700_000_100, end: false };
    }
    return {};
  });
  await runCrawler(
    rpc,
    {
      games: (query) => {
        calls.push("provider");
        discoveryQuery = query;
        return Promise.resolve(batch());
      },
    },
    false,
    "run",
  );
  assert.deepEqual(calls, [
    "crawler_claim",
    "crawler_consume",
    "provider",
    "crawler_observe",
    "crawler_consume",
    "provider",
    "crawler_discover_release",
    "crawler_finish",
  ]);
  assert.match(discoveryQuery, /limit 20;/);
  assert.match(discoveryQuery, /first_release_date <= \d+/);
  assert.match(discoveryQuery, /sort first_release_date asc;/);
  assert.doesNotMatch(discoveryQuery, /sort[^;]*,/);
});
Deno.test("failed DB commit stops provider work and reports failure", async () => {
  let queries = 0;
  let finished = false;
  const rpc = mockRpc((name, body) => {
    if (name === "crawler_claim") return { ids: [], discovery_id: 0, release_cursor_at: 1_700_000_000 };
    if (name === "crawler_consume") return { refreshed: 0, errors: 0 };
    if (name === "crawler_discover_release") throw Error("SUPABASE_HTTP_402");
    if (name === "crawler_finish") {
      finished = body?.p_status === "failed";
      return {};
    }
  });
  await assert.rejects(() =>
    runCrawler(
      rpc,
      {
        games: () => {
          queries++;
          return Promise.resolve(batch());
        },
      },
      false,
      "run",
    ), /402/);
  assert.equal(queries, 1);
  assert.equal(finished, true);
});

Deno.test("malformed discovery records are reported and checkpointed", async () => {
  const bodies: Record<string, unknown>[] = [];
  let providerCalls = 0;
  const rpc = mockRpc((name, body) => {
    if (name === "crawler_claim") return { ids: [], discovery_id: 10, release_cursor_at: 1_700_000_000 };
    if (name === "crawler_consume") {
      return { refreshed: 0, pending: 0, errors: 0 };
    }
    if (name === "crawler_discover_release") {
      bodies.push(body!);
      return body?.p_scanned_through == null
        ? { inserted: 1, cursor: 0, releaseCursorAt: 1_700_000_000, end: true }
        : { inserted: 1, cursor: 12, releaseCursorAt: 1_700_000_100, end: false };
    }
    if (name === "crawler_finish") return body?.p_report;
  });
  const result = await runCrawler(
    rpc,
    {
      games: () =>
        Promise.resolve(
          providerCalls++ === 0
            ? { games: [game(11)], scannedThrough: 12, scannedReleaseAt: 1_700_000_100, rejectedIds: [12] }
            : { games: [], scannedThrough: null, scannedReleaseAt: null, rejectedIds: [] },
        ),
    },
    false,
    "run",
  ) as Record<string, unknown>;
  assert.equal(bodies[0].p_scanned_through_id, 12);
  assert.equal(result.sourceRejected, 1);
  assert.equal(result.errors, 1);
});
