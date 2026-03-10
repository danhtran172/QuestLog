using System;

namespace MasterPlay.Models.Models
{
    // PlayDiary
    public class PlayDiary
    {
        // Fields
        public int Id { get; set; }
        public string GameId { get; set; }
        public DateTime Date { get; set; }
        public int TimeSpent { get; set; } // seconds
        public string? Notes { get; set; }

        // Navigation Properties
        public virtual Game Game { get; set; }
    }
}