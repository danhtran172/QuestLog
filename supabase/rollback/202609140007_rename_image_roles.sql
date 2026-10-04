-- Tra ba vai tro anh ve ten cu. Sau khi chay, phai chay lai cac file ham
-- (202608240002, 202609140002, 202609140003, 202609140004, 202609140006)
-- BAN CU de than ham khop lai ten cot.

do $$
begin
  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'banner_url') then
    alter table public.metadata_games rename column banner_url to steam_library_image_url;
  end if;

  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'backdrop_url') then
    alter table public.metadata_games rename column backdrop_url to gallery_image_url;
  end if;

  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'poster_url') then
    alter table public.metadata_games rename column poster_url to cover_image_url;
  end if;

  if to_regclass('public.metadata_publish_rules') is not null then
    if exists (select 1 from information_schema.columns
               where table_schema = 'public' and table_name = 'metadata_publish_rules'
                 and column_name = 'require_banner') then
      alter table public.metadata_publish_rules rename column require_banner to require_card_cover;
    end if;
    if exists (select 1 from information_schema.columns
               where table_schema = 'public' and table_name = 'metadata_publish_rules'
                 and column_name = 'require_backdrop') then
      alter table public.metadata_publish_rules rename column require_backdrop to require_gallery;
    end if;
    if exists (select 1 from information_schema.columns
               where table_schema = 'public' and table_name = 'metadata_publish_rules'
                 and column_name = 'require_poster') then
      alter table public.metadata_publish_rules rename column require_poster to require_portrait;
    end if;
  end if;
end
$$;
