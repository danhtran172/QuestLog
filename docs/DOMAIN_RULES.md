# QuestLog — DOMAIN RULES (nguồn sự thật nghiệp vụ)

| | |
|---|---|
| Phiên bản | 1.0 |
| Ngày | 2026-08-14 |
| Trạng thái | Chốt cho v1. Mọi thay đổi phải sửa file này **trước**, code sau. |
| Nguồn trích xuất | `MainWindowViewModel.cs`, `GameEditorViewModel.cs`, `GameViewModel.cs`, `FilterOptions.cs`, `FilterPanel.xaml.cs`, `DataService.cs` @ `d9efa03` |

> **Nguyên tắc số 1.** Mọi quy tắc trong file này được cài đặt **duy nhất một lần, ở backend**.
> Frontend không được cài lại bất kỳ ngưỡng, thứ hạng, hay phép so sánh nào dưới đây.
> Đây là cách xoá vĩnh viễn nhóm mismatch M16–M27 (xem `UI_MIGRATION_PLAN.md`), thay vì phải đồng bộ hai bản cài đặt mãi mãi.

---

## 1. Đơn vị đo — bất biến, không được đổi ở bất kỳ tầng nào

| Khái niệm | Đơn vị chuẩn | Tên field trong API | Ghi chú |
|---|---|---|---|
| Tổng thời gian chơi | **giây** | `totalPlaytimeSeconds` | DB: `Game.TotalPlayTimeSeconds`. **Không bao giờ** truyền phút/giờ qua API. |
| Thời lượng trung bình để hoàn thành | **giờ** | `avgPlaytimeHours` | DB: `Game.AvgPlayTime` (int, số giờ). Dải (band) là **suy ra**, không lưu. |
| Thời gian trong PlayDiary | **phút** | `timeSpentMinutes` | DB: `PlayDiary.TimeSpent`. |

**Quy tắc hiển thị:** UI nhận giây, tự format thành `{h}h {m}m`. UI nhập giờ+phút, tự quy đổi ra giây **trước khi** gửi. Không có endpoint nào nhận/trả phút cho tổng playtime.

> ⚠️ Đây là nguồn bug âm thầm số 1 (mismatch M1: backend giây ↔ Lovable phút, sai 60 lần). Tên field bắt buộc mang hậu tố đơn vị để không tái diễn.

---

## 2. Thang giá trị và sentinel

> ✅ **Mục 2.1 và 2.2 đã được ĐỐI CHIẾU VỚI DB THẬT ngày 2026-08-14** (96 game, qua `GET /api/games`).
> Bản nháp trước đó dựa trên suy đoán và **sai ở hai điểm**, đã sửa bên dưới.

### 2.1. `score` (điểm người dùng chấm)
- Kiểu: `number | null` (double).
- Miền thực tế: **0–10, bước 0.5**, cộng sentinel **999**.
  Giá trị quan sát được trong DB: `6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10, 999`.
- ❌ **Bản nháp trước ghi "0–100" — SAI.** Không có giá trị nào vượt 10 ngoài sentinel.
- `0` = **chưa chấm điểm** → hiển thị `—`, không hiển thị số 0.
- **Sentinel `999`** = "điểm tuyệt đối", hiển thị **`10🔥`**.
- API trả nguyên giá trị thật (`999`), **không** trả chuỗi đã format.

#### Chất liệu hiển thị User Score

Backend suy ra `scoreMaterialBand`; frontend chỉ map band sang style, **không tự so sánh ngưỡng**:

| Giá trị `score` | `scoreMaterialBand` |
|---|---|
| `null`, `<= 0` | `null` |
| `> 0` và `< 3` | `Rust` |
| `>= 3` và `< 6` | `Copper` |
| `>= 6` và `< 8` | `Silver` |
| `>= 8` và `< 9` | `Gold` |
| `>= 9` và `< 10` | `Platinum` |
| `10` | `Diamond` |
| sentinel `999` | `Opal` |

Gallery hiển thị sentinel `999` thành `10` với chất liệu Opal, không kèm icon lửa. Các màn hình
khác vẫn dùng quy ước `10🔥` ở trên.

### 2.2. `excited` (mức độ háo hức)
- Kiểu: `number | null` (double).
- Miền thực tế: **1–10, bước 0.5**, cộng sentinel **999**.
  Giá trị quan sát được: `1, 1.5, 2.5, 3, 3.5, 4, 4.5, 5, 5.5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10, 999`.
