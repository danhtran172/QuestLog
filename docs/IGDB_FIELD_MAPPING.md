# QuestLog — ĐỐI CHIẾU DỮ LIỆU GAME: App hiện tại ↔ IGDB API v4

| | |
|---|---|
| Phiên bản | 1.0 |
| Ngày | 2026-08-21 |
| Trạng thái | Tài liệu khảo sát. Chưa phải quyết định kiến trúc. |
| Nguồn App | `MasterPlay.Core/Entities/*.cs`, `MasterPlay.Host/Dtos.cs`, `MasterPlay.Host/Program.cs`, `Integrations/Rawg`, `Integrations/HowLongToBeat`, `Integrations/Gemini`, `MasterPlay.Template.db` |
| Nguồn IGDB | `https://api-docs.igdb.com/` (kéo trực tiếp 2026-08-21), `igdb.com/genres`, `igdb.com/themes`, `igdb.com/player_perspectives` |
| Liên quan | [DOMAIN_RULES.md](DOMAIN_RULES.md) §1, §2, §5.5, §12, §13 |

---

## 1. App đang quản lý gì — kiểm kê đầy đủ

### 1.1. Bảng `Game` (thực thể chính)

| # | Cột DB | Kiểu | Ý nghĩa | **Ai điền?** |
|---|---|---|---|---|
| 1 | `Id` | `string` (GUID) | Khoá chính | Hệ thống |
| 2 | `Name` | `string` | Tên game | RAWG / tay |
| 3 | `SlugName` | `string?` | Slug RAWG — **đồng thời dùng dựng URL Metacritic** | RAWG |
| 4 | `ExeName` | `string?` | Tên process để **đếm giờ chơi** | Scanner / Gemini |
| 5 | `InitExeName` | `string?` | Deprecated (DOMAIN_RULES §10.5) | Cũ |
| 6 | `LaunchTargetKind` | `string?` | `exe` \| `shortcut` | Người dùng |
| 7 | `LaunchTargetPath` | `string?` | Đường dẫn tuyệt đối để khởi chạy | Người dùng |
| 8 | `Ready` | `bool` | Đã cài & quét thấy | Scanner |
| 9 | `IsDisabled` | `bool` | Soft-delete | Người dùng |
| 10 | `IsDlc` | `bool` | Là DLC | Người dùng |
| 11 | `BaseGameId` | `string?` | Game gốc của DLC | Người dùng |
| 12 | `SaveFolderPath` | `string?` | Thư mục save | Người dùng |
| 13 | `BackupFolderPath` | `string?` | Thư mục backup | Người dùng |
| 14 | `TotalPlayTimeSeconds` | `int?` | Tổng giờ chơi — **đơn vị GIÂY** | Process monitor |
| 15 | `ImagePath` | `string?` | Ảnh cover (dọc) — hero màn chi tiết | RAWG `background_image` (!) |
| 16 | `GImagePath` | `string?` | Ảnh gallery (ngang 460×215) — card + queue | Steam `header.jpg` (scrape) |
| 17 | `SteamLibraryImagePath` | `string?` | Library capsule Steam | Steam Store API (scrape) |
| 18 | `PublishDate` | `DateOnly?` | Ngày phát hành | RAWG `released` |
| 19 | `Publisher` | `string?` | Nhà phát hành (chuỗi gộp `, `) | RAWG |
| 20 | `Developer` | `string?` | Nhà phát triển (chuỗi gộp `, `) | RAWG |
| 21 | `SeriesId` | `int?` | Dòng game (24 series trong DB) | Người dùng |
| 22 | `Score` | `double?` | **Điểm cá nhân** 0–11 bước 0.5; `11` = Peak Score | Người dùng |
| 23 | `MetaScore` | `int?` | Điểm Metacritic 0–100 | RAWG `metacritic` |
| 24 | `Excited` | `double?` | **Mức háo hức** 0–11 bước 0.5; `11` = Peak Score | Người dùng |
| 25 | `AvgPlayTime` | `int?` | Thời lượng phá đảo — **đơn vị GIỜ** | RAWG `playtime` (HLTB **chưa được nối dây**) |
| 26 | `FinishedIn` | `string?` | Deprecated (DOMAIN_RULES §9.1) | Cũ |
| 27 | `OwnershipId` | `int?` | FK trạng thái sở hữu | Người dùng |
| 28 | `ProgressId` | `int?` | FK tiến độ chơi | Người dùng |
| 29 | `CreateAt` | `DateTime?` | Ngày thêm vào thư viện | DB |

