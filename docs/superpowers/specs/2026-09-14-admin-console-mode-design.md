# QuestLog — Admin Console Mode & Tag Origin

**Date:** 2026-09-14

**Status:** Design đã chốt, chờ implementation plan

**Scope:** `MasterPlay.Host` (auth guard + endpoint admin), `MasterPlay.Infrastructure` (schema SQLite + client Supabase metadata), `MasterPlay.Web` (workspace mode + Admin Console surface), Supabase migration

**Tiếp nối:** [2026-08-24-metadata-catalog-design.md](2026-08-24-metadata-catalog-design.md) — bản đó ghi *"Crawler, đồng bộ IGDB và Admin Console là phạm vi sau"*. Đây là phần "sau" đó.

---

## 1. Mục tiêu

Tài khoản admin có **hai chế độ làm việc tách bạch**:

| Mode | Làm gì | Dữ liệu chạm tới |
|---|---|---|
| `library` (mặc định) | Quản lý thư viện của chính mình, **giống hệt một user thường** | SQLite local, scope `LibraryOwnerKey` của admin |
| `adminConsole` | Biên tập kho metadata dùng chung | Supabase `metadata_*` |

Đồng thời chốt **nguồn gốc (origin) của genre/theme**: tag từ metadata và tag thủ công **cộng dồn** trên cùng một game, với quyền tạo/sửa khác nhau theo role.

Feature này **không** đổi đơn vị đo, không thêm logic filter/sort ở client, không cho user ghi vào metadata dùng chung.

---

## 2. Hai khái niệm độc lập — không được trộn

1. **Role** — `admin | user`, đọc từ claim `user_role` trong JWT (đã có ở `202608240001_account_foundation.sql`). Bất biến trong phiên. Guest **không bao giờ** là admin.
2. **Workspace mode** — `library | adminConsole`, là **state UI của phiên**, không persist.

> **Mode là rào chắn UX, không phải rào chắn bảo mật.** Một access token của admin vẫn đủ quyền gọi endpoint admin kể cả khi UI đang ở `library`. Vì vậy **mọi endpoint admin phải tự kiểm tra role từ JWT**, không được tin `mode` hay bất kỳ field nào trong payload.

### 2.1. Quy tắc vào/ra mode

- Chỉ hiện mục **Admin console** trong menu **More** khi `account.role === "admin"` và `authStatus === "signedIn"`. Với user và guest, mục này **không render** (không phải disable).
- Vào mode: reset về section `Games` của console. Thoát mode: quay lại **đúng tab thư viện** đang mở trước đó.
- Không ghi `localStorage`, không ghi `/api/settings`. Reload trang hoặc đăng nhập lại → về `library`. Lý do: AGENTS.md §2 cấm localStorage cho settings, và mode "dính" qua reload là cách dễ nhất để admin vô tình sửa metadata.
- **Loại trừ với Console (controller) mode:** vào `adminConsole` thì thoát console session và trả lại window state trước đó; trong `adminConsole`, `F10` và chord `LB + RB + A` **không hoạt động**. Hai fullscreen surface không được chồng nhau.
- Account bị khoá → request kế tiếp trả `403`, client rơi về màn hình đăng nhập bất kể đang ở mode nào.

---

## 3. Signature hình ảnh của Admin Console

Yêu cầu: admin **không thể nhầm** mình đang ở đâu, kể cả khi đã cuộn sâu.

| Tín hiệu | Chi tiết |
|---|---|
| Accent xanh dương | Bọc app trong `[data-workspace="admin"]`, **remap token** `--primary`, `--primary-fg`, `--ring` sang thang xanh dương. Mọi `IconButton`/pill/focus ring kế thừa — **không sửa từng component** |
| Band trên cùng | Thanh mảnh cố định phía trên header: icon shield + chữ `ADMIN CONSOLE` + username + nút **Exit admin console** |
| Viền khung | `ring-1` màu accent admin quanh khung app, luôn thấy khi cuộn |
| Tiêu đề cửa sổ | Shell đổi title thành `QuestLog — Admin Console` |

Ràng buộc:

- Token admin phải định nghĩa đủ cho **cả 4 theme mode** (`light`, `dark`, `system`, `jungle` — DOMAIN_RULES §15). Không hardcode hex trong component.
- **Màu không được là tín hiệu duy nhất.** Chữ `ADMIN CONSOLE` trong band là phần bắt buộc, phục vụ cả người mù màu lẫn screenshot đen trắng.
- Chuỗi lấy từ i18n (`admin.console.title`, `admin.console.exit`, …), không hardcode.

---

## 4. Bề mặt Admin Console

Console **thay toàn bộ** vùng thư viện. Grid/table/queue/dashboard của thư viện bị ẩn cho tới khi thoát mode.

### 4.1. Section

| Section | Nội dung |
|---|---|
| **Games** | Tìm kiếm, tạo, sửa bản ghi `metadata_games`: name, developer, publisher, summary, publish date, `is_dlc` + base game, 3 ảnh, `meta_score`, `avg_playtime_hours`, series, genre, theme |
| **Series** | `metadata_series`: tạo, đổi tên, gộp, xoá |
| **Dashboard** | Sức khoẻ **kho metadata dùng chung** — xem §12.8 |

**Không có section Genres / Themes.** Xem §4.4.

Ngoài phạm vi bản này: quản lý account, crawler IGDB, audit viewer.

### 4.2. Tái dụng component

