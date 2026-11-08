using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace DeskWidgets
{
    enum AppTheme { System, Light, Dark }

    static class Theme
    {
        static bool dark = ReadDark();
        public static bool Dark { get { return dark; } }
        static AppTheme appearance = AppTheme.System;
        public static AppTheme Appearance
        {
            get { return appearance; }
            set
            {
                if (value == appearance) return;
                appearance = value;
                Refresh();
            }
        }

        public static event Action Changed;

        public static void Refresh()
        {
            bool d = ReadDark();
            if (d == dark) return;
            dark = d;
            if (Changed != null) Changed();
        }

        static bool ReadDark()
        {
            if (appearance == AppTheme.Light) return false;
            if (appearance == AppTheme.Dark) return true;
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return k != null && k.GetValue("AppsUseLightTheme") is int && (int)k.GetValue("AppsUseLightTheme") == 0;
        }

        static Color Pick(int light, int darkRgb) { return Rgb(dark ? darkRgb : light); }
        public static Color Rgb(int rgb) { return Color.FromArgb(unchecked((int)0xFF000000) | rgb); }

        // Professional, minimal Windows palette
        public static Color WindowBg { get { return Pick(0xFFFFFF, 0x121214); } }
        public static Color SidebarBg { get { return Pick(0xF8F9FA, 0x18181B); } }
        public static Color Card { get { return Pick(0xFFFFFF, 0x1E1E22); } }
        public static Color CardHover { get { return Pick(0xF4F4F6, 0x27272C); } }
        public static Color CardBorder { get { return Pick(0xE4E4E7, 0x2E2E34); } }
        public static Color Border { get { return CardBorder; } }
        public static Color FillHighlight { get { return Selection; } }

        public static Color Label { get { return Pick(0x18181B, 0xF4F4F5); } }
        public static Color Label2 { get { return Pick(0x71717A, 0xA1A1AA); } }
        public static Color Label3 { get { return Pick(0xA1A1AA, 0x71717A); } }

        public static Color Link { get { return Pick(0x0284C7, 0x38BDF8); } }
        public static readonly Color PrimaryFill = Rgb(0x0284C7);
        public static readonly Color OnPrimary = Color.White;
        public static Color Destructive { get { return Pick(0xDC2626, 0xF87171); } }
        public static Color Success { get { return Pick(0x16A34A, 0x4ADE80); } }

        public static Color Fill { get { return dark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(12, 0, 0, 0); } }
        public static Color Tinted { get { return dark ? Color.FromArgb(40, 2, 132, 199) : Color.FromArgb(20, 2, 132, 199); } }
        public static Color Selection { get { return dark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0); } }
        public static Color Separator { get { return dark ? Color.FromArgb(255, 46, 46, 52) : Color.FromArgb(255, 228, 228, 231); } }
        public static Color Field { get { return Pick(0xFFFFFF, 0x1E1E22); } }
        public static Color FieldBorder { get { return Pick(0xD4D4D8, 0x3F3F46); } }

        public static Color MenuBg { get { return dark ? Color.FromArgb(252, 28, 28, 32) : Color.FromArgb(252, 255, 255, 255); } }
        public static Color MenuEdge { get { return dark ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0); } }

        // Consistent modern radii
        public const int RadiusCard = 12, RadiusField = 8, RadiusSmall = 6, RadiusItem = 6, RadiusMenu = 8;

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, path);
        }

        public static void DrawRoundBorder(Graphics g, Color c, RectangleF r, float radius, float stroke = 1f)
        {
            using (var path = RoundRect(r, radius))
            using (var pen = new Pen(c, stroke))
                g.DrawPath(pen, path);
        }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                a.A + (int)((b.A - a.A) * t),
                a.R + (int)((b.R - a.R) * t),
                a.G + (int)((b.G - a.G) * t),
                a.B + (int)((b.B - a.B) * t));
        }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public static Font Font(float px, int weight = 400)
        {
            string key = px + "/" + weight;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            string optical = px >= 20 ? "Display" : "Text";
            string family = "Segoe UI Variable " + optical + (weight == 600 ? " Semibold" : "");
            FontStyle style = weight >= 700 ? FontStyle.Bold : FontStyle.Regular;
            f = new Font(family, px * 0.75f, style);
            if (f.Name != family)
            {
                f.Dispose();
                f = new Font(weight == 600 ? "Segoe UI Semibold" : "Segoe UI", px * 0.75f, style);
            }
            fonts[key] = f;
            return f;
        }

        public static Font LargeTitle { get { return Font(28, 700); } }
        public static Font Title3 { get { return Font(18, 600); } }
        public static Font Headline { get { return Font(16, 600); } }
        public static Font SectionTitle { get { return Font(15, 700); } }
        public static Font Body { get { return Font(14); } }
        public static Font BodyEmphasized { get { return Font(14, 600); } }
        public static Font Footnote { get { return Font(12); } }
        public static Font Caption { get { return Font(11); } }
        public static Font CaptionEmphasized { get { return Font(11, 600); } }
        public static Font Button { get { return Font(12, 600); } }
        public static Font Menu { get { return Font(13); } }

        static Font mono;
        public static Font Mono
        {
            get
            {
                if (mono == null)
                {
                    mono = new Font("Cascadia Mono", 9.5f);
                    if (mono.Name != "Cascadia Mono") { mono.Dispose(); mono = new Font("Consolas", 9.5f); }
                }
                return mono;
            }
        }

        static Font icons;
        public static Font Icons
        {
            get
            {
                if (icons == null)
                {
                    icons = new Font("Segoe Fluent Icons", 11f);
                    if (icons.Name != "Segoe Fluent Icons") { icons.Dispose(); icons = new Font("Segoe MDL2 Assets", 11f); }
                }
                return icons;
            }
        }

        public static void ApplyToWindow(IntPtr hwnd)
        {
            int d = dark ? 1 : 0;
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4);
        }

        public static void ApplyToScrollbars(IntPtr hwnd)
        {
            Native.UseControlTheme(hwnd, dark);
        }
    }
}
