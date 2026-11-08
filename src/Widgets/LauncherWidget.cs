using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Quick Launcher", Description = "Instant access to system utilities and apps")]
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
        Label(g, "Quick Access", F(11, "regular"), pal.Fg2, W - m, m + P(1), P(100), StringAlignment.Far);

        for (int i = 0; i < items.Length; i++)
        {
            RectangleF b = ItemBox(i);
            FillRound(g, pal.Fill, b, P(8));

            // Icon circle
            float iconS = P(22);
            float ix = b.Left + P(8), iy = b.Top + (b.Height - iconS) / 2;
            FillCircle(g, items[i].Color, ix + iconS / 2, iy + iconS / 2, iconS / 2);

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
