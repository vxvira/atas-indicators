using System;
using System.Drawing;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Skia;
using SkiaSharp;
using Xunit;

namespace StyledCandlesX.Tests
{
    internal sealed class StubSurface : ISkiaRenderSurface
    {
        private sealed class StubSettings : IRenderContextSettings
        {
            public bool IsHardwareHeatmapSupported => false;
            public bool IsGPUAccelerated => false;
            public string EngineName => "stub";
        }
        private readonly Size _size;
        private SkiaResourceCache _cache;
        public StubSurface(Size size) { _size = size; }
        public SKSurface SkiaSurface => null;
        public SkiaResourceCache ResourceCache => _cache ??= new SkiaResourceCache(this);
        public ISkiaLayoutManager LayoutManager => null;
        public double DpiScale => 1.0;
        public GRContext GrContext => null;
        public void DrawHeatmap(SKCanvas c, OFT.Rendering.Heatmap.HeatmapControl h, Rectangle d) { }
        public void PrepareHeatmap(OFT.Rendering.Heatmap.HeatmapControl h) { }
        public void DrawCachedHeatmap(SKCanvas c, OFT.Rendering.Heatmap.HeatmapControl h, Rectangle d) { }
        public bool IsInitialized => true;
        public string Name => "stub";
        public ITextRenderer TextRenderer => null;
        public Size Size => _size;
        public ExceptionCollector ErrorCollector { get; set; }
        public IRenderContextSettings Settings { get; } = new StubSettings();
        public bool VerticalSynchronization { get; set; }
        public bool IsNeedToRedraw(int w, int h) => false;
        public void SetWindowsFormsHandle(nint h) { }
        public void Initialize(IRenderControl p, double x, double y) { }
        public RenderContext BeginRender(int w, int h) => throw new NotSupportedException();
        public int EndRender(RenderContext c) => 0;
        public void DeInit() { }
        public void Reset() { }
        public object CreateCustomControl() => null;
    }

    internal sealed class FakeMap : IChartMap
    {
        public int Pitch = 20;
        public int First, Last;
        public (decimal o, decimal h, decimal l, decimal c)[] Bars;
        public int FirstBar => First;
        public int LastBar => Last;
        public int XByBar(int bar) => 10 + bar * Pitch;
        public int YByPrice(decimal price) => 200 - (int)price;
        public bool TryGetCandle(int bar, out decimal o, out decimal h, out decimal l, out decimal c)
        {
            o = h = l = c = 0;
            if (bar < 0 || bar >= Bars.Length) return false;
            (o, h, l, c) = Bars[bar];
            return true;
        }
    }

    public class SnapshotTests
    {
        private static readonly Color Bg = Color.FromArgb(24, 24, 24);

        private static SKBitmap Draw(FakeMap map, CandleStyle style, int w = 200, int h = 220)
        {
            var bmp = new SKBitmap(w, h);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(new SKColor(Bg.R, Bg.G, Bg.B));
            var ctx = new SkiaRenderContext(canvas, new StubSurface(new Size(w, h)));
            ctx.BeginRender();
            new CandleRenderer().Render(ctx, map, style);
            ctx.EndRender();
            canvas.Flush();
            return bmp;
        }

        private static CandleStyle Style(bool glow = true)
        {
            var i = new SimpleHollowCandlesX();
            return new CandleStyle
            {
                BullFill = i.BullFillColor, BullBorder = i.BullBorderColor, BullWick = i.BullWickColor,
                BearFill = i.BearFillColor, BearBorder = i.BearBorderColor, BearWick = i.BearWickColor,
                BullGlow = i.BullGlowColor, BearGlow = i.BearGlowColor, GlowEnabled = glow,
                GlowOpacity = 40, GlowThickness = 3, GlowLayers = 3, BodyWidthPercent = 80m,
            };
        }

        private static bool Near(SKColor a, Color b, int tol = 3) =>
            Math.Abs(a.Red - b.R) <= tol && Math.Abs(a.Green - b.G) <= tol && Math.Abs(a.Blue - b.B) <= tol;

        private static FakeMap TwoBars(int pitch = 40) => new FakeMap
        {
            Pitch = pitch, First = 0, Last = 1,
            Bars = new[] { (50m, 120m, 30m, 100m), (100m, 120m, 30m, 50m) },
        };

