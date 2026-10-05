namespace MasterPlay.Shell;

/// <summary>
/// Chuoi cua man cai dat lan dau. Man nay chay truoc Host nen khong dung duoc i18n
/// cua giao dien web; doi ngon ngu o buoc 1 la doi ngay toan bo chu tren cua so.
/// </summary>
internal static class SetupStrings
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["WindowTitle"] = "QuestLog Setup",
        ["Step"] = "Step {0} of {1}",
        ["LanguageTitle"] = "Welcome to QuestLog",
        ["LanguageSubtitle"] = "Choose the language for QuestLog. You can change it later in Settings.",
        ["DataTitle"] = "Where should QuestLog keep your data?",
        ["DataSubtitle"] = "Your game library, settings and images are stored in this folder. The default location works for most people.",
        ["DataLabel"] = "Data folder",
        ["Browse"] = "Browse…",
        ["BrowseTitle"] = "Choose where to keep QuestLog data",
        ["UseDefault"] = "Use the default location",
        ["ExistingData"] = "This folder already has QuestLog data. QuestLog will keep using it.",
        ["MissingFolder"] = "The data folder chosen earlier is not available. Reconnect its drive and try again, or choose another folder.",
        ["UnreadableLocation"] = "QuestLog could not read where your data is kept. Choose the data folder again.",
        ["TrayNote"] = "QuestLog Tray is installed too. It starts with Windows to track your playtime.",
        ["Back"] = "Back",
        ["Next"] = "Next",
        ["Finish"] = "Start QuestLog",
        ["Close"] = "Close",
        ["ErrorRequired"] = "Enter a folder for QuestLog data.",
        ["ErrorNotFullPath"] = "Enter a full path, for example D:\\QuestLog.",
        ["ErrorInsideInstall"] = "Choose a folder outside the app's install folder. That folder is replaced on every update.",
        ["ErrorNotWritable"] = "QuestLog can't write to this folder. {0}",
        ["ErrorSaveFailed"] = "QuestLog couldn't save these choices. {0}",
    };

    private static readonly IReadOnlyDictionary<string, string> Vietnamese = new Dictionary<string, string>
    {
        ["WindowTitle"] = "Cài đặt QuestLog",
        ["Step"] = "Bước {0}/{1}",
        ["LanguageTitle"] = "Chào mừng đến với QuestLog",
        ["LanguageSubtitle"] = "Chọn ngôn ngữ cho QuestLog. Bạn có thể đổi lại trong Cài đặt.",
        ["DataTitle"] = "QuestLog lưu dữ liệu ở đâu?",
        ["DataSubtitle"] = "Thư viện game, cài đặt và hình ảnh được lưu trong thư mục này. Đa số người dùng chỉ cần để mặc định.",
        ["DataLabel"] = "Thư mục dữ liệu",
        ["Browse"] = "Chọn thư mục…",
        ["BrowseTitle"] = "Chọn nơi lưu dữ liệu QuestLog",
        ["UseDefault"] = "Dùng vị trí mặc định",
        ["ExistingData"] = "Thư mục này đã có dữ liệu QuestLog. QuestLog sẽ dùng tiếp dữ liệu đó.",
        ["MissingFolder"] = "Không tìm thấy thư mục dữ liệu đã chọn trước đây. Hãy cắm lại ổ đĩa rồi thử lại, hoặc chọn thư mục khác.",
        ["UnreadableLocation"] = "QuestLog không đọc được nơi lưu dữ liệu. Hãy chọn lại thư mục dữ liệu.",
        ["TrayNote"] = "QuestLog Tray cũng đã được cài. Tray khởi động cùng Windows để đếm giờ chơi.",
        ["Back"] = "Quay lại",
        ["Next"] = "Tiếp tục",
        ["Finish"] = "Mở QuestLog",
        ["Close"] = "Đóng",
        ["ErrorRequired"] = "Hãy nhập thư mục lưu dữ liệu QuestLog.",
        ["ErrorNotFullPath"] = "Hãy nhập đường dẫn đầy đủ, ví dụ D:\\QuestLog.",
        ["ErrorInsideInstall"] = "Hãy chọn thư mục nằm ngoài thư mục cài đặt của app. Thư mục đó bị thay mỗi lần cập nhật.",
        ["ErrorNotWritable"] = "QuestLog không ghi được vào thư mục này. {0}",
        ["ErrorSaveFailed"] = "QuestLog không lưu được các lựa chọn này. {0}",
    };

    public static string Get(string language, string key)
    {
        var strings = language == FirstRunSetup.Vietnamese ? Vietnamese : English;
        return strings.TryGetValue(key, out var value) ? value : English[key];
    }
}