Bảng metadata là **component riêng** (`AdminMetadataTable`), không tái dụng `GameTable`: `GameTable` render progress/ownership/score/playtime — toàn field userdata không tồn tại trên bản ghi metadata. Tái dụng ở tầng primitive: `ui.tsx`, `IconButton`, `Dialog`, `TaxonomyPicker`, `editorInputClass`.

### 4.3. Gộp (merge) series

Kho dùng chung sẽ sinh trùng lặp. `POST /api/admin/metadata/series/merge { sourceId, targetId }`: chuyển mọi `metadata_games.series_id` sang target, xoá source, **trong một transaction**. Client không tự làm vòng lặp nhiều request.

### 4.4. Genre / Theme metadata là **sync-owned** — không sửa tay ở đâu cả

Chốt: **`metadata_genres` / `metadata_themes` và mọi liên kết `metadata_game_genres` / `metadata_game_themes` do một tiến trình đồng bộ ghi.** Không có UI biên tập cho chúng — không trong Admin Console, không ở đâu khác.

Hệ quả dây chuyền:

- Admin Console **không có** section Genres/Themes, **không có** endpoint CRUD cho hai bảng này.
- Editor metadata game **không có** picker genre/theme. Hai field đó hiện **read-only** kèm nhãn nguồn.
- **Không có merge cho genre/theme.** Trùng lặp là vấn đề của nguồn dữ liệu; gộp tay sẽ bị lần sync kế tiếp ghi đè. Việc khử trùng thuộc thiết kế sync.
- ⚠️ **API sync chưa chốt** (nguồn, lịch chạy, chiến lược đối soát, cách xử lý tag bị xoá ở nguồn). Cho tới lúc đó hai bảng **rỗng** và mọi tag hiển thị đều đến từ nhánh manual (§6.1).

Còn **taxonomy manual** — bộ `Genres` / `Themes` local, gồm cả bộ dùng chung có icon — ~~admin sửa ở giao diện thư viện qua `ManageTagsDialog`~~. **Thay thế bởi spec 2026-10-04-theme-groups (D2):** bộ chung (`system:shared`) admin sửa ở Admin Console › Taxonomy; `ManageTagsDialog` chỉ còn cho dữ liệu cá nhân. Phân quyền icon vẫn theo §5.1 và §7.

> Ghi chú phạm vi: anh chỉ nêu genre và theme. **Series vẫn ở lại console** vì `series_id` là field admin đặt trên bản ghi metadata, giống publisher. Nếu series cũng thuộc sync thì bỏ nốt section Series và endpoint của nó — chỉ là xoá bớt, không phá gì khác.

---

## 5. Origin của Genre / Theme — mô hình cộng dồn

### 5.1. Ba nhóm tag

| Origin | Bảng nguồn | Ai tạo / sửa | Icon | Ai nhìn thấy |
|---|---|---|---|---|
| `metadata` | `metadata_genres` / `metadata_themes` (Supabase), snapshot local | **Tiến trình sync — không ai sửa tay** (§4.4) | Luôn `null` | Mọi user có game đó trong thư viện |
| `manual` + shared | `Genres` / `Themes` local, `LibraryOwnerKey = 'system:shared'` — chính là **bộ custom hiện tại có `IconData`** | **Chỉ admin**, qua `ManageTagsDialog` ở **mode thư viện bình thường** | Được phép | Mọi owner trên thiết bị |
| `manual` + personal | `Genres` / `Themes` local, `LibraryOwnerKey = 'user:<id>'` / `'guest:local'` | Chủ sở hữu (user thường tạo được) | **Server ép `null`** khi ghi, bất kể payload | Chỉ chính owner đó |

Genre/theme hiển thị trên một game = **hợp của cả ba nhóm**. Không có chuyển đổi "một trong hai".

> Ghi chú đối chiếu yêu cầu: phát biểu ban đầu là *"2 mode để hiển thị theme/genre, chỉ admin mới được chọn manual"*. Sau khi chốt lại, mô hình là **cộng dồn**, và "chỉ admin mới được chọn manual" được hiện thực hoá thành **chỉ admin mới sở hữu bộ manual dùng chung (có icon)**; user vẫn thêm được tag manual nhưng là tag cá nhân và không icon.

### 5.2. Khử trùng lặp

Server khử trùng, **không phải client** (AGENTS.md §2).

- Khoá khử trùng: tên đã normalize (trim, casefold, gộp khoảng trắng).
- Thứ tự ưu tiên khi trùng: `manual personal` → `manual shared` → `metadata`. Bản thắng giữ `iconData` nếu có.
- Kết quả là một danh sách phẳng; UI chỉ render.

### 5.3. `TaxonomyItem` mở rộng

```ts
{
  id: number
  key: string                        // khoá đã normalize, ổn định giữa các origin
  name: string
  displayName: string                // localize theo DOMAIN_RULES §14
  origin: "metadata" | "manual"
  scope: "shared" | "personal"
  iconData: string | null            // null với mọi origin metadata và mọi scope personal
  sortOrder: number
  editable: boolean                  // server quyết định theo role + scope
}
```

`editable` do **server** tính. Client không được suy ra quyền từ `role` + `scope`.

### 5.4. Ảnh hưởng tới filter — ⚠️ breaking

Hiện `GET /api/games` nhận `genreIds` / `themeIds` là csv int (API_CONTRACT §5). Hai không gian ID (`metadata_genres.id` bigint và `Genres.Id` int) **va nhau**.

