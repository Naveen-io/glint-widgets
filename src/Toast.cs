using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DeskWidgets
{
    sealed class Toast : Form
    {
        static Toast showing;
        static int Px(float v) { return WidgetWindow.Px(v); }
        static readonly int Shadow = Px(10);

        readonly LayeredSurface surface;
        readonly Timer autoDismiss = new Timer { Interval = 4000 };
        readonly int targetY;
        byte fadeOpacity;
        Anim anim;

        Toast(string title, string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;

            int bodyW = Px(280), bodyH = Px(64);
            Size = new Size(bodyW + Shadow * 2, bodyH + Shadow * 2);
            surface = new LayeredSurface(Width, Height);

            var g = surface.Graphics;
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var body = new RectangleF(Shadow, Shadow, bodyW, bodyH);
            float radius = Px(12);
            for (int i = Shadow; i >= 1; i--)
            {
                float t = 1f - i / (float)(Shadow + 1);
                var r = body;
                r.Inflate(i * 0.7f, i * 0.7f);
                r.Offset(0, Px(2));
                Theme.FillRound(g, Color.FromArgb((int)(12 * t * t), 0, 0, 0), r, radius + i);
            }
            using (var path = Theme.RoundRect(body, radius))
            {
                using (var fill = new SolidBrush(Theme.MenuBg)) g.FillPath(fill, path);
                using (var edge = new Pen(Theme.MenuEdge, 1f)) g.DrawPath(edge, path);
            }

            var fmt = new StringFormat(StringFormat.GenericTypographic) { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using (var brush = new SolidBrush(Theme.Label))
                g.DrawString(title, Theme.BodyEmphasized, brush, new RectangleF(Shadow + Px(14), Shadow + Px(12), bodyW - Px(28), Px(20)), fmt);
            using (var brush = new SolidBrush(Theme.Label2))
                g.DrawString(message, Theme.Footnote, brush, new RectangleF(Shadow + Px(14), Shadow + Px(34), bodyW - Px(28), Px(20)), fmt);
            fmt.Dispose();

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            targetY = wa.Bottom - Height - Px(8);
            Location = new Point(wa.Right - Width - Px(8), wa.Bottom);

            autoDismiss.Tick += delegate { Dismiss(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            surface.Present(Handle, 0);
        }

        void SlideIn()
        {
            int startY = Location.Y;
            int end = targetY;
            anim = Anim.Run(startY, end, 300, delegate(float v)
            {
                if (IsDisposed) return;
                Location = new Point(Location.X, (int)v);
                float progress = (startY - v) / Math.Max(1, startY - end);
                fadeOpacity = (byte)Math.Min(255, 255 * progress);
                surface.Present(Handle, fadeOpacity);
            }, delegate { autoDismiss.Start(); });
        }

        void Dismiss()
        {
            autoDismiss.Stop();
            if (anim != null) anim.Cancel();
            anim = Anim.Run(fadeOpacity, 0f, 200, delegate(float v)
            {
                if (IsDisposed) return;
                fadeOpacity = (byte)v;
                surface.Present(Handle, fadeOpacity);
            }, delegate
            {
                if (!IsDisposed) { Close(); Dispose(); }
                if (showing == this) showing = null;
            });
        }

        protected override void OnMouseClick(MouseEventArgs e) { Dismiss(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { autoDismiss.Dispose(); surface.Dispose(); }
            base.Dispose(disposing);
        }

        public static void Show(string title, string message)
        {
            if (showing != null)
            {
                try { if (!showing.IsDisposed) { showing.Close(); showing.Dispose(); } }
                catch (Exception) { }
                showing = null;
            }
            var t = new Toast(title, message);
            showing = t;
            t.Show();
            t.SlideIn();
        }
    }
}
