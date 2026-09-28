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
            AccentTheme.Lavender => Color.FromRgb(0xB3, 0x94, 0xF6),
            AccentTheme.Mint => Color.FromRgb(0x66, 0xDC, 0xB1),
            AccentTheme.Peach => Color.FromRgb(0xFF, 0xB0, 0x87),
            AccentTheme.Gold => Color.FromRgb(0xE4, 0xC1, 0x56),
            AccentTheme.Teal => Color.FromRgb(0x21, 0xA6, 0x9B),
            AccentTheme.Indigo => Color.FromRgb(0x66, 0x78, 0xE8),
            AccentTheme.Coral => Color.FromRgb(0xF3, 0x7C, 0x83),
            AccentTheme.Sky => Color.FromRgb(0x7E, 0xCE, 0xF4),
            AccentTheme.Fuchsia => Color.FromRgb(0xD4, 0x65, 0xD9),
            AccentTheme.Ice => Color.FromRgb(0xCB, 0xEC, 0xEF),
            AccentTheme.Custom => ThemeColors.Parse(App.Settings.Prop.CustomAccentColor, Color.FromRgb(0x7C, 0x6F, 0xD8)),
            _                    => Color.FromRgb(0x7C, 0x6F, 0xD8) // Onyx Violet
        };
    }
}
