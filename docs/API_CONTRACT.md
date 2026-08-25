# QuestLog — API CONTRACT v1

| | |
|---|---|
| Phiên bản | 1.0 |
| Ngày | 2026-08-14 |
| Sở hữu | **Backend.** Frontend không được sửa file này hay các type sinh ra từ nó. |
| Quy tắc nghiệp vụ | Xem [`DOMAIN_RULES.md`](DOMAIN_RULES.md) — file này chỉ mô tả *hình dạng*, không lặp lại *quy tắc*. |

---

## 0. Hai chế độ chạy — cùng một codebase

| | **Local API** | **Hosted API** |
|---|---|---|
| Chạy ở | Máy chơi game, kèm agent | Cloud (luôn bật) |
| Bind | `127.0.0.1:{port ngẫu nhiên}` | `0.0.0.0:{PORT}`, HTTPS |
| Phục vụ | Desktop shell (WebView2) | Web browser |
| Nhóm endpoint native (§8) | ✅ Đầy đủ | ❌ Trả `501 Not Implemented` |
| Dữ liệu | Supabase Postgres | Supabase Postgres (**cùng một DB**) |

Client phát hiện chế độ qua `GET /api/capabilities` (§10) và **ẩn nút** thay vì gọi rồi nuốt lỗi.

---

## 1. Quy ước chung

### 1.1. Đặt tên
- JSON dùng `camelCase`.
- **Field thời lượng bắt buộc mang hậu tố đơn vị**: `totalPlaytimeSeconds`, `avgPlaytimeHours`, `timeSpentMinutes`. Không có field nào tên trần kiểu `playtime`.

### 1.2. Xác thực
Mọi endpoint domain yêu cầu:
```
Authorization: Bearer <Supabase JWT | desktop guest token>
```
Hosted API luôn validate Supabase access token. Local API cũng bắt buộc token và
không cho truy cập ẩn danh trực tiếp; riêng Desktop Shell có thể đổi handshake token
ngẫu nhiên theo lần chạy lấy desktop guest token. Guest token chỉ hợp lệ từ loopback,
chỉ dùng userdata local và không đại diện cho tài khoản Supabase.

Các endpoint public duy nhất là `/api/health`, `/api/auth/config`, `/api/auth/login`,
`/api/auth/signup`, `/api/auth/guest`, `/api/auth/session` và `/api/auth/logout`.
`/api/auth/guest` vẫn yêu cầu header bí mật do Desktop Shell inject. Refresh token tài
khoản nằm trong cookie `HttpOnly`; frontend chỉ giữ access token ngắn hạn trong memory.

### 1.3. Localize
Client gửi `Accept-Language: en-US | vi-VN`. Backend dùng nó để điền `displayName` (DOMAIN_RULES §14).
Mặc định hệ thống: **`en-US`**.

### 1.4. Lỗi — shape thống nhất
```json
{
  "error": {
    "code": "GAME_NOT_FOUND",
    "message": "Localized human-readable message",
    "details": { "gameId": "..." }
  }
}
```
| HTTP | Khi nào |
|---|---|
| `400` | Payload sai shape / validate thất bại |
| `401` | Thiếu hoặc sai JWT |
| `404` | Không tìm thấy tài nguyên |
| `409` | Xung đột (vd: xoá taxonomy đang được dùng, tuỳ chính sách) |
| `423` | Tài khoản bị khóa |
| `429` | Quá nhiều yêu cầu auth |
| `501` | Endpoint native gọi trên Hosted API |
| `502` | Dịch vụ ngoài lỗi (RAWG, Cloudinary, Gemini) |

**Frontend không được nuốt lỗi.** Mọi lỗi phải hiển thị được cho người dùng.

### 1.5. Phân trang
```json
{ "items": [...], "page": 1, "pageSize": 30, "totalItems": 412, "totalPages": 14 }
```

---

## 1.6. Auth và account

| Method | Path | Auth | Ghi chú |
|---|---|---|---|
| `GET` | `/api/auth/config` | Public | Chỉ trả cờ cấu hình, không trả key |
| `POST` | `/api/auth/login` | Public | `identifier` nhận username hoặc email |
| `POST` | `/api/auth/signup` | Public | Luôn tạo role `user`, yêu cầu verify email |
| `POST` | `/api/auth/guest` | Desktop handshake | Cấp token theo lần chạy; chỉ WebView2 + Local API loopback |
| `POST` | `/api/auth/session` | HttpOnly cookie | Refresh session |
| `GET` | `/api/auth/me` | Bearer | Hồ sơ account hiện tại |
| `POST` | `/api/auth/change-password` | Bearer | Xác thực lại password hiện tại, tối thiểu 12 ký tự |
| `PATCH` | `/api/auth/account/username` | Bearer | Xác thực lại password, cập nhật username unique |
| `POST` | `/api/auth/account/email-change` | Bearer | Xác thực lại password, gửi luồng xác nhận email mới; email cũ còn hiệu lực đến khi xác nhận |
| `POST` | `/api/auth/logout` | Tuỳ chọn Bearer | Revoke provider session và xoá cookie |

