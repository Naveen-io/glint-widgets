using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Clipboard", Description = "Recent text clips in liquid glass")]
public sealed class ClipboardWidget : CardWidget
{
    const int MaxClips = 3;
    readonly List<string> history = new List<string>();
    string lastSeen = "";
    int feedbackIndex = -1;
    int hoverIndex = -1;
    int pressedIndex = -1;
    readonly System.Windows.Forms.Timer pollTimer = new System.Windows.Forms.Timer { Interval = 1500 };

    public ClipboardWidget(Section settings) : base(settings, false)
    {
        string saved = Settings.GetText("history", null);
        if (!string.IsNullOrEmpty(saved))
        {
            string[] parts = saved.Split(new[] { "\n---\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts) history.Add(p);
        }

        pollTimer.Tick += delegate { CheckClipboard(); };
    }

    protected override void OnStart()
    {
        base.OnStart();
        pollTimer.Start();
        CheckClipboard();
    }

    void CheckClipboard()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                if (text.Length > 0 && text != lastSeen)
                {
                    lastSeen = text;
                    history.Remove(text);
                    history.Insert(0, text);
                    if (history.Count > MaxClips) history.RemoveAt(history.Count - 1);
                    SaveHistory();
                    Render();
                }
            }
        }
        catch { }
    }

    void SaveHistory()
    {
        Settings.SetText("history", string.Join("\n---\n", history.ToArray()));
    }

    RectangleF ClipBox(int i)
    {
        float m = P(14);
        float h = P(36);
        float gap = P(6);
        return new RectangleF(m, m + P(24) + i * (h + gap), W - m * 2, h);
    }

    protected override bool IsButton(Point p)
    {
        for (int i = 0; i < history.Count; i++)
            if (ClipBox(i).Contains(p)) return true;
        return false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = -1;
        for (int i = 0; i < history.Count; i++)
        {
            if (ClipBox(i).Contains(e.Location)) { hit = i; break; }
        }
        if (hit != hoverIndex)
        {
            hoverIndex = hit;
            Render();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverIndex = -1;
        pressedIndex = -1;
        Render();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            for (int i = 0; i < history.Count; i++)
            {
                if (ClipBox(i).Contains(e.Location))
                {
                    pressedIndex = i;
                    Render();
                    return;
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (pressedIndex >= 0)
        {
            pressedIndex = -1;
            Render();
        }
    }

    protected override void OnWidgetClick(Point p)
    {
        for (int i = 0; i < history.Count; i++)
        {
            if (ClipBox(i).Contains(p))
            {
                CopyItem(i);
                return;
            }
        }
    }

    void CopyItem(int index)
    {
        if (index < 0 || index >= history.Count) return;
        try
        {
            string t = history[index];
            lastSeen = t;
            Clipboard.SetText(t);
            feedbackIndex = index;
            Render();
            Anim.Run(1f, 0f, 600, delegate { }, delegate
            {
                feedbackIndex = -1;
                Render();
            });
        }
        catch { }
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(14);
        Label(g, "Clipboard", F(13, "semibold"), pal.Purple, m, m - P(1));

        // Clips badge at top right
        string tag = history.Count + (history.Count == 1 ? " clip" : " clips");
        float tagW = P(64), tagH = P(20);
        var tagRect = new RectangleF(W - m - tagW, m - P(2), tagW, tagH);
        DrawLiquidGlassWell(g, tagRect, tagH / 2f, IsDark);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            using (var b = new SolidBrush(pal.Fg2))
                g.DrawString(tag, F(9, "semibold"), b, tagRect, sf);
        }

        if (history.Count == 0)
        {
            var emptyRect = new RectangleF(m, m + P(28), W - m * 2, H - m * 2 - P(28));
            DrawLiquidGlassWell(g, emptyRect, P(8), IsDark);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                using (var b = new SolidBrush(pal.Fg2))
                    g.DrawString("Copy text to stage clips here…", F(11, "regular"), b, emptyRect, sf);
            }
            return;
        }

        for (int i = 0; i < history.Count; i++)
        {
            RectangleF b = ClipBox(i);
            bool copied = feedbackIndex == i;
            bool hover = i == hoverIndex;
            bool pressed = i == pressedIndex;

            using (var path = RoundRect(b, P(6)))
            {
                Color tileFill;
                if (copied)
                {
                    tileFill = Color.FromArgb(100, 175, 82, 222);
                }
                else if (pressed)
                {
                    tileFill = IsDark ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(160, 255, 255, 255);
                }
                else if (hover)
                {
                    tileFill = IsDark ? Color.FromArgb(55, 255, 255, 255) : Color.FromArgb(110, 255, 255, 255);
                }
                else
                {
                    tileFill = IsDark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(60, 255, 255, 255);
                }

                using (var brush = new SolidBrush(tileFill)) g.FillPath(brush, path);

                // Hover Light Bloom
                if (hover && !pressed && !copied)
                {
                    try
                    {
                        using (var glow = new PathGradientBrush(path))
                        {
                            glow.CenterColor = Color.FromArgb(30, 255, 255, 255);
                            glow.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                            g.FillPath(glow, path);
                        }
                    }
                    catch { }
                }

                // Specular Rim
                Color rim = copied
                    ? Color.FromArgb(180, 255, 255, 255)
                    : (IsDark
                        ? Color.FromArgb(hover ? 85 : 40, 255, 255, 255)
                        : Color.FromArgb(hover ? 150 : 80, 255, 255, 255));
                using (var pen = new Pen(rim, 1f))
                    g.DrawPath(pen, path);
            }

            string text = history[i].Replace("\r\n", " ").Replace("\n", " ");
            if (text.Length > 45) text = text.Substring(0, 42) + "…";

            if (copied)
            {
                Label(g, "✓ Copied to clipboard", F(11, "semibold"), Color.White, b.X + P(10), b.Y + P(10), b.Width - P(20), StringAlignment.Near);
            }
            else
            {
                Label(g, text, F(11, "regular"), pal.Fg, b.X + P(10), b.Y + P(10), b.Width - P(20), StringAlignment.Near);
            }
        }
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        if (history.Count > 0)
        {
            menu.Add("Copy Top Item", delegate { CopyItem(0); });
            menu.Add("Clear History", delegate
            {
                history.Clear();
                lastSeen = "";
                SaveHistory();
                Render();
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) pollTimer.Dispose();
        base.Dispose(disposing);
    }
}