### 1.2. Bảng phụ

| Bảng | Nội dung | Ai điền |
|---|---|---|
| `Genres` (25 mục) | Taxonomy **tự định nghĩa**, có emoji | Người dùng |
| `Themes` (33 mục) | Taxonomy **tự định nghĩa**, có emoji | Người dùng |
| `Series` (24 mục) | Dòng game | Người dùng |
| `ProgressStatus` (6) | `Done, Playing, Nextplay, Intended, Drop, Pending` | Người dùng |
| `OwnershipStatus` (6) | `Ready, Pirated, GamePass, Not Owned, PS Required, Steam Off` | Người dùng |
| `GameFinish` | `{ FinishedAt, Score }` — 1 game hoàn thành nhiều lần | Người dùng |
| `GamePosition` | `PlayOrder` cho Play Queue | Kéo-thả |
| `GameOwnershipInfo` | Tài khoản Steam/Ubisoft, mật khẩu, link tải, ghi chú | Người dùng |
| `PlayDiary` | `{ Date, TimeSpent (phút), Notes }` | Người dùng |
| `DetectedProcess` | Kết quả quét ổ đĩa | Scanner |

### 1.3. Field suy ra (backend tính, không lưu DB)

`MetaScoreBand` · `AvgPlaytimeBand` · `ScoreMaterialBand` · `IsUpcoming` · `IsRunning` · `CanLaunch` · `FolderWarning` · `LastFinishedAt` · `BaseGameName` · `cardHints`

### 1.4. Pipeline lấy dữ liệu hiện tại

`POST /api/games/{id}/sync/metadata` (`Program.cs:509-530`) gọi song song 4 nguồn:

| Nguồn | Lấy được gì | Ghi vào |
|---|---|---|
| **RAWG** `/api/games` | `name`, `slug`, `background_image`, `released`, `metacritic`, `playtime`, genres, developers, publishers | Name, SlugName, ImagePath, PublishDate, MetaScore, AvgPlayTime, Publisher, Developer |
| **Steam** (regex HTML `store.steampowered.com/search`) | `appid` → `header.jpg` + library capsule | GImagePath, SteamLibraryImagePath |
| ~~**HowLongToBeat**~~ | `MainStoryWithExtras` | **KHÔNG NƠI NÀO GỌI** — `HowLongToBeatService.GetPlayTimeAsync` không có call site nào trong toàn repo. Code chết. |
| **Gemini 2.5 Flash** | Đoán tên file `.exe` | ExeName |

#### App lấy đúng những field nào từ RAWG

RAWG detail response có **54 field top-level** (đo thực tế trên `/api/games/3328`). App chỉ deserialize **10** (`RawgData` trong `Integrations/Rawg/Models/ApiDtos.cs`) và chỉ **ghi 8** vào DB.

| RAWG field | → `GameMetadata` | → Cột DB | Có ghi? |
|---|---|---|---|
| `name` | `Name` | `Game.Name` | ✅ |
| `slug` | `SlugName` | `Game.SlugName` | ✅ |
| `background_image` | `Background_Image` | `Game.ImagePath` | ✅ (sai tỉ lệ, xem dưới) |
| `released` | `ReleaseDate` | `Game.PublishDate` | ✅ |
| `metacritic` | `MetaScore` | `Game.MetaScore` | ✅ (`?? 0`) |
| `playtime` | `AvgPlaytime` | `Game.AvgPlayTime` | ✅ (sai ngữ nghĩa, xem §3) |
| `developers[].name` | `Developers` (gộp `, `) | `Game.Developer` | ✅ |
| `publishers[].name` | `Publishers` (gộp `, `) | `Game.Publisher` | ✅ |
| `genres[].name` | `Genres` (gộp `, `) | — | ❌ **không ghi đi đâu cả** |
| `id` | (chỉ để gọi detail) | — | ❌ không lưu |