Username được normalize và kiểm tra unique ở database. Client không được tự phân giải
username thành email và không được gửi role lúc signup. Email trong `profiles` chỉ được
đồng bộ sau khi Supabase trả địa chỉ mới đã xác nhận cho session.

---

## 2. Kiểu dữ liệu dùng chung

### 2.1. `CatalogItem` — Progress / Ownership
```ts
{
  id: number
  systemKey: string      // BẤT BIẾN — code chỉ được dùng field này
  name: string           // người dùng sửa được
  displayName: string    // đã localize theo Accept-Language
  colorCode: string
  iconData: string | null   // base64 PNG data URL
  sortOrder: number
}
```
> `systemKey` hợp lệ: xem DOMAIN_RULES §3.1. Frontend **không được** hardcode danh sách này để suy ra hành vi hiển thị — dùng `displayName` + `iconData` + `colorCode` từ API.

### 2.2. `TaxonomyItem` — Series / Genre / Theme
```ts
{
  id: number
  name: string
  iconData: string | null   // Series: cần thêm cột mới, xem §12
  sortOrder: number
}
```

### 2.3. `LaunchTarget`
```ts
{ kind: "exe" | "shortcut", path: string } | null
```

### 2.4. `GameListItem` — dùng cho lưới card
```ts
{
  id: string
  name: string
  developer: string | null
  publisher: string | null
  series: TaxonomyItem | null
  imagePath: string | null            // COVER (dọc) — card dùng cái này
  progress: CatalogItem | null
  ownership: CatalogItem | null
  metaScore: number | null
  excited: number | null              // 1-10, sentinel 999
  score: number | null                // 0-100, sentinel 999
  totalPlaytimeSeconds: number | null
  avgPlaytimeHours: number | null
  avgPlaytimeBand: "VeryShort"|"Short"|"Medium"|"Long"|"VeryLong"|null   // backend suy ra
  publishDate: string | null          // ISO date "YYYY-MM-DD"
  lastFinishedAt: string | null       // ISO date-time, MAX(GameFinish.FinishedAt)
  isDlc: boolean
  ready: boolean                      // kết quả scan, UI KHÔNG sửa
  isRunning: boolean                  // từ process monitor
  canLaunch: boolean                  // backend quyết định (có launchTarget hoặc exe đã detect)
  folderWarning: string | null        // đã localize
}
```

### 2.5. `GameDetail` — kế thừa `GameListItem`, thêm:
```ts
{
  slugName: string | null
  exeName: string | null              // TÊN PROCESS để tracking, không phải để launch
  launchTarget: LaunchTarget
  saveFolderPath: string | null
  backupFolderPath: string | null
  gImagePath: string | null           // gallery (ngang)
  steamLibraryImagePath: string | null
  genres: TaxonomyItem[]
  themes: TaxonomyItem[]
  baseGameId: string | null
  baseGameName: string | null
  finishes: { id: number, finishedAt: string, score: number | null }[]
  ownershipInfo: {
    steamAccount: string | null
    steamPassword: string | null      // xem §11 về mã hoá
    ubisoftAccount: string | null
    ubisoftPassword: string | null
    supportDocumentUrl: string | null
    downloadUrl: string | null
    note: string | null
  } | null
  createAt: string | null
}
```

---

## 3. Library

### `GET /api/games`

Query params:

| Param | Kiểu | Mặc định | Ghi chú |
|---|---|---|---|
| `tab` | `all\|planning\|finished\|queue` | `all` | DOMAIN_RULES §4 |
| `q` | string | — | search term |
| `scope` | `all\|name\|series\|publisher\|developer\|releaseYear` | `all` | §6.3 |
| `sort` | `default\|status\|name\|playTime\|publish\|finishTime\|metaScore\|excited\|score\|ownership\|progress\|avgPlaytime` | `default` | §7.1 |
| `reverseSort` | boolean | `false` | §7.3 — đảo TOÀN BỘ list sau sort |
| `reverseFilter` | boolean | `false` | §5.9 — kiểm tra `hasAnyFilter` TRƯỚC |
| `finishedYearOp` / `finishedYearValue` | op, number | — | §5.6 khớp ANY |
| `excitedOp` / `excitedValue` | op, number | — | |
| `scoreOp` / `scoreValue` | op, number | — | |
| `playtimeOp` / `playtimeSeconds` | op, number | — | **client nhân 3600 trước khi gửi** |
| `avgBands` | csv | — | `VeryShort,Short,Medium,Long,VeryLong` |
| `includeAvgNone` | boolean | `false` | cờ RIÊNG, §5.4 |
| `metaBands` | csv | — | `VeryLow,Low,Medium,High,VeryHigh` |
| `includeMetaNone` | boolean | `false` | cờ RIÊNG |
| `progressKeys` | csv of systemKey | — | KHÔNG dùng id |
| `ownershipKeys` | csv of systemKey | — | |
| `genreIds` | csv of int | — | OR với themeIds, §5.7 |
| `themeIds` | csv of int | — | |
| `page` / `pageSize` | int | `1` / `30` | tab `queue` bỏ qua, trả toàn bộ |

Op hợp lệ: `gt` `gte` `eq` `lte` `lt` (ánh xạ `>` `>=` `=` `<=` `<`).

Response:
```ts
{
  items: GameListItem[]
  page, pageSize, totalItems, totalPages
  activeFilterCount: number      // backend đếm, §5.10
  cardHints: {                   // §17
    showDeveloper, showPublisher, showFinishTime, showPlaytime,
    showExcited, showScore, showOwnership, showMetaScore,
    showAvgPlaytime, showPublishDate, showSeries, showProgress: boolean
  }
}
```

### Persistent sort profile theo view

| Method | Endpoint | Ý nghĩa |
|---|---|---|
| `GET` | `/api/library/sort-profiles` | Chuỗi sort hiệu lực của All/Planning/Finished |
| `PUT` | `/api/library/sort-profiles/{view}` | Lưu toàn bộ `{ key, direction }[]`; mảng rỗng = Name↑ |
| `DELETE` | `/api/library/sort-profiles/{view}` | Xoá override, khôi phục chuỗi mặc định |

`GET /api/games?sort=default` dùng profile đã lưu của tab. Sort khác `default` là override tạm thời từ toolbar. Queue không có profile và luôn giữ `PlayOrder`.

---

## 4. Game

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/games/{id}` | → `GameDetail` |
| `POST` | `/api/games` | tạo mới |
| `PUT` | `/api/games/{id}` | cập nhật toàn phần |
| `POST` | `/api/games/{id}/disable` | soft delete |
| `POST` | `/api/games/{id}/restore` | khôi phục |
| `POST` | `/api/games/{id}/finishes` | body `{ score }` — tự lấy thời gian hiện tại, chạy trong một transaction |
| `PUT` | `/api/games/{id}/finishes/{finishId}` | body `{ finishedMonth: "YYYY-MM" }` — chỉ sửa tháng/năm của đúng một lịch sử |
| `POST` | `/api/games/{id}/promote` | §8.4 |
| `POST` | `/api/games/{id}/playtime` | body `{ deltaMinutes }` — cộng/trừ |
| `GET` | `/api/games/disabled` | danh sách đã disable (màn Restore) |

**Không có `DELETE`** — hệ thống chỉ soft delete.

---

## 5. Queue

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/queue` | Trả toàn bộ, đã chuẩn hoá + persist thứ tự (§8.3) |
| `PUT` | `/api/queue/order` | body `{ gameIds: string[] }` — **server áp quy tắc §8.2**, client chỉ gửi thứ tự mới |

> Client **không** được tự tính NextPlay/Intended. Gửi thứ tự, nhận lại danh sách đã áp quy tắc.

---

## 6. Taxonomy & Catalog

| Method | Path |
|---|---|
| `GET/POST` | `/api/taxonomy/{series\|genres\|themes}` |
| `PUT/DELETE` | `/api/taxonomy/{series\|genres\|themes}/{id}` |
| `GET` | `/api/catalog/{progress\|ownership}` |
| `PUT` | `/api/catalog/{progress\|ownership}/{id}` — chỉ `name`, `iconData`, `colorCode`, `sortOrder` |
| `PUT` | `/api/catalog/{progress\|ownership}/order` — body `{ ids: number[] }` |

`systemKey` **không** nằm trong payload ghi — server từ chối nếu client cố gửi.
Xoá taxonomy: server xử lý lại association trong **một transaction**.

---

## 7. Metadata sync

