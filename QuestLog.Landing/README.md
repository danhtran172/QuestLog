# QuestLog Landing

Trang giới thiệu public của QuestLog. Astro, xuất HTML tĩnh, deploy lên Firebase Hosting.

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
  site.ts                Link tải xuống / trang phát hành (repo public questlog-releases)
  shots.ts               Tra ảnh chụp app theo ngôn ngữ + chip điểm
  glance.ts              Dữ liệu bảng "Thư viện của bạn" ở hero (cùng bộ với ảnh chụp)
  assets/app/{vi,en}/    Ảnh chụp app, mỗi ngôn ngữ một bộ
  assets/app/chips/      Chip điểm cá nhân (Rust → Opal) và điểm háo hức (lạnh → dung nham)
  assets/app/wall.webp   Trang đầu thư viện (30 game), làm nền hero và phần tải xuống
  assets/glance/         Ảnh bìa nhỏ cho bảng ở hero
  styles/global.css      Design token + Tailwind
  scripts/landing.ts     Đường ray dòng thời gian theo cuộn, hiệu ứng xuất hiện, header
  layouts/Base.astro     <head>, SEO, hreflang, Open Graph
  components/            Header, Hero, GlancePanel, Showcase, Journey (dòng thời gian),
                         Stop, ConsoleBand, TrayCard, Scores, Organize, Download, Footer
  pages/index.astro      /      (tiếng Việt, mặc định)
  pages/en/index.astro   /en/   (English)
public/
  _headers               Header bảo mật + cache khi host trên Cloudflare (Firebase dùng firebase.json)
  og.jpg                 Ảnh chia sẻ 1200×630
```

## Ý tưởng nội dung

Trang trả lời đúng một câu: **đã chơi gì, sắp chơi gì**. Phần chính là một dòng thời gian
ba chặng — **Đã chơi → Đang chơi → Sắp chơi** — mỗi chặng kèm ảnh màn hình làm việc đó
(dashboard, chi tiết game + dịch vụ nền, hàng chờ + thông báo phát hành). Sau đó là hai thang
điểm của người dùng (điểm cá nhân, điểm háo hức) và phần phân loại (bộ lọc, chủ đề theo nhóm,
giao diện card trực quan, thứ tự sắp xếp từng tab).

- Không quảng cáo tính năng đang tắt (sao lưu save: `FeatureFlags.GameBackup`) hay chưa sửa được
  (Manage tags cho trạng thái/nơi sở hữu vẫn chỉ đọc).
- Chỉ có theme tối: app đang khoá theme tối và mọi ảnh chụp đều nền tối.
- Font: Big Shoulders Display (tiêu đề), Manrope (nội dung, giống app), JetBrains Mono (nhãn).
- Không có JS trang vẫn đọc đủ; `prefers-reduced-motion` tắt nền trôi, nghiêng 3D và hiệu ứng xuất hiện.

## Ảnh chụp app

Ảnh trong `src/assets/app/` chụp từ app thật (bản build hiện tại), độ phân giải 2×, chế độ chỉ đọc.
Game và ảnh bìa là thư viện của chủ dự án; **trạng thái, điểm, độ háo hức, giờ chơi và metadata
được dựng lại** ngay trong phản hồi API lúc chụp cho đa dạng và đầy đủ (không ghi gì vào dữ liệu thật).
Game sắp ra mắt chỉ giữ GTA VI và Phantom Blade Zero.

Ảnh bìa game thuộc bản quyền của các nhà phát hành.

## Biến môi trường

Xem `.env.example`.

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `PUBLIC_SITE_URL` | `https://getquestlog.web.app` | Domain chính, dùng cho canonical/hreflang |
| `PUBLIC_DOWNLOAD_URL` | `releases/latest/download/QuestLogDesktop-win-Setup.exe` | Link nút "Tải xuống": tải thẳng file cài của bản mới nhất. Cần đã có ít nhất một release (`tools/release.ps1 -Upload`), nếu không link trả 404 |

## Deploy lên Firebase Hosting

Site `getquestlog` trong project Firebase `questlog-8e895` → https://getquestlog.web.app (cấu hình ở `firebase.json`, `.firebaserc`).

```bash
npm run build
npx firebase-tools deploy --only hosting
```

Cần đăng nhập Firebase CLI một lần (`npx firebase-tools login`) bằng tài khoản có quyền với project. Header bảo mật và cache khai báo trong `firebase.json` (`public/_headers` chỉ dùng khi host trên Cloudflare).

Gói miễn phí (Spark) giới hạn 360 MB truyền tải/ngày; nếu lượt xem tăng thì chuyển sang Cloudflare (không giới hạn băng thông) hoặc gói Blaze.

File cài đặt (.exe/.msi) đặt ở **GitHub Releases**. Nếu file lớn hoặc nhiều lượt tải thì chuyển sang Cloudflare R2 và đổi `PUBLIC_DOWNLOAD_URL`.

## Việc còn lại

- [ ] Rà lại yêu cầu hệ thống (Windows 10/11, 64-bit).
- [ ] Domain chính thức.
- [ ] Phát hành bản cài đầu tiên lên `danhtran172/questlog-releases` — nút tải đang trỏ vào `releases/latest` của repo đó.
