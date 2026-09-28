using System.Text.Json.Nodes;
using Onyxstrap.Integrations;

namespace Onyxstrap
{
    internal static class PreferenceBackup
    {
        // Explicit portable preferences only: never copy accounts, executable paths, or integration commands.
        private static readonly string[] Fields = {
            "Theme", "AccentTheme", "CustomAccentColor", "SpotifyColorMode", "SpotifyAccentColor", "SpotifyCompactMode", "ColorProfiles",
            "EnableActivityTracking", "UseDiscordRichPresence", "HideRPCButtons", "ShowAccountOnRichPresence", "EnableSpotifyOverlay",
            "SpotifyOverlayKey", "SpotifyOverlayModifiers", "ShowSpotifyOnRichPresence", "ShowServerDetails", "SpotifySnapToEdges", "FavoriteGames",
            "ConfirmLaunches", "WPFSoftwareRender", "RebrandGameWindow"
        };
        internal static string Export(Settings settings)
        {
            var source = JsonSerializer.SerializeToNode(settings)!.AsObject();
            var portable = new JsonObject();
            foreach (string field in Fields) portable[field] = source[field]?.DeepClone();
            return new JsonObject { ["Format"] = "OnyxstrapPreferences", ["Version"] = 1, ["Preferences"] = portable }
                .ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        internal static Settings Import(string json, Settings current)
        {
            if (json.Length > 1024 * 1024) throw new InvalidDataException("The backup is too large.");
            var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Invalid backup.");
            if (root["Format"]?.GetValue<string>() != "OnyxstrapPreferences" || root["Version"]?.GetValue<int>() != 1)
                throw new InvalidDataException("Unsupported backup format or version.");
            var preferences = root["Preferences"]?.AsObject() ?? throw new InvalidDataException("Missing preferences.");
            if (preferences.Any(p => !Fields.Contains(p.Key)) || preferences.Count == 0)
                throw new InvalidDataException("This backup contains unsupported settings.");
            var merged = JsonSerializer.SerializeToNode(current)!.AsObject();
            foreach (var pair in preferences) {
                if (pair.Value is null) throw new InvalidDataException("The backup contains an empty preference.");
                merged[pair.Key] = pair.Value.DeepClone();
            }
            var result = merged.Deserialize<Settings>() ?? throw new InvalidDataException("Invalid preferences.");
            Validate(result);
            return result;
        }
        private static void Validate(Settings s)
        {
            if (!Enum.IsDefined(s.Theme) || !Enum.IsDefined(s.AccentTheme) || !Enum.IsDefined(s.SpotifyColorMode)
                || !ThemeColors.TryParse(s.CustomAccentColor, out _) || !ThemeColors.TryParse(s.SpotifyAccentColor, out _)
                || !OverlayShortcut.TryCreate(s.SpotifyOverlayKey, s.SpotifyOverlayModifiers, out _))
                throw new InvalidDataException("The backup contains an invalid theme or shortcut.");
            if (s.FavoriteGames is null || s.FavoriteGames.Count > 50 || s.FavoriteGames.Any(f => f is null || f.PlaceId <= 0 || string.IsNullOrWhiteSpace(f.Name) || f.Name.Length > 60)
                || s.FavoriteGames.Select(f => f.PlaceId).Distinct().Count() != s.FavoriteGames.Count)
                throw new InvalidDataException("The backup contains invalid favorites.");
            if (s.ColorProfiles is null || s.ColorProfiles.Count > 32 || s.ColorProfiles.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 60
                || !Enum.IsDefined(p.Theme) || !Enum.IsDefined(p.Accent) || !Enum.IsDefined(p.PlayerMode)
                || !ThemeColors.TryParse(p.CustomAccent, out _) || !ThemeColors.TryParse(p.PlayerAccent, out _)))
                throw new InvalidDataException("The backup contains invalid color profiles.");
        }
        internal static void Apply(Settings imported, Settings target)
        {
            Validate(imported);
            foreach (string field in Fields) {
                var property = typeof(Settings).GetProperty(field)!;
                property.SetValue(target, property.GetValue(imported));
            }
        }
        internal static void Write(string path, string json)
        {
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try { File.WriteAllText(temp, json); File.Move(temp, path, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
