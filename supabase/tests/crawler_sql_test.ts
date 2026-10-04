import { PGlite } from "@electric-sql/pglite";
import { strict as assert } from "node:assert";
import { normalize } from "../functions/igdb-crawler/core.ts";

Deno.test("crawler SQL: real migrations, caps, fencing, merge, priorities and permissions", async (t) => {
  const db = new PGlite();
  const sql = async (name: string) =>
    await Deno.readTextFile(
      new URL(`../migrations/${name}.sql`, import.meta.url),
    );
  const scalar = async <T>(query: string, params: unknown[] = []) =>
    (await db.query<{ value: T }>(query, params)).rows[0].value;
  const source = (id: number) =>
    normalize({
      id,
      name: `Game ${id}`,
      updated_at: 100,
      game_type: { type: "Main Game" },
      summary: "original",
      genres: [{ id: 1, name: "RPG" }],
    });
  let run = crypto.randomUUID();
  const claim = () =>
    scalar<Record<string, unknown>>(
      "select public.crawler_claim($1) as value",
      [run],
    );
  const discover = (
    games: unknown[],
    cursor: number,
    scannedThrough = games.length
      ? Number((games.at(-1) as { id: number }).id)
      : null,
  ) =>
    scalar<{ inserted: number; cursor: number }>(
      "select public.crawler_discover($1,$2,$3,$4) as value",
      [run, JSON.stringify(games), cursor, scannedThrough],
    );
  const consume = () =>
    scalar<{ refreshed: number; errors: number; pending: number }>(
      "select public.crawler_consume($1) as value",
      [run],
    );
  const observe = (games: ReturnType<typeof source>[]) =>
    db.query("select public.crawler_observe($1,$2,$3)", [
      run,
      JSON.stringify(games.map((g) => g.id)),
      JSON.stringify(games),
    ]);
  const nextRun = async () => {
    await db.query("select public.crawler_finish($1,'completed','{}')", [run]);
    await db.exec("update crawler.state set next_run_at='-infinity'");
    run = crypto.randomUUID();
    await claim();
  };
  try {
    await db.exec(
      `create role anon; create role authenticated; create role service_role bypassrls;
      create schema auth; create table auth.users(id uuid primary key);
      create schema vault; create table vault.decrypted_secrets(name text, decrypted_secret text);
      create schema net;
      create function net.http_post(url text, headers jsonb, body jsonb, timeout_milliseconds integer)
        returns bigint language sql as $$select 1::bigint$$;
      create function public.set_updated_at() returns trigger language plpgsql as $$begin new.updated_at=now(); return new; end$$;`,
    );
    for (
      const name of [
        "202608240002_metadata_catalog",
        "202609140001_metadata_admin",
        "202609140006_metadata_publishing",
        "20261001175945_igdb_catalog_crawler",
        "20261003164016_admin_crawler_console",
        "20261003171516_crawler_release_date_discovery",
        "20261004061011_crawler_release_cursor_status",
        "20261004063737_crawler_steam_cdn_enrichment",
      ]
    ) {
      await db.exec(await sql(name));
    }
    // Hosted Supabase default privileges on public objects (not private crawler).
    await db.exec(
      "grant usage on schema public to service_role; grant all on all tables in schema public to service_role; grant all on all sequences in schema public to service_role",
    );
    await t.step(
      "disabled by default; authenticated/anon cannot call or read private state",
      async () => {
        assert.deepEqual(await claim(), { skip: "disabled" });
        for (const role of ["anon", "authenticated"]) {
          await db.exec(`set role ${role}`);
          await assert.rejects(() => claim(), /permission denied/);
          await assert.rejects(
            () => db.query("select * from crawler.state"),
            /permission denied/,
          );
          await db.exec("reset role");
        }
      },
    );
    await db.exec(
      "update crawler.state set enabled=true; set role service_role",
    );
    await t.step(
      "claim, overlapping run and 100-insert cap enforced in DB",
      async () => {
        await claim();
        assert.deepEqual(
          await scalar("select public.crawler_claim($1) as value", [
            crypto.randomUUID(),
          ]),
          { skip: "busy" },
        );
        assert.equal(
          (await discover(
            Array.from({ length: 100 }, (_, i) => source(i + 1)),
            0,
          )).inserted,
          100,
        );
        assert.equal((await discover([source(101)], 100)).inserted, 100);
        assert.equal(
          await scalar("select count(*)::int as value from metadata_games"),
          100,
        );
        assert.equal(
          await scalar(
            "select count(*)::int as value from metadata_audit_events where action='create'",
          ),
          100,
        );
        assert.equal(
          await scalar(
            "select count(*)::int as value from metadata_games where revision<>1",
          ),
          0,
        );
        assert.equal(
          await scalar(
            "select count(*)::int as value from crawler.run_items where run_id=$1 and action='inserted'",
            [run],
          ),
          100,
        );
        await assert.rejects(
          () => db.query("select crawler.fence($1)", [crypto.randomUUID()]),
          /LEASE_LOST/,
        );
      },
    );
    await t.step(
      "10-update cap, oldest backlog beats upcoming then earliest upcoming wins",
      async () => {
        await nextRun();
        const games = Array.from(
          { length: 12 },
          (_, i) => ({
            ...source(i + 1),
            summary: "changed",
            updated_at: 200,
            publish_date: i === 11 ? "2090-01-01" : null,
          }),
        );
        await observe(games);
        await db.exec(
          "update crawler.queue set queued_at=now()-interval '8 days' where game_id=(select id from metadata_games where igdb_id=11)",
        );
        const result = await consume();
        assert.equal(result.refreshed, 10);
        assert.equal(result.pending, 2);
        assert.equal(
          await scalar(
            "select summary as value from metadata_games where igdb_id=12",
          ),
          "changed",
        );
        assert.equal(
          await scalar(
            "select summary as value from metadata_games where igdb_id=11",
          ),
          "changed",
        );
        assert.equal((await consume()).refreshed, 10);
        assert.equal(
          await scalar(
            "select count(*)::int as value from crawler.run_items where run_id=$1 and action='refreshed'",
            [run],
          ),
          10,
        );
      },
    );
    await t.step(
      "admin overrides and unpublish survive source changes; source-owned fields update",
      async () => {
        await nextRun();
        await consume();
        await db.exec(
          "update metadata_games set name='Admin title', manually_unpublished=true where igdb_id=1",
        );
        await observe([{
          ...source(1),
          name: "Remote title",
          summary: "latest",
          updated_at: 300,
        }]);
        await consume();
        const row = await scalar<Record<string, unknown>>(
          "select to_jsonb(g) as value from metadata_games g where igdb_id=1",
        );
        assert.equal(row.name, "Admin title");
        assert.equal(row.summary, "latest");
        assert.equal(row.is_published, false);
        assert.equal(row.banner_url, null);
        assert.equal(row.meta_score, null);
        const revision = row.revision;
        await observe([{
          ...source(1),
          name: "Remote title",
          summary: "latest",
          updated_at: 300,
        }]);
        await consume();
        assert.equal(
          await scalar(
            "select revision as value from metadata_games where igdb_id=1",
          ),
          revision,
        );
      },
    );
    await t.step(
      "failed discovery rolls back rows AND cursor; retry starts at same checkpoint",
      async () => {
        await nextRun();
        await assert.rejects(() =>
          discover([source(101), { ...source(102), name: "" }], 100)
        );
        assert.equal(
          await scalar("select discovery_id as value from crawler.state"),
          100,
        );
        assert.equal(
          await scalar(
            "select count(*)::int as value from metadata_games where igdb_id=101",
          ),
          0,
        );
        assert.equal(
          (await discover([source(101), source(102)], 100)).cursor,
          102,
        );
      },
    );
    await t.step(
      "bad queued record is retained with retry; other games still update",
      async () => {
        await observe([{ ...source(50), genres: [{ id: 1, name: "" }] }, {
          ...source(51),
          summary: "healthy",
        }]);
        const result = await consume();
        assert.equal(result.errors, 1);
        assert.equal(
          await scalar(
            "select summary as value from metadata_games where igdb_id=51",
          ),
          "healthy",
        );
        assert.equal(
          await scalar(
            "select attempts as value from crawler.queue where game_id=(select id from metadata_games where igdb_id=50)",
          ),
          1,
        );
      },
    );
    await t.step(
      "stale observations cannot overwrite newer queued snapshots; reversions cancel queue",
      async () => {
        const changed = { ...source(60), summary: "new", updated_at: 200 };
        await observe([changed]);
        await observe([{ ...source(60), updated_at: 150 }]);
        assert.equal(
          await scalar(
            "select payload->>'summary' as value from crawler.queue where game_id=(select id from metadata_games where igdb_id=60)",
          ),
          "new",
        );
        // Same-timestamp relations can change and can return to the accepted state.
        await observe([{ ...source(61), genres: [] }]);
        await observe([source(61)]);
        assert.equal(
          await scalar(
            "select count(*)::int as value from crawler.queue where game_id=(select id from metadata_games where igdb_id=61)",
          ),
          0,
        );
      },
    );
    await t.step(
      "missing source flagged without deletion; identity edits invalidate queued payload",
      async () => {
        await db.query("select public.crawler_observe($1,'[62]','[]')", [run]);
        assert.equal(
          await scalar(
            "select missing_since is not null as value from crawler.sources where game_id=(select id from metadata_games where igdb_id=62)",
          ),
          true,
        );
        await db.exec(
          "update metadata_games set igdb_id=1060 where igdb_id=60",
        );
        assert.equal(
          await scalar(
            "select count(*)::int as value from crawler.queue where game_id=(select id from metadata_games where igdb_id=1060)",
          ),
          0,
        );
      },
    );
    await t.step(
      "unresolved parents link after discovery; legacy fields remain fill-only",
      async () => {
        await nextRun();
        await discover([
          { ...source(103), parent_id: 104, is_dlc: true },
          source(104),
        ], 102);
        await observe([{ ...source(103), parent_id: 104, is_dlc: true }]);
        await consume();
        assert.equal(
          await scalar(
            "select base_metadata_game_id is not null as value from metadata_games where igdb_id=103",
          ),
          true,
        );
        await db.exec(
          "insert into metadata_games(igdb_id,name) values(9000,'Legacy title')",
        );
        await observe([source(9000)]);
        await consume();
        assert.equal(
          await scalar(
            "select name as value from metadata_games where igdb_id=9000",
          ),
          "Legacy title",
        );
        assert.equal(
          await scalar(
            "select summary as value from metadata_games where igdb_id=9000",
          ),
          "original",
        );
      },
    );
    await t.step(
      "malformed discovery IDs can be quarantined without poisoning the cursor",
      async () => {
        await nextRun();
        const result = await discover([source(105)], 104, 106);
        assert.equal(result.cursor, 106);
        assert.equal(
          await scalar(
            "select count(*)::int as value from metadata_games where igdb_id=106",
          ),
          0,
        );
      },
    );
    await t.step(
      "discovery stops earlier to preserve database headroom for updates",
      async () => {
        await db.exec(
          "update crawler.state set max_database_bytes=ceil(pg_database_size(current_database())/0.95)",
        );
        const result = await discover([source(107)], 106);
        assert.equal(result.cursor, 106);
        await consume();
      },
    );
    await t.step("budget and expired lease block writes", async () => {
      await db.exec("update crawler.state set max_database_bytes=1");
      await assert.rejects(() => consume(), /DATABASE_BUDGET/);
      await db.exec(
        "update crawler.state set max_database_bytes=471859200, lease_until=now()-interval '1 second'",
      );
      await assert.rejects(() => consume(), /LEASE_LOST/);
    });
  } finally {
    await db.close();
  }
});
