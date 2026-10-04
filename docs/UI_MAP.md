# QuestLog — UI MAP: Lovable ⇄ App hiện tại ⇄ API

| | |
|---|---|
| Ngày | 2026-08-14 |
| Nguồn Lovable | `D:\Workspace\_lovable-reference` (đã cài, chạy tại `localhost:8080`) |
| Nguồn hiện tại | `MasterPlay.UI` (WPF) @ `d9efa03` |
| Mục đích | Mỗi mảnh giao diện Lovable phải chỉ được: **thay cho cái gì** trong app hiện tại, **gọi API nào**, và **còn thiếu gì** |

> Đọc kèm: [`DOMAIN_RULES.md`](DOMAIN_RULES.md) (quy tắc nghiệp vụ) · [`openapi.yaml`](openapi.yaml) (hợp đồng) · [`PARITY_CHECKLIST.md`](PARITY_CHECKLIST.md) (theo dõi tiến độ)

---

## 0. Kết luận nhanh

Bản Lovable **không phải bản vẽ lại từ đầu** — nó bám khá sát app hiện tại về cấu trúc:

- Kích thước card **270 × 126.2 px** trùng **chính xác** với WPF.
- Đúng 4 tab, đúng 11 khoá sort, đúng 6 phạm vi search, đúng debounce 280ms, đúng 30 item/trang.
- Ownership info map **1:1** với bảng `GameOwnershipInfo`.

Khác biệt lớn nhất **không nằm ở giao diện** mà ở chỗ: WPF dùng **cửa sổ riêng** (`Window`) cho Editor/Settings, còn Lovable dùng **dialog/sheet trong cùng một trang**. Đây là thay đổi UX có chủ ý và nên giữ.

---

## 1. Bản đồ khung ứng dụng (shell)

Lovable gom toàn bộ app vào **một route duy nhất** `routes/index.tsx` (528 dòng) + các dialog. WPF trải ra nhiều `Window`.

| Vùng | Lovable | WPF hiện tại | API |
|---|---|---|---|
| **Header — tab** | `index.tsx:228-248` nav 4 nút | `MainWindow.xaml` ToggleButton nhóm đầu | `GET /api/games?tab=` |
| **Header — search** | `index.tsx:250-290` Input + Select scope | `SearchBox` + `SearchByComboBox` (`MainWindow.xaml:207,216`) | `?q=&scope=` |
| **Header — sort** | `index.tsx:293-337` Popover 11 mục + nút đảo chiều | `SortComboBox` (`:237`) + `ReverseCheckBox` (`:100`) | `?sort=&reverseSort=` |
| **Header — filter** | `index.tsx:339-351` nút + badge đếm | `FilterToggleButton` (`:258`) + `FilterPopup` (`:287`) | `?...filters` + `activeFilterCount` |
| **Header — thêm game** | nút `Plus` (`:353`) | phím `` ` `` / nút Add | `POST /api/games` |
| **Header — menu ⋯** | DropdownMenu (`:357-379`): Settings · Dashboard · Manage tags | rải rác ở menu/nút khác nhau | — |
| **Lưới card** | `index.tsx:408-419` WrapPanel 270px | `CardScrollViewer` → `ItemsControl` + `WrapPanel` (`:300-311`) | `GET /api/games` |
| **Footer — trạng thái** | `:425-428` chấm tròn + số kết quả | `PaginationSummaryTextBlock` (`:1153`) | — |
| **Footer — phân trang** | `:430-450` Prev / `Trang x/y` / Next | `PrevPageButton`/`PageInfoTextBlock`/`NextPageButton` (`:1166-1178`) | `?page=&pageSize=30` |
| **Footer — phím tắt** | Popover bàn phím (`:453-473`) | ❌ **không có** — tính năng thừa T11, giữ | — |
| **Footer — tray** | nút Gamepad2 mở TrayPanel (`:475-487`) | Tray là **tiến trình WinForms riêng** | `GET /api/tray/status` |

**Ghi chú kiến trúc:** WPF có `FilterBackdrop` (`:1196`) và `FilterPopup` là popup **neo trong cửa sổ chính**; Lovable dùng `Dialog` modal. Giữ cách của Lovable.

---

## 2. Bản đồ theo màn hình

### 2.1. Game Card

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/game-card.tsx` (114 dòng) | `MainWindow.xaml:313-1128` (~815 dòng inline) |
| Kích thước | `width: 270`, ảnh `height: 126.2` | `Width="270"`, ảnh `Height="126.2"` |
| **Ảnh** | `game.gallery` | `Game.GImagePath` |

