using System.Windows;
namespace Onyxstrap.Integrations
{
    internal sealed class OverlayOffset
    {
        public double X { get; set; } = 28;
        public double Y { get; set; } = 90;
        public bool Right { get; set; }
        public bool Bottom { get; set; }
    }
    internal static class OverlayPlacement
    {
        internal static Point Resolve(OverlayOffset position, Rect bounds, double width, double height)
        {
            double x = double.IsFinite(position.X) ? Math.Max(0, position.X) : 28;
            double y = double.IsFinite(position.Y) ? Math.Max(0, position.Y) : 90;
            return new Point(Math.Clamp(position.Right ? bounds.Right - width - x : bounds.Left + x, bounds.Left, Math.Max(bounds.Left,bounds.Right-width)),
                Math.Clamp(position.Bottom ? bounds.Bottom-height-y : bounds.Top+y, bounds.Top, Math.Max(bounds.Top,bounds.Bottom-height)));
        }
        internal static OverlayOffset Capture(Point point, Rect bounds, double width, double height, bool snap)
        {
            double maxX = Math.Max(0,bounds.Width-width), maxY = Math.Max(0,bounds.Height-height);
            double x = Math.Clamp(point.X-bounds.Left,0,maxX), y = Math.Clamp(point.Y-bounds.Top,0,maxY);
            bool right = snap && maxX-x <= 24 && x > maxX/2;
            bool bottom = snap && maxY-y <= 24 && y > maxY/2;
            if (snap && x <= 24) x = Math.Min(12,maxX);
            if (snap && y <= 24) y = Math.Min(12,maxY);
            return new OverlayOffset { X = right ? Math.Min(12,maxX) : x, Y = bottom ? Math.Min(12,maxY) : y, Right = right, Bottom = bottom };
        }
    }
}
