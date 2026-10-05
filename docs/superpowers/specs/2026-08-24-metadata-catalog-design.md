# QuestLog — Metadata Catalog & Library Import

## 1. Phạm vi

Mục tiêu là tạo kho metadata dùng chung và cho phép admin, user và guest thêm game vào thư viện local theo đúng quyền.

- Admin, user và guest: Manual hoặc From metadata *(trước 2026-10-05 Manual chỉ dành cho admin)*.
- Metadata dùng chung: Supabase.
- Userdata và dữ liệu thiết bị: SQLite local.
- Không gọi IGDB trực tiếp từ luồng tìm kiếm.
- Crawler, đồng bộ IGDB và Admin Console là phạm vi sau.

## 2. Kiến trúc đã chốt

Supabase là nguồn chuẩn metadata. Khi import, Host lấy metadata từ Supabase và tạo snapshot local trong `Games`; nhờ đó thư viện vẫn chạy offline.

Không có ghi hai chiều: người dùng không sửa metadata dùng chung, còn userdata/launcher/folder/playtime/progress/ownership/finish không ghi lên Supabase.

SQLite dùng chung trên một thiết bị nên game phải có owner scope do server suy ra:

- User: `user:<supabase-user-id>`.
- Guest: `guest:local`.

Tên field là `LibraryOwnerKey`, không nhận từ payload. Game metadata được định danh trong một thư viện bằng `(LibraryOwnerKey, MetadataGameId)`. Game manual có `MetadataGameId = null`.

## 3. Supabase metadata schema

### 3.1. Bảng chính

`metadata_games` gồm:

- `id` bigint identity primary key.
- `igdb_id` bigint nullable, unique khi có; dành cho crawler tương lai.
- `name` bắt buộc; `developer`, `publisher`, `summary` nullable.
- `publish_date`, `is_dlc`, `base_metadata_game_id`.
- `cover_url`, `gallery_url`, `banner_url`.
- `meta_score`, `avg_playtime_hours`, `series_id`.
- `revision`, `created_at`, `updated_at`.
- `search_vector` sinh từ name, developer, publisher và series.

Bảng liên quan: `metadata_series`, `metadata_genres`, `metadata_themes`, `metadata_game_genres`, `metadata_game_themes`.

### 3.2. Bảo mật và index

- Bật RLS cho tất cả bảng metadata.
- Không cấp quyền ghi cho `anon` hoặc `authenticated`.
- Host/Admin Console/crawler sau này ghi qua backend service role.
- GIN index trên `search_vector`.
- Index cho mọi foreign key, `igdb_id`, `updated_at` và `publish_date`.
- Search dùng cursor pagination, không dùng offset.

## 4. SQLite local schema

Giữ metadata và userdata trong `Games` hiện tại để giảm phạm vi migration. Bổ sung:

- `LibraryOwnerKey TEXT NOT NULL`.
- `MetadataGameId BIGINT NULL`.
- `MetadataRevision BIGINT NULL`.
- `MetadataSyncedAt TEXT NULL`.

Tạo unique index `(LibraryOwnerKey, MetadataGameId)` khi `MetadataGameId` khác null, cùng index `(LibraryOwnerKey, IsDisabled)` và index `MetadataGameId`.

Game lấy từ metadata có metadata fields do backend điền và chỉ đọc trong UI; userdata/device data vẫn sửa được.

## 5. API contract

### 5.1. Search

`GET /api/metadata/games?q=<query>&cursor=<cursor>&pageSize=<size>`

- User/admin dùng được; guest dùng được qua desktop guest token.
- Query tối thiểu 2 ký tự, `pageSize` bị giới hạn server.
- Kết quả chỉ là metadata summary, không có userdata.
- Mỗi item có id, tên, developer/publisher, ngày, ảnh, DLC, series, điểm/thời lượng nếu có, revision và trạng thái owner hiện tại: `notInLibrary`, `active`, `disabled`.

### 5.2. Batch import

`POST /api/library/game-imports`

