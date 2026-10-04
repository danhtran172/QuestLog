-- Steam enrichment is identity-safe: IGDB already supplies the Steam app id.
-- The Edge Function forms a public CDN URL; it never searches the Steam Store.
-- Banner remains source-owned only while no admin override exists.
create or replace function crawler.apply_game(p_game bigint, p jsonb, p_created boolean default false) returns boolean
language plpgsql set search_path = pg_catalog, public, crawler as $$
declare g public.metadata_games; s crawler.sources; merged jsonb; incoming jsonb;
  k text; v_series bigint; v_base bigint; v_id bigint; item jsonb;
  before_tags jsonb; after_tags jsonb; changed boolean; updated public.metadata_games;
begin
  select * into g from public.metadata_games where id=p_game for update;
  select * into s from crawler.sources where game_id=p_game;
  if g.igdb_id is distinct from (p->>'id')::bigint then raise exception 'CRAWLER_ID_CHANGED'; end if;
  if length(btrim(p->>'name'))=0 or p->>'name' is null then raise exception 'CRAWLER_NAME_REQUIRED'; end if;
  if p->>'series' is not null then
    insert into public.metadata_series(name) values(p->>'series') on conflict(name) do nothing;
    select id into v_series from public.metadata_series where name=p->>'series';
  end if;
  select id into v_base from public.metadata_games where igdb_id=(p->>'parent_id')::bigint and id<>p_game;
  incoming := (p - array['id','updated_at','checksum','genres','themes','parent_id','series'])
    || jsonb_build_object('series_id',v_series,'base_metadata_game_id',v_base);
  merged := to_jsonb(g);
  foreach k in array array['name','developer','publisher','summary','publish_date',
      'is_dlc','poster_url','banner_url','series_id','base_metadata_game_id'] loop
    if not (k=any(coalesce(s.overrides,'{}'))) and
      (merged->k='null'::jsonb or merged->>k='' or
       (s.baseline is not null and merged->k is not distinct from s.baseline->k)) then
      merged := jsonb_set(merged,array[k],coalesce(incoming->k,'null'::jsonb));
    end if;
  end loop;
  select jsonb_build_object('genres',(select jsonb_agg(genre_id order by genre_id) from public.metadata_game_genres where metadata_game_id=p_game),
    'themes',(select jsonb_agg(theme_id order by theme_id) from public.metadata_game_themes where metadata_game_id=p_game)) into before_tags;
  delete from public.metadata_game_genres where metadata_game_id=p_game;
  for item in select value from jsonb_array_elements(p->'genres') loop
    insert into public.metadata_genres(name) values(item->>'name') on conflict(name) do nothing;
    select id into v_id from public.metadata_genres where name=item->>'name';
    insert into public.metadata_game_genres values(p_game,v_id) on conflict do nothing;
  end loop;
  delete from public.metadata_game_themes where metadata_game_id=p_game;
  for item in select value from jsonb_array_elements(p->'themes') loop
    insert into public.metadata_themes(name) values(item->>'name') on conflict(name) do nothing;
    select id into v_id from public.metadata_themes where name=item->>'name';
    insert into public.metadata_game_themes values(p_game,v_id) on conflict do nothing;
  end loop;
  select jsonb_build_object('genres',(select jsonb_agg(genre_id order by genre_id) from public.metadata_game_genres where metadata_game_id=p_game),
    'themes',(select jsonb_agg(theme_id order by theme_id) from public.metadata_game_themes where metadata_game_id=p_game)) into after_tags;
  changed := merged is distinct from to_jsonb(g) or before_tags is distinct from after_tags;
  if changed or p_created then
    updated := jsonb_populate_record(g,merged);
    perform set_config('questlog.crawler_write','on',true);
    update public.metadata_games set name=updated.name, developer=updated.developer,
      publisher=updated.publisher, summary=updated.summary, publish_date=updated.publish_date,
      is_dlc=updated.is_dlc, poster_url=updated.poster_url, banner_url=updated.banner_url, series_id=updated.series_id,
      base_metadata_game_id=updated.base_metadata_game_id, revision=g.revision+case when p_created then 0 else 1 end
    where id=p_game returning * into updated;
    perform set_config('questlog.crawler_write','off',true);
    insert into public.metadata_audit_events(entity,entity_id,action,before,after)
    values('game',p_game,case when p_created then 'create' else 'update' end,
      case when not p_created then to_jsonb(g)-'search_vector' end,
      (to_jsonb(updated)-'search_vector') || jsonb_build_object('source','igdb-crawler'));
  end if;
  insert into crawler.sources(game_id,payload,baseline,checked_at)
    values(p_game,p,incoming,now()) on conflict(game_id) do update set
      payload=excluded.payload,baseline=excluded.baseline,checked_at=now(),missing_since=null;
  return changed;
end $$;
