# Nhóm theme + taxonomy theo chủ sở hữu

**Ngày:** 2026-10-04
**Trạng thái:** Thiết kế đã duyệt; chờ kế hoạch triển khai

## 1. Mục tiêu

1. Gom theme thành **nhóm** (Thời đại / Bối cảnh / Phong cách thế giới / Nhân vật).
   Admin quản lý **nhóm chung** trong Admin Console; mỗi user tạo được **nhóm cá nhân**.
2. Kéo thả theme giữa các nhóm và sắp xếp trong nhóm.
3. Bảng chọn theme cho game chuyển từ popover phẳng sang **dialog lớn, lưới theo nhóm**.
4. Triển khai phần SQLite của mô hình sở hữu taxonomy ở spec
   [2026-09-14 §5–6](2026-09-14-admin-console-mode-design.md) (`system:shared` vs personal) — điều kiện
   tiên quyết của nhóm cá nhân, đồng thời **vá lỗ hổng hiện tại**: `POST/PUT/DELETE /api/taxonomy/*`
   không kiểm tra quyền, user thường hoặc guest sửa/xoá được bộ theme chung.

## 2. Phạm vi

**Trong phạm vi**

- `Genres` / `Themes`: thêm `LibraryOwnerKey`, `NameKey`; `Themes` thêm `GroupId`.
- Bảng mới `ThemeGroups` (song ngữ EN/VI).
- Endpoint thư viện (chỉ ghi dữ liệu cá nhân) + endpoint admin (chỉ ghi dữ liệu `system:shared`).
- Admin Console: section mới **Taxonomy** (tab Themes có nhóm, tab Genres phẳng).
- `ManageTagsDialog` tab Themes: board theo nhóm, user quản lý nhóm/theme cá nhân.
- `ThemePickerDialog` thay popover chọn **theme** ở chi tiết game và form editor.
- Seed 4 nhóm chung + gán 42 theme hiện có.

**Ngoài phạm vi**

- Origin `metadata` (snapshot `MetadataThemes`…) và chuyển filter sang `themeKeys`/`genreKeys`
  (spec 09-14 §5.4). SQLite hiện chưa có tag metadata nào nên chưa có va chạm ID.
- Nhóm cho genre.
- Nhóm con (ví dụ Quá khứ / Hiện tại / Tương lai) — chỉ một cấp nhóm.
- Gán đè nhóm cho theme chung theo từng user: theme chung luôn nằm ở nhóm admin gán.
- Picker genre: giữ popover hiện tại.

## 3. Quyết định đã chốt

| # | Quyết định |
|---|---|
| D1 | Một cấp nhóm, không nhóm con. |
| D2 | Nhóm chung do admin sửa trong **Admin Console** (section Taxonomy). Thay thế ghi chú spec 09-14 §4.4 "admin sửa taxonomy manual ở ManageTagsDialog". |
| D3 | User tạo/sửa/xoá/sắp xếp **nhóm cá nhân**. |
| D4 | User chỉ kéo được **theme cá nhân** của mình — vào nhóm chung hoặc nhóm cá nhân của mình. Theme chung không di chuyển được ở mode thư viện. |
| D5 | Tên nhóm song ngữ `NameEn` / `NameVi`; server trả `displayName` theo `Accept-Language`. |
| D6 | Kéo thả dùng `@dnd-kit/core` + `@dnd-kit/sortable` (local-only, không gọi mạng; hỗ trợ bàn phím, cảm ứng). |
| D7 | Picker theme: dialog lớn, các khối nhóm xếp dọc, mỗi khối là lưới ô icon + tên. |
| D8 | Một spec gộp: owner-scoping + nhóm chung + nhóm cá nhân. |

## 4. Dữ liệu (SQLite)

### 4.1. Schema

```
Genres, Themes:
  + LibraryOwnerKey TEXT NOT NULL DEFAULT 'system:shared'   -- 'system:shared' | 'user:<id>' | 'guest:local'
  + NameKey         TEXT NOT NULL                            -- normalize(Name)
  UNIQUE INDEX (LibraryOwnerKey, NameKey)                    -- thay UNIQUE(Name)

Themes:
  + GroupId INTEGER NULL REFERENCES ThemeGroups(Id) ON DELETE SET NULL

ThemeGroups(
  Id              INTEGER PRIMARY KEY AUTOINCREMENT,
  LibraryOwnerKey TEXT NOT NULL,
  NameEn          TEXT NOT NULL,
  NameVi          TEXT NOT NULL,
  SortOrder       INTEGER NOT NULL DEFAULT 0
)
INDEX (LibraryOwnerKey, SortOrder)
```

