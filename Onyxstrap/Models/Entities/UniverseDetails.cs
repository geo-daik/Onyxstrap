namespace Onyxstrap.Models.Entities
{
    /// <summary>
    /// Explicit loading. Load from cache before and after a fetch.
    /// </summary>
    public class UniverseDetails
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, UniverseDetails> _cache = new();

        public GameDetailResponse Data { get; set; } = null!;

        /// <summary>
        /// Returns data for a 128x128 icon
        /// </summary>
        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public static UniverseDetails? LoadFromCache(long id)
        {
            return _cache.TryGetValue(id, out var details) ? details : null;
        }

        public static Task FetchSingle(long id) => FetchBulk(id.ToString());

        public static async Task FetchBulk(string ids)
        {
            var gameDetailResponse = await Http.GetJson<ApiArrayResponse<GameDetailResponse>>($"https://games.roblox.com/v1/games?universeIds={ids}");

            if (!gameDetailResponse.Data.Any())
                return;

            var universeThumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>($"https://thumbnails.roblox.com/v1/games/icons?universeIds={ids}&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false");

            if (!universeThumbnailResponse.Data.Any())
                throw new InvalidHTTPResponseException("Roblox API for Game Thumbnails returned invalid data");

            foreach (string strId in ids.Split(','))
            {
                long id = long.Parse(strId);

                var data = gameDetailResponse.Data.FirstOrDefault(x => x.Id == id);
                if (data is null) continue;
                _cache[id] = new UniverseDetails
                {
                    Data = data,
                    Thumbnail = universeThumbnailResponse.Data.FirstOrDefault(x => x.TargetId == id)
                        ?? new ThumbnailResponse { TargetId = id }
                };
            }
        }
    }
}
