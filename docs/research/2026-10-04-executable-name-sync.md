# Nghiên cứu: đồng bộ tên file executable cho game

**Phạm vi.** Báo cáo chỉ đọc source code và tài liệu thuộc repo. Không có thay đổi
nghiệp vụ. `ExeName` bên dưới có nghĩa là tên process để theo dõi thời gian chơi,
không phải đường dẫn để chạy game.

## Kết luận

Không nên để IGDB crawler ghi thẳng `Game.ExeName`. Crawler chạy trong Supabase,
không nhìn thấy ổ đĩa hoặc bản cài đặt của người dùng; một game có thể có nhiều
file `.exe`, mỗi launcher/store/bản phát hành có thể khác nhau. Hướng phù hợp là
**hai pha**:

1. Crawler chỉ đồng bộ identity dùng chung và có thể kiểm chứng (trước mắt là
   `steam_app_id` mà crawler đã đọc từ IGDB nhưng hiện đang bỏ đi).
2. Scanner trên máy người dùng dùng identity đó để tìm thư mục cài đặt và các
   executable thực có; chỉ sau khi đã xác nhận local mới ghi binding/exe process
   cho library game của đúng người dùng. Trường hợp nhiều ứng viên phải yêu cầu
   người dùng chọn, không dùng một heuristic âm thầm.

Gemini hiện chỉ phù hợp làm **candidate gợi ý**, không phải nguồn ghi đè. Nếu vẫn
muốn enrich từ crawler, tách nó thành một worker/queue có ngân sách riêng và lưu
candidate có provenance/confidence; không nhét Gemini vào IGDB crawler hiện tại.

## Sự thật hiện tại

### 1. `exeName` là runtime/local data, không phải metadata catalog

