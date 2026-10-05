# Cài đặt và lần mở đầu tiên

Ngày: 2026-10-05

## Yêu cầu

- Khi cài thì cho chọn ngôn ngữ.
- Chọn chỗ lưu dữ liệu, có sẵn chỗ mặc định.
- Tray app luôn được cài.
- Mở app thì vào thẳng app, không qua màn đăng nhập.

## Vì sao làm trong app chứ không trong bộ cài

Bộ cài là `Setup.exe` của Velopack (`tools/release.ps1`). Nó cài một lèo, không có
trang tuỳ chọn nào. Velopack báo lần mở đầu tiên sau khi cài qua hook `OnFirstRun`,
nên các câu hỏi lúc cài được đặt trong một cửa sổ WPF của Shell (`SetupWindow`). Cửa sổ
này chạy **trước** khi Host và tray khởi động, vì cả hai đọc thư mục dữ liệu ngay lúc chạy.

## Luồng

```
Setup.exe ──cài──▶ --veloapp-install ──▶ đăng ký tray khởi động cùng Windows (HKCU Run)
          ──mở──▶ QuestLog.exe (OnFirstRun)
                    └─ SetupWindow: 1. ngôn ngữ  2. thư mục dữ liệu
                         └─ ghi lựa chọn ──▶ Host + tray ──▶ cửa sổ chính ──▶ vào thẳng app (guest)
```

Màn cài đặt hiện khi:

- vừa cài xong (kể cả cài lại); hoặc
- file vị trí dữ liệu hỏng, hoặc thư mục đã chọn không còn (ổ đĩa ngoài đã rút); hoặc
- máy chưa từng có dữ liệu QuestLog/MasterPlay ở vị trí mặc định.

Người đang dùng nhận bản cập nhật không rơi vào trường hợp nào ở trên nên không bị hỏi lại.
Đóng cửa sổ là huỷ: app thoát, lần mở sau hỏi lại.

## Thư mục dữ liệu

- Mặc định `%APPDATA%\QuestLog` (như trước).
- Lựa chọn nằm trong `%LOCALAPPDATA%\QuestLog\data-location.json`
  (`{ "dataDirectory": "..." }`). File này nằm ngoài thư mục dữ liệu vì phải đọc nó mới
  biết thư mục dữ liệu ở đâu, và theo máy chứ không roaming.
- `AppPaths` là nơi duy nhất đọc file này. Shell biên dịch kèm `AppPaths.cs` (link nguồn)
  thay vì tham chiếu `MasterPlay.Infrastructure`, để thư mục cài không phải chứa EF Core/SQLite.
  Shell, Host, Tray và bản dev (`run-web.cmd`) đều thấy cùng một thư mục.
- Chọn đúng thư mục mặc định thì vẫn đi qua bước dời dữ liệu `MasterPlay` → `QuestLog`.
- File vị trí hỏng thì báo lỗi, **không** lặng lẽ dùng thư mục mặc định: một thư viện
  rỗng trong khi dữ liệu thật vẫn còn là đúng cái bẫy `AppPaths` dựng ra để tránh.
- Không cho chọn thư mục nằm trong thư mục cài của Velopack: nó bị thay mỗi lần cập nhật.
- "Chọn thư mục…" thêm `\QuestLog` vào thư mục được chọn, trừ khi thư mục đó đã có dữ liệu
  QuestLog hoặc đã tên là `QuestLog`.
- `auth.local.json` được đọc ở cả `%APPDATA%\QuestLog` và thư mục đã chọn (thư mục đã chọn thắng).

## Ngôn ngữ

- Lựa chọn ghi vào `Language` trong `config.json` của thư mục dữ liệu (dùng chung với Tray,
  DOMAIN_RULES §15). Config đã có thì chỉ sửa khoá đó; chưa có thì tạo file chỉ có
  `Language`, `JsonService` điền phần còn lại ở lần đọc đầu.
- Giao diện web nạp ngôn ngữ từ `GET /api/settings` trước khi vẽ app
  (`SettingsLanguageGate`), và Cài đặt lưu ngôn ngữ qua `PUT /api/settings`. Trước đây
  web luôn mở bằng tiếng Anh và không lưu ngôn ngữ đã chọn.

## Tray

