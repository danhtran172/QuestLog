using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterPlay.Models.Models
{
    public class Config
    {
        public string BackupRootPath { get; set; } = string.Empty;
        public List<string> GameLibraryPath { get; set; } = new List<string>();
        public string DatabasePath { get; set; } = string.Empty;
        public bool isDarkMode { get; set; } = false;

        // ==================== CLOUDINARY ====================
        public string CloudinaryCloudName { get; set; } = string.Empty;
        public string CloudinaryApiKey { get; set; } = string.Empty;
        public string CloudinaryApiSecret { get; set; } = string.Empty;
    }
}