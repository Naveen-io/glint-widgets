using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Quick Launcher", Description = "Instant access to system utilities in liquid glass")]
public sealed class LauncherWidget : CardWidget
{
    struct LaunchItem
    {
        public string Title;
        public string Target;
        public char Glyph;
        public Color Color;
    }

    readonly LaunchItem[] items;
    int hoverIdx = -1;
    int pressedIdx = -1;

    public LauncherWidget(Section settings) : base(settings, false)
    {
        items = new[]
        {
            new LaunchItem { Title = "Terminal", Target = "wt.exe", Glyph = '\uE756', Color = Color.FromArgb(76, 175, 80) },
            new LaunchItem { Title = "Task Mgr", Target = "taskmgr.exe", Glyph = '\uE7F4', Color = Color.FromArgb(33, 150, 243) },
            new LaunchItem { Title = "Explorer", Target = "explorer.exe", Glyph = '\uEC50', Color = Color.FromArgb(255, 152, 0) },
            new LaunchItem { Title = "Settings", Target = "ms-settings:", Glyph = '\uE713', Color = Color.FromArgb(156, 39, 176) },
        };
    }

    RectangleF ItemBox(int i)
    {
        float m = P(16);
        float w = (W - m * 2 - P(10)) / 2;
        float h = (H - m * 2 - P(24) - P(10)) / 2;
        int col = i % 2, row = i / 2;
        return new RectangleF(m + col * (w + P(10)), m + P(22) + row * (h + P(10)), w, h);
    }

    protected override bool IsButton(Point p)
    {
        for (int i = 0; i < items.Length; i++)
            if (ItemBox(i).Contains(p)) return true;
        return false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = -1;
        for (int i = 0; i < items.Length; i++)
        {
            if (ItemBox(i).Contains(e.Location)) { hit = i; break; }
        }
        if (hit != hoverIdx)
        {
            hoverIdx = hit;
            Render();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverIdx = -1;
        pressedIdx = -1;
        Render();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (ItemBox(i).Contains(e.Location))
                {
                    pressedIdx = i;
                    Render();
                    return;
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (pressedIdx >= 0)
        {
            pressedIdx = -1;
            Render();
        }
    }

    protected override void OnWidgetClick(Point p)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (ItemBox(i).Contains(p))
            {
                Launch(items[i].Target);
                return;
            }
        }
    }

    void Launch(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Toast.Show("Launch Failed", ex.Message);
        }
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16);
        Label(g, "Launcher", F(13, "semibold"), pal.Blue, m, m - P(1));

        // Quick Access tag
        float tagW = P(78), tagH = P(20);
        var tagRect = new RectangleF(W - m - tagW, m - P(2), tagW, tagH);
        DrawLiquidGlassWell(g, tagRect, tagH / 2f, IsDark);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            using (var b = new SolidBrush(pal.Fg2))
                g.DrawString("Quick Access", F(9, "semibold"), b, tagRect, sf);
        }

        for (int i = 0; i < items.Length; i++)
        {
            RectangleF b = ItemBox(i);
            bool hover = i == hoverIdx;
            bool pressed = i == pressedIdx;

            // Liquid Glass Tile
            using (var path = RoundRect(b, P(8)))
            {
                Color tileFill = pressed
                    ? (IsDark ? Color.FromArgb(95, 255, 255, 255) : Color.FromArgb(160, 255, 255, 255))
                    : hover
                        ? (IsDark ? Color.FromArgb(58, 255, 255, 255) : Color.FromArgb(115, 255, 255, 255))
                        : (IsDark ? Color.FromArgb(32, 255, 255, 255) : Color.FromArgb(65, 255, 255, 255));
                using (var brush = new SolidBrush(tileFill)) g.FillPath(brush, path);

                // Hover Light Bloom
                if (hover && !pressed)
                {
                    try
                    {
                        using (var glow = new PathGradientBrush(path))
                        {
                            glow.CenterColor = Color.FromArgb(35, 255, 255, 255);
                            glow.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                            g.FillPath(glow, path);
                        }
                    }
                    catch { }
                }

                // Specular Rim
                Color rim = IsDark
                    ? Color.FromArgb(hover ? 90 : 45, 255, 255, 255)
                    : Color.FromArgb(hover ? 160 : 85, 255, 255, 255);
                using (var pen = new Pen(rim, 1f))
                    g.DrawPath(pen, path);
            }

            // App Icon Circle with subtle gradient and glass border
            float iconS = P(22);
            float ix = b.Left + P(8), iy = b.Top + (b.Height - iconS) / 2;
            var iconBox = new RectangleF(ix, iy, iconS, iconS);
            using (var iconPath = new GraphicsPath())
            {
                iconPath.AddEllipse(iconBox);
                using (var ib = new SolidBrush(items[i].Color)) g.FillPath(ib, iconPath);
                using (var ipen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawPath(ipen, iconPath);
            }

            // Icon glyph
            TextRenderer.DrawText(g, items[i].Glyph.ToString(), Theme.Icons,
                new Rectangle((int)ix, (int)iy, (int)iconS, (int)iconS),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // Label
            float tx = ix + iconS + P(8);
            Label(g, items[i].Title, F(11, "semibold"), pal.Fg, tx, b.Top + (b.Height - P(14)) / 2, b.Right - tx - P(4), StringAlignment.Near);
        }
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        foreach (var item in items)
        {
            var target = item.Target;
            menu.Add("Launch " + item.Title, delegate { Launch(target); });
        }
    }
}
