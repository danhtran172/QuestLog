-- Hoan tac 20261004120000_admin_list_page_offset.sql.
--
-- Xoa ban 12 tham so. Sau do chay lai MUC 12 (ham `admin_list_metadata_games`)
-- cua 202609140006_metadata_publishing.sql de co lai ban 11 tham so.
drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean, int);
