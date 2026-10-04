# QuestLog — Supabase migrations

Supabase giữ **metadata dùng chung** và **hồ sơ tài khoản**. Userdata, launcher, folder,
playtime và secret thiết bị nằm ở SQLite local, **không** đồng bộ lên đây.

## Cách áp dụng

Repo có `supabase/config.toml` cho Edge Function. Các migration cũ vẫn có thể áp dụng
bằng **SQL Editor**; crawler mới có script deploy riêng và không tự phát lại toàn bộ lịch sử.

1. Mở project → **SQL Editor** → **New query**.
2. Dán **toàn bộ** nội dung file migration.
3. **Run**.

Mỗi file viết theo kiểu idempotent (`if not exists`, `create or replace`, `drop ... if exists`)
nên chạy lại một file đã chạy là an toàn.

## Thứ tự — chạy đúng thứ tự này

| # | File | Nội dung | Yêu cầu thêm sau khi chạy |
|---|---|---|---|
| 1 | `202608240001_account_foundation.sql` | `profiles`, role `admin`/`user`, audit tài khoản, RLS, trigger Auth→profile, Custom Access Token Hook | Bật `public.custom_access_token_hook` ở **Authentication → Hooks → Custom Access Token** |
| 2 | `202608240002_metadata_catalog.sql` | `metadata_games` / `series` / `genres` / `themes` + bảng nối, GIN index, `search_metadata_games`, RLS chỉ-đọc | — |
| 3 | `202609140001_metadata_admin.sql` | `metadata_audit_events`, 4 hàm CRUD/merge cho `metadata_series`, `updated_at` cho genres/themes | — |
| 4 | `202609140002_metadata_admin_games.sql` | `metadata_game_payload_error` + 3 hàm create/update/delete cho `metadata_games` | — |
| 5 | `202609140003_metadata_admin_stats.sql` | `admin_metadata_stats()` — số liệu dashboard admin, tính trong **một** truy vấn | — |
| 6 | `202609140004_metadata_publish_library.sql` | `admin_publish_library()` — đẩy cả lô thư viện lên kho trong **một** transaction | — |
| 7 | `202609140005_metadata_admin_list.sql` | `admin_list_metadata_games()` — lọc + sắp xếp + keyset đa khoá, kèm index | — |
| 8 | `202609140006_metadata_publishing.sql` | Tiêu chí xuất bản: bảng tiêu chí, `manually_unpublished` / `is_published` / `gate_failures`, trigger, **siết RLS**, làm mới `search_metadata_games` và hàm liệt kê. Có khối đổi tên **có bảo vệ** nên chạy được dù bản trước đã chạy hay chưa | — |
| 9 | `202609140007_rename_image_roles.sql` | Đổi tên ba vai trò ảnh: `banner_url` / `backdrop_url` / `poster_url` (+ 3 cột tiêu chí). **Sau khi chạy phải chạy lại 2, 4, 5, 6, 8** — `rename column` không sửa thân function | Chạy lại các file hàm |
| 10 | `20261001175945_igdb_catalog_crawler.sql` | State/queue/source baseline, lease và RPC nội bộ cho IGDB crawler | Deploy Edge Function rồi dry-run trước khi bật |
| 11 | `202610030001_admin_integrations.sql` | Vault-backed credentials, audit và RPC nội bộ cho RAWG/IGDB/Steam/Cloudinary/Gemini | Áp dụng trước khi dùng Admin Console Integrations hoặc deploy crawler mới |
| 12 | `20261004120000_admin_list_page_offset.sql` | Thêm `p_offset` vào `admin_list_metadata_games` — phân trang số trang cho chế độ danh sách và Preview mode của Admin Console. Chạy **sau** 8; chạy lại 8 thì phải chạy lại file này | — |
| 13 | `20261004150000_google_signup_onboarding.sql` | Đăng ký bằng Google: `profiles.username` nullable, thêm `onboarding_state` / `signup_method` / `has_password`, trigger Auth→profile tạo profile `username_required` cho Google. Nên chạy trước bản Host mới. Thiếu cột thì Host tự lùi về đọc profile kiểu cũ: đăng nhập mật khẩu vẫn chạy nhưng Google và chọn username chưa dùng được | Bật Google provider theo `docs/AUTH_SETUP.md` §2 |

## IGDB crawler

Thiết kế: `docs/superpowers/specs/2026-10-02-igdb-crawler-design.md`.

Worker mặc định **tắt**. Mỗi lượt có ngân sách tối đa 100 game mới và 10 game
đang có cần hòa giải; cập nhật tồn đọng lâu và game sắp phát hành được ưu tiên.
Lịch cloud chạy mỗi 6 giờ. Nếu quota/DB/provider lỗi, checkpoint không tiến và
queue được giữ lại để lần sau xử lý.

Yêu cầu PowerShell 7 và biến môi trường cấp user/process:

```powershell
$env:SUPABASE_ACCESS_TOKEN = 'sbp_...'
```

Token Supabase là Personal Access Token có quyền database, Edge Functions và
secrets; không dùng `service_role` để deploy. Không ghi token/secret vào repo.