`normalize`: trim, casefold (invariant), gộp chuỗi khoảng trắng thành một dấu cách. Một hàm duy nhất
ở backend (`TaxonomyRules.NormalizeName`), dùng cho cả migration lẫn ghi.

`LibraryOwnerKey` của người gọi lấy bằng `TryGetLibraryOwnerKey` sẵn có (`Program.cs`). Client
**không bao giờ** gửi `libraryOwnerKey`.

### 4.2. Bất biến (server kiểm tra)

- I1. Theme `system:shared` chỉ có `GroupId` trỏ tới nhóm `system:shared` hoặc `NULL`.
- I2. Theme cá nhân của owner O có `GroupId` trỏ tới nhóm `system:shared`, nhóm của O, hoặc `NULL`.
- I3. Nhóm cá nhân chỉ owner thấy.
- I4. Xoá nhóm → mọi theme trong đó (kể cả theme cá nhân của owner khác nằm trong nhóm chung) về
  `GroupId = NULL`. Theme không bị xoá.
- I5. Theme cá nhân và nhóm cá nhân luôn `IconData = NULL` (server ép, bỏ qua payload) — spec 09-14 §5.1.
- I6. Nhóm cá nhân: ít nhất một trong `nameEn`/`nameVi` khác rỗng; thiếu thì server lấy tên còn lại
  điền vào. Nhóm chung (admin): bắt buộc cả hai.

### 4.3. Thứ tự (server sắp, client chỉ render — AGENTS.md §2)

- Nhóm: `system:shared` trước (theo `SortOrder`, rồi `Id`), rồi nhóm cá nhân (theo `SortOrder`, rồi `Id`),
  cuối cùng mục *chưa phân nhóm*.
- Theme trong một nhóm: theme chung trước, theme cá nhân sau; mỗi phần theo `SortOrder`, rồi `Name`.

### 4.4. Khử trùng

Theo spec 09-14 §5.2, chỉ trong nhánh manual: cùng `NameKey`, ưu tiên personal → shared. Bản bị ẩn
không xuất hiện trong mọi GET của người gọi đó. Liên kết `GameThemes` tới bản bị ẩn vẫn giữ nguyên.

### 4.5. Migration

Chạy trong static ctor của `DataService`, idempotent, theo pattern `EnsureColumn` /
`CREATE TABLE IF NOT EXISTS` sẵn có. Sao lưu DB bằng cơ chế backup hiện có trước khi đổi schema.

1. `CREATE TABLE IF NOT EXISTS ThemeGroups …`.
2. `EnsureColumn` cho `LibraryOwnerKey`, `NameKey` (Genres, Themes) và `GroupId` (Themes).
3. Điền `NameKey` cho hàng còn rỗng (tính bằng `NormalizeName` trong C#, không bằng SQL `lower()`
   vì SQLite chỉ casefold ASCII).
4. `DROP INDEX IF EXISTS` index unique cũ trên `Name`; `CREATE UNIQUE INDEX IF NOT EXISTS` index mới.
   Cập nhật `MasterPlayContext` tương ứng (bỏ `HasIndex(Name).IsUnique()`).
5. Seed một lần — chỉ khi `ThemeGroups` rỗng: tạo 4 nhóm chung và gán theme theo §4.6 cho các theme
   `system:shared` khớp `NameKey` và đang `GroupId IS NULL`. Theme không có trong bảng → chưa phân nhóm.
6. Áp dụng tương tự cho `QuestLog.Template.db` (commit file template đã migrate).

### 4.6. Dữ liệu seed