✅ **Ảnh khớp nhau** — cả hai dùng gallery (ngang, decode 460px). *Bản đối chiếu cũ ghi sai ở M38, đã đính chính.*

Map từng field trên card:

| Hiển thị | Lovable | WPF | API field |
|---|---|---|---|
| Tên | `game.name` | `DisplayName` | `name` |
| MetaScore chip | `<MetaChip>` | Border + `MetaScoreColorConverter` | `metaScore` |
| Developer | `game.developer` | binding developer | `developer` |
| Progress pill | `<ProgressPill>` | pill + icon | `progress.displayName` + `iconData` + `colorCode` |
| Ownership pill | `<OwnershipPill>` | pill + icon | `ownership.*` |
| Excited | `<ExcitedFire>` | `FireValueControl` | `excited` |
| Ngày phát hành | `formatPublishDate(publishDate)` → `dd/MM/yyyy` | `DisplayPublishDate` | `publishDate` |
| Score | Gallery: `<GalleryScoreChip>` · Detail/Queue: `<ScoreBadge>` | `DisplayScore` | `score`, `scoreMaterialBand` |
| Tổng giờ chơi | `formatPlaytime(totalPlaytime)` | `DisplayPlayTime` | `totalPlaytimeSeconds` |
| Avg | `t('avg.'+avgPlaytime)` | `DisplayAvgPlayTime` | `avgPlaytimeBand` |
| Thời gian hoàn thành / Genres | `formatFinishTime(lastFinishedAt)` → `MM/yyyy` hoặc 2 genre đầu | tương tự | `lastFinishedAt` / `genres` |
| Nút Play | chỉ khi `exeFound` | chỉ khi có exe | **`canLaunch`** (backend quyết định) |
| Nút Details | luôn có | luôn có | — |
| Double-click | `onDoubleClick` | `MouseDoubleClick` | — |

**Còn thiếu trên card Lovable:**
- ❌ Cảnh báo thiếu folder (A10) → thêm badge từ `folderWarning`
- ❌ Icon "đang chơi" thật (A11) → từ `isRunning` qua SSE
- ❌ Ẩn/hiện field động theo sort+filter (A2) → dùng `cardHints`
- ❌ Nút Promote to Next Play (A12)

**Phải sửa:**
- `ExcitedFire` dùng thang **0–11 bước 0.5**; `11` là Peak Score và hiển thị `10+`.
- `MetaChip` dùng ngưỡng `>=75` / `>=50` → phải đổi sang dải chuẩn **90/80/70/50** (mismatch M39).

---

### 2.2. Play Queue

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/play-queue.tsx` (129 dòng) | `Features/Queue/QueueView.xaml` (235 dòng) |
| Ảnh hàng | gallery | `Game.GImagePath` ✅ khớp |
| Kéo thả | có | GongSolutions.WPF.DragDrop |

| API | Ghi chú |
|---|---|
| `GET /api/queue` | trả toàn bộ, **không phân trang**, đã chuẩn hoá thứ tự |
| `PUT /api/queue/order` | client **chỉ gửi `gameIds[]`**, server áp quy tắc |

**Điểm nguy hiểm nhất của màn này:** Lovable chỉ đánh lại số thứ tự. Quy tắc thật (`MainWindowViewModel.cs:960-1027`) còn phải **đổi cả ProgressId**: game `Playing` giữ nguyên, 3 game đầu trong nhóm không-Playing → `NextPlay`, còn lại → `Intended`. Toàn bộ chuyển về server (DOMAIN §8.2) — frontend **không được** cài lại.

---

### 2.3. Game Detail / Editor

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/game-detail.tsx` (**1004 dòng** — component lớn nhất) | `GameEditorControl.xaml` (1760) + `GameEditorWindow.xaml` + `GameEditorViewModel.cs` |
| Hình thái | Dialog trong trang, 3 mode `view` / `edit` / `add` | **Cửa sổ riêng**, cùng 3 mode |
| Ownership info | inline | `OwnershipInfoEditorControl.xaml` + `OwnershipInfoViewControl.xaml` (tách 2 file) |

