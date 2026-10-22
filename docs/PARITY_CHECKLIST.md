# QuestLog — PARITY CHECKLIST

> **Cổng bắt buộc trước khi xoá bất kỳ phần nào của giao diện WPF cũ.**
> Mỗi dòng phải có trạng thái — **không dòng nào được để trống**:
>
> | Ký hiệu | Nghĩa |
> |---|---|
> | ✅ | Khớp — đã port và verify cho cùng kết quả |
> | 🟨 | **FE + mock đã cài đúng, CHỜ backend thật** — chưa được tính là parity |
> | 🔁 | Đổi có chủ ý — đã được duyệt, ghi rõ lý do |
> | ⏸ | Hoãn — phải có issue kèm theo |
> | ⬜ | Chưa làm |

> ⚠️ **Đừng nhầm 🟨 với ✅.** 🟨 nghĩa là quy tắc đã được cài trong `MasterPlay.Web/src/mocks/library.ts`
> và verify bằng cách gọi API mock thật. Nó **chưa** chứng minh backend thật cho cùng kết quả —
> đó chính là việc của cổng parity ở Giai đoạn 4.

Cập nhật lần cuối: 2026-09-04

---

## A — Tính năng Lovable còn thiếu (xử lý được)

| # | Tính năng | Điều khoản | Màn hình | TT |
|---|---|---|---|---|
| A1 | Filter Genre/Theme (OR giữa 2 nhóm) | DOMAIN §5.7 | Library | 🟨 Host nhận `genreIds`/`themeIds` (+ `progressKeys`/`ownershipKeys` §5.8), unit test `LibraryFilterTests`; FilterDialog có chip + tìm ở server. Chưa đối chiếu trên DB thật |
| A2 | Card field visibility động (`cardHints`) | DOMAIN §17 | Library | 🟨 |
| A3 | DLC (`isDlc`, `baseGameId`) | DOMAIN §13 | Editor | ✅ Editor dùng selector base game từ API; backend validate game gốc và chặn tự tham chiếu |
| A4 | Lịch sử hoàn thành nhiều lần (`GameFinish[]`) | DOMAIN §9 | Editor/Detail | ✅ Detail hiển thị toàn bộ lịch sử; Editor sửa riêng tháng/năm từng lần qua API thật |
| A5 | `posterPath` + nút sync riêng | DOMAIN §12 | Editor | ✅ Sync riêng qua `/api/games/{id}/sync/portrait-cover`, IGDB only; Detail tab Files mở được image-only editor |
| A6 | Gemini đoán tên executable | API §7 | Editor | ✅ Metadata sync gọi Gemini ở backend và trả `exeName`; lỗi được trả về UI |
| A7 | Mở HowLongToBeat/Metacritic theo slug | API §7 | Editor | ✅ URL do backend dựng; Editor và Detail dùng nút icon mở URL trả từ API |
| A8 | Generate Backup Folder | API §8 | Editor | ✅ Backend tạo tên thư mục duy nhất, ghi `SaveLocation.txt`; Editor cập nhật path và hiển thị lỗi |
| A9 | Mở Save/Backup folder (2 nút riêng) | API §8 | Detail | ✅ Hai nút icon gọi `/api/fs/reveal`; tự ẩn khi capability không hỗ trợ và hiển thị lỗi API |
| A10 | Cảnh báo thiếu folder trên card | DOMAIN §11 | Library | 🟨 |
| A11 | Trạng thái "đang chơi" thật (SSE) | DOMAIN §11 | Library | ✅ |
| A12 | Promote to Next Play | DOMAIN §8.4 | Card/Queue | ⬜ |
| A13 | Quy tắc nghiệp vụ kéo-thả Queue | DOMAIN §8.2 | Queue | ✅ API thật nhận `gameIds[]`, cập nhật Progress + PlayOrder trong transaction |
| A14 | Chuẩn hoá + persist thứ tự queue | DOMAIN §8.3 | Queue | ✅ `GET /api/queue` chuẩn hoá và persist thứ tự |
| A15 | Quản lý Progress/Ownership (tên/icon/thứ tự) | API §6 | Settings | ⬜ |
| A16 | Icon danh mục lưu DB | API §6 | Settings | ⬜ |
| A17 | Điều hướng tay cầm + Console mode | — | Toàn cục | ✅ Gamepad API điều khiển toàn app; F10 hoặc LB+RB+A tự mở Console fullscreen phủ cả vùng taskbar; F11 dùng cùng fullscreen native; Console chặn L3/Menu đổi sang desktop, bỏ phím L và hướng dẫn đổi layout/Gallery; giữ Esc/B để thoát và khôi phục cửa sổ; Tab/A mở Detail; Enter/X Play; gợi ý Shift/D-pad Up có nhãn theo ngôn ngữ, popup đóng bằng D-pad Down hoặc action khác, xếp dọc ở góc phải dưới. Verify: 25 test web + typecheck/build; kiểm tra native từ Normal/Maximized và fullscreen có sẵn đạt; chưa kiểm tra tay cầm vật lý; repo chưa có script lint |
| A18 | Ctrl+K focus search | — | Library | 🟨 |
| A19 | Scroll to top khi đổi trang | DOMAIN §16 | Library | 🟨 |
| A20 | Localize giá trị dữ liệu (`displayName`) | DOMAIN §14 | Toàn cục | 🟨 |
| A21 | Launch game | DOMAIN §10.3 | Card/Detail | ✅ Card/Detail gọi endpoint launch thật; backend ưu tiên launch target rồi mới dùng process đã phát hiện |
| A22 | File dialog cho Config/Database path | API §8 | Settings | ⬜ |
| A23 | Cancel trong editor revert từ DB | — | Editor | ✅ Form tạo lại draft từ `GET /api/games/{id}`, Cancel bỏ toàn bộ thay đổi chưa lưu |
| A24 | Settings Cancel khôi phục cả theme | — | Settings | ⬜ |
| A25 | Đóng popup con trước khi đóng modal (Esc) | — | Toàn cục | ⬜ |
| A26 | Game Detail + Editor mở từ mọi view | API §4 | All/Planning/Finished/Queue | ✅ Đã verify card, double-click queue, View → Edit → PUT thật; Game Information có form gọn riêng và lưu qua PUT thật |
| A27 | Add Game ghi DB thật | API §4 | Toàn cục | ✅ Form dùng catalog/taxonomy từ API, validate tên và gọi `POST /api/games` |
| A28 | Tag Upcoming trên card | API §2.3 | Library | ✅ Backend trả `isUpcoming` từ `publishDate > ngày hiện tại`; card chỉ hiển thị ribbon |
| A29 | Console view playable + recent play | DOMAIN §4, §7.2 | Console | ✅ Tray ghi `lastPlayedAt` theo cạnh bắt đầu phiên; API lọc `canLaunch` và sort recent phía server; vào/thoát CM chuyển Console/khôi phục tab cũ |
| A30 | Quản lý crawler IGDB | API §6.5.5 | Admin Console | ✅ Trạng thái, pause/resume, run-now, lịch sử keyset và game audit qua API thật; quota/ưu tiên vẫn ở Supabase |
| A31 | Preview mode duyệt metadata | API §6.5.2, §6.5.3 | Admin Console | 🟨 Popup 2 cột (Tổng quan / Ảnh & xuất bản), search + bộ lọc + chuyển bản ghi ←/→ theo `page` server-side, tô ô thiếu (đỏ nếu đang chặn xuất bản theo `gateFailures`), nút Duyệt = `PUT .../published`. Verify: typecheck/build/test web, SQL offset chạy trên PGlite; **chưa** kiểm UI với Host mới + migration `20261004120000` |
| A32 | Danh sách metadata: cột trạng thái xuất bản + phân trang số trang | API §6.5.2 | Admin Console | 🟨 Cột Trạng thái; trang đầu/trước/số/sau/cuối, nhảy trang, chọn 25/50/100 mỗi trang. Chế độ thẻ giữ keyset "Tải thêm". Cần migration `20261004120000` |

