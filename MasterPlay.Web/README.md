# MasterPlay.Web — giao diện QuestLog (bản mới)

Frontend thay thế cho `MasterPlay.UI` (WPF). SPA tĩnh, dùng chung cho **cả bản web lẫn bản desktop**:

- **Desktop**: shell WPF + WebView2 trỏ vào Local API (`http://127.0.0.1:{port}`)
- **Web**: deploy tĩnh, gọi Hosted API

## Chạy

```bash
npm install
npm run dev
```

Mở `http://localhost:5173`.

Khi chưa có backend, **mock API chạy ngay trong Vite dev server** (`mock-api-plugin.ts`) —
không cần Service Worker, là HTTP thật nên xem được trong tab Network của DevTools.

| Lệnh | Việc |
|---|---|
| `npm run dev` | Dev server + mock API |
| `npm run build` | Build production (**không kèm mock**) |
| `npm run typecheck` | Kiểm tra kiểu |
| `npm run gen:api` | **Sinh lại type từ `../docs/openapi.yaml`** |

## Quy tắc bắt buộc

Đọc [`../AGENTS.md`](../AGENTS.md) trước khi sửa bất cứ thứ gì. Tóm tắt:

1. **`src/lib/api/types.gen.ts` là file SINH TỰ ĐỘNG — không sửa tay.**
   Thiếu field thì sửa `docs/openapi.yaml` (backend sở hữu) rồi chạy `npm run gen:api`.
2. **Không cài logic lọc/sort/ngưỡng ở client.** Toàn bộ chạy ở server
   (xem [`../docs/DOMAIN_RULES.md`](../docs/DOMAIN_RULES.md)). Đây là cách xoá vĩnh viễn
   nhóm mismatch M16–M27 thay vì phải đồng bộ hai bản cài đặt mãi mãi.
3. **Không đổi đơn vị ở tầng dữ liệu.** API luôn dùng giây cho playtime; chỉ đổi khi hiển thị.
4. **Không hardcode `systemKey` để suy ra hiển thị.** Dùng `displayName`/`colorCode`/`iconData`
   backend trả về — người dùng sửa được tên và màu trong Catalog Settings.
5. **Không lưu `language`/`themeMode`/Settings vào `localStorage`** — thuộc config dùng chung với Tray.

## Cấu trúc

```
src/
  lib/
    api/types.gen.ts   SINH TỰ ĐỘNG từ openapi.yaml — KHÔNG SỬA TAY
    api/domain.ts      tái xuất type + hằng số dùng chung
    api/client.ts      fetch wrapper, JWT, shape lỗi
    format.ts          format hiển thị theo DOMAIN_RULES (Peak Score 11, giây→h/m)
    i18n.tsx           CHUỖI GIAO DIỆN (không dịch giá trị dữ liệu)
    useGames.ts        TanStack Query hook
  components/
    Pills.tsx          Progress/Ownership/Excited/Score/MetaScore
    GameCard.tsx       card 270×126.2 (khớp chính xác WPF)
  mocks/               ⚠️ XOÁ khi có backend thật
    data.ts            dữ liệu giả
    library.ts         cài đặt DOMAIN_RULES — đặc tả chạy được cho backend
mock-api-plugin.ts     ⚠️ XOÁ khi có backend thật
```

## Đã sửa so với bản Lovable

| Lỗi ở bản Lovable | Bản này |
|---|---|
| `excited` ép về thang 0–5 | Thang **0–11 bước 0.5**, `11 → 10+` |
| MetaScore chip dùng ngưỡng `≥75`/`≥50` | Dùng `metaScoreBand` backend trả về (ngưỡng **90/80/70/50**) |
| Progress/Ownership hardcode 6 literal + bảng màu cứng | Lấy `displayName`/`colorCode`/`iconData` từ DB |
| Thiếu Ctrl+K, thiếu scroll-to-top khi đổi trang | Đã có |
| Ngôn ngữ mặc định `vi` | `en-US` |
| Lọc/sort chạy ở client (sai so với hệ thống thật) | Chuyển hết về server |

## Chưa làm

Filter dialog · Game detail/editor · Play Queue · Settings · Manage tags · Tray panel · Dashboard.
Auth cơ bản (đăng ký, đăng nhập, đổi mật khẩu bắt buộc và đăng xuất) đã có; Google/MFA
và màn hình quản trị tài khoản là các lát triển khai tiếp theo.
Thứ tự port: xem [`../docs/UI_MAP.md`](../docs/UI_MAP.md) §6.