- ❌ **Bản nháp trước ghi "số nguyên" — SAI.** Có giá trị lẻ `.5`, **cấm làm tròn**.
- `0` = chưa đánh giá → hiển thị `—`.
- **Sentinel `999`** hiển thị **`10🔥`**, cùng quy ước với `score`.
- ❌ **Lovable sai gấp đôi**: vừa ép thang `0..5` (cắt mất nửa trên), vừa dùng số nguyên
  (mất toàn bộ phần `.5`), lại còn bỏ sót sentinel 999.
- **Cách hiển thị đã chốt:** hiện **số** kèm 1 icon lửa, **không** vẽ dãy 10 icon —
  dãy icon rời rạc không biểu diễn được giá trị `.5`.
- ✔ Không cần script migrate — dữ liệu vốn đã ở thang này.

### 2.3. `metaScore`
- Kiểu: `number | null` (int), miền `0–100`.
- Giá trị `null` hoặc `<= 0` được coi là **"None"** (chưa có điểm), xử lý riêng ở filter (xem §5.4).

---

## 3. Thứ hạng danh mục (Progress / Ownership)

### 3.1. `SystemKey` — bắt buộc, thay cho mọi magic number

Hiện tại code hardcode Id (`ProgressId == 1` là Done, `== 2` Playing, `OwnershipId == 6` SteamOff…) trong khi DB lại cho phép người dùng đổi tên/thứ tự. Đây là mâu thuẫn phải xoá.

**Thêm cột `SystemKey` (string, unique, bất biến)** vào `ProgressStatus` và `OwnershipStatus`:

> ✅ **ĐÃ ĐỐI CHIẾU DB THẬT 2026-08-14.** Bảng dưới là giá trị **thực tế đang có**, không phải suy đoán.
> Bản nháp trước ghi `Dropped`/`Intended` và cả bộ Ownership `Ready/Pirated/GamePass/NotOwned/PSRequired/SteamOff`
> — **SAI hoàn toàn**, đã thay bằng dữ liệu thật.

| Bảng | `SystemKey` hợp lệ (từ DB thật) |
|---|---|
| `ProgressStatus` | `Done`, `Playing`, `Nextplay`, `Planned`, `Drop`, `Pending` |
| `OwnershipStatus` | `Owned`, `Shared`, `Subscription`, `Exclusive`, `Picklocked`, `Wishlist` |

> ⚠️ Hệ quả: mọi chỗ code cũ hardcode `OwnershipId == 6` là "SteamOff" hay `== 2` là "Pirated"
> đều **không còn ứng với dữ liệu hiện tại**. Đây là bằng chứng mạnh nhất cho việc phải bỏ magic number
> và chuyển sang `SystemKey`.

Quy tắc:
- **Code chỉ được tham chiếu `SystemKey`**, tuyệt đối không dùng Id số.
- Người dùng chỉ sửa được `Name`, `IconData`, `SortOrder`, `ColorCode`. **Không** sửa/xoá `SystemKey`.
- Backfill khi migrate: map theo Id hiện tại (`1→Done, 2→Playing, 3→NextPlay, 4→Intended`; `2→Pirated, 6→SteamOff`), phần còn lại map theo `Name`.

### 3.2. Thứ hạng dùng để sort
- **Progress**: sort theo `ProgressStatus.SortOrder` (dữ liệu, người dùng sửa được) — **không** theo Id, **không** theo thứ hạng hardcode.
- **Ownership**: sort theo `OwnershipStatus.SortOrder`.
- Giá trị `null` (game chưa gán) luôn xếp **cuối** (`int.MaxValue`).

> ⚠️ Lovable đang hardcode 2 bảng thứ hạng riêng (M22, M23) và cho Done xếp **sau** Playing, ngược với hệ thống hiện tại. Sau khi chuyển sang `SortOrder` từ DB, thứ hạng do dữ liệu quyết định — hai bên không thể lệch nữa.

---

## 4. Phân loại theo chế độ xem (tab)

Dùng `SystemKey`, không dùng Id:

| Tab | Điều kiện |
|---|---|
| `all` | Tất cả game chưa disable |
| `planning` | `progress.systemKey != "Done"` |
| `finished` | `progress.systemKey == "Done"` |
| `queue` | `progress.systemKey ∈ {"Playing", "NextPlay", "Intended"}` |

**Luôn luôn**: chỉ trả game có `IsDisabled == false` (trừ endpoint dành riêng cho màn Restore).

---

## 5. Filter — quy tắc chính xác

### 5.1. Cấu trúc
4 nhóm **so sánh số** (mỗi nhóm có toán tử + giá trị) và 6 nhóm **chip đa chọn**.

Toán tử hợp lệ: `>` `>=` `=` `<=` `<`.