## B — Quyết định sản phẩm/kiến trúc

| # | Vấn đề | TT | Kết luận |
|---|---|---|---|
| B1 | Mô hình host | 🔁 | **Chốt**: WebView2 (desktop, Local API) + Hosted API (web), cùng codebase C#, data Supabase |
| B2 | `avgPlayTime` giờ hay dải | 🔁 | **Chốt (giả định)**: giữ **số giờ** là nguồn sự thật, backend trả kèm `avgPlaytimeBand` |
| B3 | Thang `excited` | 🔁 | **Chốt**: 0–11 bước 0.5; `11` = Peak Score, hiển thị `10+`. Lovable ép 0–5 → đã sửa trong FE mới |
| B4 | `finishedIn` vs `GameFinish[]` | 🔁 | **Chốt (giả định)**: bỏ `finishedIn` khỏi UI; filter khớp **ANY**, sort dùng **MAX** |
| B5 | Progress/Ownership enum vs bảng | 🔁 | **Chốt (giả định)**: thêm `SystemKey` bất biến, bỏ toàn bộ magic number |
| B6 | Ngôn ngữ mặc định | 🔁 | **Chốt**: `en-US` |
| B7 | Điều hướng tay cầm | 🔁 | **Chốt 2026-08-29**: Gamepad API luôn bật; Console fullscreen là surface bổ sung, Shell chỉ giữ cầu nối fullscreen |
| B8 | Tray tách rời hay gộp | 🔁 | Tray giữ là tiến trình WinForms riêng; panel web chỉ là control panel qua API |
| B9 | Bảo mật API | 🔁 | Auth gate + Supabase token validation + Desktop guest token chỉ qua WebView2/loopback + Account UI đã có; Google sign-up/sign-in (Host PKCE, chọn username), quên mật khẩu và xác thực lại bằng mã email đã có (2026-10-04); còn Postgres cutover, MFA và vault credential |
| B10 | RAWG/Gemini key lộ | 🔁 | Admin Console Integrations lưu replacement key trong Vault và Host không còn hardcode key. ⚠️ *Người dùng vẫn phải thu hồi/rotate hai key đã lộ trong git history* |
| B11 | Màn Restore game đã disable | 🔁 | **Chốt (giả định)**: thêm — endpoint `GET /api/games/disabled` đã có trong hợp đồng |
| B12 | Phân trang 30/trang | 🔁 | **Chốt (giả định)**: giữ 30/trang ở v1 để đo parity |

