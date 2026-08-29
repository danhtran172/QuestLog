# QuestLog — Đối chiếu Lovable UI vs hệ thống hiện tại & chiến lược thay thế giao diện

| Thuộc tính | Giá trị |
|---|---|
| Ngày lập | 2026-08-14 |
| Nguồn Lovable | `lovable-project-49c970a2.zip` (TanStack Start + React 19, template `tanstack_start_ts_current`) |
| Nguồn hiện tại | `MasterPlaySystem.slnx` @ `d9efa03` (nhánh `dev`) |
| Phạm vi | Toàn bộ UI/UX hai phía + mô hình dữ liệu + hợp đồng backend |
| Phân vai | **Backend + hợp đồng dữ liệu: tôi.** **Frontend/UI: Lovable.** Lovable **không** được quyết định backend. |

---

## 0. Kết luận quan trọng nhất (đọc trước khi làm gì)

Đây **không phải** hai bản cài đặt của cùng một app. Không có đường nối nào giữa chúng ở thời điểm hiện tại:

| | Hệ thống hiện tại | Bản Lovable |
|---|---|---|
| Loại | Desktop WPF `net8.0-windows` + tray WinForms | Web app SSR (TanStack Start + Nitro, target Cloudflare) |
| Truy cập dữ liệu | EF Core → SQLite **in-process**, `DataService` là `static class` | **100% mock in-memory** (`src/data/games.ts` → `DEMO_GAMES`, 53 game giả) |
| Lớp API | **Không tồn tại** (không có HttpListener/Kestrel/NamedPipe/SignalR trong toàn solution) | Không gọi API nào; mọi mutation là `setState` |
| Khả năng native | Process launch, OpenFolderDialog, Explorer, Registry HKCU, FileSystemWatcher, XInput | Không có; đều là `toast.info(...)` giả lập |
| Nghiệp vụ | Nằm trong `MainWindowViewModel` / `GameEditorViewModel` / `DataService` | Nằm trong `src/lib/filters.ts` + `src/lib/store.tsx` (bản sao **không khớp**) |

⇒ "Merge UI/UX" thực chất gồm **3 việc**, phải làm đúng thứ tự:
1. **Dựng đường nối** (API cục bộ + vỏ desktop) — chưa hề có.
2. **Chốt hợp đồng dữ liệu** để Lovable không tự bịa field/enum/đơn vị.
3. **Port từng màn hình**, đo parity, rồi mới xoá WPF.

Nếu merge trước khi làm bước 1–2, mọi thứ Lovable đang tính toán ở client (ngưỡng lọc, thứ hạng sort, đơn vị thời gian) sẽ **âm thầm cho kết quả khác** với hệ thống thật. Mục 4 liệt kê 30 điểm lệch loại này.

---

## 0.1. Kiến trúc v2 — chốt sau trao đổi ngày 2026-08-14 (thay thế giả định "desktop-only" ở Mục 0)

Trao đổi trực tiếp đã đổi hẳn phạm vi: đây **không còn** là app desktop đơn thuần cần "khoác áo web", mà là **app web + desktop song song, dùng chung tài khoản/dữ liệu, kiểu Notion**. Các quyết định đã chốt qua hỏi-đáp trực tiếp (không phải suy đoán):

| Hạng mục | Chốt |
|---|---|
| Hosting dữ liệu | **Supabase** (Postgres + Auth + Storage) |
| Đăng nhập | **Auth thật** qua Supabase Auth — không phải single-user bỏ qua đăng nhập |
| Excited (B3) | Dữ liệu thật đang ở thang **1–10 nguyên + sentinel 999** (xác nhận trực tiếp, không phải 0–5). Sentinel 999 hiển thị `10🔥` giống Score. **Lovable đang sai** — đang ép về 0–5 và vẽ tối đa 5 ngọn lửa, bắt buộc sửa lại component trước khi dùng được. |
| Ngôn ngữ mặc định (B6) | Giữ **en-US**, không đổi theo Lovable |
| API key RAWG/Gemini (B10) | **Thu hồi cả 2 key đang lộ**, tạo key mới, chuyển vào cấu hình — không hardcode trong source nữa |
| Vỏ desktop (B1) | WebView2 vẫn đúng cho phần shell — nhưng bối cảnh đổi, xem kiến trúc song song bên dưới |

### Kiến trúc song song web/desktop

Vấn đề cốt lõi: **scan thư viện, launch game, theo dõi process đang chạy, đọc XInput, mở Explorer** đều là hành vi chỉ máy cài game mới làm được — trình duyệt không bao giờ làm được (khớp xác nhận "web không có tray"). Nên không thể có 1 backend giống hệt nhau phục vụ cả hai; phải tách theo **nơi chạy**, không tách theo **code**:

- **1 bộ business logic duy nhất, viết 1 lần bằng C# (ASP.NET Core Minimal API)** — đúng như kế hoạch gốc — nhưng **deploy 2 nơi**:
  1. **Local API** — chạy ngay trên máy chơi game, đi kèm local agent (kế thừa `MasterPlay.Core`/`Infrastructure`/`TrayApp` hiện có). Đủ mọi endpoint kể cả nhóm native (scan, launch, reveal folder, XInput). Vỏ desktop (WebView2 + WPF) trỏ vào `http://127.0.0.1:{port}` này.
  2. **Hosted API** — cùng 1 codebase, build/deploy lên một host luôn bật, **ẩn/tắt** các endpoint native (trả `501` hoặc ẩn nút trên UI khi phát hiện chạy ở chế độ hosted). Phục vụ bản web.
- **Cả hai** trỏ về **cùng một Postgres (Supabase)** làm nguồn dữ liệu thật — bỏ SQLite in-process. EF Core đổi provider `Sqlite` → `Npgsql`.
- **Auth**: Supabase Auth phát JWT; cả Local API lẫn Hosted API validate JWT đó (bearer + JWKS của Supabase). Local API có thể nới lỏng hơn (đã đứng sau `127.0.0.1`, máy của chính chủ) nhưng vẫn nên yêu cầu token để nhất quán và để tray panel dùng chung cơ chế xác thực.
- **RLS Postgres** làm lớp phòng thủ thứ 2 (theo best practice chuẩn của Supabase) — dù hiện chỉ có 1 user thật, vẫn thiết kế cột `user_id` và policy ngay từ đầu để không phải migrate lại khi có user thứ hai.

### Việc PHẢI làm thêm so với kế hoạch gốc (Giai đoạn 0–1 phình to)
- Migration schema SQLite → Postgres (kiểu dữ liệu, index, sequence thay AUTOINCREMENT).
- Viết RLS policy cho từng bảng.
- Tích hợp Supabase Auth vào cả Lovable frontend (đăng nhập/đăng xuất/session) và 2 API (JWT validation).
- Chọn nơi host **Hosted API** — Supabase tự nó không chạy .NET (Edge Functions chỉ chạy Deno/TS), nên cần thêm một dịch vụ hosting nhỏ độc lập (Fly.io / Render / VPS...).
- Chiến lược khi Local API mất mạng (Supabase là managed cloud, không phải LAN nội bộ nữa) — cache/queue cục bộ hay bắt buộc online mới dùng được app? *Còn mở, xem Mục 7.*

