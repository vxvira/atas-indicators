using System.ComponentModel.DataAnnotations;
using System.Drawing;

namespace VolumeProfileX
{
    public sealed partial class AnchoredVolumeProfile
    {
        [Display(Name = "Use fixed anchor start (else rolling hours)", GroupName = "1. Window / Anchor", Order = 10, Description = "Off: window = last N hours ending at the last bar. On: window = anchor start time .. last bar.")]
        public bool UseFixedAnchor { get => _s.UseFixedAnchor; set => Set(ref _s.UseFixedAnchor, value, true); }

        [Display(Name = "Window hours (rolling)", GroupName = "1. Window / Anchor", Order = 20, Description = "Fractional allowed (0.5, 1.25, 6). Bars whose open time is later than lastBarTime - hours are included.")]
        [Range(0.01, 2000)]
        public double Hours { get => _s.Hours; set => Set(ref _s.Hours, Clamp(value, 0.01, 2000), true); }

        [Display(Name = "Window days (0 = use hours)", GroupName = "1. Window / Anchor", Order = 30, Description = "If above 0, overrides Window hours: window = last N days (fractional ok, no upper limit).")]
        public double Days { get => _s.Days; set => Set(ref _s.Days, value, true); }

        [Display(Name = "Anchor start (yyyy-MM-dd HH:mm)", GroupName = "1. Window / Anchor", Order = 40, Description = "Fixed anchor start, e.g. 2026-11-06 01:20. Used when 'Use fixed anchor' is on. Invalid text -> rolling window.")]
        public string AnchorStart { get => _s.AnchorStart; set => Set(ref _s.AnchorStart, value, true); }

        [Display(Name = "Anchor time zone", GroupName = "1. Window / Anchor", Order = 50, Description = "How the anchor text is interpreted: PC local, UTC or instrument exchange time.")]
        public AnchorTimeBase AnchorBase { get => _s.AnchorBase; set => Set(ref _s.AnchorBase, value, true); }

        [Display(Name = "Render layer", GroupName = "1. Window / Anchor", Order = 60, Description = "Which ATAS drawing layer the profile is painted in. If nothing shows, try LatestBar or All.")]
        public RenderLayer Layer { get => _s.Layer; set => Set(ref _s.Layer, value, false); }

        [Display(Name = "Border on box edge", GroupName = "2. Position & Size", Order = 10, Description = "Which vertical edge of the anchor box the border (profile origin) sits on.")]
        public BorderEdge Edge { get => _s.Edge; set => Set(ref _s.Edge, value, false); }

        [Display(Name = "Border offset, px", GroupName = "2. Position & Size", Order = 20, Description = "Shifts the border horizontally (positive = right).")]
        [Range(-5000, 5000)]
        public int OffsetPx { get => _s.OffsetPx; set => Set(ref _s.OffsetPx, Clamp(value, -5000, 5000), false); }

        [Display(Name = "Right-side data grows", GroupName = "2. Position & Size", Order = 30, Description = "Direction the 'Right side' data grows from the border; the 'Left side' data grows the other way.")]
        public GrowDirection Direction { get => _s.Direction; set => Set(ref _s.Direction, value, false); }

        [Display(Name = "Width unit", GroupName = "2. Position & Size", Order = 40, Description = "Side widths in pixels or as a percent of the window (box) width.")]
        public WidthUnit WidthUnit { get => _s.WidthUnit; set => Set(ref _s.WidthUnit, value, false); }

        [Display(Name = "Right side width", GroupName = "2. Position & Size", Order = 50, Description = "px or % of window depending on Width unit.")]
        [Range(0, 5000)]
        public double RightWidth { get => _s.RightWidth; set => Set(ref _s.RightWidth, Clamp(value, 0, 5000), false); }

        [Display(Name = "Left side width", GroupName = "2. Position & Size", Order = 60, Description = "px or % of window depending on Width unit.")]
        [Range(0, 5000)]
        public double LeftWidth { get => _s.LeftWidth; set => Set(ref _s.LeftWidth, Clamp(value, 0, 5000), false); }

        [Display(Name = "Max width per side, px (0 = no cap)", GroupName = "2. Position & Size", Order = 70)]
        [Range(0, 5000)]
        public int MaxWidthPx { get => _s.MaxWidthPx; set => Set(ref _s.MaxWidthPx, Clamp(value, 0, 5000), false); }