Ánh xạ `GameOwnershipInfo` — **1:1 hoàn hảo**, không cần sửa:

| C# | Lovable |
|---|---|
| `SteamAccount` / `SteamPassword` | `steamAccount` / `steamPassword` |
| `SupportDocumentUrl` | `steamSupportUrl` |
| `UbisoftAccount` / `UbisoftPassword` | `ubisoftAccount` / `ubisoftPassword` |
| `DownloadUrl` | `downloadLink` |
| `Note` | `note` |

**Còn thiếu ở Lovable:** DLC (A3) · lịch sử `GameFinish[]` (A4) · `steamLibraryImagePath` + nút sync (A5) · Gemini đoán exe (A6) · link HLTB/Metacritic dựng từ slug (A7) · Generate Backup Folder (A8) · 2 nút mở Save/Backup folder (A9) · Cancel revert từ DB (A23).

**Phải đổi:** `InitExeName` → `launchTarget { kind: "exe"|"shortcut", path }` (DOMAIN §10) — đúng luồng mới đã chốt.

**Dùng mới:** hero ảnh dùng `imagePath` (cover) — field này **chưa từng hiển thị** trong WPF, nên phải xử lý trường hợp rỗng.

---

### 2.4. Filter

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/filter-dialog.tsx` (237) | `FilterPanel.xaml` (357) + `FilterPanel.xaml.cs` + `FilterOptions.cs` |

| Nhóm | Trạng thái |
|---|---|
| 4 nhóm số (Finished year, Excited, Score, Playtime) × 5 toán tử | ✅ có cả hai |
| 4 nhóm chip (Avg, Progress, Ownership, MetaScore) | ✅ có cả hai |
| Reverse filter | ✅ có — nhưng **logic sai** (M17) |
| **Filter theo Genre / Theme + ô search riêng** | ❌ **Lovable thiếu hẳn** (A1) — WPF có ở `FilterPanel.xaml:268-320` |

Toàn bộ ngưỡng và phép so sánh **chuyển về server**; `lib/filters.ts` chỉ còn giữ state UI.

---

### 2.5. Settings

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/settings-dialog.tsx` (377) | `SettingsWindow.xaml` (165) + `SettingsViewModel.cs` |
| Bố cục | **5 section có nav trái** | form phẳng |

| Section Lovable | Đối ứng WPF | API |
|---|---|---|
| `appearance` | theme (bool) | `themeMode` — `light` / `dark` / `system` / `jungle` (T7) |
| `language` | EN/VI | `language`, mặc định `en-US` |
| `storage` | Backup root, Config path, Database path | cần `pick-folder`/`pick-file` (A22) |
| `libraries` | danh sách thư mục game | **tách** `paths` (config) khỏi `runtime` (scanner) — M13 |
| `integrations` | Cloudinary | + RAWG key (T6), + test connection (T5) |

Bố cục 5 section tốt hơn form phẳng → **giữ**.

---

