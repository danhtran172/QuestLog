-- Doi ten ba vai tro anh cho de phan biet — DOMAIN_RULES §12.
--
--   steam_library_image_url -> banner_url    Steam, NGANG, CO ten game in san
--   gallery_image_url       -> backdrop_url  RAWG,  NGANG, SACH (phu chu duoc)
--   cover_image_url         -> poster_url    IGDB,  DOC
--
-- Ten cu hong theo ba kieu khac nhau:
--   - chu "cover" xuat hien o ca ba cot phia SQLite, khong doc luot ra duoc
--   - `gallery_image_url` NOI DOI: anh hien o gallery la anh Steam, khong phai
--     cot nay
--   - `steam_library_image_url` dat ten theo NGUON, doi nha cung cap la sai ten
--
-- Ba cot tieu chi xuat ban doi theo cho khop.
--
-- === SAU KHI CHAY FILE NAY PHAI CHAY LAI CAC FILE HAM ===
--
-- `alter table ... rename column` KHONG sua than cua function: Postgres luu than
-- ham duoi dang van ban va chi phan giai ten luc CHAY. Moi ham dang tham chieu
-- ten cu se loi runtime cho toi khi duoc tao lai.
--
-- Chay lai theo dung thu tu:
--   202608240002_metadata_catalog.sql
--   202609140002_metadata_admin_games.sql
--   202609140003_metadata_admin_stats.sql
--   202609140004_metadata_publish_library.sql
--   202609140006_metadata_publishing.sql
--
-- File cuoi ket thuc bang `metadata_recompute_published()`, nen `gate_failures`
-- dang giu khoa cu ('cardCover' / 'gallery' / 'portrait') se duoc tinh lai thanh
-- khoa moi ('banner' / 'backdrop' / 'poster').

do $$
begin
  -- == metadata_games ==========================================
  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'steam_library_image_url') then
    alter table public.metadata_games rename column steam_library_image_url to banner_url;
  end if;

  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'gallery_image_url') then
    alter table public.metadata_games rename column gallery_image_url to backdrop_url;
  end if;

  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'cover_image_url') then
    alter table public.metadata_games rename column cover_image_url to poster_url;
  end if;

  -- == metadata_publish_rules ==================================
  -- Bang nay chi ton tai neu 202609140006 da chay.
  if to_regclass('public.metadata_publish_rules') is not null then
    if exists (select 1 from information_schema.columns
               where table_schema = 'public' and table_name = 'metadata_publish_rules'
                 and column_name = 'require_card_cover') then
      alter table public.metadata_publish_rules rename column require_card_cover to require_banner;
    end if;

    if exists (select 1 from information_schema.columns
               where table_schema = 'public' and table_name = 'metadata_publish_rules'
                 and column_name = 'require_gallery') then
      alter table public.metadata_publish_rules rename column require_gallery to require_backdrop;
    end if;

    if exists (select 1 from information_schema.columns
               where table_schema = 'public' and table_name = 'metadata_publish_rules'
                 and column_name = 'require_portrait') then
      alter table public.metadata_publish_rules rename column require_portrait to require_poster;
    end if;
  end if;
end
$$;

-- Index cua `search_vector` khong dinh toi ba cot nay nen khong phai dung lai.
-- Cac index sap xep (`metadata_games_*_id_idx`) cung vay.