Chốt: filter chuyển sang `genreKeys` / `themeKeys`, dùng `TaxonomyItem.key`. Nhất quán với khoá khử trùng ở §5.2 — lọc `rpg` bắt cả tag metadata lẫn tag cá nhân cùng tên, đúng kỳ vọng người dùng. Quan hệ OR trong nhóm giữ nguyên (DOMAIN_RULES §5.7).

Việc này cần cập nhật `openapi.yaml`, regenerate `src/lib/api/**`, và sửa DOMAIN_RULES §5.7 + API_CONTRACT §5.

---

## 6. Thay đổi schema

### 6.1. SQLite

```
Genres:  + LibraryOwnerKey TEXT NOT NULL DEFAULT 'system:shared'
         + NameKey TEXT NOT NULL                      -- tên đã normalize
         UNIQUE (LibraryOwnerKey, NameKey)
Themes:  y hệt

MetadataGenres(Id BIGINT PK, Name TEXT NOT NULL, NameKey TEXT NOT NULL)   -- snapshot
MetadataThemes(...)                                                       -- snapshot
GameMetadataGenres(GameId TEXT, MetadataGenreId BIGINT, PK(GameId, MetadataGenreId))
GameMetadataThemes(...)
```

- Hàng seed sẵn trong `QuestLog.Template.db` (bộ custom có icon) nhận `LibraryOwnerKey = 'system:shared'` — migration giữ nguyên icon hiện có.
- Snapshot metadata ghi lúc import và refresh khi `MetadataRevision` của game đổi → **thư viện vẫn hiển thị đủ tag khi offline**.
- **Genre/theme metadata đến từ sync, và API sync chưa chốt** (§4.4). Vì vậy ở bản này:
  - Bảng `metadata_genres` / `metadata_themes` và toàn bộ đường ống snapshot **vẫn xây đủ**, nhưng **để trống dữ liệu**.
  - `POST /api/library/game-imports` copy liên kết genre/theme nếu có; hiện tại sẽ luôn ra tập rỗng. Đây là trạng thái **đúng như thiết kế**, không phải bug.
  - Hệ quả UI: cho tới khi có sync, mọi tag hiển thị đều đến từ nhánh `manual`. UI **không được** hiển thị empty state kiểu lỗi cho nhánh `metadata` rỗng.
  - Không viết endpoint ghi nào cho hai bảng này. Khi chốt sync, tiến trình đó ghi bằng service role như mọi đường ghi metadata khác.

### 6.2. Supabase

Bảng metadata đã đủ. Bổ sung:

- `updated_at` + trigger cho `metadata_genres`, `metadata_themes` (hiện chỉ `metadata_games`/`metadata_series` có).
- `metadata_audit_events(id, actor_user_id, entity, entity_id, action, before, after, created_at)` — mọi ghi từ Admin Console để lại một dòng. Kho dùng chung không có undo; audit là cách duy nhất truy vết.
- **Không đổi RLS.** `authenticated` vẫn chỉ đọc. Toàn bộ ghi đi qua Host bằng service role sau khi Host đã xác thực role admin.

---

## 7. API