### 5.2. Quy tắc `null` — quan trọng
**Mọi phép so sánh số với giá trị `null` đều trả `false`** (game bị loại).
Cụ thể: `CompareDouble(null, …) = false`, `CompareInt(null, …) = false`.

So sánh số thực dùng epsilon `1e-6`:
```
">"  : value >  target + eps
">=" : value >= target - eps
"="  : |value - target| < eps
"<=" : value <= target + eps
"<"  : value <  target - eps
```
So sánh số nguyên (playtime) dùng phép so sánh thẳng, **không** epsilon.

### 5.3. 4 nhóm so sánh số

| Nhóm | So với field | Đơn vị giá trị nhập | Chuyển đổi |
|---|---|---|---|
| Finished year | năm hoàn thành | năm (int) | xem §5.6 |
| Excited | `excited` | 1–10 / 999 | không |
| Score | `score` | 0–100 / 999 | không |
| Total playtime | `totalPlaytimeSeconds` | **người dùng nhập GIỜ** | `hours × 3600` → so sánh với **giây** |

> ⚠️ Mismatch M25: người dùng nhập giờ, hệ thống nhân 3600 rồi so với giây. Lovable đang chia phút cho 60 rồi làm tròn → lệch ở biên. **API nhận `playtimeSeconds` đã quy đổi sẵn**, UI chịu trách nhiệm nhân 3600.

### 5.4. Nhóm chip: Avg length và MetaScore — có cờ "None" RIÊNG

Hai nhóm này có **cờ boolean riêng** tách khỏi mảng chip:

```
avgPass = true
if (includeAvgNone || selectedAvgBands.length > 0):
    avgPass = false
    if includeAvgNone AND (avgPlaytimeHours == null || avgPlaytimeHours <= 0): avgPass = true
    if !avgPass AND selectedAvgBands.length > 0 AND matchesAvgBand(game): avgPass = true
```
MetaScore áp dụng **đúng công thức tương đương** với `includeMetaNone` / `selectedMetaBands`.

Shape API (đã chốt, không gộp "None" vào mảng chip như Lovable đang làm):
```
avgBands: ("VeryShort"|"Short"|"Medium"|"Long"|"VeryLong")[]
includeAvgNone: boolean
metaBands: ("VeryLow"|"Low"|"Medium"|"High"|"VeryHigh")[]
includeMetaNone: boolean
```

### 5.5. Ngưỡng dải — CHỐT CỨNG

**AvgLength** (đơn vị: giờ):

| Band | Điều kiện |
|---|---|
| `VeryLong` | `avg >= 80` |
| `Long` | `60 <= avg < 80` |
| `Medium` | `35 <= avg < 60` |
| `Short` | `20 <= avg < 35` |
| `VeryShort` | `0 < avg < 20` |
| *(None)* | `avg == null || avg <= 0` |

**MetaScore**:

| Band | Điều kiện |
|---|---|
| `VeryHigh` | `meta >= 90` |
| `High` | `80 <= meta < 90` |
| `Medium` | `70 <= meta < 80` |
| `Low` | **`50 <= meta < 70`** |
| `VeryLow` | **`0 < meta < 50`** |
| *(None)* | `meta == null || meta <= 0` |

> ⚠️ **Mismatch M16 — đã verify trực tiếp trong `MainWindowViewModel.cs:849-872`.** Lovable đang dùng mốc `60` cho ranh giới Low/VeryLow, lệch **10 điểm** so với hệ thống thật. Cùng bộ dữ liệu sẽ ra kết quả filter khác nhau. Bảng trên là bản đúng.

### 5.6. Filter "Finished year" — ✅ chốt theo `GameFinish[]`

- Nguồn dữ liệu: bảng `GameFinish` (một game có thể hoàn thành **nhiều lần**).
- Quy tắc khớp: **khớp nếu CÓ ÍT NHẤT MỘT lần hoàn thành** thoả điều kiện so sánh (`ANY`, không phải "lần gần nhất").
  *Lý do: người dùng hỏi "game nào tôi đã hoàn thành trong 2023" — một game hoàn thành 2021 và 2023 phải xuất hiện.*
- Game không có bản ghi `GameFinish` nào → **không khớp** (nhất quán với quy tắc `null` ở §5.2).
- Cột cũ `Game.FinishedIn` (string): **không dùng cho filter nữa**, giữ lại trong DB chỉ để backfill. Xem §9.

### 5.7. Filter Genre / Theme — quan hệ OR

```
if (selectedGenreIds.length > 0 || selectedThemeIds.length > 0):
    matches &&= (game có genre ∈ selectedGenreIds) OR (game có theme ∈ selectedThemeIds)
```
> Đây là **OR giữa hai nhóm**, không phải AND. Trích từ `MainWindowViewModel.cs:756-761`.