> Các mục ghi *(giả định)* là quyết định tôi tự chốt để không chặn tiến độ. Lật lại lúc nào cũng được — sửa ở đây và ở `DOMAIN_RULES.md` trước, code sau.

## T — Tính năng thừa của Lovable

| # | Tính năng | TT |
|---|---|---|
| T1 | Dashboard | 🔁 Giữ — `GET /api/dashboard/stats` |
| T2 | Notification center | 🔁 Giữ — cần lưu lịch sử event |
| T3 | Bảng chi tiết từng service | 🔁 Giữ — expose từ 3 service |
| T4 | Scan Now / Backup now thủ công | 🔁 Giữ |
| T5 | Test connection Cloudinary | 🔁 Giữ |
| T6 | Ô nhập RAWG key | 🔁 Giữ (chỉ trả cờ `configured`, không trả key) |
| T7 | Theme "System" + "Jungle" | ✅ `themeMode` 4 giá trị, lưu qua API/config |
| T8 | Loading skeleton | ✅ Đã có trong FE mới |
| T9 | Empty state có Reset | ✅ Đã có trong FE mới |
| T10 | Badge đếm filter | 🟨 `activeFilterCount` từ backend |
| T11 | Popover phím tắt | ✅ Đã có trong FE mới |
| T12 | Toast (sonner) | 🔁 Giữ — nhưng xác nhận huỷ/xoá vẫn là dialog chặn |
| T13 | Icon cho Series | 🔁 Giữ — thêm cột `Series.IconData` (S2) |
| T14 | "Has custom launch" | 🔁 **Đổi luồng**: chọn **exe hoặc shortcut** → `launchTarget` (DOMAIN §10) |
| T15 | Settings 5 section có nav trái | 🔁 Giữ |
| T16 | Layout responsive | 🔁 Giữ (web cần, desktop vô hại) |
| T17 | SSR wrapper + telemetry Lovable | ✅ **Bỏ** — không tồn tại trong FE mới |
| T18 | ~50 component shadcn chưa dùng | ✅ **Bỏ** — FE mới chỉ thêm component thực dùng |
| T19 | Dependencies thừa | ✅ **Bỏ** — FE mới chỉ 8 dependency runtime |