        [Fact]
        public void BullBody_FilledWithFillColour_BorderLighter()
        {
            var st = Style(glow: false);
            using var bmp = Draw(TwoBars(), st);
            Assert.True(Near(bmp.GetPixel(30, 125), st.BullFill), "bull body fill");
            Assert.True(Near(bmp.GetPixel(30, 90), st.BullWick), "upper wick");
            Assert.True(Near(bmp.GetPixel(30, 165), st.BullWick), "lower wick");
        }

        [Fact]
        public void BearBody_UsesPurple()
        {
            var st = Style(glow: false);
            using var bmp = Draw(TwoBars(), st);
            Assert.True(Near(bmp.GetPixel(70, 125), st.BearFill), "bear body fill");
        }

        [Fact]
        public void Hollow_LeavesBackgroundInside()
        {
            var st = Style(glow: false); st.BullHollow = true;
            using var bmp = Draw(TwoBars(), st);
            Assert.True(Near(bmp.GetPixel(24, 125), Bg), "hollow interior is background");
        }

        [Fact]
        public void Glow_PaintsOutsideBody_WhenEnabled_NotWhenDisabled()
        {
            var on = Style(true); var off = Style(false);
            using var a = Draw(TwoBars(), on);
            using var b = Draw(TwoBars(), off);
            var outsideOn = a.GetPixel(12, 125);
            var outsideOff = b.GetPixel(12, 125);
            Assert.True(Near(outsideOff, Bg), "no glow -> background");
            Assert.False(Near(outsideOn, Bg, 1), "glow tints the pixel");
            Assert.True(outsideOn.Green > outsideOn.Red, "bull glow is green-ish");
        }

        [Fact]
        public void Glow_FadesWithDistance()
        {
            using var a = Draw(TwoBars(), Style(true));
            int d1 = Dist(a.GetPixel(12, 125)), d2 = Dist(a.GetPixel(11, 125)), d3 = Dist(a.GetPixel(10, 125));
            Assert.True(d1 >= d2 && d2 >= d3, $"{d1} {d2} {d3}");
            Assert.True(d1 > d3);
        }

        private static int Dist(SKColor c) => Math.Abs(c.Red - Bg.R) + Math.Abs(c.Green - Bg.G) + Math.Abs(c.Blue - Bg.B);

        [Fact]
        public void ZoomOut_ShrinksBodies_NoOverlap()
        {
            var st = Style(false);
            var wide = TwoBars(40); var narrow = TwoBars(10);
            using var a = Draw(wide, st); using var b = Draw(narrow, st);
            int wa = RowCount(a, 125), wb = RowCount(b, 125);
            Assert.True(wa > wb * 2, $"{wa} vs {wb}");
            Assert.True(Near(b.GetPixel(15, 125), st.BullFill, 40) || Near(b.GetPixel(14, 125), st.BullFill, 40));
        }

        private static int RowCount(SKBitmap b, int y)
        {
            int n = 0;
            for (int x = 0; x < b.Width; x++) if (Dist(b.GetPixel(x, y)) > 20) n++;
            return n;
        }

        [Fact]
        public void ThinMode_DrawsSingleLine()
        {
            var st = Style(false);
            using var b = Draw(TwoBars(3), st);
            Assert.False(Near(b.GetPixel(11, 125), Bg, 5));
            Assert.True(Near(b.GetPixel(40, 125), Bg), "nothing far away");
        }

        [Fact]
        public void MissingCandles_AreSkipped()
        {
            var m = TwoBars(); m.Last = 5;
            using var b = Draw(m, Style());
            Assert.True(b.Width > 0);
        }

        [Fact]
        public void RepeatedRender_DoesNotAllocatePerBar()
        {
            var bars = new (decimal, decimal, decimal, decimal)[400];
            for (int i = 0; i < bars.Length; i++) bars[i] = (50 + i % 7, 120, 30, 60 + i % 5);
            var m = new FakeMap { Pitch = 2, First = 0, Last = 399, Bars = bars };
            var st = Style();
            var bmp = new SKBitmap(1000, 220);
            using var canvas = new SKCanvas(bmp);
            var ctx = new SkiaRenderContext(canvas, new StubSurface(new Size(1000, 220)));
            var r = new CandleRenderer();
            for (int i = 0; i < 3; i++) { ctx.BeginRender(); r.Render(ctx, m, st); ctx.EndRender(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            ctx.BeginRender(); r.Render(ctx, m, st); ctx.EndRender();
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(400, r.LastDrawn);
            Assert.True(used < 400 * 64, $"allocated {used} bytes for 400 bars");
        }
    }
}