> **Bốn điểm yếu lộ ra khi khảo sát:**
> 1. `metadata.Genres` được RAWG trả về nhưng **không hề được ghi vào `game.Genres`** — toàn bộ 25 genre + 33 theme đang nhập tay 100%.
> 2. `game.ImagePath` (được mô tả là cover **dọc**) đang nhận `background_image` của RAWG — vốn là ảnh **ngang 16:9**. Sai tỉ lệ ngay từ nguồn.
> 3. Steam appid lấy bằng **regex trên HTML trang search** (`RawgService.cs:160`) — vỡ bất cứ lúc nào Steam đổi markup.
> 4. `RawgService.GetGameFormatedName` và `GetGameCover` **không có call site nào** — code chết, cùng nhóm với `HowLongToBeatService`.
>
> **Đặt tên gây hiểu nhầm:** `GetGalleryImage` và `GetSteamLibraryImage` nằm trong `RawgService` nhưng **không gọi RAWG** — chúng scrape Steam.

---

## 2. IGDB API v4 trả về những gì

### 2.1. Endpoint `/v4/games` — 59 field

| Nhóm | Field |
|---|---|
| **Định danh** | `id`, `name`, `slug`, `url`, `checksum`, `alternative_names[]` |
| **Mô tả** | `summary`, `storyline` |
| **Ngày phát hành** | `first_release_date` (unix), `release_dates[]` (theo từng platform + region, có `date_format` cho ngày mờ như "Q1 2026") |
| **Điểm số** | `rating` (user IGDB), `rating_count`, `aggregated_rating` (giới phê bình), `aggregated_rating_count`, `total_rating`, `total_rating_count`, `hypes` (số follow trước khi ra mắt) |
| **Phân loại** | `genres[]`, `themes[]`, `keywords[]`, `game_modes[]`, `player_perspectives[]`, `tags[]` |
| **Công ty** | `involved_companies[]` → `{ company, developer, publisher, porting, supporting }` |
| **Ảnh & video** | `cover`, `artworks[]`, `screenshots[]`, `videos[]` |
| **Kiểu game** | `game_type` (main_game / dlc_addon / expansion / bundle / standalone_expansion / mod / episode / season / remake / remaster / expanded_game / port / fork / pack / update), `game_status` (released / alpha / beta / early_access / offline / cancelled / rumored / delisted) |
| **Quan hệ game** | `parent_game`, `dlcs[]`, `expansions[]`, `standalone_expansions[]`, `expanded_games[]`, `bundles[]`, `remakes[]`, `remasters[]`, `ports[]`, `forks[]`, `similar_games[]`, `version_parent`, `version_title` |
| **Nhóm / dòng game** | `collections[]`, `franchise`, `franchises[]` |
| **Kỹ thuật** | `platforms[]`, `game_engines[]`, `multiplayer_modes[]`, `language_supports[]`, `game_localizations[]` |
| **Liên kết ngoài** | `websites[]`, `external_games[]` (**Steam appid, GOG, Epic, Itch…**) |
| **Kiểm duyệt** | `age_ratings[]` (ESRB / PEGI + synopsis + content descriptions) |
| **Đồng bộ** | `created_at`, `updated_at` |
| **Deprecated** | `category`→`game_type`, `status`→`game_status`, `collection`→`collections`, `follows` |

### 2.2. Endpoint phụ đáng chú ý

