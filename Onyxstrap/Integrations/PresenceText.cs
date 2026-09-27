namespace Onyxstrap.Integrations
{
    internal static class PresenceText
    {
        // Discord limits text fields by UTF-8 bytes, not UTF-16 characters.
        public static string? Limit(string? value, int maxBytes = 128)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var result = new StringBuilder();
            int bytes = 0;
            foreach (var rune in value.Trim().EnumerateRunes())
            {
                if (bytes + rune.Utf8SequenceLength > maxBytes)
                    break;
                result.Append(rune.ToString());
                bytes += rune.Utf8SequenceLength;
            }

            if (bytes == 1)
                result.Append(' ');
            return result.ToString();
        }

        public static string? State(string? gameState, bool spotifyEnabled, string? track) =>
            Limit(spotifyEnabled && !string.IsNullOrWhiteSpace(track) ? $"Listening to {track}" : gameState);
    }
}
