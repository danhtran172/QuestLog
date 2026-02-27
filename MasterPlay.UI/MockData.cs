using System.Collections.ObjectModel;

namespace MasterPlay.UI { 
    public static class MockData
    {
        public static ObservableCollection<string> Series { get; } = new ObservableCollection<string>
        {
            "The Witcher",
            "Final Fantasy",
            "The Legend of Zelda",
            "Mass Effect",
            "Dragon Age",
            "Metal Gear Solid",
            "God of War",
            "Grand Theft Auto",
            "Red Dead Redemption",
            "Assassin's Creed"
        };

        public static ObservableCollection<string> Genres { get; } = new ObservableCollection<string>
        {
            "Stealth", "Puzzle", "Action", "Adventure", "Open World",
            "Semi-Open World", "Linear", "Story-driven", "Soulslike", "Soulsborn",
            "RPG", "Freeflow", "Hack & Slash", "Shooter", "RTS",
            "Turn-based", "Survival", "Horror", "Platformer", "Racing",
            "Sports", "Fighting", "Strategy", "Simulation"
        };

        public static ObservableCollection<string> Themes { get; } = new ObservableCollection<string>
        {
            "Fantasy", "Sci-Fi", "Historical", "Modern", "Post-Apocalyptic",
            "Horror", "Mystery", "Comedy", "Drama", "Mythology",
            "Cyberpunk", "Steampunk", "Medieval", "War", "Supernatural",
            "Detective", "Pirate", "Ninja", "Samurai", "Western"
        };
    }
}