Request chỉ nhận `metadataGameId` và userdata/device data; không nhận name, ảnh hoặc publisher để ghi đè metadata.

Backend lấy owner từ JWT/guest session, batch-load metadata, validate toàn bộ request, rồi mở transaction SQLite. Nếu có ID không tồn tại hoặc userdata sai, trả `422` và không ghi item nào. Game trùng trả `alreadyInLibrary`, game disabled trả `restoreRequired`; các item mới được commit cùng một lần.

Response luôn có cùng shape, gồm trạng thái từng item và tổng số `created`, `alreadyInLibrary`, `restoreRequired`.

### 5.3. Userdata và manual

`PATCH /api/games/{id}/userdata` chỉ cập nhật userdata/device data và kiểm tra owner. Metadata-derived game không đổi metadata qua endpoint này.

`POST /api/games` giữ flow manual hiện tại, mở cho mọi owner (trước 2026-10-05 bắt buộc admin); game manual có `MetadataGameId = null`, scope theo owner, `BaseGameId` phải cùng owner.

### 5.4. Errors

Dùng một shape `{ error: { code, message, details? } }`:

- `401 AUTH_REQUIRED`.
- `403 FORBIDDEN`.
- `422 VALIDATION_ERROR`.
- `404 METADATA_NOT_FOUND`.
- `409 LIBRARY_CONFLICT`.
- `503 METADATA_UNAVAILABLE`.

## 6. UI flow

- Admin mở method chooser; user/guest vào thẳng metadata picker.
- Search mở popup; kết quả không chiếm vùng danh sách chính.
- Có thể tìm nhiều lần và giữ selection khi popup đóng.
- Card kết quả hiển thị ảnh, tên, developer/publisher, ngày và trạng thái thư viện.
- Card đã có bị khóa; card disabled báo cần restore.
- Cột trái hiển thị game đã chọn dạng gallery card với ảnh, DLC/Upcoming, tên, developer/publisher, progress, ownership, score, playtime và ngày phát hành.
- Cột phải chỉnh userdata/device data của card đang chọn; metadata chỉ đọc.
- Add selected gửi một batch request.
- Đóng khi dirty phải hỏi xác nhận.
- Idle, loading, empty, error, partial result và offline đều có trạng thái hiển thị rõ; lỗi search không làm mất selection.

Guest dùng cùng UI metadata như user. Host proxy metadata bằng guest session, không đưa secret Supabase xuống frontend. Offline guest vẫn xem thư viện local nhưng không search/import thêm.

## 7. Migration hiện tại

Các game đang có trong SQLite là thư viện cá nhân hiện tại và được gán cho admin `VidagrrAdmin`:

```text
LibraryOwnerKey = user:<id profile VidagrrAdmin>
```

Migration lấy user id từ profile bootstrap, không hardcode UUID, chạy idempotent sau khi profile tồn tại. Nếu profile chưa sẵn sàng, Host báo migration pending thay vì gán owner sai.

Game cũ không có `MetadataGameId`, nên được giữ là manual và bảo toàn toàn bộ metadata/userdata hiện tại.

## 8. Phân kỳ triển khai

1. Contract và database foundation: OpenAPI, Supabase schema/RLS/index, SQLite columns/owner migration, regenerate types.
2. Backend search/import: repository Supabase, cursor search, batch transaction, owner/role checks, structured errors.
3. Frontend wiring: thay nguồn tạm bằng metadata API, bật Add selected, userdata PATCH, result summary, refresh library.
4. Verification: chạy migration trên bản sao, kiểm tra admin thấy thư viện cũ, owner isolation, offline library, typecheck/build/backend tests/security checks.

## 9. Ngoài phạm vi

- Crawler IGDB và lịch cập nhật.
- Admin Console tạo/sửa metadata.
- Live fallback khi không tìm thấy.
- Đồng bộ userdata lên cloud.
- Chuyển game guest sang tài khoản.
- Lịch sử version đầy đủ của metadata.
