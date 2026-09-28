namespace Onyxstrap
{
    internal static class GameFavorites
    {
        internal static bool TryParsePlace(string? input, out long placeId)
        {
            placeId = 0;
            string value = input?.Trim() ?? "";
            if (!Regex.IsMatch(value, @"^[0-9]+$"))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                    || !uri.IsDefaultPort || uri.UserInfo.Length != 0
                    || !(uri.Host.Equals("www.roblox.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("roblox.com", StringComparison.OrdinalIgnoreCase))) return false;
                var match = Regex.Match(uri.AbsolutePath, @"^/games/([0-9]+)(?:/|$)");
                if (!match.Success) return false;
                value = match.Groups[1].Value;
            }
            return long.TryParse(value, out placeId) && placeId > 0;
        }
        internal static string LaunchUri(long placeId) => placeId > 0
            ? "roblox://placeId=" + placeId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(nameof(placeId));
        internal static void Add(ICollection<FavoriteGame> favorites, string name, string input)
        {
            name = name.Trim();
            if (name.Length is < 1 or > 60) throw new InvalidDataException("Enter a name of 1–60 characters.");
            if (!TryParsePlace(input, out long id)) throw new InvalidDataException("Paste a Roblox /games/ link or a positive place ID.");
            if (favorites.Any(f => f.PlaceId == id)) throw new InvalidDataException("That game is already in your favorites.");
            if (favorites.Count >= 50) throw new InvalidDataException("You can save up to 50 favorites.");
            favorites.Add(new FavoriteGame { Name = name, PlaceId = id });
        }
    }
}
