import { PGlite } from "@electric-sql/pglite";
import { citext } from "@electric-sql/pglite/contrib/citext";
import { strict as assert } from "node:assert";

const migration = async (name: string) =>
  await Deno.readTextFile(new URL(`../migrations/${name}.sql`, import.meta.url));

Deno.test("account SQL: Google sign-up creates a username_required profile", async (t) => {
  const db = new PGlite({ extensions: { citext } });
  await db.exec(
    `create role anon; create role authenticated; create role supabase_auth_admin;
     create schema extensions;
     create schema auth;
     create table auth.users(
       id uuid primary key,
       email text not null,
       raw_user_meta_data jsonb not null default '{}'::jsonb,
       raw_app_meta_data jsonb not null default '{}'::jsonb
     );
     create function auth.uid() returns uuid language sql stable as $$ select null::uuid $$;`,
  );
  await db.exec(await migration("202608240001_account_foundation"));

  // Rows created before the new migration must be backfilled.
  await db.exec(
    `insert into auth.users(id, email, raw_user_meta_data, raw_app_meta_data) values
       ('00000000-0000-0000-0000-000000000001', 'admin@example.test', '{"username":"Admin"}', '{"provider":"email"}'),
       ('00000000-0000-0000-0000-000000000002', 'old@example.test', '{"username":"Old.User"}', '{"provider":"email"}');
     update public.profiles set role = 'admin' where user_id = '00000000-0000-0000-0000-000000000001';`,
  );
  await db.exec(await migration("20261004150000_google_signup_onboarding"));

  const profile = async (id: string) =>
    (await db.query<Record<string, unknown>>(
      `select username::text as username, onboarding_state, signup_method, has_password
         from public.profiles where user_id = $1`,
      [id],
    )).rows[0];

  await t.step("backfills existing rows", async () => {
    assert.deepEqual(await profile("00000000-0000-0000-0000-000000000001"), {
      username: "Admin",
      onboarding_state: "complete",
      signup_method: "bootstrap",
      has_password: true,
    });
    assert.deepEqual(await profile("00000000-0000-0000-0000-000000000002"), {
      username: "Old.User",
      onboarding_state: "complete",
      signup_method: "password",
      has_password: true,
    });
  });

  await t.step("password sign-up stays complete", async () => {
    await db.exec(
      `insert into auth.users(id, email, raw_user_meta_data, raw_app_meta_data)
       values ('00000000-0000-0000-0000-000000000003', 'new@example.test', '{"username":"Newbie"}', '{"provider":"email"}')`,
    );
    assert.deepEqual(await profile("00000000-0000-0000-0000-000000000003"), {
      username: "Newbie",
      onboarding_state: "complete",
      signup_method: "password",
      has_password: true,
    });
  });

  await t.step("Google sign-up has no username and no password", async () => {
    await db.exec(
      `insert into auth.users(id, email, raw_user_meta_data, raw_app_meta_data) values
         ('00000000-0000-0000-0000-000000000004', 'g1@example.test', '{"full_name":"G One","username":"ignored"}', '{"provider":"google"}'),
         ('00000000-0000-0000-0000-000000000005', 'g2@example.test', '{"full_name":"G Two"}', '{"provider":"google"}')`,
    );
    for (const id of ["00000000-0000-0000-0000-000000000004", "00000000-0000-0000-0000-000000000005"]) {
      assert.deepEqual(await profile(id), {
        username: null,
        onboarding_state: "username_required",
        signup_method: "google",
        has_password: false,
      });
    }
  });

  await t.step("email sign-up without username is still rejected", async () => {
    await assert.rejects(
      db.exec(
        `insert into auth.users(id, email, raw_app_meta_data)
         values ('00000000-0000-0000-0000-000000000006', 'x@example.test', '{"provider":"email"}')`,
      ),
      /USERNAME_REQUIRED/,
    );
  });

  await t.step("complete requires a username", async () => {
    await assert.rejects(
      db.exec(
        `update public.profiles set onboarding_state = 'complete'
          where user_id = '00000000-0000-0000-0000-000000000004'`,
      ),
      /profiles_complete_has_username/,
    );
    await db.exec(
      `update public.profiles
          set username = 'GoogleUser', normalized_username = 'googleuser', onboarding_state = 'complete'
        where user_id = '00000000-0000-0000-0000-000000000004'`,
    );
    assert.equal((await profile("00000000-0000-0000-0000-000000000004")).onboarding_state, "complete");
  });

  await t.step("claimed usernames stay unique", async () => {
    await assert.rejects(
      db.exec(
        `update public.profiles
            set username = 'googleUSER', normalized_username = 'googleuser', onboarding_state = 'complete'
          where user_id = '00000000-0000-0000-0000-000000000005'`,
      ),
      /profiles_username_key/,
    );
  });

  await t.step("rejects unknown onboarding and signup values", async () => {
    await assert.rejects(
      db.exec(`update public.profiles set onboarding_state = 'pending'`),
      /profiles_onboarding_state_check/,
    );
    await assert.rejects(
      db.exec(`update public.profiles set signup_method = 'github'`),
      /profiles_signup_method_check/,
    );
  });

  await db.close();
});
