using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using ATAS.Indicators;
using OFT.Rendering.Context;

namespace VolumeProfileX
{
    [DisplayName("Volume Profile X (Anchored)")]
    [Category("Volume & OrderFlow")]
    [Description("Time-anchored volume profile: last N hours (fractional ok) or a fixed anchor time. Fully configurable sides, rows, colours, lines, box and handles.")]
    public sealed partial class AnchoredVolumeProfile : Indicator
    {
        private readonly ProfileSettings _s = new ProfileSettings();
        private readonly ProfileWindow _win = new ProfileWindow();
        private readonly ProfileSnapshot _snap = new ProfileSnapshot();
        private readonly ProfileRenderer _renderer = new ProfileRenderer();
        private readonly ChartLayout _layout = new ChartLayout();
        private readonly List<LevelIn> _buf = new List<LevelIn>(512);
        private readonly object _sync;
        private int _pendingBar = -1;
        private decimal _tick = 0.25m;

        public AnchoredVolumeProfile() : base(true)
        {
            _sync = _win.SyncRoot;
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Historical | DrawingLayouts.LatestBar | DrawingLayouts.Final);

            if (DataSeries.Count > 0 && DataSeries[0] is ValueDataSeries vds)
            {
                vds.VisualType = VisualMode.Hide;
                vds.IsHidden = true;
            }
        }

        private void Set<T>(ref T field, T value, bool recalc)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            _s.Version++;
            if (recalc) RecalculateValues();
            RedrawChart(new RedrawArg(Rectangle.Empty) { ForceRedraw = true });
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        protected override void OnRecalculate()
        {
            lock (_sync) ResetLocked();
        }

        private void ResetLocked()
        {
            _win.Configure(_s.EffectiveHours, ComputeAnchorUtc());
            _win.Reset();
            _pendingBar = -1;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var c = GetCandle(bar);
            if (c == null) return;
            lock (_sync)
            {
                if (bar == 0) ResetLocked();
                if (_pendingBar >= 0 && _pendingBar != bar) FlushLocked();
                UpdateTickLocked();
                int lastIdx = CurrentBar - 1;
                var last = lastIdx >= 0 ? GetCandle(lastIdx) : null;
                if (last == null) last = c;
                if (!_win.Accepts(c.Time, last.Time)) return;
                if (bar >= lastIdx) { _pendingBar = bar; return; }
                FeedLocked(bar, c);
            }
        }

        private int EffectiveRowsPerBucket()
        {
            if (_s.RowSizePrice > 0 && _tick > 0)
                return Math.Max(1, (int)Math.Round(_s.RowSizePrice / (double)_tick));
            return _s.RowsPerBucket;
        }

        private void UpdateTickLocked()
        {
            var t = InstrumentInfo?.TickSize ?? 0m;
            if (t > 0) _tick = t;
        }

        private void FlushLocked()
        {
            int b = _pendingBar;
            _pendingBar = -1;
            if (b < 0) return;
            var c = GetCandle(b);
            if (c != null) FeedLocked(b, c);
        }

        private void FeedLocked(int bar, IndicatorCandle c)
        {
            _buf.Clear();
            decimal inv = _tick;
            foreach (var p in c.GetAllPriceLevels())
                _buf.Add(new LevelIn((long)Math.Round(p.Price / inv), (double)p.Volume, (double)p.Bid, (double)p.Ask, p.Ticks));
            _win.Feed(bar, c.Time, _buf);
        }

        private DateTime? ComputeAnchorUtc()
        {
            if (!_s.UseFixedAnchor) return null;
            string[] fmts = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "yyyy/MM/dd HH:mm", "dd.MM.yyyy HH:mm" };
            if (!DateTime.TryParseExact((_s.AnchorStart ?? "").Trim(), fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                && !DateTime.TryParse(_s.AnchorStart, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return null;
            return AnchorToUtc(dt, _s.AnchorBase, InstrumentInfo?.TimeZoneOffset ?? TimeSpan.Zero);
        }

        public static DateTime AnchorToUtc(DateTime dt, AnchorTimeBase b, TimeSpan exchangeOffset)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            switch (b)
            {
                case AnchorTimeBase.Utc: return dt;
                case AnchorTimeBase.Exchange: return dt - exchangeOffset;
                default:
                    try { return TimeZoneInfo.ConvertTimeToUtc(dt, TimeZoneInfo.Local); }
                    catch (ArgumentException) { return dt - TimeZoneInfo.Local.GetUtcOffset(dt.AddHours(1)); }
            }
        }

        private bool LayerOk(DrawingLayouts l)
        {
            switch (_s.Layer)
            {
                case RenderLayer.All: return true;
                case RenderLayer.Historical: return l == DrawingLayouts.Historical;
                case RenderLayer.LatestBar: return l == DrawingLayouts.LatestBar;
                default: return l == DrawingLayouts.Final;
            }
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (!LayerOk(layout)) return;
            var pc = ChartInfo?.PriceChartContainer;
            if (pc == null) return;
            lock (_sync)
            {
                FlushLocked();
                UpdateTickLocked();
                _snap.Update(_win, EffectiveRowsPerBucket(), _s.ValueAreaPct, _s.HvnPct, _s.LvnPct, (double)_tick);
            }
            _layout.C = pc; _layout.Tick = _tick; _layout.ClipR = context.ClipBounds;
            _renderer.Draw(context, _snap, _s, _layout);
        }

        private sealed class ChartLayout : IProfileLayout
        {
            public IChartContainer C; public decimal Tick = 0.25m; public Rectangle ClipR;
            public Rectangle Clip => ClipR;
            public int BarX(int bar) => C.GetXByBar(bar, true);
            public int BarWidth(int bar)
            {
                int w = C.GetXByBar(bar + 1, true) - C.GetXByBar(bar, true);
                if (w <= 0 && bar > 0) w = C.GetXByBar(bar, true) - C.GetXByBar(bar - 1, true);
                return Math.Max(1, w);
            }
            public int TickTopY(long tick) => C.GetYByPrice(tick * Tick, false);
            public int TickBottomY(long tick) => C.GetYByPrice(tick * Tick, true);
        }
    }
}
