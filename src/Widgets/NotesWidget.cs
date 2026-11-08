using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

// Small: one pinned note in liquid glass. Click it and type straight into it; click away (or press Esc) to
// save. Like Apple Notes, the first line is the title.
[DeskWidget(Name = "Notes", Description = "One liquid glass note, click to edit")]
public sealed class NotesWidget : CardWidget
{
    NoteEditor editor;

    public NotesWidget(Section settings) : base(settings, false) { }

    // Older versions stored a separate title and body: fold them into one text.
    string NoteText
    {
        get
        {
            string text = Settings.GetText("text", null);
            if (text != null) return text;
            string title = Settings.GetText("title", null), body = Settings.GetText("body", null);
            return title == null && body == null ? "" : (title ?? "") + "\n" + (body ?? "");
        }
    }

    // Where the text sits, below the "Notes" header and above the "Edited" line.
    RectangleF TextArea
    {
        get
        {
            float m = P(16);
            return new RectangleF(m, m + P(24), W - m * 2, H - m * 2 - P(24) - P(20));
        }
    }

    void BeginEdit()
    {
        if (editor != null) { editor.Activate(); return; }
        RectangleF a = TextArea;
        float pad = P(8);
        var screen = new Rectangle(Left + (int)(a.X + pad), Top + (int)(a.Y + pad), (int)(a.Width - pad * 2), (int)(a.Height - pad * 2));
        Color back = RenderedColor(new Point((int)(a.X + a.Width - 5), (int)(a.Bottom - 5)));
        if (back.A < 200) back = IsDark ? Color.FromArgb(28, 28, 32) : Color.FromArgb(240, 240, 245);

        editor = new NoteEditor(screen, NoteText.Replace("\n", "\r\n"), F(12, "regular"), back, Pal.Fg);
        editor.Done += text =>
        {
            editor = null;
            text = text.Replace("\r\n", "\n").TrimEnd();
            if (text != NoteText.TrimEnd())
            {
                Settings.SetText("text", text);
                Settings.Remove("title");
                Settings.Remove("body");
                Settings.Set("edited", DateTime.Now.Ticks);
            }
            Render();
        };
        editor.Show();
    }

    // A click (not a drag) anywhere starts typing; the note stays draggable.
    protected override void OnWidgetClick(Point p) { BeginEdit(); }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        menu.Add("Edit Note", BeginEdit);
        menu.Add("Clear Note", delegate
        {
            Settings.SetText("text", "");
            Settings.Remove("title");
            Settings.Remove("body");
            Settings.Set("edited", DateTime.Now.Ticks);
            Render();
        });
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16), w = W - m * 2;

        // Top header: icon glyph and title
        DrawNoteGlyph(g, pal.Yellow, m, m + P(1), P(13));
        Label(g, "Notes", F(13, "semibold"), pal.Yellow, m + P(18), m - P(1));

        // Word count badge at top right
        string raw = NoteText;
        int words = string.IsNullOrEmpty(raw) ? 0 : raw.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        string wordTag = words == 1 ? "1 word" : words + " words";
        float tagW = P(62), tagH = P(20);
        var tagRect = new RectangleF(W - m - tagW, m - P(2), tagW, tagH);
        DrawLiquidGlassWell(g, tagRect, tagH / 2f, IsDark);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            using (var b = new SolidBrush(pal.Fg2))
                g.DrawString(wordTag, F(9, "semibold"), b, tagRect, sf);
        }

        // Recessed liquid glass note well
        RectangleF area = TextArea;
        DrawLiquidGlassWell(g, area, P(8), IsDark);

        float pad = P(8);
        var textRect = new RectangleF(area.X + pad, area.Y + pad, area.Width - pad * 2, area.Height - pad * 2);

        if (raw.Trim().Length == 0)
        {
            Label(g, "Click to write…", F(12, "regular"), pal.Fg2, textRect.X, textRect.Y + P(2), textRect.Width, StringAlignment.Near);
        }
        else
        {
            int nl = raw.IndexOf('\n');
            string title = nl < 0 ? raw : raw.Substring(0, nl);
            string body = nl < 0 ? "" : raw.Substring(nl + 1).Trim();

            Label(g, title, F(13, "bold"), pal.Fg, textRect.X, textRect.Y, textRect.Width, StringAlignment.Near);
            if (body.Length > 0)
            {
                var bodyRect = new RectangleF(textRect.X, textRect.Y + P(20), textRect.Width, textRect.Height - P(20));
                Paragraph(g, body, F(11, "regular"), pal.Fg2, bodyRect);
            }
        }

        // Footer: Edited timestamp
        long ticks;
        if (long.TryParse(Settings.Get("edited", ""), out ticks))
        {
            var t = new DateTime(ticks);
            string stamp = "Edited " + (t.Date == DateTime.Today ? t.ToString("t") : t.ToString("d MMM"));
            Label(g, stamp, F(10, "regular"), pal.Fg2, m + P(4), H - m - P(14));
        }
    }

    static void DrawNoteGlyph(Graphics g, Color c, float x, float y, float s)
    {
        var rect = new RectangleF(x, y, s, s);
        using (var path = RoundRect(rect, s * 0.25f))
        {
            using (var b = new SolidBrush(Color.FromArgb(230, 245, 185, 30)))
                g.FillPath(b, path);

            using (var pen = new Pen(Color.FromArgb(140, 255, 255, 255), 1f))
                g.DrawPath(pen, path);

            using (var linePen = new Pen(Color.FromArgb(180, 40, 30, 0), Math.Max(1.2f, s * 0.12f)))
            {
                g.DrawLine(linePen, x + s * 0.25f, y + s * 0.38f, x + s * 0.75f, y + s * 0.38f);
                g.DrawLine(linePen, x + s * 0.25f, y + s * 0.62f, x + s * 0.6f, y + s * 0.62f);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && editor != null) editor.Close();
        base.Dispose(disposing);
    }

    // Borderless text box laid exactly over the note's text, in the note's own colours, so typing
    // looks like writing on the widget. (Layered widget windows can't host a real text box.)
    public sealed class NoteEditor : Form
    {
        readonly TextBox box;
        bool finished;
        public event Action<string> Done;

        public NoteEditor(Rectangle screenRect, string text, Font font, Color back, Color fore)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = screenRect;
            BackColor = back;
            box = new TextBox
            {
                Multiline = true, AcceptsReturn = true, WordWrap = true, ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.None, BackColor = back, ForeColor = fore, Font = font,
                Dock = DockStyle.Fill, Text = text
            };
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Finish(); }
                else if (e.Control && e.KeyCode == Keys.A) { e.SuppressKeyPress = true; box.SelectAll(); }
            };
            Controls.Add(box);
            Deactivate += delegate { Finish(); }; // clicking anywhere else saves
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: not in the taskbar or Alt+Tab
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            box.Focus();
            box.SelectionStart = box.TextLength;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            if (Done != null) Done(box.Text);
            BeginInvoke((Action)Close);
        }
    }
}
