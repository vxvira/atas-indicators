using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OFT.Rendering.Skia;
using SkiaSharp;
using VolumeProfileX;
using Xunit;

namespace VolumeProfileX.Tests
{
    internal static class AtasResolver
    {
        private const string Dir = @"C:\Program Files\ATAS X";

        [ModuleInitializer]
        internal static void Init()
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (ctx, name) =>
            {
                var p = Path.Combine(Dir, name.Name + ".dll");
                return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
            };
            try { NativeLibrary.Load(Path.Combine(Dir, "libSkiaSharp.dll")); } catch { }
        }
    }

    internal sealed class StubSurface : ISkiaRenderSurface, IDisposable
    {
        public SKSurface Surface;
        public StubSurface(int w, int h) { Surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul)); Cache = new SkiaResourceCache(this); }
        public SKSurface SkiaSurface => Surface;
        public SkiaResourceCache Cache;
        public SkiaResourceCache ResourceCache => Cache;
        public ISkiaLayoutManager LayoutManager => null;
        public double DpiScale => 1.0;
        public GRContext GrContext => null;
        public void DrawHeatmap(SKCanvas c, OFT.Rendering.Heatmap.HeatmapControl h, Rectangle d) => throw new NotSupportedException();
        public void PrepareHeatmap(OFT.Rendering.Heatmap.HeatmapControl h) => throw new NotSupportedException();
        public void DrawCachedHeatmap(SKCanvas c, OFT.Rendering.Heatmap.HeatmapControl h, Rectangle d) => throw new NotSupportedException();
        public bool IsInitialized => true;
        public string Name => "stub";
        public OFT.Rendering.Context.ITextRenderer TextRenderer => null;
        public Size Size => new Size(Surface.Canvas.DeviceClipBounds.Width, Surface.Canvas.DeviceClipBounds.Height);
        public OFT.Rendering.Control.ExceptionCollector ErrorCollector { get; set; }
        public OFT.Rendering.Context.IRenderContextSettings Settings => new StubSettings();
        public bool VerticalSynchronization { get; set; }
        public bool IsLikelyReady => true;
        public bool IsNeedToRedraw(int width, int height) => false;
        public void SetWindowsFormsHandle(IntPtr h) { }
        public void Initialize(OFT.Rendering.Control.IRenderControl parent, double dx, double dy) { }
        public OFT.Rendering.Context.RenderContext BeginRender(int width, int height) => throw new NotSupportedException();
        public int EndRender(OFT.Rendering.Context.RenderContext c) => 0;
        public void DeInit() { }
        public void Reset() { }
        public object CreateCustomControl() => null;
        public void Dispose() => Surface.Dispose();
    }

    internal sealed class StubSettings : OFT.Rendering.Context.IRenderContextSettings
    {
        public bool IsHardwareHeatmapSupported => false;
        public bool IsGPUAccelerated => false;
        public string EngineName => "stub";
    }

    internal sealed class LinearLayout : IProfileLayout
    {
        public Rectangle Clip { get; set; }
        public int X0 = 100, BarPx = 8, RowPx = 4; public long TopTick = 1100; public int Y0 = 20;
        public int BarX(int bar) => X0 + bar * BarPx;
        public int BarWidth(int bar) => BarPx;
        public int TickTopY(long t) => Y0 + (int)(TopTick - t) * RowPx;
        public int TickBottomY(long t) => TickTopY(t) + RowPx;
    }

    public class RenderTests
    {
        private const int W = 900, H = 520;
        private static readonly DateTime T0 = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);

        private static ProfileSnapshot BuildSnap(int rpb = 1)
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            for (int b = 0; b < 60; b++)
            {
                var lv = new List<LevelIn>();
                for (long t = 1000; t <= 1100; t++)
                {
                    double g = Math.Exp(-Math.Pow((t - 1050) / 18.0, 2));
                    double v = Math.Round(g * 20 + (t == 1062 ? 40 : 0) + 1);
                    double ask = Math.Round(v * (t % 3 == 0 ? 0.7 : 0.35));
                    lv.Add(new LevelIn(t, v, v - ask, ask, 2));
                }
                w.Feed(b, T0.AddMinutes(b), lv);
            }
            var s = new ProfileSnapshot(); s.Update(w, rpb, 70, 85, 15, 0.25); return s;
        }

        private static SKBitmap Render(ProfileSnapshot s, ProfileSettings st, out ProfileRenderer r)
        {
            using var surf = new StubSurface(W, H);
            var canvas = surf.SkiaSurface.Canvas;
            canvas.Clear(new SKColor(20, 20, 20));
            var ctx = new SkiaRenderContext(canvas, surf, new Size(W, H));
            ctx.BeginRender();
            r = new ProfileRenderer();
            var lay = new LinearLayout { Clip = new Rectangle(0, 0, W, H) };
            r.Draw(ctx, s, st, lay);
            ctx.EndRender();
            canvas.Flush();
            var bmp = new SKBitmap(W, H);
            surf.SkiaSurface.ReadPixels(bmp.Info, bmp.GetPixels(), bmp.RowBytes, 0, 0);
            return bmp;
        }

        private static Color Px(SKBitmap b, int x, int y) { var c = b.GetPixel(x, y); return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue); }
        private static Color OverBg(Color c) { double a = c.A / 255.0; return Color.FromArgb(255, (int)Math.Round(c.R * a + 20 * (1 - a)), (int)Math.Round(c.G * a + 20 * (1 - a)), (int)Math.Round(c.B * a + 20 * (1 - a))); }
        private static bool Near(Color a, Color b, int tol = 6) { b = OverBg(b); return Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol; }

        private static string Out(string name)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "snapshots"); Directory.CreateDirectory(dir);
            return Path.Combine(dir, name + ".png");
        }

        private static void Save(SKBitmap b, string name)
        {
            using var img = SKImage.FromBitmap(b); using var d = img.Encode(SKEncodedImageFormat.Png, 100);
            using var f = File.Create(Out(name)); d.SaveTo(f);
        }

        private static void Golden(SKBitmap b, string name)
        {
            Save(b, name);
            var repo = FindRepoRoot();
            var gdir = Path.Combine(repo, "tests", "golden"); Directory.CreateDirectory(gdir);
            var gp = Path.Combine(gdir, name + ".png");
            if (!File.Exists(gp)) { File.Copy(Out(name), gp); return; }
            using var g = SKBitmap.Decode(gp);
            Assert.Equal(g.Width, b.Width); Assert.Equal(g.Height, b.Height);
            int diff = 0;
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) if (g.GetPixel(x, y) != b.GetPixel(x, y)) diff++;
            Assert.True(diff == 0, name + ": " + diff + " pixels differ from golden");
        }

        private static string FindRepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "VolumeProfileX.csproj"))) d = d.Parent;
            return d?.FullName ?? AppContext.BaseDirectory;
        }

        private static ProfileSettings Base()
        {
            var st = new ProfileSettings();
            st.WidthUnit = WidthUnit.Pixels; st.RightWidth = 200; st.LeftWidth = 120; st.MaxWidthPx = 0;
            st.RowsPerBucket = 1; st.ShowHandles = false; st.Shape = ProfileShape.SeparateRows;
            return st;
        }

        [Fact]
        public void Default_look_matches_reference_layout()
        {
            var s = BuildSnap();
            var st = Base(); st.ShowBox = true; st.ShowHandles = true; st.ShowVwap = true;
            using var bmp = Render(s, st, out var r);
            Golden(bmp, "default_look");
            int bx = r.LastBorderX;
            Assert.Equal(100, bx);
            Assert.Equal(100, r.LastBox.Left); Assert.Equal(100 + 60 * 8, r.LastBox.Right);
            int pocY = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            Assert.True(Near(Px(bmp, bx + 6, pocY + 1), st.PocRowColor) || Near(Px(bmp, bx + 6, pocY), st.PocRowColor), "POC row colour");
            var left = Px(bmp, bx - 4, 20 + (1100 - 1050) * 4 + 2);
            Assert.True(Near(left, st.BuyColor) || Near(left, st.SellColor), "left split colour " + left);
        }

        [Fact]
        public void Value_area_rows_use_inside_colour_and_outside_rows_outside_colour()
        {
            var s = BuildSnap();
            var st = Base(); st.ShowHvn = false; st.ColorPocRow = false; st.RightData = SideData.TotalVolume; st.LeftData = SideData.None;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBorder = false; st.ShowBox = false;
            using var bmp = Render(s, st, out _);
            int inside = s.PocRow, outside = 0;
            Assert.False((s.Flags[outside] & RowFlags.InValueArea) != 0);
            int yIn = 20 + (1100 - (int)s.RowFirstTick(inside)) * 4 + 1;
            Assert.True(Near(Px(bmp, 100 + 3, yIn), st.InsideVaColor, 8), "inside " + Px(bmp, 103, yIn));
            int yOut = 20 + (1100 - (int)s.RowFirstTick(outside)) * 4 + 1;
            Assert.True(Near(Px(bmp, 100 + 1, yOut), st.OutsideVaColor, 8), "outside " + Px(bmp, 101, yOut));
        }

        [Fact]
        public void Full_fill_has_no_gaps_visible_rows_have_gaps()
        {
            var s = BuildSnap();
            var st = Base(); st.Shape = ProfileShape.SeparateRows; st.ShowBox = false; st.ShowBorder = false; st.LeftData = SideData.None;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false;
            using (var full = Render(s, st, out _))
            {
                int bg = 0;
                for (int y = 20 + 40 * 4; y < 20 + 60 * 4; y++) if (Near(Px(full, 104, y), Color.FromArgb(20, 20, 20), 2)) bg++;
                Assert.Equal(0, bg);
            }
            st.Rows = RowMode.VisibleRows; st.RowGapPx = 2;
            using (var vis = Render(s, st, out _))
            {
                Save(vis, "visible_rows");
                int bg = 0;
                for (int y = 20 + 40 * 4; y < 20 + 60 * 4; y++) if (Near(Px(vis, 104, y), Color.FromArgb(20, 20, 20), 2)) bg++;
                Assert.True(bg >= 20, "gap pixels: " + bg);
            }
        }

        [Fact]
        public void Poc_line_extends_to_box_end_and_fixed_px_stops()
        {
            var s = BuildSnap();
            var st = Base(); st.ShowBox = false; st.ShowBorder = false; st.RightData = SideData.None; st.LeftData = SideData.None;
            st.ShowVah = st.ShowVal = st.ShowVwap = false;
            int y = (20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 4) / 2;
            using (var a = Render(s, st, out var r))
            {
                Assert.True(Near(Px(a, r.LastBox.Right - 3, y), st.PocLine.Color, 3) || Near(Px(a, r.LastBox.Right - 3, y - 1), st.PocLine.Color, 3), "reaches box end");
                Assert.False(Near(Px(a, r.LastBox.Right + 30, y), st.PocLine.Color, 3));
            }
            st.PocLine.Extend = LineExtend.FixedPx; st.PocLine.ExtendPx = 50;
            using (var b = Render(s, st, out var r))
            {
                Assert.True(Near(Px(b, 100 + 40, y), st.PocLine.Color, 3) || Near(Px(b, 100 + 40, y - 1), st.PocLine.Color, 3));
                Assert.False(Near(Px(b, 100 + 80, y), st.PocLine.Color, 3) || Near(Px(b, 100 + 80, y - 1), st.PocLine.Color, 3));
            }
            st.PocLine.Extend = LineExtend.ToBoxEnd; st.PocLine.ExtendPx = 40;
            using (var c = Render(s, st, out var r))
                Assert.True(Near(Px(c, r.LastBox.Right + 20, y), st.PocLine.Color, 3) || Near(Px(c, r.LastBox.Right + 20, y - 1), st.PocLine.Color, 3), "extra beyond box");
        }

        [Fact]
        public void Border_on_right_edge_with_left_direction_mirrors_the_profile()
        {
            var s = BuildSnap();
            var st = Base(); st.Edge = BorderEdge.Right; st.Direction = GrowDirection.Left; st.LeftData = SideData.None;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBorder = false; st.ShowBox = false; st.ColorPocRow = false; st.ShowHvn = false;
            using var bmp = Render(s, st, out var r);
            Golden(bmp, "right_edge_left_growth");
            Assert.Equal(r.LastBox.Right, r.LastBorderX);
            int y = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            Assert.True(Near(Px(bmp, r.LastBorderX - 5, y), st.InsideVaColor, 8));
            Assert.False(Near(Px(bmp, r.LastBorderX + 5, y), st.InsideVaColor, 8));
        }

        [Fact]
        public void Mirror_swaps_sides_and_offset_moves_border()
        {
            var s = BuildSnap();
            var st = Base(); st.RightData = SideData.TotalVolume; st.LeftData = SideData.None; st.Mirror = true; st.OffsetPx = 30;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBox = false; st.ColorPocRow = false; st.ShowHvn = false;
            using var bmp = Render(s, st, out var r);
            Assert.Equal(130, r.LastBorderX);
            int y = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            Assert.True(Near(Px(bmp, 130 - 6, y), st.InsideVaColor, 8), "mirrored total now on the left");
            Assert.False(Near(Px(bmp, 130 + 6, y), st.InsideVaColor, 8));
        }

        [Fact]
        public void Percent_width_and_max_cap()
        {
            var s = BuildSnap();
            var st = Base(); st.WidthUnit = WidthUnit.PercentOfWindow; st.RightWidth = 50; st.LeftData = SideData.None; st.MaxWidthPx = 100;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBox = false; st.ShowBorder = false; st.ColorPocRow = false; st.ShowHvn = false;
            using var bmp = Render(s, st, out var r);
            int y = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            Assert.True(Near(Px(bmp, 100 + 97, y), st.InsideVaColor, 8));
            Assert.False(Near(Px(bmp, 100 + 110, y), st.InsideVaColor, 8));
        }

        [Fact]
        public void Delta_buy_sell_sides_use_signed_colours()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, new List<LevelIn> { new LevelIn(1100, 10, 2, 8, 1), new LevelIn(1099, 10, 9, 1, 1) });
            var s = new ProfileSnapshot(); s.Update(w, 1, 70, 85, 15, 0.25);
            var st = Base(); st.RightData = SideData.Delta; st.LeftData = SideData.None;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBox = false; st.ShowBorder = false;
            using var bmp = Render(s, st, out _);
            Assert.True(Near(Px(bmp, 110, 20 + 1), st.BuyColor, 4), "top row delta>0 -> buy colour");
            Assert.True(Near(Px(bmp, 110, 20 + 4 + 1), st.SellColor, 4), "next row delta<0 -> sell colour");
        }

        [Fact]
        public void Split_stacked_puts_buy_then_sell()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, new List<LevelIn> { new LevelIn(1100, 100, 50, 50, 1) });
            var s = new ProfileSnapshot(); s.Update(w, 1, 70, 85, 15, 0.25);
            var st = Base(); st.RightData = SideData.BuySellSplit; st.Split = SplitStyle.Stacked; st.LeftData = SideData.None;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBox = false; st.ShowBorder = false;
            using var bmp = Render(s, st, out _);
            Assert.True(Near(Px(bmp, 100 + 50, 22), st.BuyColor, 4));
            Assert.True(Near(Px(bmp, 100 + 150, 22), st.SellColor, 4));
        }

        [Fact]
        public void Shared_normalisation_scales_both_sides_by_same_max()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, new List<LevelIn> { new LevelIn(1100, 100, 50, 50, 1), new LevelIn(1099, 50, 25, 25, 1) });
            var s = new ProfileSnapshot(); s.Update(w, 1, 70, 85, 15, 0.25);
            var st = Base(); st.WidthUnit = WidthUnit.Pixels; st.RightWidth = 80; st.LeftWidth = 80;
            st.RightData = SideData.TotalVolume; st.LeftData = SideData.BuyVolume; st.ColorPocRow = false; st.ShowHvn = false;
            st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false; st.ShowBox = false; st.ShowBorder = false;
            st.Norm = Normalisation.PerSide;
            using (var a = Render(s, st, out _)) Assert.True(Near(Px(a, 100 - 75, 22), st.BuyColor, 4));
            st.Norm = Normalisation.Shared;
            using (var b = Render(s, st, out _))
            {
                Assert.True(Near(Px(b, 100 - 35, 22), st.BuyColor, 4));
                Assert.False(Near(Px(b, 100 - 45, 22), st.BuyColor, 4));
            }
        }

        [Fact]
        public void Box_handles_labels_and_row_border_render_without_error()
        {
            var s = BuildSnap(rpb: 4);
            var st = Base(); st.ShowBox = true; st.ShowHandles = true; st.BoxFillOpacity = 10; st.Labels = LabelSides.Both; st.RowsPerBucket = 4;
            st.Rows = RowMode.VisibleRows; st.RowBorder = true; st.RowHeightPx = 12; st.LabelMinRowHeight = 8;
            using var bmp = Render(s, st, out var r);
            Golden(bmp, "handles_labels_rows");
            Assert.True(Near(Px(bmp, r.LastBox.Left + 1, r.LastBox.Top + 1), st.HandleColor, 20), "handle");
        }

        [Fact]
        public void Continuous_shape_has_no_row_gaps_and_draws_outline()
        {
            var s = BuildSnap();
            var st = Base();
            st.Shape = ProfileShape.Continuous; st.ContinuousSmooth = false; st.Rows = RowMode.VisibleRows; st.RowGapPx = 3; st.RowBorder = true;
            st.RightData = SideData.TotalVolume; st.LeftData = SideData.None;
            st.ShapeFillSingle = true; st.ShapeFillColor = Color.FromArgb(255, 200, 30, 30);
            st.ShowOutline = true; st.OutlineColor = Color.FromArgb(255, 255, 255, 255); st.OutlineWidth = 1; st.OutlineByValueArea = false; st.OutlineSmooth = false;
            st.ColorPocRow = false; st.ShowHvn = false; st.ShowLvn = false; st.ShowPoc = false; st.ShowVah = false; st.ShowVal = false; st.ShowMedian = false; st.ShowVwap = false;
            using var bmp = Render(s, st, out var r);
            Golden(bmp, "continuous_outline");
            int bx = r.LastBorderX;
            int pocY = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            for (int y = pocY - 6; y <= pocY + 6; y++)
                Assert.True(Near(Px(bmp, bx + 3, y), st.ShapeFillColor), "gap at y=" + y + " " + Px(bmp, bx + 3, y));
            bool found = false;
            for (int x = bx + 1; x < bx + 260 && !found; x++) found = Near(Px(bmp, x, pocY), st.OutlineColor, 10);
            Assert.True(found, "outline pixel on POC row");
        }

        [Fact]
        public void Outline_is_coloured_inside_and_outside_value_area()
        {
            var s = BuildSnap();
            var st = Base();
            st.Shape = ProfileShape.Continuous; st.ContinuousSmooth = false; st.RightData = SideData.TotalVolume; st.LeftData = SideData.None;
            st.ShowOutline = true; st.OutlineByValueArea = true; st.OutlineWidth = 1; st.OutlineSmooth = false;
            st.OutlineInsideColor = Color.FromArgb(255, 250, 250, 250); st.OutlineOutsideColor = Color.FromArgb(255, 10, 250, 10);
            st.ShowPoc = false; st.ShowVah = false; st.ShowVal = false; st.ShowMedian = false; st.ShowVwap = false;
            using var bmp = Render(s, st, out var r);
            bool inside = false, outside = false;
            for (int y = 0; y < bmp.Height; y++) for (int x = r.LastBorderX + 1; x < r.LastBorderX + 260; x++)
            { var c = Px(bmp, x, y); inside |= Near(c, st.OutlineInsideColor, 3); outside |= Near(c, st.OutlineOutsideColor, 3); }
            Assert.True(inside, "inside-VA outline colour present");
            Assert.True(outside, "outside-VA outline colour present");
        }

        [Fact]
        public void Smooth_continuous_fills_solid_with_value_area_colours_and_outline()
        {
            var s = BuildSnap();
            var st = Base();
            st.Shape = ProfileShape.Continuous; st.ContinuousSmooth = false;
            st.RightData = SideData.TotalVolume; st.LeftData = SideData.None;
            st.ColorPocRow = false; st.ShowHvn = false; st.ShowLvn = false;
            st.ShowPoc = false; st.ShowVah = false; st.ShowVal = false; st.ShowMedian = false; st.ShowVwap = false;
            st.ShowOutline = true; st.OutlineByValueArea = true; st.OutlineWidth = 1;
            using var bmp = Render(s, st, out var r);
            Golden(bmp, "smooth_continuous");
            int bx = r.LastBorderX;
            int pocY = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            for (int y = pocY - 7; y <= pocY + 7; y++)
                Assert.True(Near(Px(bmp, bx + 3, y), st.InsideVaColor, 10), "seam/non-fill at y=" + y + " " + Px(bmp, bx + 3, y));
            bool inside = false, outside = false;
            for (int y = 0; y < bmp.Height; y++) for (int x = bx + 1; x < bx + 260; x++)
            { var c = Px(bmp, x, y); inside |= Near(c, st.OutlineInsideColor, 8); outside |= Near(c, st.OutlineOutsideColor, 8); }
            Assert.True(inside && outside, "both outline colours present");
        }

        [Fact]
        public void Outline_off_leaves_no_outline_colour()
        {
            var s = BuildSnap();
            var st = Base();
            st.Shape = ProfileShape.Continuous; st.ContinuousSmooth = false; st.RightData = SideData.TotalVolume; st.LeftData = SideData.None;
            st.ShapeFillSingle = true; st.ShapeFillColor = Color.FromArgb(255, 200, 30, 30);
            st.ShowOutline = false; st.OutlineColor = Color.FromArgb(255, 255, 255, 255);
            st.ColorPocRow = false; st.ShowHvn = false; st.ShowLvn = false; st.ShowPoc = false; st.ShowVah = false; st.ShowVal = false; st.ShowMedian = false; st.ShowVwap = false;
            using var bmp = Render(s, st, out var r);
            int pocY = 20 + (1100 - (int)s.RowFirstTick(s.PocRow)) * 4 + 1;
            for (int x = r.LastBorderX + 1; x < r.LastBorderX + 260; x++)
                Assert.False(Near(Px(bmp, x, pocY), st.OutlineColor, 10), "outline at x=" + x);
        }

        [Fact]
        public void Stacked_split_buy_and_sell_never_overlap()
        {
            var s = BuildSnap();
            var st = Base();
            st.LeftData = SideData.BuySellSplit; st.RightData = SideData.None; st.Split = SplitStyle.Stacked;
            using var bmp = Render(s, st, out var r);
            int bx = r.LastBorderX;
            for (int i = 0; i < s.RowCount; i++)
            {
                if (s.Vol[i] <= 0) continue;
                int y = 20 + (1100 - (int)s.RowFirstTick(i)) * 4 + 1;
                bool sawSell = false;
                for (int x = bx - 1; x > bx - 125; x--)
                {
                    var c = Px(bmp, x, y);
                    if (Near(c, st.SellColor)) sawSell = true;
                    else if (Near(c, st.BuyColor)) Assert.False(sawSell, "buy after sell at row " + i + " x=" + x);
                }
            }
        }

        [Fact]
        public void Hidden_elements_leave_background()
        {
            var s = BuildSnap();
            var st = Base(); st.ShowBox = false; st.ShowBorder = false; st.ShowPoc = st.ShowVah = st.ShowVal = st.ShowVwap = false;
            st.RightData = SideData.None; st.LeftData = SideData.None; st.ShowHandles = true;
            using var bmp = Render(s, st, out _);
            for (int y = 0; y < H; y += 7) for (int x = 0; x < W; x += 7) Assert.True(Near(Px(bmp, x, y), Color.FromArgb(20, 20, 20), 1));
        }

        [Fact]
        public void Render_is_allocation_free_after_warmup()
        {
            var s = BuildSnap();
            var st = Base(); st.Labels = LabelSides.None;
            using var surf = new StubSurface(W, H);
            var ctx = new SkiaRenderContext(surf.SkiaSurface.Canvas, surf, new Size(W, H));
            var r = new ProfileRenderer(); var lay = new LinearLayout { Clip = new Rectangle(0, 0, W, H) };
            for (int i = 0; i < 3; i++) r.Draw(ctx, s, st, lay);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) r.Draw(ctx, s, st, lay);
            long per = (GC.GetAllocatedBytesForCurrentThread() - before) / 20;
            Assert.True(per < 20000, "bytes per render: " + per);
        }
    }
}
