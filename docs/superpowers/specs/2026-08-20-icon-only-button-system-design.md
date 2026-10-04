# Thiết kế hệ action button icon-only cho QuestLog

**Ngày:** 2026-08-20  
**Trạng thái:** Chờ duyệt triển khai

## 1. Mục tiêu

Chuẩn hóa toàn bộ action button trong QuestLog thành icon-only có tooltip, dùng cùng một ngôn ngữ glass nhẹ như nút đóng Detail Card. Thay đổi chỉ tác động cách trình bày và khả năng truy cập; không thay đổi hành vi, nghiệp vụ, API hay dữ liệu.

## 2. Phạm vi

### Chuyển sang icon-only

- Hành động toàn cục: Sort, đảo chiều sort, Filter, Add game, More, Help, Tray.
- Hành động trên game: Play, Details, Edit, mở tệp/thư mục.
- Hành động form/dialog: Save, Cancel, Reset, Retry, Delete, Test connection và các thao tác tương đương.
- Hành động bảng/queue: Edit, Reset order, các nút thao tác hàng.
- Phân trang Prev/Next chuyển sang mũi tên; trạng thái trang hiện tại vẫn là văn bản thường.
- Nút đóng dialog tiếp tục dùng icon-only và trở thành mẫu vật liệu tham chiếu.

### Giữ chữ

- Navigation: All, Planning, Finished, Play Queue và các tab điều hướng khác.
- Detail page navigation `1 / 2`.
- Settings sidebar và Manage Tags tabs.
- Select/dropdown trigger vì cần hiển thị giá trị đang chọn.
- Selection chip/toggle biểu diễn giá trị như MetaScore band, Avg band, Progress, Ownership, theme và language.
- Nội dung trạng thái, badge và pill không phải action button.

## 3. Kiến trúc component

Tạo primitive `IconButton` dùng chung trong lớp UI:

- Nhận `label` bắt buộc, icon, `tone`, `size`, kiểu button và toàn bộ thuộc tính button chuẩn.
- Tự gắn `aria-label` từ `label`.
- Tự bọc Radix Tooltip; tooltip xuất hiện bằng hover và keyboard focus.
- Dùng `TooltipProvider` dùng chung để thống nhất delay và tránh mỗi nút tự quản lý tooltip.
- Hỗ trợ nút disabled: vẫn hiển thị tooltip giải thích nhãn, trong khi hành động bị khóa.
- Không thêm dependency; dự án đã có `@radix-ui/react-tooltip` và `lucide-react`.

Các action button đang viết trực tiếp bằng `<button>` hoặc component `Button` sẽ được chuyển dần sang `IconButton`. Navigation và selection control giữ component hiện tại.

## 4. Hệ hình ảnh

### Vật liệu

- Nền glass trung tính, trong vừa phải; không dùng mảng màu semantic đậm trên toàn nút.
- Highlight trong mảnh, shadow ngắn và border nhẹ như nút đóng Detail Card.
- Màu semantic chỉ áp dụng cho icon:
  - `neutral`: thao tác thông thường.
  - `play`: xanh lá.
  - `edit`: xanh lam.
  - `add`: xanh ngọc.
  - `open`: vàng dịu.
  - `danger`: đỏ.
- Hover tăng độ rõ vừa phải; không scale, nhảy hoặc đổi thành mảng màu đậm.
- Active dịch xuống tối đa 1 px như hệ glass hiện tại.

### Kích thước

- `compact`: 28 × 28 px cho dialog close và vùng rất dày.
- `default`: 32 × 32 px cho toolbar, card, footer và form action.
- Icon 14 px ở compact, 15–16 px ở default.
- Không đổi kích thước navigation/selection control nằm ngoài phạm vi.

### Tooltip

- Dùng surface của QuestLog, chữ ngắn từ i18n, cỡ 11–12 px.
- Delay khoảng 300 ms; xuất hiện nhanh hơn khi di chuyển giữa các nút liên tiếp.
- Hiển thị shortcut trong tooltip khi hành động đã có shortcut.
- Tooltip render qua portal để không bị cắt bởi dialog/card `overflow-hidden`.

## 5. Accessibility và hành vi

- Mỗi action button phải có tên truy cập rõ ràng qua `aria-label`.
- Tooltip mở khi hover và focus; Escape đóng tooltip.
- Focus ring dùng token primary, không bị pseudo-element glass che khuất.
- Nút submit vẫn giữ `type="submit"`; icon-only không được thay đổi luồng submit/cancel.
- Nút destructive giữ xác nhận hiện có; không thêm hoặc bỏ bước xác nhận.
- Trạng thái disabled đủ tương phản và không nhận click.

## 6. Token và Tailwind

- Đăng ký đúng các color token cần dùng với Tailwind v4 để các utility như foreground, border và primary thực sự được sinh CSS.
- Giữ CSS variable làm nguồn màu duy nhất cho light/dark theme.
- Loại bỏ các class tone phủ nền mạnh khỏi action button sau khi consumer đã chuyển sang `IconButton`.
- Không sửa token/domain do backend sở hữu và không đụng `src/lib/api/**`.

## 7. Trình tự triển khai

1. Sửa đăng ký token Tailwind và xác minh utility được sinh.
2. Thêm Tooltip primitives, `TooltipProvider` và `IconButton`.
3. Chuyển toolbar/header và pagination.
4. Chuyển Game Card, Detail, Editor và Queue.
5. Chuyển Settings, Filter, Manage Tags và Tray; giữ nguyên navigation/selection chip.
6. Xóa style action button cũ khi không còn consumer.

Mỗi bước phải build được độc lập và không thay đổi hành vi.

## 8. Kiểm thử và tiêu chí hoàn thành

- Chạy `npm run typecheck` và `npm run build` sau từng lát triển khai.
- Kiểm tra light/dark theme ở Gallery, Queue và tất cả dialog.
- Kiểm tra hover, focus bằng Tab, kích hoạt bằng Enter/Space và Escape đóng tooltip.
- Kiểm tra tooltip không bị cắt và có nội dung i18n đúng.
- Kiểm tra Save/Cancel/Delete/Retry/Play/Edit vẫn gọi đúng handler cũ.
- Quét source: không còn action button hiển thị text; navigation, select và selection chip vẫn giữ text.
- Không có thay đổi trong backend, OpenAPI hay generated API types.

## 9. Ngoài phạm vi

- Không đổi layout tổng thể, luồng điều hướng hoặc chức năng.
- Không thay đổi badge, status pill, input, select hay switch thành icon-only.
- Không tự gán icon cho domain value dựa trên `systemKey`.
- Không sửa title bar native trong thay đổi này.