Credential IGDB không còn được truyền vào script deploy hoặc lưu thành Edge Function
secret. Admin lưu Client ID/Client Secret qua **Admin Console → Integrations**; crawler
đọc revision đang active qua RPC nội bộ khi chạy. Áp dụng migration
`202610030001_admin_integrations.sql` trước khi deploy crawler.

```powershell
# Chỉ kiểm tra project và dung lượng; không thay đổi cloud
pwsh ./supabase/crawler/deploy.ps1 -ProjectRef '<20-char-ref>' -PreflightOnly

# Migration mới + secrets + function + dry-run; vẫn để worker tắt
pwsh ./supabase/crawler/deploy.ps1 -ProjectRef '<20-char-ref>'

# Deploy, chạy thật lần đầu, chỉ lên lịch khi lần đầu sạch lỗi
pwsh ./supabase/crawler/deploy.ps1 -ProjectRef '<20-char-ref>' -EnableCron
```

Vận hành bằng `supabase/crawler/status.sql`. Dừng khẩn cấp, không xóa dữ liệu,
bằng `supabase/crawler/pause.sql`. File rollback của crawler cũng chỉ dừng an toàn;
không tự hoàn nguyên metadata đã được audit.

Migration 3 phụ thuộc cả 1 (dùng `public.set_updated_at()`) lẫn 2 (sửa bảng metadata).
Migration 4 phụ thuộc 3 (ghi `metadata_audit_events`). Migration 6 phụ thuộc cả 3 lẫn 4
(dùng `metadata_game_payload_error`).

## Biết migration nào đã chạy

Chạy trước khi áp dụng bất kỳ file nào. Ô nào `NULL` nghĩa là migration tạo ra nó chưa chạy:

```sql
select to_regclass('public.profiles')              as m1_profiles,
       to_regclass('public.metadata_games')        as m2_games,
       to_regclass('public.metadata_genres')       as m2_genres,
       to_regclass('public.metadata_audit_events') as m3_audit;
```

Ba migration cuối tạo **hàm** chứ không tạo bảng, nên `to_regclass` không thấy. Kiểm riêng:

```sql
select routine_name
from information_schema.routines
where routine_schema = 'public'
  and routine_name in ('admin_create_metadata_game',   -- migration 4
                       'admin_metadata_stats',          -- migration 5
                       'admin_publish_library',         -- migration 6
                       'admin_list_metadata_games',     -- migration 7
                       'admin_set_publish_rules')       -- migration 8
order by routine_name;
```

Sau migration 9, kiểm tên cột đã đổi:

```sql
select column_name from information_schema.columns
where table_schema = 'public' and table_name = 'metadata_games'
  and column_name in ('banner_url', 'backdrop_url', 'poster_url')
order by routine_name;
```

⚠️ **Chạy đúng thứ tự.** Supabase SQL Editor chạy cả script trong **một transaction**: nếu một
câu lệnh giữa file lỗi vì thiếu bảng của migration trước, **toàn bộ file bị rollback** — kể cả
những bảng đã tạo thành công phía trên. Triệu chứng điển hình là lỗi
`relation "..." does not exist` trỏ vào chính bảng mà file đó vừa tạo.

Một cái bẫy khác của SQL Editor: nếu trong editor **đang có text được bôi đen**, nó chỉ chạy
phần bôi đen. Dán xong nhớ bấm ra chỗ trống để bỏ selection.

## Kiểm tra sau khi chạy migration 3

Chạy trong SQL Editor — đúng thì trả về **4 dòng hàm** và **1 dòng bảng**:

```sql
select routine_name
from information_schema.routines
where routine_schema = 'public'
  and routine_name like 'admin_%metadata_series'
order by routine_name;

select tablename, rowsecurity
from pg_tables
where schemaname = 'public' and tablename = 'metadata_audit_events';
```

`rowsecurity` phải là `true`. Bảng audit **cố tình không có policy nào** — chỉ service role
đọc được, và Host là nơi duy nhất giữ service role key.

## Rollback

`supabase/rollback/<cùng tên file>.sql`, chạy **ngược thứ tự** so với lúc áp dụng.

⚠️ Rollback migration 2 hoặc 3 sẽ **xoá dữ liệu metadata**. Thư viện local của user vẫn chạy
(đã có snapshot trong SQLite) nhưng `MetadataGameId` sẽ trỏ vào bản ghi không còn tồn tại.

## Quy tắc không được phá

- Service role key **chỉ** nằm ở Host. Không bao giờ đưa vào biến `VITE_*` hay file frontend.
- `anon` và `authenticated` **chỉ được đọc** bảng metadata. Mọi đường ghi đi qua Host sau khi
  Host đã xác thực role admin từ JWT.
- `metadata_genres` / `metadata_themes` thuộc tiến trình đồng bộ (API sync chưa chốt) —
  **không viết endpoint ghi thủ công** cho hai bảng này. Xem
  [specs/2026-09-14-admin-console-mode-design.md](../docs/superpowers/specs/2026-09-14-admin-console-mode-design.md) §4.4.