### 2.6. Manage Tags / Catalog

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/manage-tags-dialog.tsx` (282) + `taxonomy-picker.tsx` (208) | `CatalogSettingsControl.xaml` (36) + `CatalogSettingsViewModel.cs` |
| Danh mục | Series · Genres · Themes (**3**) | Genre · Theme · **Progress** · **Ownership** (**4**) |

❌ **Lovable thiếu 2 danh mục Progress và Ownership** (A15), và thiếu trường `SortOrder` — mà `SortOrder` chính là **nguồn thứ hạng sort** sau khi bỏ magic number (DOMAIN §3.2). Bắt buộc bổ sung.

❌ Icon hiện lưu data-URL trong React state → **mất khi reload** (A16). Phải đẩy lên `iconData` qua API.

⚠️ Series cần thêm cột `IconData` + `SortOrder` ở DB (S2, S3).

---

### 2.7. Tray Panel

| | Lovable | WPF/WinForms |
|---|---|---|
| File | `components/mp/tray-panel.tsx` (332) | `MasterPlay.TrayApp` (tiến trình riêng) |

| Chức năng | Lovable | Hiện tại |
|---|---|---|
| Bật/tắt 3 service | ✅ | ✅ |
| Open app · Sync images · Open logs · Start with Windows · Exit | ✅ | ✅ |
| **Bảng chi tiết từng service** (last scan, pending, next backup…) | ✅ **thừa** (T3) | dữ liệu có sẵn nhưng **chưa lộ ra UI** |
| **Notification center** | ✅ **thừa** (T2) | chỉ có balloon tip thoáng qua |
| Scan Now / Backup now thủ công | ✅ **thừa** (T4) | chỉ chạy theo lịch |

**Quyết định (B8):** tray thật vẫn là tiến trình WinForms riêng (phải sống khi UI đóng). Panel trong web chỉ là **bảng điều khiển đọc/ghi qua API**.

---

### 2.8. Dashboard

| | Lovable | WPF |
|---|---|---|
| File | `components/mp/dashboard-dialog.tsx` (99) | ❌ **không có** |

Tính năng thừa T1 — **giữ**, nhưng số liệu phải do `GET /api/dashboard/stats` tính, không tính ở client.

---

## 3. Thành phần Lovable KHÔNG có đối ứng (giữ lại)

| Lovable | Ghi chú |
|---|---|
| `dashboard-dialog.tsx` | T1 |
| Notification center trong `tray-panel.tsx` | T2 |
| `GameCardSkeleton` | T8 |
| Empty state có nút Reset | T9 — phân biệt "thư viện trống" vs "không có kết quả" |
| Badge đếm filter | T10 |
| Popover phím tắt | T11 |
| Toast (sonner) | T12 — thay `MessageBox`, nhưng xác nhận huỷ/xoá **vẫn phải là dialog chặn** |

## 4. Thành phần WPF KHÔNG có đối ứng (phải dựng mới)

| WPF | Xử lý |
|---|---|
| `FilterPanel.xaml:268-320` — filter Genre/Theme | A1 |
| 12 property `ShouldShowX` | A2 → `cardHints` |
| `ControllerNavigationService.cs` (414 dòng) | A17 — ⏸ **cắt khỏi v1** |
| `PopupKeyboardService.cs` | A25 — thứ tự đóng lớp |
| `GameProcessMonitor.cs` | A11 → SSE |
| `CatalogSettingsViewModel` (Progress/Ownership) | A15 |
| `Base64ImageConverter` | icon `iconData` từ DB |
| `MetaScoreColorConverter` | ngưỡng 90/80/70/50 — thay bảng màu Lovable |
| Màn Restore game đã disable | B11 — **mới hoàn toàn** |

## 5. File Lovable sẽ KHÔNG mang sang

| File | Lý do |
|---|---|
| `src/data/games.ts` (251) | 53 game giả |
| `src/lib/store.tsx` (312) | mock state → thay bằng TanStack Query gọi API |
| `src/lib/filters.ts` (236) | logic lọc/sort **sai** so với hệ thống thật → về server |
| `src/server.ts`, `src/start.ts`, `lib/error-capture.ts`, `lib/error-page.ts`, `lib/lovable-error-reporting.ts` | SSR + telemetry của editor Lovable (T17) |
| `vite.config.ts` (dùng `@lovable.dev/vite-tanstack-config`) | viết lại: SPA tĩnh, bỏ Nitro/Cloudflare |
| ~50 component `ui/` chưa dùng | T18 |

**Giữ và dùng lại:** `lib/i18n.tsx` (513 — đồng bộ khoá với `LocalizationService`), `lib/theme.tsx` (lưu qua API), `lib/tag-icons.tsx` (icon từ API), toàn bộ `components/mp/*.tsx` về mặt **layout**.

---

## 6. Thứ tự port

Mỗi màn hình chỉ merge khi API xanh **và** dòng tương ứng trong `PARITY_CHECKLIST.md` đã đánh dấu.

| # | Màn hình | Kéo theo |
|---|---|---|
| 1 | Library grid + search + sort + filter + phân trang | A1, A2, A18, A19, M16–M27 |
| 2 | Game detail (view) + launch + folder warning | A9, A10, A11, A21, M34 |
| 3 | Game editor (add/edit) | A3–A8, A23, M28, M30, M31, M35 |
| 4 | Play Queue | A12, A13, A14, M29 |
| 5 | Settings + Catalog/Manage tags | A15, A16, A22, A24, T13 |
| 6 | Tray panel | T2, T3, T4, B8 |
| 7 | Dashboard | T1 |

Lý do thứ tự này: Library dùng **nhiều nhất** các quy tắc dễ sai âm thầm (M16–M27), nên làm trước để cổng parity phát hiện lệch sớm, thay vì phát hiện ở cuối.
