using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace VolumeProfileX
{
    public interface IProfileLayout
    {
        Rectangle Clip { get; }
        int BarX(int bar);
        int BarWidth(int bar);
        int TickTopY(long tick);
        int TickBottomY(long tick);
    }

    public sealed class ProfileRenderer
    {
        private readonly Dictionary<(int, int, LineDash), RenderPen> _pens = new Dictionary<(int, int, LineDash), RenderPen>();
        private readonly Dictionary<(long, long, int), string> _text = new Dictionary<(long, long, int), string>();
        private RenderFont _font; private int _fontSize = -1;
        private readonly RenderStringFormat _fmtNear = new RenderStringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        private readonly RenderStringFormat _fmtFar = new RenderStringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        private int[] _edge = new int[0];
        private int _edgeBuild = -1, _edgeY0, _edgeY1, _edgeRows;

        public Rectangle LastBox;
        public int LastBorderX;

        private RenderPen Pen(Color c, int width, LineDash dash)
        {
            if (width < 1) width = 1;
            var key = (c.ToArgb(), width, dash);
            if (_pens.TryGetValue(key, out var p)) return p;
            if (_pens.Count > 96) _pens.Clear();
            p = new RenderPen(c, width, ToDash(dash));
            _pens[key] = p;
            return p;
        }

        public static DashStyle ToDash(LineDash d)
        {
            switch (d)
            {
                case LineDash.Dash: return DashStyle.Dash;
                case LineDash.Dot: return DashStyle.Dot;
                case LineDash.DashDot: return DashStyle.DashDot;
                default: return DashStyle.Solid;
            }
        }

        public static Color WithOpacity(Color c, int opacityPct)
        {
            int a = c.A * Math.Max(0, Math.Min(100, opacityPct)) / 100;
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        private static double MaxOf(ProfileSnapshot s, SideData d)
        {
            switch (d)
            {
                case SideData.TotalVolume: return s.MaxVol;
                case SideData.BuyVolume: return s.MaxAsk;
                case SideData.SellVolume: return s.MaxBid;
                case SideData.Delta: return s.MaxAbsDelta;
                case SideData.BuySellSplit: return s.MaxVol;
                case SideData.TradeCount: return s.MaxTrades;
                default: return 0;
            }
        }

        private Color StateColor(ProfileSettings st, RowFlags f)
        {
            if (st.ColorPocRow && (f & RowFlags.Poc) != 0) return st.PocRowColor;
            if (st.ShowHvn && (f & RowFlags.Hvn) != 0) return st.HvnColor;
            if (st.ShowLvn && (f & RowFlags.Lvn) != 0) return st.LvnColor;
            return (f & RowFlags.InValueArea) != 0 ? st.InsideVaColor : st.OutsideVaColor;
        }

        private Color FillColor(ProfileSettings st, RowFlags f)
            => st.Shape == ProfileShape.Continuous && st.ShapeFillSingle ? st.ShapeFillColor : StateColor(st, f);

        private static int RowLen(ProfileSnapshot s, ProfileSettings st, int row, SideData d, int widthPx, double denom)
        {
            if (s.Vol[row] <= 0 && s.Trades[row] <= 0) return 0;
            switch (d)
            {
                case SideData.TotalVolume: return Len(s.Vol[row] / denom, widthPx);
                case SideData.TradeCount: return Len(s.Trades[row] / denom, widthPx);
                case SideData.BuyVolume: return Len(s.Ask[row] / denom, widthPx);
                case SideData.SellVolume: return Len(s.Bid[row] / denom, widthPx);
                case SideData.Delta: return Len(Math.Abs(s.Ask[row] - s.Bid[row]) / denom, widthPx);
                case SideData.BuySellSplit:
                    {
                        int la = Len(s.Ask[row] / denom, widthPx), lb = Len(s.Bid[row] / denom, widthPx);
                        return st.Split == SplitStyle.Stacked ? Math.Min(widthPx, la + lb) : Math.Max(la, lb);
                    }
                default: return 0;
            }
        }

        private int[] _len = new int[0], _len2 = new int[0], _key = new int[0];

        private static Point[] Pts(int k) => new Point[k];

        private int YMid(int row) => (_edge[row] + _edge[row + 1]) / 2;

        private Point[] RunPoints(int i, int j, int n, int[] len, int dir, int bx, bool closeToBorder, bool smooth)
        {
            if (!smooth)
            {
                int m = 2 * (j - i + 1) + (closeToBorder ? 2 : 0);
                var b = Pts(m);
                int q = 0;
                if (closeToBorder) b[q++] = new Point(bx, Math.Max(_edge[i], _edge[i + 1]));
                for (int r = i; r <= j; r++)
                {
                    int x = bx + dir * len[r];
                    int yb = Math.Max(_edge[r], _edge[r + 1]), yt = Math.Min(_edge[r], _edge[r + 1]);
                    b[q++] = new Point(x, yb);
                    b[q++] = new Point(x, yt);
                }
                if (closeToBorder) b[q++] = new Point(bx, Math.Min(_edge[j], _edge[j + 1]));
                return b;
            }
            bool prev = i > 0, next = j < n - 1;
            int k = (j - i + 1) + (prev ? 1 : 0) + (next ? 1 : 0) + (closeToBorder ? 2 : 0);
            var a = Pts(k);
            int c = 0;
            if (closeToBorder) a[c++] = new Point(bx, prev ? (YMid(i - 1) + YMid(i)) / 2 : YMid(i));
            if (prev) a[c++] = new Point(bx + dir * (len[i - 1] + len[i]) / 2, (YMid(i - 1) + YMid(i)) / 2);
            for (int r = i; r <= j; r++) a[c++] = new Point(bx + dir * len[r], YMid(r));
            if (next) a[c++] = new Point(bx + dir * (len[j] + len[j + 1]) / 2, (YMid(j) + YMid(j + 1)) / 2);
            if (closeToBorder) a[c++] = new Point(bx, next ? (YMid(j) + YMid(j + 1)) / 2 : YMid(j));
            return a;
        }

        private void FillRuns(RenderContext g, int n, int[] len, int[] key, int dir, int bx, bool smooth)
        {
            int i = 0;
            while (i < n)
            {
                int j = i; int col = key[i];
                while (j + 1 < n && key[j + 1] == col) j++;
                if (col != 0 || true) g.FillPolygon(Color.FromArgb(col), RunPoints(i, j, n, len, dir, bx, true, smooth));
                i = j + 1;
            }
        }

        private void DrawSmoothSide(RenderContext g, ProfileSnapshot s, ProfileSettings st, SideData d, int dir, int bx, int widthPx, double denom, bool outline)
        {
            if (d == SideData.None || denom <= 0 || widthPx <= 0) return;
            int n = s.RowCount;
            if (_len.Length < n) { _len = new int[n * 2]; _len2 = new int[n * 2]; _key = new int[n * 2]; }
            for (int i = 0; i < n; i++)
            {
                _len[i] = RowLen(s, st, i, d, widthPx, denom);
                var f = s.Flags[i];
                switch (d)
                {
                    case SideData.TotalVolume: case SideData.TradeCount: _key[i] = FillColor(st, f).ToArgb(); break;
                    case SideData.BuyVolume: _key[i] = st.BuyColor.ToArgb(); break;
                    case SideData.SellVolume: _key[i] = st.SellColor.ToArgb(); break;
                    case SideData.Delta: _key[i] = (s.Ask[i] - s.Bid[i] >= 0 ? st.BuyColor : st.SellColor).ToArgb(); break;
                    default: _key[i] = st.SellColor.ToArgb(); break;
                }
            }
            FillRuns(g, n, _len, _key, dir, bx, st.ContinuousSmooth);
            if (d == SideData.BuySellSplit)
            {
                for (int i = 0; i < n; i++)
                {
                    _len2[i] = (s.Vol[i] <= 0 && s.Trades[i] <= 0) ? 0 : Len(s.Ask[i] / denom, widthPx);
                    _key[i] = st.BuyColor.ToArgb();
                }
                FillRuns(g, n, _len2, _key, dir, bx, st.ContinuousSmooth);
            }
            if (!outline) return;
            int a0 = 0;
            while (a0 < n)
            {
                int b0 = a0; int vaKey = st.OutlineByValueArea ? ((s.Flags[a0] & RowFlags.InValueArea) != 0 ? 1 : 2) : 0;
                while (b0 + 1 < n && (st.OutlineByValueArea ? ((s.Flags[b0 + 1] & RowFlags.InValueArea) != 0 ? 1 : 2) : 0) == vaKey) b0++;
                var pen = OutlinePen(st, s.Flags[a0]);
                var pts = RunPoints(a0, b0, n, _len, dir, bx, false, st.ContinuousSmooth);
                if (pts.Length >= 2) g.DrawLines(pen, pts);
                a0 = b0 + 1;
            }
        }

        private RenderPen OutlinePen(ProfileSettings st, RowFlags f)
        {
            Color c = !st.OutlineByValueArea ? st.OutlineColor
                : (f & RowFlags.InValueArea) != 0 ? st.OutlineInsideColor : st.OutlineOutsideColor;
            return Pen(c, st.OutlineWidth, st.OutlineDash);
        }

        private void DrawOutline(RenderContext g, ProfileSnapshot s, ProfileSettings st, SideData d, int dir, int bx, int widthPx, double denom)
        {
            if (widthPx <= 0) return;
            int n = s.RowCount;
            int px = bx, py = int.MinValue;
            bool any = false;
            RowFlags lastF = 0;
            for (int i = 0; i < n; i++)
            {
                int yTop = _edge[i + 1], yBot = _edge[i];
                if (yBot < yTop) { int t = yTop; yTop = yBot; yBot = t; }
                int x = bx + dir * RowLen(s, st, i, d, widthPx, denom);
                var f = s.Flags[i];
                var pen = OutlinePen(st, f);
                lastF = f;
                if (st.OutlineSmooth)
                {
                    int ym = (yTop + yBot) / 2;
                    if (!any) { g.DrawLine(pen, bx, ym, x, ym); any = true; }
                    else g.DrawLine(pen, px, py, x, ym);
                    px = x; py = ym;
                }
                else
                {
                    if (!any) { px = bx; py = yBot; any = true; }
                    g.DrawLine(pen, px, yBot, x, yBot);
                    g.DrawLine(pen, x, yBot, x, yTop);
                    px = x; py = yTop;
                }
            }
            if (any) g.DrawLine(OutlinePen(st, lastF), px, py, bx, py);
        }

        private void EnsureEdges(ProfileSnapshot s, IProfileLayout lay)
        {
            int n = s.RowCount;
            int y0 = lay.TickBottomY(s.RowFirstTick(0));
            int y1 = lay.TickTopY(s.RowLastTick(n - 1));
            if (_edgeBuild == s.BuildId && _edgeY0 == y0 && _edgeY1 == y1 && _edgeRows == n && _edge.Length > n) return;
            if (_edge.Length < n + 1) _edge = new int[Math.Max(n + 1, _edge.Length * 2)];
            for (int i = 0; i < n; i++) _edge[i] = lay.TickBottomY(s.RowFirstTick(i));
            _edge[n] = y1;
            _edgeBuild = s.BuildId; _edgeY0 = y0; _edgeY1 = y1; _edgeRows = n;
        }

        public void Draw(RenderContext g, ProfileSnapshot s, ProfileSettings st, IProfileLayout lay)
        {
            if (s.IsEmpty || s.FirstBar < 0) return;
            var clip = lay.Clip;

            int xL = lay.BarX(s.FirstBar);
            int xR = lay.BarX(s.LastBar) + Math.Max(1, lay.BarWidth(s.LastBar));
            if (xR <= xL) xR = xL + 1;
            int topY = lay.TickTopY(s.MaxTick);
            int botY = lay.TickBottomY(s.MinTick);
            if (botY < topY) { int t = topY; topY = botY; botY = t; }
            var box = new Rectangle(xL, topY, xR - xL, Math.Max(1, botY - topY));
            LastBox = box;

            EnsureEdges(s, lay);

            int winW = xR - xL;
            int bx = (st.Edge == BorderEdge.Left ? xL : xR) + st.OffsetPx;
            LastBorderX = bx;
            int wRight = SideWidth(st, st.RightWidth, winW);
            int wLeft = SideWidth(st, st.LeftWidth, winW);
            int dirR = st.Direction == GrowDirection.Right ? 1 : -1;
            var dataR = st.Mirror ? st.LeftData : st.RightData;
            var dataL = st.Mirror ? st.RightData : st.LeftData;
            double maxR = MaxOf(s, dataR), maxL = MaxOf(s, dataL);
            if (st.Norm == Normalisation.Shared) { double m = Math.Max(maxR, maxL); maxR = m; maxL = m; }

            g.SetClip(clip);
            try
            {
                if (st.ShowBox)
                {
                    if (st.BoxFillOpacity > 0) g.FillRectangle(WithOpacity(st.BoxColor, st.BoxFillOpacity), box);
                    g.DrawRectangle(Pen(st.BoxColor, st.BoxLineWidth, st.BoxDash), box);
                }

                int n = s.RowCount;
                int minH = Math.Max(1, st.LabelMinRowHeight);
                bool smooth = st.Shape == ProfileShape.Continuous;
                bool oR = st.ShowOutline && st.Shape == ProfileShape.Continuous && (st.OutlineSide == OutlineSides.Right || st.OutlineSide == OutlineSides.Both);
                bool oL = st.ShowOutline && st.Shape == ProfileShape.Continuous && (st.OutlineSide == OutlineSides.Left || st.OutlineSide == OutlineSides.Both);
                if (smooth)
                {
                    DrawSmoothSide(g, s, st, dataR, dirR, bx, wRight, maxR, oR);
                    DrawSmoothSide(g, s, st, dataL, -dirR, bx, wLeft, maxL, oL);
                }
                for (int i = 0; !smooth && i < n; i++)
                {
                    int yTop = _edge[i + 1], yBot = _edge[i];
                    if (yBot < yTop) { int t = yTop; yTop = yBot; yBot = t; }
                    if (yBot < clip.Top || yTop > clip.Bottom) continue;
                    int h = Math.Max(1, yBot - yTop);
                    int ry, rh;
                    if (st.Rows == RowMode.FullFill || st.Shape == ProfileShape.Continuous) { ry = yTop; rh = h; }
                    else
                    {
                        rh = st.RowHeightPx > 0 ? Math.Min(st.RowHeightPx, h) : Math.Max(1, h - st.RowGapPx);
                        ry = yTop + (h - rh) / 2;
                    }
                    if (s.Vol[i] <= 0 && s.Trades[i] <= 0) continue;
                    var f = s.Flags[i];
                    if (dataR != SideData.None && maxR > 0) DrawSide(g, s, st, i, dataR, dirR, bx, wRight, maxR, ry, rh, f);
                    if (dataL != SideData.None && maxL > 0) DrawSide(g, s, st, i, dataL, -dirR, bx, wLeft, maxL, ry, rh, f);
                }

                if (!smooth && st.Shape == ProfileShape.Continuous && st.ShowOutline)
                {
                    if (oR && dataR != SideData.None && maxR > 0) DrawOutline(g, s, st, dataR, dirR, bx, wRight, maxR);
                    if (oL && dataL != SideData.None && maxL > 0) DrawOutline(g, s, st, dataL, -dirR, bx, wLeft, maxL);
                }

                if (st.ShowBorder)
                    g.DrawLine(Pen(st.BorderColor, st.BorderWidth, st.BorderDash), bx, topY, bx, botY);

                int farX = dirR > 0 ? xR : xL;
                if (st.ShowPoc && s.PocRow >= 0) HLine(g, st.PocLine, RowCenterY(s.PocRow), bx, dirR, farX, clip);
                if (st.ShowVah && s.VahRow >= 0) HLine(g, st.VahLine, _edge[s.VahRow + 1], bx, dirR, farX, clip);
                if (st.ShowVal && s.ValRow >= 0) HLine(g, st.ValLine, _edge[s.ValRow], bx, dirR, farX, clip);
                if (st.ShowMedian && s.MedianRow >= 0) HLine(g, st.MedianLine, RowCenterY(s.MedianRow), bx, dirR, farX, clip);
                if (st.ShowVwap && s.Vwap > 0)
                {
                    long tk = (long)Math.Round(s.Vwap / s.TickSize);
                    HLine(g, st.VwapLine, (lay.TickTopY(tk) + lay.TickBottomY(tk)) / 2, bx, dirR, farX, clip);
                }

                if (st.Labels != LabelSides.None) DrawLabels(g, s, st, dataR, dataL, dirR, bx, wRight, wLeft, maxR, maxL, minH, clip);

                if (st.ShowBox && st.ShowHandles) DrawHandles(g, st, box);
            }
            finally { g.ResetClip(); }
        }

        private int RowCenterY(int row) => (_edge[row] + _edge[row + 1]) / 2;

        private static int SideWidth(ProfileSettings st, double v, int winW)
        {
            double px = st.WidthUnit == WidthUnit.Pixels ? v : winW * v / 100.0;
            if (st.MaxWidthPx > 0 && px > st.MaxWidthPx) px = st.MaxWidthPx;
            return Math.Max(0, (int)Math.Round(px));
        }

        private void HLine(RenderContext g, LineStyle ls, int y, int bx, int dir, int farX, Rectangle clip)
        {
            int x2;
            switch (ls.Extend)
            {
                case LineExtend.FixedPx: x2 = bx + dir * Math.Max(0, ls.ExtendPx); break;
                case LineExtend.ToChartEdge: x2 = dir > 0 ? clip.Right : clip.Left; break;
                default: x2 = farX + dir * Math.Max(0, ls.ExtendPx); break;
            }
            g.DrawLine(Pen(ls.Color, ls.Width, ls.Dash), bx, y, x2, y);
        }

        private void DrawSide(RenderContext g, ProfileSnapshot s, ProfileSettings st, int row, SideData d, int dir, int bx, int widthPx, double denom, int y, int h, RowFlags f)
        {
            if (widthPx <= 0) return;
            switch (d)
            {
                case SideData.TotalVolume: Bar(g, st, dir, bx, widthPx, s.Vol[row] / denom, FillColor(st, f), y, h); break;
                case SideData.TradeCount: Bar(g, st, dir, bx, widthPx, s.Trades[row] / denom, FillColor(st, f), y, h); break;
                case SideData.BuyVolume: Bar(g, st, dir, bx, widthPx, s.Ask[row] / denom, st.BuyColor, y, h); break;
                case SideData.SellVolume: Bar(g, st, dir, bx, widthPx, s.Bid[row] / denom, st.SellColor, y, h); break;
                case SideData.Delta:
                    {
                        double dl = s.Ask[row] - s.Bid[row];
                        Bar(g, st, dir, bx, widthPx, Math.Abs(dl) / denom, dl >= 0 ? st.BuyColor : st.SellColor, y, h);
                        break;
                    }
                case SideData.BuySellSplit:
                    {
                        double a = s.Ask[row] / denom, b = s.Bid[row] / denom;
                        if (st.Split == SplitStyle.Stacked)
                        {
                            int la = Len(a, widthPx), lb = Len(b, widthPx);
                            if (la + lb > widthPx) lb = Math.Max(0, widthPx - la);
                            Seg(g, st, dir, bx, 0, la, st.BuyColor, y, h);
                            Seg(g, st, dir, bx, la, lb, st.SellColor, y, h);
                        }
                        else if (a >= b)
                        {
                            Bar(g, st, dir, bx, widthPx, a, st.BuyColor, y, h);
                            Bar(g, st, dir, bx, widthPx, b, st.SellColor, y, h);
                        }
                        else
                        {
                            Bar(g, st, dir, bx, widthPx, b, st.SellColor, y, h);
                            Bar(g, st, dir, bx, widthPx, a, st.BuyColor, y, h);
                        }
                        break;
                    }
            }
        }

        private static int Len(double frac, int widthPx)
        {
            if (!(frac > 0)) return 0;
            int l = (int)Math.Round(Math.Min(1.0, frac) * widthPx);
            return l < 1 ? 1 : l;
        }

        private void Bar(RenderContext g, ProfileSettings st, int dir, int bx, int widthPx, double frac, Color c, int y, int h)
            => Seg(g, st, dir, bx, 0, Len(frac, widthPx), c, y, h);

        private void Seg(RenderContext g, ProfileSettings st, int dir, int bx, int from, int len, Color c, int y, int h)
        {
            if (len <= 0) return;
            int x = dir > 0 ? bx + from : bx - from - len;
            var r = new Rectangle(x, y, len, h);
            g.FillRectangle(c, r);
            if (st.RowBorder && st.Rows == RowMode.VisibleRows && st.Shape == ProfileShape.SeparateRows) g.DrawRectangle(Pen(st.RowBorderColor, 1, LineDash.Solid), r);
        }

        private void DrawHandles(RenderContext g, ProfileSettings st, Rectangle b)
        {
            int big = Math.Max(3, st.HandleSize), small = Math.Max(2, big / 2);
            int x1 = b.Left, x2 = b.Right, y1 = b.Top, y2 = b.Bottom;
            int qx1 = x1 + (x2 - x1) / 4, qx3 = x1 + 3 * (x2 - x1) / 4, qy1 = y1 + (y2 - y1) / 4, qy3 = y1 + 3 * (y2 - y1) / 4;
            Dot(g, st.HandleColor, x1, y1, big); Dot(g, st.HandleColor, x2, y1, big);
            Dot(g, st.HandleColor, x1, y2, big); Dot(g, st.HandleColor, x2, y2, big);
            Dot(g, st.HandleColor, qx1, y1, small); Dot(g, st.HandleColor, qx3, y1, small);
            Dot(g, st.HandleColor, qx1, y2, small); Dot(g, st.HandleColor, qx3, y2, small);
            Dot(g, st.HandleColor, x1, qy1, small); Dot(g, st.HandleColor, x1, qy3, small);
            Dot(g, st.HandleColor, x2, qy1, small); Dot(g, st.HandleColor, x2, qy3, small);
        }

        private static void Dot(RenderContext g, Color c, int cx, int cy, int size)
            => g.FillEllipse(c, new Rectangle(cx - size / 2, cy - size / 2, size, size));

        private void DrawLabels(RenderContext g, ProfileSnapshot s, ProfileSettings st, SideData dataR, SideData dataL, int dirR, int bx, int wR, int wL, double maxR, double maxL, int minH, Rectangle clip)
        {
            if (_font == null || _fontSize != st.LabelFontSize)
            {
                _font = new RenderFont("Arial", Math.Max(5, st.LabelFontSize)); _fontSize = st.LabelFontSize;
            }
            bool doR = st.Labels == LabelSides.Right || st.Labels == LabelSides.Both;
            bool doL = st.Labels == LabelSides.Left || st.Labels == LabelSides.Both;
            int n = s.RowCount;
            for (int i = 0; i < n; i++)
            {
                int yTop = _edge[i + 1], yBot = _edge[i];
                if (yBot < yTop) { int t = yTop; yTop = yBot; yBot = t; }
                int h = yBot - yTop;
                if (h < minH || yBot < clip.Top || yTop > clip.Bottom) continue;
                if (s.Vol[i] <= 0 && s.Trades[i] <= 0) continue;
                if (doR) Label(g, s, st, i, dataR, dirR, bx, wR, maxR, yTop, h);
                if (doL) Label(g, s, st, i, dataL, -dirR, bx, wL, maxL, yTop, h);
            }
        }

        private void Label(RenderContext g, ProfileSnapshot s, ProfileSettings st, int row, SideData d, int dir, int bx, int widthPx, double denom, int yTop, int h)
        {
            if (d == SideData.None || denom <= 0 || widthPx <= 0) return;
            string text; double frac;
            switch (d)
            {
                case SideData.TotalVolume: text = Num(s.Vol[row], st.LabelAbbreviate); frac = s.Vol[row] / denom; break;
                case SideData.BuyVolume: text = Num(s.Ask[row], st.LabelAbbreviate); frac = s.Ask[row] / denom; break;
                case SideData.SellVolume: text = Num(s.Bid[row], st.LabelAbbreviate); frac = s.Bid[row] / denom; break;
                case SideData.Delta: text = Num(s.Ask[row] - s.Bid[row], st.LabelAbbreviate); frac = Math.Abs(s.Ask[row] - s.Bid[row]) / denom; break;
                case SideData.TradeCount: text = Num(s.Trades[row], st.LabelAbbreviate); frac = s.Trades[row] / denom; break;
                default:
                    text = Num2(s.Ask[row], s.Bid[row], st.LabelAbbreviate); frac = Math.Max(s.Ask[row], s.Bid[row]) / denom; break;
            }
            int tip = Len(frac, widthPx);
            const int boxW = 90;
            Rectangle r = dir > 0 ? new Rectangle(bx + tip + 2, yTop, boxW, h) : new Rectangle(bx - tip - 2 - boxW, yTop, boxW, h);
            g.DrawString(text, _font, st.LabelColor, r, dir > 0 ? _fmtNear : _fmtFar);
        }

        private string Num(double v, bool abbr)
        {
            long k = (long)Math.Round(v);
            var key = (k, 0L, abbr ? 1 : 0);
            if (_text.TryGetValue(key, out var s)) return s;
            if (_text.Count > 8192) _text.Clear();
            s = Fmt(k, abbr); _text[key] = s; return s;
        }

        private string Num2(double a, double b, bool abbr)
        {
            long ka = (long)Math.Round(a), kb = (long)Math.Round(b);
            var key = (ka, kb, abbr ? 3 : 2);
            if (_text.TryGetValue(key, out var s)) return s;
            if (_text.Count > 8192) _text.Clear();
            s = Fmt(ka, abbr) + "x" + Fmt(kb, abbr); _text[key] = s; return s;
        }

        public static string Fmt(long v, bool abbr)
        {
            if (!abbr) return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            long a = Math.Abs(v);
            if (a >= 1000000) return (v / 1000000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
            if (a >= 1000) return (v / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K";
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