| Method | Path | Nguồn |
|---|---|---|
| `POST` | `/api/games/{id}/sync/metadata` | IGDB (nếu đã cấu hình): canonical metadata; RAWG fallback cho name/publisher/developer/slug/metascore/avgPlaytime/releaseDate + cover ngang; Steam cho gallery/card; Gemini đoán `exeName`. Thiếu IGDB không được làm cả sync thất bại. |
| `POST` | `/api/games/{id}/sync/cover` | RAWG `background_image` — cover ngang cho detail hero |
| `POST` | `/api/games/{id}/sync/gallery` | Steam `header.jpg` — ảnh ngang cho gallery card |
| `POST` | `/api/games/{id}/sync/steam-library` | Steam CDN |
| `GET` | `/api/games/{id}/external-links` | → `{ hltbUrl, metacriticUrl }` **backend dựng từ name/slug** |

> Frontend **không** hardcode URL HowLongToBeat/Metacritic trên object game (Lovable đang làm vậy — phải bỏ).

---

## 8. Native — ❗ chỉ Local API, Hosted trả `501`

| Method | Path | Ghi chú |
|---|---|---|
| `POST` | `/api/games/{id}/launch` | §10.3 |
| `POST` | `/api/fs/pick-file` | body `{ filter: "exe-or-shortcut" \| "database" \| "json" }` |
| `POST` | `/api/fs/pick-folder` | |
| `POST` | `/api/fs/reveal` | body `{ path }` — mở Explorer |
| `POST` | `/api/games/{id}/backup-folder/generate` | tạo tên duy nhất + ghi `SaveLocation.txt` |
| `GET` | `/api/tray/status` | |
| `POST` | `/api/tray/services/{name}` | bật/tắt 1 trong 3 service |
| `POST` | `/api/tray/{sync-images\|scan-now\|backup-now\|open-logs\|startup}` | |

---

## 9. Settings, Dashboard, Events

| Method | Path | Ghi chú |
|---|---|---|
| `GET/PUT` | `/api/settings` | gồm `language`, `themeMode`, Cloudinary, RAWG key, library paths |
| `POST` | `/api/settings/test-cloudinary` | ping thật |
| `GET` | `/api/dashboard/stats` | 6 ô thống kê + top-5 playtime — **backend tính**, không tính ở client |
| `GET` | `/api/events` | **SSE**: `runningProcesses`, `refresh`, `trayNotification`, `syncProgress` |

`GET /api/settings` trả `libraries` tách đôi:
```ts
{
  paths: string[]                                      // CONFIG — người dùng sửa
  runtime: { path, valid, exeCount }[]                 // RUNTIME — scanner ghi, read-only
}
```
> Mismatch M13: Lovable gộp chung thành một mảng. Phải tách.

---

## 10. Capabilities & Health

### `GET /api/capabilities`
```ts
{
  mode: "local" | "hosted",
  features: {
    launch: boolean, filePicker: boolean, revealFolder: boolean,
    tray: boolean, scanner: boolean, controller: boolean
  }
}
```
UI **ẩn** nút của feature `false`, không gọi rồi nuốt `501`.

### `GET /api/health` — không cần auth, dùng cho readiness probe.

---

## 11. Bảo mật

- `ownershipInfo.steamPassword` / `ubisoftPassword`: mã hoá **at-rest** (pgcrypto). Chỉ trả plaintext qua HTTPS cho chính chủ; **không** log, **không** đưa vào response list.
- RAWG / Gemini / Cloudinary key: chỉ đọc từ config phía server, **không bao giờ** trả về client. Endpoint Settings trả trạng thái đã cấu hình (`{ configured: true }`), không trả giá trị key.
- Rate limit trên Hosted API cho nhóm sync (§7) — tránh đốt quota RAWG.

---

## 12. Thay đổi schema cần thiết cho v1

| # | Thay đổi | Vì |
|---|---|---|
| S1 | `ProgressStatus.SystemKey`, `OwnershipStatus.SystemKey` (unique, not null) | DOMAIN_RULES §3.1 |
| S2 | `Series.IconData` (nullable) | Lovable có icon cho Series, backend chưa có cột (T13) |
| S3 | `Series.SortOrder` | Đồng bộ với Genre/Theme |
| S4 | `Game.LaunchTargetPath`, `Game.LaunchTargetKind` | Luồng launch mới §10.2 |
| S5 | `Config.ThemeMode` (`light\|dark\|system\|jungle`) thay `isDarkMode` | T7 + Jungle theme |
| S6 | Backfill `GameFinish` từ `FinishedIn` | §9.4 |
| S7 | Toàn bộ bảng: cột `user_id` + RLS policy | Auth thật, Supabase |
| S8 | SQLite → Postgres (kiểu dữ liệu, sequence, index) | Kiến trúc v2 |

`Game.InitExeName` và `Game.FinishedIn`: **giữ, đánh dấu deprecated**, xoá ở bản sau khi xác nhận migrate sạch.