### 5.8. Progress / Ownership chip — quan hệ AND giữa nhóm
```
if selectedProgressKeys.length > 0: matches &&= selectedProgressKeys.includes(game.progress.systemKey)
if selectedOwnershipKeys.length > 0: matches &&= selectedOwnershipKeys.includes(game.ownership.systemKey)
```

### 5.9. Reverse filter — quy tắc thứ tự CỰC KỲ QUAN TRỌNG

```
hasAnyFilter = (bất kỳ nhóm nào ở §5.3–5.8 đang bật)

if (!hasAnyFilter) return true      // ← KIỂM TRA TRƯỚC, không đảo
… tính matches …
if (reverse) matches = !matches
return matches
```

> ⚠️ **Mismatch M17.** Khi **chưa bật filter nào** mà `reverse = true`:
> - Hệ thống hiện tại → trả **toàn bộ** danh sách (vì thoát sớm ở dòng `if (!hasAnyFilter) return true`).
> - Lovable → trả **rỗng** (vì đảo `base = true` thành `false`).
>
> Bản đúng là **trả toàn bộ**. Việc `hasAnyFilter` được kiểm tra **trước** khi đảo là bắt buộc.

### 5.10. `activeFilterCount`
Backend đếm và trả về số nhóm filter đang bật (để UI vẽ badge). Không đếm ở client.

---

## 6. Search

### 6.1. Chung
- Debounce **280 ms** (giữ nguyên, Lovable đã đúng).
- So khớp **không phân biệt hoa thường**, `trim()` trước khi so.
- **Tắt hoàn toàn khi ở tab `queue`.**

### 6.2. Thuật toán "initials" (viết tắt chữ cái đầu)
```
tách text theo các ký tự phân cách:  ' '  '-'  '_'  '.'   (bỏ phần tử rỗng)
initials = ghép chữ cái đầu của mỗi từ, viết thường
khớp khi:  initials.CONTAINS(searchTerm)        // ← contains, KHÔNG phải startsWith
```
Ví dụ: `"God of War"` → `initials = "gow"`; gõ `ow` → **khớp** (vì contains).

> ⚠️ **Mismatch M27.** Lovable dùng `startsWith` và chỉ áp cho Name ở scope `all` → kết quả hẹp hơn nhiều. Bản đúng là `contains` và áp theo bảng §6.3.

### 6.3. Phạm vi search — áp dụng chính xác

| Scope | Khớp trên |
|---|---|
| `all` | `name` contains · `series.name` contains · **bất kỳ genre nào** contains · **bất kỳ theme nào** contains · initials(`name`) · initials(`series.name`) |
| `name` | `name` contains · initials(`name`) |
| `series` | `series.name` contains · initials(`series.name`) |
| `publisher` | `publisher` contains · initials(`publisher`) |
| `developer` | `developer` contains · initials(`developer`) |
| `releaseYear` | xem §6.4 |

### 6.4. Search theo năm phát hành
```
if (!int.TryParse(term)) return EMPTY      // không parse được → trả RỖNG, không phải trả tất cả
return games.where(publishDate != null && publishDate.Year == parsedYear)
```
> ⚠️ **Mismatch M26.** So sánh **bằng đúng năm**, không phải `includes`. Lovable dùng `publishDate.slice(0,4).includes(term)` nên gõ `02` sẽ trả về cả 2020, 2021, 2022… Bản đúng: gõ `02` → **rỗng**.

---

## 7. Sort

### 7.1. Danh sách khoá sort — đúng 12 mục, đúng thứ tự

| Index | Khoá | Hành vi |
|---|---|---|
| 0 | `default` | phụ thuộc tab — xem §7.2 |
| 1 | `status` | `ready↓, publishDate↓, name↑` |
| 2 | `name` | `name↑` |
| 3 | `playTime` | `totalPlaytimeSeconds↓` |
| 4 | `publish` | `publishDate↓` |
| 5 | `finishTime` | `MAX(GameFinish.FinishedAt)↓`, chưa hoàn thành → cuối, cùng thời điểm → `name↑` |
| 6 | `metaScore` | `metaScore↓` |
| 7 | `excited` | `excited↓` |
| 8 | `score` | `score↓` |
| 9 | `ownership` | `ownership.sortOrder↑` (null → cuối) |
| 10 | `progress` | `progress.sortOrder↑` (null → cuối) |
| 11 | `avgPlaytime` | `avgPlaytimeHours↓` |

### 7.2. Default sort theo tab — CHỐT ĐẦY ĐỦ MỌI KHOÁ PHỤ

