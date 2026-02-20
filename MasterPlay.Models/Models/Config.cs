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
        public string ConfigFilePath { get; set; } = string.Empty;
        public string DataFilePath { get; set; } = string.Empty;
        public List<string> GameLibraryPath { get; set; } = new List<string>();
        public string AvailableGamesPath { get; set; } = string.Empty;
        public string MetaDataPath { get; set; } = string.Empty;
        public string SelectionPath { get; set; } = string.Empty;
    }
}
