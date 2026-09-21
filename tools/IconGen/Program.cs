// NutDesk icon generator.
//
// Every icon and picture NutDesk ships is drawn here, from code, so the whole set can be regenerated or restyled in
// one pass instead of being retouched bitmap by bitmap. WPF does the rendering (DrawingVisual + RenderTargetBitmap):
// it gives vector paths with proper antialiasing and nothing has to be installed. The .ico container is written by
// hand because no encoder in the framework produces multi-frame icons.
//
// Usage: IconGen.exe <repository root> [--sheet <contact sheet .png>]
// See README.md next to this file.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGen
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string root = null;
            string sheet = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--sheet" && i + 1 < args.Length)
                {
                    sheet = Path.GetFullPath(args[++i]);
                }
                else if (root == null)
                {
                    root = Path.GetFullPath(args[i]);
                }
            }

            if (root == null)
            {
                Console.Error.WriteLine("Usage: IconGen.exe <repository root> [--sheet <contact sheet .png>]");
                return 2;
            }

            string resources = Path.Combine(root, "src", "NutDesk", "Resources");
            if (!Directory.Exists(resources))
            {
                // Refuse rather than scatter files: a wrong argument would otherwise create a stray tree.
                Console.Error.WriteLine("Not a NutDesk checkout (missing " + resources + ").");
                return 2;
            }
            string assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(assets);

            // Status icons: the tray and the main window share them, hence the window sizes next to the tray ones.
            foreach (var state in StatusState.All())
            {
                var frames = StatusArt.Sizes.Select(s => Raster.Render(s, s, dc => StatusArt.Draw(dc, s, state))).ToList();
                Ico.Write(Path.Combine(resources, state.Index + ".ico"), frames);
                Console.WriteLine(state.Index + ".ico  " + state.Name);
            }

            // Application icon (exe, toast, dialogs) and its updater variant.
            Ico.Write(Path.Combine(resources, "NutDesk.ico"),
                      BrandArt.IconSizes.Select(s => Raster.Render(s, s, dc => BrandArt.DrawTile(dc, s, false))).ToList());
            Ico.Write(Path.Combine(resources, "NutDesk-Updater.ico"),
                      BrandArt.IconSizes.Select(s => Raster.Render(s, s, dc => BrandArt.DrawTile(dc, s, true))).ToList());
            Console.WriteLine("NutDesk.ico, NutDesk-Updater.ico");

            // Log file icons, kept at the frame sizes the previous set had.
            Ico.Write(Path.Combine(resources, "2001_ViewLogFile.ico"),
                      UiArt.LogIconSizes.Select(s => Raster.Render(s, s, dc => UiArt.ViewLog(dc, s))).ToList());
            Ico.Write(Path.Combine(resources, "2002_Delete_LogFile.ico"),
                      UiArt.LogIconSizes.Select(s => Raster.Render(s, s, dc => UiArt.DeleteLog(dc, s))).ToList());
            Console.WriteLine("2001_ViewLogFile.ico, 2002_Delete_LogFile.ico");

            // Bitmaps used by the WinForms dialogs and menus, each at the pixel size its control was laid out for.
            foreach (var picture in UiArt.Pictures)
            {
                var bmp = Raster.Render(picture.Size, picture.Size, dc => picture.Draw(dc, picture.Size));
                Raster.SavePng(bmp, Path.Combine(resources, picture.File));
                Console.WriteLine(picture.File);
            }

            // README and repository artwork.
            Raster.SavePng(Raster.Render(256, 256, dc => BrandArt.DrawTile(dc, 256, false)), Path.Combine(assets, "logo.png"));
            Raster.SavePng(Raster.Render(512, 512, dc => BrandArt.DrawTile(dc, 512, false)), Path.Combine(assets, "logo-512.png"));
            Raster.SavePng(Raster.Render(1280, 640, BrandArt.DrawSocialPreview), Path.Combine(assets, "social-preview.png"));
            Console.WriteLine("assets/logo.png, assets/logo-512.png, assets/social-preview.png");

            if (sheet != null)
            {
                Sheet.WriteAll(sheet);
                Console.WriteLine("Contact sheets: " + sheet);
            }
            return 0;
        }
    }

    /// <summary>The two ink sets of the status icons: one for a light taskbar, one for a dark one.</summary>
    internal sealed class Ink
    {
        public Color Glyph;
        public Color Amber;
        public Color Red;
        public Color Mains;
        public Color Accent;

        // Glyph colours follow the app's own text colours (Theme.TextPrimary); amber, red, green and the accent are
        // the app's Warning / Danger / Success / Accent, adjusted where a tiny tray fill needed more contrast.
        public static readonly Ink Light = new Ink
        {
            Glyph = Hex.C("#1B1B1B"),
            Amber = Hex.C("#B87A00"),
            Red = Hex.C("#C42B1C"),
            Mains = Hex.C("#107C10"),
            Accent = Hex.C("#0F6CBD"),
        };

        public static readonly Ink Dark = new Ink
        {
            Glyph = Hex.C("#F2F2F2"),
            Amber = Hex.C("#FFC83D"),
            Red = Hex.C("#FF6A5C"),
            Mains = Hex.C("#6CCB5F"),
            Accent = Hex.C("#4CC2FF"),
        };
    }

    internal static class Hex
    {
        public static Color C(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        public static Color A(Color c, double alpha)
        {
            return Color.FromArgb((byte)Math.Round(255 * alpha), c.R, c.G, c.B);
        }

        public static SolidColorBrush B(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }

    /// <summary>
    /// One status icon: the value is the AppIconIdx combination MainForm asks for (IDX_OFFSET + flags).
    /// </summary>
    internal sealed class StatusState
    {
        public int Level = -1;     // 0..4 = IDX_BATT_0 .. IDX_BATT_100, -1 when the icon shows no charge
        public bool OnLine;
        public bool Offline;
        public bool Retry;
        public bool Dark;

        public int Index
        {
            get
            {
                int idx = 1024 | (Dark ? 64 : 0);
                if (Retry) return idx | 256;
                if (Offline) return idx | 128;
                return idx | (1 << Level) | (OnLine ? 32 : 0);
            }
        }

        public string Name
        {
            get
            {
                string what = Retry ? "retry" : Offline ? "offline" : (OnLine ? "mains " : "battery ") + (Level * 25) + "%";
                return what + (Dark ? " (dark)" : " (light)");
            }
        }

        /// <summary>The 24 combinations MainForm can request: 5 levels x mains/battery, offline, retry; each light and dark.</summary>
        public static IEnumerable<StatusState> All()
        {
            foreach (bool dark in new[] { false, true })
            {
                foreach (bool online in new[] { false, true })
                {
                    for (int level = 0; level < 5; level++)
                    {
                        yield return new StatusState { Level = level, OnLine = online, Dark = dark };
                    }
                }
                yield return new StatusState { Offline = true, Dark = dark };
                yield return new StatusState { Retry = true, Dark = dark };
            }
        }
    }

    /// <summary>
    /// The battery status glyph. Tray sizes are laid out by hand on the pixel grid (integer edges, strokes centred
    /// on half pixels) because a notification-area icon is shown 1:1 and any resampling turns it to mush; the
    /// window sizes scale the 32 px layout freely.
    /// </summary>
    internal static class StatusArt
    {
        public static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

        // Share of the inner height filled at 0/25/50/75/100 %. 0 keeps a sliver so "empty" still reads as a battery
        // with something (red) left in it; the others are nudged so every step differs by whole pixels at 16 px.
        private static readonly double[] LevelFraction = { 0.1, 0.3, 0.55, 0.78, 1.0 };

        private struct Geo
        {
            public double Stroke, BodyW, BodyH, TermW, TermH, Gap, Radius, MainsX, BoltGap, Margin;
            public bool Snap;
        }

        private static Geo For(int s)
        {
            switch (s)
            {
                case 16: return new Geo { Stroke = 1, BodyW = 9, BodyH = 13, TermW = 5, TermH = 2, Gap = 1, Radius = 1.5, MainsX = 0, BoltGap = 1, Margin = 0, Snap = true };
                case 20: return new Geo { Stroke = 1, BodyW = 11, BodyH = 16, TermW = 5, TermH = 2, Gap = 1, Radius = 2, MainsX = 0, BoltGap = 1, Margin = 0, Snap = true };
                case 24: return new Geo { Stroke = 2, BodyW = 13, BodyH = 19, TermW = 5, TermH = 2, Gap = 1, Radius = 2.5, MainsX = 0, BoltGap = 2, Margin = 0, Snap = true };
                case 32: return new Geo { Stroke = 2, BodyW = 17, BodyH = 26, TermW = 7, TermH = 3, Gap = 2, Radius = 3, MainsX = 1, BoltGap = 2, Margin = 1, Snap = true };
                default:
                    double k = s / 32.0;
                    var g = For(32);
                    return new Geo
                    {
                        Stroke = g.Stroke * k, BodyW = g.BodyW * k, BodyH = g.BodyH * k, TermW = g.TermW * k, TermH = g.TermH * k,
                        Gap = g.Gap * k, Radius = g.Radius * k * 1.2, MainsX = g.MainsX * k, BoltGap = g.BoltGap * k,
                        Margin = g.Margin * k, Snap = false
                    };
            }
        }

        public static void Draw(DrawingContext dc, int s, StatusState st)
        {
            var ink = st.Dark ? Ink.Dark : Ink.Light;
            var g = For(s);

            if (st.Retry)
            {
                DrawRetry(dc, s, g, ink);
                return;
            }

            double totalH = g.TermH + g.BodyH;
            double y0 = g.Snap ? Math.Floor((s - totalH + 1) / 2) : (s - totalH) / 2;
            bool mains = st.OnLine && !st.Offline;
            double x0 = mains ? g.MainsX : (g.Snap ? Math.Floor((s - g.BodyW) / 2) : (s - g.BodyW) / 2);

            if (st.Offline)
            {
                // Dimmed, empty battery struck through: "no data", deliberately unlike any charge level.
                double inset = Math.Round(s * 0.125);
                var p1 = new Point(inset, inset);
                var p2 = new Point(s - inset, s - inset);
                double slashW = Math.Max(1.5, g.Stroke * 1.25);
                double gap = Math.Max(1, g.Stroke * 0.75);
                var slash = new LineGeometry(p1, p2);
                var cut = slash.GetWidenedPathGeometry(new Pen(Brushes.Black, slashW + 2 * gap) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
                dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, s, s)), cut));
                DrawBattery(dc, g, x0, y0, Hex.A(ink.Glyph, 0.5), Colors.Transparent, 0);
                dc.Pop();
                dc.DrawLine(new Pen(Hex.B(ink.Glyph), slashW) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, p1, p2);
                return;
            }

            Color fill = ink.Glyph;
            if (!st.OnLine && st.Level == 0) fill = ink.Red;
            else if (!st.OnLine && st.Level == 1) fill = ink.Amber;

            double inner = g.BodyH - 2 * (g.Stroke + g.Gap);
            double f = LevelFraction[st.Level];
            double h = g.Snap ? Math.Max(g.Stroke, Math.Floor(inner * f + 0.5)) : Math.Max(inner * 0.12, inner * f);
            DrawBattery(dc, g, x0, y0, ink.Glyph, fill, h);

            if (mains)
            {
                double bx = x0 + g.BodyW + g.BoltGap;
                double bw = s - g.Margin - bx;
                double bh = g.Snap ? Math.Round(bw * 1.75) : bw * 1.75;
                double yb = y0 + g.TermH;
                double by = g.Snap ? Math.Floor(yb + (g.BodyH - bh) / 2) : yb + (g.BodyH - bh) / 2;
                // Tray sizes take the pixel-friendly block bolt; the window sizes have room for the brand's slanted one.
                var box = new Rect(bx, by, bw, bh);
                dc.DrawGeometry(Hex.B(ink.Mains), null, g.Snap ? Shapes.BlockBolt(box) : Shapes.Bolt(box));
            }
        }

        private static void DrawBattery(DrawingContext dc, Geo g, double x0, double y0, Color stroke, Color fill, double fillH)
        {
            var strokeBrush = Hex.B(stroke);
            double yb = y0 + g.TermH;
            double tx = x0 + (g.BodyW - g.TermW) / 2;
            if (g.Snap)
            {
                dc.DrawRectangle(strokeBrush, null, new Rect(tx, y0, g.TermW, g.TermH));
            }
            else
            {
                // Tucked under the body stroke so only the rounded top of the terminal shows.
                double r = g.TermH * 0.45;
                dc.DrawRoundedRectangle(strokeBrush, null, new Rect(tx, y0, g.TermW, g.TermH + g.Stroke), r, r);
            }

            var pen = new Pen(strokeBrush, g.Stroke);
            dc.DrawRoundedRectangle(null, pen, new Rect(x0 + g.Stroke / 2, yb + g.Stroke / 2, g.BodyW - g.Stroke, g.BodyH - g.Stroke), g.Radius, g.Radius);

            if (fillH > 0)
            {
                double left = x0 + g.Stroke + g.Gap;
                double width = g.BodyW - 2 * (g.Stroke + g.Gap);
                double bottom = yb + g.BodyH - g.Stroke - g.Gap;
                double fr = g.Snap ? (g.Stroke > 1 ? 0.5 : 0) : g.Radius * 0.45;
                dc.DrawRoundedRectangle(Hex.B(fill), null, new Rect(left, bottom - fillH, width, fillH), fr, fr);
            }
        }

        private static void DrawRetry(DrawingContext dc, int s, Geo g, Ink ink)
        {
            // A clockwise "refresh" arrow in the accent colour: the one state that is in progress rather than a reading.
            double w = Math.Max(1.5, s * 0.095);
            double r = s * 0.31;
            var c = new Point(s / 2.0, s / 2.0);
            dc.DrawGeometry(Hex.B(ink.Accent), null, Shapes.ClockwiseArrow(c, r, w, 50, 325));
        }
    }

    /// <summary>Geometry shared by several pictures.</summary>
    internal static class Shapes
    {
        /// <summary>The lightning bolt, point-symmetric so it balances in any box.</summary>
        public static Geometry Bolt(Rect box)
        {
            var pts = new[]
            {
                new Point(0.66, 0.00), new Point(0.04, 0.60), new Point(0.50, 0.60),
                new Point(0.34, 1.00), new Point(0.96, 0.40), new Point(0.50, 0.40),
            };
            return Polygon(pts.Select(p => new Point(box.X + p.X * box.Width, box.Y + p.Y * box.Height)));
        }

        /// <summary>
        /// A squarer bolt with a vertical back and a flat top. Those edges land on whole pixels, which keeps the badge
        /// sharp at 16 px where the slanted brand bolt would be a grey smudge.
        /// </summary>
        public static Geometry BlockBolt(Rect box)
        {
            var pts = new[]
            {
                new Point(0.0, 0.0), new Point(0.0, 0.56), new Point(0.34, 0.56), new Point(0.34, 1.0),
                new Point(1.0, 0.4), new Point(0.62, 0.4), new Point(1.0, 0.0),
            };
            return Polygon(pts.Select(p => new Point(box.X + p.X * box.Width, box.Y + p.Y * box.Height)));
        }

        public static Geometry Polygon(IEnumerable<Point> points)
        {
            var list = points.ToList();
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(list[0], true, true);
                ctx.PolyLineTo(list.Skip(1).ToList(), true, true);
            }
            geo.Freeze();
            return geo;
        }

        /// <summary>Flat-topped hexagon (the shape of a hardware nut seen from above); halfHeight is centre to flat.</summary>
        public static Geometry Hexagon(Point c, double halfHeight)
        {
            double r = halfHeight * 2 / Math.Sqrt(3);
            return Polygon(Enumerable.Range(0, 6).Select(i =>
            {
                double a = Math.PI / 3 * i;
                return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
            }));
        }

        private static Point OnCircle(Point c, double r, double degreesFromTop)
        {
            double a = degreesFromTop * Math.PI / 180;
            return new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
        }

        /// <summary>
        /// A clockwise circular arrow as one filled geometry: an arc stroke from startDeg to endDeg (degrees clockwise
        /// from 12 o'clock) with an arrowhead at its end.
        /// </summary>
        public static Geometry ClockwiseArrow(Point c, double r, double w, double startDeg, double endDeg)
        {
            double head = w * 1.75;
            // Stop the arc a little early so its flat cap disappears inside the arrowhead.
            double arcEnd = endDeg - (w * 0.4 / r) * 180 / Math.PI;
            var arc = new StreamGeometry();
            using (var ctx = arc.Open())
            {
                ctx.BeginFigure(OnCircle(c, r, startDeg), false, false);
                ctx.ArcTo(OnCircle(c, r, arcEnd), new Size(r, r), 0, arcEnd - startDeg > 180, SweepDirection.Clockwise, true, true);
            }
            var body = arc.GetWidenedPathGeometry(new Pen(Brushes.Black, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Flat });

            double a = endDeg * Math.PI / 180;
            var e = OnCircle(c, r, endDeg);
            var tangent = new Vector(Math.Cos(a), Math.Sin(a));
            var normal = new Vector(Math.Sin(a), -Math.Cos(a));
            var tip = e + tangent * head;
            var arrow = Polygon(new[] { e + normal * head, tip, e - normal * head, e - tangent * (w * 0.3) });
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            g.Children.Add(body);
            g.Children.Add(arrow);
            return g.GetOutlinedPathGeometry();
        }

        public static Geometry Exclude(Geometry a, Geometry b)
        {
            return new CombinedGeometry(GeometryCombineMode.Exclude, a, b);
        }

        public static Geometry Widen(Geometry g, double width)
        {
            return g.GetWidenedPathGeometry(new Pen(Brushes.Black, width) { LineJoin = PenLineJoin.Round });
        }
    }

    /// <summary>The NutDesk mark: a hex nut pierced by a lightning bolt, on a blue-to-teal tile.</summary>
    internal static class BrandArt
    {
        public static readonly int[] IconSizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

        // Deep blue into teal; the middle stop is the light theme accent (#0F6CBD) so the tile sits with the app.
        private static Brush TileBrush()
        {
            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Hex.C("#0A3A8A"), 0.0),
                    new GradientStop(Hex.C("#0F6CBD"), 0.5),
                    new GradientStop(Hex.C("#16B0C4"), 1.0),
                }
            };
            b.Freeze();
            return b;
        }

        public static void DrawTile(DrawingContext dc, double s, bool updateBadge)
        {
            bool small = s <= 32;
            double inset = s <= 20 ? 0 : Math.Round(s / 16);
            var tile = new Rect(inset, inset, s - 2 * inset, s - 2 * inset);
            double radius = tile.Width * 0.23;
            Geometry tileGeo = new RectangleGeometry(tile, radius, radius);

            Point badgeC = new Point(tile.Right - tile.Width * 0.2, tile.Bottom - tile.Width * 0.2);
            double badgeR = tile.Width * (small ? 0.27 : 0.23);
            double badgeGap = Math.Max(1, s * 0.035);
            if (updateBadge)
            {
                tileGeo = Shapes.Exclude(tileGeo, new EllipseGeometry(badgeC, badgeR + badgeGap, badgeR + badgeGap));
            }

            dc.DrawGeometry(TileBrush(), null, tileGeo);
            if (s > 24)
            {
                // A faint top light gives the flat tile the slight depth of the Windows 11 app icons.
                var sheen = new LinearGradientBrush(Hex.A(Colors.White, 0.18), Hex.A(Colors.White, 0), new Point(0, 0), new Point(0, 0.6));
                sheen.Freeze();
                dc.DrawGeometry(sheen, null, tileGeo);
            }

            var c = new Point(tile.X + tile.Width / 2, tile.Y + tile.Height / 2);
            Geometry mark = Mark(c, tile.Height, small, out Geometry bolt);
            if (updateBadge)
            {
                var notch = new EllipseGeometry(badgeC, badgeR + badgeGap, badgeR + badgeGap);
                mark = Shapes.Exclude(mark, notch);
                if (bolt != null) bolt = Shapes.Exclude(bolt, notch);
            }

            if (s >= 48)
            {
                // Soft shadow under the mark, faked with a few offset translucent copies (DrawingContext has no blur).
                for (int i = 1; i <= 3; i++)
                {
                    dc.PushTransform(new TranslateTransform(0, s * 0.006 * i));
                    dc.DrawGeometry(Hex.B(Hex.A(Hex.C("#021A40"), 0.12)), null, mark);
                    if (bolt != null) dc.DrawGeometry(Hex.B(Hex.A(Hex.C("#021A40"), 0.12)), null, bolt);
                    dc.Pop();
                }
            }
            dc.DrawGeometry(Brushes.White, null, mark);
            if (bolt != null) dc.DrawGeometry(Brushes.White, null, bolt);

            if (updateBadge)
            {
                dc.DrawEllipse(Hex.B(Hex.C("#107C10")), null, badgeC, badgeR, badgeR);
                dc.DrawGeometry(Brushes.White, null, DownArrow(badgeC, badgeR));
            }
        }

        /// <summary>
        /// The mark in white. Small sizes are a solid nut with the bolt cut out of it (a hole and a bolt inside it
        /// would be a couple of grey pixels at 16 px); from 40 px up the nut has its round hole and the bolt passes
        /// through it, separated from the nut by a thin gap.
        /// </summary>
        public static Geometry Mark(Point c, double extent, bool small, out Geometry bolt)
        {
            if (small)
            {
                double hh = Math.Round(extent * 0.34);
                var hex = Shapes.Hexagon(c, hh);
                double bh = Math.Round(hh * 1.6);
                double bw = Math.Round(bh * 0.6);
                var cutBolt = Shapes.BlockBolt(new Rect(Math.Round(c.X - bw / 2), c.Y - bh / 2, bw, bh));
                bolt = null;
                return Shapes.Exclude(hex, cutBolt);
            }
            else
            {
                double hh = extent * 0.3;
                var hex = Shapes.Hexagon(c, hh);
                double holeR = hh * 2 / Math.Sqrt(3) * 0.5;
                double bh = hh * 1.62;
                double bw = bh * 0.64;
                bolt = Shapes.Bolt(new Rect(c.X - bw / 2, c.Y - bh / 2, bw, bh));
                var ring = Shapes.Exclude(hex, new EllipseGeometry(c, holeR, holeR));
                return Shapes.Exclude(ring, Shapes.Widen(bolt, extent * 0.055));
            }
        }

        private static Geometry DownArrow(Point c, double r)
        {
            double w = r * 0.28;          // half the stem width
            double head = r * 0.62;       // half the head width
            double top = c.Y - r * 0.58;
            double bottom = c.Y + r * 0.6;
            double neck = c.Y + r * 0.02;
            return Shapes.Polygon(new[]
            {
                new Point(c.X - w, top), new Point(c.X + w, top), new Point(c.X + w, neck), new Point(c.X + head, neck),
                new Point(c.X, bottom), new Point(c.X - head, neck), new Point(c.X - w, neck),
            });
        }

        public static void DrawSocialPreview(DrawingContext dc)
        {
            const double W = 1280, H = 640;
            var bg = new LinearGradientBrush(Hex.C("#071426"), Hex.C("#0C2C4E"), new Point(0, 0), new Point(1, 1));
            dc.DrawRectangle(bg, null, new Rect(0, 0, W, H));

            // Accent glow behind the mark and a faint field of nuts, echoing the logo without competing with it.
            var glow = new RadialGradientBrush(Hex.A(Hex.C("#4CC2FF"), 0.28), Hex.A(Hex.C("#4CC2FF"), 0))
            {
                Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5
            };
            dc.DrawEllipse(glow, null, new Point(240, 320), 320, 320);
            var faint = new Pen(Hex.B(Hex.A(Colors.White, 0.045)), 2);
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    double x = 760 + col * 96 + (row % 2) * 48;
                    double y = 40 + row * 140;
                    if (x > W + 60) continue;
                    dc.DrawGeometry(null, faint, Shapes.Hexagon(new Point(x, y), 34));
                }
            }

            const double tileSize = 260, tileX = 110;
            dc.PushTransform(new TranslateTransform(tileX, (H - tileSize) / 2));
            DrawTile(dc, tileSize, false);
            dc.Pop();

            // Title and tagline as one block, centred on the tile; the tagline shrinks if a font runs wide.
            var family = new FontFamily("Segoe UI Variable Display, Segoe UI");
            double textX = tileX + tileSize + 70;
            double room = W - textX - 60;
            var title = Text("NutDesk", new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 132, Colors.White);
            var taglineFace = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double taglineSize = 36;
            var tagline = Text("A modern Windows client for NUT UPS monitoring", taglineFace, taglineSize, Hex.C("#A8D8F5"));
            if (tagline.Width > room)
            {
                taglineSize *= room / tagline.Width;
                tagline = Text("A modern Windows client for NUT UPS monitoring", taglineFace, taglineSize, Hex.C("#A8D8F5"));
            }
            double barGap = 22, barH = 5;
            double block = title.Baseline + barGap + barH + barGap + tagline.Height;
            double top = (H - block) / 2 - (title.Height - title.Baseline) * 0.2;
            dc.DrawText(title, new Point(textX - 6, top));
            double barY = top + title.Baseline + barGap;
            dc.DrawRoundedRectangle(Hex.B(Hex.C("#4CC2FF")), null, new Rect(textX, barY, 96, barH), barH / 2, barH / 2);
            dc.DrawText(tagline, new Point(textX, barY + barH + barGap));
        }

        public static FormattedText Text(string s, Typeface face, double size, Color color)
        {
            return new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, Hex.B(color), 1.0);
        }
    }

    /// <summary>
    /// Pictures of the WinForms dialogs and menus. They appear on both light and dark menus, so they use the
    /// Fluent "colour" treatment: white or accent-filled shapes with a mid-grey outline, which read on either.
    /// </summary>
    internal static class UiArt
    {
        public static readonly int[] LogIconSizes = { 16, 24, 32, 48, 64 };

        private static readonly Color Outline = Hex.C("#6B6B6B");
        private static readonly Color Paper = Colors.White;
        private static readonly Color Fold = Hex.C("#DCDCDC");
        private static readonly Color Accent = Hex.C("#0078D4");
        private static readonly Color AccentLight = Hex.C("#2B9AF3");
        private static readonly Color AccentDark = Hex.C("#005A9E");
        private static readonly Color Teal = Hex.C("#00B7C3");
        private static readonly Color Green = Hex.C("#107C10");
        private static readonly Color Red = Hex.C("#C42B1C");
        private static readonly Color Amber = Hex.C("#D48C00");

        internal sealed class Picture
        {
            public string File;
            public int Size;
            public Action<DrawingContext, int> Draw;
        }

        public static readonly Picture[] Pictures =
        {
            new Picture { File = "Battery_Charged.png", Size = 24, Draw = (dc, s) => Battery(dc, 1.0, Green, BadgeKind.Check) },
            new Picture { File = "Battery_Charging.png", Size = 24, Draw = (dc, s) => Battery(dc, 0.6, Accent, BadgeKind.Bolt) },
            new Picture { File = "Battery_Discharging.png", Size = 24, Draw = (dc, s) => Battery(dc, 0.4, Amber, BadgeKind.Down) },
            new Picture { File = "CopyHS.png", Size = 16, Draw = (dc, s) => Copy(dc) },
            new Picture { File = "saveHS.png", Size = 16, Draw = (dc, s) => Save(dc) },
            new Picture { File = "RepeatHS.png", Size = 16, Draw = (dc, s) => Repeat(dc) },
            new Picture { File = "disconnect2.png", Size = 16, Draw = (dc, s) => Globe(dc, s, false) },
            new Picture { File = "internetconnection.png", Size = 48, Draw = (dc, s) => Globe(dc, s, true) },
            new Picture { File = "regedit.exe_14_100-0.png", Size = 32, Draw = (dc, s) => Blocks(dc) },
            new Picture { File = "XP_Information.png", Size = 48, Draw = (dc, s) => Information(dc) },
            new Picture { File = "ViewLogFile_24x24.png", Size = 24, Draw = ViewLog },
            new Picture { File = "Delete_LogFile_24x24.png", Size = 24, Draw = DeleteLog },
            new Picture { File = "ups_104x104.png", Size = 104, Draw = (dc, s) => BrandArt.DrawTile(dc, s, false) },
        };

        private static Pen P(Color c, double w, bool round = false)
        {
            var p = new Pen(Hex.B(c), w);
            if (round)
            {
                p.StartLineCap = p.EndLineCap = PenLineCap.Round;
                p.LineJoin = PenLineJoin.Round;
            }
            return p;
        }

        /// <summary>Clips everything drawn until Pop() to outside the circle: the gap that sets a badge off.</summary>
        private static void PushNotch(DrawingContext dc, double s, Point c, double r)
        {
            dc.PushClip(Shapes.Exclude(new RectangleGeometry(new Rect(-s, -s, 3 * s, 3 * s)), new EllipseGeometry(c, r, r)));
        }

        /// <summary>A page with a folded corner; the rect is the outline's centre line, text lines in the accent.</summary>
        private static void Page(DrawingContext dc, Rect r, double stroke, double fold, int lines)
        {
            var page = Shapes.Polygon(new[]
            {
                new Point(r.Left, r.Top), new Point(r.Right - fold, r.Top), new Point(r.Right, r.Top + fold),
                new Point(r.Right, r.Bottom), new Point(r.Left, r.Bottom),
            });
            dc.DrawGeometry(Hex.B(Paper), P(Outline, stroke), page);
            var corner = Shapes.Polygon(new[] { new Point(r.Right - fold, r.Top), new Point(r.Right - fold, r.Top + fold), new Point(r.Right, r.Top + fold) });
            dc.DrawGeometry(Hex.B(Fold), P(Outline, stroke), corner);

            double lineW = Math.Max(1, stroke);
            double left = r.Left + fold * 0.55;
            double step = (r.Height - fold) / (lines + 1.4);
            for (int i = 0; i < lines; i++)
            {
                double y = Math.Round(r.Top + fold + step * (i + 0.9)) + (lineW % 2 == 1 ? 0.5 : 0);
                double right = r.Right - fold * (i == lines - 1 ? 1.4 : 0.55);
                dc.DrawLine(P(Accent, lineW), new Point(left, y), new Point(right, y));
            }
        }

        public static void ViewLog(DrawingContext dc, int s)
        {
            double k = s / 24.0;
            double t = Math.Max(1, Math.Round(k));
            double o = t % 2 == 1 ? 0.5 : 0;   // keep odd strokes on half pixels
            var lens = new Point(Math.Round(15 * k) + 0.5 * (s % 2), Math.Round(15 * k));
            double lensR = 4.6 * k;
            PushNotch(dc, s, lens, lensR + 1.6 * k + t / 2);
            Page(dc, new Rect(Math.Round(3 * k) + o, Math.Round(1 * k) + o, Math.Round(14 * k), Math.Round(21 * k)), t, Math.Round(5 * k), 3);
            dc.Pop();
            double lw = Math.Max(1.5, 2 * k);
            dc.DrawEllipse(Hex.B(Paper), P(Accent, lw), lens, lensR, lensR);
            var d = new Vector(1, 1) / Math.Sqrt(2);
            dc.DrawLine(P(Accent, Math.Max(2, 2.6 * k), true), lens + d * (lensR + lw / 2), lens + d * (lensR + 4.2 * k));
        }

        public static void DeleteLog(DrawingContext dc, int s)
        {
            double k = s / 24.0;
            double t = Math.Max(1, Math.Round(k));
            double o = t % 2 == 1 ? 0.5 : 0;
            var c = new Point(17 * k, 17 * k);
            double r = 6.2 * k;
            PushNotch(dc, s, c, r + 1.4 * k);
            Page(dc, new Rect(Math.Round(3 * k) + o, Math.Round(1 * k) + o, Math.Round(14 * k), Math.Round(21 * k)), t, Math.Round(5 * k), 3);
            dc.Pop();
            dc.DrawEllipse(Hex.B(Red), null, c, r, r);
            double x = r * 0.42;
            var pen = P(Colors.White, Math.Max(1.5, 1.8 * k), true);
            dc.DrawLine(pen, new Point(c.X - x, c.Y - x), new Point(c.X + x, c.Y + x));
            dc.DrawLine(pen, new Point(c.X + x, c.Y - x), new Point(c.X - x, c.Y + x));
        }

        private enum BadgeKind { Check, Bolt, Down }

        private static void Battery(DrawingContext dc, double level, Color color, BadgeKind badge)
        {
            // 24 x 24, the size of the status picture box on the main window.
            var badgeC = new Point(18, 17.5);
            const double badgeR = 5.5;
            PushNotch(dc, 24, badgeC, badgeR + 1.5);
            dc.DrawRectangle(Hex.B(Outline), null, new Rect(5, 1, 6, 2));
            dc.DrawRoundedRectangle(Hex.B(Paper), P(Outline, 1.5), new Rect(2.75, 3.75, 10.5, 18.5), 2, 2);
            double inner = 15;
            double h = Math.Round(inner * level);
            dc.DrawRoundedRectangle(Hex.B(color), null, new Rect(5, 20 - h, 6, h), 1, 1);
            dc.Pop();

            dc.DrawEllipse(Hex.B(color), null, badgeC, badgeR, badgeR);
            var pen = P(Colors.White, 1.6, true);
            switch (badge)
            {
                case BadgeKind.Check:
                    dc.DrawGeometry(null, pen, Polyline(new Point(15.5, 17.8), new Point(17.3, 19.6), new Point(20.6, 15.6)));
                    break;
                case BadgeKind.Bolt:
                    dc.DrawGeometry(Brushes.White, null, Shapes.Bolt(new Rect(15.6, 13.6, 4.8, 7.8)));
                    break;
                case BadgeKind.Down:
                    dc.DrawLine(pen, new Point(18, 14.6), new Point(18, 20.2));
                    dc.DrawGeometry(null, pen, Polyline(new Point(15.6, 18), new Point(18, 20.4), new Point(20.4, 18)));
                    break;
            }
        }

        private static Geometry Polyline(params Point[] pts)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
            }
            g.Freeze();
            return g;
        }

        private static void Copy(DrawingContext dc)
        {
            dc.DrawRoundedRectangle(Hex.B(Paper), P(Outline, 1), new Rect(5.5, 1.5, 8, 10), 1, 1);
            dc.DrawRoundedRectangle(Hex.B(Paper), P(Outline, 1), new Rect(2.5, 4.5, 8, 10), 1, 1);
            var line = P(Accent, 1);
            dc.DrawLine(line, new Point(4.5, 7.5), new Point(8.5, 7.5));
            dc.DrawLine(line, new Point(4.5, 9.5), new Point(8.5, 9.5));
            dc.DrawLine(line, new Point(4.5, 11.5), new Point(7.5, 11.5));
        }

        private static void Save(DrawingContext dc)
        {
            var body = Shapes.Polygon(new[] { new Point(1.5, 2.5), new Point(2.5, 1.5), new Point(12, 1.5), new Point(14.5, 4), new Point(14.5, 13.5), new Point(13.5, 14.5), new Point(2.5, 14.5), new Point(1.5, 13.5) });
            dc.DrawGeometry(Hex.B(Accent), P(AccentDark, 1), body);
            dc.DrawRectangle(Hex.B(Hex.C("#E8F2FB")), null, new Rect(4, 2, 7, 4));
            dc.DrawRectangle(Hex.B(AccentDark), null, new Rect(8, 2.5, 2, 3));
            dc.DrawRectangle(Hex.B(Paper), null, new Rect(4, 9, 8, 5));
            dc.DrawRectangle(Hex.B(Hex.C("#9CC8EE")), null, new Rect(5, 10.5, 6, 1));
        }

        private static void Repeat(DrawingContext dc)
        {
            // The "keep the connection" loop: two half loops chasing each other.
            var pen = P(Accent, 1.6, true);
            var fill = Hex.B(Accent);
            var top = new StreamGeometry();
            using (var ctx = top.Open())
            {
                ctx.BeginFigure(new Point(2.5, 9.5), false, false);
                ctx.LineTo(new Point(2.5, 7.5), true, true);
                ctx.ArcTo(new Point(5.5, 4.5), new Size(3, 3), 0, false, SweepDirection.Clockwise, true, true);
                ctx.LineTo(new Point(10.5, 4.5), true, true);
            }
            dc.DrawGeometry(null, pen, top);
            dc.DrawGeometry(fill, null, Shapes.Polygon(new[] { new Point(10, 1.6), new Point(14, 4.5), new Point(10, 7.4) }));
            var bottom = new StreamGeometry();
            using (var ctx = bottom.Open())
            {
                ctx.BeginFigure(new Point(13.5, 6.5), false, false);
                ctx.LineTo(new Point(13.5, 8.5), true, true);
                ctx.ArcTo(new Point(10.5, 11.5), new Size(3, 3), 0, false, SweepDirection.Clockwise, true, true);
                ctx.LineTo(new Point(5.5, 11.5), true, true);
            }
            dc.DrawGeometry(null, pen, bottom);
            dc.DrawGeometry(fill, null, Shapes.Polygon(new[] { new Point(6, 8.6), new Point(2, 11.5), new Point(6, 14.4) }));
        }

        /// <summary>Connect (48 px) and disconnect (16 px): the network globe with a green check or a red cross.</summary>
        private static void Globe(DrawingContext dc, int s, bool connected)
        {
            double k = s / 16.0;
            var c = new Point(7.5 * k, 7.5 * k);
            double r = 6.6 * k;
            var badgeC = new Point(12 * k, 12 * k);
            double badgeR = 3.9 * k;

            PushNotch(dc, s, badgeC, badgeR + Math.Max(1, 0.9 * k));
            var sphere = new RadialGradientBrush(AccentLight, AccentDark) { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.4, 0.35), RadiusX = 0.75, RadiusY = 0.75 };
            sphere.Freeze();
            dc.DrawEllipse(sphere, null, c, r, r);
            var grid = P(Hex.A(Colors.White, 0.85), Math.Max(1, 0.75 * k));
            dc.PushClip(new EllipseGeometry(c, r - 0.3 * k, r - 0.3 * k));
            dc.DrawEllipse(null, grid, c, r * 0.42, r);
            dc.DrawLine(grid, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
            dc.DrawLine(grid, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
            if (s >= 32)
            {
                dc.DrawLine(grid, new Point(c.X - r, c.Y - r * 0.5), new Point(c.X + r, c.Y - r * 0.5));
                dc.DrawLine(grid, new Point(c.X - r, c.Y + r * 0.5), new Point(c.X + r, c.Y + r * 0.5));
            }
            dc.Pop();
            dc.Pop();

            dc.DrawEllipse(Hex.B(connected ? Green : Red), null, badgeC, badgeR, badgeR);
            var pen = P(Colors.White, Math.Max(1.2, 0.95 * k), true);
            if (connected)
            {
                dc.DrawGeometry(null, pen, Polyline(new Point(badgeC.X - badgeR * 0.45, badgeC.Y + badgeR * 0.02),
                                                     new Point(badgeC.X - badgeR * 0.1, badgeC.Y + badgeR * 0.38),
                                                     new Point(badgeC.X + badgeR * 0.48, badgeC.Y - badgeR * 0.34)));
            }
            else
            {
                double x = badgeR * 0.4;
                dc.DrawLine(pen, new Point(badgeC.X - x, badgeC.Y - x), new Point(badgeC.X + x, badgeC.Y + x));
                dc.DrawLine(pen, new Point(badgeC.X + x, badgeC.Y - x), new Point(badgeC.X - x, badgeC.Y + x));
            }
        }

        /// <summary>Settings blocks for the legacy (registry) preferences import.</summary>
        private static void Blocks(DrawingContext dc)
        {
            dc.DrawRoundedRectangle(Hex.B(AccentDark), null, new Rect(3, 17, 12, 12), 2.5, 2.5);
            dc.DrawRoundedRectangle(Hex.B(Accent), null, new Rect(17, 17, 12, 12), 2.5, 2.5);
            dc.DrawRoundedRectangle(Hex.B(Accent), null, new Rect(3, 3, 12, 12), 2.5, 2.5);
            dc.PushTransform(new RotateTransform(45, 23, 9));
            dc.DrawRoundedRectangle(Hex.B(Teal), null, new Rect(18, 4, 10, 10), 2.5, 2.5);
            dc.Pop();
        }

        private static void Information(DrawingContext dc)
        {
            var disc = new LinearGradientBrush(AccentLight, AccentDark, new Point(0.2, 0), new Point(0.8, 1));
            disc.Freeze();
            dc.DrawEllipse(disc, null, new Point(24, 24), 21, 21);
            dc.DrawEllipse(Brushes.White, null, new Point(24, 14.5), 3.2, 3.2);
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(21, 20.5, 6, 16), 3, 3);
        }
    }

    internal static class Raster
    {
        public static BitmapSource Render(int w, int h, Action<DrawingContext> draw)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                draw(dc);
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        /// <summary>Straight (non-premultiplied) BGRA, the layout both PNG and icon DIBs expect.</summary>
        public static byte[] Bgra(BitmapSource bmp)
        {
            var conv = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            var px = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
            conv.CopyPixels(px, bmp.PixelWidth * 4, 0);
            return px;
        }

        public static byte[] Png(BitmapSource bmp)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0)));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms);
                return ms.ToArray();
            }
        }

        public static void SavePng(BitmapSource bmp, string path)
        {
            File.WriteAllBytes(path, Png(bmp));
        }
    }

    /// <summary>
    /// Minimal .ico writer: ICONDIR, one ICONDIRENTRY per frame, then the frames. Frames below 256 px are 32-bit DIBs,
    /// which every consumer (Explorer, the notification area, GDI+) reads; the 256 px frame is PNG, as Windows expects.
    /// </summary>
    internal static class Ico
    {
        public static void Write(string path, IList<BitmapSource> frames)
        {
            var ordered = frames.OrderByDescending(f => f.PixelWidth).ToList();
            var images = ordered.Select(f => f.PixelWidth >= 256 ? Raster.Png(f) : Dib(f)).ToList();

            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)ordered.Count);
                int offset = 6 + 16 * ordered.Count;
                for (int i = 0; i < ordered.Count; i++)
                {
                    int px = ordered[i].PixelWidth;
                    w.Write((byte)(px >= 256 ? 0 : px));
                    w.Write((byte)(px >= 256 ? 0 : ordered[i].PixelHeight));
                    w.Write((byte)0);      // no palette
                    w.Write((byte)0);
                    w.Write((ushort)1);    // planes
                    w.Write((ushort)32);   // bits per pixel
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images)
                {
                    w.Write(img);
                }
            }
        }

        private static byte[] Dib(BitmapSource bmp)
        {
            int wd = bmp.PixelWidth, ht = bmp.PixelHeight;
            var px = Raster.Bgra(bmp);
            int maskStride = (wd + 31) / 32 * 4;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                // BITMAPINFOHEADER; the height counts the colour rows and the AND mask rows together.
                w.Write(40);
                w.Write(wd);
                w.Write(ht * 2);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write(0);
                w.Write(wd * ht * 4 + maskStride * ht);
                w.Write(0);
                w.Write(0);
                w.Write(0);
                w.Write(0);
                for (int y = ht - 1; y >= 0; y--)
                {
                    w.Write(px, y * wd * 4, wd * 4);
                }
                // The AND mask is ignored where alpha is present, but old consumers still use it: mark the fully
                // transparent pixels so they stay transparent there too.
                for (int y = ht - 1; y >= 0; y--)
                {
                    var row = new byte[maskStride];
                    for (int x = 0; x < wd; x++)
                    {
                        if (px[(y * wd + x) * 4 + 3] == 0)
                        {
                            row[x / 8] |= (byte)(0x80 >> (x % 8));
                        }
                    }
                    w.Write(row);
                }
                w.Flush();
                return ms.ToArray();
            }
        }
    }

    /// <summary>Contact sheets for review: every icon at every size, on light and dark backgrounds, labelled.</summary>
    internal static class Sheet
    {
        private static readonly Color LightBg = Hex.C("#F3F3F3");
        private static readonly Color DarkBg = Hex.C("#202020");

        public static void WriteAll(string path)
        {
            string dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            string stem = Path.Combine(dir, Path.GetFileNameWithoutExtension(path));
            Raster.SavePng(StatusSheet(), path);
            Raster.SavePng(BrandSheet(), stem + "_brand.png");
            Raster.SavePng(ZoomSheet(0), stem + "_zoom_battery.png");
            Raster.SavePng(ZoomSheet(6), stem + "_zoom_mains.png");
        }

        private static FormattedText Label(string s, Color c, double size = 12)
        {
            return BrandArt.Text(s, new Typeface("Segoe UI"), size, c);
        }

        private static void Img(DrawingContext dc, BitmapSource b, double x, double y, int zoom = 1)
        {
            if (zoom > 1) b = Zoom(b, zoom);
            dc.DrawImage(b, new Rect(x, y, b.PixelWidth, b.PixelHeight));
        }

        /// <summary>Pixel replication, so a magnified frame shows exactly the pixels the icon has.</summary>
        private static BitmapSource Zoom(BitmapSource b, int z)
        {
            int w = b.PixelWidth, h = b.PixelHeight;
            var src = new byte[w * h * 4];
            new FormatConvertedBitmap(b, PixelFormats.Pbgra32, null, 0).CopyPixels(src, w * 4, 0);
            var dst = new byte[w * z * h * z * 4];
            for (int y = 0; y < h * z; y++)
            {
                for (int x = 0; x < w * z; x++)
                {
                    Buffer.BlockCopy(src, ((y / z) * w + x / z) * 4, dst, (y * w * z + x) * 4, 4);
                }
            }
            var result = BitmapSource.Create(w * z, h * z, 96, 96, PixelFormats.Pbgra32, null, dst, w * z * 4);
            result.Freeze();
            return result;
        }

        private static BitmapSource StatusSheet()
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64 };
            var states = StatusState.All().ToList();
            const int rowH = 74, labelW = 130, half = 640;
            int width = labelW + 2 * half;
            int height = 40 + rowH * 12;

            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.NearestNeighbor);
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                dc.DrawRectangle(Hex.B(LightBg), null, new Rect(labelW, 0, half, height));
                dc.DrawRectangle(Hex.B(DarkBg), null, new Rect(labelW + half, 0, half, height));
                dc.DrawText(Label("light variants, 16-64 px, then 16 px x4 and 24 px x3", Colors.Black), new Point(labelW + 8, 8));
                dc.DrawText(Label("dark variants (+64), same layout", Colors.White), new Point(labelW + half + 8, 8));

                for (int i = 0; i < 12; i++)
                {
                    var light = states[i];
                    var dark = states[i + 12];
                    double y = 40 + i * rowH;
                    dc.DrawText(Label(light.Name.Replace(" (light)", ""), Colors.Black), new Point(8, y + 4));
                    dc.DrawText(Label(light.Index + " / " + dark.Index, Hex.C("#666666"), 11), new Point(8, y + 22));
                    for (int side = 0; side < 2; side++)
                    {
                        var st = side == 0 ? light : dark;
                        double x = labelW + side * half + 8;
                        foreach (int s in sizes)
                        {
                            Img(dc, Raster.Render(s, s, d => StatusArt.Draw(d, s, st)), x, y);
                            x += s + 10;
                        }
                        Img(dc, Raster.Render(16, 16, d => StatusArt.Draw(d, 16, st)), x + 4, y, 4);
                        Img(dc, Raster.Render(24, 24, d => StatusArt.Draw(d, 24, st)), x + 80, y, 3);
                    }
                }
            }
            return Flatten(dv, width, height);
        }

        /// <summary>Six states, tray sizes only, magnified 4x: the sizes that must hold up pixel by pixel.</summary>
        private static BitmapSource ZoomSheet(int first)
        {
            int[] sizes = { 16, 20, 24, 32 };
            var states = StatusState.All().ToList();
            const int z = 4, rowH = 32 * z + 12, half = 400;
            int width = 2 * half, height = 6 * rowH + 8;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Hex.B(LightBg), null, new Rect(0, 0, half, height));
                dc.DrawRectangle(Hex.B(DarkBg), null, new Rect(half, 0, half, height));
                for (int i = 0; i < 6 && first + i < 12; i++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        var st = states[first + i + side * 12];
                        double x = side * half + 6;
                        double y = 6 + i * rowH;
                        foreach (int s in sizes)
                        {
                            Img(dc, Raster.Render(s, s, d => StatusArt.Draw(d, s, st)), x, y, z);
                            x += s * z + 6;
                        }
                    }
                }
            }
            return Flatten(dv, width, height);
        }

        private static BitmapSource BrandSheet()
        {
            const int width = 1500, height = 1180, half = 750;
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.NearestNeighbor);
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Hex.B(LightBg), null, new Rect(0, 0, half, height));
                dc.DrawRectangle(Hex.B(DarkBg), null, new Rect(half, 0, half, height));
                for (int side = 0; side < 2; side++)
                {
                    double ox = side * half + 10;
                    var fg = side == 0 ? Colors.Black : Colors.White;
                    double y = 10;
                    foreach (bool badge in new[] { false, true })
                    {
                        dc.DrawText(Label(badge ? "NutDesk-Updater.ico" : "NutDesk.ico", fg), new Point(ox, y));
                        double x = ox;
                        foreach (int s in new[] { 16, 20, 24, 32, 40, 48, 64, 96, 128 })
                        {
                            Img(dc, Raster.Render(s, s, d => BrandArt.DrawTile(d, s, badge)), x, y + 20);
                            x += s + 8;
                        }
                        Img(dc, Raster.Render(16, 16, d => BrandArt.DrawTile(d, 16, badge)), x, y + 20, 4);
                        y += 160;
                    }
                    Img(dc, Raster.Render(256, 256, d => BrandArt.DrawTile(d, 256, false)), ox, y);
                    Img(dc, Raster.Render(16, 16, d => BrandArt.DrawTile(d, 16, false)), ox + 500, y, 4);
                    Img(dc, Raster.Render(20, 20, d => BrandArt.DrawTile(d, 20, false)), ox + 270, y, 4);
                    Img(dc, Raster.Render(24, 24, d => BrandArt.DrawTile(d, 24, false)), ox + 360, y, 4);
                    Img(dc, Raster.Render(32, 32, d => BrandArt.DrawTile(d, 32, false)), ox + 270, y + 100, 4);
                    Img(dc, Raster.Render(20, 20, d => BrandArt.DrawTile(d, 20, true)), ox + 410, y + 100, 4);
                    y += 270;

                    dc.DrawText(Label("dialog and menu pictures, 1x and 3x", fg), new Point(ox, y));
                    y += 20;
                    double px = ox;
                    foreach (var p in UiArt.Pictures.Where(p => p.Size <= 48))
                    {
                        var b = Raster.Render(p.Size, p.Size, d => p.Draw(d, p.Size));
                        Img(dc, b, px, y);
                        Img(dc, b, px, y + 52, p.Size <= 24 ? 3 : 2);
                        px += Math.Max(p.Size, p.Size <= 24 ? p.Size * 3 : p.Size * 2) + 12;
                        if (px > side * half + half - 110)
                        {
                            px = ox;
                            y += 160;
                        }
                    }
                    y += 160;
                    double lx = ox;
                    foreach (int s in UiArt.LogIconSizes)
                    {
                        Img(dc, Raster.Render(s, s, d => UiArt.ViewLog(d, s)), lx, y);
                        Img(dc, Raster.Render(s, s, d => UiArt.DeleteLog(d, s)), lx, y + 70);
                        lx += s + 10;
                    }
                }
            }
            return Flatten(dv, width, height);
        }

        private static BitmapSource Flatten(Visual v, int w, int h)
        {
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(v);
            rtb.Freeze();
            return rtb;
        }
    }
}
