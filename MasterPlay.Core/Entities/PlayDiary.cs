using System;

namespace MasterPlay.Core.Entities
{

    public class PlayDiary
    {

        public int Id { get; set; }
        public string GameId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int TimeSpent { get; set; }
        public string? Notes { get; set; }


        public virtual Game Game { get; set; } = null!;
    }
}
