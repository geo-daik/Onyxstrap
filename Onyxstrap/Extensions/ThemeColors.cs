using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Onyxstrap.Extensions
{
    internal static class ThemeColors
    {
        internal static bool TryParse(string? text, out Color color)
        {
            color = Colors.Transparent;
            string value = (text ?? "").Trim();
            if (value.StartsWith('#')) value = value[1..];
            if (value.Length != 6 || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb)) return false;
            color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        internal static Color Parse(string? text, Color fallback) => TryParse(text, out var color) ? color : fallback;
        internal static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        internal static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
        private static double Luminance(Color c)
        {
            static double Linear(byte v) { double s = v / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        internal static double Contrast(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
        internal static Color TextOn(Color background) => Contrast(background, Colors.Black) >= Contrast(background, Colors.White) ? Colors.Black : Colors.White;
        internal static Color Accent(Models.Persistable.Settings settings) => settings.AccentTheme == AccentTheme.Custom
            ? Parse(settings.CustomAccentColor, Color.FromRgb(124, 111, 216)) : settings.AccentTheme.GetColor();

        internal static void ApplySurfaces(ResourceDictionary resources, Color accent, bool dark)
        {
            Color background = Mix(dark ? Color.FromRgb(15, 15, 18) : Color.FromRgb(250, 250, 252), accent, dark ? .07 : .025);
            resources["ApplicationBackgroundColor"] = background;
            resources["ApplicationBackgroundBrush"] = new LinearGradientBrush(Mix(background, accent, .04), background, 90);
            string[] names = { "Base", "Secondary", "Tertiary", "Quarternary" };
            for (int i = 0; i < names.Length; i++)
                resources[$"SolidBackgroundFillColor{names[i]}Brush"] = new SolidColorBrush(Mix(background, dark ? Colors.White : Colors.Black, .025 + i * .025));
            resources["OnyxHeroStart"] = Color.FromArgb(38, accent.R, accent.G, accent.B);
            resources["OnyxHeroEnd"] = Color.FromArgb(5, accent.R, accent.G, accent.B);
        }

        internal static Color? FromArtwork(BitmapSource? source)
        {
            if (source is null || source.PixelWidth == 0 || source.PixelHeight == 0) return null;
            try
            {
                var small = new TransformedBitmap(source, new ScaleTransform(32d / source.PixelWidth, 32d / source.PixelHeight));
                var bitmap = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
                int stride = bitmap.PixelWidth * 4;
                var pixels = new byte[stride * bitmap.PixelHeight];
                bitmap.CopyPixels(pixels, stride, 0);
                var bins = new Dictionary<int, (double Score, long R, long G, long B, int Count)>();
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] < 128) continue;
                    int r = pixels[i + 2], g = pixels[i + 1], b = pixels[i];
                    int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                    if (max < 35 || min > 230) continue;
                    int key = (r / 32 << 6) | (g / 32 << 3) | b / 32;
                    var bin = bins.GetValueOrDefault(key);
                    bins[key] = (bin.Score + .15 + (max - min) / 255d, bin.R + r, bin.G + g, bin.B + b, bin.Count + 1);
                }
                if (bins.Count == 0) return null;
                var best = bins.Values.MaxBy(b => b.Score);
                return Color.FromRgb((byte)(best.R / best.Count), (byte)(best.G / best.Count), (byte)(best.B / best.Count));
            }
            catch { return null; }
        }
    }
}
