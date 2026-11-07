import os
G = {1: "1. Window / Anchor", 2: "2. Position & Size", 3: "3. Data", 4: "4. Rows", 5: "5. Value Area & Levels",
     6: "6. Colors", 7: "7. Lines", 8: "8. Border / Box / Handles", 9: "9. Labels"}
P = []


def add(g, name, typ, field, disp, desc="", recalc=False, rng=None):
    P.append((g, name, typ, field, disp, desc, recalc, rng))


add(1, "UseFixedAnchor", "bool", "UseFixedAnchor", "Use fixed anchor start (else rolling hours)", "Off: window = last N hours ending at the last bar. On: window = anchor start time .. last bar.", True)
add(1, "Hours", "double", "Hours", "Window hours (rolling)", "Fractional allowed (0.5, 1.25, 6). Bars whose open time is later than lastBarTime - hours are included.", True, (0.01, 2000))
add(1, "Days", "double", "Days", "Window days (0 = use hours)", "If above 0, overrides Window hours: window = last N days (fractional ok, no upper limit).", True)
add(1, "Multi", "bool", "Multi", "Multiple profiles (one per window)", "Instead of one profile, draw one for every window period (every N hours or N days, aligned to midnight in the anchor time zone; with a fixed anchor, periods start at the anchor).", True)
add(1, "AnchorStart", "string", "AnchorStart", "Anchor start (yyyy-MM-dd HH:mm)", "Fixed anchor start, e.g. 2026-11-06 01:20. Used when 'Use fixed anchor' is on. Invalid text -> rolling window.", True)
add(1, "AnchorBase", "AnchorTimeBase", "AnchorBase", "Anchor time zone", "How the anchor text is interpreted: PC local, UTC or instrument exchange time.", True)
add(1, "Layer", "RenderLayer", "Layer", "Render layer", "Which ATAS drawing layer the profile is painted in. If nothing shows, try LatestBar or All.")

add(2, "Edge", "BorderEdge", "Edge", "Border on box edge", "Which vertical edge of the anchor box the border (profile origin) sits on.")
add(2, "OffsetPx", "int", "OffsetPx", "Border offset, px", "Shifts the border horizontally (positive = right).", False, (-5000, 5000))
add(2, "Direction", "GrowDirection", "Direction", "Right-side data grows", "Direction the 'Right side' data grows from the border; the 'Left side' data grows the other way.")
add(2, "WidthUnit", "WidthUnit", "WidthUnit", "Width unit", "Side widths in pixels or as a percent of the window (box) width.")
add(2, "RightWidth", "double", "RightWidth", "Right side width", "px or % of window depending on Width unit.", False, (0, 5000))
add(2, "LeftWidth", "double", "LeftWidth", "Left side width", "px or % of window depending on Width unit.", False, (0, 5000))
add(2, "MaxWidthPx", "int", "MaxWidthPx", "Max width per side, px (0 = no cap)", "", False, (0, 5000))

add(3, "RightData", "SideData", "RightData", "Right side data", "None / Total / Buy(ask) / Sell(bid) / Delta / Buy+Sell split / Trade count.")
add(3, "LeftData", "SideData", "LeftData", "Left side data", "Same choices, independent of the right side.")
add(3, "Mirror", "bool", "Mirror", "Mirror (swap sides)", "Swaps what is shown on the right and left of the border.")
add(3, "Norm", "Normalisation", "Norm", "Normalisation", "PerSide: each side scaled to its own max. Shared: both sides use the larger of the two maxima.")
add(3, "Split", "SplitStyle", "Split", "Buy+Sell split style", "Stacked: buy then sell end to end, never overlapping (default). Overlay: both from the border, smaller drawn over larger.")