| Tab | Thứ tự khoá |
|---|---|
| `all` | `ready↓` → `publishDate↓` → `excited↓` → `name↑` → `progress.sortOrder↑` → `ownership.sortOrder↑` |
| `planning` | `progress.sortOrder↑` → `excited↓` → `avgPlaytimeHours↑` (null→cuối) → `name↑` |
| `finished` | `progress.sortOrder↑` → `score↓` → `finishTime↓` → `publishDate↓` → `metaScore↓` |
| `queue` | xem §8.1 |

> ⚠️ **Mismatch M19–M21.** Lovable chỉ cài 2 khoá cho mỗi tab và sai thứ tự ưu tiên. Bảng trên là bản đúng, đầy đủ.
> *Lưu ý: hệ thống cũ sort theo `ProgressId`; theo §3.2 đổi sang `SortOrder`. Đây là **thay đổi có chủ ý** — phải seed `SortOrder` khớp với thứ tự Id hiện tại khi migrate để không đổi kết quả.*

### 7.3. Reverse sort
**Đảo toàn bộ danh sách SAU KHI đã sort xong** (`list.Reverse()`), **không** nhân hướng vào từng comparator.

> ⚠️ **Mismatch M18.** Với sort nhiều khoá, hai cách cho kết quả **khác nhau**: đảo toàn bộ list giữ nguyên quan hệ giữa các khoá phụ, còn nhân `* dir` vào comparator sẽ đảo cả khoá phụ. Bản đúng là **đảo toàn bộ list sau cùng**.

### 7.4. Sort profile tuỳ chỉnh theo view

- `all`, `planning`, `finished` có profile nhiều khoá riêng, lưu trong config backend.
- `sort=default` dùng profile hiệu lực của view; sort khác `default` là override tạm thời của UI.
- Mỗi rule gồm SortKey (không nhận `default`) và chiều `asc|desc`; không cho trùng key.
- Mảng rule rỗng hợp lệ và được thực thi như `name↑`.
- Giá trị null luôn xếp cuối ở cả hai chiều.
- Backend thêm `name↑` làm tie-breaker ẩn nếu profile chưa có Name để phân trang ổn định.
- Reset xoá override và khôi phục chuỗi §7.2.
- Queue không có sort profile, bỏ qua filter/sort thư viện và giữ `PlayOrder`.

---

## 8. Play Queue

### 8.1. Thứ tự hiển thị
```
1. game có PlayOrder    → xếp trước (nhóm 0)
   game không PlayOrder → xếp sau  (nhóm 1)
2. trong nhóm: PlayOrder↑ (null → int.MaxValue)
3. excited↓
4. avgPlaytimeHours↑ (null → int.MaxValue)
```
Queue **không phân trang** — trả toàn bộ để phục vụ kéo-thả.

### 8.2. Quy tắc nghiệp vụ sau khi KÉO-THẢ — bắt buộc, backend thực thi

Trích chính xác từ `MainWindowViewModel.cs:960-1027`:

```
reordered = danh sách sau khi kéo thả
playingGames    = reordered.where(progress.systemKey == "Playing")     // GIỮ NGUYÊN progress
nonPlayingGames = reordered.where(progress.systemKey != "Playing")     // theo đúng thứ tự mới

// bước 1: toàn bộ non-playing → Intended
for each g in nonPlayingGames:  g.progress = "Intended"

// bước 2: 3 game ĐẦU TIÊN trong nonPlayingGames → NextPlay
nextPlayCount = min(3, nonPlayingGames.length)
for i in 0..nextPlayCount-1:    nonPlayingGames[i].progress = "NextPlay"

// bước 3: persist CẢ PlayOrder LẪN ProgressId
persist(reordered)
```

Điểm mấu chốt:
- Game đang `Playing` **không bị đổi progress**, nhưng **vẫn nằm trong danh sách và vẫn tính PlayOrder**.
- "3 game đầu" đếm trên **nonPlayingGames**, không phải trên toàn danh sách.
- Phải lưu **cả hai** thứ: `PlayOrder` và `ProgressId`.

> ⚠️ **Mismatch M29.** Lovable chỉ đánh lại số thứ tự, không đổi progress → sai hoàn toàn nghiệp vụ.

### 8.3. Chuẩn hoá thứ tự khi mở tab Queue
Khi vào tab `queue`, backend chuẩn hoá và **persist** lại `PlayOrder`:
```
withOrder    = game có PlayOrder, sort theo PlayOrder↑
withoutOrder = game không có PlayOrder, sort theo excited↓ …
kết quả = withOrder ++ withoutOrder, đánh lại PlayOrder = 0,1,2,…
```