        [Display(Name = "Right side data", GroupName = "3. Data", Order = 10, Description = "None / Total / Buy(ask) / Sell(bid) / Delta / Buy+Sell split / Trade count.")]
        public SideData RightData { get => _s.RightData; set => Set(ref _s.RightData, value, false); }

        [Display(Name = "Left side data", GroupName = "3. Data", Order = 20, Description = "Same choices, independent of the right side.")]
        public SideData LeftData { get => _s.LeftData; set => Set(ref _s.LeftData, value, false); }

        [Display(Name = "Mirror (swap sides)", GroupName = "3. Data", Order = 30, Description = "Swaps what is shown on the right and left of the border.")]
        public bool Mirror { get => _s.Mirror; set => Set(ref _s.Mirror, value, false); }

        [Display(Name = "Normalisation", GroupName = "3. Data", Order = 40, Description = "PerSide: each side scaled to its own max. Shared: both sides use the larger of the two maxima.")]
        public Normalisation Norm { get => _s.Norm; set => Set(ref _s.Norm, value, false); }

        [Display(Name = "Buy+Sell split style", GroupName = "3. Data", Order = 50, Description = "Stacked: buy then sell end to end, never overlapping (default). Overlay: both from the border, smaller drawn over larger.")]
        public SplitStyle Split { get => _s.Split; set => Set(ref _s.Split, value, false); }

        [Display(Name = "Draw profile as", GroupName = "4. Rows", Order = 10, Description = "SeparateRows: each row is its own bar. Continuous: rows merge into one filled shape (no gaps or row borders) with an outline.")]
        public ProfileShape Shape { get => _s.Shape; set => Set(ref _s.Shape, value, false); }

        [Display(Name = "Continuous: smooth edge", GroupName = "4. Rows", Order = 20, Description = "Off (default): bar-shaped steps, one per row, joined into a single solid shape. On: a smooth curve through the row centres (can look spiky).")]
        public bool ContinuousSmooth { get => _s.ContinuousSmooth; set => Set(ref _s.ContinuousSmooth, value, false); }

        [Display(Name = "Continuous: single fill colour", GroupName = "4. Rows", Order = 30, Description = "Total volume / trade count use the fill colour below instead of the value-area colours.")]
        public bool ShapeFillSingle { get => _s.ShapeFillSingle; set => Set(ref _s.ShapeFillSingle, value, false); }

        [Display(Name = "Continuous: fill colour", GroupName = "4. Rows", Order = 40)]
        public Color ShapeFillColor { get => _s.ShapeFillColor; set => Set(ref _s.ShapeFillColor, value, false); }

        [Display(Name = "Row size (price units)", GroupName = "4. Rows", Order = 50, Description = "Own row size in price units (e.g. 2.5 points), independent of the chart tick scale. Rounded to whole instrument ticks. 0 = use Rows per bucket.")]
        [Range(0, 100000)]
        public double RowSizePrice { get => _s.RowSizePrice; set => Set(ref _s.RowSizePrice, Clamp(value, 0, 100000), false); }

        [Display(Name = "Rows per bucket (ticks)", GroupName = "4. Rows", Order = 60, Description = "Row height = this many instrument ticks.")]
        [Range(1, 1000)]
        public int RowsPerBucket { get => _s.RowsPerBucket; set => Set(ref _s.RowsPerBucket, Clamp(value, 1, 1000), false); }

        [Display(Name = "Row mode", GroupName = "4. Rows", Order = 70, Description = "FullFill: contiguous rows, no gaps. VisibleRows: real rows with gap / fixed height / optional border.")]
        public RowMode Rows { get => _s.Rows; set => Set(ref _s.Rows, value, false); }

        [Display(Name = "Visible rows: fixed height, px (0 = auto)", GroupName = "4. Rows", Order = 80)]
        [Range(0, 200)]
        public int RowHeightPx { get => _s.RowHeightPx; set => Set(ref _s.RowHeightPx, Clamp(value, 0, 200), false); }

        [Display(Name = "Visible rows: gap, px", GroupName = "4. Rows", Order = 90)]
        [Range(0, 50)]
        public int RowGapPx { get => _s.RowGapPx; set => Set(ref _s.RowGapPx, Clamp(value, 0, 50), false); }

        [Display(Name = "Visible rows: border", GroupName = "4. Rows", Order = 100)]
        public bool RowBorder { get => _s.RowBorder; set => Set(ref _s.RowBorder, value, false); }

