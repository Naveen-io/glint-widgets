using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;

namespace DeskWidgets.Widgets
{
    // Base for the Apple-style card widgets: small (170 x 170 pt), medium (364 x 170 pt) or large (364 x 382 pt),
    // 22 pt corner radius, 16 pt margins, dark / light / vibrant appearance, three sizes.
    // Subclasses lay out in points and call P() to convert; everything is drawn on demand.
    public abstract class CardWidget : WidgetWindow
    {
        protected sealed class Palette
        {
            public Color Bg, Fg, Fg2, Fill, Sep, Red, Orange, Yellow, Green, Blue, Purple;
            public Color OnAccent; // glyphs and text drawn on top of an accent-coloured fill
        }

        protected static readonly Palette DarkPalette = new Palette
        {
            Bg = Rgb(0x18181B), Fg = Rgb(0xF4F4F5), Fg2 = Rgb(0xA1A1AA), Fill = Rgb(0x27272A), Sep = Rgb(0x3F3F46),
            Red = Rgb(0xEF4444), Orange = Rgb(0xF97316), Yellow = Rgb(0xF59E0B), Green = Rgb(0x10B981),
            Blue = Rgb(0x3B82F6), Purple = Rgb(0x8B5CF6), OnAccent = Color.White
        };

        protected static readonly Palette LightPalette = new Palette
        {
            Bg = Color.White, Fg = Rgb(0x09090B), Fg2 = Rgb(0x71717A), Fill = Rgb(0xF4F4F5), Sep = Rgb(0xE4E4E7),
            Red = Rgb(0xDC2626), Orange = Rgb(0xEA580C), Yellow = Rgb(0xD97706), Green = Rgb(0x059669),
            Blue = Rgb(0x2563EB), Purple = Rgb(0x7C3AED), OnAccent = Color.White
        };

        // macOS "vibrant" desktop widgets: frosted wallpaper behind, everything in white.
        protected static readonly Palette VibrantPalette = new Palette
        {
            Bg = Color.FromArgb(40, 255, 255, 255), Fg = Color.White, Fg2 = Color.FromArgb(175, 255, 255, 255),
            Fill = Color.FromArgb(40, 255, 255, 255), Sep = Color.FromArgb(55, 255, 255, 255),
            Red = Color.White, Orange = Color.White, Yellow = Color.White, Green = Color.White, Blue = Color.White, Purple = Color.White,
            OnAccent = Rgb(0x2A3B42)
        };

        protected static Color Rgb(int rgb) { return Color.FromArgb(unchecked((int)0xFF000000) | rgb); }

        static readonly int[] ScaleOptions = { 85, 100, 125 };
        static readonly string[] ScaleNames = { "Compact", "Standard", "Large" };

        protected enum CardSize { Small, Medium, Large }

        readonly CardSize size;
        readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        float fontScale;

        protected CardWidget(Section settings, bool medium) : this(settings, medium ? CardSize.Medium : CardSize.Small) { }

        protected CardWidget(Section settings, CardSize size) : base(settings)
        {
            this.size = size;
        }

        protected string Face { get { return Settings.Get("face", DefaultFace); } }
        protected virtual string DefaultFace { get { return "glass"; } }
        protected bool IsGlass { get { return Face == "glass" || Face == "clear" || Face == "vibrant" || Face == "default"; } }
        protected bool IsClear { get { return Face == "clear"; } }
        protected bool IsVibrant { get { return Face == "vibrant"; } }
        protected bool IsDark { get { return Face != "light"; } }
        protected Palette Pal { get { return IsVibrant ? VibrantPalette : Face == "light" ? LightPalette : DarkPalette; } }
        protected float Zoom { get { return Dpi * Settings.GetInt("scale", 100) / 100f; } }
        protected float P(float pt) { return pt * Zoom; }
        protected float W
        {
            get
            {
                int cw = Settings.GetInt("custom_w", 0);
                return cw > 0 ? cw : P(size == CardSize.Small ? 170 : 364);
            }
        }
        protected float H
        {
            get
            {
                int ch = Settings.GetInt("custom_h", 0);
                return ch > 0 ? ch : P(size == CardSize.Large ? 382 : 170);
            }
        }