### 8.4. Promote to Next Play
Đưa game lên đầu queue: đặt `PlayOrder` nhỏ nhất, rồi **chạy lại toàn bộ quy tắc §8.2** để tái phân bổ NextPlay/Intended.

---

## 9. Hoàn thành game (Finish)

### 9.1. Mô hình — ✅ chốt dùng `GameFinish[]`
- Một game hoàn thành **nhiều lần** → bảng `GameFinish { Id, GameId, FinishedAt: DateTime, Score: double? }`.
- Cột cũ `Game.FinishedIn` (string): **loại khỏi mọi API và UI mới**. Giữ cột trong DB để backfill, đánh dấu deprecated, xoá ở bản sau.

### 9.2. Luồng Finish — thứ tự bắt buộc (giao dịch)
```
1. game.score      = điểm người dùng nhập (0-10 bước 0.5 hoặc 999)
2. game.progress   = "Done"
3. LƯU game
4. THÊM bản ghi GameFinish { FinishedAt = now, Score = điểm vừa nhập }
5. reload
```
> ⚠️ **Mismatch M28.** Lovable chỉ patch 4 field trong state, **không ghi lịch sử**. Bước 4 là bắt buộc.

### 9.3. Finish time dùng cho sort/filter
Sort `finishTime↓` dùng **thời điểm đầy đủ của lần gần nhất** (`MAX(FinishedAt)`). Filter năm hoàn thành vẫn dùng `FinishedAt.Year` và khớp ANY (§5.6).
*(Lưu ý phân biệt: **filter** dùng ANY theo năm, **sort** dùng MAX theo thời điểm đầy đủ. Đây là chủ ý, không phải nhầm lẫn.)*

### 9.4. Sửa thời gian hoàn thành
UI chỉ cho chọn tháng và năm (`YYYY-MM`). Backend cập nhật đúng một `GameFinish`, chuẩn hoá `FinishedAt` thành ngày đầu tháng lúc `00:00:00`, không sửa score và không thay thế các lịch sử khác.

### 9.5. Backfill khi migrate
Với game có `FinishedIn` parse được thành năm và **chưa có** bản ghi `GameFinish` nào:
tạo 1 bản ghi `GameFinish { FinishedAt = <năm>-01-01, Score = game.Score }`.
Giá trị `FinishedIn` không parse được → ghi log, bỏ qua, **không** làm hỏng migration.

---

## 10. Launch game — ✅ luồng mới: chọn EXE hoặc SHORTCUT

### 10.1. Bối cảnh thay đổi
Hệ thống cũ: `Game.InitExeName` là **tên process** (không đường dẫn); khi launch thì nối `.exe` và tra bảng `DetectedProcess` để tìm `FullPath`. Hạn chế: chỉ launch được thứ scanner đã quét thấy.

Luồng mới (đã chốt 2026-08-14): người dùng **chọn thẳng file `.exe` hoặc shortcut `.lnk`** làm mục tiêu khởi chạy.

### 10.2. Mô hình dữ liệu mới

| Field | Kiểu | Ý nghĩa |
|---|---|---|
| `exeName` | `string?` | **Tên process** dùng để *theo dõi thời gian chơi*. Do scanner (`LibraryService`) đồng bộ. **Không phải** thứ để launch. |
| `launchTarget` | `{ kind: "exe" \| "shortcut", path: string } \| null` | **Đường dẫn tuyệt đối** tới file khởi chạy do người dùng chọn. Thay thế `InitExeName`. |

### 10.3. Quy tắc khởi chạy
```
1. nếu game.isDlc            → không cho launch
2. nếu launchTarget != null  → Process.Start(launchTarget.path, UseShellExecute = true)
                               (UseShellExecute = true là BẮT BUỘC để .lnk chạy được)
3. ngược lại                 → tra DetectedProcess theo "{exeName}.exe", lấy FullPath, Process.Start
4. không tìm thấy            → trả lỗi có localize, KHÔNG im lặng
```

### 10.4. Validate `launchTarget`
- Chỉ chấp nhận đuôi `.exe` và `.lnk`.
- Kiểm tra file **tồn tại** tại thời điểm lưu; nếu không → cảnh báo nhưng **vẫn cho lưu** (ổ đĩa ngoài có thể chưa cắm).
- Endpoint launch kiểm tra lại lúc chạy; file biến mất → lỗi rõ ràng.

### 10.5. Migrate từ `InitExeName`
```
với mỗi game có InitExeName != null:
    tra DetectedProcess theo "{InitExeName}.exe"
    nếu thấy  → launchTarget = { kind: "exe", path: FullPath }
    nếu không → để null, ghi log để rà tay
giữ cột InitExeName (deprecated) cho tới khi xác nhận migrate sạch
```