| SortOrder | NameEn | NameVi | Theme |
|---|---|---|---|
| 0 | Era | Thời đại | Ancient, Prehistoric, Medieval, Renaissance, Early modern, Steam Punk, Modern, Retro, Near Future, Far Future, Cyberpunk, Post-Apocalyptic |
| 1 | Setting | Bối cảnh | Chinese, Feudal Japan, Greece, Roman, Egypt, Middle East, Nordic, Far West, Jungle, Ocean, Glacial, Desert, Urban, Villages, Underground, Space, Micro World |
| 2 | World Style | Phong cách thế giới | Historical, Sci-fi, Fantasy, Mythology, Dark, Gothic, Horror |
| 3 | Character | Nhân vật | Military, Agent / Spy, Pirate, Spartan, Super Hero, Super Power |

Tổng 42 theme. `SortOrder` của theme giữ nguyên giá trị hiện có.

## 5. API

### 5.1. Schema (`docs/openapi.yaml`)

`TaxonomyItem` — mở rộng **cộng thêm**, không đổi field cũ:

```ts
{
  id: number
  key: string                     // NameKey
  name: string
  iconData: string | null
  sortOrder: number
  scope: "shared" | "personal"
  editable: boolean               // server tính theo endpoint + role + scope
  groupId?: number | null         // chỉ có với theme
}
```

`ThemeGroup` — mới:

```ts
{
  id: number
  scope: "shared" | "personal"
  nameEn: string
  nameVi: string
  displayName: string             // theo Accept-Language (vi → nameVi, còn lại → nameEn)
  sortOrder: number
  editable: boolean
}
```

`GroupedThemes` — mới:

```ts
{ groups: Array<{ group: ThemeGroup | null; themes: TaxonomyItem[] }> }   // group null = chưa phân nhóm
```

Request:

```ts
ThemeOrderRequest      { groupId: number | null; ids: number[] }
ThemeGroupWriteRequest { nameEn?: string; nameVi?: string; sortOrder?: number }
IdOrderRequest         { ids: number[] }
```

`editable`: ở endpoint thư viện = `scope === "personal"`; ở endpoint admin = `scope === "shared"`.

### 5.2. Endpoint thư viện — user thường và guest

