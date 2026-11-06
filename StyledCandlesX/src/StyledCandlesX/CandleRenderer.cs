using System;
using System.Drawing;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace StyledCandlesX
{
    public interface IChartMap
    {
        int FirstBar { get; }
        int LastBar { get; }
        int XByBar(int bar);
        int YByPrice(decimal price);
        bool TryGetCandle(int bar, out decimal open, out decimal high, out decimal low, out decimal close);
    }

    public sealed class CandleStyle
    {
        public bool BullHollow, BearHollow;
        public Color BullFill, BullBorder, BullWick, BearFill, BearBorder, BearWick;
        public decimal BodyWidthPercent = 80m, HorizontalOffsetPercent;
        public int WickWidth = 1, BorderWidth = 1;
        public bool GlowEnabled = true;
        public Color BullGlow, BearGlow;
        public int GlowOpacity = 35, GlowThickness = 3, GlowLayers = 3;

        public int Signature()
        {
            var h = new HashCode();
            h.Add(BullFill); h.Add(BullBorder); h.Add(BullWick); h.Add(BearFill); h.Add(BearBorder); h.Add(BearWick);
            h.Add(BullGlow); h.Add(BearGlow);
            h.Add(WickWidth); h.Add(BorderWidth); h.Add(GlowEnabled); h.Add(GlowOpacity); h.Add(GlowThickness); h.Add(GlowLayers);
            return h.ToHashCode();
        }
    }

    public sealed class CandleRenderer
    {
        public const int MaxLayers = 4;

        private sealed class SidePens
        {
            public RenderPen Wick, Border, Thin2, ThinGlow;
            public readonly RenderPen[] GlowRing = new RenderPen[MaxLayers + 1];
        }

        private readonly SidePens _bull = new SidePens();
        private readonly SidePens _bear = new SidePens();
        private int _sig;
        private bool _built;
        private int _layers, _step;
        private int _lastPitch = 6;
        private CandleShape _shape;

        public int LastDrawn { get; private set; }

        private void Build(CandleStyle s)
        {
            _layers = Math.Clamp(s.GlowLayers, 1, MaxLayers);
            _step = Glow.Step(s.GlowThickness, _layers);
            BuildSide(_bull, s, s.BullWick, s.BullBorder, s.BullFill, s.BullGlow);
            BuildSide(_bear, s, s.BearWick, s.BearBorder, s.BearFill, s.BearGlow);
            _sig = s.Signature();
            _built = true;
        }

        private void BuildSide(SidePens p, CandleStyle s, Color wick, Color border, Color fill, Color glow)
        {
            p.Wick = new RenderPen(wick, Math.Max(1, s.WickWidth));
            p.Border = new RenderPen(border, Math.Max(1, s.BorderWidth));
            p.Thin2 = new RenderPen(fill.A == 0 ? border : fill, 2f);
            for (int k = 1; k <= _layers; k++)
                p.GlowRing[k] = new RenderPen(Color.FromArgb(Glow.LayerAlpha(s.GlowOpacity, k, _layers), glow), _step);
            p.ThinGlow = new RenderPen(ColorMath.WithOpacity(glow, s.GlowOpacity / 2), 1 + 2 * _step);
        }

        public void Render(RenderContext ctx, IChartMap map, CandleStyle style)
        {
            if (!_built || style.Signature() != _sig) Build(style);
            int first = Math.Max(map.FirstBar, 0), last = map.LastBar, n = 0;
            double bodyPct = (double)style.BodyWidthPercent, offPct = (double)style.HorizontalOffsetPercent;
            for (int i = first; i <= last; i++)
            {
                if (!map.TryGetCandle(i, out var o, out var h, out var l, out var c)) continue;
                bool bull = c >= o;
                var pens = bull ? _bull : _bear;
                bool hollow = bull ? style.BullHollow : style.BearHollow;
                int x = map.XByBar(i);
                int pitch = CandleGeometry.Pitch(x, map.XByBar(i + 1), _lastPitch);
                _lastPitch = pitch;
                CandleGeometry.Compute(ref _shape, x, pitch, bodyPct, offPct,
                    map.YByPrice(o), map.YByPrice(c), map.YByPrice(h), map.YByPrice(l));
                if (pitch <= CandleGeometry.ThinPitch) DrawThin(ctx, pens, style);
                else DrawFull(ctx, pens, style, bull, hollow);
                n++;
            }
            LastDrawn = n;
        }

        private void DrawThin(RenderContext ctx, SidePens p, CandleStyle st)
        {
            ref var s = ref _shape;
            if (st.GlowEnabled && st.GlowOpacity > 0)
                ctx.DrawLine(p.ThinGlow, s.CenterX, s.WickTop, s.CenterX, s.WickBottom);
            ctx.DrawLine(p.Wick, s.CenterX, s.WickTop, s.CenterX, s.WickBottom);
            if (s.Pitch >= 2) ctx.DrawLine(p.Thin2, s.CenterX, s.BodyTop, s.CenterX, s.BodyBottom);
        }

        private void DrawFull(RenderContext ctx, SidePens p, CandleStyle st, bool bull, bool hollow)
        {
            ref var s = ref _shape;
            int cx = s.CenterX;
            bool upper = s.WickTop < s.BodyTop, lower = s.WickBottom > s.BodyBottom;

            if (st.GlowEnabled && st.GlowOpacity > 0)
            {
                int ww = Math.Max(1, st.WickWidth), bw = Math.Max(1, st.BorderWidth);
                for (int k = _layers; k >= 1; k--)
                {
                    var gp = p.GlowRing[k];
                    int ring = (bw + _step) / 2 + (k - 1) * _step;
                    ctx.DrawRectangle(gp, new Rectangle(s.Left - ring, s.BodyTop - ring, s.BodyWidth + 2 * ring, s.BodyHeight + 2 * ring));
                    int wr = (ww + _step) / 2 + (k - 1) * _step;
                    if (upper)
                    {
                        ctx.DrawLine(gp, cx - wr, s.WickTop, cx - wr, s.BodyTop);
                        ctx.DrawLine(gp, cx + wr, s.WickTop, cx + wr, s.BodyTop);
                    }
                    if (lower)
                    {
                        ctx.DrawLine(gp, cx - wr, s.BodyBottom, cx - wr, s.WickBottom);
                        ctx.DrawLine(gp, cx + wr, s.BodyBottom, cx + wr, s.WickBottom);
                    }
                }
            }

            if (upper) ctx.DrawLine(p.Wick, cx, s.WickTop, cx, s.BodyTop);
            if (lower) ctx.DrawLine(p.Wick, cx, s.BodyBottom, cx, s.WickBottom);

            var body = new Rectangle(s.Left, s.BodyTop, s.BodyWidth, s.BodyHeight);
            if (!hollow) ctx.FillRectangle(bull ? st.BullFill : st.BearFill, body);
            ctx.DrawRectangle(p.Border, body);
        }
    }
}
