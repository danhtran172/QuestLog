using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MasterPlay.Models
{
    public class GameInfo
    {
        public string Name { get; set; }
        public string ExeName { get; set; }
        public string SaveFolderPath { get; set; }
    }
    public class RawgResponse
    {
        public List<RawgGame> Results { get; set; }
    }

    public class RawgGame
    {
        public string Name { get; set; }
        public string Background_Image { get; set; } 
    }
    public class GameEntry
    {
        public string Game { get; set; } = string.Empty;
        public string Exe { get; set; } = string.Empty;
    }
}