        [Display(Name = "Visible rows: border colour", GroupName = "4. Rows", Order = 110)]
        public Color RowBorderColor { get => _s.RowBorderColor; set => Set(ref _s.RowBorderColor, value, false); }

        [Display(Name = "Value area %", GroupName = "5. Value Area & Levels", Order = 10)]
        [Range(1, 100)]
        public double ValueAreaPct { get => _s.ValueAreaPct; set => Set(ref _s.ValueAreaPct, Clamp(value, 1, 100), false); }

        [Display(Name = "Show HVN rows", GroupName = "5. Value Area & Levels", Order = 20, Description = "High-volume nodes: local peaks >= HVN % of the max row.")]
        public bool ShowHvn { get => _s.ShowHvn; set => Set(ref _s.ShowHvn, value, false); }

        [Display(Name = "HVN threshold, % of max", GroupName = "5. Value Area & Levels", Order = 30)]
        [Range(1, 100)]
        public double HvnPct { get => _s.HvnPct; set => Set(ref _s.HvnPct, Clamp(value, 1, 100), false); }

        [Display(Name = "Show LVN rows", GroupName = "5. Value Area & Levels", Order = 40, Description = "Low-volume nodes: interior local troughs <= LVN % of the max row.")]
        public bool ShowLvn { get => _s.ShowLvn; set => Set(ref _s.ShowLvn, value, false); }

        [Display(Name = "LVN threshold, % of max", GroupName = "5. Value Area & Levels", Order = 50)]
        [Range(0, 99)]
        public double LvnPct { get => _s.LvnPct; set => Set(ref _s.LvnPct, Clamp(value, 0, 99), false); }

        [Display(Name = "Fill: inside value area", GroupName = "6. Colors", Order = 10)]
        public Color InsideVaColor { get => _s.InsideVaColor; set => Set(ref _s.InsideVaColor, value, false); }

        [Display(Name = "Fill: outside value area", GroupName = "6. Colors", Order = 20)]
        public Color OutsideVaColor { get => _s.OutsideVaColor; set => Set(ref _s.OutsideVaColor, value, false); }

        [Display(Name = "Colour the POC row", GroupName = "6. Colors", Order = 30)]
        public bool ColorPocRow { get => _s.ColorPocRow; set => Set(ref _s.ColorPocRow, value, false); }

        [Display(Name = "Fill: POC row", GroupName = "6. Colors", Order = 40)]
        public Color PocRowColor { get => _s.PocRowColor; set => Set(ref _s.PocRowColor, value, false); }

        [Display(Name = "Fill: HVN rows", GroupName = "6. Colors", Order = 50)]
        public Color HvnColor { get => _s.HvnColor; set => Set(ref _s.HvnColor, value, false); }

        [Display(Name = "Fill: LVN rows", GroupName = "6. Colors", Order = 60)]
        public Color LvnColor { get => _s.LvnColor; set => Set(ref _s.LvnColor, value, false); }

        [Display(Name = "Buy (ask) colour", GroupName = "6. Colors", Order = 70)]
        public Color BuyColor { get => _s.BuyColor; set => Set(ref _s.BuyColor, value, false); }

        [Display(Name = "Sell (bid) colour", GroupName = "6. Colors", Order = 80)]
        public Color SellColor { get => _s.SellColor; set => Set(ref _s.SellColor, value, false); }

        [Display(Name = "POC: show", GroupName = "7. Lines", Order = 10)]
        public bool ShowPoc { get => _s.ShowPoc; set => Set(ref _s.ShowPoc, value, false); }

        [Display(Name = "POC: colour", GroupName = "7. Lines", Order = 20)]
        public Color PocColor { get => _s.PocLine.Color; set => Set(ref _s.PocLine.Color, value, false); }

        [Display(Name = "POC: width, px", GroupName = "7. Lines", Order = 30)]
        [Range(1, 20)]
        public int PocWidth { get => _s.PocLine.Width; set => Set(ref _s.PocLine.Width, Clamp(value, 1, 20), false); }

        [Display(Name = "POC: dash", GroupName = "7. Lines", Order = 40)]
        public LineDash PocDash { get => _s.PocLine.Dash; set => Set(ref _s.PocLine.Dash, value, false); }

        [Display(Name = "POC: extend", GroupName = "7. Lines", Order = 50, Description = "ToBoxEnd / FixedPx (length from border) / ToChartEdge.")]
        public LineExtend PocExtend { get => _s.PocLine.Extend; set => Set(ref _s.PocLine.Extend, value, false); }