Mọi thao tác ghi chỉ chạm dữ liệu **cá nhân của người gọi** (admin cũng vậy ở mode thư viện).

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/taxonomy/{genres\|themes}?q=` | Sẵn có. Trả shared + personal của người gọi, đã khử trùng (§4.4). |
| `GET` | `/api/taxonomy/themes/grouped?q=` | `GroupedThemes`. Có `q` → lọc theo tên phía server, bỏ nhóm rỗng. Không `q` → giữ cả nhóm rỗng. |
| `POST` | `/api/taxonomy/{genres\|themes}` | Tạo personal. `iconData` bị bỏ qua. Theme nhận `groupId` (kiểm tra I2). Trùng `NameKey` với hàng shared hoặc personal mà người gọi thấy → `409 TAXONOMY_NAME_EXISTS`. |
| `PUT` | `/api/taxonomy/{genres\|themes}/{id}` | Chỉ hàng personal của người gọi, ngược lại `403 TAXONOMY_NOT_EDITABLE`. Theme nhận `groupId`. |
| `DELETE` | `/api/taxonomy/{genres\|themes}/{id}` | Như trên. Xoá liên kết `GameGenres`/`GameThemes` + hàng trong **một transaction** (đóng PARITY M33). |
| `PUT` | `/api/taxonomy/themes/order` | `ThemeOrderRequest`. Mọi `ids` phải là theme personal của người gọi; đặt `GroupId = groupId`, `SortOrder = index`. Một transaction. |
| `POST` | `/api/taxonomy/theme-groups` | Tạo nhóm personal (I6). |
| `PUT` | `/api/taxonomy/theme-groups/{id}` | Chỉ nhóm của người gọi. |
| `DELETE` | `/api/taxonomy/theme-groups/{id}` | Chỉ nhóm của người gọi; theme trong nhóm → chưa phân nhóm (I4). |
| `PUT` | `/api/taxonomy/theme-groups/order` | `IdOrderRequest`, chỉ nhóm của người gọi. |

Series giữ nguyên hành vi hiện tại (không owner-scope trong spec này).

### 5.3. Endpoint admin — `RequireAdmin`

Chỉ chạm dữ liệu `system:shared`. Được ghi `iconData`.

| Method | Path |
|---|---|
| `GET` | `/api/admin/taxonomy/themes/grouped` — chỉ nhóm/theme shared, kèm mục chưa phân nhóm |
| `GET` | `/api/admin/taxonomy/genres` |
| `POST` / `PUT` / `DELETE` | `/api/admin/taxonomy/{genres\|themes}[/{id}]` |
| `PUT` | `/api/admin/taxonomy/themes/order` (`ThemeOrderRequest`, kiểm tra I1) |
| `PUT` | `/api/admin/taxonomy/genres/order` (`IdOrderRequest`) |
| `POST` / `PUT` / `DELETE` | `/api/admin/taxonomy/theme-groups[/{id}]` |
| `PUT` | `/api/admin/taxonomy/theme-groups/order` |

Admin tạo theme shared trùng `NameKey` với personal của user khác: cho phép (khử trùng §4.4 xử lý).
Trùng với shared khác → `409 TAXONOMY_NAME_EXISTS`.

### 5.4. Thay đổi endpoint khác

- `PUT /api/games/{id}`: mọi id trong `genres`/`themes` phải là hàng shared hoặc personal của người gọi,
  ngược lại `400 TAXONOMY_NOT_VISIBLE`.
- `GameDetail.genres/themes` trả `TaxonomyItem` mở rộng.

### 5.5. Mã lỗi mới

| HTTP | Code | Khi nào |
|---|---|---|
| 403 | `TAXONOMY_NOT_EDITABLE` | Ghi vào hàng không thuộc phạm vi endpoint |
| 400 | `THEME_GROUP_INVALID` | `groupId` vi phạm I1/I2 hoặc không tồn tại với người gọi |
| 400 | `THEME_GROUP_NAME_REQUIRED` | Vi phạm I6 |
| 400 | `TAXONOMY_NOT_VISIBLE` | Gán cho game một tag người gọi không thấy |
| 404 | `THEME_GROUP_NOT_FOUND` | |

## 6. UI

### 6.1. Thành phần chung (`src/components/themes/`)

- **`ThemeGroupBoard`** — props: `data: GroupedThemes`, `onMoveThemes(groupId, ids)`, `onReorderGroups(ids)`,
  các callback sửa/xoá nhóm và theme (tuỳ nơi dùng).
  - Mỗi nhóm là một thẻ có tiêu đề `displayName`; theme là chip (icon qua `iconSrc()` + tên).
  - Chip kéo được chỉ khi `editable`; chip khác hiện icon khoá nhỏ. Thẻ nhóm kéo được chỉ khi `editable`.
  - Thả → cập nhật lạc quan cache React Query → gọi một `PUT …/order` → lỗi thì rollback + toast lỗi.
  - Sensor: pointer, touch, keyboard (Space nhấc, mũi tên di chuyển, Space thả, Esc huỷ); announcements
    của dnd-kit lấy chuỗi từ i18n.
- **`src/lib/themeBoard.ts`** — hàm thuần: từ trạng thái board + sự kiện thả → `{ groupId, ids }` cần gửi
  (chỉ gồm theme `editable` của nhóm đích, theo thứ tự hiển thị). Không lọc/sort dữ liệu domain.

### 6.2. Admin Console — section `taxonomy`

- `workspace.ts`: thêm `taxonomy` vào danh sách section; sửa comment "không có Genres/Themes".
- `AdminTaxonomySection.tsx`, client trong `src/lib/adminApi.ts`.
- Tab **Themes**: `ThemeGroupBoard` dữ liệu shared. Nút thêm nhóm; menu nhóm (đổi tên EN/VI, xoá có xác
  nhận); thêm/đổi tên/xoá theme; **đặt icon**: chọn file ảnh → client vẽ lại 128×128 PNG trên canvas →
  gửi data URL làm `iconData`.
- Tab **Genres**: danh sách sortable phẳng, cùng thao tác (không nhóm).

### 6.3. `ManageTagsDialog` — tab Themes

- Thay danh sách phẳng bằng `ThemeGroupBoard` từ `GET /api/taxonomy/themes/grouped`.
- Hai khu: **Nhóm chung** (khoá, chỉ xem) và **Nhóm của tôi** (nút "+ Nhóm", đổi tên, xoá, kéo sắp xếp).
- Theme cá nhân kéo được giữa mọi nhóm hợp lệ (I2); theme chung không kéo được.
- Các tab khác giữ nguyên read-only.

### 6.4. `ThemePickerDialog`

Thay popover chọn **theme** ở `GameDetailTaxonomyRow` (kind `themes`) và `TaxonomyPicker` (form editor,
kind `themes`). Picker genre không đổi.

```
┌ Chọn theme ─────────────────── [🔍 Tìm…      ] ┐
│ THỜI ĐẠI                                       │
│ [📜 Ancient] [🏰 Medieval] [✓ 💡 Modern] …     │
│ BỐI CẢNH                                       │
│ [🌴 Jungle] [🌊 Ocean] [✓ 🏙 Urban] …          │
│ PHONG CÁCH THẾ GIỚI · NHÂN VẬT · CỦA TÔI · …   │
│ CHƯA PHÂN NHÓM                                 │
│                   [+ Tạo theme]   [Lưu (2)]    │
└────────────────────────────────────────────────┘
```

- Dữ liệu `GET /api/taxonomy/themes/grouped?q=`; ô tìm kiếm debounce ~200 ms, **lọc ở server**.
- Ô theme: icon lớn (~40 px) + tên; đã chọn → ✓ + viền accent. Lưới tự co số cột; dialog `max-w ~960px`,
  cuộn dọc.
- Chọn/bỏ trong bản nháp; "Lưu (n)" gửi một `PUT /api/games/{id}` như luồng hiện tại
  (`gameDetailApi.update` / form editor giữ `themeIds`).
- "+ Tạo theme" → tạo personal (chưa phân nhóm) rồi tự chọn.
- Bàn phím: mũi tên di chuyển trong lưới, Enter/Space chọn, Esc đóng (hỏi nếu có thay đổi chưa lưu).
- Lỗi API hiển thị trong dialog, không nuốt.

### 6.5. i18n

Mọi chuỗi giao diện mới (tiêu đề, nút, "Chưa phân nhóm", "Nhóm chung", "Nhóm của tôi", thông báo dnd)
vào `i18n.tsx` (EN + VI). Tên nhóm/theme lấy từ API (`displayName` / `name`).

## 7. Kiểm thử

- **Backend:** tách logic thuần vào `TaxonomyRules` (normalize, kiểm tra I1/I2/I6, sắp xếp §4.3, khử trùng
  §4.4). Thêm project `MasterPlay.Host.Tests` (xUnit) chỉ test class này.
- **Migration:** chạy trên **bản sao** DB thật; kiểm tra 42 theme giữ icon, 4 nhóm, phân bổ đúng §4.6, chạy lần
  hai không đổi gì.
- **Frontend:** `tests/themeBoard.test.mjs` (`node:test`) cho `themeBoard.ts`.
- **Thủ công trên dev server** (bước đăng nhập do người dùng làm): kéo thả chuột + bàn phím, user thường không
  sửa được theme chung (403), picker lưu đúng, admin đặt icon.
- `npm run typecheck && npm run test`; `dotnet build`.

## 8. Tài liệu phải cập nhật

- `docs/openapi.yaml` → `npm run gen:api` (không sửa tay `src/lib/api/**`).
- `docs/API_CONTRACT.md` §2.2 (`TaxonomyItem`, `ThemeGroup`), §6 (endpoint mới + quy tắc quyền).
- `docs/DOMAIN_RULES.md`: quyền sở hữu taxonomy, nhóm theme, thứ tự server-side.
- Spec 09-14 §4.4: admin sửa taxonomy chung ở Admin Console section Taxonomy (D2).
- `docs/PARITY_CHECKLIST.md`: M33 và dòng mới cho nhóm theme.

## 9. Rủi ro

- **Đổi unique index trên `Name`:** EF model và DB phải khớp, nếu không `EnsureCreated` trên DB mới sẽ tạo
  index cũ. Template DB phải migrate cùng lúc.
- **Khử trùng ẩn bản shared:** user có theme cá nhân trùng tên thấy bản cá nhân (không icon). Chủ đích theo
  spec 09-14, nhưng cần ghi rõ trong DOMAIN_RULES.
- **Hành vi đổi với user thường:** sau thay đổi, user không còn đổi tên/xoá theme chung từ `TaxonomyPicker`
  được nữa (trước đây làm được do thiếu kiểm tra). Đây là sửa lỗi bảo mật, không phải regression.
