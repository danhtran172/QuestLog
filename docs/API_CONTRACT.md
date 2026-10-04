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
`/api/auth/signup`, `/api/auth/guest`, `/api/auth/session`, `/api/auth/logout`,
`/api/auth/google/start`, `/api/auth/google/callback`, `/api/auth/google/complete`,
`/api/auth/password/forgot`, `/api/auth/password/verify-code` và `/api/auth/password/reset`. Tài khoản Google chưa chọn
username (`onboarding = usernameRequired`) bị chặn `403 USERNAME_REQUIRED` ở mọi endpoint
ngoài `/api/auth/*` (trừ đổi username/email), `GET /api/settings` và `GET /api/capabilities`.
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
| `POST` | `/api/auth/change-password` | Bearer | `{newPassword, reauthTicket}`, tối thiểu 12 ký tự. Tài khoản chỉ có Google dùng nó để **đặt** mật khẩu đầu tiên. Ngoại lệ duy nhất: khi `mustChangePassword = true` (bootstrap admin) nhận `currentPassword` thay ticket |
| `PATCH` | `/api/auth/account/username` | Bearer | `{username, reauthTicket}`, cập nhật username unique |
| `POST` | `/api/auth/account/email-change` | Bearer | `{email, reauthTicket}`, gửi luồng xác nhận email mới; email cũ còn hiệu lực đến khi xác nhận |
| `POST` | `/api/auth/account/username-claim` | Bearer | Chỉ khi `onboarding = usernameRequired`: chọn username đầu tiên cho tài khoản Google. Cần `registrationEnabled` |
| `POST` | `/api/auth/reauth/email` | Bearer | Gửi mã 6 số tới email hiện tại. `202 {sent, resendAvailableAt}`; trong cooldown 60 giây trả `429 CODE_RESEND_COOLDOWN` |
| `POST` | `/api/auth/reauth/verify` | Bearer | `{code}` → `{reauthTicket, expiresAt}`: dùng một lần, sống 5 phút, gắn account + phiên. Sai 5 lần → `429 CODE_ATTEMPTS_EXCEEDED` |
| `POST` | `/api/auth/password/forgot` | Public | `{identifier}` (username hoặc email). **Luôn** `202`, không tiết lộ tài khoản có tồn tại |
| `POST` | `/api/auth/password/verify-code` | Public | `{identifier, code}` → kiểm tra mã ngay, đúng thì `{resetTicket, expiresAt}` (dùng một lần, 10 phút) |
| `POST` | `/api/auth/password/reset` | Public | `{resetTicket, newPassword}` → `AuthSession` + cookie; thu hồi mọi phiên khác. Ticket sai/hết hạn: `401 RESET_TICKET_INVALID` |
| `POST` | `/api/auth/google/start` | Public (+ desktop handshake khi `client = desktop`) | `{client: web\|desktop}` → `{flowId, authorizeUrl, expiresAt}`. Web nhận cookie `questlog_oauth_flow` |
| `GET` | `/api/auth/google/callback` | Gắn với `state` | Supabase redirect về; chỉ gắn code vào flow và trả trang HTML tĩnh |
| `POST` | `/api/auth/google/complete` | Gắn với flow (cookie Web / desktop token) | Poll mỗi 1,5 giây: `{status: pending}` hoặc `{status: completed, session}`; `410 OAUTH_FLOW_EXPIRED` khi hết hạn |
| `POST` | `/api/auth/logout` | Tuỳ chọn Bearer | Revoke **chỉ phiên hiện tại** (`scope=local`) và xoá cookie; thiết bị khác vẫn đăng nhập |

Username được normalize và kiểm tra unique ở database. Client không được tự phân giải
username thành email và không được gửi role lúc signup. Email trong `profiles` chỉ được
đồng bộ sau khi Supabase trả địa chỉ mới đã xác nhận cho session.

Đăng ký/đăng nhập bằng Google do Host làm trung gian PKCE: verifier không rời Host, code chỉ
được đổi ở lần `complete` đầu tiên sau callback. Thiết kế:
`docs/superpowers/specs/2026-10-04-google-signup-email-recovery-design.md`.

