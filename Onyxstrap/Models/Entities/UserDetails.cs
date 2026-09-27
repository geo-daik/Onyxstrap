using Onyxstrap.Models.RobloxApi;

namespace Onyxstrap.Models.Entities
{
    public class UserDetails
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, UserDetails> _cache = new();

        public GetUserResponse Data { get; set; } = null!;

        public ThumbnailResponse Thumbnail { get; set; } = null!;

        public static async Task<UserDetails> Fetch(long id)
        {
            if (_cache.TryGetValue(id, out var cached)) return cached;

            var userResponse = await Http.GetJson<GetUserResponse>($"https://users.roblox.com/v1/users/{id}");

            if (userResponse is null)
                throw new InvalidHTTPResponseException("Roblox API for User Details returned invalid data");

            // we can remove '-headshot' from the url if we want a full avatar picture
            var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>($"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={id}&size=180x180&format=Png&isCircular=false");

            if (thumbnailResponse is null || !thumbnailResponse.Data.Any())
                throw new InvalidHTTPResponseException("Roblox API for Thumbnails returned invalid data");

            var details = new UserDetails
            {
                Data = userResponse,
                Thumbnail = thumbnailResponse.Data.First()
            };

            _cache[id] = details;

            return details;
        }
    }
}
