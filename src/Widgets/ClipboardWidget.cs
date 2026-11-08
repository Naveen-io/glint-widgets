using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Clipboard", Description = "Recent text clips ready to re-copy in one click")]
public sealed class ClipboardWidget : CardWidget
{
    const int MaxClips = 3;
    readonly List<string> history = new List<string>();
    string lastSeen = "";
    int feedbackIndex = -1;
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
        Label(g, history.Count + " clips", F(11, "regular"), pal.Fg2, W - m, m + P(1), P(80), StringAlignment.Far);

        if (history.Count == 0)
        {
            Label(g, "Copy text to stage clips here…", F(12, "regular"), pal.Fg2,
                m, m + P(40), W - m * 2, StringAlignment.Center);
            return;
        }

        for (int i = 0; i < history.Count; i++)
        {
            RectangleF b = ClipBox(i);
            bool copied = feedbackIndex == i;
            Color bg = copied ? Color.FromArgb(60, pal.Purple) : pal.Fill;
            FillRound(g, bg, b, P(6));

            string text = history[i].Replace("\r\n", " ").Replace("\n", " ");
            if (text.Length > 45) text = text.Substring(0, 42) + "…";

            if (copied)
            {
                Label(g, "✓ Copied to clipboard", F(11, "semibold"), pal.Purple, b.X + P(8), b.Y + P(10), b.Width - P(16), StringAlignment.Near);
            }
            else
            {
                Label(g, text, F(11, "regular"), pal.Fg, b.X + P(8), b.Y + P(10), b.Width - P(16), StringAlignment.Near);
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
