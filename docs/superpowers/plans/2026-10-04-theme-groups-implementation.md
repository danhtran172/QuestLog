# Kế hoạch triển khai: nhóm theme + taxonomy theo chủ sở hữu

> Nguồn thiết kế: `docs/superpowers/specs/2026-10-04-theme-groups-design.md`
> Trạng thái: thiết kế đã duyệt; kế hoạch triển khai

## Nguyên tắc

- `docs/openapi.yaml` và DTO/endpoint ở Host là phần backend sở hữu. Sửa yaml rồi `npm run gen:api`;
  không sửa tay `MasterPlay.Web/src/lib/api/**`.
- Không lọc/sort/ngưỡng ở client. Mọi thứ tự nhóm/theme do server trả.
- Không gộp thay đổi chưa commit không liên quan của người dùng vào commit của tính năng này
  (working tree hiện có ~177 file đang sửa/untracked, gồm cả các file sẽ chạm tới).
- Mỗi bước kết thúc ở trạng thái build được: `dotnet build`, `npm run typecheck`, `npm run test`.

## 1. Logic thuần `TaxonomyRules` + project test

**File:**
- Thêm `MasterPlay.Host/Taxonomy/TaxonomyRules.cs`
- Thêm `MasterPlay.Host.Tests/MasterPlay.Host.Tests.csproj` (xUnit) + `TaxonomyRulesTests.cs`; thêm vào solution

**Việc:**

1. `NormalizeName(string)`: trim, `ToLowerInvariant`, gộp khoảng trắng.
2. `SharedOwnerKey = "system:shared"`.
3. `CanPlaceTheme(themeOwner, groupOwner?)` → kiểm tra I1/I2 (spec §4.2).
4. `ResolveGroupNames(nameEn?, nameVi?, requireBoth)` → I6; trả lỗi `THEME_GROUP_NAME_REQUIRED`.
5. `Dedupe(items, callerOwnerKey)` → khử trùng §4.4 (personal thắng shared theo `NameKey`).
6. `OrderGroups` / `OrderThemesInGroup` → §4.3.
7. Viết test trước cho từng hàm (TDD), gồm ca: Unicode (`Ă`, `Đ`), khoảng trắng kép, theme shared vào nhóm
   personal (từ chối), personal vào nhóm của owner khác (từ chối), nhóm chỉ có `nameVi`.

## 2. Schema + migration SQLite

**File:**
- Sửa `MasterPlay.Core/Entities/Theme.cs`, `Genre.cs`; thêm `MasterPlay.Core/Entities/ThemeGroup.cs`
- Sửa `MasterPlay.Infrastructure/Data/MasterPlayContext.cs`
- Sửa `MasterPlay.Infrastructure/Data/Services/DataService.cs`
- Thêm `MasterPlay.Infrastructure/Data/Migrations/ThemeGroupsMigration.cs` (gọi từ static ctor của `DataService`)
- Cập nhật `MasterPlay.Infrastructure/Data/Database/QuestLog.Template.db`

**Việc:**

1. Entity: `LibraryOwnerKey`, `NameKey` cho `Theme`/`Genre`; `GroupId` + nav `Group` cho `Theme`; entity
   `ThemeGroup`. `MasterPlayContext`: `DbSet<ThemeGroup>`, bỏ `HasIndex(Name).IsUnique()`, thêm unique
   `(LibraryOwnerKey, NameKey)`, FK `GroupId` `OnDelete(SetNull)`, default `'system:shared'`.
