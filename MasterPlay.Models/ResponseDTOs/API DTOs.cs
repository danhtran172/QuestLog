using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MasterPlay.Models.ResponseDTOs
{
    public class RawgResponse { public List<RawgData> Results { get; set; } }

    public class RawgData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Background_Image { get; set; }
        public string Released { get; set; }
        public int? Metacritic { get; set; }
        public long? Playtime { get; set; }
        public List<GenreItem> Genres { get; set; }
        public List<DeveloperItem> Developers { get; set; }
        public List<PublisherItem> Publishers { get; set; }
    }

    public class GenreItem { public string Name { get; set; } }
    public class DeveloperItem { public string Name { get; set; } }
    public class PublisherItem { public string Name { get; set; } }
    public class GameMetadata
    {
        public string Name { get; set; }
        public string SlugName { get; set; }
        public string Background_Image { get; set; }
        public string Genres { get; set; }
        public string ReleaseDate { get; set; }
        public string Developers { get; set; }
        public string Publishers { get; set; }
        public int? MetaScore { get; set; }
        public int? AvgPlaytime { get; set; }
        public string Game { get; set; } 
        public string Exe { get; set; } 
        public string SaveFolder { get; set; } 
    }
}
