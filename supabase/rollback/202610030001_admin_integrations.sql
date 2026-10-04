-- This rollback removes only QuestLog's integration metadata and RPCs. It does
-- not delete Vault entries automatically: deleting credentials during a schema
-- rollback is irreversible and must be an explicit operator action.

drop function if exists public.crawler_igdb_credentials();
drop function if exists public.admin_record_integration_test(text, boolean, text, uuid);
drop function if exists public.admin_read_integration_credentials(text);
drop function if exists public.admin_replace_integration_credentials(text, jsonb, uuid);
drop function if exists public.admin_list_integrations();
drop schema if exists integrations cascade;
