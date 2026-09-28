namespace Onyxstrap.Models
{
    public class ColorProfile
    {
        public string Name { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Dark;
        public AccentTheme Accent { get; set; } = AccentTheme.OnyxViolet;
        public string CustomAccent { get; set; } = "#7C6FD8";
        public SpotifyColorMode PlayerMode { get; set; }
        public string PlayerAccent { get; set; } = "#1ED760";
        public bool Compact { get; set; }
    }
}