| Endpoint | Field hữu ích |
|---|---|
| `/game_time_to_beat` | `hastily`, `normally`, `completely` (**đơn vị GIÂY**), `count`, `game_id` |
| `/covers`, `/artworks`, `/screenshots` | `image_id`, `width`, `height`, `url` |
| `/external_games` | `uid` (**Steam appid**), `external_game_source`, `platform`, `url` |
| `/involved_companies` | `developer`, `publisher`, `porting`, `supporting` (bool riêng biệt) |
| `/multiplayer_modes` | `campaigncoop`, `onlinecoop`, `offlinecoop`, `lancoop`, `splitscreen`, `dropin`, `onlinemax`, `offlinemax`, `onlinecoopmax`, `offlinecoopmax` |
| `/language_supports` | `language` + `language_support_type` (Audio / Subtitles / Interface) |
| `/popularity_primitives` | PopScore — độ hot theo thời gian thực |
| `/release_dates` | `y`, `m`, `d`, `human`, `platform`, `release_region`, `status` |
| `/age_ratings` | `organization`, `rating_category`, `rating_content_descriptions`, `synopsis` |

### 2.3. URL ảnh IGDB

```
https://images.igdb.com/igdb/image/upload/t_{size}/{image_id}.jpg
```

| size | Kích thước |
|---|---|
| `cover_small` | 90 × 128 |
| `cover_big` | 264 × 374 (`_2x` → 528 × 748) |
| `screenshot_med` | 569 × 320 |
| `screenshot_big` | 889 × 500 |
| `screenshot_huge` / `720p` | 1280 × 720 |
| `1080p` | 1920 × 1080 |
| `thumb` / `micro` | 90 × 90 / 35 × 35 |

---

## 3. BẢNG ĐỐI CHIẾU CHÍNH

Ký hiệu: ✅ khớp trực tiếp · 🟡 khớp một phần / cần chuyển đổi · ❌ IGDB không có · 🔒 dữ liệu cá nhân, không API nào có

