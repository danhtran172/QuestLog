using HowLongToBeatScraper;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.HowLongToBeat
{

    public static class HowLongToBeatService
    {

        public static async Task<float?> GetPlayTimeAsync(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return null;

            try
            {
                using var scraper = new HltbScraper();
                var results = await scraper.Search(gameName);

                var firstGame = results?.FirstOrDefault();
                if (firstGame == null)
                {
                    Console.WriteLine($"[HowLongToBeat] No game found for: {gameName}");
                    return null;
                }


                return firstGame.MainStoryWithExtras;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HowLongToBeat Error] {ex.Message} for game: {gameName}");
                return null;
            }
        }
    }
}