Tất cả dưới `/api/admin/**`. Guard: `401 AUTH_REQUIRED` nếu chưa đăng nhập hoặc là guest; `403 ADMIN_REQUIRED` nếu `user_role !== "admin"`.

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/admin/metadata/games` | `q`, `cursor`, `pageSize` — cursor pagination, không offset |
| `POST` | `/api/admin/metadata/games` | |
| `GET` | `/api/admin/metadata/games/{id}` | |
| `PUT` | `/api/admin/metadata/games/{id}` | body **bắt buộc** có `revision` |
| `DELETE` | `/api/admin/metadata/games/{id}` | |
| `GET/POST` | `/api/admin/metadata/series` | |
| `PUT/DELETE` | `/api/admin/metadata/series/{id}` | |
| `POST` | `/api/admin/metadata/series/merge` | `{ sourceId, targetId }`, một transaction |
| `GET` | `/api/admin/metadata/stats` | Số liệu Admin dashboard (§12.8); backend tính hết |

Quy tắc:

- **Optimistic concurrency:** `PUT` gửi `revision` đang thấy; lệch → `409 METADATA_REVISION_CONFLICT` kèm bản ghi mới nhất. Server bump `revision`. Không có last-write-wins trên kho dùng chung.
- **Xoá metadata game** không phá thư viện đang có: snapshot local giữ nguyên, `MetadataGameId` giữ nguyên, lần sync sau đánh dấu `metadataMissing`. Không xoá dây chuyền sang SQLite của user.
- **Không có endpoint ghi nào cho `metadata_genres` / `metadata_themes`** — hai bảng đó thuộc sync (§4.4). Đừng thêm "cho tiện", vì có UI sửa tay là sẽ có người sửa, rồi lần sync sau ghi đè.
- `POST/PUT /api/taxonomy/{genres|themes}` (endpoint sẵn có, dùng cho tag manual ở **mode thư viện bình thường**): server gán `LibraryOwnerKey` từ JWT, **bỏ qua mọi `iconData` trong payload trừ khi** caller là admin **và** target là `system:shared`. Client không được gửi `libraryOwnerKey`.

---

## 8. Admin ở mode thường

Trong `library`, admin làm việc trên thư viện của chính mình như một user thường, với **đúng một đặc quyền được giữ lại**:

- **Manual add vẫn là đặc quyền của admin.** `AddGameMethodDialog` giữ nguyên điều kiện `account?.role === "admin"` tại `App.tsx:927`. Admin ở mode thường vẫn chọn được "Manual" hoặc "From metadata"; user và guest vẫn chỉ có "From metadata".
  - Game manual tạo ở mode thường là **game local của riêng admin** (`MetadataGameId = null`, scope `LibraryOwnerKey` của admin) — **không** đẩy lên kho dùng chung. Muốn tạo bản ghi trong kho dùng chung thì vào section **Games** của Admin Console. Hai hành động này khác đích đến và không được gộp.
- Tag manual cá nhân của admin nằm ở scope `user:<id>` như mọi người; muốn sửa bộ dùng chung có icon thì vào console.
- Mọi thứ còn lại (queue, finish, diary, playtime, launch, dashboard, filter, sort) không phân biệt role.

---

## 9. Rủi ro và biện pháp

| Rủi ro | Biện pháp |
|---|---|
| Admin sửa nhầm metadata khi tưởng đang ở thư viện | Console là surface riêng + band + accent xanh; mode không persist qua reload |
| Hai admin ghi đè nhau | `revision` + `409` |
| Client tự suy quyền sửa tag | `editable` do server trả; endpoint tự kiểm role |
| User tự nâng quyền qua payload | `LibraryOwnerKey`, `iconData`, `role` đều bị server ghi đè/bỏ qua |
| ID trùng giữa hai không gian tag | Chuyển filter sang `key` (§5.4) |
| Mất tag khi offline | Snapshot metadata xuống SQLite lúc import |
| Xoá metadata làm hỏng thư viện user | Không cascade; đánh dấu `metadataMissing` |

---

## 10. Thứ tự triển khai

1. **Mode shell** — claim role về client, mục More, workspace state, token xanh + band, console rỗng, loại trừ với controller console.
2. **Series metadata** — CRUD + merge cho series, audit events.
3. **Games metadata** — CRUD + revision conflict + 3 ảnh + base game.
4. **Admin dashboard** — `GET /api/admin/metadata/stats`, tách component chart dùng chung, tab `overview` + `health` có điều hướng.
5. **Origin cộng dồn** — schema SQLite, snapshot khi import, `TaxonomyItem` mở rộng, đổi filter sang `key`, phân quyền icon.

Mỗi giai đoạn tự đứng được và merge riêng.

---

## 11. Tài liệu phải cập nhật cùng

- `docs/DOMAIN_RULES.md`: §19 (thêm quy tắc workspace mode và origin tag), §5.7 (filter theo `key`), §14 (localize `displayName` cho tag metadata).
- `docs/API_CONTRACT.md`: §2.2 (`TaxonomyItem` mới), §5 (`genreKeys`/`themeKeys`), §6 (quy tắc ghi taxonomy), mục mới cho `/api/admin/**`.
- `docs/PARITY_CHECKLIST.md`: **không thêm hàng.** File đó là cổng parity WPF → Web; Admin Console không có đối ứng bên WPF nên thêm vào sẽ làm sai nghĩa của cổng. Riêng hàng `A1` (filter Genre/Theme) sẽ đổi nghĩa khi làm giai đoạn 5 — cập nhật lúc đó.
- `openapi.yaml` → regenerate `MasterPlay.Web/src/lib/api/**`.

---

## 12. Phụ lục A — Đối chiếu từng tính năng theo mode

Nguyên tắc chung quyết định mọi dòng dưới đây:

> **Admin Console thao tác trên bản ghi metadata dùng chung. Bản ghi đó không có userdata và không có device data.** Mọi tính năng dựng trên score / excited / progress / ownership / playtime / finish / diary / launcher / folder / queue **không tồn tại** trong console — không phải bị disable, mà là không có đối tượng để áp vào.

### 12.1. Thanh điều hướng và toolbar

| Tính năng | `library` | `adminConsole` |
|---|---|---|
| Tab | `all` / `planning` / `finished` / `queue` | Thay bằng section `Games` / `Series` / `Dashboard` |
| Search box | Giữ nguyên | Giữ, nhưng bắn vào full-text search của `metadata_games` (`search_vector`), **tối thiểu 2 ký tự** |
| Search scope | `all`, `name`, `series`, `publisher`, `developer`, `releaseYear` | Còn `all`, `name`, `series`, `publisher`, `developer`, `releaseYear` — tất cả đều là field metadata nên giữ nguyên được |
| Sort | 12 khoá (DOMAIN_RULES §7.1) | 6 khoá thuần metadata: `id` (mới thêm, mặc định), `name`, `publishDate`, `metaScore`, `avgPlaytime`, `updatedAt`. **Bỏ** `default`, `status`, `playTime`, `finishTime`, `excited`, `score`, `ownership`, `progress`. `NULL` luôn xuống cuối ở cả hai chiều |
| Reverse sort | Giữ | Giữ |
| Filter | `FilterDialog` đầy đủ (progress, ownership, score, excited, playtime, finish year…) | **Không dùng `FilterDialog`.** Popover tối giản: loại bản ghi (`is_dlc`), series (kèm *không thuộc series nào*), năm phát hành, và **dữ liệu còn thiếu** — dùng chung 11 khoá với các dòng health của dashboard. **Chưa có lọc genre/theme:** hai bảng đó thuộc sync và hiện đang rỗng (§4.4); một ô chọn không bao giờ có lựa chọn là hứa hão. Thêm khi sync chạy |
| View sort profile | `/api/library/sort-profiles` theo view | **Không áp dụng.** Sort và filter trong console đều là state phiên, không lưu |
| Layout card / list | Toggle card ↔ list | Toggle card ↔ list, **mặc định card**. Dùng lại đúng nút của thư viện; state phiên, không persist. List có cột ảnh 88×48 như list thư viện |
| Phân trang | Trang số, `PAGE_SIZE = 30` | **Keyset pagination** → nút "Load more", **không có số trang**. Đây là khác biệt hành vi thật, không phải lựa chọn thẩm mỹ. Con trỏ là **chuỗi thô**: sắp xếp đa khoá biến nó thành cặp *(giá trị khoá sắp xếp, id)*, client không đọc |
| Nút `+` | Thêm game vào thư viện | **Tạo bản ghi mới trong kho metadata dùng chung** |
| Badge số lượng | Số game trong thư viện | Số bản ghi metadata khớp truy vấn |

### 12.2. Menu More

| Mục | `library` | `adminConsole` |
|---|---|---|
| Account | Có | Có — không đổi |
| Settings | 4 section: `general`, `storage`, `libraries`, `integrations` | Chỉ `general` (ngôn ngữ, theme). `storage` / `libraries` / `integrations` là cấu hình thiết bị cho thư viện local → **ẩn** |
| Dashboard | Có — thống kê thư viện cá nhân | **Thay bằng Admin dashboard** (§12.8), là một section của console chứ không phải mục menu |
| Manage tags | Có (series/genres/themes/progress/ownership local) — **đây là nơi admin sửa taxonomy manual, kể cả bộ dùng chung có icon** (§4.4) | **Ẩn** — taxonomy manual thuộc thư viện, không thuộc metadata |
| Admin console | Có (chỉ admin) | Đổi thành **Exit admin console** |

### 12.3. Bản ghi game: thư viện vs. metadata

| Nhóm field | `library` (`GameDetailDialog`) | `adminConsole` (editor metadata) |
|---|---|---|
| Tên, developer, publisher, summary, publish date | Read-only nếu game đến từ metadata | **Sửa được** |
| `is_dlc` + base game | Read-only nếu đến từ metadata | **Sửa được** |
| `meta_score`, `avg_playtime_hours` | Read-only | **Sửa được** |
| Series | Read-only nếu đến từ metadata | **Sửa được**, chọn từ `metadata_series` |
| Genre / Theme | Cộng dồn 3 nhóm (§5.1); chỉ sửa được nhánh manual | **Read-only** — thuộc sync (§4.4), không có picker |
| 3 ảnh (card cover / portrait / gallery) | Read-only nếu đến từ metadata | **Sửa được** — 3 URL metadata, dùng lại luồng upload Cloudinary |
| `score`, `excited` | Sửa được | **Không tồn tại** |
| `progress`, `ownership` | Sửa được | **Không tồn tại** |
| Playtime, `lastPlayedAt` | Do Tray ghi | **Không tồn tại** |
| Finishes, diary | Sửa được | **Không tồn tại** |
| `launchTarget`, `exeName`, save/backup folder | Sửa được | **Không tồn tại** |
| Ownership info (Steam/Ubisoft credential) | Sửa được, vault thiết bị | **Không tồn tại** — và không bao giờ được lên cloud |
| `ready`, `isRunning`, `canLaunch`, `folderWarning` | Runtime, backend ghi | **Không tồn tại** |
| External links (HowLongToBeat, Metacritic) | Read-only, backend dựng | Giữ read-only để tra cứu khi biên tập |
| `revision` | Không hiện | **Hiện**, và gửi kèm mọi `PUT` (§7) |

### 12.4. Hành động bị vô hiệu trong Admin Console

Không render, không disable:

- Launch game, tracking playtime, Finish, Disable game.
- Generate backup folder, Reveal in Explorer, chọn exe/shortcut.
- Queue: kéo-thả, Promote to Next Play, chuẩn hoá thứ tự.
- Tray panel.
- Reset library (`F6`).
- Console (controller) mode (`F10`, chord `LB + RB + A`) — §2.1.

### 12.5. Phím tắt

| Phím | `library` | `adminConsole` |
|---|---|---|
| `Ctrl+1..4` | Chuyển tab thư viện | Chuyển section console |
| `Ctrl+K` | Focus search | Giữ nguyên |
| `` ` `` | Add game | Tạo bản ghi metadata |
| `Esc` | Đóng surface trên cùng | Giữ nguyên; ở gốc console **không** thoát mode (phải bấm Exit — tránh thoát nhầm) |
| `F5` | Reload | Giữ nguyên |
| `F6` | Reset library | **Vô hiệu** |
| `F10` | Console mode | **Vô hiệu** |
| `F11` | Fullscreen | Giữ nguyên |

### 12.6. Thông báo lỗi riêng của console

| Tình huống | Hành vi |
|---|---|
| `409 METADATA_REVISION_CONFLICT` | Giữ nguyên form, hiện diff và bản mới nhất, buộc admin chọn ghi đè hay bỏ |
| Mất mạng | Console **không có chế độ offline**. Kho metadata là nguồn từ xa → hiện trạng thái mất kết nối và khoá mọi nút ghi. Khác hẳn thư viện (vẫn chạy offline nhờ snapshot SQLite) |
| Xoá series đang được gắn | Chặn, gợi ý dùng **Merge** (§4.3) |
| Account bị khoá giữa phiên | `403` → đăng xuất, về màn hình đăng nhập |

### 12.7. Luồng Add — ba đích đến khác nhau

Sau thay đổi này, cùng một nút `+` dẫn tới **ba đích đến khác hẳn nhau**. Đây là rủi ro nhầm lẫn lớn nhất của feature; đích đến phải hiện rõ ở mọi bước.

| # | Hành động | Ai làm được | Ở đâu | Endpoint | Ghi vào |
|---|---|---|---|---|---|
| **A** | Add từ kho metadata | guest, user, admin | `library` | `POST /api/library/game-imports` | SQLite, scope owner. `MetadataGameId` có giá trị |
| **B** | Add thủ công | **chỉ admin** | `library` | `POST /api/games` | SQLite, scope owner. `MetadataGameId = null` |
| **C** | Tạo bản ghi metadata | **chỉ admin** | `adminConsole` | `POST /api/admin/metadata/games` | **Supabase `metadata_games`** — kho dùng chung |

> **B không phải là C.** Add thủ công tạo game **local riêng của admin**, không ai khác thấy, không lên cloud. Muốn thêm vào kho dùng chung thì phải vào console. Hai việc này trước nay chỉ có một cái nên copy hiện tại không phân biệt — **phải sửa**.

#### 12.7.1. Hành vi nút `+`

**Ở `library`** — giữ nguyên `App.tsx:268`, không đổi logic:

- guest / user → mở thẳng `MetadataGamePickerDialog` (luồng A).
- admin → `AddGameMethodDialog` với hai lựa chọn A và B.

**Ở `adminConsole`**:

- **Không** mở `AddGameMethodDialog`, **không** mở `MetadataGamePickerDialog`.
- Mở thẳng editor metadata trống (luồng C). Không có bước chọn phương thức vì chỉ có một đích đến.
- Ở section `Series`, `+` tạo một series metadata mới, không phải game. Ở `Dashboard`, `+` không hoạt động.

#### 12.7.2. Sửa copy để đích đến không mơ hồ — ⚠️ bắt buộc

Chuỗi i18n hiện tại nói *"Admin có thể thêm thủ công hoặc chọn từ kho metadata dùng chung"* (`addGame.methodDescription`). Câu này khiến người đọc tưởng **cả hai** lựa chọn đều dính tới kho dùng chung, trong khi thực tế **cả hai đều chỉ ghi vào thư viện cá nhân**. Phải viết lại:

| Key | Nội dung mới phải truyền đạt |
|---|---|
| `addGame.methodDescription` | Cả hai cách đều thêm vào **thư viện của bạn** |
| `addGame.metadataMethodDescription` | Chép từ kho dùng chung; metadata read-only, cập nhật theo kho |
| `addGame.manualMethodDescription` | Tạo game **chỉ mình bạn thấy**; không lên kho dùng chung |
| `admin.metadata.createDescription` (mới) | Tạo bản ghi trong **kho dùng chung**; **mọi user đều thấy** |

Dialog tạo bản ghi metadata trong console mang accent xanh và nhãn "Kho dùng chung" ngay cạnh tiêu đề.

#### 12.7.3. Sau khi tạo bản ghi metadata

Luồng C **không** tự thêm game vào thư viện của admin — hai việc tách bạch, không đoán ý.

Sau khi lưu thành công, toast kèm hành động **"Add to my library"**: gọi luồng A với `metadataGameId` vừa tạo. Admin chủ động bấm.

#### 12.7.4. Chống trùng lặp

| Luồng | Quy tắc |
|---|---|
| A | Đã có: trả `alreadyInLibrary`, game đã disable trả `restoreRequired` (metadata-catalog spec §5.2) |
| B | **Không** kiểm trùng với kho metadata. Game local riêng, trùng tên là hợp lệ |
| C | **Kiểm trùng trước khi lưu.** Search `metadata_games` theo tên đã normalize, hiện các bản ghi khớp gần, buộc admin xác nhận mới lưu. **Cảnh báo, không chặn cứng** — remake/reboot trùng tên là có thật |
| C | `igdb_id` đã `unique` ở DB → nhập trùng trả `409`, không để DB tự ném lỗi thô lên UI |

Lý do C phải chặt hơn: kho dùng chung **không có undo** (§6.2 chỉ có audit trail), và game metadata **không có Merge** — dọn trùng nghĩa là sửa/xoá tay từng bản ghi, trong khi mọi thư viện đã import bản trùng vẫn giữ `MetadataGameId` trỏ vào đó. Chặn ở cửa vào rẻ hơn nhiều.

#### 12.7.5. Tái dụng metadata preview

`POST /api/game-metadata/preview` (RAWG / HLTB / IGDB, **không ghi DB**) hiện chỉ phục vụ luồng B. Luồng C dùng lại nguyên endpoint đó để điền nhanh draft khi admin gõ tên game.

Không cần endpoint mới. Ranh giới giữ nguyên: preview chỉ điền form, chỉ khi admin bấm Save mới có request ghi.

#### 12.7.6. Bootstrapping — hệ quả của việc chưa chốt nguồn genre/theme

Genre/theme metadata thuộc sync (§4.4) nên luồng C **không có picker genre/theme** — không có gì phải bootstrap.

Trong editor metadata game, hai field đó hiển thị read-only kèm nhãn nguồn. Cho tới khi chốt sync, chúng luôn rỗng; hiện đúng một dòng trung tính kiểu "chưa đồng bộ", **không** phải cảnh báo lỗi và **không** phải nút "thêm tag".

Tuyệt đối không tự sinh tag metadata từ chuỗi admin gõ ở bất kỳ đâu — sẽ đẻ trùng lặp mà không có đường gộp.

#### 12.7.7. Phím tắt và controller

- `` ` `` : ở `library` mở luồng A/B như hiện tại; ở `adminConsole` mở luồng C.
- `R3` (controller) mở Add flow — không liên quan tới console vì `adminConsole` loại trừ controller console session (§2.1).

### 12.8. Admin dashboard

Dashboard của thư viện trả lời *"tôi đang chơi gì"*. Dashboard của console trả lời một câu khác hẳn: **"kho dùng chung đang thiếu gì và hỏng ở đâu"**. Không tái dụng số liệu, chỉ tái dụng component vẽ (`Panel`, `ColumnChart`, `Empty` trong `DashboardView.tsx` — tách ra `components/charts.tsx` dùng chung).

Giữ đúng hai tab như dashboard thư viện:

#### Tab `overview` — quy mô kho

| Panel | Nội dung |
|---|---|
| Tổng quan | Số bản ghi `metadata_games`, số genre, theme, series |
| Bản ghi theo năm phát hành | Cột đứng theo `publish_date` |
| Phân bố `meta_score` | Cột theo dải điểm |
| Phân bố `avg_playtime_hours` | Cột theo dải thời lượng |
| Top genre / theme | Cột ngang, đếm liên kết `metadata_game_genres` / `_themes`. Read-only, phản ánh kết quả sync |
| DLC vs. game gốc | Vành khuyên |
| Hoạt động biên tập | Số bản ghi sửa trong 7 / 30 ngày, lấy từ `updated_at` |

#### Tab `health` — mỗi dòng là một việc cần làm

Đây mới là phần có giá trị thật. Mỗi dòng **bấm được**, dẫn thẳng tới danh sách đã lọc trong section `Games`.

| Chỉ số | Vì sao quan trọng |
|---|---|
| Thiếu cover / portrait / gallery | Card và Console mode của user sẽ trống |
| Thiếu developer hoặc publisher | Rơi khỏi search scope `developer` / `publisher` |
| Thiếu `publish_date` | Sort `publish` và filter theo năm bỏ sót |
| Thiếu `meta_score` / `avg_playtime_hours` | Filter dải và sort tương ứng bỏ sót |
| Thiếu summary | Detail rỗng |
| **Không có genre hoặc theme** | Không lọc được. **Không hành động được từ console** — đây là chỉ số theo dõi tiến độ sync (§4.4). Cho tới khi chốt sync, chỉ số này bằng **100%**: kỳ vọng đúng, không phải báo động, và dòng này **không bấm được** |
| Trùng tên game nghi ngờ | Cùng `NameKey` sau normalize → ứng viên xử lý tay |
| DLC không có base game | `is_dlc = true` nhưng `base_metadata_game_id = null` |
| Series chỉ có 1 game | Series thừa, nên gộp hoặc xoá |
| Series không gắn game nào | Series mồ côi, nên xoá |
| Thiếu `igdb_id` | Không đối soát được với nguồn ngoài khi bật crawler sau này |

Genre/theme mồ côi và genre/theme trùng tên **không nằm trong bảng này**: chúng do sync sinh ra và console không có đường sửa. Khử trùng tag thuộc thiết kế sync.

#### Ràng buộc

- **Backend tính toàn bộ.** Endpoint mới `GET /api/admin/metadata/stats`, cùng guard `401`/`403` như §7. Cộng dồn ở client sẽ sai ngay khi kho vượt một trang — đúng lý do `/api/dashboard/stats` tồn tại.
- Mỗi dòng `health` trả kèm tham số lọc để điều hướng sang `Games`, **không** trả cả danh sách id.
- Dashboard là **read-only**. Không có nút sửa hàng loạt ở bản này.
- Kho rỗng → empty state trung tính, không phải cảnh báo lỗi.

---

### 12.9. Đẩy thư viện lên kho — `publish-library`

Chiều **ngược** với `POST /api/library/games/import`. Import kéo bản ghi từ kho về máy; cái này đẩy bản ghi từ máy lên kho **và** nối dòng thư viện vào bản ghi vừa tạo.

`POST /api/admin/metadata/publish-library`, body `{ dryRun }`.

#### Phạm vi

Chỉ lấy game của **chính người gọi**, `isDisabled = false`, và **chưa** có `metadataGameId`. Game đã ẩn thì không đẩy: người dùng đã bỏ nó khỏi thư viện, đưa lên kho chung là đi ngược ý định đó.

| Lên kho | Ở lại máy |
|---|---|
| tên, developer, publisher, ngày phát hành | điểm, tiến độ, sở hữu, `excited` |
| DLC + bản gốc, series | giờ chơi thực, lần chơi cuối, lần hoàn thành |
| ba ảnh, meta score, giờ chơi trung bình, `igdbId` | exe/save/backup, thông tin tài khoản |
| | **genre/theme cá nhân** (§4.4) |

`summary` luôn `null`: thư viện cục bộ không có trường tương ứng. Để trống chứ không suy diễn.

#### Ràng buộc

- **Cả lô chạy trong một hàm plpgsql** (`admin_publish_library`). Mỗi lần gọi RPC là một transaction riêng; 98 lần gọi mà đứt giữa chừng sẽ để kho có bản ghi trong khi thư viện chưa nối bản nào — hai bên lệch và không có đường lui.
- **`dryRun` mặc định `true`.** Kho dùng chung không có undo, nên phải xem được chính xác sẽ tạo gì trước khi tạo.
- **Trùng tên thì nối vào bản ghi sẵn có** (`status: "matched"`), không tạo bản sao. Nhờ vậy chạy lại là an toàn — cần thiết vì bước ghi SQLite nằm *sau* khi kho đã ghi xong.
- **`igdbId` trùng thì bỏ trống**, đếm vào `igdbIdDropped`. Bản ghi vẫn lên được, chỉ mất khoá đối chiếu — thà thế còn hơn hỏng cả lô.
- **DLC nối về bản gốc ở lượt riêng**: DLC có thể được duyệt trước bản gốc, nên chỉ lượt hai mới có đủ bản đồ `localId → id`.
- Ba vai trò ảnh đối ứng **một-một** với đường import (DOMAIN_RULES §12). Lệch một cấp là ảnh card sẽ hoá ảnh hero sau mỗi vòng import.

#### Giao diện

Nút nằm ở thanh công cụ của console, **ngay cạnh `+`**, chỉ hiện ở section `Games`. Cả hai đều là hành động **ghi** vào kho — một bản ghi và nhiều bản ghi. Không đặt ở Dashboard: dashboard là read-only (§12.8).

Hộp thoại chạy **hai bước**, không gộp:

1. Mở hộp thoại → chạy `dryRun`, hiện số game và số series sẽ tạo, kèm bảng *lên kho / ở lại máy*.
2. Bấm nút thứ hai mới ghi thật.

Một nút bấm thẳng ra gần trăm bản ghi trong kho không có undo là thiết kế sai. Bảng ranh giới không phải trang trí: admin đang đẩy dữ liệu cá nhân vào nơi mọi người đọc được, phải nhìn thấy ranh giới trước khi bấm.

Sau khi ghi, hộp thoại hiện báo cáo (tạo mới / series / đã nối / DLC đã nối), liệt kê bản ghi **được nối thay vì nhân bản**, và cảnh báo riêng nếu có bản ghi mất `igdbId`. Thư viện rỗng-để-đẩy thì hiện trạng thái trung tính và **vô hiệu** nút ghi.

---

## 13. Tiêu chí xuất bản

**Mặc định mọi bản ghi đều được xuất bản.** Hai cơ chế có thể chặn: một bảng **tiêu chí** bật/tắt được, áp cho toàn kho, và một công tắc **gỡ xuất bản thủ công** cho từng bản ghi.

### 13.1. Hai khái niệm độc lập — không được gộp

| Cột | Ai ghi | Nghĩa |
|---|---|---|
| `manually_unpublished` | admin | **ý định** — có chủ động gỡ xuất bản bản ghi này không |
| `is_published` | trigger DB | **kết quả suy ra** — người dùng có tìm thấy không |
| `gate_failures` | trigger DB | tiêu chí đang trượt, dạng mảng khoá |

`is_published = not manually_unpublished and cardinality(gate_failures) = 0`

**Vì sao không ghi thẳng "trượt tiêu chí" vào `manually_unpublished`:** làm vậy thì sau khi admin bổ sung ảnh bìa, bản ghi **vẫn không lên** — vì cờ đó do hệ thống bật chứ không phải admin bật, và không ai còn phân biệt được nữa. Trạng thái suy ra ghi đè lên cột ý định là một đường một chiều.

Yêu cầu "trượt tiêu chí thì tự động không xuất bản và không bật lại được" được đáp ứng ở **giao diện**: công tắc hiện ở trạng thái chưa xuất bản và **khoá lại**, kèm tên tiêu chí đang trượt. API vẫn nhận `published: true` — đó là ý định hợp lệ, chỉ là chưa có hiệu lực.

### 13.2. `is_published` là cột thật, không phải biểu thức tính lúc đọc

- RLS cần một vị từ rẻ trên từng dòng; gọi hàm đọc bảng cấu hình ở mỗi dòng sẽ làm mọi truy vấn của user chậm theo.
- Cột thật thì index được.

Đổi lại: mỗi lần đổi tiêu chí phải quét lại cả bảng. Quét một lần lúc đổi cấu hình rẻ hơn nhiều so với tính lại ở mọi lượt đọc.

### 13.3. Chặn ở cả hai đường đọc

- **RLS** `metadata_games`: `using (is_published)` — client gọi thẳng PostgREST cũng không thấy.
- **`search_metadata_games`** và `GET /rest/v1/metadata_games?id=in.(…)`: chạy bằng `service_role` nên **bypass RLS**, phải lọc `is_published` riêng.

Thiếu một trong hai thì đây chỉ là lời khuyên, không phải tiêu chí.

Import một bản ghi chưa xuất bản trả `422 METADATA_GAME_NOT_FOUND` — không tiết lộ rằng nó tồn tại.

### 13.4. Giao diện

Section thứ tư của console (`publishing`), đặt **trước** Dashboard: nó là màn hình ghi, dashboard chỉ đọc. Mỗi tiêu chí là một công tắc **đang bật / đang tắt**.

Cột **"sẽ chặn"** là phần quan trọng nhất: đếm số bản ghi trượt từng tiêu chí **kể cả khi tiêu chí đang tắt**. Bật một công tắc rồi mới phát hiện nó chặn mất 95 bản ghi là quá muộn — kho dùng chung không có undo.

Ở danh sách và thẻ Games: huy hiệu "chưa xuất bản" kèm lý do, và một nút xuất bản/gỡ nhanh (khoá lại khi trượt tiêu chí).

### 13.5. Các tiêu chí

Dùng chung 11 khoá với các dòng health của dashboard, nên nhãn không phải dịch lại lần hai: ba ảnh, developer, publisher, ngày phát hành, meta score, giờ chơi trung bình, tóm tắt, IGDB id, và "DLC phải có bản gốc".

Mặc định **không tiêu chí nào bật**, nên mọi bản ghi đều được xuất bản. Bật sẵn một tiêu chí sẽ làm biến mất một phần kho ngay khi migration chạy, mà không ai chủ động chọn điều đó.