        protected override Size Measure()
        {
            return new Size((int)Math.Round(W), (int)Math.Round(H));
        }

        protected abstract void DrawContent(Graphics g, Palette pal);

        protected override void Draw(Graphics g)
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            Palette pal = Pal;
            using (var card = CardPath())
            {
                if (IsGlass || IsVibrant) DrawLiquidGlassCard(g, card, pal);
                else
                {
                    using (var b = new SolidBrush(CardBackground(pal))) g.FillPath(b, card);
                    using (var pen = new Pen(pal.Sep, 1f)) g.DrawPath(pen, card);
                }
            }
            DrawContent(g, pal);
            g.ResetClip();
        }

        // Vibrant background: the blurred wallpaper behind the widget, a soft dark tint so white text
        // stays readable on light wallpapers, and a hairline edge like glass.
        Bitmap backdrop;
        Rectangle backdropFor;

        void DrawLiquidGlassCard(Graphics g, GraphicsPath card, Palette pal)
        {
            Size size = new Size((int)Math.Round(W), (int)Math.Round(H));
            // Library previews have no place on screen yet: they get a neutral stand-in.
            Rectangle where = IsHandleCreated ? new Rectangle(Location, size) : Rectangle.Empty;
            if (backdrop == null || where != backdropFor || backdrop.Size != size)
            {
                if (backdrop != null) backdrop.Dispose();
                backdrop = DesktopBackdrop.Blurred(size, where);
                backdropFor = where;
            }
            var state = g.Save();
            g.SetClip(card);

            // 1. Refracted wallpaper backdrop
            g.DrawImageUnscaled(backdrop, 0, 0);

            // 2. Liquid Glass Translucent Base:
            // Follows @zakisheriff/liquid-glass --lg-panel-fill:
            // In dark mode: rgb(30 30 33 / 0.62) (frosted) or rgb(30 30 33 / 0.22) (clear)
            // In light mode: rgb(255 255 255 / 0.62)
            // Default 75% opacity: 255 * 0.75 = 191.25 -> 192 alpha
            Color glassFill = IsDark
                ? (IsClear ? Color.FromArgb(64, 30, 30, 33) : Color.FromArgb(192, 30, 30, 33))
                : (IsClear ? Color.FromArgb(64, 255, 255, 255) : Color.FromArgb(192, 255, 255, 255));
            using (var b = new SolidBrush(glassFill)) g.FillPath(b, card);

            // 3. Subtle incident light reflection (No harsh horizontal bands)
            using (var grad = new LinearGradientBrush(new PointF(0, 0), new PointF(W, H),
                IsDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(40, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255)))
            {
                g.FillPath(grad, card);
            }

            g.Restore(state);

            // 4. Subtle Hairline Edge matching official --lg-edge
            // rgb(255 255 255 / 0.09) in dark, rgb(0 0 0 / 0.06) in light
            Color edgeColor = IsDark
                ? Color.FromArgb(23, 255, 255, 255)
                : Color.FromArgb(15, 0, 0, 0);
            using (var edgePen = new Pen(edgeColor, 1f))
            {
                g.DrawPath(edgePen, card);
            }

            // 5. Snell's Law Specular Rim Highlight (--lg-rim):
            // Soft specular reflection along the upper rim
            float r = P(14);
            using (var rimPen = new Pen(IsDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(90, 255, 255, 255), 1f))
            {
                using (var topRim = new GraphicsPath())
                {
                    topRim.AddArc(0.5f, 0.5f, r * 2, r * 2, 180, 90);
                    topRim.AddLine(r, 0.5f, W - r, 0.5f);
                    topRim.AddArc(W - r * 2 - 0.5f, 0.5f, r * 2, r * 2, 270, 90);
                    g.DrawPath(rimPen, topRim);
                }
            }
        }

        public void DrawLiquidGlassOver(Graphics g, GraphicsPath card)
        {
            var state = g.Save();
            g.SetClip(card);

            // 1. Soft surface sheen (gives liquid glass depth over content)
            using (var sheen = new LinearGradientBrush(new PointF(0, 0), new PointF(0, H * 0.45f),
                Color.FromArgb(28, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)))
            {
                g.FillPath(sheen, card);
            }

            g.Restore(state);

            // 2. Hairline perimeter edge
            using (var edgePen = new Pen(IsDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0), 1f))
            {
                g.DrawPath(edgePen, card);
            }

