using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterPlay.Core.Entities
{
    public class LibrarySortRuleConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Direction { get; set; } = "asc";
    }

    public class Config
    {
        public string BackupRootPath { get; set; } = string.Empty;
        public List<string> GameLibraryPath { get; set; } = new List<string>();
        public string DatabasePath { get; set; } = string.Empty;
        public string ThemeMode { get; set; } = "system";
        public string JungleFx { get; set; } = "auto";
        public string JungleWeather { get; set; } = "clear";
        // Mode cuoi cung cua app: "desktop" | "console". Mo lai app se vao lai mode nay.
        public string AppMode { get; set; } = "desktop";
        // Huong dan UI nguoi dung da xem xong ("gameForm", "viewModes"). Rong = chua xem gi.
        public List<string> SeenGuides { get; set; } = new List<string>();
        public string Language { get; set; } = "en-US";
        public string CloudinaryCloudName { get; set; } = string.Empty;
        public string CloudinaryApiKey { get; set; } = string.Empty;
        public string CloudinaryApiSecret { get; set; } = string.Empty;

        // IGDB dung Twitch OAuth2 (client_credentials). Tao app tai https://dev.twitch.tv/console/apps.
        // Bien moi truong IGDB_CLIENT_ID / IGDB_CLIENT_SECRET duoc uu tien hon hai cot nay.
        public string IgdbClientId { get; set; } = string.Empty;
        public string IgdbClientSecret { get; set; } = string.Empty;
        public Dictionary<string, List<LibrarySortRuleConfig>> LibrarySortProfiles { get; set; } = new();
    }
}