- Domain contract định nghĩa `exeName` là tên process để tracking, do
  `LibraryService` scanner đồng bộ; `launchTarget` là đường dẫn người dùng chọn
  riêng cho launch ([DOMAIN_RULES.md:449-460](../DOMAIN_RULES.md#L449)). API cũng
  xác nhận `exeName` không dùng để launch ([API_CONTRACT.md:209-216](../API_CONTRACT.md#L209)).
- Bảng catalog Supabase `metadata_games` không có cột executable; schema chỉ giữ
  metadata chia sẻ như `igdb_id`, tên, mô tả, ngày phát hành, ảnh và revision
  ([202608240002_metadata_catalog.sql:10-44](../../supabase/migrations/202608240002_metadata_catalog.sql#L10)).
  Tài liệu mapping phân loại `ExeName`, `LaunchTarget*` và `Ready` là thuộc máy
  local, không có nguồn metadata bên ngoài ([IGDB_FIELD_MAPPING.md:186-192](../IGDB_FIELD_MAPPING.md#L186)).
- Play-time service thực sự gọi `Process.GetProcessesByName` với filename đã bỏ
  extension ([PlayTimeService.cs:180-201](../../MasterPlay.TrayApp/Services/PlayTimeService.cs#L180),
  [PlayTimeService.cs:309-312](../../MasterPlay.TrayApp/Services/PlayTimeService.cs#L309)).
  Chọn sai executable vì thế làm tracking sai, không chỉ ảnh hưởng UI.

### 2. IGDB crawler hiện không có nguồn hay đường ghi `exeName`

- Crawler design đã chốt rõ phạm vi: tối đa 100 catalog record / run, và **không
  RAWG, Steam, Gemini hoặc image upload** ([2026-10-02-igdb-crawler-design.md:5-9](../superpowers/specs/2026-10-02-igdb-crawler-design.md#L5)).
- Query IGDB của Edge Function chỉ lấy canonical metadata + external game identity;
  không có trường executable ([core.ts:50-54](../../supabase/functions/igdb-crawler/core.ts#L50)).
  Hàm normalize tìm Steam app id trong `external_games`, nhưng chỉ dùng nó để tạo
  `banner_url` rồi bỏ giá trị numeric đi ([core.ts:89-105](../../supabase/functions/igdb-crawler/core.ts#L89),
  [core.ts:127-140](../../supabase/functions/igdb-crawler/core.ts#L127)).
- `crawler.apply_game` chỉ merge các canonical fields. Danh sách merge và câu
  `UPDATE metadata_games` đều không có executable ([20261004063737_crawler_steam_cdn_enrichment.sql:19-28](../../supabase/migrations/20261004063737_crawler_steam_cdn_enrichment.sql#L19),
  [20261004063737_crawler_steam_cdn_enrichment.sql:47-64](../../supabase/migrations/20261004063737_crawler_steam_cdn_enrichment.sql#L47)).

### 3. Scanner đã có nền tảng, nhưng chưa tạo được mapping game ↔ executable

- `LibraryService` quét đệ quy các configured library, tạo `DetectedProcess` từ
  `Path.GetFileName(path)` và `FullPath`, sau đó đồng bộ snapshot và `Ready`
  ([LibraryService.cs:272-327](../../MasterPlay.TrayApp/Services/LibraryService.cs#L272),
  [LibraryService.cs:532-549](../../MasterPlay.TrayApp/Services/LibraryService.cs#L532)).
- Nó chỉ nhận diện được game đã có khi tên file bằng `ExeName`/legacy
  `InitExeName` sau normalize ([LibraryService.cs:427-438](../../MasterPlay.TrayApp/Services/LibraryService.cs#L427)).
  Với file mới, code dự kiến gọi `IGameMetadataResolver` bằng full path
  ([LibraryService.cs:350-395](../../MasterPlay.TrayApp/Services/LibraryService.cs#L350)),
  nhưng Tray đăng ký `NullGameMetadataResolver` ([Program.cs:89-101](../../MasterPlay.TrayApp/Program.cs#L89));
  implementation luôn trả `null` ([NullGameMetadataResolver.cs:5-12](../../MasterPlay.TrayApp/Services/NullGameMetadataResolver.cs#L5)).
  Vì vậy hiện không có auto-link mới nào từ scanner sang catalog.
- Snapshot `DetectedProcess` hiện chỉ upsert/remove theo full path
  ([DataService.cs:502-532](../../MasterPlay.Infrastructure/Data/Services/DataService.cs#L502)).
  `SyncGameStatus` chỉ so sánh `Game.ExeName` có sẵn với snapshot để đổi `Ready`
  ([DataService.cs:776-824](../../MasterPlay.Infrastructure/Data/Services/DataService.cs#L776));
  nó không suy ra hay cập nhật `ExeName`.
- Ngay cả khi resolver trả một metadata match đã có trong library, code hiện chỉ
  phát notification rồi `continue`, không sửa `ExeName`; `PrepareMetadataGame`
  chỉ gán filename khi tạo game mới
  ([LibraryService.cs:384-395](../../MasterPlay.TrayApp/Services/LibraryService.cs#L384),
  [LibraryService.cs:465-487](../../MasterPlay.TrayApp/Services/LibraryService.cs#L465)).

### 4. Gemini đang là gợi ý best-effort ở metadata preview

- Endpoint preview chạy Gemini song song rồi trả kết quả vào `ExeName`, và không
  ghi database ở bước preview ([Program.cs:2409-2456](../../MasterPlay.Host/Program.cs#L2409)).
- Prompt yêu cầu filename Windows primary, từ chối đoán khi không chắc chắn và loại
  launcher/installer/bootstrap; kết quả được normalize về basename không extension
  ([GeminiService.cs:15-25](../../MasterPlay.Infrastructure/Integrations/Gemini/GeminiService.cs#L15),
  [GeminiService.cs:37-42](../../MasterPlay.Infrastructure/Integrations/Gemini/GeminiService.cs#L37),
  [GeminiService.cs:98-113](../../MasterPlay.Infrastructure/Integrations/Gemini/GeminiService.cs#L98)).
  Đây là useful fallback, nhưng bản thân prompt đã cho thấy nó không thể chứng minh
  file đó tồn tại trên máy người dùng.

## Hướng thiết kế đề xuất

### Pha A — mở rộng identity của catalog, không mở rộng `ExeName`

1. Bổ sung `steam_app_id bigint null` (unique/index khi khác null) vào
   `metadata_games`; add migration và DTO/catalog mapper tương ứng. Nguồn là
   `external_games` Steam ID mà crawler hiện đã xác định. Đây là dữ liệu catalog
   có tính dùng chung, khác với executable cục bộ.
2. Mở rộng `SourceGame`, `fields`, `normalize`, payload baseline/override và
   `crawler.apply_game` để source-owned merge `steam_app_id`. Duy trì mọi budget,
   lease, cursor và idempotency của crawler; không gọi Gemini, không quét file từ
   Edge Function.
3. Nếu sau này cần candidate cross-store, dùng bảng riêng, ví dụ
   `metadata_game_executable_candidates(metadata_game_id, platform, file_name,
   source, confidence, status, observed_at)`. Không thêm candidate vào
   `metadata_games.exeName` và không làm nó thành điều kiện `Ready`.

Lý do chọn Steam ID làm bước đầu: crawler đã có dữ liệu đó và catalog hiện đã dùng
nó an toàn để dựng Steam CDN banner ([core.ts:130-135](../../supabase/functions/igdb-crawler/core.ts#L130)).
Ngược lại, Steam library sync/OAuth hiện còn là explicit non-goal vì cần danh tính
và consent từng người dùng, không phải Steam API key chung
([2026-10-03-admin-integrations-design.md:38-48](../superpowers/specs/2026-10-03-admin-integrations-design.md#L38)).

### Pha B — resolver/scanner local là authority của binding

1. Thay `NullGameMetadataResolver` bằng resolver local (desktop-only). Với mỗi
   Steam library đã được người dùng cho phép: đọc local install manifest để nối
   `steam_app_id` → install directory; chỉ enumerate `.exe` bên trong directory
   đó. Không upload full path hay danh sách ổ đĩa lên Supabase.
2. Resolver trả một *match có candidates*, không trả một filename được coi là đúng
   ngay. Auto-bind chỉ khi tập ứng viên có đúng một phần tử thỏa rule xác định;
   nếu có 0 hoặc >1 thì tạo notification/UI picker để người dùng xác nhận.
3. Khi đã xác nhận, ghi local binding của library game: `ExeName` = normalized
   basename đã tồn tại thực tế; `DetectedProcess.FullPath` vẫn là snapshot local.
   Giữ `LaunchTarget` tách biệt: `.lnk` có thể mở launcher nhưng không nói process
   nào cần tracking. Launch endpoint hiện vẫn có fallback lookup từ `ExeName` sang
   `DetectedProcess` ([Program.cs:2283-2310](../../MasterPlay.Host/Program.cs#L2283)).
4. Khi scan sau đó phát hiện binding biến mất, chỉ đặt `Ready=false`/hiện cảnh báo;
   không xóa selection hoặc thay sang executable “gần giống”. Khi cùng filename
   xuất hiện ở nhiều paths, full path là bằng chứng cài đặt nhưng `ExeName` vẫn chỉ
   là process key nên UI phải yêu cầu chọn path nếu launch cần phân biệt.
5. Đặt provenance cho binding (`scanner-confirmed`, `scanner-auto`, `user`), hoặc
   tối thiểu một cờ user override. Scanner chỉ điền khi trống hoặc thay giá trị mà
   chính scanner từng sở hữu; không được thay lựa chọn user. Đây cũng xử lý gap
   hiện tại: khi resolver match game đã có, thay notification-only branch bằng một
   update có điều kiện/provenance.

### Optional Pha C — Gemini candidate có kiểm soát

Chỉ làm khi Pha B vẫn thiếu coverage. Chạy một enrichment queue riêng cho catalog
records không có candidate, rate-limit và audit riêng; lưu `source=gemini`,
`confidence`/`status=candidate`. Scanner chỉ dùng candidate để *lọc/rank* những
`.exe` thực sự có trong local install directory; candidate không được ghi đè một
binding confirmed. Không đưa call này vào scheduled IGDB crawler, vì sẽ phá scope
đã chốt và tăng chi phí/failure mode cho pipeline discovery.

## Quy tắc chấp nhận và kiểm thử trước khi triển khai

- Không thay đổi `launchTarget` bằng `ExeName`; chỉ `.exe`/`.lnk` absolute path mới
  là launch target theo domain contract ([DOMAIN_RULES.md:463-477](../DOMAIN_RULES.md#L463)).
- Không set `Ready` từ một Gemini/catalog value: `Ready` là kết quả scan
  ([DOMAIN_RULES.md:481-489](../DOMAIN_RULES.md#L481)).
- Chuẩn hóa một lần và dùng chung ở scanner, match, launch fallback, `Ready` và
  playtime: `Game.ExeName` là basename không `.exe`, còn
  `DetectedProcess.ExeName` là filename có `.exe`. Hiện `LibraryService` đã bỏ
  extension khi match ([LibraryService.cs:503-512](../../MasterPlay.TrayApp/Services/LibraryService.cs#L503))
  còn scanner snapshot giữ filename đầy đủ
  ([LibraryService.cs:284-293](../../MasterPlay.TrayApp/Services/LibraryService.cs#L284)).
- Làm rõ OpenAPI trước khi implement: contract hiện cho `exeName` trong import và
  user-data patch ([openapi.yaml:634-645](../openapi.yaml#L634),
  [openapi.yaml:731-743](../openapi.yaml#L731)), trong khi DOMAIN_RULES nói field
  này do scanner ghi. Chọn một authority và sửa contract/backend theo cùng quyết
  định; không để frontend âm thầm ghi đè scanner.
- Test crawler: normalize Steam ID, merge/idempotency, `null`/multi external-game
  cases, revision/audit, và không phát sinh Gemini call.
- Test resolver/scanner: one candidate auto-bind; zero/multiple candidate requires
  no mutation; renamed/missing executable sets not-ready; confirmed manual choice
  survives re-scan; process tracking receives normalized filename.
- Test privacy: crawler payload/audit/API list không chứa full local paths;
  per-user library binding không rò sang catalog hay library khác.

## Quyết định đề nghị

Phê duyệt **Pha A + Pha B** là một feature backend/desktop-scanner riêng. Giữ
Gemini tại preview hiện tại hoặc thêm Pha C sau khi đo coverage. Đây là cách đạt
được “sync exe filename” bằng bằng chứng file local, đồng thời giữ crawler là
catalog metadata worker và không biến dữ liệu cài đặt của một máy thành metadata
dùng chung.