| Field App | IGDB tương ứng | Mức | Ghi chú bắt buộc đọc |
|---|---|---|---|
| `Name` | `name` | ✅ | Thêm `alternative_names[]` để khớp tên khi tìm kiếm |
| `SlugName` | `slug` | 🟡 | **CẢNH BÁO:** slug IGDB ≠ slug Metacritic. `Program.cs:233` và `GameEditorViewModel.cs:1065` đang dùng `SlugName` để dựng URL Metacritic. Ghi đè bằng slug IGDB sẽ **làm hỏng nút Metacritic**. Cần cột `IgdbSlug` riêng. |
| `PublishDate` | `first_release_date` (unix) | ✅ | Tốt hơn RAWG: `release_dates[]` tách theo platform, `date_format` xử lý được "Q1 2026" thay vì phải bỏ trống |
| `Developer` | `involved_companies` where `developer == true` → `company.name` | ✅ | Hơn hẳn RAWG: tách bạch developer / publisher / porting / supporting, không gộp nhầm |
| `Publisher` | `involved_companies` where `publisher == true` | ✅ | như trên |
| `MetaScore` | `aggregated_rating` (0–100 double) | 🟡 | **ĐIỂM PHÁ VỠ LỚN NHẤT.** IGDB **KHÔNG** trả điểm Metacritic. `aggregated_rating` là điểm tổng hợp **của riêng IGDB**, cũng thang 0–100 nhưng **giá trị khác Metacritic**. Ngưỡng band ở DOMAIN_RULES §5.5 đang hiệu chỉnh theo Metacritic; 96 game hiện có cũng là Metacritic. Trộn hai nguồn ⇒ dữ liệu không so sánh được với nhau. |
| `AvgPlayTime` (giờ) | `game_time_to_beat.normally` (**giây**) | ✅ | **Đây là nâng cấp ngữ nghĩa, không phải thay nguồn ngang bằng.** RAWG `playtime` = *thời gian trung bình người chơi bỏ ra*, KHÔNG phải thời lượng phá đảo (Witcher 3: RAWG = **43h**, HLTB main+extras ≈ **103h**). IGDB `normally` mới đúng nghĩa "phá đảo có làm phụ". **Phải chia 3600.** Được thêm `hastily` (main story) và `completely` (100%). Lưu ý: không phải game nào cũng có (`count` = số lượt submit). ⚠️ Đổi nguồn sẽ **dịch chuyển mạnh** giá trị của 96 game hiện có và kéo theo `AvgPlaytimeBand` (DOMAIN_RULES §5.5) — Witcher 3 nhảy từ `Medium` sang `VeryLong`. |
| `ImagePath` (cover dọc) | `cover.image_id` → `t_cover_big` (264×374) | ✅ | **Nâng cấp thật sự.** Hiện đang nhận nhầm ảnh ngang từ RAWG; IGDB cho ảnh dọc đúng tỉ lệ, có `t_cover_big_2x` (528×748) cho màn retina |
| `GImagePath` (gallery ngang 460×215) | `artworks[]` / `screenshots[]` → `t_720p` | 🟡 | IGDB **không có** ảnh tỉ lệ Steam header (460×215 ≈ 2.14:1). Artwork/screenshot là 16:9 (1.78:1). Muốn giữ đúng khung card 270×126.2 (DOMAIN_RULES §12) thì hoặc crop, hoặc **giữ nguồn Steam header hiện tại** |
| `SteamLibraryImagePath` | `external_games[]` where source = Steam → `uid` = **appid** | ✅ | **Thắng lớn.** Lấy được appid chính xác từ API, **xoá bỏ hoàn toàn việc regex HTML trang search Steam**. Ảnh vẫn lấy từ CDN Steam như cũ, chỉ thay cách tìm appid |
| `Series` | `collections[]` (hoặc `franchises[]`) | 🟡 | 24 series thủ công ("God of War", "The Witcher"…) map khá tốt sang `collection`. Nhưng IGDB tách `collection` (series) vs `franchise` (thương hiệu lớn) — cần chọn một và chấp nhận sai lệch ở vài trường hợp |
| `IsDlc` | `game_type` ∈ `{dlc_addon, expansion, standalone_expansion, pack, episode, season}` | ✅ | Tự nhận diện được, khỏi tick tay |
| `BaseGameId` | `parent_game` | ✅ | Cần bảng ánh xạ `IgdbId → Game.Id` nội bộ. Có thêm `dlcs[]` để đi ngược chiều |
| `IsUpcoming` (suy ra) | `game_status` | 🟡 | Hiện suy từ `PublishDate` > hôm nay. `game_status` mạnh hơn: phân biệt `early_access`, `cancelled`, `rumored`, `delisted` |
| `Genres` (25 mục) | `genres[]` + `themes[]` + `game_modes[]` + `keywords[]` | 🟡 | Xem §4 — trục phân loại của app **lệch 90°** so với IGDB |
| `Themes` (33 mục) | `keywords[]` (chủ yếu) | 🟡 | Xem §4 — chỉ 5/33 có nguồn chuẩn |
| `Score` | — | 🔒 | Điểm cá nhân |
| `Excited` | (`hypes` chỉ để **gợi ý giá trị khởi tạo**) | 🔒 | `hypes` = số follow trước ra mắt, không phải cảm xúc của người dùng |
| `Progress` / `Ownership` | — | 🔒 | Trạng thái cá nhân |
| `TotalPlayTimeSeconds` | — | 🔒 | Do process monitor đo |
| `GameFinish[]` | — | 🔒 | Lịch sử phá đảo cá nhân |
| `PlayDiary` | — | 🔒 | Nhật ký cá nhân |
| `GamePosition.PlayOrder` | — | 🔒 | Thứ tự queue cá nhân |
| `GameOwnershipInfo` | — | 🔒 | Tài khoản / mật khẩu / link tải |
| `ExeName`, `LaunchTarget*`, `SaveFolderPath`, `BackupFolderPath`, `Ready` | — | ❌ | Thuộc về máy local, không nguồn nào có |
| `CreateAt`, `IsDisabled` | — | ❌ | Cục bộ |

### Tổng kết mức phủ

| | Số field |
|---|---|
| ✅ IGDB phủ trực tiếp, tốt hơn nguồn hiện tại | **9** |
| 🟡 Phủ một phần / phải chuyển đổi / có bẫy | **7** |
| 🔒 Dữ liệu cá nhân — không API nào thay được | **9** |
| ❌ Cục bộ máy tính | **7** |