| Code | HTTP | Khi nào |
|---|---|---|
| `GOOGLE_DISABLED` | 403 | Chưa bật Google hoặc thiếu `SUPABASE_GOOGLE_REDIRECT_URL` |
| `DESKTOP_HANDSHAKE_REQUIRED` | 403 | Flow `desktop` không đến từ WebView2 + loopback |
| `INVALID_OAUTH_CLIENT` | 400 | `client` không phải `web`/`desktop` |
| `OAUTH_FLOW_EXPIRED` | 410 | Flow không tồn tại, hết hạn, đã dùng, hoặc không gắn với người gọi |
| `OAUTH_CANCELLED` | 400 | Người dùng huỷ ở Google hoặc Supabase trả lỗi |
| `USERNAME_REQUIRED` | 403 | Tài khoản Google chưa chọn username |
| `ONBOARDING_COMPLETE` | 409 | Claim username khi tài khoản đã có username |
| `CODE_INVALID` | 400 | Mã email sai hoặc hết hạn |
| `CODE_RESEND_COOLDOWN` | 429 | Xin mã lại trong 60 giây; `details.resendAvailableAt` |
| `CODE_ATTEMPTS_EXCEEDED` | 429 | Sai mã 5 lần; phải xin mã mới |
| `EMAIL_RATE_LIMITED` | 429 | Supabase hết hạn mức gửi email (mail mặc định rất thấp); **không** có mã nào được gửi |
| `RESET_TICKET_INVALID` | 401 | Reset ticket sai, hết hạn (10 phút) hoặc đã dùng; phải gửi mã mới |
| `REAUTH_REQUIRED` | 400 | Thao tác nhạy cảm thiếu `reauthTicket` |
| `REAUTH_INVALID` | 401 | Ticket sai, hết hạn, đã dùng hoặc gắn phiên khác |
| `SESSION_EXPIRED` | 401 | Bearer không còn hợp lệ ở provider (phiên bị thu hồi/hết hạn). Client làm mới qua `/api/auth/session` rồi gửi lại; thất bại thì về màn đăng nhập |

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
  // Server điền — bị bỏ qua khi là body request (spec 2026-10-04-theme-groups §5.1):
  key: string | null        // tên đã chuẩn hoá
  scope: "shared" | "personal" | null
  editable: boolean         // server tính; client không tự suy ra quyền
  groupId?: number | null   // chỉ theme; null = chưa phân nhóm
}
```

`ThemeGroup`: `{ id, scope, nameEn, nameVi, displayName, sortOrder, editable }` — `displayName` theo
`Accept-Language`. `GroupedThemes`: `{ groups: [{ group: ThemeGroup | null, themes: TaxonomyItem[] }] }`,
server sắp sẵn (nhóm chung → nhóm cá nhân → chưa phân nhóm).

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
  bannerPath: string | null        // Steam header ngang — card/queue
  posterPath: string | null    // IGDB cover dọc — Console mode
  backdropPath: string | null            // RAWG ảnh ngang — detail hero/fallback Console
  progress: CatalogItem | null
  ownership: CatalogItem | null
  metaScore: number | null
  excited: number | null              // 0-11 bước 0.5; 11 = Peak Score → 10+
  score: number | null                // 0-11 bước 0.5; 11 = Peak Score → 10+
  totalPlaytimeSeconds: number | null
  avgPlaytimeHours: number | null
  avgPlaytimeBand: "VeryShort"|"Short"|"Medium"|"Long"|"VeryLong"|null   // backend suy ra
  publishDate: string | null          // ISO date "YYYY-MM-DD"
  lastFinishedAt: string | null       // ISO date-time, MAX(GameFinish.FinishedAt)
  lastPlayedAt: string | null         // UTC ISO date-time, Tray ghi khi phát hiện một phiên chơi mới
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
  genres: TaxonomyItem[]
  themes: TaxonomyItem[]
  baseGameId: string | null
  baseGameName: string | null
  finishes: { id: number, finishedAt: string, hasMonth: boolean, score: number | null }[]
                                      // hasMonth=false -> chỉ nhớ năm, hiện "2019" chứ không phải "01/2019" (§9.1)
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
| `tab` | `all\|planning\|finished\|queue\|console` | `all` | DOMAIN_RULES §4; console là view cố định playable/recent |
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

View `console` không có sort profile: server chỉ trả game có `canLaunch=true` và sắp theo `lastPlayedAt↓` (null cuối), rồi `name↑`.

`GET /api/games?sort=default` dùng profile đã lưu của tab. Sort khác `default` là override tạm thời từ toolbar. Queue không có profile và luôn giữ `PlayOrder`.

### 3.1. Kéo cập nhật metadata về thư viện

`POST /api/library/metadata-refresh` là **chiều kéo về**: admin sửa một bản ghi trên kho thì
mọi dòng thư viện đã nối tới bản ghi đó phải đổi theo — **trừ** field chính chủ thư viện đã sửa tay.
Luật hợp nhất ba chiều nằm ở DOMAIN_RULES §20.

Chạy trên mọi dòng của owner hiện tại có `metadataGameId`, **kể cả dòng đã disable**: khôi
phục một game thì nó phải sẵn sàng dùng ngay chứ không phải chờ thêm một vòng đồng bộ.

`dryRun` mặc định **`false`** — ngược với các endpoint `admin/metadata/*`. Hai cái đó ghi
vào kho dùng chung (không có undo) nên phải hỏi lại; cái này chỉ ghi vào SQLite của chính
người gọi và chỉ chạm vào ô người dùng chưa từng sửa.

Server có **cửa chặn 10 phút**: mọi dòng vừa đồng bộ trong 10 phút thì trả `skipped: true`
mà không gọi kho. Gửi `{ "force": true }` để bỏ qua — dùng cho nút bấm tay sau khi vừa sửa
bên Admin Console.

Kho không trả về một bản ghi (bị gỡ xuất bản hoặc đã xoá) thì dòng thư viện **không bị
đụng tới**, chỉ cộng vào `missing`. Game vẫn là của người dùng; chỉ là kho không còn nói gì
về nó nữa.

```jsonc
{
  "dryRun": false,
  "skipped": false,
  "linked": 99,          // dòng thư viện có metadataGameId
  "checked": 96,         // dòng kho thực sự trả về dữ liệu
  "updated": 4,          // số GAME có ít nhất một field đổi
  "fieldsUpdated": 7,
  "fieldsKept": 2,       // field kho nói khác nhưng người dùng đã sửa tay
  "missing": 3,
  "games": [
    { "gameId": "...", "name": "...", "metadataGameId": 42,
      "updated": ["posterPath"], "kept": ["developer"] }
  ]
}
```

| Code | HTTP | Khi nào |
|---|---|---|
| `METADATA_UNAVAILABLE` | 503 | Không gọi được Supabase |

---

## 4. Game

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/games/{id}` | → `GameDetail` |
| `POST` | `/api/games` | tạo mới |
| `PUT` | `/api/games/{id}` | cập nhật toàn phần |
| `POST` | `/api/games/{id}/disable` | soft delete |
| `POST` | `/api/games/{id}/restore` | khôi phục |
| `POST` | `/api/games/{id}/finishes` | body `{ score, finishedYear?, finishedMonth? }` — bỏ trống cả hai mốc thời gian thì dùng thời gian hiện tại; chạy trong một transaction |
| `PUT` | `/api/games/{id}/finishes/{finishId}` | body `{ finishedYear, finishedMonth? }` — chỉ sửa thời điểm của đúng một lịch sử, không đụng score |
| `DELETE` | `/api/games/{id}/finishes/{finishId}` | Xoá đúng một lịch sử. Score giữ nguyên; hết lịch sử thì Progress bị bỏ trống (§9.6) |
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
| `GET` | `/api/taxonomy/themes/grouped?q=` |
| `PUT` | `/api/taxonomy/themes/order` — `{ groupId, ids }` |
| `POST` | `/api/taxonomy/theme-groups` · `PUT/DELETE` `/api/taxonomy/theme-groups/{id}` · `PUT` `/api/taxonomy/theme-groups/order` |
| `GET` | `/api/admin/taxonomy/themes/grouped`, `/api/admin/taxonomy/{genres\|themes}` |
| `POST/PUT/DELETE` | `/api/admin/taxonomy/{genres\|themes}[/{id}]`, `/api/admin/taxonomy/theme-groups[/{id}]` |
| `PUT` | `/api/admin/taxonomy/{themes\|genres\|theme-groups}/order` |
| `GET` | `/api/catalog/{progress\|ownership}` |
| `PUT` | `/api/catalog/{progress\|ownership}/{id}` — chỉ `name`, `iconData`, `colorCode`, `sortOrder` |
| `PUT` | `/api/catalog/{progress\|ownership}/order` — body `{ ids: number[] }` |

`systemKey` **không** nằm trong payload ghi — server từ chối nếu client cố gửi.
Xoá taxonomy: server xử lý lại association trong **một transaction**.

**Quyền sở hữu genre/theme** (spec 2026-10-04-theme-groups): hàng có `LibraryOwnerKey`
`system:shared` (bộ chung, có icon) hoặc `user:<id>` / `guest:local` (cá nhân).
- `/api/taxonomy/*`: thấy chung + của mình; **chỉ ghi được hàng của mình**, `iconData` luôn bị ép `null`.
  Sửa/xoá hàng chung → `403 TAXONOMY_NOT_EDITABLE`. `PUT` không đổi nhóm — chuyển nhóm qua `/themes/order`.
- `/api/admin/taxonomy/*`: `RequireAdmin`, chỉ chạm hàng `system:shared`, được đặt `iconData`.
- `PUT /api/games/{id}`: gán tag không thuộc chung/của mình → `400 TAXONOMY_NOT_VISIBLE`.
- Series chưa tách theo chủ sở hữu.

---

## 6.5. Admin Console — kho metadata dùng chung

> Thiết kế: [specs/2026-09-14-admin-console-mode-design.md](superpowers/specs/2026-09-14-admin-console-mode-design.md)

### 6.5.0 Integrations dùng chung

Admin Console quản lý credential server-side cho `rawg`, `igdb`, `steam`,
`cloudinary` và `gemini`. Mọi endpoint bên dưới yêu cầu role admin từ JWT; browser
chỉ nhận `configured`, tên các field đã lưu, revision và kết quả test an toàn —
**không** nhận plaintext, ciphertext, hoặc một phần credential.

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/admin/integrations` | Trạng thái của cả 5 provider |
| `PUT` | `/api/admin/integrations/{provider}` | Thay thế **trọn bộ** credential provider; field thiếu là lỗi `422`, không có nghĩa “giữ key cũ” |
| `POST` | `/api/admin/integrations/{provider}/test` | Kiểm tra read-only trên backend; trả status/code đã được redaction |

IGDB crawler lấy active revision từ kho secret chung khi bắt đầu lượt chạy; lưu key
mới không cần deploy lại function. Không có endpoint hay nút deploy/enable cron trong
Admin Console. Steam Web API chỉ test app key; đồng bộ thư viện Steam cần danh tính và
uỷ quyền riêng của từng người dùng, nên không nằm trong integration này.

Mọi endpoint dưới `/api/admin/**` **tự kiểm role admin từ JWT**. Workspace mode của UI
không tham gia vào quyết định quyền — mode là rào chắn UX, không phải rào chắn bảo mật.

| Method | Path | Ghi chú |
|---|---|---|
| `GET` | `/api/admin/metadata/series` | `q` (ilike), `limit` (1-200, mặc định 100) |
| `POST` | `/api/admin/metadata/series` | body `{ name }` |
| `PUT` | `/api/admin/metadata/series/{id}` | body `{ name }` |
| `DELETE` | `/api/admin/metadata/series/{id}` | `204` khi thành công |
| `POST` | `/api/admin/metadata/series/merge` | body `{ sourceId, targetId }` → `{ target, movedGames }` |
| `GET` | `/api/admin/metadata/games` | `q`, `missing`, `sort`, `desc`, `isDlc`, `seriesId`, `noSeries`, `year`, `published`, `cursor` **hoặc** `page` (≥ 1), `pageSize` (1-100, mặc định 30) |
| `GET` | `/api/admin/metadata/games/{id}` | |
| `POST` | `/api/admin/metadata/games` | body là bản ghi đầy đủ + `confirmDuplicate` |
| `PUT` | `/api/admin/metadata/games/{id}` | body là bản ghi đầy đủ + **`revision` bắt buộc** |
| `DELETE` | `/api/admin/metadata/games/{id}` | → `{ orphanedDlcCount }` |
| `GET` | `/api/admin/metadata/publishing` | tiêu chí xuất bản + số liệu |
| `PUT` | `/api/admin/metadata/publishing` | body là các khoá `require*`; **field vắng mặt = giữ nguyên** |
| `PUT` | `/api/admin/metadata/games/{id}/published` | body `{ published }` — xuất bản/gỡ thủ công. **Không nhận `revision`** |
| `GET` | `/api/admin/metadata/crawler` | Trạng thái worker, lease, queue, storage, cron và run gần nhất |
| `PUT` | `/api/admin/metadata/crawler/enabled` | body `{ enabled }`; tạm dừng sẽ fence run đang chạy ở lần ghi kế tiếp |
| `POST` | `/api/admin/metadata/crawler/run` | trả `202 { requestId }`; xếp request qua `pg_net`, không chạy đồng bộ trong HTTP request |
| `GET` | `/api/admin/metadata/crawler/runs` | lịch sử run, keyset cursor server-owned |
| `GET` | `/api/admin/metadata/crawler/runs/{runId}/games` | game được crawler thêm/làm mới trong run đó |
| `GET` | `/api/admin/metadata/crawler/games` | source snapshot crawler theo dõi, keyset cursor server-owned |

### 6.5.2. Lọc và sắp xếp danh sách games

| Tham số | Giá trị |
|---|---|
| `sort` | `id` (mặc định), `name`, `publishDate`, `metaScore`, `avgPlaytime`, `updatedAt` |
| `desc` | `true` (mặc định) / `false` |
| `missing` | `cardCover`, `portrait`, `gallery`, `developer`, `publisher`, `publishDate`, `metaScore`, `avgPlaytime`, `summary`, `igdbId`, `dlcWithoutBase` |
| `isDlc` | `true` chỉ DLC, `false` chỉ bản gốc, bỏ trống là cả hai |
| `seriesId` | id series |
| `noSeries` | `true` — chỉ bản ghi không thuộc series nào. **Loại trừ** `seriesId` |
| `published` | `true` chỉ bản đã xuất bản, `false` chỉ bản chưa xuất bản |
| `year` | năm phát hành, 1950-2200 |

**Hai cách phân trang**, chọn một:

- **Keyset** (`cursor`) — chế độ thẻ, nút "Tải thêm". Ổn định khi kho bị sửa giữa hai lần lật
  trang.
- **Số trang** (`page`, từ 1) — chế độ danh sách và Preview mode, nhảy thẳng tới trang bất
  kỳ. Kho bị sửa giữa hai lần lật có thể lệch một dòng; với màn hình duyệt của admin, đổi
  lấy khả năng nhảy trang là chấp nhận được. `page < 1` → `422 METADATA_PAGE_INVALID`.
  Cần migration `20261004120000_admin_list_page_offset.sql`.

Gửi cả hai thì `cursor` thắng, `page` bị bỏ qua.

`nextCursor` là **chuỗi thô do server phát ra** — gửi lại nguyên si ở `?cursor=`, **không đọc
cấu trúc bên trong**. Từ khi có sắp xếp đa khoá nó không còn là một `id` mà là cặp *(giá trị
khoá sắp xếp, id)*. Hết dữ liệu thì `null`.

`totalItems` có ở trang đầu của keyset và ở **mọi** trang theo `page`; các trang keyset sau
là `null` — đếm lại ở mỗi lần "Tải thêm" là quét cả bảng để trả về đúng con số vừa trả.

Quy ước sắp xếp: **`NULL` luôn xuống cuối ở cả hai chiều**. "Thiếu dữ liệu" không bao giờ được
trao lên đầu chỉ vì đổi chiều sắp xếp. Tie-break luôn là `id desc`.

Toàn bộ lọc và sắp xếp chạy trong `admin_list_metadata_games` — **không ghép chuỗi filter của
PostgREST ở Host nữa**: cặp keyset và sáu khoá sắp xếp có thể `NULL` không viết được trong
query string.

| Code | HTTP | Khi nào |
|---|---|---|
| `METADATA_SORT_UNKNOWN` | 422 | `sort` ngoài danh sách |
| `METADATA_FILTER_UNKNOWN` | 422 | `missing` ngoài danh sách |
| `METADATA_YEAR_INVALID` | 422 | `year` ngoài 1950-2200 |
| `METADATA_CURSOR_INVALID` | 422 | `cursor` không phải JSON hợp lệ, hoặc thiếu `id` |
| `METADATA_PAGE_INVALID` | 422 | `page` nhỏ hơn 1 |

### 6.5.3. Tiêu chí xuất bản

**Mặc định mọi bản ghi đều được xuất bản.** Bản ghi chỉ không lên được khi admin chủ động gỡ,
hoặc khi nó trượt một tiêu chí đang bật. Hai khái niệm độc lập:

| Field | Ai ghi | Nghĩa |
|---|---|---|
| `manuallyUnpublished` | admin, qua `PUT .../published` | **ý định** — có chủ động gỡ không |
| `isPublished` | trigger DB | **kết quả suy ra** — người dùng có tìm thấy không. Read-only |
| `gateFailures` | trigger DB | danh sách tiêu chí đang trượt. Rỗng = đạt hết |

`isPublished = not manuallyUnpublished and gateFailures rỗng`.

**Không ghi trạng thái suy ra vào cột ý định.** Nếu bản ghi trượt tiêu chí mà ghi thẳng
`manuallyUnpublished = true`, thì sau khi admin bổ sung dữ liệu nó **vẫn không lên** — vì cờ đó
do hệ thống bật chứ không phải admin, và không ai còn phân biệt được nữa.

Bản ghi đang trượt tiêu chí **vẫn nhận được `published: true`**: đó là ý định hợp lệ của admin,
chỉ là chưa có hiệu lực. Chặn ở API sẽ khiến admin không bao giờ gỡ được ý định cũ sau khi bổ
sung dữ liệu. Giao diện khoá công tắc lại và nói rõ lý do.

**Cả hai đường đọc đều bị chặn**, không chỉ một:
- RLS `metadata_games`: `using (is_published)` — client gọi thẳng PostgREST cũng không thấy.
- `search_metadata_games` và `GET /rest/v1/metadata_games?id=in.(…)`: chạy bằng `service_role`
  nên **bypass RLS**, phải lọc `is_published` riêng.

Thiếu một trong hai thì tiêu chí xuất bản chỉ là lời khuyên.

Import một bản ghi chưa xuất bản trả `422 METADATA_GAME_NOT_FOUND` — không tiết lộ là nó tồn tại.

| Code | HTTP | Khi nào |
|---|---|---|
| `PUBLISH_RULES_REQUIRED` | 422 | `PUT /publishing` không có khoá nào |
| `PUBLISHED_REQUIRED` | 422 | `PUT .../published` thiếu `published` |



`PUT` là **thay thế toàn bộ**: field vắng mặt được hiểu là `null`, không phải "giữ nguyên".

Body ghi **không có** `genreIds` / `themeIds` — genre và theme thuộc tiến trình đồng bộ,
read-only ở mọi đường ghi.

Mã lỗi:

| Code | HTTP | Khi nào |
|---|---|---|
| `AUTH_REQUIRED` | 401 | Chưa đăng nhập |
| `ADMIN_REQUIRED` | 403 | Đã đăng nhập nhưng không phải admin, hoặc đang là guest |
| `SERIES_NAME_INVALID` | 422 | Tên rỗng hoặc quá 200 ký tự |
| `SERIES_NAME_TAKEN` | 409 | Trùng tên (không phân biệt hoa/thường); trả kèm bản ghi đang giữ tên |
| `SERIES_NOT_FOUND` | 404 | |
| `SERIES_IN_USE` | 409 | Còn game đang gắn; trả kèm `gameCount`. Dùng **merge** thay vì xoá |
| `SERIES_MERGE_SAME` | 422 | `sourceId == targetId` |
| `GAME_NAME_INVALID` | 422 | Tên rỗng hoặc quá 300 ký tự |
| `GAME_PUBLISH_DATE_INVALID` | 422 | Không đúng `YYYY-MM-DD` |
| `GAME_META_SCORE_RANGE` | 422 | Ngoài 0-100 |
| `GAME_AVG_PLAYTIME_RANGE` | 422 | Số âm |
| `GAME_NOT_FOUND` | 404 | |
| `GAME_IGDB_ID_TAKEN` | 409 | `igdb_id` đã thuộc bản ghi khác |
| `GAME_SERIES_NOT_FOUND` | 422 | `seriesId` không tồn tại |
| `GAME_BASE_NOT_FOUND` | 422 | `baseMetadataGameId` không tồn tại |
| `GAME_BASE_SELF_REFERENCE` | 422 | Game tự làm game gốc của chính nó |
| `GAME_NAME_SIMILAR` | 409 | **Cảnh báo, không chặn cứng.** `details` là danh sách bản ghi trùng tên; gửi lại kèm `confirmDuplicate: true` để vẫn tạo |
| `METADATA_REVISION_REQUIRED` | 422 | `PUT` thiếu `revision` |
| `METADATA_REVISION_CONFLICT` | 409 | Có người sửa trước. `details` là **bản ghi mới nhất** để UI hiện diff |
| `METADATA_UNAVAILABLE` | 503 | Không gọi được Supabase |

Riêng hai mã `GAME_NAME_SIMILAR` và `METADATA_REVISION_CONFLICT` có trường `error.details`:
biết mỗi "thất bại" thì người dùng không hành động được, phải kèm dữ liệu để họ tự quyết.

**Không có endpoint ghi cho `metadata_genres` / `metadata_themes`.** Hai bảng đó do tiến
trình đồng bộ ghi (API sync chưa chốt); không được thêm đường ghi thủ công.

Mọi thao tác ghi đi qua một hàm plpgsql để bản ghi và dòng `metadata_audit_events` nằm
trong **cùng một transaction** — kho dùng chung không có undo.

### 6.5.1. Đẩy thư viện lên kho — đã gỡ

`POST /api/admin/metadata/publish-library` và `POST /api/admin/metadata/push-images` (§6.5.4
cũ) **đã gỡ khỏi Host và Admin Console** (2026-10-04): kho được nạp bằng crawler IGDB (§6.5.5)
và sửa tay trong console, không còn đẩy từ thư viện cá nhân. Hai hàm plpgsql
`admin_publish_library` / `admin_push_game_images` vẫn nằm trong DB nhưng không còn đường gọi.

---

### 6.5.5. Crawler vận hành và audit

Crawler chỉ nhận lệnh qua Host đã kiểm role admin; browser không biết service key,
Vault secret hoặc trực tiếp gọi Edge Function. `POST .../crawler/run` chỉ xếp một
HTTP request bằng `pg_net` rồi trả `202`; trạng thái thật được đọc lại qua `GET`.

Mỗi game crawler thực sự thêm hoặc consume khỏi refresh queue ghi một dòng
`crawler.run_items` với `action: inserted|refreshed`. Dòng này cascade theo run;
retention 30 ngày của `crawler.runs` xoá audit cùng lúc. Run trước migration có thể
không có item-level audit và UI phải nói rõ, không suy diễn từ source snapshot hiện tại.

`runs`, `runs/{runId}/games` và `games` dùng cursor keyset do database phát. Client
chỉ chuyển lại `nextCursor`, không đọc, lọc hay sắp xếp danh sách. Crawler vẫn giữ giới
hạn 100 game discovery, 10 refresh mỗi run và chỉ discovery tới 90% storage budget.

| Code | HTTP | Khi nào |
|---|---|---|
| `CRAWLER_ENABLED_REQUIRED` | 422 | thiếu `enabled` |
| `CRAWLER_DISABLED` | 409 | yêu cầu chạy khi crawler đang tạm dừng |
| `CRAWLER_RUN_ACTIVE` | 409 | đã có lease/run hoạt động |
| `CRAWLER_RUN_NOT_FOUND` | 404 | run detail đã hết retention hoặc không tồn tại |
| `CRAWLER_CURSOR_INVALID` | 422 | cursor không hợp lệ |
| `CRAWLER_SCHEDULER_NOT_READY` | 503 | thiếu Vault URL/secret cần cho trigger |

## 7. Metadata preview

Một endpoint **duy nhất**, nhận `{ name, source }`, resolve provider theo **tên** rồi trả
`GameMetadataPreview` để **điền draft**. ❗ **Không ghi DB** — chỉ khi người dùng bấm Save
(`POST/PUT /api/games`) dữ liệu mới được lưu. Không cần `id` nên dùng được cả khi **thêm game mới**.

| Method | Path | `source` | Nguồn |
|---|---|---|---|
| `POST` | `/api/game-metadata/preview` | `metadata` | IGDB (nếu đã cấu hình): canonical metadata; RAWG fallback cho name/publisher/developer/slug/metascore/avgPlaytime/releaseDate + cover ngang (RAWG không có thì Steam `library_hero.jpg`); Steam cho gallery/card; Gemini đoán `exeName`. `series` = Series đã có khớp collection/franchise IGDB, `seriesName` = tên gợi ý (preview không tạo series). Thiếu IGDB không được làm cả preview thất bại. |
| `POST` | `/api/game-metadata/preview` | `cover` | RAWG `background_image`, dự phòng Steam `library_hero.jpg` — cover ngang cho detail hero |
| `POST` | `/api/game-metadata/preview` | `card-cover` | Steam `header.jpg` — ảnh ngang cho card và queue |
| `POST` | `/api/game-metadata/preview` | `portrait-cover` | IGDB `t_cover_big_2x` — ảnh dọc; không gọi Steam |
| `GET` | `/api/games/{id}/external-links` | — | → `{ hltbUrl, metacriticUrl }` **backend dựng từ name/slug** |

> Frontend **không** hardcode URL HowLongToBeat/Metacritic trên object game (Lovable đang làm vậy — phải bỏ).

---

## 8. Native — ❗ chỉ Local API, Hosted trả `501`

| Method | Path | Ghi chú |
|---|---|---|
| `POST` | `/api/games/{id}/launch` | §10.3 |
| `POST` | `/api/fs/pick-file` | body `{ filter: "exe-or-shortcut" \| "database" \| "json" }` |
| `POST` | `/api/fs/pick-folder` | |
| `POST` | `/api/fs/reveal` | body `{ path }` — mở Explorer |
| `POST` | `/api/games/{id}/backup-folder/generate` | tạo tên duy nhất + ghi `SaveLocation.txt`. **Tạm tắt**: trả `501 FEATURE_DISABLED` khi `capabilities.features.backup = false`; client ẩn mọi UI backup (thư mục backup gốc, thư mục backup từng game, dịch vụ/nút backup của Tray) |
| `GET` | `/api/tray/status` | |
| `POST` | `/api/tray/services/{name}` | bật/tắt 1 trong 3 service |
| `POST` | `/api/tray/sync-images` | Chạy **trong Host**, không cần Tray. Đưa ảnh banner/backdrop/poster của thư viện người đang đăng nhập lên Cloudinary, trả `ImageSyncReport` khi xong (kèm lý do từng ảnh lỗi). Chỉ thay đúng cột ảnh, chỉ khi giá trị chưa đổi trong lúc chạy. `409 IMAGE_SYNC_RUNNING`, `422 INTEGRATION_NOT_CONFIGURED`. Nút hiện theo `capabilities.features.imageSync`. Bình thường không cần bấm: Host tự upload nền sau mỗi lần lưu ảnh (DOMAIN_RULES §12); nút này vét những ảnh upload nền bị lỗi |
| `POST` | `/api/tray/{scan-now\|backup-now\|open-logs\|startup}` | |

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
