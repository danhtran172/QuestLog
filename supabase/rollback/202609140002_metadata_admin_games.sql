drop function if exists public.admin_delete_metadata_game(bigint, uuid);
drop function if exists public.admin_update_metadata_game(bigint, jsonb, bigint, uuid);
drop function if exists public.admin_create_metadata_game(jsonb, uuid, boolean);
drop function if exists public.metadata_game_payload_error(jsonb);
