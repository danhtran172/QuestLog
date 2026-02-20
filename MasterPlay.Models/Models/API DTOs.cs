using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MasterPlay.Models.Models
{
    public class RawgResponse { public List<RawgGameDetail> Results { get; set; } }

    public class RawgGameDetail
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Background_Image { get; set; }
        public string Released { get; set; }
        public List<GenreItem> Genres { get; set; }
        public List<DeveloperItem> Developers { get; set; }
        public List<PublisherItem> Publishers { get; set; }
    }

    public class GenreItem { public string Name { get; set; } }
    public class DeveloperItem { public string Name { get; set; } }
    public class PublisherItem { public string Name { get; set; } }
    public class RawgGame
    {
        public string Name { get; set; }
        public string Background_Image { get; set; }
        public string Genres { get; set; }
        public string ReleaseDate { get; set; }
        public string Developers { get; set; }
        public string Publishers { get; set; }
    }
    public class GameMetadata
    {
        public string Game { get; set; } = string.Empty;
        public string Exe { get; set; } = string.Empty;
        public string SaveFolder { get; set; } = string.Empty;

    }
}