add(4, "Shape", "ProfileShape", "Shape", "Draw profile as", "SeparateRows: each row is its own bar. Continuous: rows merge into one filled shape (no gaps or row borders) with an outline.")
add(4, "ContinuousSmooth", "bool", "ContinuousSmooth", "Continuous: smooth edge", "Off (default): bar-shaped steps, one per row, joined into a single solid shape. On: a smooth curve through the row centres (can look spiky).")
add(4, "ShapeFillSingle", "bool", "ShapeFillSingle", "Continuous: single fill colour", "Total volume / trade count use the fill colour below instead of the value-area colours.")
add(4, "ShapeFillColor", "Color", "ShapeFillColor", "Continuous: fill colour")
add(4, "RowSizePrice", "double", "RowSizePrice", "Row size (price units)", "Own row size in price units (e.g. 2.5 points), independent of the chart tick scale. Rounded to whole instrument ticks. 0 = use Rows per bucket.", False, (0, 100000))
add(4, "RowsPerBucket", "int", "RowsPerBucket", "Rows per bucket (ticks)", "Row height = this many instrument ticks.", False, (1, 1000))
add(4, "Rows", "RowMode", "Rows", "Row mode", "FullFill: contiguous rows, no gaps. VisibleRows: real rows with gap / fixed height / optional border.")
add(4, "RowHeightPx", "int", "RowHeightPx", "Visible rows: fixed height, px (0 = auto)", "", False, (0, 200))
add(4, "RowGapPx", "int", "RowGapPx", "Visible rows: gap, px", "", False, (0, 50))
add(4, "RowBorder", "bool", "RowBorder", "Visible rows: border", "")
add(4, "RowBorderColor", "Color", "RowBorderColor", "Visible rows: border colour")

add(5, "ValueAreaPct", "double", "ValueAreaPct", "Value area %", "", False, (1, 100))
add(5, "ShowHvn", "bool", "ShowHvn", "Show HVN rows", "High-volume nodes: local peaks >= HVN % of the max row.")
add(5, "HvnPct", "double", "HvnPct", "HVN threshold, % of max", "", False, (1, 100))
add(5, "ShowLvn", "bool", "ShowLvn", "Show LVN rows", "Low-volume nodes: interior local troughs <= LVN % of the max row.")
add(5, "LvnPct", "double", "LvnPct", "LVN threshold, % of max", "", False, (0, 99))

add(6, "InsideVaColor", "Color", "InsideVaColor", "Fill: inside value area")
add(6, "OutsideVaColor", "Color", "OutsideVaColor", "Fill: outside value area")
add(6, "ColorPocRow", "bool", "ColorPocRow", "Colour the POC row")
add(6, "PocRowColor", "Color", "PocRowColor", "Fill: POC row")
add(6, "HvnColor", "Color", "HvnColor", "Fill: HVN rows")
add(6, "LvnColor", "Color", "LvnColor", "Fill: LVN rows")
add(6, "BuyColor", "Color", "BuyColor", "Buy (ask) colour")
add(6, "SellColor", "Color", "SellColor", "Sell (bid) colour")

for key, nice in (("Poc", "POC"), ("Vah", "VAH"), ("Val", "VAL"), ("Vwap", "VWAP"), ("Median", "Median")):
    add(7, "Show" + key, "bool", "Show" + key, "%s: show" % nice)
    add(7, key + "Color", "Color", key + "Line.Color", "%s: colour" % nice)
    add(7, key + "Width", "int", key + "Line.Width", "%s: width, px" % nice, "", False, (1, 20))
    add(7, key + "Dash", "LineDash", key + "Line.Dash", "%s: dash" % nice)
    add(7, key + "Extend", "LineExtend", key + "Line.Extend", "%s: extend" % nice, "ToBoxEnd / FixedPx (length from border) / ToChartEdge.")
    add(7, key + "ExtendPx", "int", key + "Line.ExtendPx", "%s: extension length, px" % nice, "FixedPx: length from border. ToBoxEnd: extra px beyond the box.", False, (0, 10000))