## M — Mismatch phải xử lý

### M1–M15 · Mô hình dữ liệu

| # | Hạng mục | Điều khoản | TT |
|---|---|---|---|
| M1 | Đơn vị playtime (giây ↔ phút) | DOMAIN §1 | 🟨 API dùng `totalPlaytimeSeconds` |
| M2 | `avgPlayTime` giờ ↔ dải | DOMAIN §1, §5.5 | 🟨 trả cả `avgPlaytimeHours` + `avgPlaytimeBand` |
| M3 | Thang `excited` | DOMAIN §2.2 | ✅ FE/BE dùng 0–11 bước 0.5; Peak Score hiển thị `10+` |
| M4 | `score` sentinel | DOMAIN §2.1 | ✅ Legacy `999` được migrate thành `11`; Peak Score loại khỏi average/critic comparison |
| M5 | `finishedIn` string ↔ number | DOMAIN §9 | ⬜ |
| M6 | `series` entity ↔ string | API §2.2 | 🟨 |
| M7 | `genres`/`themes` entity ↔ string[] | API §2.2 | ✅ Detail/Editor đọc và ghi entity qua API thật |
| M8 | Progress/Ownership | DOMAIN §3 | 🟨 dùng `systemKey` + `sortOrder`; Detail đổi trực tiếp bằng chip từ catalog API, game mới mặc định Pending + Wishlist ở backend |
| M9 | Ảnh thứ 3 | DOMAIN §12 | ✅ `posterPath` từ IGDB có trong Detail/Editor và persist qua PUT |
| M10 | `isDlc`/`baseGameId` | DOMAIN §13 | ⬜ |
| M11 | `ready` là kết quả scan | DOMAIN §11 | 🟨 read-only ở FE |
| M12 | `createAt`/`playDiary`/`detectedProcess` | — | ⬜ |
| M13 | Config ↔ runtime của library path | API §9 | ⬜ |
| M14 | `themeMode` | DOMAIN §15 | ✅ migrate bool cũ sang enum 4 giá trị |
| M15 | RAWG key | API §6.5.0 | 🔁 Admin Console + Vault-backed Host runtime; cần migration/deploy và user rotate key cũ |

### M16–M27 · Thuật toán (nguy hiểm nhất — sai âm thầm)