---

## 1. Các tính năng ĐÃ TRÙNG KHỚP

Trùng ở mức "có mặt và cùng ý đồ UX". Sai lệch chi tiết của một số mục nằm ở **Mục 4**.

### 1.1. Điều hướng & thư viện
- 4 chế độ: All / Planning / Finished / Play Queue.
- Planning = `progress != Done`; Finished = `progress == Done`; Queue = `Playing | NextPlay | Intended`.
- Chỉ hiển thị game chưa bị disable.
- Phân trang 30 item/trang, Prev/Next, hiển thị `Trang x/y`.
- Play Queue lấy toàn bộ item (không phân trang) để phục vụ drag-and-drop.

### 1.2. Search
- Debounce **280 ms** (Lovable dùng đúng con số 280).
- 6 phạm vi: All / Name / Series / Publisher / Developer / Release year.
- Tắt search khi ở Play Queue.
- Search "All" đối chiếu: tên, series, genre, theme, chữ cái đầu.

### 1.3. Sort
- Đúng 11 khoá, **đúng thứ tự**: Default, Status, Name, PlayTime, Publish, MetaScore, Excited, Score, Ownership, Progress, AvgPlaytime.
- Có toggle đảo chiều.
- Default sort thay đổi theo tab.

### 1.4. Filter
- 4 nhóm số với 5 toán tử `>` `>=` `=` `<=` `<`: Finished year, Excited, Score, Total playtime.
- 4 nhóm chip đa chọn: Avg length, Progress, Ownership, MetaScore.
- Reverse filter (đảo kết quả khớp).
- Apply / Clear / Cancel.

### 1.5. Game card
Cover, tên, developer, ProgressPill, OwnershipPill, MetaScore chip, Excited, Score, tổng giờ chơi, Avg, năm phát hành, năm hoàn thành, nút Play (chỉ khi có exe), nút Details, double-click mở chi tiết.

### 1.6. Play Queue
Bảng: ảnh, số thứ tự, tên, series/developer, Progress, Excited, Published, MetaScore, AvgPlay, Ownership. Kéo thả đổi thứ tự, double-click mở editor, nút mở editor.

### 1.7. Game Editor
- 3 chế độ: Add / View (read-only) / Edit.
- Trường: Name, Score, TotalPlaytime (giờ + phút), PublishDate, Publisher, Developer, Slug, Series, Genres (nhiều), Themes (nhiều), MetaScore, AvgPlaytime, Excited, Ownership, Progress, FinishedIn, ExeName, InitExeName (có checkbox bật/tắt), SaveFolderPath, BackupFolderPath, Cover, Gallery.
- Cộng/trừ playtime theo số phút do người dùng nhập.
- 3 nút sync: Metadata (RAWG) / Cover / Gallery.
- Preview ảnh, fallback khi ảnh lỗi.
- Finish Game popup: nhập điểm, chấp nhận 0–100 và giá trị đặc biệt **999 → `10🔥`**, chuyển Progress = Done, ghi năm hoàn thành.
- Disable game có xác nhận (soft delete).
- Save / Cancel / Close.

### 1.8. Ownership info (Steam Off / Pirate / Ubisoft)
Ánh xạ **1:1 hoàn hảo** với bảng `GameOwnershipInfo`:

| `GameOwnershipInfo` (C#) | Lovable `Game` |
|---|---|
| `SteamAccount` | `steamAccount` |
| `SteamPassword` | `steamPassword` |
| `SupportDocumentUrl` | `steamSupportUrl` |
| `UbisoftAccount` | `ubisoftAccount` |
| `UbisoftPassword` | `ubisoftPassword` |
| `DownloadUrl` | `downloadLink` |
| `Note` | `note` |

Cùng có: nút Info toggle khung thông tin, che/hiện mật khẩu, nút copy, mở link tài liệu hỗ trợ, hiện Ubisoft chỉ khi Publisher/Developer chứa "Ubisoft".

### 1.9. Series / Genre / Theme
Chọn 1 Series, nhiều Genres, nhiều Themes; search trong danh sách; tạo mới ngay từ nội dung search; sửa tên; xoá có xác nhận; cập nhật association sau khi xoá.

### 1.10. Settings
Backup root, Config JSON path, Database `.db` path, danh sách Game library path (thêm/sửa/xoá), ngôn ngữ EN/VI hiển thị tên bản địa, preview ngôn ngữ ngay khi chọn, Cancel khôi phục ngôn ngữ cũ, Cloudinary cloud name / api key / api secret.

### 1.11. Tray
Toggle 3 service (Playtime Tracking / Library Scanner / Backup Service), Open app, Sync Images to Cloudinary + log tiến trình, Open Log Folder, Start with Windows, Exit có xác nhận.

### 1.12. Khác
- Phím tắt: F5, F6, F11, `` ` ``, Ctrl+1..4, Esc.
- Light / Dark theme.
- Song ngữ EN / VI.
- Overlay + Esc đóng lớp trên cùng.

---

## 2. Tính năng Lovable CÒN THIẾU so với hệ thống hiện tại

### 2.A — Nhóm XỬ LÝ ĐƯỢC (kỹ thuật rõ ràng, không cần quyết định sản phẩm)

| # | Tính năng thiếu | Ở đâu trong code hiện tại | Cách xử lý |
|---|---|---|---|
| A1 | **Filter theo Genre và Theme** (có ô search riêng cho từng nhóm) | `FilterOptions.SelectedGenreIds/SelectedThemeIds`, `FilterPanel.xaml:268-320` | Backend nhận `genreIds[]`, `themeIds[]`; Lovable thêm 2 nhóm chip có search vào `filter-dialog.tsx`. Lưu ý logic hiện tại là **OR giữa genre và theme** (`MainWindowViewModel.cs:756-761`). |
| A2 | **Card field visibility động** theo sort/filter đang bật | 12 property `ShouldShowX` (`MainWindowViewModel.cs:88-102`) | Backend trả `cardHints` trong response list; card render có điều kiện. |
| A3 | **DLC** (`IsDlc`, `BaseGameId`) | `Game.cs`, `GameEditorViewModel.cs:1609-1667` | Thêm checkbox "Is DLC", picker Base game, auto gắn hậu tố `" [DLC]"`, xoá dữ liệu runtime, ẩn Series/Exe/Save/Backup/Playtime/Finish khi là DLC, kế thừa `SeriesId` từ base game. |
| A4 | **Lịch sử hoàn thành nhiều lần** (`GameFinish`) | `GameFinish.cs`, `DataService.AddGameFinish`, `DisplayFinishHistory` | API trả `finishes[]`; UI hiện danh sách `yyyy-MM-dd · score`. Đây đã **thay thế** ô `FinishedIn` đơn lẻ ở màn hình hiện tại. |
| A5 | **Ảnh thứ 3: `SteamLibraryImagePath`** + nút Sync riêng | `Game.cs`, `SyncSteamLibraryImageCommand` | Thêm field + preview + nút sync. Lovable chỉ có cover + gallery. |
| A6 | **Gemini đoán tên executable** khi sync metadata | `GeminiService.GetExecutableNameAsync` | Backend làm; UI chỉ hiển thị kết quả điền vào `ExeName`. |
| A7 | **Mở HowLongToBeat / Metacritic theo Slug** | `CheckPlayTimeCommand`, `CheckMetaScoreCommand` | Backend dựng URL từ `Name` / `SlugName` và mở bằng shell. Lovable đang hardcode URL sẵn trên object game (`hltbUrl`, `metacriticUrl`) — phải bỏ. |
| A8 | **Generate Backup Folder** (tạo tên duy nhất, đổi tên khi game đổi tên, ghi `SaveLocation.txt`) | `DataService.CreateBackupFolder / RenameBackupFolder / UpdateSaveLocationFile` | Nút "Generate" gọi API; backend giữ nguyên logic. |
| A9 | **Mở Save folder / Backup folder trong Explorer (2 nút riêng)** | `OpenSaveFolderCommand`, `OpenBackupFolderCommand` | API `POST /api/fs/reveal`. Lovable chỉ có 1 nút giả lập bằng toast. |
| A10 | **Cảnh báo thiếu folder trên card** (`ShowFolderWarning` + text lý do) | `GameViewModel.CheckFolderWarning` | Backend tính, trả `folderWarning: string \| null`. |
| A11 | **Trạng thái "đang chơi" thật** (poll process 2 s, chỉ phát khi snapshot đổi) | `GameProcessMonitor` | SSE `/api/events` đẩy danh sách process đang chạy; UI bật icon Playing. |
| A12 | **Promote to Next Play** | `DataService.ReorderOnPromoteToNextPlay` | Lovable có sẵn key i18n `action.promote` và hàm `promoteToNextPlay` trong store nhưng **không nút nào gọi**. Thêm nút vào card/queue/detail. |
| A13 | **Quy tắc nghiệp vụ khi kéo thả Play Queue** | `MainWindowViewModel.cs:960-1027` | Sau khi kéo: game `Playing` giữ nguyên; **3 game đầu không-Playing → NextPlay**, phần còn lại → **Intended**; lưu cả PlayOrder lẫn ProgressId. Lovable chỉ đánh lại số thứ tự. |
| A14 | **Chuẩn hoá + persist thứ tự queue khi vào tab Queue** | `ResetAndPersistPlayQueueOrder` | Backend làm khi trả queue. |
| A15 | **Quản lý Progress / Ownership** (đổi tên, đổi icon, đổi SortOrder, kéo thả sắp xếp) | `CatalogSettingsViewModel` (4 danh mục: Genre, Theme, Progress, Ownership) | Lovable `ManageTagsDialog` chỉ có Series/Genres/Themes. Phải bổ sung 2 danh mục và cả trường `SortOrder`. |
| A16 | **Icon danh mục lưu vào DB** (`IconData`, base64 PNG) | `Genre/Theme/ProgressStatus/OwnershipStatus.IconData` | Lovable lưu data-URL trong React state → **mất khi reload**. Phải đẩy lên API. |
| A17 | **Điều hướng tay cầm + Console mode** | `ControllerNavigationService` (414 dòng) | ✅ **ĐÃ CHỐT 2026-08-29**: web dùng Gamepad API luôn bật; F10 hoặc LB+RB+A mở Console fullscreen; Shell chỉ nhận lệnh fullscreen có schema cố định. |
| A18 | **Ctrl+K focus ô search** | `MainWindow.xaml.cs:179` | Thêm handler. |
| A19 | **Scroll to top khi đổi trang** | `PrevPageButton_Click` / `NextPageButton_Click` | Thêm. |
| A20 | **Localize giá trị dữ liệu** (`GetDataValue` chuẩn hoá tên Progress/Ownership từ DB rồi mới dịch) | `LocalizationService` | Lovable dịch bằng key cứng `status.Done`. Khi user đổi tên Progress trong Catalog Settings, key sẽ trượt. Backend phải trả kèm `displayName` đã localize. |
| A21 | **Launch game qua `DetectedProcess.FullPath`** (ưu tiên `InitExeName`, cảnh báo nếu không tìm thấy) | `GameViewModel.GetLaunchProcessPath`, `MainWindowActions.LaunchGame` | API `POST /api/games/{id}/launch`. |
| A22 | **Chọn Config path / Database path bằng file dialog** | `SettingsViewModel.EditConfig/EditDatabase` | API `POST /api/fs/pick-file` + `pick-folder`. Lovable đang cho gõ tay chuỗi. |
| A23 | **Cancel trong editor khôi phục dữ liệu từ DB** | `GameEditorViewModel.Cancel` → `ReloadGameFromConfig` | Lovable `cancel` chỉ đổi mode, **không** revert draft. |
| A24 | **Settings Cancel khôi phục cả theme** | `SettingsViewModel.RestoreTheme` | Lovable chỉ khôi phục ngôn ngữ. |
| A25 | **Đóng ComboBox đang mở trước khi đóng window bằng Esc** | `PopupKeyboardService` | Thứ tự đóng lớp: popup con → popup cha → modal. |

### 2.B — Nhóm CẦN TRAO ĐỔI (quyết định sản phẩm/kiến trúc, tôi không tự quyết)

| # | Vấn đề | Vì sao cần bàn | Đề xuất của tôi |
|---|---|---|---|
| **B1** | **Mô hình host: chạy UI web trong cái gì?** | Lovable build ra SSR app cho Cloudflare. Muốn thành app desktop phải chọn vỏ. Đây là quyết định *không thể lùi rẻ*. | ✅ **ĐÃ CHỐT** (xem Mục 0.1): WebView2 trong WPF cho bản desktop, trỏ Local API; đồng thời deploy song song một Hosted API (cùng codebase) cho bản web. Không dùng Electron/Tauri. |
| **B2** | **`AvgPlayTime`: số giờ hay dải (band)?** | Backend lưu `int?` = số giờ và tự suy ra dải. Lovable lưu **thẳng dải** (`"Long"`), **mất số liệu gốc**, và RAWG trả về số giờ. | Giữ **số giờ** làm nguồn sự thật. Backend trả cả `avgPlaytimeHours` và `avgPlaytimeBand`. UI hiển thị dải, form nhập số. |
| **B3** | **`Excited`: thang nào?** | Backend `double?`, có sentinel `999 → "10"`, không chặn trên. Lovable ép `0..5` nguyên và vẽ 5 ngọn lửa. | ✅ **ĐÃ CHỐT** (xem Mục 0.1): dữ liệu thật là **1–10 + sentinel 999**. Giữ nguyên thang này, sentinel hiển thị `10🔥`. Lovable phải sửa lại UI Excited (bỏ giới hạn 5 ngọn lửa), **không** cần script migrate vì dữ liệu vốn đã đúng thang. |
| **B4** | **`FinishedIn` (string) vs `GameFinish[]`** | DB đang có cả hai; `GameViewModel` ưu tiên `Finishes` nếu có, fallback `FinishedIn`. Đây là di sản nửa vời. | Bỏ hẳn `FinishedIn` khỏi UI mới, chỉ dùng `GameFinish[]`; giữ cột trong DB để backfill. Cần xác nhận filter "Finished year" sẽ lọc theo lần hoàn thành **gần nhất** hay **bất kỳ lần nào**. |
| **B5** | **Progress/Ownership: enum cứng hay bảng động?** | DB là bảng cho phép sửa tên/icon/thứ tự, **nhưng code lại hardcode Id** (`ProgressId==1` là Done, `==2` Playing, `==3` NextPlay, `==4` Intended; `OwnershipId==6` SteamOff, `==2` Pirated). Lovable hardcode 6 chuỗi. | Thêm cột **`SystemKey`** bất biến (`Done`,`Playing`,`NextPlay`,`Intended`,`Dropped`,`Pending` / `Ready`,`Pirated`,`GamePass`,`NotOwned`,`PSRequired`,`SteamOff`). Code dùng `SystemKey`, người dùng chỉ sửa `Name`/`Icon`/`SortOrder`. Bỏ hết magic number. **Đây là việc bắt buộc** dù chọn hướng nào. |
| **B6** | **Ngôn ngữ mặc định** | Backend `en-US`. Lovable `vi` (kể cả `<html lang="vi">`). | ✅ **ĐÃ CHỐT**: giữ `en-US`. Lovable phải đổi `<html lang>` và default locale. |
| **B7** | **Có giữ điều hướng tay cầm không?** | Console cần hoạt động tốt từ TV và không phụ thuộc chuột. | ✅ **ĐÃ CHỐT 2026-08-29**: giữ và mở rộng bằng Gamepad API, prompt theo ngữ cảnh, Console coverflow và fullscreen qua WebView2 bridge. |
| **B8** | **Tray app tách rời hay gộp?** | Hiện là tiến trình WinForms độc lập, có vòng đời riêng. Lovable dựng panel tray **bên trong** UI web. | Giữ tray là tiến trình riêng (nó phải sống khi UI đóng); panel trong web chỉ là **bảng điều khiển đọc/ghi qua API**. Tray menu thật vẫn ở WinForms. |
| **B9** | **Bảo mật API** | Config chứa Cloudinary secret; DB chứa **mật khẩu Steam/Ubisoft dạng plaintext**. Sau quyết định Mục 0.1, **Hosted API thật sự lộ ra Internet** — mức rủi ro cao hơn hẳn giả định gốc (chỉ `127.0.0.1`). | Local API: giữ bind `127.0.0.1`, port ngẫu nhiên ghi `%AppData%`, vẫn yêu cầu JWT. Hosted API: JWT bắt buộc + RLS Postgres + rate limit + HTTPS only + CORS chỉ origin web chính thức. **Bắt buộc mã hoá `GameOwnershipInfo` bằng DPAPI (local) hoặc pgcrypto (Postgres)** trước khi dữ liệu rời máy — plaintext password không được phép rời khỏi localhost. |
| **B10** | **RAWG / Gemini API key** | `rawgApiKey` **hardcode trong `RawgService.cs:12`**; Gemini có fallback key **hardcode trong `GeminiService.cs:9`**. Lovable Settings lại có ô nhập RAWG key. | ✅ **ĐÃ CHỐT**: thu hồi cả 2 key đang lộ, tạo key mới, chuyển vào cấu hình (không hardcode), expose ô nhập trong Settings đúng như Lovable đã thiết kế. |
| **B11** | **Restore game đã disable** | Hiện tại không có màn hình nào khôi phục. Giao diện mới là dịp tốt để thêm. | Thêm tab/filter "Disabled" + nút Restore. Cần anh duyệt vì là tính năng mới. |
| **B12** | **Phân trang 30/trang có giữ không?** | Web hiện đại thường dùng virtual scroll. Nhưng đổi là đổi UX. | Giữ 30/trang ở v1 để đo parity; virtual scroll để sau. |

---

## 3. Tính năng THỪA của Lovable (không có trong hệ thống hiện tại)

| # | Tính năng | Đánh giá | Xử lý |
|---|---|---|---|
| T1 | **Dashboard** — 6 ô thống kê (tổng game, đã xong, đang chơi, trong queue, tổng giờ, điểm TB) + top-5 game chơi nhiều nhất | Tốt, dữ liệu backend đã có sẵn đủ | **Giữ.** Backend thêm `GET /api/dashboard/stats`. Không tính ở client. |
| T2 | **Notification center** trong tray panel | Tốt; hiện tray chỉ có balloon tip thoáng qua | **Giữ.** Backend cần lưu lịch sử event (bảng mới hoặc ring buffer trong tray). |
| T3 | **Bảng chi tiết từng service**: last scan, last flush, pending minutes, next eligible backup, số save folder đang theo dõi, số bản ghi diary | Dữ liệu này **đã tồn tại trong 3 service** nhưng chưa từng lộ ra UI | **Giữ.** Cần expose từ `PlayTimeService`/`LibraryService`/`BackupService`. |
| T4 | **Nút "Scan Now" / "Backup now" thủ công** | Hiện chỉ chạy theo lịch (15 phút / event) | **Giữ.** Thêm API trigger. |
| T5 | **"Test connection" cho Cloudinary** | Hữu ích | **Giữ.** Backend gọi Cloudinary ping thật. |
| T6 | **Ô nhập RAWG API key trong Settings** | Đúng hướng — xem B10 | **Giữ**, kèm chuyển key ra khỏi source. |
| T7 | **Theme mode "System" + "Jungle"** | Backend cũ chỉ có `bool isDarkMode` | **Giữ.** Đổi `Config.isDarkMode` → `Config.ThemeMode: "light"\|"dark"\|"system"\|"jungle"` (migrate: `true`→dark, `false`→light). |
| T8 | **Loading skeleton** cho lưới card | Tốt | Giữ. |
| T9 | **Empty state có nút Reset** (phân biệt "thư viện trống" vs "không có kết quả") | Tốt | Giữ. |
| T10 | **Badge đếm số filter đang bật** | Tốt | Giữ (backend trả `activeFilterCount`). |
| T11 | **Popover danh sách phím tắt** | Tốt | Giữ. |
| T12 | **Toast (sonner)** thay cho `MessageBox` chặn luồng | Tốt hơn hẳn hiện tại | Giữ. Nhưng xác nhận huỷ/xoá vẫn phải là dialog chặn. |
| T13 | **Icon tuỳ biến cho Series** | Backend `Series` **không có cột `IconData`** | Cần quyết: thêm cột, hoặc bỏ icon cho Series. |
| T14 | **"Has custom launch" chọn file `.lnk`** | Backend `InitExeName` là **tên process, không phải đường dẫn** | ✅ **ĐÃ CHỐT 2026-08-14**: đổi luồng thành **chọn exe hoặc shortcut**. Thay `InitExeName` bằng `launchTarget {kind, path}`. Chi tiết: `docs/DOMAIN_RULES.md` §10. |
| T15 | **Settings chia 5 section có nav trái** | Tốt hơn form phẳng hiện tại | Giữ. |
| T16 | **Layout responsive / mobile** (`use-mobile.tsx`, breakpoint `lg:`) | Không cần cho app desktop | Bỏ hoặc để nguyên, vô hại. |
| T17 | **404 page, error boundary, `src/server.ts` (SSR error wrapper), `error-capture.ts`, `lovable-error-reporting.ts`** | Chỉ có nghĩa khi chạy SSR/trong editor Lovable. `reportLovableError` gọi `window.__lovableEvents` — chỉ tồn tại trong preview của Lovable, không tự gửi ra ngoài ở bản build. | **Gỡ toàn bộ** ở bản desktop (cùng với Nitro/Cloudflare target). Giữ error boundary, bỏ phần telemetry. |
| T18 | **~50 component shadcn/ui chưa dùng** (carousel, chart, menubar, input-otp, resizable, drawer, pagination…) | Rác | Tree-shake / xoá khi build. |
| T19 | **`recharts`, `date-fns`, `zod`, `react-hook-form`, `embla-carousel`, `vaul`, `cmdk`, `input-otp`** trong dependencies | Phần lớn chưa dùng | Dọn `package.json`. `zod` **nên giữ** để validate response API. |

---

## 4. Các điểm CHỈ TƯƠNG ĐỐI GIỐNG — mismatch phải xử lý trước khi merge

Đây là phần nguy hiểm nhất: cả hai bên **đều có** tính năng, nên nhìn qua tưởng khớp, nhưng **kết quả tính ra khác nhau**.

### 4.1. Mismatch mô hình dữ liệu

| # | Trường | Backend (C#) | Lovable (TS) | Hậu quả |
|---|---|---|---|---|
| M1 | `TotalPlayTime` | **giây** (`int?`) | **phút** (`number`) | Sai 60 lần nếu nối thẳng |
| M2 | `AvgPlayTime` | `int?` = **số giờ** | `AvgLength` = **dải** | Mất dữ liệu gốc → xem B2 |
| M3 | `Excited` | `double?`, sentinel `999` | `number` ép `0..5` | Dữ liệu cũ >5 sẽ bị cắt → xem B3 |
| M4 | `Score` | `double?` | `number \| null` | Lovable `Number()` không kiểm số thực; sentinel 999 thì khớp |
| M5 | `FinishedIn` | `string?` | `number \| null` | Backend parse `double.TryParse`, chuỗi không phải số → loại khỏi filter |
| M6 | `Series` | Entity `{Id, Name}` | `string \| null` | Đổi tên Series ở Lovable chỉ so khớp chuỗi → hỏng khi trùng tên |
| M7 | `Genres`/`Themes` | Entity `{Id, Name, IconData, SortOrder}` | `string[]` | Không có Id ⇒ rename/merge/icon đều gãy |
| M8 | `Progress`/`Ownership` | Bảng động, Id hardcode trong code | 6 literal cứng | Xem B5 |
| M9 | Ảnh | `ImagePath`, `GImagePath`, **`SteamLibraryImagePath`** | `cover`, `gallery` | Thiếu 1 ảnh (A5) |
| M10 | `IsDlc`, `BaseGameId` | Có | Không | Thiếu hẳn (A3) |
| M11 | `Ready` | bool do `LibraryService` đồng bộ | `exeFound` — Lovable set `= !!exeName` khi gõ tay | Sai bản chất: `Ready` là kết quả scan, không phải người dùng nhập |
| M12 | `CreateAt`, `PlayDiary`, `DetectedProcess` | Có | Không | Diary chỉ hiện số giả `128` trong tray panel |
| M13 | `Config` | `GameLibraryPath: List<string>` | `libraries: {id, path, valid, exeCount}[]` | `valid`/`exeCount` là dữ liệu **runtime từ scanner**, không phải config — phải tách |
| M14 | `Config.isDarkMode` | `bool` | `"light"\|"dark"\|"system"\|"jungle"` | Xem T7 |
| M15 | RAWG key | Hardcode trong source | Có trong Settings | Xem B10 |

### 4.2. Mismatch thuật toán (nguy hiểm nhất — sai âm thầm)

| # | Hạng mục | Backend | Lovable | Ghi chú |
|---|---|---|---|---|
| **M16** | **Dải MetaScore** | VeryHigh ≥90 · High 80–89 · Medium 70–79 · **Low 50–69** · **VeryLow 1–49** | ≥90 · ≥80 · ≥70 · **Low ≥60** · **VeryLow <60** | **Ranh giới Low/VeryLow lệch 10 điểm.** Cùng bộ dữ liệu → kết quả filter khác |
| **M17** | **Reverse filter khi chưa bật filter nào** | `if (!hasAnyFilter) return true;` **trước** khi đảo → trả toàn bộ | `return f.reverse ? !base : base` với `base=true` → **trả rỗng** | Lovable cho danh sách trống trong khi backend cho đầy đủ |
| **M18** | **Reverse sort** | Đảo **toàn bộ list sau khi sort** (`_filteredGamesCache.Reverse()`) | Nhân hướng comparator `* dir` | Với sort nhiều khoá, hai cách cho thứ tự **khác nhau** |
| **M19** | **Default sort — All** | `Ready↓, PublishDate↓, Excited↓, Name↑, ProgressId↑, Ownership.SortOrder↑` | `ProgressRank↑, Name↑` | Hoàn toàn khác |
| **M20** | **Default sort — Planning** | `ProgressId↑, Excited↓, AvgPlayTime↑, Name↑` | `ProgressRank↑, Excited↓` | Thiếu 2 khoá phụ |
| **M21** | **Default sort — Finished** | `ProgressId↑, Score↓, FinishedYear↓, PublishDate↓, MetaScore↓` | `finishedIn↓, score↓` | Thiếu 3 khoá phụ, đảo thứ tự ưu tiên |
| **M22** | **Thứ hạng Progress** | Theo `ProgressId` DB (1=Done, 2=Playing, 3=NextPlay, 4=Intended…) → **Done xếp trước Playing** | `Playing 0, NextPlay 1, Intended 2, Pending 3, Done 4, Dropped 5` | Ngược nhau ở vị trí Done |
| **M23** | **Thứ hạng Ownership** | `OwnershipStatus.SortOrder` (dữ liệu, user sửa được) | Hardcode `Ready0, GamePass1, SteamOff2, Pirated3, PSRequired4, NotOwned5` | User đổi thứ tự trong Catalog Settings → Lovable không biết |
| **M24** | **Filter "None" của AvgLength / MetaScore** | Cờ **riêng** `IncludeAvgPlayNone` / `IncludeMetaScoreNone`, OR với danh sách dải | Gộp `"None"` vào cùng mảng chip | Kết quả tương đương, nhưng shape API khác — phải thống nhất |
| **M25** | **Filter Total playtime** | Nhập **giờ** → `× 3600` → so với **giây** | So `Math.round(minutes/60)` với giá trị nhập | Làm tròn khác nhau ở biên |
| **M26** | **Search "Release year"** | `int.Parse` + so **bằng đúng năm** | `publishDate.slice(0,4).includes(term)` | Gõ `02` → Lovable trả 2020/2021/2022…, backend trả rỗng |
| **M27** | **Search chữ cái đầu** | `initials.Contains(term)`, áp cho **cả Name và Series**, cả ở scope `Name`/`Series` | `initials(name).startsWith(term)`, **chỉ Name**, **chỉ scope `all`** | Kết quả hẹp hơn nhiều |

### 4.3. Mismatch hành vi

| # | Hạng mục | Backend | Lovable |
|---|---|---|---|
| M28 | **Finish Game** | Set Score + Progress=Done + FinishedIn=năm, **Save**, rồi `AddGameFinish` (ghi lịch sử), reload | Chỉ patch 4 field trong state, không ghi lịch sử |
| M29 | **Kéo thả Queue** | Ghi lại **cả ProgressId** (top-3 → NextPlay, còn lại → Intended) + PlayOrder, persist DB | Chỉ đánh lại `queueOrder` |
| M30 | **Sync metadata** | RAWG thật: name, publisher, developer, slug, metascore, avgPlaytime, releaseDate, background image; + gallery; + steam library image; + Gemini exe name | `setTimeout` 1.4 s rồi điền hằng số giả (`"Bandai Namco"`, `"FromSoftware"`, `88`, `"2023-05-12"`) |
| M31 | **Upload ảnh** | Khi Save: ảnh chưa nằm trên Cloudinary sẽ được upload vào `MasterPlay/Covers/{GameId}` / `Galleries/{GameId}` với overwrite+invalidate; lỗi upload **không chặn** save | Sinh URL `picsum.photos` giả; nhánh gallery **luôn** toast "thất bại" |
| M32 | **Disable game** | `IsDisabled = true` → `UpdateGame` → ghi DB | Đổi flag trong state |
| M33 | **Xoá taxonomy** | `DataService.DeleteXxx` xử lý lại association ở tầng DB, có transaction | Lọc mảng trong state |
| M34 | **Launch game** | Tra `DetectedProcess` theo `InitExeName ?? ExeName` + `.exe`, `Process.Start` với `UseShellExecute`, cảnh báo localize nếu không thấy | `toast.success("Đang khởi chạy game...")` |
| M35 | **`InitExeName`** | **Tên process** (hiển thị `{tên}.exe`) | Lovable picker gán **đường dẫn** `C:\Games\...\launcher.lnk` → ✅ **giải quyết**: luồng mới `launchTarget {kind: exe\|shortcut, path}`, xem `DOMAIN_RULES.md` §10 |
| M36 | **Ngôn ngữ** | Mã culture `en-US`/`vi-VN`, đổi `CurrentCulture`+`CurrentUICulture`, ghi vào `config.json`, **tray theo dõi config và tự reload sau 500 ms** | `"en"`/`"vi"` trong `localStorage`, tray không biết |
| M37 | **Theme** | `config.json` → tray dùng chung | `localStorage`, chỉ trong web |
| ~~M38~~ | **Ảnh trên card** — ❌ **GHI SAI, ĐÃ ĐÍNH CHÍNH 2026-08-14** | Card **và** Queue đều dùng `GImagePath` (gallery), decode 460px — `ImagePath` (cover) **không dùng ở XAML nào** | Card cũng dùng `gallery`, cùng kích thước 270×126.2 → **KHỚP**, không phải mismatch. Xem `DOMAIN_RULES.md` §12 |
| M39 | **Màu MetaScore chip** | Bảng màu riêng theo dải 90/80/70/50 | `≥75` xanh, `≥50` vàng, còn lại đỏ | 
| M40 | **Refresh xuyên tiến trình** | `WeakReferenceMessenger` + `GlobalEvents` → tray báo UI reload | Không có cơ chế nào |

---

## 5. Chiến lược triển khai

### Nguyên tắc nền

> **1. Backend là nguồn sự thật duy nhất cho nghiệp vụ.**
> Toàn bộ lọc, sắp xếp, phân trang, ngưỡng, thứ hạng, đơn vị **chuyển hết về server**. Frontend gửi query, nhận `{items, totalItems, totalPages, activeFilterCount, cardHints}` và **chỉ render**.
> → Điều này **xoá vĩnh viễn toàn bộ nhóm mismatch M16–M27** thay vì phải đồng bộ hai bản cài đặt mãi mãi.
>
> **2. Lovable không được định nghĩa domain.**
> Types TS được **sinh ra từ OpenAPI của tôi**, commit vào repo Lovable, và **read-only**. CI fail nếu bị sửa tay.
>
> **3. Không xoá WPF cho tới khi qua cổng parity.**

### Giai đoạn 0 — Chốt hợp đồng *(trước mọi dòng code UI)* — ✅ **PHẦN TÀI LIỆU ĐÃ XONG 2026-08-14**

| # | Việc | Trạng thái |
|---|---|---|
| 1 | [`docs/DOMAIN_RULES.md`](docs/DOMAIN_RULES.md) — 18 mục: đơn vị, sentinel, `SystemKey`, ngưỡng MetaScore/AvgLength, quy tắc reverse, quy tắc queue top-3, luồng launch mới, localize | ✅ Xong |
| 2 | [`docs/API_CONTRACT.md`](docs/API_CONTRACT.md) — quy ước, 2 chế độ Local/Hosted, shape lỗi, 8 thay đổi schema | ✅ Xong |
| 3 | [`docs/openapi.yaml`](docs/openapi.yaml) — 31 path, 21 schema, đã validate parse | ✅ Xong |
| 4 | [`AGENTS.md`](AGENTS.md) — ranh giới FE/BE, danh sách cấm, Definition of Done | ✅ Xong |
| 5 | [`docs/PARITY_CHECKLIST.md`](docs/PARITY_CHECKLIST.md) — 96 dòng (25 A + 12 B + 19 T + 40 M) | ✅ Xong |
| 6 | Sinh `types.gen.ts` từ `openapi.yaml` | ⬜ Chờ khởi tạo project FE |
| 7 | **Migration DB** thực tế (8 thay đổi schema S1–S8) | ⬜ Giai đoạn 1 |

**Quyết định đã tự chốt để không chặn tiến độ** (ghi rõ trong `PARITY_CHECKLIST.md`, lật lại lúc nào cũng được):
B2 giữ số giờ · B4 bỏ `FinishedIn`, filter ANY + sort MAX · B5 thêm `SystemKey` · B7 giữ tay cầm + Console mode · B11 thêm màn Restore · B12 giữ 30/trang.

### Giai đoạn 1 — Backend host — ~3–4 tuần (tăng so với ước lượng gốc do Mục 0.1)

> Cập nhật theo Mục 0.1: project **`MasterPlay.Host`** giờ build **1 lần, deploy 2 nơi** — Local (kèm agent trên máy chơi game, đủ endpoint native) và Hosted (phục vụ web, ẩn endpoint native). Cả hai nối cùng **Supabase Postgres** qua `Npgsql`, xác thực bằng JWT của Supabase Auth. Việc migrate schema SQLite→Postgres và viết RLS nằm ở GĐ0, không lặp lại ở đây.

Project mới **`MasterPlay.Host`** (ASP.NET Core Minimal API, `net8.0`), tham chiếu `Core` + `Infrastructure`. **Không đụng `Core`/`Infrastructure`/`TrayApp`** ngoài migration ở GĐ0.

Nhóm endpoint:

| Nhóm | Endpoint |
|---|---|
| Library | `GET /api/games` (tab, q, scope, sort, dir, reverse, các filter, genreIds, themeIds, page, pageSize) → `{items, totalItems, totalPages, activeFilterCount, cardHints}` |
| Game | `GET/POST/PUT /api/games[/{id}]`, `POST /api/games/{id}/disable\|restore\|finish\|promote\|launch\|playtime` |
| Queue | `GET /api/queue`, `PUT /api/queue/order` (áp quy tắc top-3 ở server) |
| Taxonomy | `GET/POST/PUT/DELETE /api/taxonomy/{series\|genres\|themes}` |
| Catalog | `GET/PUT /api/catalog/{progress\|ownership}` (name, icon, sortOrder) |
| Metadata | `POST /api/games/{id}/sync/{metadata\|cover\|gallery\|steam-library}` |
| Settings | `GET/PUT /api/settings`, `POST /api/settings/test-cloudinary` |
| Native | `POST /api/fs/{pick-folder\|pick-file\|reveal}` |
| Tray | `GET /api/tray/status`, `POST /api/tray/{services/{name}\|sync-images\|scan-now\|backup-now\|open-logs\|startup}` |
| Dashboard | `GET /api/dashboard/stats` |
| Events | `GET /api/events` (SSE): running processes, refresh, tray notifications, tiến độ sync |

Bảo mật (B9): `127.0.0.1` only, port ngẫu nhiên ghi `%AppData%\MasterPlay\host.json`, bearer token per-session.

Refactor cần thiết: `DataService` là `static class` — bọc bằng interface + DI để test được; **không viết lại logic**, chỉ bọc.

**Cổng ra GĐ1:** bộ integration test chạy song song WPF-path và API-path trên cùng DB, so khớp `id[]` cho ≥50 tổ hợp filter/sort/tab.

### Giai đoạn 2 — Vỏ desktop — ~1 tuần

`MasterPlay.Shell` (WPF + WebView2): 1 cửa sổ, single-instance, load `http://127.0.0.1:{port}`, inject token, F11 fullscreen, custom title bar, chặn navigation ra ngoài domain.

Bên Lovable: đổi build sang **SPA tĩnh**, bỏ SSR / Nitro / Cloudflare / `src/server.ts` / telemetry (T17). Asset tĩnh do Host phục vụ.

### Giai đoạn 3 — Port UI theo từng màn hình — ~4–6 tuần — 🚧 **ĐÃ BẮT ĐẦU 2026-08-14**

> Project **`MasterPlay.Web`** đã dựng: Vite + React 19 + TS + Tailwind v4 + TanStack Query.
> Type sinh từ `docs/openapi.yaml` (`npm run gen:api`), **không** copy `src/` của Lovable.
> Mock API chạy trong Vite dev server (`mock-api-plugin.ts`) nên UI chạy được khi chưa có backend —
> và `src/mocks/library.ts` chính là **đặc tả chạy được** để đối chiếu ở cổng parity.
>
> Đã xong: shell (tab/search/sort/phân trang/phím tắt), GameCard, Pills, lớp API.
> Xem `MasterPlay.Web/README.md`.

Thứ tự (mỗi màn hình chỉ merge khi API xanh **và** qua checklist parity):

1. Library grid + search + sort + filter + phân trang → **kèm A1, A2, M16–M27**
2. Game detail (view) + launch + folder warning → A9, A10, A11, A21
3. Game editor (add/edit) → A3, A4, A5, A6, A7, A8, A23, M35
4. Play Queue → A12, A13, A14
5. Settings + Catalog/Manage tags → A15, A16, A22, A24, T13
6. Tray panel → T2, T3, T4, B8
7. Dashboard → T1

### Giai đoạn 4 — Cổng parity *(bắt buộc, trước khi xoá bất cứ thứ gì)*

`docs/PARITY_CHECKLIST.md`: mỗi dòng trong Mục 2 và Mục 4 phải ở một trong ba trạng thái — **✅ khớp** / **🔁 đổi có chủ ý (anh duyệt)** / **⏸ hoãn (có issue)**. Không dòng nào được để trống.

Ngoài ra: chạy song song 2 UI trên **cùng DB thật** ít nhất 1 tuần; đối chiếu số lượng kết quả ở 20 tổ hợp filter thường dùng.

### Giai đoạn 5 — Xoá giao diện cũ — ~2 ngày

1. Gỡ `MasterPlay.UI` khỏi `MasterPlaySystem.slnx`, xoá thư mục project.
2. Di chuyển asset còn dùng (`fire.png`, logo, icon) sang `public/` của web; xoá phần còn lại.
3. Cập nhật `run.bat`, publish profiles, `.github/workflows`.
4. Cập nhật `APP_SPEC.md` (đang mô tả bản `fae3fad`, **đã lạc hậu**: thiếu DLC, `GameFinish`, `SteamLibraryImagePath`, `IconData`, Gemini, Catalog Settings, XInput).
5. Tag release cuối của bản WPF để có đường lùi.

**Không đụng tới:** `MasterPlay.Core`, `MasterPlay.Infrastructure`, `MasterPlay.TrayApp`.

---

## 6. Quy tắc làm việc với Lovable (chống Lovable lấn sân backend)

Đưa nguyên khối này vào `AGENTS.md` của repo Lovable:

### Lovable ĐƯỢC làm
- Toàn quyền về layout, component, animation, design token, accessibility, responsive.
- Đề xuất thay đổi API — **qua issue**, không tự sửa.
- Dùng MSW mock để thiết kế, **nhưng mock phải sinh từ `types.gen.ts`**.

### Lovable KHÔNG được làm
| Cấm | Lý do |
|---|---|
| Sửa `src/lib/api/**` (file sinh tự động) | Là hợp đồng, do backend sở hữu — CI chặn |
| Thêm/đổi tên/xoá field domain, hoặc thêm giá trị enum | Enum đến từ DB |
| Cài đặt logic lọc/sort/ngưỡng ở client | Đã chuyển hết về server; `src/lib/filters.ts` **chỉ còn giữ state UI** |
| Đổi đơn vị (giây↔phút↔giờ) | Nguồn gây bug âm thầm |
| Ghi `localStorage` bất cứ thứ gì ngoài preference thuần UI (tab cuối, độ rộng panel) | Ngôn ngữ, theme, settings **thuộc `config.json`** — tray dùng chung |
| Giữ `src/data/games.ts` / `DEMO_GAMES` | Phải xoá ở GĐ3 |
| Thêm SSR, server function, Nitro route, target Cloudflare | Build là SPA tĩnh chạy trong WebView2 |
| Thêm dependency gọi mạng ra ngoài | App chứa credential |

### Định nghĩa "khớp bánh răng" (Definition of Done cho mỗi màn hình)
1. Mọi field UI hiển thị đều **truy vết được** về một field trong `openapi.yaml`.
2. Không còn dữ liệu giả nào trong màn hình đó.
3. Mọi mutation gọi API thật; lỗi API hiển thị đúng, không nuốt.
4. Chuỗi hiển thị lấy từ i18n; giá trị dữ liệu (Progress/Ownership name) lấy `displayName` **từ backend** (A20).
5. Dòng tương ứng trong `PARITY_CHECKLIST.md` đã được đánh dấu.

---

## 7. Việc cần anh quyết ngay để tôi khởi động GĐ0

Cập nhật sau trao đổi ngày 2026-08-14 — các mục đã chốt chuyển xuống bảng "Đã chốt", chỉ còn mục thật sự mở ở bảng trên.

### Đã tự chốt (giả định) — người dùng lật lại lúc nào cũng được

Sau chỉ đạo "triển thôi" (2026-08-14), tôi chốt các mục sau theo đề xuất đã nêu để không chặn tiến độ. **Muốn đổi thì sửa `DOMAIN_RULES.md` + `PARITY_CHECKLIST.md` trước, code sau.**

| # | Quyết định | Chốt theo |
|---|---|---|
| 1 | **Hosted API deploy lên Fly.io** | Free tier đủ cho 1 service nhỏ, deploy bằng Dockerfile, không cần tự vận hành VPS |
| 2 | **v1 bắt buộc online** | Desktop gọi Local API → Supabase. Offline-first (cache + queue sync) đưa vào backlog, không đội GĐ1 |
| 3 | **B5** — thêm `SystemKey`, bỏ toàn bộ magic number | Bắt buộc dù chọn hướng nào — `DOMAIN_RULES.md` §3.1 |
| 4 | **B4** — bỏ `FinishedIn` khỏi UI; filter khớp **ANY**, sort dùng **MAX** | §5.6, §9.3 |
| 5 | **B2** — `avgPlaytimeHours` là nguồn sự thật, backend trả kèm `avgPlaytimeBand` | §1, §5.5 |
| 6 | **B7** — giữ điều hướng tay cầm + Console mode | Gamepad API luôn bật; Shell bridge fullscreen; xem spec 2026-08-29 |
| 7 | **B11/B12** — có màn Restore, giữ 30/trang | `GET /api/games/disabled`; §16 |

### Việc cần người dùng tự làm (tôi không làm thay được)

| # | Việc | Vì sao |
|---|---|---|
| 🔴 1 | **Thu hồi 2 API key đang lộ** (RAWG `RawgService.cs:12`, Gemini `GeminiService.cs:9`) và tạo key mới | Cần đăng nhập tài khoản RAWG/Google — chỉ chủ tài khoản làm được. Key đã nằm trong git history nên xoá khỏi source **không đủ** |
| 🔴 2 | **Tạo project Supabase** + cung cấp connection string / anon key / service role key | Cần tài khoản của người dùng |
| 🟡 3 | Tạo tài khoản Fly.io (nếu chốt theo mục 1 ở trên) | Cần tài khoản |

### Đã chốt (2026-08-14)

| # | Quyết định | Kết luận |
|---|---|---|
| B1 | Vỏ desktop + mô hình host | WebView2 (desktop) + Hosted API song song (web), cùng codebase C#. Data: Supabase (Postgres + Auth). Xem Mục 0.1. |
| B3 | Thang `Excited` | 1–10 nguyên + sentinel 999→`10🔥`. Lovable phải sửa UI (hiện ép 0–5). |
| B6 | Ngôn ngữ mặc định | `en-US` |
| B9 | Bảo mật | JWT bắt buộc cả 2 API, RLS Postgres, mã hoá credential trước khi rời localhost |
| B10 | RAWG/Gemini key | Thu hồi key cũ, tạo mới, chuyển vào config |
| — | Multi-user/auth | Auth thật qua Supabase Auth (không phải single-user bỏ qua đăng nhập) |

---

## Phụ lục — Bản đồ file Lovable

| File | Dòng | Số phận |
|---|---|---|
| `src/data/games.ts` | 251 | **Xoá.** Type chuyển sang `api/types.gen.ts` |
| `src/lib/store.tsx` | 312 | **Viết lại** thành TanStack Query hooks gọi API |
| `src/lib/filters.ts` | 236 | **Rút gọn** — chỉ giữ type state UI; toàn bộ `matchesFilter`/`sortGames`/`metaBand` **xoá** (về server) |
| `src/lib/i18n.tsx` | 513 | Giữ; đồng bộ khoá với `LocalizationService` (467 chuỗi); ngôn ngữ lưu qua API |
| `src/lib/theme.tsx` | 52 | Giữ; lưu qua API thay vì `localStorage` |
| `src/lib/tag-icons.tsx` | 87 | Giữ; icon lấy từ API (`IconData`) |
| `src/components/mp/*.tsx` | 2 761 | **Giữ toàn bộ UI**, thay nguồn dữ liệu |
| `src/components/ui/*.tsx` | ~60 file | Giữ file đang dùng, xoá phần còn lại |
| `src/routes/index.tsx` | 528 | Giữ layout; bỏ tính toán client |
| `src/server.ts`, `src/start.ts`, `lib/error-capture.ts`, `lib/error-page.ts`, `lib/lovable-error-reporting.ts` | 259 | **Xoá** (SSR/telemetry của editor) |
| `vite.config.ts` | 12 | **Viết lại** — SPA tĩnh, bỏ `@lovable.dev/vite-tanstack-config` |
