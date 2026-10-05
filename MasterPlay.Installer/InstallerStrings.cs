using System.Collections.Generic;
using System.Globalization;

namespace MasterPlay.Installer;

/// <summary>
/// Chuoi cua bo cai. Ngon ngu theo Windows: nguoi dung chon ngon ngu cho app o man cai
/// dat lan dau (MasterPlay.Shell/SetupWindow), ngay sau buoc nay.
/// </summary>
internal static class InstallerStrings
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["WindowTitle"] = "QuestLog Setup",
        ["Title"] = "Where should QuestLog be installed?",
        ["Subtitle"] = "QuestLog and its updates live in this folder. Your game library is stored separately — you'll choose where right after installing.",
        ["Label"] = "Install folder",
        ["Browse"] = "Browse…",
        ["BrowseTitle"] = "Choose where to install QuestLog",
        ["UseDefault"] = "Use the default location",
        ["Existing"] = "QuestLog is already installed in this folder. Reinstalling keeps your game library. To install somewhere else, uninstall the current copy first.",
        ["FolderNote"] = "Uninstalling deletes this whole folder, so use a folder just for QuestLog.",
        ["Cancel"] = "Cancel",
        ["Install"] = "Install",
        ["Close"] = "Close",
        ["ErrorRequired"] = "Enter a folder to install QuestLog in.",
        ["ErrorNotFullPath"] = "Enter a full path, for example D:\\Apps\\QuestLog.",
        ["ErrorDriveRoot"] = "QuestLog can't be installed at the root of a drive. Try D:\\Apps\\QuestLog.",
        ["ErrorProtectedFolder"] = "This folder needs administrator rights, so QuestLog couldn't update itself there. Choose another folder, for example D:\\Apps\\QuestLog.",
        ["ErrorDataFolder"] = "This is where QuestLog keeps your data. The install folder is deleted on uninstall, so it has to be somewhere else.",
        ["ErrorNotEmpty"] = "This folder already has files in it. Choose an empty folder: uninstalling deletes the whole install folder.",
        ["ErrorNotWritable"] = "QuestLog can't write to this folder. {0}",
        ["ErrorMissingPackage"] = "This installer is missing the QuestLog package. Download the installer again.",
        ["ErrorFailed"] = "Setup didn't finish (code {0}). Details are in {1}.",
        ["ErrorLaunch"] = "Setup couldn't start. {0}",
    };

    private static readonly IReadOnlyDictionary<string, string> Vietnamese = new Dictionary<string, string>
    {
        ["WindowTitle"] = "Cài đặt QuestLog",
        ["Title"] = "Cài QuestLog vào đâu?",
        ["Subtitle"] = "QuestLog và các bản cập nhật nằm trong thư mục này. Thư viện game được lưu riêng, bạn sẽ chọn chỗ lưu ngay sau khi cài.",
        ["Label"] = "Thư mục cài đặt",
        ["Browse"] = "Chọn thư mục…",
        ["BrowseTitle"] = "Chọn nơi cài QuestLog",
        ["UseDefault"] = "Dùng vị trí mặc định",
        ["Existing"] = "QuestLog đã được cài ở thư mục này. Cài lại vẫn giữ nguyên thư viện game. Muốn cài sang chỗ khác, hãy gỡ bản đang cài trước.",
        ["FolderNote"] = "Gỡ cài đặt sẽ xoá cả thư mục này, nên hãy dùng một thư mục chỉ dành cho QuestLog.",
        ["Cancel"] = "Huỷ",
        ["Install"] = "Cài đặt",
        ["Close"] = "Đóng",
        ["ErrorRequired"] = "Hãy nhập thư mục để cài QuestLog.",
        ["ErrorNotFullPath"] = "Hãy nhập đường dẫn đầy đủ, ví dụ D:\\Apps\\QuestLog.",
        ["ErrorDriveRoot"] = "Không cài thẳng vào gốc ổ đĩa được. Thử D:\\Apps\\QuestLog.",
        ["ErrorProtectedFolder"] = "Thư mục này cần quyền quản trị nên QuestLog không tự cập nhật được. Hãy chọn thư mục khác, ví dụ D:\\Apps\\QuestLog.",
        ["ErrorDataFolder"] = "Đây là nơi QuestLog lưu dữ liệu của bạn. Thư mục cài bị xoá khi gỡ, nên phải nằm ở chỗ khác.",
        ["ErrorNotEmpty"] = "Thư mục này đã có file. Hãy chọn thư mục trống: gỡ cài đặt sẽ xoá cả thư mục cài.",
        ["ErrorNotWritable"] = "QuestLog không ghi được vào thư mục này. {0}",
        ["ErrorMissingPackage"] = "Bộ cài này bị thiếu gói QuestLog. Hãy tải lại bộ cài.",
        ["ErrorFailed"] = "Cài đặt chưa xong (mã {0}). Chi tiết ghi ở {1}.",
        ["ErrorLaunch"] = "Không chạy được trình cài đặt. {0}",
    };

    private static readonly IReadOnlyDictionary<string, string> Current =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? Vietnamese : English;

    public static string Get(string key) => Current.TryGetValue(key, out var value) ? value : English[key];
}
