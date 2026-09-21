using System.Windows;
using System.Windows.Media;

namespace NutDesk.Wpf
{
    /// <summary>
    /// Builds the dashboard's brush set for the light or dark palette.
    /// </summary>
    /// <remarks>
    /// The values mirror <see cref="Theming.Theme"/> so the WPF dashboard and the remaining WinForms dialogs
    /// agree. They are handed out as a ResourceDictionary that the view merges, and swapping that dictionary is
    /// what makes a theme change take effect live: everything binds with DynamicResource.
    /// There is no WPF Application object in this process - the shell is a WinForms application - so nothing
    /// here may reach for Application.Current.
    /// </remarks>
    public static class Palette
    {
        public const string WindowBrushKey = "WindowBrush";
        public const string CardBrushKey = "CardBrush";
        public const string CardBorderBrushKey = "CardBorderBrush";
        public const string TextPrimaryBrushKey = "TextPrimaryBrush";
        public const string TextSecondaryBrushKey = "TextSecondaryBrush";
        public const string AccentBrushKey = "AccentBrush";
        public const string AccentTextBrushKey = "AccentTextBrush";
        public const string TrackBrushKey = "TrackBrush";
        public const string SuccessBrushKey = "SuccessBrush";
        public const string WarningBrushKey = "WarningBrush";
        public const string DangerBrushKey = "DangerBrush";
        public const string NeutralBrushKey = "NeutralBrush";
        public const string HoverBrushKey = "HoverBrush";
        public const string PressedBrushKey = "PressedBrush";
        public const string ShadowOpacityKey = "ShadowOpacity";

        public static ResourceDictionary Build(bool isDark)
        {
            var d = new ResourceDictionary();

            Add(d, WindowBrushKey, isDark ? Rgb(32, 32, 32) : Rgb(243, 243, 243));
            Add(d, CardBrushKey, isDark ? Rgb(43, 43, 43) : Colors.White);
            Add(d, CardBorderBrushKey, isDark ? Rgb(58, 58, 58) : Rgb(229, 229, 229));
            Add(d, TextPrimaryBrushKey, isDark ? Rgb(242, 242, 242) : Rgb(27, 27, 27));
            Add(d, TextSecondaryBrushKey, isDark ? Rgb(158, 158, 158) : Rgb(98, 98, 98));
            Add(d, AccentBrushKey, isDark ? Rgb(76, 194, 255) : Rgb(15, 108, 189));
            Add(d, AccentTextBrushKey, isDark ? Rgb(16, 16, 16) : Colors.White);
            Add(d, TrackBrushKey, isDark ? Rgb(58, 58, 58) : Rgb(233, 233, 233));
            Add(d, SuccessBrushKey, isDark ? Rgb(108, 203, 95) : Rgb(16, 137, 62));
            Add(d, WarningBrushKey, isDark ? Rgb(252, 225, 0) : Rgb(186, 126, 0));
            Add(d, DangerBrushKey, isDark ? Rgb(255, 153, 164) : Rgb(196, 43, 28));
            Add(d, NeutralBrushKey, isDark ? Rgb(96, 96, 96) : Rgb(198, 198, 198));
            Add(d, HoverBrushKey, isDark ? Rgb(58, 58, 58) : Rgb(236, 236, 236));
            Add(d, PressedBrushKey, isDark ? Rgb(70, 70, 70) : Rgb(222, 222, 222));

            // A card on a dark surface needs far less shadow than one on a light surface.
            d[ShadowOpacityKey] = isDark ? 0.32 : 0.12;

            return d;
        }

        private static void Add(ResourceDictionary d, string key, Color colour)
        {
            var brush = new SolidColorBrush(colour);
            brush.Freeze();
            d[key] = brush;
        }

        private static Color Rgb(byte r, byte g, byte b)
        {
            return Color.FromRgb(r, g, b);
        }
    }
}