- Tray nằm sẵn trong bộ cài (`tray\`). Lúc cài (`OnAfterInstallFastCallback`) Shell đăng ký
  tray khởi động cùng Windows, đúng định dạng `StartupHelper` của tray nên mục menu
  "Start with Windows" hiện đúng trạng thái. Lúc cập nhật thì không đăng ký lại: người
  dùng đã tắt thì không bật lại. Lúc gỡ thì xoá (đã có từ trước).
- Đổi thư mục dữ liệu ở màn cài đặt mà tray cũ đang chạy thì tắt nó để Shell mở lại với
  thư mục mới.

## Điều khiển tray từ app

Panel "Dịch vụ nền" trước đây luôn báo "Not connected" vì `/api/tray/*` chưa có
và `capabilities.features.tray` bị gán cứng `false`. Giờ:

- Tray mở named pipe `QuestLogTrayControl-<domain>-<user>` (`PipeOptions.CurrentUserOnly`).
  Mỗi kết nối là một dòng JSON lệnh, một dòng JSON trả lời
  (`MasterPlay.Infrastructure/TrayControl/TrayControl.cs`). Lệnh chạy trên UI thread
  của tray, dùng chung code với menu chuột phải.
- Host cài `GET /api/tray/status` (tray không chạy → 200 `running: false`),
  `POST /api/tray/services/{playtime|scanner}`, `POST /api/tray/scan-now`, `POST /api/tray/open-logs`.
  `backup-now` và `startup` trả 501. Lệnh gửi tới tray không chạy → 409 `TRAY_NOT_RUNNING`.
- `capabilities.features.tray = true` trên Windows (Host điều khiển được tray); tray có
  đang chạy hay không thì xem `TrayStatus.running`.

## Thư mục cài và gỡ cài đặt

Bổ sung cùng ngày: cho chọn chỗ cài app, và có file gỡ cài đặt nhìn thấy được.

- Bộ cài phát hành giờ là `QuestLog-Setup.exe` (`MasterPlay.Installer`). Nó chạy trên .NET Framework 4.8,
  có sẵn trên Windows 10/11, vì lúc này máy chưa chắc có .NET 8. Bên trong nhúng `Setup.exe`
  của Velopack (`tools/release.ps1`, `-p:VelopackSetup=...`). Người dùng chọn thư mục, rồi bộ cài chạy
  `Setup.exe --installto <thư mục> --log %TEMP%\QuestLog-Setup.log`. Chạy kèm đối số
  (`--silent`, `--installto ...`) thì bỏ qua cửa sổ, chuyển nguyên đối số cho `Setup.exe`.
- Mặc định là `%LOCALAPPDATA%\QuestLogDesktop`, chỗ Velopack vẫn cài trước đây.
- Velopack **xoá cả thư mục cài** khi gỡ. Cài vào thư mục đã có file thì nó đổi tên thư mục đó rồi
  xoá luôn. Vì vậy thư mục cài phải trống hoặc chưa tồn tại, và "Chọn thư mục…" thêm `\QuestLog`
  vào cuối. Ngoài ra còn chặn:
  - gốc ổ đĩa;
  - Windows, Program Files, ProgramData: cần quyền quản trị, mà bản cập nhật chạy không có quyền đó;
  - bất kỳ chỗ nào trùng hoặc lồng với thư mục dữ liệu (`%APPDATA%\QuestLog`, `%APPDATA%\MasterPlay`,
    `%LOCALAPPDATA%\QuestLog`, thư mục trong `data-location.json`).
- Máy đã có bản cài (mục `QuestLogDesktop` trong Uninstall của HKCU) thì khoá đường dẫn vào chỗ cũ.
  Velopack chỉ giữ một bản cho mỗi người dùng: cài sang chỗ khác thì mục gỡ cài đặt trỏ sang bản mới,
  còn thư mục cũ nằm lại không ai dọn.
- Velopack gỡ bằng `Update.exe --uninstall`, không có file gỡ riêng. Shell tạo shortcut
  "Gỡ cài đặt QuestLog" (Windows tiếng Anh thì là "Uninstall QuestLog") trỏ vào lệnh đó, đặt trong
  thư mục cài và Start Menu (`UninstallShortcut`). Shortcut được tạo lúc cài và cả lúc cập nhật, để máy
  đã cài bản cũ cũng có; lúc gỡ thì xoá.
- Landing tải `QuestLog-Setup.exe`. File này chỉ có từ bản phát hành có nó trở đi.

## Icon

`app.ico`/`TrayIcon.ico` có các khung BMP 32-bit thiếu mặt nạ AND. Windows và .NET bỏ
qua chỗ thiếu này, nhưng bộ đọc ICO của Velopack đọc tràn ở khung đầu tiên rồi bỏ cả
file, nên `Setup.exe` và `Update.exe` ra nhóm icon rỗng. Đã thêm mặt nạ (tính từ kênh
alpha, pixel giữ nguyên). Khi thay icon, kiểm tra `Setup.exe` sau khi đóng gói có đủ
`RT_ICON`. `MasterPlay.Host.exe` cũng được gắn icon (trước đây hiện icon exe trống
trong Task Manager).

## Vào thẳng app

Desktop không khôi phục được phiên tài khoản (Host trả 401) hoặc auth chưa cấu hình thì
tự vào guest (dữ liệu trên máy). Màn đăng nhập chỉ hiện khi người dùng chọn "Đăng nhập"
trong menu. Lỗi mạng khi khôi phục một phiên đã có vẫn hiện màn lỗi như cũ. Web/hosted
không có guest nên vẫn phải đăng nhập. Xem DOMAIN_RULES §19 và `src/lib/authStartup.ts`.

## Kiểm thử

- `MasterPlay.Host.Tests/AppPathsTests.cs`: đọc/ghi file vị trí, file hỏng, đường dẫn tương đối.
- `MasterPlay.Web/tests/authStartup.test.mjs`: quy tắc vào thẳng app.
- `MasterPlay.Host.Tests/InstallTargetTests.cs`: quy tắc chọn thư mục cài, ghép dòng lệnh cho `Setup.exe`
  (dịch kèm nguồn của `MasterPlay.Installer`).
- Thử bộ cài mà không đụng bản đang cài: đóng gói một app giả bằng packId khác (`vpk pack --packId
  QuestLogInstallerTest --skipVeloAppCheck --shortcuts None`), build `MasterPlay.Installer` với
  `-p:VelopackSetup=<Setup.exe của app giả>`, rồi chạy `QuestLog-Setup.exe --silent --installto <thư mục>`.
  Đừng cài thử bằng gói QuestLog thật: hook cài đặt sẽ ghi đè mục tự khởi động của tray.
- Thử tay bộ cài: `tools\release.ps1 -Version x.y.z`, chạy `Setup.exe` trong
  `.artifacts\release\releases`. Lưu ý chọn thư mục khác ở bước 2 sẽ đổi thư mục dữ liệu
  của **cả bản dev** trên máy đó; xoá `%LOCALAPPDATA%\QuestLog\data-location.json` để về mặc định.