            // 3. Snell\'s law specular rim highlight along upper edge
            float r = P(14);
            using (var rimPen = new Pen(IsDark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(100, 255, 255, 255), 1.2f))
            {
                using (var topRim = new GraphicsPath())
                {
                    topRim.AddArc(0.5f, 0.5f, r * 2, r * 2, 180, 90);
                    topRim.AddLine(r, 0.5f, W - r, 0.5f);
                    topRim.AddArc(W - r * 2 - 0.5f, 0.5f, r * 2, r * 2, 270, 90);
                    g.DrawPath(rimPen, topRim);
                }
            }
        }

        public override void BackdropChanged()
        {
            if (backdrop != null) { backdrop.Dispose(); backdrop = null; }
            if (IsGlass || IsVibrant) Render();
        }

        public static void DrawLiquidGlassWell(Graphics g, RectangleF rect, float radius, bool isDark)
        {
            using (var path = RoundRect(rect, radius))
            {
                Color wellFill = isDark ? Color.FromArgb(80, 10, 10, 14) : Color.FromArgb(45, 0, 0, 0);
                using (var b = new SolidBrush(wellFill)) g.FillPath(b, path);

                using (var p1 = new Pen(isDark ? Color.FromArgb(60, 0, 0, 0) : Color.FromArgb(25, 0, 0, 0), 1f))
                    g.DrawPath(p1, path);

                using (var p2 = new Pen(isDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(50, 255, 255, 255), 1f))
                {
                    var bottomR = new RectangleF(rect.X, rect.Y + 1f, rect.Width, rect.Height);
                    using (var pBottom = RoundRect(bottomR, radius))
                        g.DrawPath(p2, pBottom);
                }
            }
        }

        public static void DrawLiquidGlassButton(Graphics g, RectangleF rect, string text, Font font, Color textCol, Color baseTint, bool isAccent, bool hover, bool pressed, bool isDark)
        {
            float radius = Math.Min(rect.Height / 2f, 8f);
            using (var path = RoundRect(rect, radius))
            {
                Color fill;
                if (isAccent)
                {
                    fill = pressed ? Color.FromArgb(240, 230, 110, 20) : hover ? Color.FromArgb(245, 255, 155, 45) : Color.FromArgb(220, 245, 130, 32);
                }
                else
                {
                    if (pressed) fill = isDark ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(170, 255, 255, 255);
                    else if (hover) fill = isDark ? Color.FromArgb(65, 255, 255, 255) : Color.FromArgb(120, 255, 255, 255);
                    else fill = isDark ? Color.FromArgb(38, 255, 255, 255) : Color.FromArgb(70, 255, 255, 255);
                }

                using (var b = new SolidBrush(fill)) g.FillPath(b, path);

                if (hover && !pressed)
                {
                    try
                    {
                        using (var glow = new PathGradientBrush(path))
                        {
                            glow.CenterColor = Color.FromArgb(40, 255, 255, 255);
                            glow.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                            g.FillPath(glow, path);
                        }
                    }
                    catch { }
                }

                Color rim = isAccent
                    ? Color.FromArgb(120, 255, 255, 255)
                    : (isDark ? Color.FromArgb(hover ? 90 : 50, 255, 255, 255) : Color.FromArgb(hover ? 160 : 90, 255, 255, 255));
                using (var pen = new Pen(rim, 1f))
                    g.DrawPath(pen, path);

                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using (var tb = new SolidBrush(textCol))
                    g.DrawString(text, font, tb, rect, sf);
            }
        }

        public static void DrawLiquidGlassSwitch(Graphics g, RectangleF rect, bool isChecked, bool hover, bool isDark)
        {
            float radius = rect.Height / 2f;
            using (var track = RoundRect(rect, radius))
            {
                Color trackFill = isChecked
                    ? Color.FromArgb(235, 52, 199, 89)
                    : (isDark ? Color.FromArgb(80, 120, 120, 128) : Color.FromArgb(50, 120, 120, 128));
                using (var b = new SolidBrush(trackFill)) g.FillPath(b, track);

                using (var pen = new Pen(isDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0), 0.8f))
                    g.DrawPath(pen, track);

                float pad = 2f;
                float thumbD = rect.Height - pad * 2f;
                float thumbX = isChecked ? rect.Right - pad - thumbD : rect.Left + pad;
                float thumbY = rect.Top + pad;
                var thumbRect = new RectangleF(thumbX, thumbY, thumbD, thumbD);

                using (var sb = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                    g.FillEllipse(sb, thumbX, thumbY + 1.5f, thumbD, thumbD);

                using (var kb = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                    g.FillEllipse(kb, thumbRect);

                using (var kRim = new Pen(Color.FromArgb(140, 255, 255, 255), 1f))
                    g.DrawEllipse(kRim, thumbRect);
            }
        }

        public static void DrawLiquidGlassProgressBar(Graphics g, RectangleF track, float pct, Color fillTint, bool isDark)
        {
            float radius = track.Height / 2f;
            DrawLiquidGlassWell(g, track, radius, isDark);

            if (pct > 0.01f)
            {
                float fillW = Math.Max(track.Height, track.Width * Math.Min(1f, pct));
                var fillRect = new RectangleF(track.X, track.Y, fillW, track.Height);
                using (var p = RoundRect(fillRect, radius))
                {
                    using (var b = new SolidBrush(fillTint)) g.FillPath(b, p);

                    var hlRect = new RectangleF(fillRect.X, fillRect.Y, fillRect.Width, fillRect.Height * 0.5f);
                    using (var hl = new LinearGradientBrush(hlRect.Location, new PointF(hlRect.Left, hlRect.Bottom),
                        Color.FromArgb(90, 255, 255, 255), Color.FromArgb(10, 255, 255, 255)))
                    {
                        using (var hlp = RoundRect(fillRect, radius))
                        {
                            var oldClip = g.Clip;
                            g.SetClip(hlp);
                            g.FillRectangle(hl, hlRect);
                            g.Clip = oldClip;
                        }
                    }

                    using (var edge = new Pen(Color.FromArgb(100, 255, 255, 255), 1f))
                        g.DrawPath(edge, p);
                }
            }
        }

        // Widgets that cover the card with an image use a dark backing so no light edge shows at the corners.
        protected virtual Color CardBackground(Palette pal) { return pal.Bg; }

        protected GraphicsPath CardPath()
        {
            return RoundRect(new RectangleF(0.5f, 0.5f, W - 1f, H - 1f), P(14));
        }

        // Runs slow work (network, disk, decoding) on a worker thread and hands the result, or the
        // exception, back on the UI thread. Silently dropped if the widget closed meanwhile.
        protected void Background<T>(Func<T> work, Action<T, Exception> done)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                T result = default(T);
                Exception error = null;
                try { result = work(); }
                catch (Exception ex) { error = ex; }
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)delegate { if (!IsDisposed) done(result, error); }); }
                catch (InvalidOperationException) { } // handle destroyed between the check and the call
            });
        }

        // Cache file for this widget instance (null in library previews, which never write).
        protected string CacheFile(string name)
        {
            return App.DataDir == null ? null : System.IO.Path.Combine(App.DataDir, "cache", name + "-" + Settings.Name + ".txt");
        }

        // Appearance menu entries: { setting value, label }. Weather swaps Dark/Light for its sky.
        protected virtual string[][] AppearanceChoices
        {
            get { return new[] { new[] { "glass", "Liquid Glass (Regular)" }, new[] { "clear", "Liquid Glass (Clear)" }, new[] { "dark", "Dark Solid" }, new[] { "light", "Light Solid" } }; }
        }

        // Called after a setting changes: drop caches that depend on it and redraw.
        protected virtual void Changed()
        {
            Render();
        }

        // ---- drawing helpers ----
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, path);
        }

        // Draws an image desaturated (vibrant mode tones down photos and avatars, like macOS).
        protected static void DrawGray(Graphics g, Image img, RectangleF dest)
        {
            var m = new System.Drawing.Imaging.ColorMatrix(new[]
            {
                new[] { 0.30f, 0.30f, 0.30f, 0, 0 }, new[] { 0.59f, 0.59f, 0.59f, 0, 0 }, new[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 }
            });
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                attrs.SetColorMatrix(m);
                g.DrawImage(img, new[] { dest.Location, new PointF(dest.Right, dest.Top), new PointF(dest.Left, dest.Bottom) },
                    new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel, attrs);
            }
        }

        protected static void FillCircle(Graphics g, Color c, float cx, float cy, float radius)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - radius, cy - radius, radius * 2, radius * 2);
        }

        // Weight: "regular", "semibold", "light", "bold". Sizes in points (Apple's type scale: 11, 13, 15...).
        protected Font F(float pt, string weight)
        {
            if (fontScale != Zoom)
            {
                foreach (var f in fonts.Values) f.Dispose();
                fonts.Clear();
                fontScale = Zoom;
            }
            string key = weight + pt;
            Font font;
            if (!fonts.TryGetValue(key, out font))
            {
                string family = weight == "semibold" ? "Segoe UI Semibold" : weight == "light" ? "Segoe UI Light" : "Segoe UI";
                FontStyle style = weight == "bold" ? FontStyle.Bold : FontStyle.Regular;
                font = new Font(family, P(pt), style, GraphicsUnit.Pixel);
                fonts[key] = font;
            }
            return font;
        }

        static readonly StringFormat Typo = MakeFormat(StringAlignment.Near);
        static readonly StringFormat TypoRight = MakeFormat(StringAlignment.Far);
        static readonly StringFormat TypoCenter = MakeFormat(StringAlignment.Center);

        static StringFormat MakeFormat(StringAlignment align)
        {
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.Alignment = align;
            f.Trimming = StringTrimming.EllipsisCharacter;
            f.FormatFlags |= StringFormatFlags.NoWrap;
            return f;
        }

        // Single line of text; x is the left edge (or right edge / centre for other alignments),
        // y the top of the line box. Long text is cut with an ellipsis at maxWidth.
        protected void Label(Graphics g, string s, Font f, Color c, float x, float y, float maxWidth, StringAlignment align)
        {
            var fmt = align == StringAlignment.Far ? TypoRight : align == StringAlignment.Center ? TypoCenter : Typo;
            float left = align == StringAlignment.Far ? x - maxWidth : align == StringAlignment.Center ? x - maxWidth / 2 : x;
            using (var b = new SolidBrush(c))
            {
                if (HasEmoji(s)) LabelWithEmoji(g, s, f, b, left, y, maxWidth, align);
                else g.DrawString(s, f, b, new RectangleF(left, y, maxWidth, f.GetHeight() * 1.3f), fmt);
            }
        }

        // ---- emoji (calendar titles, reminders): GDI+ has no font fallback, so Segoe UI draws them as
        // boxes. Emoji runs are drawn in Segoe UI Emoji (single-color glyphs in the text color), on the
        // same baseline as the text around them.

        static bool IsEmojiAt(string s, int i)
        {
            char ch = s[i];
            if (char.IsHighSurrogate(ch)) return true; // everything beyond U+FFFF that people type is emoji
            return (ch >= 0x2300 && ch <= 0x23FF) || (ch >= 0x2600 && ch <= 0x27BF) || (ch >= 0x2B00 && ch <= 0x2BFF) ||
                   ch == 0x3030 || ch == 0x303D || ch == 0x3297 || ch == 0x3299;
        }

        // Joiners and presentation selectors only shape color emoji; drawn on their own they're boxes.
        static bool IsEmojiFormat(char ch) { return ch == 0x200D || ch == 0xFE0E || ch == 0xFE0F || ch == 0x20E3; }

        static bool HasEmoji(string s)
        {
            if (s == null) return false;
            for (int i = 0; i < s.Length; i++) if (IsEmojiAt(s, i) || IsEmojiFormat(s[i])) return true;
            return false;
        }

        Font emojiFont;

        Font EmojiFont(Font like)
        {
            if (emojiFont == null || emojiFont.Size != like.Size)
            {
                if (emojiFont != null) emojiFont.Dispose();
                emojiFont = new Font("Segoe UI Emoji", like.Size, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            return emojiFont;
        }

        // Runs sit side by side, so a run's trailing space must count ("Call mum " + bell).
        static readonly StringFormat RunFormat = MakeRunFormat();

        static StringFormat MakeRunFormat()
        {
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return f;
        }

        static float Ascent(Font f)
        {
            return f.Size * f.FontFamily.GetCellAscent(f.Style) / f.FontFamily.GetEmHeight(f.Style);
        }

        void LabelWithEmoji(Graphics g, string s, Font f, Brush b, float left, float y, float maxWidth, StringAlignment align)
        {
            Font emoji = EmojiFont(f);
            // Split into runs of text and emoji, dropping the invisible shaping characters.
            var runs = new List<KeyValuePair<string, bool>>();
            var cur = new System.Text.StringBuilder();
            bool curEmoji = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (IsEmojiFormat(s[i])) continue;
                bool e = IsEmojiAt(s, i);
                int len = char.IsHighSurrogate(s[i]) && i + 1 < s.Length ? 2 : 1;
                if (cur.Length > 0 && e != curEmoji) { runs.Add(new KeyValuePair<string, bool>(cur.ToString(), curEmoji)); cur.Clear(); }
                curEmoji = e;
                cur.Append(s, i, len);
                i += len - 1;
            }
            if (cur.Length > 0) runs.Add(new KeyValuePair<string, bool>(cur.ToString(), curEmoji));

            Func<KeyValuePair<string, bool>, float> width = r =>
                g.MeasureString(r.Key, r.Value ? emoji : f, PointF.Empty, RunFormat).Width;
            float total = 0;
            foreach (var r in runs) total += width(r);
            float x = left;
            if (total <= maxWidth && align == StringAlignment.Far) x = left + maxWidth - total;
            else if (total <= maxWidth && align == StringAlignment.Center) x = left + (maxWidth - total) / 2;

            float baseline = y + Ascent(f), right = left + maxWidth;
            foreach (var r in runs)
            {
                Font font = r.Value ? emoji : f;
                float w = width(r), top = baseline - Ascent(font);
                if (x + w <= right) { g.DrawString(r.Key, font, b, x, top, RunFormat); x += w; continue; }
                // Doesn't fit: text is cut with an ellipsis in the space left; an emoji is replaced by one.
                if (!r.Value) g.DrawString(r.Key, f, b, new RectangleF(x, top, Math.Max(0, right - x), f.GetHeight() * 1.3f), Typo);
                else g.DrawString("…", f, b, x, baseline - Ascent(f), StringFormat.GenericTypographic);
                break;
            }
        }

        protected void Label(Graphics g, string s, Font f, Color c, float x, float y)
        {
            Label(g, s, f, c, x, y, W, StringAlignment.Near);
        }

        // Wrapped paragraph clipped to a box, with an ellipsis on the last visible line.
        protected void Paragraph(Graphics g, string s, Font f, Color c, RectangleF box)
        {
            using (var fmt = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var b = new SolidBrush(c))
            {
                fmt.Trimming = StringTrimming.EllipsisWord;
                fmt.FormatFlags |= StringFormatFlags.LineLimit;
                g.DrawString(s, f, b, box, fmt);
            }
        }

        protected float TextWidth(Graphics g, string s, Font f)
        {
            return g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic).Width;
        }

        // ---- shared menu: appearance and size ----
        protected override void BuildMenu(PopupMenu menu)
        {
            BuildWidgetMenu(menu);
            menu.AddSeparator();
            var look = menu.AddSubmenu("Appearance");
            string face = Face;
            foreach (var choice in AppearanceChoices)
            {
                string id = choice[0];
                look.AddChoice(choice[1], face == id, delegate { Settings.Set("face", id); Changed(); });
            }
            var size = menu.AddSubmenu("Size");
            int cur = Settings.GetInt("scale", 100);
            for (int i = 0; i < ScaleOptions.Length; i++)
            {
                int pct = ScaleOptions[i];
                size.AddChoice(ScaleNames[i], cur == pct, delegate { Settings.Set("scale", pct); Changed(); });
            }
        }

        protected virtual void BuildWidgetMenu(PopupMenu menu) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var f in fonts.Values) f.Dispose();
                fonts.Clear();
                if (emojiFont != null) { emojiFont.Dispose(); emojiFont = null; }
                if (backdrop != null) { backdrop.Dispose(); backdrop = null; }
            }
            base.Dispose(disposing);
        }
    }
}
