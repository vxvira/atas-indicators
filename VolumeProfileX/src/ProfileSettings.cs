using System.Drawing;

namespace VolumeProfileX
{
    public enum AnchorTimeBase { PcLocal, Utc, Exchange }
    public enum BorderEdge { Left, Right }
    public enum GrowDirection { Right, Left }
    public enum WidthUnit { Pixels, PercentOfWindow }
    public enum SideData { None, TotalVolume, BuyVolume, SellVolume, Delta, BuySellSplit, TradeCount }
    public enum Normalisation { PerSide, Shared }
    public enum RowMode { FullFill, VisibleRows }
    public enum SplitStyle { Overlay, Stacked }
    public enum ProfileShape { SeparateRows, Continuous }
    public enum OutlineSides { Right, Left, Both }
    public enum LineExtend { ToBoxEnd, FixedPx, ToChartEdge }
    public enum LineDash { Solid, Dash, Dot, DashDot }
    public enum LabelSides { None, Right, Left, Both }
    public enum RenderLayer { Final, LatestBar, Historical, All }

    public struct LineStyle
    {
        public bool Show;
        public Color Color;
        public int Width;
        public LineDash Dash;
        public LineExtend Extend;
        public int ExtendPx;
    }

    public sealed class ProfileSettings
    {
        public int Version;

        public bool UseFixedAnchor = false;
        public double Hours = 6;
        public bool Multi = false;
        public double Days = 0;
        public double EffectiveHours => Days > 0 ? Days * 24.0 : Hours;
        public string AnchorStart = "";
        public AnchorTimeBase AnchorBase = AnchorTimeBase.PcLocal;
        public RenderLayer Layer = RenderLayer.Final;

        public BorderEdge Edge = BorderEdge.Left;
        public int OffsetPx = 0;
        public GrowDirection Direction = GrowDirection.Right;
        public WidthUnit WidthUnit = WidthUnit.PercentOfWindow;
        public double RightWidth = 25;
        public double LeftWidth = 18;
        public int MaxWidthPx = 600;

        public SideData RightData = SideData.TotalVolume;
        public SideData LeftData = SideData.Delta;
        public bool Mirror = false;
        public Normalisation Norm = Normalisation.PerSide;
        public SplitStyle Split = SplitStyle.Stacked;

        public ProfileShape Shape = ProfileShape.Continuous;
        public bool ContinuousSmooth = false;
        public bool ShapeFillSingle = false;
        public Color ShapeFillColor = Color.FromArgb(200, 214, 150, 60);
        public bool ShowOutline = true;
        public Color OutlineColor = Color.FromArgb(255, 235, 210, 120);
        public bool OutlineByValueArea = true;
        public Color OutlineInsideColor = Color.FromArgb(255, 250, 160, 118);
        public Color OutlineOutsideColor = Color.FromArgb(255, 252, 240, 165);
        public OutlineSides OutlineSide = OutlineSides.Right;
        public int OutlineWidth = 1;
        public LineDash OutlineDash = LineDash.Solid;
        public bool OutlineSmooth = false;
        public double RowSizePrice = 0;
        public int RowsPerBucket = 4;
        public RowMode Rows = RowMode.FullFill;
        public int RowHeightPx = 0;
        public int RowGapPx = 1;
        public bool RowBorder = false;
        public Color RowBorderColor = Color.FromArgb(120, 0, 0, 0);

        public double ValueAreaPct = 70;
        public bool ShowHvn = true;
        public double HvnPct = 85;
        public bool ShowLvn = false;
        public double LvnPct = 15;

        public Color InsideVaColor = Color.FromArgb(255, 183, 81, 33);
        public Color OutsideVaColor = Color.FromArgb(255, 192, 176, 78);
        public Color PocRowColor = Color.FromArgb(255, 225, 45, 165);
        public Color HvnColor = Color.FromArgb(255, 222, 12, 18);
        public Color LvnColor = Color.FromArgb(255, 80, 130, 210);
        public Color BuyColor = Color.FromArgb(255, 48, 192, 62);
        public Color SellColor = Color.FromArgb(255, 122, 56, 220);
        public bool ColorPocRow = true;

        public bool ShowPoc = true, ShowVah = true, ShowVal = true, ShowVwap = false, ShowMedian = false;
        public LineStyle PocLine = new LineStyle { Color = Color.FromArgb(255, 225, 45, 165), Width = 1, Dash = LineDash.Solid, Extend = LineExtend.ToBoxEnd, ExtendPx = 0 };
        public LineStyle VahLine = new LineStyle { Color = Color.FromArgb(255, 30, 144, 255), Width = 1, Dash = LineDash.Solid, Extend = LineExtend.ToBoxEnd, ExtendPx = 0 };
        public LineStyle ValLine = new LineStyle { Color = Color.FromArgb(255, 30, 144, 255), Width = 1, Dash = LineDash.Solid, Extend = LineExtend.ToBoxEnd, ExtendPx = 0 };
        public LineStyle VwapLine = new LineStyle { Color = Color.FromArgb(255, 255, 225, 66), Width = 1, Dash = LineDash.Dash, Extend = LineExtend.ToBoxEnd, ExtendPx = 0 };
        public LineStyle MedianLine = new LineStyle { Color = Color.FromArgb(255, 0, 200, 255), Width = 1, Dash = LineDash.Dash, Extend = LineExtend.ToBoxEnd, ExtendPx = 0 };

        public bool ShowBorder = true;
        public Color BorderColor = Color.FromArgb(255, 190, 180, 70);
        public int BorderWidth = 2;
        public LineDash BorderDash = LineDash.Solid;
        public bool ShowBox = false;
        public Color BoxColor = Color.FromArgb(255, 120, 117, 51);
        public int BoxLineWidth = 2;
        public int BoxFillOpacity = 0;
        public LineDash BoxDash = LineDash.Solid;
        public bool ShowHandles = false;
        public Color HandleColor = Color.FromArgb(255, 120, 117, 51);
        public int HandleSize = 12;

        public LabelSides Labels = LabelSides.None;
        public int LabelFontSize = 9;
        public Color LabelColor = Color.FromArgb(255, 235, 235, 235);
        public int LabelMinRowHeight = 11;
        public bool LabelAbbreviate = true;
    }
}
