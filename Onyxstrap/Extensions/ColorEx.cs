using System.Windows.Media;

namespace Onyxstrap.Extensions
{
    public static class ColorEx
    {
        public static Color Lerp(this Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);

            return Color.FromArgb(
                (byte)(from.A + (to.A - from.A) * amount),
                (byte)(from.R + (to.R - from.R) * amount),
                (byte)(from.G + (to.G - from.G) * amount),
                (byte)(from.B + (to.B - from.B) * amount));
        }
    }
}
