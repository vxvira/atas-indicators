using System.Drawing;
using Xunit;

namespace StyledCandlesX.Tests
{
    public class CalcTests
    {
        [Fact]
        public void Pitch_UsesNextBarDistance_AndFallsBack()
        {
            Assert.Equal(7, CandleGeometry.Pitch(100, 107, 99));
            Assert.Equal(9, CandleGeometry.Pitch(100, 100, 9));
            Assert.Equal(9, CandleGeometry.Pitch(100, 90, 9));
            Assert.Equal(1, CandleGeometry.Pitch(100, 100, 0));
        }

        [Fact]
        public void Width_ScalesWithPitch_NotAFixedValue()
        {
            var a = new CandleShape(); var b = new CandleShape();
            CandleGeometry.Compute(ref a, 0, 40, 80, 0, 10, 20, 5, 25);
            CandleGeometry.Compute(ref b, 0, 10, 80, 0, 10, 20, 5, 25);
            Assert.Equal(32, a.BodyWidth);
            Assert.Equal(8, b.BodyWidth);
            Assert.Equal(20, a.CenterX);
            Assert.Equal(5, b.CenterX);
        }

        [Fact]
        public void Offset_IsPercentOfPitch()
        {
            var s = new CandleShape();
            CandleGeometry.Compute(ref s, 100, 20, 50, 25, 0, 10, 0, 10);
            Assert.Equal(100 + 10 + 5, s.CenterX);
            CandleGeometry.Compute(ref s, 100, 20, 50, -25, 0, 10, 0, 10);
            Assert.Equal(100 + 10 - 5, s.CenterX);
        }

        [Fact]
        public void Doji_GetsOnePixelBody_AndMinHalfWidth()
        {
            var s = new CandleShape();
            CandleGeometry.Compute(ref s, 0, 1, 80, 0, 50, 50, 40, 60);
            Assert.Equal(1, s.BodyHeight);
            Assert.Equal(1, s.HalfWidth);
        }

        [Fact]
        public void Body_OrderedRegardlessOfDirection()
        {
            var s = new CandleShape();
            CandleGeometry.Compute(ref s, 0, 10, 80, 0, 30, 10, 5, 35);
            Assert.Equal(10, s.BodyTop);
            Assert.Equal(30, s.BodyBottom);
        }

        [Fact]
        public void GlowAlpha_DecreasesOutwards()
        {
            int a1 = Glow.LayerAlpha(40, 1, 3), a2 = Glow.LayerAlpha(40, 2, 3), a3 = Glow.LayerAlpha(40, 3, 3);
            Assert.True(a1 > a2 && a2 > a3 && a3 > 0);
            Assert.Equal(102, a1);
            Assert.Equal(0, Glow.LayerAlpha(0, 1, 3));
            Assert.Equal(255, Glow.LayerAlpha(100, 1, 1));
        }

        [Fact]
        public void GlowStep_AtLeastOne()
        {
            Assert.Equal(1, Glow.Step(0, 3));
            Assert.Equal(2, Glow.Step(6, 3));
            Assert.Equal(3, Glow.Step(3, 1));
        }

        [Fact]
        public void ColorMath_Works()
        {
            var l = ColorMath.Lighten(Color.FromArgb(200, 0, 100, 200), 0.5);
            Assert.Equal(200, l.A);
            Assert.Equal(128, l.R); Assert.Equal(178, l.G); Assert.Equal(228, l.B);
            Assert.Equal(128, ColorMath.WithOpacity(Color.Red, 50).A);
            Assert.Equal(255, ColorMath.WithOpacity(Color.Red, 500).A);
        }

        [Fact]
        public void Defaults_MatchReferenceScreenshot()
        {
            var i = new SimpleHollowCandlesX();
            Assert.Equal(Color.FromArgb(60, 202, 18), i.BullFillColor);
            Assert.Equal(Color.FromArgb(84, 32, 138), i.BearFillColor);
            Assert.True(i.GlowEnabled);
            Assert.Equal(80m, i.BodyWidthPercent);
            Assert.True(i.HideNativeCandles);
        }

        [Fact]
        public void StyleSignature_ChangesWithColours()
        {
            var a = new CandleStyle { BullFill = Color.Lime };
            var b = new CandleStyle { BullFill = Color.Lime };
            Assert.Equal(a.Signature(), b.Signature());
            b.GlowOpacity = 10;
            Assert.NotEqual(a.Signature(), b.Signature());
        }
    }
}