---

## 4. Đối chiếu TAXONOMY — phần lệch nhiều nhất

### 4.1. Vấn đề gốc: hai trục phân loại lệch nhau

| | App QuestLog | IGDB |
|---|---|---|
| **Genre** nghĩa là gì | Lối chơi **+ cấu trúc màn chơi** (Open World, Linear, Story-driven, Semi-Open World) | Thuần thể loại lối chơi |
| **Theme** nghĩa là gì | **Bối cảnh / thời đại / địa điểm** (Feudal Japan, Egypt, Medieval, Cyberpunk) | **Tông màu / không khí** (Action, Horror, Comedy, Drama, Sandbox) |

Hệ quả: `Action`, `Stealth`, `Survival`, `Open World` — app xếp vào **Genre**, IGDB xếp vào **Theme**. Không thể map 1-1 theo tên field, phải map theo **từng giá trị**.

### 4.2. IGDB Genres — 23 mục (nguồn `igdb.com/genres`)

`Point-and-click` · `Fighting` · `Shooter` · `Music` · `Platform` · `Puzzle` · `Racing` · `Real Time Strategy (RTS)` · `Role-playing (RPG)` · `Simulator` · `Sport` · `Strategy` · `Turn-based strategy (TBS)` · `Tactical` · `Hack and slash/Beat 'em up` · `Quiz/Trivia` · `Pinball` · `Adventure` · `Indie` · `Arcade` · `Visual Novel` · `Card & Board Game` · `MOBA`

### 4.3. IGDB Themes — 22 mục (nguồn `igdb.com/themes`)

`Action` · `Fantasy` · `Science fiction` · `Horror` · `Thriller` · `Survival` · `Historical` · `Stealth` · `Comedy` · `Business` · `Drama` · `Non-fiction` · `Sandbox` · `Educational` · `Kids` · `Open world` · `Warfare` · `Party` · `4X` · `Erotic` · `Mystery` · `Romance`

### 4.4. IGDB Player Perspectives — 7 mục

`First person` · `Third person` · `Bird view/Isometric` · `Side view` · `Text` · `Auditory` · `Virtual Reality`

### 4.5. Map 25 Genre của app → IGDB

| Genre app | Nguồn IGDB | Field IGDB |
|---|---|---|
| 🧩 Puzzle | `Puzzle` | `genres` ✅ |
| 🧭 Adventure | `Adventure` | `genres` ✅ |
| 🏹 RPG | `Role-playing (RPG)` | `genres` ✅ |
| 🔫 Shooter | `Shooter` | `genres` ✅ |
| 🚩 RTS | `Real Time Strategy (RTS)` | `genres` ✅ |
| ♟️ Turn-based | `Turn-based strategy (TBS)` | `genres` ✅ |
| 🏡 Simulation | `Simulator` | `genres` ✅ |
| 🎯 Platformer | `Platform` | `genres` ✅ |
| 🃏 Card & Board | `Card & Board Game` | `genres` ✅ |
| 🏎️ Racing | `Racing` | `genres` ✅ |
| ⚽ Sports | `Sport` | `genres` ✅ |
| 💥 Hash & Slash | `Hack and slash/Beat 'em up` | `genres` ✅ *(tên trong DB đang gõ nhầm "Hash")* |
| 🗡️ Action | `Action` | **`themes`** 🟡 |
| 🥷 Stealth | `Stealth` | **`themes`** 🟡 |
| 🔨 Survival | `Survival` | **`themes`** 🟡 |
| 🌍 Open World | `Open world` | **`themes`** 🟡 |
| 👬 Co-op | `Co-operative` | **`game_modes`** 🟡 — chính xác hơn nữa: `multiplayer_modes.campaigncoop / onlinecoop` |
| ⚔️ Soulslike | keyword `soulslike` | `keywords` (khớp chuỗi, không đảm bảo) |
| 🎲 Roguelike | keyword `roguelike` / `roguelite` | `keywords` |
| 🛖 Base-Building | keyword `base building` | `keywords` |
| 📔 Story-driven | keyword `story rich` | `keywords` |
| 👊 Freeflow | keyword `freeflow combat` | `keywords` (hiếm) |
| 💀 Soulborn | — | ❌ khái niệm riêng của bạn |
| 🗺️ Semi-Open World | — | ❌ IGDB chỉ có nhị phân Open world |
| 📏 Linear | — | ❌ IGDB không gắn nhãn ngược lại |

