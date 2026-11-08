using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Focus Timer", Description = "A liquid glass ring that counts down")]
public sealed class FocusTimer : CardWidget
{
    static readonly int[] Lengths = { 15, 25, 45, 60 };

    int hoverBtn = 0; // 0 = none, 1 = play, 2 = reset
    int pressedBtn = 0;

    public FocusTimer(Section settings) : base(settings, false) { }

    int LengthSec { get { return Settings.GetInt("length", 25) * 60; } }

    // Running: "ends" holds the UTC end time. Paused: "left" holds the seconds remaining.
    bool Running { get { return Settings.Get("ends", "").Length > 0; } }

    int SecondsLeft
    {
        get
        {
            if (Running)
            {
                long ends;
                if (!long.TryParse(Settings.Get("ends", ""), out ends)) return 0;
                return Math.Max(0, (int)Math.Ceiling((new DateTime(ends, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds));
            }
            return Math.Min(LengthSec, Settings.GetInt("left", LengthSec));
        }
    }

    void Start()
    {
        int left = SecondsLeft;
        if (left == 0) left = LengthSec;
        Settings.Remove("left");
        Settings.Set("ends", DateTime.UtcNow.AddSeconds(left).Ticks);
        Render();
    }

    void Pause()
    {
        int left = SecondsLeft;
        Settings.Remove("ends");
        Settings.Set("left", left);
        Render();
    }

    void Reset()
    {
        Settings.Remove("ends");
        Settings.Remove("left");
        Render();
    }

    protected override int NextFrameDelay()
    {
        if (!Running || !IsHandleCreated) return -1;
        long ends;
        long.TryParse(Settings.Get("ends", ""), out ends);
        double ms = (new DateTime(ends, DateTimeKind.Utc) - DateTime.UtcNow).TotalMilliseconds;
        if (ms <= 0) return 0;
        return (int)(ms % 1000) + 15;
    }

    protected override void Draw(Graphics g)
    {
        if (Running && SecondsLeft == 0)
        {
            Settings.Remove("ends");
            Settings.Set("left", 0);
            if (IsHandleCreated) SystemSounds.Asterisk.Play();
        }
        base.Draw(g);
    }

    PointF RingCenter { get { return new PointF(P(16) + P(46), P(97)); } }
    PointF PlayCenter { get { return new PointF(W - P(16) - P(14), P(80)); } }
    PointF ResetCenter { get { return new PointF(W - P(16) - P(14), P(116)); } }

    static bool Within(Point p, PointF c, float r)
    {
        float dx = p.X - c.X, dy = p.Y - c.Y;
        return dx * dx + dy * dy <= r * r;
    }

    protected override bool IsButton(Point p)
    {
        return Within(p, PlayCenter, P(17)) || Within(p, ResetCenter, P(17));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = Within(e.Location, PlayCenter, P(17)) ? 1 : Within(e.Location, ResetCenter, P(17)) ? 2 : 0;
        if (hit != hoverBtn)
        {
            hoverBtn = hit;
            Render();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverBtn = 0;
        pressedBtn = 0;
        Render();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            int hit = Within(e.Location, PlayCenter, P(17)) ? 1 : Within(e.Location, ResetCenter, P(17)) ? 2 : 0;
            if (hit != 0)
            {
                pressedBtn = hit;
                Render();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (pressedBtn != 0)
        {
            pressedBtn = 0;
            Render();
        }
    }

    protected override void OnWidgetClick(Point p)
    {
        if (Within(p, PlayCenter, P(17))) { if (Running) Pause(); else Start(); }
        else if (Within(p, ResetCenter, P(17))) Reset();
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16);
        int left = SecondsLeft, total = LengthSec;
        bool running = Running;
        string state = left == 0 ? "Done" : running ? "Running" : left == total ? "Ready" : "Paused";

        Label(g, "Focus", F(13, "semibold"), pal.Orange, m, m - P(1));

        // State pill at top right
        float stateW = P(64), stateH = P(20);
        var stateRect = new RectangleF(W - m - stateW, m - P(2), stateW, stateH);
        DrawLiquidGlassWell(g, stateRect, stateH / 2f, IsDark);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            Color stateColor = running ? pal.Orange : pal.Fg2;
            using (var b = new SolidBrush(stateColor))
                g.DrawString(state, F(10, "semibold"), b, stateRect, sf);
        }

        PointF c = RingCenter;
        float r = P(46) - P(4.5f), stroke = P(8);
        var box = new RectangleF(c.X - r, c.Y - r, r * 2, r * 2);

        // Inner circular well behind timer digits
        float innerR = r - stroke / 2f - P(3);
        var innerRect = new RectangleF(c.X - innerR, c.Y - innerR, innerR * 2, innerR * 2);
        DrawLiquidGlassWell(g, innerRect, innerR, IsDark);

        // Circular track groove
        Color trackCol = IsDark ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0);
        using (var track = new Pen(trackCol, stroke)) g.DrawEllipse(track, box);

        // Active countdown arc with luminous gradient and rounded caps
        float sweep = total > 0 ? 360f * left / total : 0;
        if (sweep > 0.5f)
        {
            Color arcCol = running ? Color.FromArgb(255, 149, 0) : Color.FromArgb(255, 180, 64);
            using (var arc = new Pen(arcCol, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(arc, box, -90, sweep);

            // Specular highlight on top edge of arc
            using (var arcRim = new Pen(Color.FromArgb(90, 255, 255, 255), 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                var rimBox = new RectangleF(box.X + 0.8f, box.Y + 0.8f, box.Width - 1.6f, box.Height - 1.6f);
                g.DrawArc(arcRim, rimBox, -90, Math.Min(sweep, 150f));
            }
        }

        string time = (left / 60) + ":" + (left % 60).ToString("00");
        Font tf = F(20, "semibold");
        Label(g, time, tf, pal.Fg, c.X, c.Y - tf.GetHeight() / 2, r * 2, StringAlignment.Center);

        PointF pc = PlayCenter, rc = ResetCenter;
        float br = P(15);
        var playRect = new RectangleF(pc.X - br, pc.Y - br, br * 2, br * 2);
        var resetRect = new RectangleF(rc.X - br, rc.Y - br, br * 2, br * 2);

        bool playHover = hoverBtn == 1, playPress = pressedBtn == 1;
        bool resetHover = hoverBtn == 2, resetPress = pressedBtn == 2;

        // Play Button: Liquid Glass Amber Button
        using (var pPath = RoundRect(playRect, br))
        {
            Color playBg = playPress
                ? Color.FromArgb(240, 230, 110, 20)
                : playHover ? Color.FromArgb(255, 255, 155, 45) : Color.FromArgb(230, 245, 130, 32);
            using (var b = new SolidBrush(playBg)) g.FillPath(b, pPath);

            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
                g.DrawPath(pen, pPath);

            using (var white = new SolidBrush(Color.White))
            {
                if (running)
                {
                    float bw = P(3.2f), bh = P(11);
                    g.FillRectangle(white, pc.X - P(4.6f), pc.Y - bh / 2, bw, bh);
                    g.FillRectangle(white, pc.X + P(1.4f), pc.Y - bh / 2, bw, bh);
                }
                else
                {
                    float s = P(6);
                    g.FillPolygon(white, new[] { new PointF(pc.X - s * 0.7f, pc.Y - s), new PointF(pc.X - s * 0.7f, pc.Y + s), new PointF(pc.X + s, pc.Y) });
                }
            }
        }

        // Reset Button: Liquid Glass Frosted Button
        using (var rPath = RoundRect(resetRect, br))
        {
            Color resetBg = resetPress
                ? (IsDark ? Color.FromArgb(110, 255, 255, 255) : Color.FromArgb(170, 255, 255, 255))
                : resetHover
                    ? (IsDark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(120, 255, 255, 255))
                    : (IsDark ? Color.FromArgb(38, 255, 255, 255) : Color.FromArgb(70, 255, 255, 255));
            using (var b = new SolidBrush(resetBg)) g.FillPath(b, rPath);

            using (var pen = new Pen(IsDark ? Color.FromArgb(resetHover ? 90 : 50, 255, 255, 255) : Color.FromArgb(resetHover ? 160 : 90, 255, 255, 255), 1f))
                g.DrawPath(pen, rPath);

            float ar = P(5.5f);
            using (var pen = new Pen(pal.Fg, P(1.7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, rc.X - ar, rc.Y - ar, ar * 2, ar * 2, -60, 300);
            using (var b = new SolidBrush(pal.Fg))
            {
                float ax = rc.X + ar * (float)Math.Cos(-Math.PI / 3), ay = rc.Y + ar * (float)Math.Sin(-Math.PI / 3), s = P(3.2f);
                g.FillPolygon(b, new[] { new PointF(ax - s, ay - s * 1.2f), new PointF(ax + s * 1.3f, ay - s * 0.2f), new PointF(ax - s * 0.3f, ay + s * 1.2f) });
            }
        }
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        if (Running) menu.Add("Pause", Pause); else menu.Add(SecondsLeft == 0 || SecondsLeft == LengthSec ? "Start" : "Resume", Start);
        menu.Add("Reset", Reset);
        var len = menu.AddSubmenu("Length");
        int cur = Settings.GetInt("length", 25);
        foreach (int each in Lengths)
        {
            int min = each;
            len.AddChoice(min + " Minutes", cur == min, delegate { Settings.Set("length", min); Reset(); });
        }
    }
}
