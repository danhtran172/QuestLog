using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Rawg.Models
{
    public class RawgResponse { public List<RawgData> Results { get; set; } = []; }

    public class RawgData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Background_Image { get; set; } = string.Empty;
        public string Released { get; set; } = string.Empty;
        public int? Metacritic { get; set; }
        public long? Playtime { get; set; }
        public List<GenreItem> Genres { get; set; } = [];
        public List<DeveloperItem> Developers { get; set; } = [];
        public List<PublisherItem> Publishers { get; set; } = [];
    }

    public class GenreItem { public string Name { get; set; } = string.Empty; }
    public class DeveloperItem { public string Name { get; set; } = string.Empty; }
    public class PublisherItem { public string Name { get; set; } = string.Empty; }
    public class GameMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string SlugName { get; set; } = string.Empty;
        public string Background_Image { get; set; } = string.Empty;
        public string Genres { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string Developers { get; set; } = string.Empty;
        public string Publishers { get; set; } = string.Empty;
        public int? MetaScore { get; set; }
        public int? AvgPlaytime { get; set; }
        public string Game { get; set; } = string.Empty;
        public string Exe { get; set; } = string.Empty;
        public string SaveFolder { get; set; } = string.Empty;
    }
}