**Kết quả: 12 tự động chuẩn · 5 tự động nhưng nằm ở field khác · 5 đoán qua keyword · 3 buộc nhập tay.**

### 4.6. Map 33 Theme của app → IGDB

| Nhóm | Theme app | Nguồn |
|---|---|---|
| **Có nguồn chuẩn (5)** | 🧙 Fantasy → `Fantasy` · 👽 Sci-fi → `Science fiction` · 🧛 Horror → `Horror` · ⓘ Historical → `Historical` · 🪖 Military → `Warfare` | `themes` ✅ |
| **Đoán qua `keywords` (20)** | Cyberpunk, Post-Apocalyptic, Space, Feudal Japan, Egypt, Greece, Nordic, Pirate, Medieval, Steam Punk, Super Hero, Super Power, Mythology, Gothic, Renaissance, Ocean, China, Middle East, Far West, Ancient | `keywords` — chuỗi tự do, cần bảng từ điển tự dựng + duyệt tay |
| **Không nguồn (8)** | Modern, Near Future, Far Future, Urban, Dark, Micro World, Amazon, Early modern | ❌ hoàn toàn thủ công |

> **Kết luận taxonomy:** IGDB **không thay thế được** bộ Genre/Theme của bạn. Nó chỉ **gợi ý ban đầu** cho ~17/25 genre và ~5/33 theme. Bộ taxonomy hiện tại mang tính biên tập cá nhân — đây là giá trị riêng, không nên đánh đổi để lấy dữ liệu tự động.

---

## 5. IGDB có mà app CHƯA có — cơ hội

| Dữ liệu IGDB | Dùng được vào việc gì |
|---|---|
| `summary` / `storyline` | Mô tả game ở màn chi tiết — app hiện **không có ô mô tả nào** |
| `external_games[].uid` | Steam appid chuẩn ⇒ **bỏ được toàn bộ scrape HTML Steam** |
| `game_type` + `parent_game` + `dlcs[]` | Tự động phát hiện DLC và tự nối vào base game (thay vì tick tay) |
| `game_status` | Phân biệt `early_access` / `cancelled` / `delisted` — hiện `IsUpcoming` chỉ so ngày |
| `game_time_to_beat.hastily / completely` | Thêm 2 dải thời lượng bên cạnh `normally` |
| `multiplayer_modes` | Cờ chính xác `campaigncoop`, `splitscreen`, `onlinecoopmax` — thay cho genre "Co-op" mơ hồ |
| `player_perspectives` | Trục lọc mới: FPS / TPS / isometric |
| `language_supports` | Game này có phụ đề / giao diện tiếng Việt không |
| `age_ratings` | ESRB / PEGI |
| `game_engines` | Unreal / Unity / RE Engine |
| `similar_games[]` | Gợi ý "chơi tiếp gì" |
| `remakes` / `remasters` / `ports` | Nối các phiên bản của cùng một game |
| `websites[]` | Link chính thức, Steam, GOG, Epic |
| `videos[]` | Trailer YouTube |
| `screenshots[]` / `artworks[]` | Ảnh nền cho màn chi tiết |
| `popularity_primitives` (PopScore) | Độ hot thời gian thực |
| `updated_at` | **Đồng bộ tăng dần** — chỉ kéo lại game đã đổi |
| `rating` / `total_rating` + count | Điểm cộng đồng, bổ sung cho `aggregated_rating` |

---

## 6. Ràng buộc kỹ thuật khi tích hợp

