// WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
// Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NutDesk.Theming;

namespace NutDesk.Controls
{
    /// <summary>
    /// A flat arc gauge, drawn entirely by <see cref="OnPaint"/>.
    /// </summary>
    /// <remarks>
    /// This used to derive from AGaugeClassic.AGauge, but every pixel it shows is now produced here. The base
    /// class kept painting its own frame, background arc, tick marks and needle from a sealed
    /// OnPaintBackground: it could not be overridden away, it left a dark rectangle around the control, and it
    /// threw ArgumentException from Graphics.DrawArc as soon as the docked layout let the gauge take sizes its
    /// fixed pixel radii were never written for. The properties the designer serialises are kept below so the
    /// generated code still compiles; the purely geometric ones are inert.
    /// </remarks>
    internal partial class UPSVarGauge : Control
    {
        #region Geometry

        // Sweep of the arc, in degrees, starting from the lower left and running clockwise through the top.
        private const float ARC_START = 135.0f;
        private const float ARC_SWEEP = 270.0f;

        // All of the following are fractions of the gauge's short side, so the drawing scales with the control
        // (and therefore with the display's DPI) instead of being pinned to a fixed pixel radius.
        private const float STROKE_FRACTION = 0.085f;
        private const float VALUE_TEXT_FRACTION = 0.175f;
        private const float SECOND_TEXT_FRACTION = 0.115f;
        private const float LIMIT_TEXT_FRACTION = 0.085f;

        #endregion

        #region Private Fields

        private float m_value2;

        private GradientTypeEnum m_gradientType = GradientTypeEnum.RedGreen;
        private GradientOrientationEnum m_gradientOrientation = GradientOrientationEnum.BottomToTop;
        private UnitValueEnum m_unitvalue1 = UnitValueEnum.Volts;
        private UnitValueEnum m_unitvalue2 = UnitValueEnum.None;

        private float? m_warnBelow = null;
        private float? m_criticalBelow = null;
        private float? m_warnAbove = null;
        private float? m_criticalAbove = null;

        // Fonts are rebuilt only when the control is resized, not on every one-second repaint.
        private int cachedFontSide = -1;
        private Font valueFont;
        private Font secondFont;
        private Font limitFont;

        #endregion

        #region Scale

        private float m_value;
        private int m_minValue = 0;
        private int m_maxValue = 100;

        /// <summary>The reading the arc represents.</summary>
        [Browsable(true), Category("Gauge"), Description("Current value.")]
        public float Value
        {
            get => m_value;
            set
            {
                if (m_value != value)
                {
                    m_value = value;
                    Invalidate();
                }
            }
        }

        [Browsable(true), Category("Gauge"), Description("Lowest value on the scale.")]
        public int MinValue
        {
            get => m_minValue;
            set
            {
                if (m_minValue != value)
                {
                    m_minValue = value;
                    Invalidate();
                }
            }
        }