> ⚠️ **Mismatch M35 + T14 được giải quyết bằng luồng mới này.** Lovable thiết kế picker gán đường dẫn `.lnk` — hướng đúng, nhưng phải theo shape `launchTarget` ở §10.2, **không** nhét đường dẫn vào `initExeName`.

---

## 11. Các field runtime — chỉ backend ghi, UI không được sửa

| Field | Ai ghi | Ghi chú |
|---|---|---|
| `ready` | `LibraryService` (scanner) | **Kết quả quét**, không phải người dùng nhập. ⚠️ Mismatch M11: Lovable set `= !!exeName` khi gõ tay — sai bản chất. |
| `folderWarning` | Backend tính | `string \| null`, lý do cảnh báo thiếu folder. |
| `isRunning` | `GameProcessMonitor` | Đẩy qua SSE, poll 2s, chỉ phát khi snapshot đổi. |
| `createAt` | DB | Không sửa. |
| `library.valid`, `library.exeCount` | Scanner | **Dữ liệu runtime, KHÔNG phải config.** Mismatch M13: phải tách khỏi `Config.GameLibraryPath` (vốn chỉ là `string[]`). |

---

## 12. Ảnh

| Field | Nội dung | Dùng ở đâu trong WPF hiện tại | Dùng ở đâu trong UI mới |
|---|---|---|---|
| `gImagePath` (gallery) | Ảnh ngang, decode ở **460px** (Steam header 460×215) | **Card trong lưới** (`MainWindow.xaml:348`) + **hàng trong Queue** (`QueueView.xaml:93`) | Giữ nguyên: card + queue |
| `imagePath` (cover) | Ảnh ngang từ RAWG `background_image` | ❗ **KHÔNG dùng ở bất kỳ file XAML nào** — chỉ được lưu và sync | Hero ở màn chi tiết; fallback sang `gImagePath` khi RAWG không có ảnh |
| `steamLibraryImagePath` | Ảnh thư viện Steam | Chỉ preview trong Game Editor (`GameEditorControl.xaml:1490`) | Giữ nguyên |

**Kích thước card chốt cứng: rộng `270px`, vùng ảnh cao `126.2px`** — Lovable đã dựng **đúng y hệt** hai con số này.

> ✅ **M38 đã được đính chính (2026-08-14).** Bản ghi trước đó cho rằng "card WPF dùng cover (dọc), Lovable dùng gallery → ngược nhau" là **SAI**.
> Kiểm chứng trực tiếp: **cả hai bên đều dùng `gImagePath` (gallery) cho card**, cùng kích thước 270×126.2. Đây là **điểm khớp**, không phải mismatch.
>
> `Game.ImagePath` là cover ngang của RAWG cho hero màn chi tiết. Cover dọc của IGDB sẽ được tách thành `portraitCoverPath` trong migration image-role kế tiếp; không được ghi đè `imagePath` bằng ảnh IGDB.

Upload: khi lưu, ảnh chưa nằm trên Cloudinary sẽ được upload vào `MasterPlay/Covers/{GameId}` / `MasterPlay/Galleries/{GameId}` với `overwrite + invalidate`. **Lỗi upload không được chặn thao tác lưu.**

---

## 13. DLC

| Field | Kiểu |
|---|---|
| `isDlc` | `boolean` |
| `baseGameId` | `string \| null` |

Khi `isDlc == true`:
- Tên tự động gắn hậu tố **`" [DLC]"`**.
- **Xoá sạch dữ liệu runtime**: `exeName`, `launchTarget`, `saveFolderPath`, `backupFolderPath`, `totalPlaytimeSeconds`.
- **Ẩn** các trường: Series, Exe, Save/Backup folder, Playtime, nút Finish.
- **Kế thừa `seriesId`** từ base game.
- Không cho launch (§10.3).

---

## 14. Localize giá trị dữ liệu

Tên `Progress` / `Ownership` / `Genre` / `Theme` **đến từ DB và người dùng sửa được** → **không** dịch bằng key cứng phía client.

**Backend trả kèm `displayName` đã localize** theo `Accept-Language` của request:
```json
{ "systemKey": "Done", "name": "Done", "displayName": "Đã hoàn thành", "sortOrder": 0, "iconData": "..." }
```
> ⚠️ Mismatch A20. Lovable đang dịch bằng key cứng `status.Done` — khi người dùng đổi tên Progress trong Catalog Settings, key sẽ trượt và mất bản dịch.

Chuỗi giao diện thuần (nút, nhãn, tiêu đề) vẫn do i18n phía client lo.

---

