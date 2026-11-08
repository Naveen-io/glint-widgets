using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "System", Description = "CPU, memory and disk in liquid glass")]
public sealed class SystemWidget : CardWidget
{
    const int History = 30;
    const int SampleMs = 2000;

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    readonly List<float> cpu = new List<float>();
    static readonly List<float> PreviewSeries = new List<float> { 8, 11, 9, 14, 22, 18, 12, 10, 9, 16, 28, 24, 15, 11, 9, 8, 12, 19, 14, 10, 9, 13, 21, 17, 12, 10, 11, 15, 13, 12 };
    long lastIdle, lastTotal;
    double memUsedGb, memTotalGb;
    float diskUsed = -1;
    DateTime diskReadAt;

    public SystemWidget(Section settings) : base(settings, false)
    {
        Sample();
    }

    string Drive { get { return Settings.Get("drive", "C"); } }

    protected override int NextFrameDelay()
    {
        return IsHandleCreated ? SampleMs : -1;
    }

    protected override void Draw(Graphics g)
    {
        if (IsHandleCreated) Sample();
        base.Draw(g);
    }

    void Sample()
    {
        long idle, kernel, user;
        if (GetSystemTimes(out idle, out kernel, out user))
        {
            long total = kernel + user;
            if (lastTotal != 0 && total > lastTotal)
            {
                float load = 100f * (1f - (idle - lastIdle) / (float)(total - lastTotal));
                cpu.Add(Math.Max(0, Math.Min(100, load)));
                if (cpu.Count > History) cpu.RemoveAt(0);
            }
            lastIdle = idle;
            lastTotal = total;
        }

        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
        if (GlobalMemoryStatusEx(ref m))
        {
            memTotalGb = m.ullTotalPhys / 1073741824.0;
            memUsedGb = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
        }

        if (diskUsed < 0 || (DateTime.UtcNow - diskReadAt).TotalSeconds > 60)
        {
            diskReadAt = DateTime.UtcNow;
            try
            {
                var d = new DriveInfo(Drive);
                diskUsed = d.IsReady ? 100f * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize : -1;
            }
            catch (Exception) { diskUsed = -1; }
        }
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16), w = W - m * 2;
        List<float> cpu = this.cpu.Count < 2 && !IsHandleCreated ? PreviewSeries : this.cpu;
        float now = cpu.Count > 0 ? cpu[cpu.Count - 1] : 0;

        Label(g, "CPU Activity", F(11, "semibold"), pal.Fg2, m, m - P(1));
        Label(g, Math.Round(now) + "%", F(24, "bold"), pal.Fg, m - P(1), m + P(12));

        // Activity badge at top right
        float tagW = P(64), tagH = P(20);
        var tagRect = new RectangleF(W - m - tagW, m - P(2), tagW, tagH);
        DrawLiquidGlassWell(g, tagRect, tagH / 2f, IsDark);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            using (var b = new SolidBrush(Color.FromArgb(50, 180, 255)))
                g.DrawString("Realtime", F(9, "semibold"), b, tagRect, sf);
        }

        float top = P(62), h = P(30);
        float max = 40;
        foreach (var v in cpu) max = Math.Max(max, v * 1.25f);

        // Recessed liquid glass chart well
        var chartBox = new RectangleF(m, top - P(2), w, h + P(4));
        DrawLiquidGlassWell(g, chartBox, P(6), IsDark);

        using (var grid = new Pen(IsDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(15, 0, 0, 0), 1))
        {
            for (int i = 1; i <= 3; i++)
                g.DrawLine(grid, m + P(4), top + h * i / 4, m + w - P(4), top + h * i / 4);
        }

        if (cpu.Count > 1)
        {
            var pts = new PointF[cpu.Count];
            float step = (w - P(8)) / (History - 1), x0 = m + P(4) + (w - P(8)) - step * (cpu.Count - 1);
            for (int i = 0; i < cpu.Count; i++)
                pts[i] = new PointF(x0 + i * step, top + h - (cpu[i] / max * (h - P(4))));

            using (var area = new GraphicsPath())
            {
                area.AddLines(pts);
                area.AddLine(pts[pts.Length - 1], new PointF(pts[pts.Length - 1].X, top + h));
                area.AddLine(new PointF(pts[pts.Length - 1].X, top + h), new PointF(pts[0].X, top + h));
                area.CloseFigure();

                var gradRect = new RectangleF(m, top, w, h);
                using (var grad = new LinearGradientBrush(gradRect.Location, new PointF(gradRect.Left, gradRect.Bottom),
                    Color.FromArgb(80, 0, 160, 255), Color.FromArgb(5, 0, 120, 255)))
                {
                    g.FillPath(grad, area);
                }
            }

            using (var pen = new Pen(Color.FromArgb(255, 60, 180, 255), P(1.5f)) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, pts);

            // Apex indicator dot with specular halo
            var last = pts[pts.Length - 1];
            using (var halo = new SolidBrush(Color.FromArgb(80, 60, 180, 255)))
                g.FillEllipse(halo, last.X - P(4), last.Y - P(4), P(8), P(8));
            using (var core = new SolidBrush(Color.White))
                g.FillEllipse(core, last.X - P(2), last.Y - P(2), P(4), P(4));
        }

        Meter(g, pal, "Memory", memUsedGb.ToString("0.0") + " of " + Math.Round(memTotalGb) + " GB",
            memTotalGb > 0 ? (float)(memUsedGb / memTotalGb) : 0, Color.FromArgb(175, 82, 222), P(102));
        Meter(g, pal, "Disk " + Drive + ":", diskUsed < 0 ? "—" : Math.Round(diskUsed) + "% used",
            diskUsed < 0 ? 0 : diskUsed / 100f, Color.FromArgb(255, 149, 0), P(128));
    }

    void Meter(Graphics g, Palette pal, string name, string value, float fraction, Color color, float y)
    {
        float m = P(16), w = W - m * 2;
        Label(g, name, F(11, "semibold"), pal.Fg, m, y);
        Label(g, value, F(10, "regular"), pal.Fg2, W - m, y, w * 0.6f, StringAlignment.Far);
        float by = y + P(15), bh = P(5);
        DrawLiquidGlassProgressBar(g, new RectangleF(m, by, w, bh), fraction, color, IsDark);
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        var drives = menu.AddSubmenu("Drive");
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed) continue;
            string letter = d.Name.Substring(0, 1);
            drives.AddChoice(letter + ":", letter == Drive, delegate { Settings.Set("drive", letter); diskUsed = -1; Changed(); });
        }
    }
}
