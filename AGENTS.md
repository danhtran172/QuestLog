# AGENTS.md — quy tắc bắt buộc khi làm việc trên repo QuestLog

> Áp dụng cho **mọi agent/người** đóng góp code, đặc biệt là phần frontend.
> Đọc cùng với [`docs/DOMAIN_RULES.md`](docs/DOMAIN_RULES.md) và [`docs/API_CONTRACT.md`](docs/API_CONTRACT.md).

---

## 0. Phân vai — không thương lượng

| Vai | Sở hữu | Không được đụng |
|---|---|---|
| **Backend** | Toàn bộ nghiệp vụ, mô hình dữ liệu, hợp đồng API, schema DB, migration | — |
| **Frontend** | Layout, component, animation, design token, accessibility, state UI | Mọi thứ ở cột "Backend sở hữu" |

**Frontend không được định nghĩa domain.** Type TS được **sinh ra từ `openapi.yaml`** của backend, commit vào repo, và **read-only**.

---

## 1. Frontend ĐƯỢC làm
- Toàn quyền quyết định layout, thành phần, hiệu ứng, design token, accessibility, responsive.
- Quản lý state thuần UI: tab đang mở, dialog đang mở, độ rộng panel, trạng thái sidebar.
- Đề xuất thay đổi API — **qua issue**, kèm lý do. Không tự sửa hợp đồng.
- Dùng mock để dựng giao diện, **nhưng mock phải sinh từ type đã generate**, không viết tay object giả.

## 2. Frontend KHÔNG được làm

| Cấm | Lý do |
|---|---|
| Sửa `src/lib/api/**` (file sinh tự động) | Là hợp đồng, backend sở hữu — CI chặn |
| Thêm/đổi tên/xoá field domain, hoặc thêm giá trị enum | Enum đến từ DB, không phải từ UI |
| Cài đặt logic **lọc / sort / ngưỡng / thứ hạng** ở client | Đã chuyển hết về server (DOMAIN_RULES §5, §7) |
| Đổi đơn vị (giây ↔ phút ↔ giờ) ở tầng dữ liệu | Nguồn bug âm thầm số 1 (M1). Chỉ format lúc **hiển thị** |
| Hardcode danh sách `systemKey` để suy ra hành vi | Dùng `displayName`/`iconData`/`colorCode` từ API (DOMAIN_RULES §14) |
| Hardcode URL ngoài (HowLongToBeat, Metacritic) | Backend dựng, xem API_CONTRACT §7 |
| Ghi `localStorage` cho `language`, `themeMode`, hay bất kỳ Settings nào | Thuộc config dùng chung với Tray (DOMAIN_RULES §15) |
| Giữ lại dữ liệu giả (`DEMO_GAMES` hoặc tương đương) sau khi màn hình đã nối API | Phải xoá sạch |
| Nuốt lỗi API (catch rồi bỏ qua) | Mọi lỗi phải hiển thị được |
| Gọi thẳng Supabase từ client cho dữ liệu nghiệp vụ | Đi qua API. Client chỉ dùng Supabase SDK cho **auth** |
| Thêm dependency gọi mạng ra ngoài | App chứa credential người dùng |
| Đoán trạng thái native (launch/scan/tray) | Hỏi `GET /api/capabilities`, ẩn nút nếu `false` |

---

## 3. Definition of Done cho mỗi màn hình

Một màn hình chỉ được coi là xong khi **cả 6 điều** sau đúng:

1. Mọi field hiển thị **truy vết được** về một field trong `openapi.yaml`.
2. Không còn dữ liệu giả nào trong màn hình đó.
3. Mọi mutation gọi API thật; lỗi hiển thị đúng, không nuốt.
4. Chuỗi giao diện lấy từ i18n; **giá trị dữ liệu** (tên Progress/Ownership/Genre/Theme) lấy `displayName` từ backend.
5. Không có phép lọc/sort/ngưỡng nào chạy ở client.
6. Dòng tương ứng trong `docs/PARITY_CHECKLIST.md` đã đánh dấu.

---

## 4. Nguồn thiết kế

Bản Lovable (`lovable-project-49c970a2.zip`) là **nguồn tham chiếu thiết kế**, không phải nguồn code.

- ✅ Lấy: bố cục, hệ màu, spacing, luồng tương tác, ý tưởng component, cấu trúc màn hình.
- ❌ Không lấy: `src/` nguyên khối, `src/data/games.ts`, `src/lib/filters.ts`, `src/lib/store.tsx`, SSR/Nitro/Cloudflare target, telemetry của editor Lovable.
- Mọi logic nghiệp vụ trong bản Lovable đều là **bản sao không khớp** với hệ thống thật — xem 40 mismatch ở `UI_MIGRATION_PLAN.md` §4. Không copy sang.

---

## 5. Kiểm tra trước khi merge

```bash
npm run typecheck && npm run lint && npm run test
```

CI chặn nếu:
- `src/lib/api/**` bị sửa tay (so với bản sinh từ `openapi.yaml`).
- Xuất hiện chuỗi `localStorage` gắn với `language` / `theme` / `settings`.
- Còn import từ file dữ liệu giả.
