-- Hotfix for databases where 20261003164016 was applied before its final
-- security-definer clauses were added. Safe to run repeatedly.
alter function public.admin_crawler_status() security definer;
alter function public.admin_set_crawler_enabled(boolean) security definer;
alter function public.admin_trigger_crawler() security definer;
alter function public.admin_list_crawler_runs(jsonb,integer) security definer;
alter function public.admin_list_crawler_run_items(uuid,jsonb,integer) security definer;
alter function public.admin_list_crawler_games(jsonb,integer) security definer;
