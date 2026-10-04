# QuestLog Landing

Trang giới thiệu public của QuestLog. Astro, xuất HTML tĩnh, deploy lên Cloudflare Pages.

Project này **tách riêng** khỏi `MasterPlay.Web` (giao diện app desktop):
- Không dùng chung `package.json`, không import code từ app.
- Quy tắc trong `AGENTS.md` (type sinh từ `openapi.yaml`, cấm gọi mạng ra ngoài...) áp dụng cho app, không áp dụng cho landing.
- Design token được **chép** từ `MasterPlay.Web/src/index.css` sang `src/styles/global.css`. App đổi palette thì cập nhật tay.

## Chạy local

```bash
npm install
npm run dev        # http://localhost:4321
npm run build      # astro check + build ra dist/
npm run preview
```

Tắt telemetry ẩn danh của Astro trên máy bạn (một lần):

```bash
npx astro telemetry disable
```

## Cấu trúc

```
src/
  i18n/ui.ts             Toàn bộ chuỗi hiển thị (vi, en)
  site.ts                Link tải xuống / trang phát hành
  shots.ts               Tra ảnh chụp app theo ngôn ngữ
  assets/app/{vi,en}/    Ảnh chụp app thật, mỗi ngôn ngữ một bộ
  assets/app/wall.webp   Trang đầu thư viện (30 game), làm nền hero
  assets/brand/          Tấm bản đồ kho báu (lấy từ màn hình đăng nhập của app)
  styles/global.css      Design token + Tailwind
  scripts/landing.ts     Tuyến đường theo cuộn, hiệu ứng xuất hiện, header
  layouts/Base.astro     <head>, SEO, hreflang, Open Graph
  components/            Header, Hero, QuestTracker, Showcase, Journey, Stop,
                         ConsoleBand, TrayCard, Download, Footer, Landing
  pages/index.astro      /      (tiếng Việt, mặc định)
  pages/en/index.astro   /en/   (English)
public/
  _headers               Header bảo mật + cache cho Cloudflare Pages
  og.jpg                 Ảnh chia sẻ 1200×630
```

## Ý tưởng thiết kế

Trang là một **tuyến đường trên bản đồ kho báu** (lấy từ tấm bản đồ ở màn hình đăng nhập của app).
Mỗi chặng là một trạng thái tiến độ thật trong app — **Planned → Nextplay → Playing → Done** —
kèm ảnh màn hình làm việc đó. Đường đi tự vẽ theo vị trí cuộn và kết thúc ở dấu **X** = nút tải.

- Chỉ có theme tối: app đang khoá theme tối và mọi ảnh chụp đều nền tối.
- Font: Big Shoulders Display (tiêu đề), Manrope (nội dung, giống app), JetBrains Mono (nhãn kiểu HUD).
  Cả ba đều tự host qua Fontsource, có bộ ký tự tiếng Việt.
- Không có JS trang vẫn đọc đủ: mốc sáng sẵn, đường hiện nét đứt.
- `prefers-reduced-motion`: tắt nền trôi, nghiêng 3D và hiệu ứng xuất hiện.

## Ảnh chụp app

Ảnh trong `src/assets/app/` chụp từ app thật (thư viện của tài khoản chủ dự án), độ phân giải 2×,
chế độ chỉ đọc. Đã chỉnh trước khi đưa lên trang public:

- Xoá nhãn sở hữu "Picklocked" khỏi ảnh nền hero và cắt bỏ cột Sở hữu khỏi ảnh Play Queue.
- Cắt ảnh danh sách/Play Queue cho vừa bố cục; ảnh nền hero cắt đúng 6 hàng card để lặp không lộ đường nối.

Ảnh bìa game trong ảnh chụp thuộc bản quyền của các nhà phát hành.

## Biến môi trường

Xem `.env.example`.

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `PUBLIC_SITE_URL` | `https://questlog.pages.dev` | Domain chính, dùng cho canonical/hreflang |
| `PUBLIC_DOWNLOAD_URL` | GitHub Releases `latest` | Link nút "Tải xuống" |

## Deploy lên Cloudflare Pages

1. Cloudflare dashboard → **Workers & Pages** → **Create** → **Pages** → **Connect to Git** → chọn repo.
2. Cấu hình build:
   - Framework preset: **Astro**
   - Root directory: `QuestLog.Landing`
   - Build command: `npm run build`
   - Build output directory: `dist`
   - Biến môi trường: `NODE_VERSION=22`, cùng `PUBLIC_SITE_URL` và `PUBLIC_DOWNLOAD_URL` nếu khác mặc định.
3. **Settings → Builds → Build watch paths**: include `QuestLog.Landing/*` để commit chỉ sửa app không kích hoạt build landing.
4. Gắn domain riêng tại **Custom domains**, rồi cập nhật `PUBLIC_SITE_URL`.

File cài đặt (.exe/.msi) đặt ở **GitHub Releases**. Nếu file lớn hoặc nhiều lượt tải thì chuyển sang Cloudflare R2 và đổi `PUBLIC_DOWNLOAD_URL`.

## Việc còn lại

- [ ] Rà lại nội dung: yêu cầu hệ thống (Windows 10/11, 64-bit), danh sách tính năng.
- [ ] Domain chính thức.
