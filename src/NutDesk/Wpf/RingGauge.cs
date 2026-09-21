using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NutDesk.Wpf
{
    /// <summary>
    /// A circular gauge: a muted track, a coloured arc for the reading, the reading itself in the middle and
    /// the scale end points below it.
    /// </summary>
    /// <remarks>
    /// Drawn in OnRender rather than composed from shapes: one pass, no visual tree per gauge, and it stays
    /// sharp at any size because everything is derived from the current bounds. Value changes are animated, so
    /// a reading that moves sweeps to its new position instead of jumping.
    /// </remarks>
    public class RingGauge : FrameworkElement
    {
        private const double ARC_START_DEGREES = 135.0;
        private const double ARC_SWEEP_DEGREES = 270.0;

        #region Dependency properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(0.0, OnValueChanged));

        /// <summary>The animated position actually drawn; <see cref="Value"/> is the target.</summary>
        private static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(nameof(DisplayValue), typeof(double), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(0.0,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(0.0,
                                            FrameworkPropertyMetadataOptions.AffectsRender,
                                            OnMinimumChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(100.0,
                                            FrameworkPropertyMetadataOptions.AffectsRender,
                                            null, CoerceMaximum));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(string.Empty,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondaryTextProperty =
            DependencyProperty.Register(nameof(SecondaryText), typeof(string), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(string.Empty,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(Brushes.Gainsboro,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ArcBrushProperty =
            DependencyProperty.Register(nameof(ArcBrush), typeof(Brush), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(Brushes.DodgerBlue,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PrimaryTextBrushProperty =
            DependencyProperty.Register(nameof(PrimaryTextBrush), typeof(Brush), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(Brushes.Black,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondaryTextBrushProperty =
            DependencyProperty.Register(nameof(SecondaryTextBrush), typeof(Brush), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(Brushes.Gray,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Scales the value text; 1.0 suits a small card, larger suits the hero card.</summary>
        public static readonly DependencyProperty EmphasisProperty =
            DependencyProperty.Register(nameof(Emphasis), typeof(double), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(1.0,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowScaleLimitsProperty =
            DependencyProperty.Register(nameof(ShowScaleLimits), typeof(bool), typeof(RingGauge),
                                        new FrameworkPropertyMetadata(true,
                                            FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private double DisplayValue
        {
            get => (double)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        public string SecondaryText
        {
            get => (string)GetValue(SecondaryTextProperty);
            set => SetValue(SecondaryTextProperty, value);
        }

        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public Brush ArcBrush
        {
            get => (Brush)GetValue(ArcBrushProperty);
            set => SetValue(ArcBrushProperty, value);
        }

        public Brush PrimaryTextBrush
        {
            get => (Brush)GetValue(PrimaryTextBrushProperty);
            set => SetValue(PrimaryTextBrushProperty, value);
        }

        public Brush SecondaryTextBrush
        {
            get => (Brush)GetValue(SecondaryTextBrushProperty);
            set => SetValue(SecondaryTextBrushProperty, value);
        }

        public double Emphasis
        {
            get => (double)GetValue(EmphasisProperty);
            set => SetValue(EmphasisProperty, value);
        }

        public bool ShowScaleLimits
        {
            get => (bool)GetValue(ShowScaleLimitsProperty);
            set => SetValue(ShowScaleLimitsProperty, value);
        }

        #endregion

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gauge = (RingGauge)d;

            // Sweep to the new reading rather than snapping to it.
            var animation = new DoubleAnimation((double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(420)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            gauge.BeginAnimation(DisplayValueProperty, animation);
        }

        private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Maximum is only coerced when Maximum itself is assigned, so a minimum that rises afterwards would
            // quietly restore the inverted pair; asking for a fresh coercion keeps the span honest either way.
            d.CoerceValue(MaximumProperty);
        }

        /// <summary>Keeps at least one unit of span between the two ends of the scale.</summary>
        /// <remarks>
        /// The limits come from the calibration settings, and a maximum at or below the minimum draws an empty
        /// arc with the same number printed at both ends: a gauge that looks broken instead of one that shows a
        /// reading. Widening the span here means the arc still means something whatever the settings hold.
        /// </remarks>
        private static object CoerceMaximum(DependencyObject d, object baseValue)
        {
            double minimum = (double)d.GetValue(MinimumProperty);
            return Math.Max((double)baseValue, minimum + 1.0);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double side = Math.Min(ActualWidth, ActualHeight);

            if (side < 24 || double.IsNaN(side))
            {
                return;
            }

            double limitSize = Math.Max(9.0, side * 0.085);
            double stroke = Math.Max(3.0, side * 0.085);
            double diameter = side - stroke - (ShowScaleLimits ? limitSize * 1.7 : 0.0);

            if (diameter <= stroke)
            {
                return;
            }

            double radius = diameter / 2.0;
            var centre = new Point(ActualWidth / 2.0,
                                   ActualHeight / 2.0 - (ShowScaleLimits ? limitSize * 0.35 : 0.0));

            DrawArc(dc, centre, radius, stroke, TrackBrush, 1.0);
            DrawArc(dc, centre, radius, stroke, ArcBrush, Fraction());

            if (ShowScaleLimits)
            {
                DrawScaleLimits(dc, centre, radius, stroke, limitSize);
            }

            DrawReadings(dc, centre, side);
        }

        private double Fraction()
        {
            double span = Maximum - Minimum;

            if (span <= 0.0)
            {
                return 0.0;
            }

            return Math.Max(0.0, Math.Min(1.0, (DisplayValue - Minimum) / span));
        }

        private static void DrawArc(DrawingContext dc, Point centre, double radius, double stroke,
                                    Brush brush, double fraction)
        {
            if (brush == null || fraction <= 0.0)
            {
                return;
            }

            double sweep = ARC_SWEEP_DEGREES * fraction;
            Point startPoint = PointOnCircle(centre, radius, ARC_START_DEGREES);
            Point endPoint = PointOnCircle(centre, radius, ARC_START_DEGREES + sweep);

            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0,
                                               sweep > 180.0, SweepDirection.Clockwise, true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();

            // Left unfrozen on purpose: freezing a Pen freezes everything it holds, so it would reach into the
            // caller's brush - and throw outright if that brush were animated. A pen built and dropped inside a
            // single render pass has nothing to gain from being frozen anyway.
            var pen = new Pen(brush, stroke)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };

            dc.DrawGeometry(null, pen, geometry);
        }

        private static Point PointOnCircle(Point centre, double radius, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Point(centre.X + radius * Math.Cos(radians), centre.Y + radius * Math.Sin(radians));
        }

        private void DrawScaleLimits(DrawingContext dc, Point centre, double radius, double stroke,
                                     double fontSize)
        {
            string low = FormatLimit(Minimum);
            string high = FormatLimit(Maximum);

            FormattedText lowText = BuildText(low, fontSize, SecondaryTextBrush, FontWeights.Normal);
            FormattedText highText = BuildText(high, fontSize, SecondaryTextBrush, FontWeights.Normal);

            Point lowAnchor = PointOnCircle(centre, radius, ARC_START_DEGREES);
            Point highAnchor = PointOnCircle(centre, radius, ARC_START_DEGREES + ARC_SWEEP_DEGREES);
            double top = centre.Y + radius * Math.Sin(135.0 * Math.PI / 180.0) + stroke * 0.35;

            dc.DrawText(lowText, new Point(lowAnchor.X - lowText.Width / 2.0, top));
            dc.DrawText(highText, new Point(highAnchor.X - highText.Width / 2.0, top));
        }

        private static string FormatLimit(double value)
        {
            return Math.Round(value).ToString("0", CultureInfo.CurrentCulture);
        }

        private void DrawReadings(DrawingContext dc, Point centre, double side)
        {
            bool hasSecondary = !string.IsNullOrEmpty(SecondaryText);

            if (string.IsNullOrEmpty(DisplayText))
            {
                return;
            }

            double primarySize = side * 0.165 * Emphasis;
            FormattedText primary = BuildText(DisplayText, primarySize, PrimaryTextBrush, FontWeights.SemiBold);
            double y = centre.Y - primary.Height / 2.0 - (hasSecondary ? primary.Height * 0.34 : 0.0);

            dc.DrawText(primary, new Point(centre.X - primary.Width / 2.0, y));

            if (hasSecondary)
            {
                FormattedText secondary = BuildText(SecondaryText, side * 0.105 * Emphasis, SecondaryTextBrush,
                                                    FontWeights.Normal);
                dc.DrawText(secondary, new Point(centre.X - secondary.Width / 2.0, y + primary.Height * 0.95));
            }
        }

        private static readonly FontFamily GaugeFontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");

        // A reading that moves keeps OnRender busy for the whole 420 ms sweep, once per compositor frame, and
        // every pass asks for up to four texts. The gauge only ever draws these two weights, so they are built
        // once here rather than rebuilt for every string.
        private static readonly Typeface NormalTypeface = new Typeface(GaugeFontFamily, FontStyles.Normal,
                                                                       FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface SemiBoldTypeface = new Typeface(GaugeFontFamily, FontStyles.Normal,
                                                                         FontWeights.SemiBold, FontStretches.Normal);

        /// <summary>Cached DPI scale for text layout; zero means it has not been read yet.</summary>
        private double _pixelsPerDip;

        /// <summary>
        /// The app is system DPI aware, so this hardly ever fires, but moving the window to a screen with a
        /// different scale would otherwise leave every glyph laid out for the old one.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _pixelsPerDip = newDpi.PixelsPerDip;
        }

        private FormattedText BuildText(string text, double size, Brush brush, FontWeight weight)
        {
            if (_pixelsPerDip <= 0.0)
            {
                // Seeded on first use: OnDpiChanged reports changes only, never the scale the gauge starts on.
                _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            }

            Typeface typeface = weight == FontWeights.SemiBold ? SemiBoldTypeface : NormalTypeface;

            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                              typeface, Math.Max(6.0, size),
                                              brush ?? Brushes.Gray,
                                              _pixelsPerDip);
            return formatted;
        }
    }
}
