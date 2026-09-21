using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NutDesk.Theming
{
    /// <summary>
    /// The application's colour palette and the routines that apply it to existing WinForms controls.
    /// </summary>
    /// <remarks>
    /// The forms keep their designer/resx layout untouched - which matters, because the layout lives in the
    /// localized .resx files for seven cultures. Everything here is applied at runtime on top of that layout:
    /// typeface, colours, flat chrome and owner-drawn cards. That keeps the visual refresh independent of the
    /// translation files.
    /// </remarks>
    public static class Theme
    {
        // Static palette holder; never instantiated.

        public const string FONT_FAMILY = "Segoe UI";

        /// <summary>Body text size of the new typeface, in points.</summary>
        public const float FONT_SIZE = 9.0F;

        /// <summary>The typeface and size every form in this application was originally laid out against.</summary>
        private const string LEGACY_FONT_FAMILY = "Microsoft Sans Serif";
        private const float LEGACY_FONT_SIZE = 8.25F;

        /// <summary>
        /// Whether the dark palette is in use. Follows the Windows "app" theme by default.
        /// </summary>
        public static bool IsDark { get; set; }

        #region Palette

        // Light values first, dark second. Chosen to sit close to the Windows 11 system palette so the app does
        // not look foreign next to Settings or Explorer.

        public static Color WindowBack => IsDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);

        public static Color CardBack => IsDark ? Color.FromArgb(43, 43, 43) : Color.White;

        public static Color CardBorder => IsDark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(226, 226, 226);

        public static Color TextPrimary => IsDark ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);

        public static Color TextSecondary => IsDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);

        public static Color Accent => IsDark ? Color.FromArgb(76, 194, 255) : Color.FromArgb(15, 108, 189);

        /// <summary>The unfilled part of a gauge arc.</summary>
        public static Color Track => IsDark ? Color.FromArgb(62, 62, 62) : Color.FromArgb(230, 230, 230);

        public static Color Success => IsDark ? Color.FromArgb(108, 203, 95) : Color.FromArgb(16, 124, 16);

        public static Color Warning => IsDark ? Color.FromArgb(252, 225, 0) : Color.FromArgb(193, 132, 0);

        public static Color Danger => IsDark ? Color.FromArgb(255, 153, 164) : Color.FromArgb(196, 43, 28);

        public static Color MenuBack => IsDark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);

        public static Color MenuHover => IsDark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(234, 234, 234);

        public static Color ControlBack => IsDark ? Color.FromArgb(50, 50, 50) : Color.White;

        /// <summary>A badge colour with no signal attached to it - "we do not know yet".</summary>
        public static Color Neutral => IsDark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(214, 214, 214);

        #endregion

        #region Windows theme detection

        private const string PERSONALIZE_KEY =
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        /// <summary>The user's theme choice: follow Windows, or force one of the two palettes.</summary>
        public enum Preference
        {
            System = 0,
            Light = 1,
            Dark = 2
        }

        /// <summary>
        /// Decide which palette to use from the stored preference, consulting Windows only when asked to.
        /// </summary>
        public static bool ResolveIsDark(Preference choice)
        {
            switch (choice)
            {
                case Preference.Light:
                    return false;
                case Preference.Dark:
                    return true;
                default:
                    return WindowsPrefersDark();
            }
        }

        /// <summary>
        /// Read the user's current "app" theme preference from Windows. Defaults to light when the value is
        /// missing, which is the case on Windows versions that predate the setting.
        /// </summary>
        public static bool WindowsPrefersDark()
        {
            try
            {
                return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(PERSONALIZE_KEY, "AppsUseLightTheme", 1)) == 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // 20 on Windows 10 2004+ and Windows 11; 19 on the earlier Windows 10 builds that supported it at all.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY = 19;

        /// <summary>
        /// Ask the desktop window manager to draw this window's title bar in the dark style, so the caption does
        /// not stay bright white above a dark client area.
        /// </summary>
        public static void ApplyTitleBarTheme(Form frm)
        {
            if (frm == null || !frm.IsHandleCreated)
            {
                return;
            }

            int useDark = IsDark ? 1 : 0;

            try
            {
                if (DwmSetWindowAttribute(frm.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, 4) != 0)
                {
                    DwmSetWindowAttribute(frm.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY, ref useDark, 4);
                }
            }
            catch
            {
                // Pre-Windows-10: no immersive dark mode, and nothing to fall back to.
            }
        }

        #endregion

        #region Applying the theme

        /// <summary>
        /// Restyle a form and everything in it. Safe to call more than once.
        /// </summary>
        public static void Apply(Form frm)
        {
            if (frm == null)
            {
                return;
            }

            ApplyScaling(frm);

            frm.SuspendLayout();

            try
            {
                frm.BackColor = WindowBack;
                frm.ForeColor = TextPrimary;
                ConvertGroupBoxesToCards(frm);
                ApplyFont(frm);
                ApplyToControls(frm.Controls);
            }
            finally
            {
                frm.ResumeLayout(true);
            }

            EnsureClientFitsContent(frm);
            ApplyTitleBarTheme(frm);
            frm.Invalidate(true);
        }

        /// <summary>
        /// Grow the client area if the scaled layout no longer fits inside it.
        /// </summary>
        /// <remarks>
        /// Scaling a Form does not simply multiply its client size: WinForms reconciles the new bounds with the
        /// window chrome, which is already sized for the display and is not scaled again. The result can come out
        /// a little short, which clips whatever sits along the bottom or right edge. Measuring the controls and
        /// fitting the window to them is exact, and costs nothing when the size is already right.
        /// </remarks>
        private static readonly ConditionalWeakTable<Form, object> fittedForms = new ConditionalWeakTable<Form, object>();

        private static void EnsureClientFitsContent(Form frm)
        {
            // Once only: anchored controls move when the window grows, so re-measuring after a later Apply can
            // ratchet the window wider every time the theme is switched.
            if (fittedForms.TryGetValue(frm, out object alreadyFitted))
            {
                return;
            }
            fittedForms.Add(frm, new object());

            int contentRight = 0;
            int contentBottom = 0;

            foreach (Control ctrl in frm.Controls)
            {
                // Docked controls follow the client area rather than defining it.
                if (!ctrl.Visible || ctrl.Dock != DockStyle.None)
                {
                    continue;
                }

                contentRight = Math.Max(contentRight, ctrl.Right);
                contentBottom = Math.Max(contentBottom, ctrl.Bottom);
            }

            if (contentRight == 0 && contentBottom == 0)
            {
                return;
            }

            // Reuse the gutter the layout already leaves on the left and top edges.
            int gutter = LeadingGutter(frm);
            var needed = new Size(contentRight + gutter, contentBottom + gutter);

            // A tab strip that does not fit collapses into scroll arrows, which hides tabs entirely. Translated
            // captions are frequently longer than the English the layout was measured against, so the width the
            // tabs actually need has to be part of the calculation.
            needed.Width = Math.Max(needed.Width, RequiredWidthForTabStrips(frm, gutter));

            if (needed.Width <= frm.ClientSize.Width && needed.Height <= frm.ClientSize.Height)
            {
                return;
            }

            bool wasFixedSize = frm.MinimumSize != Size.Empty && frm.MinimumSize == frm.MaximumSize;

            frm.MinimumSize = Size.Empty;
            frm.MaximumSize = Size.Empty;
            frm.ClientSize = new Size(Math.Max(frm.ClientSize.Width, needed.Width),
                                      Math.Max(frm.ClientSize.Height, needed.Height));

            if (wasFixedSize)
            {
                frm.MinimumSize = frm.Size;
                frm.MaximumSize = frm.Size;
            }
        }

        /// <summary>
        /// How wide the form has to be for every tab caption to be visible, across all its tab controls.
        /// </summary>
        private static int RequiredWidthForTabStrips(Control root, int gutter)
        {
            int required = 0;

            foreach (TabControl tabs in FindAll<TabControl>(root))
            {
                if (tabs.TabCount == 0 || tabs.Alignment == TabAlignment.Left ||
                    tabs.Alignment == TabAlignment.Right)
                {
                    continue;
                }

                int strip = 0;

                foreach (TabPage page in tabs.TabPages)
                {
                    // The per-tab padding the control adds around the caption, plus a little slack.
                    strip += TextRenderer.MeasureText(page.Text, tabs.Font).Width + tabs.Padding.X * 2 + 10;
                }

                int offset = tabs.Left;

                Control parent = tabs.Parent;
                while (parent != null && !(parent is Form))
                {
                    offset += parent.Left;
                    parent = parent.Parent;
                }

                required = Math.Max(required, offset + strip + gutter);
            }

            return required;
        }

        /// <summary>Every control of the given type anywhere under <paramref name="root"/>.</summary>
        private static IEnumerable<T> FindAll<T>(Control root) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                var match = child as T;

                if (match != null)
                {
                    yield return match;
                }

                foreach (var nested in FindAll<T>(child))
                {
                    yield return nested;
                }
            }
        }

        /// <summary>
        /// The smallest left/top offset among the form's free-floating controls, used as the matching margin on
        /// the opposite edges.
        /// </summary>
        private static int LeadingGutter(Form frm)
        {
            int gutter = int.MaxValue;

            foreach (Control ctrl in frm.Controls)
            {
                if (!ctrl.Visible || ctrl.Dock != DockStyle.None)
                {
                    continue;
                }

                gutter = Math.Min(gutter, ctrl.Left);
            }

            if (gutter == int.MaxValue || gutter < 0)
            {
                return 0;
            }

            return gutter;
        }

        /// <summary>
        /// Tracks the forms already scaled, so a second <see cref="Apply"/> on the same form cannot scale it twice.
        /// A weak table means holding a form here never keeps it alive.
        /// </summary>
        private static readonly ConditionalWeakTable<Form, object> scaledForms = new ConditionalWeakTable<Form, object>();

        /// <summary>
        /// The uniform factor this form's layout was scaled by. Code that builds layout at runtime needs it to
        /// size padding, spacing and fixed heights in the same units as everything else.
        /// </summary>
        public static float ScaleOf(Form frm)
        {
            if (frm != null && scaledForms.TryGetValue(frm, out object stored) && stored is float scale)
            {
                return scale;
            }

            return frm == null ? 1.0F : ScaleFactorFor(frm);
        }

        /// <summary>
        /// The display scale alone, with no typeface component.
        /// </summary>
        /// <remarks>
        /// <see cref="ScaleOf"/> also carries the factor by which Segoe UI is taller than the typeface the
        /// designer layout was measured against. That is right for stretching that old layout, and wrong for
        /// layout written against the new typeface from the start - which would otherwise be inflated twice.
        /// </remarks>
        public static float DpiScaleOf(Form frm)
        {
            if (frm == null)
            {
                return 1.0F;
            }

            // Control.DeviceDpi reports 96 unless the application opts in to per-monitor DPI through app.config,
            // which this one does not. A device context reports the real figure for a system-DPI-aware process.
            try
            {
                if (frm.IsHandleCreated)
                {
                    using (var g = frm.CreateGraphics())
                    {
                        if (g.DpiX > 0)
                        {
                            return g.DpiX / 96.0F;
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                return frm.DeviceDpi / 96.0F;
            }
            catch
            {
                return 1.0F;
            }
        }

        /// <summary>Round a design-time pixel measurement to the current scale.</summary>
        public static int Px(Form frm, float designPixels)
        {
            return Convert.ToInt32(Math.Round(designPixels * ScaleOf(frm)));
        }

        /// <summary>
        /// Resize a form and its contents for the current display and for the new typeface.
        /// </summary>
        /// <remarks>
        /// The layout of every form here is absolute pixel positions recorded in the localized .resx files against
        /// Microsoft Sans Serif 8.25pt at 96 DPI. Two things now invalidate that: the application declares itself
        /// DPI aware (so Windows no longer stretches the bitmap), and the text is drawn in Segoe UI, which is
        /// noticeably taller at the same point size. Both are folded into one uniform factor here rather than
        /// left to WinForms auto-scaling, which cannot see the font substitution that happens at runtime and
        /// would scale width and height by different amounts.
        /// </remarks>
        private static void ApplyScaling(Form frm)
        {
            if (scaledForms.TryGetValue(frm, out object alreadyScaled))
            {
                return;
            }
            float factor = ScaleFactorFor(frm);
            scaledForms.Add(frm, factor);

            if (Math.Abs(factor - 1.0F) < 0.01F)
            {
                return;
            }

            // A fixed-size window pins MinimumSize to MaximumSize. Lift that while scaling and restore it
            // afterwards from the freshly scaled size, otherwise the constraint clips the grown layout.
            // A MinimumSize without a matching MaximumSize is a different thing - a genuine floor - and has to
            // be scaled and put back, not silently dropped.
            bool wasFixedSize = frm.MinimumSize != Size.Empty && frm.MinimumSize == frm.MaximumSize;
            Size originalMinimum = frm.MinimumSize;
            Size originalClient = frm.ClientSize;

            frm.MinimumSize = Size.Empty;
            frm.MaximumSize = Size.Empty;

            frm.Scale(new SizeF(factor, factor));

            // Set the client area explicitly: scaling the window rectangle would fold in the non-client borders,
            // which are already sized for the display and must not be scaled a second time.
            frm.ClientSize = new Size(Convert.ToInt32(Math.Round(originalClient.Width * factor)),
                                      Convert.ToInt32(Math.Round(originalClient.Height * factor)));

            if (wasFixedSize)
            {
                frm.MinimumSize = frm.Size;
                frm.MaximumSize = frm.Size;
            }
            else if (originalMinimum != Size.Empty)
            {
                frm.MinimumSize = new Size(Convert.ToInt32(Math.Round(originalMinimum.Width * factor)),
                                           Convert.ToInt32(Math.Round(originalMinimum.Height * factor)));
            }
        }

        /// <summary>
        /// Swap every GroupBox for a <see cref="CardPanel"/>, keeping its bounds, children and tab order.
        /// </summary>
        private static void ConvertGroupBoxesToCards(Control parent)
        {
            var boxes = parent.Controls.OfType<GroupBox>().ToList();

            foreach (GroupBox box in boxes)
            {
                // Handle nesting from the inside out.
                ConvertGroupBoxesToCards(box);

                var card = new CardPanel
                {
                    Name = box.Name,
                    Bounds = box.Bounds,
                    Anchor = box.Anchor,
                    Dock = box.Dock,
                    TabIndex = box.TabIndex,
                    Caption = box.Text,
                    Font = box.Font,
                    Visible = box.Visible,
                    Enabled = box.Enabled
                };

                var children = box.Controls.Cast<Control>().ToArray();
                box.Controls.Clear();
                card.Controls.AddRange(children);

                int index = parent.Controls.GetChildIndex(box);
                parent.Controls.Remove(box);
                parent.Controls.Add(card);
                parent.Controls.SetChildIndex(card, index);

                // Size constraints are copied after the panel is parented, so layout does not fight them.
                card.MinimumSize = box.MinimumSize;
                card.MaximumSize = box.MaximumSize;

                box.Dispose();
            }

            // Recurse into whatever is left, so group boxes nested in panels or tab pages are converted too.
            foreach (Control child in parent.Controls)
            {
                if (!(child is CardPanel))
                {
                    ConvertGroupBoxesToCards(child);
                }
            }
        }

        /// <summary>
        /// The uniform scale factor: display DPI relative to 96, multiplied by how much taller the new typeface
        /// is than the one the layout was designed for. Both fonts are measured at the current DPI, so the ratio
        /// between them is the typeface contribution alone.
        /// </summary>
        private static float ScaleFactorFor(Form frm)
        {
            float dpiFactor = 1.0F;

            try
            {
                dpiFactor = frm.DeviceDpi / 96.0F;
            }
            catch
            {
                // DeviceDpi needs .NET 4.7; fall back to no DPI component.
            }

            float fontFactor = 1.0F;

            try
            {
                using (var legacy = new Font(LEGACY_FONT_FAMILY, LEGACY_FONT_SIZE))
                {
                    using (var modern = new Font(FONT_FAMILY, FONT_SIZE))
                    {
                        if (legacy.Height > 0)
                        {
                            fontFactor = modern.Height / (float)legacy.Height;
                        }
                    }
                }
            }
            catch
            {
                // Either family missing: keep the DPI component only.
            }

            return dpiFactor * fontFactor;
        }

        /// <summary>
        /// Switch a control tree over to the modern typeface. Point sizes are converted in proportion, so a
        /// control that was emphasised with a larger font stays proportionally larger.
        /// </summary>
        private static readonly bool modernFontAvailable = IsFontInstalled(FONT_FAMILY);

        private static bool IsFontInstalled(string family)
        {
            try
            {
                using (var probe = new FontFamily(family))
                {
                    return string.Equals(probe.Name, family, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // FontFamily throws when the family is not installed.
                return false;
            }
        }

        private static void ApplyFont(Control ctrl)
        {
            // Without this guard a missing Segoe UI would leave every control reporting some other family, so
            // each Apply would scale the point size up again.
            if (!modernFontAvailable)
            {
                return;
            }

            if (ctrl.Font != null &&
                !string.Equals(ctrl.Font.FontFamily.Name, FONT_FAMILY, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ctrl.Font = new Font(FONT_FAMILY,
                                         ctrl.Font.SizeInPoints * (FONT_SIZE / LEGACY_FONT_SIZE),
                                         ctrl.Font.Style);
                }
                catch
                {
                    // Segoe UI is present on every supported Windows version, but never let a font swap be fatal.
                }
            }

            foreach (Control child in ctrl.Controls)
            {
                ApplyFont(child);
            }
        }

        private static void ApplyToControls(Control.ControlCollection controls)
        {
            foreach (Control ctrl in controls)
            {
                ApplyToControl(ctrl);
            }
        }

        private static void ApplyToControl(Control ctrl)
        {
            switch (ctrl)
            {
                case MenuStrip menu:
                    menu.Renderer = new ModernMenuRenderer();
                    menu.BackColor = MenuBack;
                    menu.ForeColor = TextPrimary;
                    // The designer pins the menu to its own width, leaving a gap to the right of the last item.
                    menu.MaximumSize = Size.Empty;
                    menu.Dock = DockStyle.Top;
                    break;

                case ContextMenuStrip contextMenu:
                    contextMenu.Renderer = new ModernMenuRenderer();
                    break;

                case CardPanel card:
                    // Refreshed on every Apply, not just at construction: children inherit this as their
                    // ambient background, so a stale value leaves dark labels on a light card after a theme
                    // switch.
                    card.BackColor = CardBack;
                    card.ForeColor = TextPrimary;
                    break;

                case Button btn:
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = CardBorder;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.MouseOverBackColor = MenuHover;
                    btn.BackColor = ControlBack;
                    btn.ForeColor = TextPrimary;
                    btn.UseVisualStyleBackColor = false;
                    break;

                case TextBox tb:
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    tb.BackColor = ControlBack;
                    tb.ForeColor = TextPrimary;
                    break;

                case NumericUpDown nud:
                    nud.BorderStyle = BorderStyle.FixedSingle;
                    nud.BackColor = ControlBack;
                    nud.ForeColor = TextPrimary;
                    break;

                case ComboBox cb:
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = ControlBack;
                    cb.ForeColor = TextPrimary;
                    // Owner-drawing covers both the closed display area and the drop-down list, which otherwise
                    // keep the system colours.
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.DrawItem -= PaintComboItem;
                    cb.DrawItem += PaintComboItem;
                    break;

                case CheckBox chk:
                    // FlatStyle.Flat makes the box follow BackColor/ForeColor; the themed default renders a
                    // bright white square that is jarring on a dark form. BackColor stays transparent so only
                    // the box picks up the colour, not the whole caption strip.
                    chk.FlatStyle = FlatStyle.Flat;
                    chk.FlatAppearance.BorderColor = CardBorder;
                    chk.FlatAppearance.CheckedBackColor = Accent;
                    // Matching the parent keeps the caption strip invisible while the box itself gets the dark
                    // fill; the border is what makes it readable.
                    chk.BackColor = chk.Parent != null ? chk.Parent.BackColor : ControlBack;
                    chk.ForeColor = TextPrimary;
                    chk.UseVisualStyleBackColor = false;
                    break;

                case RadioButton rb:
                    rb.FlatStyle = FlatStyle.Flat;
                    rb.FlatAppearance.BorderColor = CardBorder;
                    rb.FlatAppearance.CheckedBackColor = Accent;
                    rb.BackColor = rb.Parent != null ? rb.Parent.BackColor : ControlBack;
                    rb.ForeColor = TextPrimary;
                    rb.UseVisualStyleBackColor = false;
                    break;

                case TreeView tv:
                    tv.BorderStyle = BorderStyle.FixedSingle;
                    tv.BackColor = ControlBack;
                    tv.ForeColor = TextPrimary;
                    tv.LineColor = CardBorder;
                    break;

                case ListBox lb:
                    lb.BorderStyle = BorderStyle.FixedSingle;
                    lb.BackColor = ControlBack;
                    lb.ForeColor = TextPrimary;
                    break;

                case TabControl tabs:
                    // The tab headers are drawn by the OS theme and ignore BackColor entirely, so they have to
                    // be owner-drawn to follow the palette.
                    tabs.BackColor = WindowBack;
                    tabs.ForeColor = TextPrimary;
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tabs.DrawItem -= PaintTabHeader;
                    tabs.DrawItem += PaintTabHeader;
                    break;

                case TabPage page:
                    // The page reads as the card the tabs sit on.
                    page.BackColor = CardBack;
                    page.ForeColor = TextPrimary;
                    break;

                case LinkLabel link:
                    // The default link blue is close to unreadable on a dark background.
                    link.ForeColor = TextPrimary;
                    link.LinkColor = Accent;
                    link.ActiveLinkColor = Accent;
                    link.VisitedLinkColor = Accent;
                    link.DisabledLinkColor = TextSecondary;
                    break;

                case Label label:
                    // Labels used as coloured status lamps set their own BackColor from code; leave those alone.
                    label.ForeColor = TextPrimary;
                    break;

                case PictureBox picture:
                    // Sits on a card; anything else shows as a coloured block around the image.
                    picture.BackColor = picture.Parent != null ? picture.Parent.BackColor : CardBack;
                    break;

                case Panel panel:
                    // Layout panels nested inside a card must not punch a window-coloured hole through it.
                    panel.BackColor = panel.Parent is CardPanel ||
                                      (panel.Parent != null && panel.Parent.Parent is CardPanel)
                                      ? CardBack : WindowBack;
                    panel.ForeColor = TextPrimary;
                    break;
            }

            foreach (Control child in ctrl.Controls)
            {
                ApplyToControl(child);
            }
        }

        /// <summary>
        /// Draws one tab header. The selected tab gets the card colour and an accent underline; the rest sit
        /// flat on the window background, which is the current Windows convention.
        /// </summary>
        private static void PaintTabHeader(object sender, DrawItemEventArgs e)
        {
            var tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count)
            {
                return;
            }

            TabPage page = tabs.TabPages[e.Index];
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Graphics g = e.Graphics;

            // The strip to the right of the last tab is painted by the control in the system colour, which stays
            // bright on a dark form. Cover it while drawing the last tab.
            if (e.Index == tabs.TabPages.Count - 1)
            {
                using (var strip = new SolidBrush(WindowBack))
                {
                    g.FillRectangle(strip, e.Bounds.Right, e.Bounds.Top - 4,
                                    Math.Max(0, tabs.Width - e.Bounds.Right), e.Bounds.Height + 8);
                }
            }

            using (var back = new SolidBrush(isSelected ? CardBack : WindowBack))
            {
                g.FillRectangle(back, e.Bounds);
            }

            if (isSelected)
            {
                using (var underline = new SolidBrush(Accent))
                {
                    int thickness = Math.Max(2, e.Bounds.Height / 12);
                    g.FillRectangle(underline, e.Bounds.Left + 2, e.Bounds.Bottom - thickness,
                                    e.Bounds.Width - 4, thickness);
                }
            }

            // NoPadding, and a slightly widened rectangle: TextRenderer's default padding costs several pixels
            // per side, which is enough to truncate the longer captions.
            Rectangle textBounds = e.Bounds;
            textBounds.Inflate(2, 0);

            TextRenderer.DrawText(g, page.Text, tabs.Font, textBounds,
                                  isSelected ? TextPrimary : TextSecondary,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                  TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        /// <summary>
        /// Draws one combo box row, and the closed control's display area, in the palette colours.
        /// </summary>
        private static void PaintComboItem(object sender, DrawItemEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null)
            {
                return;
            }

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = isSelected && (e.State & DrawItemState.ComboBoxEdit) == 0 ? MenuHover : ControlBack;

            using (var back = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(back, e.Bounds);
            }

            if (e.Index < 0 || e.Index >= cb.Items.Count)
            {
                return;
            }

            string text = Convert.ToString(cb.Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, TextPrimary,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                  TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>
        /// Build a rounded-rectangle path, degenerating to a plain rectangle when the radius does not fit.
        /// </summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0 || bounds.Width <= radius * 2 || bounds.Height <= radius * 2)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion
    }

    /// <summary>
    /// Lets the theme turn on double buffering for controls that do not expose it, so the owner-drawn cards do
    /// not flicker while the gauges repaint once a second.
    /// </summary>
    internal static class ControlExtensions
    {
        public static void SetStyle_DoubleBuffer(this Control ctrl)
        {
            try
            {
                PropertyInfo prop = ctrl.GetType().GetProperty("DoubleBuffered",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                prop?.SetValue(ctrl, true, null);
            }
            catch
            {
                // Purely cosmetic - a control that refuses double buffering just flickers a little.
            }
        }
    }
}
