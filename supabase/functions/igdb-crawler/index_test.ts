import { strict as assert } from "node:assert";
import { createProvider, handler } from "./handler.ts";

Deno.test("handler denies requests before DB/provider work and validates body", async () => {
  const oldFetch = globalThis.fetch;
  const oldSecret = Deno.env.get("CRAWLER_SECRET");
  const secret = "test-only-".repeat(5);
  Deno.env.set("CRAWLER_SECRET", secret);
  globalThis.fetch = () => {
    throw Error("UNEXPECTED_NETWORK");
  };
  try {
    assert.equal(
      (await handler(new Request("https://example.test"))).status,
      405,
    );
    assert.equal(
      (await handler(new Request("https://example.test", { method: "POST" })))
        .status,
      401,
    );
    for (const body of ["{", "[]", '{"dryRun":"false"}', '{"unknown":true}']) {
      assert.equal(
        (await handler(
          new Request("https://example.test", {
            method: "POST",
            headers: { "x-crawler-secret": secret },
            body,
          }),
        )).status,
        400,
      );
    }
    assert.equal(
      (await handler(
        new Request("https://example.test", {
          method: "POST",
          headers: { "x-crawler-secret": secret },
          body: "x".repeat(1025),
        }),
      )).status,
      413,
    );
  } finally {
    globalThis.fetch = oldFetch;
    if (oldSecret === undefined) Deno.env.delete("CRAWLER_SECRET");
    else Deno.env.set("CRAWLER_SECRET", oldSecret);
  }
});

Deno.test("provider renews on 401 once, then returns normalized games", async () => {
  const oldFetch = globalThis.fetch;
  let tokens = 0, games = 0;
  globalThis.fetch = (input, init) => {
    if (String(input).includes("oauth2")) {
      tokens++;
      return Promise.resolve(
        Response.json({ access_token: `test-token-${tokens}`, expires_in: 0 }),
      );
    }
    games++;
    assert.equal(new Headers(init?.headers).get("Client-ID"), "test-client");
    return Promise.resolve(
      games === 1 ? new Response(null, { status: 401 }) : Response.json([
        {
          id: 1,
          name: "Game",
          updated_at: 1,
          game_type: { type: "Main Game" },
        },
      ]),
    );
  };
  try {
    const result = await createProvider(
      "test-client",
      "test-secret",
      Date.now() + 20000,
      1,
    ).games("fields id;");
    assert.equal(result.games.length, 1);
    assert.equal(tokens, 2);
    assert.equal(games, 2);
  } finally {
    globalThis.fetch = oldFetch;
  }
});

Deno.test("provider isolates malformed records with valid IDs", async () => {
  const oldFetch = globalThis.fetch;
  globalThis.fetch = (input) =>
    Promise.resolve(
      String(input).includes("oauth2")
        ? Response.json({ access_token: "test", expires_in: 0 })
        : Response.json([
          {
            id: 20,
            name: "Valid",
            updated_at: 1,
            game_type: { type: "Main Game" },
          },
          { id: 21, name: "Invalid", updated_at: 1 },
        ]),
    );
  try {
    const result = await createProvider("test", "test", Date.now() + 20000, 1)
      .games("fields id;");
    assert.equal(result.games.length, 1);
    assert.deepEqual(result.rejectedIds, [21]);
    assert.equal(result.scannedThrough, 21);
  } finally {
    globalThis.fetch = oldFetch;
  }
});

Deno.test("long Retry-After is deferred without an unbounded retry loop", async () => {
  const oldFetch = globalThis.fetch;
  let games = 0;
  globalThis.fetch = (input) => {
    if (String(input).includes("oauth2")) {
      return Promise.resolve(
        Response.json({ access_token: "test", expires_in: 0 }),
      );
    }
    games++;
    return Promise.resolve(
      new Response(null, { status: 429, headers: { "Retry-After": "120" } }),
    );
  };
  try {
    await assert.rejects(
      () =>
        createProvider("test", "test", Date.now() + 20000, 1).games("fields id;"),
      /IGDB_RETRY_DEFERRED/,
    );
    assert.equal(games, 1);
  } finally {
    globalThis.fetch = oldFetch;
  }
});

Deno.test("expired time budget makes no external request", async () => {
  const oldFetch = globalThis.fetch;
  globalThis.fetch = () => {
    throw Error("UNEXPECTED_NETWORK");
  };
  try {
    await assert.rejects(
      () => createProvider("test", "test", Date.now() - 1, 1).games("fields id;"),
      /TIME_BUDGET/,
    );
  } finally {
    globalThis.fetch = oldFetch;
  }
});
