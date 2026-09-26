using System.Windows.Media;

namespace Onyxstrap.Extensions
{
    public static class AccentThemeEx
    {
        public static Color GetColor(this AccentTheme theme) => theme switch
        {
            AccentTheme.Crimson  => Color.FromRgb(0xE5, 0x48, 0x4D),
            AccentTheme.Ocean    => Color.FromRgb(0x3E, 0x9C, 0xEA),
            AccentTheme.Emerald  => Color.FromRgb(0x46, 0xA7, 0x58),
            AccentTheme.Amber    => Color.FromRgb(0xF5, 0xA6, 0x23),
            AccentTheme.Rose     => Color.FromRgb(0xE9, 0x3D, 0x82),
            AccentTheme.Cyan     => Color.FromRgb(0x00, 0xB8, 0xD9),
            AccentTheme.Slate    => Color.FromRgb(0x8E, 0x95, 0x9E),
            AccentTheme.Magma    => Color.FromRgb(0xF0, 0x6A, 0x2C),
            AccentTheme.Lime     => Color.FromRgb(0x9A, 0xDB, 0x20),
            _                    => Color.FromRgb(0x7C, 0x6F, 0xD8) // Onyx Violet
        };
    }
}