| # | Hạng mục | Điều khoản | TT |
|---|---|---|---|
| M16 | Ngưỡng MetaScore (Low 50–69, **không phải 60**) | DOMAIN §5.5 | 🟨 backend trả `metaScoreBand` |
| M17 | Reverse filter khi chưa bật filter → trả **toàn bộ** | DOMAIN §5.9 | ⬜ chờ filter |
| M18 | Reverse sort → đảo **toàn bộ list sau sort** | DOMAIN §7.3 | 🟨 đã verify qua API |
| M19 | Default sort tab All (6 khoá) | DOMAIN §7.2 | 🟨 |
| M20 | Default sort tab Planning (4 khoá) | DOMAIN §7.2 | 🟨 |
| M21 | Default sort tab Finished (5 khoá) | DOMAIN §7.2 | 🟨 đã verify score↓ |
| M22 | Thứ hạng Progress → `sortOrder` từ DB | DOMAIN §3.2 | 🟨 |
| M23 | Thứ hạng Ownership → `sortOrder` từ DB | DOMAIN §3.2 | 🟨 |
| M24 | Cờ "None" riêng cho AvgLength/MetaScore | DOMAIN §5.4 | ⬜ chờ filter |
| M25 | Filter playtime: giờ × 3600 → giây | DOMAIN §5.3 | ⬜ chờ filter |
| M26 | Search năm: **bằng đúng năm**, không `includes` | DOMAIN §6.4 | 🟨 đã verify: `02` → 0 kết quả |
| M27 | Search initials: **contains**, đủ 6 scope | DOMAIN §6.2–6.3 | 🟨 đã verify: `bg` → Baldur's Gate |

### M28–M40 · Hành vi

| # | Hạng mục | Điều khoản | TT |
|---|---|---|---|
| M28 | Finish ghi lịch sử `GameFinish` | DOMAIN §9.2 | ✅ `POST /api/games/{gameId}/finishes` cập nhật Score + Progress Done + thêm lịch sử trong transaction |
| M29 | Kéo-thả Queue đổi cả ProgressId | DOMAIN §8.2 | ✅ Verify API thật: Playing giữ nguyên, 3 non-playing đầu → NextPlay |
| M30 | Sync metadata gọi RAWG thật | API §7 | ⬜ |
| M31 | Upload ảnh lên Cloudinary thật | DOMAIN §12 | ✅ `POST /api/tray/sync-images` (Host, theo chủ thư viện) + lệnh `--sync-cloudinary-images`; chạy thật 2026-10-04: 112 ảnh lên `QuestLog/`, lỗi trả kèm lý do |
| M32 | Disable game ghi DB | API §4 | ⬜ |
| M33 | Xoá taxonomy có transaction | API §6 | ✅ `TaxonomyEndpoints.Delete` + xoá nhóm theme đều trong một transaction |
| M36 | Nhóm theme + taxonomy theo chủ sở hữu | spec 2026-10-04-theme-groups | ✅ API + ManageTags (kéo thả) + picker theo nhóm + Admin Console › Taxonomy |
| M34 | Launch game thật | DOMAIN §10.3 | ⬜ |
| M35 | `initExeName` → `launchTarget` | DOMAIN §10 | 🟨 có trong hợp đồng |
| M36 | Ngôn ngữ lưu config (tray dùng chung) | DOMAIN §15 | ⬜ |
| M37 | Theme lưu config | DOMAIN §15 | ✅ GET/PUT `/api/settings`, không dùng localStorage |
| M38 | Card dùng ảnh ngang Steam | DOMAIN §12 | ✅ Dùng `bannerPath`, cùng khung 270×126.2 |
| M39 | Màu MetaScore chip theo dải 90/80/70/50 | DOMAIN §5.5 | 🟨 map từ `metaScoreBand` |
| M40 | Refresh xuyên tiến trình → SSE | API §9 | ⬜ |
| M41 | Chất liệu User Score trên Gallery theo band backend | DOMAIN §2.1 | ✅ map từ `scoreMaterialBand` |

---

## Cổng ra bắt buộc

1. **Không dòng nào còn ⬜, và không dòng nào còn 🟨** (🟨 phải chuyển thành ✅ sau khi backend thật chạy).
2. Integration test chạy song song WPF-path và API-path trên **cùng DB**, so khớp `id[]` cho **≥50 tổ hợp** filter/sort/tab.
3. Chạy song song 2 giao diện trên **DB thật ≥1 tuần**, đối chiếu số lượng kết quả ở 20 tổ hợp filter thường dùng.
4. Tag release cuối của bản WPF để có đường lùi.