2. `ThemeGroupsMigration.Apply(context, dbPath)`:
   - Nếu thiếu cột `NameKey` trên `Themes` (dấu hiệu chưa migrate) → sao lưu trước bằng
     `SqliteConnection.BackupDatabase` thành `QuestLog.before-theme-groups-<yyyyMMdd-HHmmss>.db` cùng thư mục.
   - `CREATE TABLE IF NOT EXISTS ThemeGroups …` + index `(LibraryOwnerKey, SortOrder)`.
   - `EnsureColumn` cho 5 cột mới. `GroupId` thêm bằng `INTEGER NULL REFERENCES ThemeGroups(Id) ON DELETE SET NULL`.
   - Điền `NameKey` bằng `TaxonomyRules.NormalizeName` (C#) cho hàng rỗng.
   - Tìm và `DROP` index unique cũ trên `Name` (đọc tên thật từ `PRAGMA index_list`), tạo index unique mới.
   - Bật `PRAGMA foreign_keys=ON` cho connection nếu chưa bật (kiểm tra cấu hình hiện có trước).
   - Seed §4.6 khi `ThemeGroups` rỗng, trong một transaction.
3. Kiểm tra trên **bản sao** `%APPDATA%\QuestLog\QuestLog.db`: 42 theme còn icon, 4 nhóm, phân bổ đúng, chạy lần
   hai không đổi gì (so sánh dump trước/sau).
4. Chạy migration trên `QuestLog.Template.db`; xác nhận `EnsureCreated` trên DB trống cũng ra schema mới.

## 3. Hợp đồng API

**File:**
- Sửa `docs/openapi.yaml`
- Chạy `npm run gen:api` → `MasterPlay.Web/src/lib/api/types.gen.ts`; re-export ở `src/lib/api/domain.ts`
  (nếu `domain.ts` là file sinh/quy ước re-export — theo header của file)
- Sửa `docs/API_CONTRACT.md` §2.2, §6

**Việc:**

1. `TaxonomyItem` + `key`, `scope`, `editable`, `groupId` (nullable, optional).
2. Schema mới `ThemeGroup`, `GroupedThemes`, `ThemeOrderRequest`, `ThemeGroupWriteRequest`, `IdOrderRequest`.
3. Path thư viện (§5.2) và admin (§5.3) + mã lỗi (§5.5).

## 4. Endpoint thư viện (owner-scoped)

**File:**
- Sửa `MasterPlay.Host/Dtos.cs` (`TaxonomyItemDto` mở rộng, DTO nhóm/order)
- Thêm `MasterPlay.Host/Taxonomy/TaxonomyEndpoints.cs` (extension `MapTaxonomyEndpoints`) và chuyển các handler
  taxonomy hiện có ở `Program.cs` (~2773–2805, ~3430–3550) sang đó
- Sửa `Program.cs`: gọi `MapTaxonomyEndpoints`, sửa `PUT /api/games/{id}` (kiểm tra `TAXONOMY_NOT_VISIBLE`),
  sửa chỗ map `GameDetail.genres/themes`

**Việc:**

1. Helper `VisibleThemes(db, ownerKey)` / `VisibleGenres` = shared ∪ personal của owner, đã `Dedupe`.
2. `GET /api/taxonomy/{genres|themes}` dùng helper trên; `GET /api/taxonomy/themes/grouped?q=`.
3. `POST/PUT/DELETE` chỉ trên hàng `LibraryOwnerKey == ownerKey`; ép `IconData = null`; kiểm tra trùng tên
   với hàng người gọi thấy; `DELETE` + xoá liên kết trong một transaction.
4. `PUT /api/taxonomy/themes/order`, CRUD + order cho `/api/taxonomy/theme-groups`.
5. Series giữ nguyên hành vi.
6. `displayName` của nhóm: đọc `Accept-Language` (bắt đầu bằng `vi` → `NameVi`, còn lại `NameEn`).

## 5. Endpoint admin

**File:** `MasterPlay.Host/Taxonomy/AdminTaxonomyEndpoints.cs`; đăng ký trong `Program.cs`

**Việc:** §5.3 của spec. Mọi handler gọi `RequireAdmin` trước; chỉ chạm `system:shared`; cho ghi `iconData`;
kiểm tra I1 khi order/đặt nhóm.

**Kiểm tra:** với Host chạy local — guest gọi endpoint admin → 403 `ADMIN_REQUIRED`; guest sửa theme shared qua
endpoint thư viện → 403 `TAXONOMY_NOT_EDITABLE`. (Theo ghi chú memory: 401 không chứng minh route tồn tại —
kiểm tra bằng request có phiên guest.)

## 6. Frontend: nền tảng

**File:**
- `MasterPlay.Web/package.json`: thêm `@dnd-kit/core`, `@dnd-kit/sortable`, `@dnd-kit/utilities`
- Sửa `src/lib/api/client.ts`: `themesGrouped(q?)`, `orderThemes`, CRUD/order `themeGroups`
- Sửa `src/lib/adminApi.ts`: các hàm admin taxonomy tương ứng
- Thêm `src/lib/themeBoard.ts` + `tests/themeBoard.test.mjs`
- Sửa `src/lib/i18n.tsx`: chuỗi mới EN + VI

**Việc:**

1. `themeBoard.ts`: `applyDrop(board, activeId, overGroupId, overIndex)` → `{ board, request: { groupId, ids } }`;
   chỉ đưa theme `editable` vào `ids`; không sắp lại theme không editable. Test trước.
2. Hook `useGroupedThemes(scope: "library" | "admin", q)` dùng React Query; mutation order có optimistic update +
   rollback + toast lỗi.

## 7. `ThemeGroupBoard`

**File:** `src/components/themes/ThemeGroupBoard.tsx`, `ThemeChip.tsx`, `ThemeGroupCard.tsx`,
`ThemeGroupEditDialog.tsx` (tên EN/VI)

**Việc:** spec §6.1 — `DndContext` với Pointer/Touch/Keyboard sensor, `SortableContext` cho từng nhóm và cho danh
sách nhóm; chip/nhóm không `editable` thì `disabled`; icon qua `iconSrc()`; announcements lấy từ i18n.

## 8. Admin Console — section Taxonomy

**File:**
- Sửa `src/lib/workspace.ts` (thêm `taxonomy`, sửa comment)
- Sửa `src/components/AdminConsoleView.tsx` (điều hướng section)
- Thêm `src/components/AdminTaxonomySection.tsx`, `src/components/themes/IconPicker.tsx`

**Việc:** tab Themes (board shared + thêm/sửa/xoá nhóm và theme + đặt icon), tab Genres (sortable phẳng). `IconPicker`:
chọn file → canvas 128×128 → `toDataURL("image/png")`; báo lỗi nếu file không phải ảnh.

## 9. `ManageTagsDialog` — tab Themes

**File:** `src/components/ManageTagsDialog.tsx`

**Việc:** tab `themes` render `ThemeGroupBoard` dữ liệu thư viện, chia khu "Nhóm chung" / "Nhóm của tôi", nút "+ Nhóm",
menu nhóm cá nhân. Các tab khác giữ nguyên.

## 10. `ThemePickerDialog`

**File:**
- Thêm `src/components/themes/ThemePickerDialog.tsx`
- Sửa `src/components/GameDetailTaxonomyRow.tsx` (kind `themes` → mở dialog thay popover)
- Sửa `src/components/TaxonomyPicker.tsx` (kind `themes` → mở dialog)

**Việc:** spec §6.4 — lưới theo nhóm, tìm kiếm debounce gọi `grouped?q=`, bản nháp chọn, "Lưu (n)" dùng luồng lưu sẵn có,
"+ Tạo theme", điều hướng mũi tên trong lưới, Esc hỏi khi có thay đổi chưa lưu, lỗi hiển thị trong dialog.
Ẩn nút sửa/xoá ở các item `editable === false` trong `TaxonomyPicker`/`TaxonomyEditDialog`.

## 11. Tài liệu + nghiệm thu

1. `docs/DOMAIN_RULES.md`: mục quyền sở hữu taxonomy + nhóm theme.
2. Spec 09-14 §4.4: trỏ sang spec này (D2).
3. `docs/PARITY_CHECKLIST.md`: M33 ✅, thêm dòng nhóm theme.
4. Nghiệm thu thủ công trên dev server (người dùng đăng nhập): kéo thả chuột + bàn phím ở cả hai nơi, picker lưu
   đúng, user thường bị chặn sửa theme chung, admin đặt icon, đổi ngôn ngữ thấy tên nhóm đổi theo.
5. `dotnet build`, `dotnet test`, `npm run typecheck`, `npm run test`.