## 15. Cấu hình dùng chung với Tray

Các mục sau **thuộc `config.json` / DB**, dùng chung giữa UI và Tray — **cấm** lưu vào `localStorage`:

| Mục | Lý do |
|---|---|
| `language` | Tray theo dõi config và tự reload sau 500 ms (M36) |
| `themeMode` | Tray dùng chung (M37) |
| Toàn bộ Settings | Nguồn sự thật là config, không phải trình duyệt |

`localStorage` **chỉ** được dùng cho preference thuần UI: tab đang mở, độ rộng panel, trạng thái sidebar.

**`themeMode`** đổi từ `bool isDarkMode` thành `"light" | "dark" | "system" | "jungle"` (migrate: `true→dark`, `false→light`). `jungle` là theme trình bày có animation môi trường; dữ liệu nghiệp vụ không thay đổi.

---

## 16. Phân trang
- **30 item/trang** cho mọi tab trừ `queue` (queue trả toàn bộ).
- Backend trả `{ items, totalItems, totalPages, page }`.
- UI **scroll lên đầu** khi đổi trang.

---

## 17. Card field visibility (`cardHints`)

Card hiện/ẩn field theo sort và filter đang bật. Backend tính và trả `cardHints` trong response list — **không** tính ở client.

Quy tắc (trích từ 12 property `ShouldShowX`):

| Hint | Điều kiện |
|---|---|
| `showDeveloper` | `searchScope == "developer"` |
| `showPublisher` | `searchScope == "publisher" && tab != "all"` |
| `showFinishTime` | `tab != "finished" && filter.finishedYear có giá trị` |
| `showPlaytime` | `(sort == "playTime" && tab != "all") \|\| filter.playtime có giá trị` |
| `showExcited` | `(sort == "excited" && tab != "planning") \|\| filter.excited có giá trị` |
| `showScore` | `(sort == "score" && tab != "finished") \|\| filter.score có giá trị` |
| `showOwnership` | Luôn `true`: Gallery Card luôn hiện cả Progress và Ownership |

*(Danh sách đầy đủ 12 hint nằm trong `openapi.yaml`.)*

---

## 18. Bảng tra cứu nhanh: mismatch → điều khoản

| Mismatch | Giải quyết ở |
|---|---|
| M1 đơn vị playtime | §1 |
| M2 avgPlaytime | §1, §5.5 |
| M3 excited | §2.2 |
| M5 finishedIn | §5.6, §9 |
| M8 progress/ownership | §3 |
| M11 ready | §11 |
| M13 config vs runtime | §11 |
| M14 themeMode | §15 |
| M16 ngưỡng MetaScore | §5.5 |
| M17 reverse filter | §5.9 |
| M18 reverse sort | §7.3 |
| M19–M21 default sort | §7.2 |
| M22–M23 thứ hạng | §3.2 |
| M24 cờ "None" | §5.4 |
| M25 filter playtime | §5.3 |
| M26 search năm | §6.4 |
| M27 search initials | §6.2, §6.3 |
| M28 finish | §9.2 |
| M29 kéo thả queue | §8.2 |
| M35 + T14 launch target | §10 |
| M36–M37 language/theme | §15 |
| M38 ảnh card | §12 |

---

## 19. Tài khoản và quyền sở hữu dữ liệu

- Desktop Shell cho phép vào guest mode bằng token ngẫu nhiên theo từng lần chạy;
  Hosted/Web không hiển thị và không chấp nhận guest mode.
- Guest chỉ dùng userdata local của thiết bị, không có hồ sơ Supabase, không được gọi
  mutation quản lý account và không biến thành một user cloud ngầm định.
- Self-registration luôn tạo role `user`; role từ request client bị bỏ qua/từ chối.
- Username unique không phân biệt hoa/thường và chỉ backend/database được normalize.
- Admin bootstrap đọc username/email/password từ environment, idempotent và không reset account đã có.
- Admin bootstrap phải đổi password trước khi vào thư viện.
- Domain API lấy owner từ JWT subject; không tin `user_id` trong payload/query.
- Admin không mặc nhiên được đọc library, private game, device hoặc secret của user khác.
- Catalog metadata dùng chung: authenticated đọc, admin ghi.
- Game user tự tạo là private vĩnh viễn, không có quy trình duyệt/promote.
- Score, excited, playtime, progress, ownership, finish, diary và queue thuộc user.
- Exe/launcher/save/backup/process state thuộc cả user và device.
- Steam/Ubisoft password chỉ nằm trong vault mã hóa của thiết bị, không đồng bộ cloud.
- Account bị khóa phải bị chặn trên request tiếp theo dù access token chưa hết hạn.
