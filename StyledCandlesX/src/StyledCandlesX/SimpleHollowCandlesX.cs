using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using OFT.Rendering.Context;

namespace StyledCandlesX
{
    [DisplayName("Simple Hollow Candles X")]
    public class SimpleHollowCandlesX : Indicator, IChartMap
    {
        public static readonly Color DefaultBullFill = Color.FromArgb(60, 202, 18);
        public static readonly Color DefaultBullLine = Color.FromArgb(116, 226, 93);
        public static readonly Color DefaultBearFill = Color.FromArgb(84, 32, 138);
        public static readonly Color DefaultBearLine = Color.FromArgb(148, 108, 188);
        public static readonly Color DefaultBullGlow = Color.FromArgb(92, 222, 62);
        public static readonly Color DefaultBearGlow = Color.FromArgb(150, 100, 210);

        [Display(Name = "Bull Hollow", GroupName = "Candles", Order = 10)]
        public bool BullHollow { get; set; }

        [Display(Name = "Bear Hollow", GroupName = "Candles", Order = 11)]
        public bool BearHollow { get; set; }

        [Display(Name = "Bull Fill Color", GroupName = "Candles", Order = 20)]
        public Color BullFillColor { get; set; } = DefaultBullFill;

        [Display(Name = "Bull Border Color", GroupName = "Candles", Order = 21)]
        public Color BullBorderColor { get; set; } = DefaultBullLine;

        [Display(Name = "Bull Wick Color", GroupName = "Candles", Order = 22)]
        public Color BullWickColor { get; set; } = DefaultBullLine;

        [Display(Name = "Bear Fill Color", GroupName = "Candles", Order = 30)]
        public Color BearFillColor { get; set; } = DefaultBearFill;

        [Display(Name = "Bear Border Color", GroupName = "Candles", Order = 31)]
        public Color BearBorderColor { get; set; } = DefaultBearLine;

        [Display(Name = "Bear Wick Color", GroupName = "Candles", Order = 32)]
        public Color BearWickColor { get; set; } = DefaultBearLine;

        [Display(Name = "Body Width %", GroupName = "Candles", Order = 40, Description = "Candle body width as a percent of bar width. No min/max limit.")]
        public decimal BodyWidthPercent { get; set; } = 80m;

        [Display(Name = "Horizontal Offset %", GroupName = "Candles", Order = 41, Description = "Shifts the candle body and wick horizontally, as a percent of bar width. Scales with zoom so the offset stays anchored to the bar instead of the screen. Positive = right, negative = left. No min/max limit.")]
        public decimal HorizontalOffsetPercent { get; set; }

        [Display(Name = "Wick Width", GroupName = "Candles", Order = 42)]
        public int WickWidth { get; set; } = 1;

        [Display(Name = "Border Width", GroupName = "Candles", Order = 43)]
        public int BorderWidth { get; set; } = 1;

        [Display(Name = "Hide Native Candles", GroupName = "Candles", Order = 50, Description = "Replace ATAS's default candle rendering with this indicator's candles.")]
        public bool HideNativeCandles { get; set; } = true;

        [Display(Name = "Glow", GroupName = "Highlight", Order = 60, Description = "Draw a soft halo (stacked outlines with decreasing opacity) around every candle.")]
        public bool GlowEnabled { get; set; } = true;

        [Display(Name = "Bull Glow Color", GroupName = "Highlight", Order = 61)]
        public Color BullGlowColor { get; set; } = DefaultBullGlow;

        [Display(Name = "Bear Glow Color", GroupName = "Highlight", Order = 62)]
        public Color BearGlowColor { get; set; } = DefaultBearGlow;

        [Display(Name = "Glow Opacity %", GroupName = "Highlight", Order = 63, Description = "Opacity of the innermost glow ring, 0-100. Outer rings fade out.")]
        public int GlowOpacity { get; set; } = 35;

        [Display(Name = "Glow Thickness px", GroupName = "Highlight", Order = 64, Description = "Total halo thickness in pixels.")]
        public int GlowThickness { get; set; } = 3;

        [Display(Name = "Glow Layers", GroupName = "Highlight", Order = 65, Description = "Number of stacked rings (1-4); more layers = smoother falloff.")]
        public int GlowLayers { get; set; } = 3;

        private readonly CandleRenderer _renderer = new CandleRenderer();
        private readonly CandleStyle _style = new CandleStyle();
        private IChartContainer _container;
        private int _lastBar;
        private ChartVisualModes? _savedMode;

        public SimpleHollowCandlesX()
            : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
        }

        protected override void OnDispose()
        {
            try
            {
                if (_savedMode.HasValue && ChartInfo != null) ChartInfo.ChartVisualMode = _savedMode.Value;
            }
            catch { }
            base.OnDispose();
        }

        int IChartMap.FirstBar => Math.Max(FirstVisibleBarNumber, 0);
        int IChartMap.LastBar => Math.Min(LastVisibleBarNumber, CurrentBar);
        int IChartMap.XByBar(int bar) => _container.GetXByBar(bar, true);
        int IChartMap.YByPrice(decimal price) => _container.GetYByPrice(price, true);

        bool IChartMap.TryGetCandle(int bar, out decimal open, out decimal high, out decimal low, out decimal close)
        {
            open = high = low = close = 0m;
            try
            {
                var c = GetCandle(bar);
                if (c == null) return false;
                open = c.Open; high = c.High; low = c.Low; close = c.Close;
                return true;
            }
            catch { return false; }
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo == null || CurrentBar == 0) return;
            try
            {
                _container = ChartInfo.PriceChartContainer;
                if (_container == null) return;
                if (HideNativeCandles && ChartInfo.ChartVisualMode != ChartVisualModes.Hidden)
                {
                    _savedMode ??= ChartInfo.ChartVisualMode;
                    ChartInfo.ChartVisualMode = ChartVisualModes.Hidden;
                }
                CopyStyle();
                _renderer.Render(context, this, _style);
            }
            catch
            {
            }
        }

        private void CopyStyle()
        {
            var s = _style;
            s.BullHollow = BullHollow; s.BearHollow = BearHollow;
            s.BullFill = BullFillColor; s.BullBorder = BullBorderColor; s.BullWick = BullWickColor;
            s.BearFill = BearFillColor; s.BearBorder = BearBorderColor; s.BearWick = BearWickColor;
            s.BodyWidthPercent = BodyWidthPercent; s.HorizontalOffsetPercent = HorizontalOffsetPercent;
            s.WickWidth = WickWidth; s.BorderWidth = BorderWidth;
            s.GlowEnabled = GlowEnabled; s.BullGlow = BullGlowColor; s.BearGlow = BearGlowColor;
            s.GlowOpacity = GlowOpacity; s.GlowThickness = GlowThickness; s.GlowLayers = GlowLayers;
        }
    }
}
