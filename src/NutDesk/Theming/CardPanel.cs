using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NutDesk.Theming
{
    /// <summary>
    /// A flat rounded container, used in place of the classic GroupBox.
    /// </summary>
    /// <remarks>
    /// A GroupBox cannot be restyled from the outside: WinForms raises its Paint event first and then draws its
    /// own 3D frame on top, so any card painted by a handler ends up with the old frame over it. Since none of
    /// this application's group boxes carry a caption - a Label inside each one is the title - swapping them for
    /// this panel loses nothing and gives complete control of the chrome.
    /// </remarks>
    internal class CardPanel : Panel
    {
        /// <summary>Optional heading, drawn in the top-left corner. Usually empty here.</summary>
        public string Caption { get; set; } = string.Empty;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BorderStyle = BorderStyle.None;

            // Child controls inherit this as their ambient background, so it has to match what OnPaint fills the
            // card with - otherwise every label sits in a visible darker rectangle.
            BackColor = Theme.CardBack;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fill the corners with the window colour so the rounded card does not sit on stale pixels.
            using (var outside = new SolidBrush(Parent != null ? Parent.BackColor : Theme.WindowBack))
            {
                g.FillRectangle(outside, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Max(4, Convert.ToInt32(Math.Min(Width, Height) * 0.06));

            using (var path = Theme.RoundedRect(bounds, radius))
            {
                using (var fill = new SolidBrush(Theme.CardBack))
                {
                    g.FillPath(fill, path);
                }
                using (var pen = new Pen(Theme.CardBorder, 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            if (!string.IsNullOrEmpty(Caption))
            {
                using (var brush = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString(Caption, Font, brush, radius, radius / 2);
                }
            }

            base.OnPaint(e);
        }
    }
}