        [Display(Name = "POC: extension length, px", GroupName = "7. Lines", Order = 60, Description = "FixedPx: length from border. ToBoxEnd: extra px beyond the box.")]
        [Range(0, 10000)]
        public int PocExtendPx { get => _s.PocLine.ExtendPx; set => Set(ref _s.PocLine.ExtendPx, Clamp(value, 0, 10000), false); }

        [Display(Name = "VAH: show", GroupName = "7. Lines", Order = 70)]
        public bool ShowVah { get => _s.ShowVah; set => Set(ref _s.ShowVah, value, false); }

        [Display(Name = "VAH: colour", GroupName = "7. Lines", Order = 80)]
        public Color VahColor { get => _s.VahLine.Color; set => Set(ref _s.VahLine.Color, value, false); }

        [Display(Name = "VAH: width, px", GroupName = "7. Lines", Order = 90)]
        [Range(1, 20)]
        public int VahWidth { get => _s.VahLine.Width; set => Set(ref _s.VahLine.Width, Clamp(value, 1, 20), false); }

        [Display(Name = "VAH: dash", GroupName = "7. Lines", Order = 100)]
        public LineDash VahDash { get => _s.VahLine.Dash; set => Set(ref _s.VahLine.Dash, value, false); }

        [Display(Name = "VAH: extend", GroupName = "7. Lines", Order = 110, Description = "ToBoxEnd / FixedPx (length from border) / ToChartEdge.")]
        public LineExtend VahExtend { get => _s.VahLine.Extend; set => Set(ref _s.VahLine.Extend, value, false); }

        [Display(Name = "VAH: extension length, px", GroupName = "7. Lines", Order = 120, Description = "FixedPx: length from border. ToBoxEnd: extra px beyond the box.")]
        [Range(0, 10000)]
        public int VahExtendPx { get => _s.VahLine.ExtendPx; set => Set(ref _s.VahLine.ExtendPx, Clamp(value, 0, 10000), false); }

        [Display(Name = "VAL: show", GroupName = "7. Lines", Order = 130)]
        public bool ShowVal { get => _s.ShowVal; set => Set(ref _s.ShowVal, value, false); }

        [Display(Name = "VAL: colour", GroupName = "7. Lines", Order = 140)]
        public Color ValColor { get => _s.ValLine.Color; set => Set(ref _s.ValLine.Color, value, false); }

        [Display(Name = "VAL: width, px", GroupName = "7. Lines", Order = 150)]
        [Range(1, 20)]
        public int ValWidth { get => _s.ValLine.Width; set => Set(ref _s.ValLine.Width, Clamp(value, 1, 20), false); }

        [Display(Name = "VAL: dash", GroupName = "7. Lines", Order = 160)]
        public LineDash ValDash { get => _s.ValLine.Dash; set => Set(ref _s.ValLine.Dash, value, false); }

        [Display(Name = "VAL: extend", GroupName = "7. Lines", Order = 170, Description = "ToBoxEnd / FixedPx (length from border) / ToChartEdge.")]
        public LineExtend ValExtend { get => _s.ValLine.Extend; set => Set(ref _s.ValLine.Extend, value, false); }

        [Display(Name = "VAL: extension length, px", GroupName = "7. Lines", Order = 180, Description = "FixedPx: length from border. ToBoxEnd: extra px beyond the box.")]
        [Range(0, 10000)]
        public int ValExtendPx { get => _s.ValLine.ExtendPx; set => Set(ref _s.ValLine.ExtendPx, Clamp(value, 0, 10000), false); }

        [Display(Name = "VWAP: show", GroupName = "7. Lines", Order = 190)]
        public bool ShowVwap { get => _s.ShowVwap; set => Set(ref _s.ShowVwap, value, false); }

        [Display(Name = "VWAP: colour", GroupName = "7. Lines", Order = 200)]
        public Color VwapColor { get => _s.VwapLine.Color; set => Set(ref _s.VwapLine.Color, value, false); }

        [Display(Name = "VWAP: width, px", GroupName = "7. Lines", Order = 210)]
        [Range(1, 20)]
        public int VwapWidth { get => _s.VwapLine.Width; set => Set(ref _s.VwapLine.Width, Clamp(value, 1, 20), false); }

        [Display(Name = "VWAP: dash", GroupName = "7. Lines", Order = 220)]
        public LineDash VwapDash { get => _s.VwapLine.Dash; set => Set(ref _s.VwapLine.Dash, value, false); }