add(8, "ShowBorder", "bool", "ShowBorder", "Border: show")
add(8, "BorderColor", "Color", "BorderColor", "Border: colour")
add(8, "BorderWidth", "int", "BorderWidth", "Border: thickness, px", "", False, (1, 20))
add(8, "BorderDash", "LineDash", "BorderDash", "Border: style")
add(8, "ShowOutline", "bool", "ShowOutline", "Outline: show (Continuous shape)", "Traces the outer edge of the continuous shape.")
add(8, "OutlineSide", "OutlineSides", "OutlineSide", "Outline: which side", "Right = the right-side data shape, Left = the left-side data shape, or Both.")
add(8, "OutlineByValueArea", "bool", "OutlineByValueArea", "Outline: colour by value area", "On: the outline uses the inside / outside value area colours below. Off: one colour.")
add(8, "OutlineInsideColor", "Color", "OutlineInsideColor", "Outline: inside value area colour")
add(8, "OutlineOutsideColor", "Color", "OutlineOutsideColor", "Outline: outside value area colour")
add(8, "OutlineColor", "Color", "OutlineColor", "Outline: single colour (when not by value area)")
add(8, "OutlineWidth", "int", "OutlineWidth", "Outline: thickness, px", "", False, (1, 20))
add(8, "OutlineDash", "LineDash", "OutlineDash", "Outline: style")
add(8, "OutlineSmooth", "bool", "OutlineSmooth", "Outline: smooth (join row centres)", "Off: stepped along the rows.")
add(8, "ShowBox", "bool", "ShowBox", "Box: show")
add(8, "BoxColor", "Color", "BoxColor", "Box: colour")
add(8, "BoxLineWidth", "int", "BoxLineWidth", "Box: line width, px", "", False, (1, 20))
add(8, "BoxDash", "LineDash", "BoxDash", "Box: line style")
add(8, "BoxFillOpacity", "int", "BoxFillOpacity", "Box: fill opacity %", "0 = no fill.", False, (0, 100))
add(8, "ShowHandles", "bool", "ShowHandles", "Handles: show")
add(8, "HandleColor", "Color", "HandleColor", "Handles: colour")
add(8, "HandleSize", "int", "HandleSize", "Handles: size, px", "", False, (3, 60))

add(9, "LabelMode", "LabelSides", "Labels", "Numeric labels", "Per-row numbers at the end of the bars.")
add(9, "LabelFontSize", "int", "LabelFontSize", "Label font size", "", False, (5, 40))
add(9, "LabelColor", "Color", "LabelColor", "Label colour")
add(9, "LabelMinRowHeight", "int", "LabelMinRowHeight", "Label: min row height to show, px", "", False, (1, 100))
add(9, "LabelAbbreviate", "bool", "LabelAbbreviate", "Label: abbreviate (1.2K)")

out = ["// <auto-generated> by tools/genprops.py - edit the table there, not this file. </auto-generated>",
       "using System.ComponentModel.DataAnnotations;", "using System.Drawing;", "", "namespace VolumeProfileX", "{",
       "    public sealed partial class AnchoredVolumeProfile", "    {"]
order = {}
for (g, name, typ, field, disp, desc, recalc, rng) in P:
    order[g] = order.get(g, 0) + 10
    out.append("        [Display(Name = \"%s\", GroupName = \"%s\", Order = %d%s)]" % (
        disp.replace('"', "'"), G[g], order[g], (", Description = \"%s\"" % desc.replace('"', "'")) if desc else ""))
    numeric = rng and typ in ("int", "double")
    if numeric:
        out.append("        [Range(%s, %s)]" % (rng[0], rng[1]))
    flag = "true" if recalc else "false"
    if numeric:
        setter = "Set(ref _s.%s, Clamp(value, %s, %s), %s)" % (field, rng[0], rng[1], flag)
    else:
        setter = "Set(ref _s.%s, value, %s)" % (field, flag)
    out.append("        public %s %s { get => _s.%s; set => %s; }" % (typ, name, field, setter))
    out.append("")
out += ["    }", "}"]
path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "IndicatorProps.g.cs")
open(path, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("wrote", os.path.normpath(path), len(P), "properties")