| Mục | RAWG (hiện tại) | IGDB |
|---|---|---|
| Xác thực | 1 API key trên query string | **Twitch Client-ID + OAuth2 bearer** (`client_credentials`, token ~60 ngày, phải tự refresh) |
| Giao thức | `GET`, query string | **`POST`, body viết bằng APIcalypse** (`fields …; where …; limit …;`) |
| Rate limit | ~20.000 req/tháng | **4 req/giây, tối đa 8 request mở đồng thời** |
| Phân trang | `page_size` | `limit` mặc định 10, **tối đa 500** |
| Lấy hàng loạt | 1 game / request | `where id = (1,2,3,…);` — **kéo cả 96 game trong 1 request** |
| Gộp truy vấn | không | endpoint `/multiquery` |
| Quan hệ lồng nhau | phải gọi nhiều lần | **Expander**: `fields cover.*, involved_companies.company.name;` — 1 request lấy hết |
| Ảnh | URL trực tiếp | Ghép từ `image_id`, chọn size. **Ảnh bị gỡ chỉ tồn tại 30 ngày** ⇒ phải cache (app đã có Cloudinary, ổn) |
| Metacritic | ✅ có | ❌ **không có** |

### 6.1. Trạng thái tích hợp

| # | Việc | Trạng thái |
|---|---|---|
| 1 | Thêm cột `IgdbId` (int?) vào `Game` làm khoá ngoại ổn định để re-sync | ✅ Đã làm — `Game.IgdbId`, `DataService.EnsureColumn` |
| 2 | Không ghi đè `SlugName` (đang gánh URL Metacritic) | ✅ IGDB không đụng vào cột này; RAWG vẫn ghi |
| 3 | Quyết định số phận `MetaScore` | ✅ Giữ RAWG riêng cho field này, không trộn nguồn |
| 4 | Đưa API key ra khỏi source | ⚠️ **Chưa** — `RawgService.cs:13` và `GeminiService.cs:8` vẫn hardcode. IGDB thì đã dùng env var / config, không hardcode |

### 6.2. Cấu hình IGDB

Tạo app tại <https://dev.twitch.tv/console/apps> để lấy Client ID + Client Secret, rồi đặt theo **một trong hai** cách (biến môi trường được ưu tiên):

```bash
setx IGDB_CLIENT_ID "your-client-id"
```

hoặc thêm vào `%APPDATA%\MasterPlay\config.json`:

```json
{ "IgdbClientId": "...", "IgdbClientSecret": "..." }
```

Thiếu credential → endpoint sync trả `502 IGDB_NOT_CONFIGURED`, WPF hiện thông báo `IgdbNotConfigured`.

---

## 7. Kết luận

**IGDB thắng RAWG ở:** cover đúng tỉ lệ dọc · tách developer/publisher chuẩn · Steam appid chính thức · thời lượng phá đảo **đúng ngữ nghĩa** (RAWG `playtime` đang sai nghĩa) · quan hệ DLC/base game · và cả một kho dữ liệu app chưa dùng (summary, ngôn ngữ, engine, multiplayer, age rating).

**IGDB thua ở đúng một chỗ, nhưng chỗ đó đau:** **không có điểm Metacritic**. Mà `MetaScore` là một trong 4 trục lọc số của app và đã có 96 game dữ liệu theo thang Metacritic.

**Hướng khuyến nghị: dùng song song, không thay thế.**

| Field | Nguồn nên dùng |
|---|---|
| Name, Developer, Publisher, PublishDate, ImagePath (cover), Series, IsDlc, BaseGameId, summary | **IGDB** |
| AvgPlayTime | **IGDB** `game_time_to_beat.normally` → HLTB làm dự phòng (nhớ: đổi nguồn = đổi ngữ nghĩa, phải backfill lại cả 96 game) |
| SteamLibraryImagePath, GImagePath | **IGDB** lấy appid → CDN Steam lấy ảnh |
| **MetaScore** | **Giữ RAWG** (hoặc thêm cột `AggregatedRating` song song) |
| Genres, Themes | **Giữ thủ công**, IGDB chỉ gợi ý ban đầu |
| ExeName | Giữ Gemini |
