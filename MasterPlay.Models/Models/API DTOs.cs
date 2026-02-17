using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MasterPlay.Models.Models
{
    public class RawgResponse
    {
        public List<RawgGame> Results { get; set; }
    }
    public class RawgGame
    {
        public string Name { get; set; }
        public string Background_Image { get; set; } 
    }
    public class GameMetadata
    {
        public string Game { get; set; } = string.Empty;
        public string Exe { get; set; } = string.Empty;
        public string SaveFolder { get; set; } = string.Empty;

    }
}