        [Browsable(true), Category("Gauge"), Description("Highest value on the scale.")]
        public int MaxValue
        {
            get => m_maxValue;
            set
            {
                if (m_maxValue != value)
                {
                    m_maxValue = value;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Inert legacy geometry

        // Kept only so the designer-generated code and the existing calibration code still compile. The arc is
        // laid out from the control's own size now, so none of these affect what is drawn.
        [Browsable(false)] public int BaseArcRadius { get; set; }
        [Browsable(false)] public int BaseArcWidth { get; set; }
        [Browsable(false)] public int NeedleRadius { get; set; }
        [Browsable(false)] public int ScaleLinesInterInnerRadius { get; set; }
        [Browsable(false)] public int ScaleLinesInterOuterRadius { get; set; }
        [Browsable(false)] public int ScaleLinesMajorInnerRadius { get; set; }
        [Browsable(false)] public int ScaleLinesMajorOuterRadius { get; set; }
        [Browsable(false)] public int ScaleLinesMinorInnerRadius { get; set; }
        [Browsable(false)] public int ScaleLinesMinorOuterRadius { get; set; }
        [Browsable(false)] public int ScaleLinesMajorStepValue { get; set; }
        [Browsable(false)] public string ScaleNumbersFormat { get; set; }
        [Browsable(false)] public int ScaleNumbersRadius { get; set; }

        #endregion

        #region Properties

        // Map Value1 onto Value
        [Browsable(true),
         Category("AGauge"),
         Description("First value to display.")]
        public float Value1
        {
            get => Value;
            set => Value = value;
        }

        [Browsable(true),
         Category("AGauge"),
         Description("Second value to display.")]
        public float Value2
        {
            get => m_value2;
            set
            {
                if (m_value2 != value)
                {
                    m_value2 = value;
                    Invalidate();
                }
            }
        }

        [Browsable(true),
         Category("AGauge"),
         Description("UseColor For Arc Base Color.")]
        public GradientTypeEnum GradientType
        {
            get => m_gradientType;
            set
            {
                m_gradientType = value;
                Invalidate();
            }
        }

        [Browsable(true),
         Category("AGauge"),
         Description("Orientation Of Gradient Colors.")]
        public GradientOrientationEnum GradientOrientation
        {
            get => m_gradientOrientation;
            set
            {
                if (m_gradientOrientation != value)
                {
                    m_gradientOrientation = value;
                    Invalidate();
                }
            }
        }

        [Browsable(true),
         Category("AGauge"),
         Description("Units For Value 1")]
        public UnitValueEnum UnitValue1
        {
            get => m_unitvalue1;
            set
            {
                if (m_unitvalue1 != value)
                {
                    m_unitvalue1 = value;
                    Invalidate();
                }
            }
        }

        [Browsable(true),
         Category("AGauge"),
         Description("UseColor For Arc Base Color.")]
        public UnitValueEnum UnitValue2
        {
            get => m_unitvalue2;
            set
            {
                if (m_unitvalue2 != value)
                {
                    m_unitvalue2 = value;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Warning thresholds

        /// <summary>
        /// Colour the arc amber below this reading, red below <see cref="CriticalBelow"/>. Used for readings
        /// where low is bad, such as battery charge. Leave unset to keep the arc in the accent colour.
        /// </summary>
        [Browsable(false)]
        public float? WarnBelow
        {
            get => m_warnBelow;
            set
            {
                m_warnBelow = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        public float? CriticalBelow
        {
            get => m_criticalBelow;
            set
            {
                m_criticalBelow = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Colour the arc amber above this reading, red above <see cref="CriticalAbove"/>. Used for readings
        /// where high is bad, such as load.
        /// </summary>
        [Browsable(false)]
        public float? WarnAbove
        {
            get => m_warnAbove;
            set
            {
                m_warnAbove = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        public float? CriticalAbove
        {
            get => m_criticalAbove;
            set
            {
                m_criticalAbove = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Set both warning bands at once, as a convenience for the form's setup code.
        /// </summary>
        public void SetThresholds(float? warnBelow, float? criticalBelow,
                                  float? warnAbove, float? criticalAbove)
        {
            m_warnBelow = warnBelow;
            m_criticalBelow = criticalBelow;
            m_warnAbove = warnAbove;
            m_criticalAbove = criticalAbove;
            Invalidate();
        }

        #endregion

        public enum GradientTypeEnum
        {
            None,
            RedGreen
        }

        public enum GradientOrientationEnum
        {
            TopToBottom,
            BottomToTop,
            RightToLeft,
            LeftToRight
        }

        public enum UnitValueEnum
        {
            None,
            Hertz,
            Percent,
            Volts,
            Watts
        }

        public UPSVarGauge()
            : base()
        {
            InitializeComponent();

            Size = new Size(148, 130);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>
        /// Draws the whole gauge: track arc, value arc, scale end points and the readings.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // The gauge sits on a card, so paint the card colour rather than relying on control transparency.
            using (var back = new SolidBrush(Theme.CardBack))
            {
                g.FillRectangle(back, ClientRectangle);
            }

            var side = Math.Min(ClientSize.Width, ClientSize.Height);
            if (side < 24)
            {
                return;
            }

            EnsureFonts(side);

            var stroke = Math.Max(2.0f, side * STROKE_FRACTION);

            // Leave room under the arc for the min/max labels, which sit just outside its lower ends.
            var diameter = side - stroke - limitFont.Height * 0.9f;

            if (diameter <= stroke)
            {
                // Too small to draw a meaningful arc; the readings alone would not fit either.
                return;
            }

            var arcRect = new RectangleF(
                (ClientSize.Width - diameter) / 2.0f,
                (ClientSize.Height - diameter) / 2.0f - limitFont.Height * 0.25f,
                diameter, diameter);

            DrawTrack(g, arcRect, stroke);
            var fraction = ValueFraction();
            DrawValueArc(g, arcRect, stroke, fraction);
            DrawLimits(g, arcRect, stroke);
            DrawReadings(g, arcRect);
        }

        private void DrawTrack(Graphics g, RectangleF arcRect, float stroke)
        {
            using (var pen = new Pen(Theme.Track, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, arcRect, ARC_START, ARC_SWEEP);
            }
        }

        private void DrawValueArc(Graphics g, RectangleF arcRect, float stroke, float fraction)
        {
            if (fraction <= 0.0f)
            {
                return;
            }

            using (var pen = new Pen(ArcColor(), stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, arcRect, ARC_START, ARC_SWEEP * fraction);
            }
        }

        /// <summary>
        /// Where the current reading sits within [MinValue, MaxValue], clamped to the arc.
        /// </summary>
        private float ValueFraction()
        {
            var span = (float)(MaxValue - MinValue);
            if (span <= 0.0f)
            {
                return 0.0f;
            }

            var fraction = (Value - MinValue) / span;
            return Math.Max(0.0f, Math.Min(1.0f, fraction));
        }

        /// <summary>
        /// Accent normally; amber or red once the reading enters a configured warning band.
        /// </summary>
        private Color ArcColor()
        {
            if (m_criticalBelow.HasValue && Value <= m_criticalBelow.Value)
            {
                return Theme.Danger;
            }
            if (m_criticalAbove.HasValue && Value >= m_criticalAbove.Value)
            {
                return Theme.Danger;
            }
            if (m_warnBelow.HasValue && Value <= m_warnBelow.Value)
            {
                return Theme.Warning;
            }
            if (m_warnAbove.HasValue && Value >= m_warnAbove.Value)
            {
                return Theme.Warning;
            }

            return Theme.Accent;
        }

        /// <summary>
        /// The scale end points, placed just below the two open ends of the arc.
        /// </summary>
        private void DrawLimits(Graphics g, RectangleF arcRect, float stroke)
        {
            using (var brush = new SolidBrush(Theme.TextSecondary))
            {
                var radius = arcRect.Width / 2.0f;
                var cx = arcRect.X + radius;
                var cy = arcRect.Y + radius;

                // The arc opens downwards, 135 degrees either side of straight down.
                var dx = (float)(radius * Math.Cos(135.0 * Math.PI / 180.0));
                var dy = (float)(radius * Math.Sin(135.0 * Math.PI / 180.0));

                var minText = FormatLimit(MinValue);
                var maxText = FormatLimit(MaxValue);
                var minSize = g.MeasureString(minText, limitFont);
                var maxSize = g.MeasureString(maxText, limitFont);

                g.DrawString(minText, limitFont, brush,
                             cx + dx - minSize.Width / 2.0f, cy + dy + stroke * 0.2f);
                g.DrawString(maxText, limitFont, brush,
                             cx - dx - maxSize.Width / 2.0f, cy + dy + stroke * 0.2f);
            }
        }

        private static string FormatLimit(int value)
        {
            return value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// The primary reading in the middle of the arc, with the secondary reading beneath it when present.
        /// </summary>
        private void DrawReadings(Graphics g, RectangleF arcRect)
        {
            var cx = arcRect.X + arcRect.Width / 2.0f;
            var cy = arcRect.Y + arcRect.Height / 2.0f;

            var hasSecond = m_unitvalue2 != UnitValueEnum.None;
            var primaryText = m_unitvalue1 == UnitValueEnum.None ? string.Empty : ApplyUnit(Value1, m_unitvalue1);

            if (primaryText != string.Empty)
            {
                var size = g.MeasureString(primaryText, valueFont);
                var y = cy - size.Height / 2.0f - (hasSecond ? size.Height * 0.32f : 0.0f);

                using (var brush = new SolidBrush(Theme.TextPrimary))
                {
                    g.DrawString(primaryText, valueFont, brush, cx - size.Width / 2.0f, y);
                }

                if (hasSecond)
                {
                    var secondText = ApplyUnit(Value2, m_unitvalue2);
                    var secondSize = g.MeasureString(secondText, secondFont);

                    using (var brush = new SolidBrush(Theme.TextSecondary))
                    {
                        g.DrawString(secondText, secondFont, brush,
                                     cx - secondSize.Width / 2.0f, y + size.Height * 0.92f);
                    }
                }
            }
        }

        /// <summary>
        /// Rebuild the fonts only when the control's size has actually changed. Creating them on every repaint
        /// - as the previous implementation did, without disposing them - leaked a GDI handle per gauge per
        /// second.
        /// </summary>
        private void EnsureFonts(int side)
        {
            if (side == cachedFontSide && valueFont != null)
            {
                return;
            }

            DisposeFonts();

            // Pixel units, so the sizes follow the already DPI-scaled control instead of scaling a second time.
            valueFont = new Font(Theme.FONT_FAMILY, side * VALUE_TEXT_FRACTION, FontStyle.Bold, GraphicsUnit.Pixel);
            secondFont = new Font(Theme.FONT_FAMILY, side * SECOND_TEXT_FRACTION, FontStyle.Regular, GraphicsUnit.Pixel);
            limitFont = new Font(Theme.FONT_FAMILY, side * LIMIT_TEXT_FRACTION, FontStyle.Regular, GraphicsUnit.Pixel);

            cachedFontSide = side;
        }

        private void DisposeFonts()
        {
            valueFont?.Dispose();
            secondFont?.Dispose();
            limitFont?.Dispose();
            valueFont = null;
            secondFont = null;
            limitFont = null;
            cachedFontSide = -1;
        }

        /// <summary>
        /// The designer partial class already overrides Dispose, so the cached fonts are released here instead.
        /// EnsureFonts rebuilds them on the next paint if the handle is recreated.
        /// </summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            DisposeFonts();
            base.OnHandleDestroyed(e);
        }

        private string ApplyUnit(float value, UnitValueEnum unit)
        {
            var returnStr = value.ToString(System.Globalization.CultureInfo.CurrentCulture);

            switch (unit)
            {
                case UnitValueEnum.Hertz:
                    returnStr += " Hz";
                    break;
                case UnitValueEnum.Percent:
                    returnStr += " %";
                    break;
                case UnitValueEnum.Volts:
                    returnStr += " V";
                    break;
                case UnitValueEnum.Watts:
                    returnStr += " W";
                    break;
            }

            return returnStr;
        }
    }
}
