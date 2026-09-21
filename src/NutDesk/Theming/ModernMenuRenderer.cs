using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NutDesk.Theming
{
    /// <summary>
    /// Flat, themed chrome for the main menu and the tray context menu. The stock renderer draws a gradient
    /// bar with 3D borders and, more importantly, ignores the dark palette entirely.
    /// </summary>
    internal class ModernMenuRenderer : ToolStripProfessionalRenderer
    {
        public ModernMenuRenderer() : base(new ModernColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(Theme.MenuBack))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // A single hairline under the menu bar reads as modern; a dropdown gets a full outline.
            using (var pen = new Pen(Theme.CardBorder))
            {
                if (e.ToolStrip is ToolStripDropDown)
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                }
                else
                {
                    e.Graphics.DrawLine(pen, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
                }
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripItem item = e.Item;
            var bounds = new Rectangle(Point.Empty, item.Size);

            if (!item.Enabled)
            {
                return;
            }

            // HasDropDownItems first: reading DropDown allocates one for every leaf item that is painted.
            var menuItem = item as ToolStripMenuItem;
            bool dropDownOpen = menuItem != null && menuItem.HasDropDownItems &&
                                menuItem.DropDown.Visible;

            if (item.Selected || dropDownOpen)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var padded = new Rectangle(bounds.X + 2, bounds.Y + 1, bounds.Width - 4, bounds.Height - 2);

                using (var path = Theme.RoundedRect(padded, 4))
                {
                    using (var brush = new SolidBrush(Theme.MenuHover))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.TextPrimary : Theme.TextSecondary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.CardBorder))
            {
                Rectangle b = e.Item.ContentRectangle;

                if (e.Vertical)
                {
                    e.Graphics.DrawLine(pen, b.Width / 2, b.Top, b.Width / 2, b.Bottom);
                }
                else
                {
                    e.Graphics.DrawLine(pen, b.Left + 4, b.Height / 2, b.Right - 4, b.Height / 2);
                }
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextPrimary;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // The stock renderer paints a lighter gutter down the left of every dropdown, which looks wrong on
            // a dark background. Fill it with the menu colour instead.
            using (var brush = new SolidBrush(Theme.MenuBack))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle rect = e.ImageRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var brush = new SolidBrush(Theme.Accent))
            {
                using (var pen = new Pen(brush, Math.Max(1.6F, rect.Height / 8.0F)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;

                    // A simple check mark, sized from the image rectangle so it scales with DPI.
                    float x = rect.Left + rect.Width * 0.22F;
                    float y = rect.Top + rect.Height * 0.52F;
                    e.Graphics.DrawLines(pen, new PointF[] {
                        new PointF(x, y),
                        new PointF(rect.Left + rect.Width * 0.42F, rect.Top + rect.Height * 0.74F),
                        new PointF(rect.Left + rect.Width * 0.8F, rect.Top + rect.Height * 0.28F)});
                }
            }
        }
    }

    /// <summary>
    /// Supplies the handful of colours ToolStripProfessionalRenderer still reaches for directly.
    /// </summary>
    internal class ModernColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.MenuHover;

        public override Color MenuItemBorder => Theme.MenuHover;

        public override Color MenuBorder => Theme.CardBorder;

        public override Color ToolStripDropDownBackground => Theme.MenuBack;

        public override Color ImageMarginGradientBegin => Theme.MenuBack;

        public override Color ImageMarginGradientMiddle => Theme.MenuBack;

        public override Color ImageMarginGradientEnd => Theme.MenuBack;

        public override Color CheckBackground => Theme.MenuHover;

        public override Color CheckSelectedBackground => Theme.MenuHover;
    }
}