        [Display(Name = "VWAP: extend", GroupName = "7. Lines", Order = 230, Description = "ToBoxEnd / FixedPx (length from border) / ToChartEdge.")]
        public LineExtend VwapExtend { get => _s.VwapLine.Extend; set => Set(ref _s.VwapLine.Extend, value, false); }

        [Display(Name = "VWAP: extension length, px", GroupName = "7. Lines", Order = 240, Description = "FixedPx: length from border. ToBoxEnd: extra px beyond the box.")]
        [Range(0, 10000)]
        public int VwapExtendPx { get => _s.VwapLine.ExtendPx; set => Set(ref _s.VwapLine.ExtendPx, Clamp(value, 0, 10000), false); }

        [Display(Name = "Median: show", GroupName = "7. Lines", Order = 250)]
        public bool ShowMedian { get => _s.ShowMedian; set => Set(ref _s.ShowMedian, value, false); }

        [Display(Name = "Median: colour", GroupName = "7. Lines", Order = 260)]
        public Color MedianColor { get => _s.MedianLine.Color; set => Set(ref _s.MedianLine.Color, value, false); }

        [Display(Name = "Median: width, px", GroupName = "7. Lines", Order = 270)]
        [Range(1, 20)]
        public int MedianWidth { get => _s.MedianLine.Width; set => Set(ref _s.MedianLine.Width, Clamp(value, 1, 20), false); }

        [Display(Name = "Median: dash", GroupName = "7. Lines", Order = 280)]
        public LineDash MedianDash { get => _s.MedianLine.Dash; set => Set(ref _s.MedianLine.Dash, value, false); }

        [Display(Name = "Median: extend", GroupName = "7. Lines", Order = 290, Description = "ToBoxEnd / FixedPx (length from border) / ToChartEdge.")]
        public LineExtend MedianExtend { get => _s.MedianLine.Extend; set => Set(ref _s.MedianLine.Extend, value, false); }

        [Display(Name = "Median: extension length, px", GroupName = "7. Lines", Order = 300, Description = "FixedPx: length from border. ToBoxEnd: extra px beyond the box.")]
        [Range(0, 10000)]
        public int MedianExtendPx { get => _s.MedianLine.ExtendPx; set => Set(ref _s.MedianLine.ExtendPx, Clamp(value, 0, 10000), false); }

        [Display(Name = "Border: show", GroupName = "8. Border / Box / Handles", Order = 10)]
        public bool ShowBorder { get => _s.ShowBorder; set => Set(ref _s.ShowBorder, value, false); }

        [Display(Name = "Border: colour", GroupName = "8. Border / Box / Handles", Order = 20)]
        public Color BorderColor { get => _s.BorderColor; set => Set(ref _s.BorderColor, value, false); }

        [Display(Name = "Border: thickness, px", GroupName = "8. Border / Box / Handles", Order = 30)]
        [Range(1, 20)]
        public int BorderWidth { get => _s.BorderWidth; set => Set(ref _s.BorderWidth, Clamp(value, 1, 20), false); }

        [Display(Name = "Border: style", GroupName = "8. Border / Box / Handles", Order = 40)]
        public LineDash BorderDash { get => _s.BorderDash; set => Set(ref _s.BorderDash, value, false); }

        [Display(Name = "Outline: show (Continuous shape)", GroupName = "8. Border / Box / Handles", Order = 50, Description = "Traces the outer edge of the continuous shape.")]
        public bool ShowOutline { get => _s.ShowOutline; set => Set(ref _s.ShowOutline, value, false); }

        [Display(Name = "Outline: which side", GroupName = "8. Border / Box / Handles", Order = 60, Description = "Right = the right-side data shape, Left = the left-side data shape, or Both.")]
        public OutlineSides OutlineSide { get => _s.OutlineSide; set => Set(ref _s.OutlineSide, value, false); }

        [Display(Name = "Outline: colour by value area", GroupName = "8. Border / Box / Handles", Order = 70, Description = "On: the outline uses the inside / outside value area colours below. Off: one colour.")]
        public bool OutlineByValueArea { get => _s.OutlineByValueArea; set => Set(ref _s.OutlineByValueArea, value, false); }

        [Display(Name = "Outline: inside value area colour", GroupName = "8. Border / Box / Handles", Order = 80)]
        public Color OutlineInsideColor { get => _s.OutlineInsideColor; set => Set(ref _s.OutlineInsideColor, value, false); }

