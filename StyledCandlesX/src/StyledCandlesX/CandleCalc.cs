using System;
using System.Drawing;

namespace StyledCandlesX
{
    public static class ColorMath
    {
        public static Color Lighten(Color c, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return Color.FromArgb(c.A,
                (int)Math.Round(c.R + (255 - c.R) * t),
                (int)Math.Round(c.G + (255 - c.G) * t),
                (int)Math.Round(c.B + (255 - c.B) * t));
        }

        public static Color WithOpacity(Color c, int pct)
        {
            pct = Math.Clamp(pct, 0, 100);
            return Color.FromArgb((255 * pct + 50) / 100, c.R, c.G, c.B);
        }
    }

    public struct CandleShape
    {
        public int CenterX;
        public int HalfWidth;
        public int BodyTop;
        public int BodyBottom;
        public int WickTop;
        public int WickBottom;
        public int Pitch;

        public int Left => CenterX - HalfWidth;
        public int BodyWidth => HalfWidth * 2;
        public int BodyHeight => BodyBottom - BodyTop;
    }

    public static class CandleGeometry
    {
        public const int ThinPitch = 3;

        public static int Pitch(int xBar, int xNextBar, int fallback)
        {
            int p = xNextBar - xBar;
            return p > 0 ? p : Math.Max(fallback, 1);
        }

        public static void Compute(ref CandleShape s, int xBar, int pitch, double bodyWidthPercent,
            double horizontalOffsetPercent, int yOpen, int yClose, int yHigh, int yLow)
        {
            s.Pitch = pitch;
            s.CenterX = xBar + pitch / 2 + (int)(pitch * horizontalOffsetPercent / 100.0);
            s.HalfWidth = Math.Max(1, (int)(pitch * bodyWidthPercent / 100.0 / 2.0));
            s.BodyTop = Math.Min(yOpen, yClose);
            s.BodyBottom = Math.Max(yOpen, yClose);
            if (s.BodyBottom == s.BodyTop) s.BodyBottom = s.BodyTop + 1;
            s.WickTop = yHigh;
            s.WickBottom = yLow;
        }
    }

    public static class Glow
    {
        public static int LayerAlpha(int opacityPercent, int layer, int layers)
        {
            layers = Math.Max(1, layers);
            layer = Math.Clamp(layer, 1, layers);
            int a = (255 * Math.Clamp(opacityPercent, 0, 100) + 50) / 100;
            return a * (layers - layer + 1) / layers;
        }

        public static int Step(int thickness, int layers) => Math.Max(1, thickness / Math.Max(1, layers));
    }
}