        [Display(Name = "Outline: outside value area colour", GroupName = "8. Border / Box / Handles", Order = 90)]
        public Color OutlineOutsideColor { get => _s.OutlineOutsideColor; set => Set(ref _s.OutlineOutsideColor, value, false); }

        [Display(Name = "Outline: single colour (when not by value area)", GroupName = "8. Border / Box / Handles", Order = 100)]
        public Color OutlineColor { get => _s.OutlineColor; set => Set(ref _s.OutlineColor, value, false); }

        [Display(Name = "Outline: thickness, px", GroupName = "8. Border / Box / Handles", Order = 110)]
        [Range(1, 20)]
        public int OutlineWidth { get => _s.OutlineWidth; set => Set(ref _s.OutlineWidth, Clamp(value, 1, 20), false); }

        [Display(Name = "Outline: style", GroupName = "8. Border / Box / Handles", Order = 120)]
        public LineDash OutlineDash { get => _s.OutlineDash; set => Set(ref _s.OutlineDash, value, false); }

        [Display(Name = "Outline: smooth (join row centres)", GroupName = "8. Border / Box / Handles", Order = 130, Description = "Off: stepped along the rows.")]
        public bool OutlineSmooth { get => _s.OutlineSmooth; set => Set(ref _s.OutlineSmooth, value, false); }

        [Display(Name = "Box: show", GroupName = "8. Border / Box / Handles", Order = 140)]
        public bool ShowBox { get => _s.ShowBox; set => Set(ref _s.ShowBox, value, false); }

        [Display(Name = "Box: colour", GroupName = "8. Border / Box / Handles", Order = 150)]
        public Color BoxColor { get => _s.BoxColor; set => Set(ref _s.BoxColor, value, false); }

        [Display(Name = "Box: line width, px", GroupName = "8. Border / Box / Handles", Order = 160)]
        [Range(1, 20)]
        public int BoxLineWidth { get => _s.BoxLineWidth; set => Set(ref _s.BoxLineWidth, Clamp(value, 1, 20), false); }

        [Display(Name = "Box: line style", GroupName = "8. Border / Box / Handles", Order = 170)]
        public LineDash BoxDash { get => _s.BoxDash; set => Set(ref _s.BoxDash, value, false); }

        [Display(Name = "Box: fill opacity %", GroupName = "8. Border / Box / Handles", Order = 180, Description = "0 = no fill.")]
        [Range(0, 100)]
        public int BoxFillOpacity { get => _s.BoxFillOpacity; set => Set(ref _s.BoxFillOpacity, Clamp(value, 0, 100), false); }

        [Display(Name = "Handles: show", GroupName = "8. Border / Box / Handles", Order = 190)]
        public bool ShowHandles { get => _s.ShowHandles; set => Set(ref _s.ShowHandles, value, false); }

        [Display(Name = "Handles: colour", GroupName = "8. Border / Box / Handles", Order = 200)]
        public Color HandleColor { get => _s.HandleColor; set => Set(ref _s.HandleColor, value, false); }

        [Display(Name = "Handles: size, px", GroupName = "8. Border / Box / Handles", Order = 210)]
        [Range(3, 60)]
        public int HandleSize { get => _s.HandleSize; set => Set(ref _s.HandleSize, Clamp(value, 3, 60), false); }

        [Display(Name = "Numeric labels", GroupName = "9. Labels", Order = 10, Description = "Per-row numbers at the end of the bars.")]
        public LabelSides LabelMode { get => _s.Labels; set => Set(ref _s.Labels, value, false); }

        [Display(Name = "Label font size", GroupName = "9. Labels", Order = 20)]
        [Range(5, 40)]
        public int LabelFontSize { get => _s.LabelFontSize; set => Set(ref _s.LabelFontSize, Clamp(value, 5, 40), false); }

        [Display(Name = "Label colour", GroupName = "9. Labels", Order = 30)]
        public Color LabelColor { get => _s.LabelColor; set => Set(ref _s.LabelColor, value, false); }

        [Display(Name = "Label: min row height to show, px", GroupName = "9. Labels", Order = 40)]
        [Range(1, 100)]
        public int LabelMinRowHeight { get => _s.LabelMinRowHeight; set => Set(ref _s.LabelMinRowHeight, Clamp(value, 1, 100), false); }

        [Display(Name = "Label: abbreviate (1.2K)", GroupName = "9. Labels", Order = 50)]
        public bool LabelAbbreviate { get => _s.LabelAbbreviate; set => Set(ref _s.LabelAbbreviate, value, false); }

    }
}
