using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskWidgets
{
    // The Liquid Glass backdrop: widgets sample the desktop wallpaper behind them, blurred.
    // Kept as an optimized 1/4-scale copy of the desktop wallpaper for silky smooth refraction
    // with zero banding and zero tiling artifacts. Rebuilt only when the wallpaper or displays change.
    static class DesktopBackdrop
    {
        const int Shrink = 4;
        static Bitmap map;
        static Rectangle virt;
        static bool built;
        static bool building;
        public static event Action BackdropChanged;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SystemParametersInfo(int action, int param, StringBuilder value, int winIni);
        [DllImport("user32.dll")] static extern int GetSysColor(int index);
        const int SPI_GETDESKWALLPAPER = 0x73, COLOR_DESKTOP = 1;

        public static void Invalidate()
        {
            if (map != null) { map.Dispose(); map = null; }
            built = false;
        }

        // A blurred copy of the wallpaper behind screenRect, at the given size. Caller disposes it.
        // An empty rect (library previews) gets a neutral stand-in.
        public static Bitmap Blurred(Size size, Rectangle screenRect)
        {
            var bmp = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
            bmp.SetResolution(96, 96);
            using (var g = Graphics.FromImage(bmp))
            {
                if (!screenRect.IsEmpty && !built && !building)
                {
                    building = true;
                    RebuildAsync(delegate(Bitmap b, Rectangle r)
                    {
                        App.RunOnUi(delegate
                        {
                            Apply(b, r);
                            building = false;
                            if (BackdropChanged != null) BackdropChanged();
                        });
                    });
                }
                if (screenRect.IsEmpty || map == null)
                {
                    using (var b = new LinearGradientBrush(new Point(0, 0), new Point(bmp.Width, bmp.Height),
                        Color.FromArgb(35, 40, 50), Color.FromArgb(20, 22, 28)))
                    {
                        g.FillRectangle(b, 0, 0, bmp.Width, bmp.Height);
                    }
                    return bmp;
                }

                float sx = (screenRect.X - virt.X) / (float)Shrink;
                float sy = (screenRect.Y - virt.Y) / (float)Shrink;
                float sw = screenRect.Width / (float)Shrink;
                float sh = screenRect.Height / (float)Shrink;

                // Clamp strictly to map bounds so no out-of-bounds artifact or edge wrap can occur
                float srcX = Math.Max(0, Math.Min(map.Width - 1, sx));
                float srcY = Math.Max(0, Math.Min(map.Height - 1, sy));
                float srcW = Math.Max(1, Math.Min(map.Width - srcX, sw));
                float srcH = Math.Max(1, Math.Min(map.Height - srcY, sh));

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var attrs = new ImageAttributes())
                {
                    attrs.SetWrapMode(WrapMode.Clamp); // Clamp strictly prevents horizontal/vertical tiling stripes
                    g.DrawImage(map, new Rectangle(0, 0, bmp.Width, bmp.Height), srcX, srcY, srcW, srcH, GraphicsUnit.Pixel, attrs);
                }
            }
            return bmp;
        }

        // Build on a ThreadPool thread. done(newMap, newVirt) fires on the pool thread;
        // caller marshals to UI thread before calling Apply.
        public static void RebuildAsync(Action<Bitmap, Rectangle> done)
        {
            var virtRect = SystemInformation.VirtualScreen;
            var screens = Screen.AllScreens;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap newMap = null;
                try { newMap = BuildCore(virtRect, screens); }
                catch { }
                done(newMap, virtRect);
            });
        }

        // Swap in a freshly built backdrop. Call on UI thread only.
        public static void Apply(Bitmap newMap, Rectangle newVirt)
        {
            if (map != null) map.Dispose();
            map = newMap;
            virt = newVirt;
            built = true;
        }

        static Bitmap BuildCore(Rectangle virtRect, Screen[] screens)
        {
            int bw = Math.Max(1, virtRect.Width / Shrink);
            int bh = Math.Max(1, virtRect.Height / Shrink);
            var bmp = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
            bmp.SetResolution(96, 96);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(ColorTranslator.FromWin32(GetSysColor(COLOR_DESKTOP)));

                string path = FindWallpaper();
                if (path == null) return bmp;

                int style = 10, tile = 0;
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    if (k != null)
                    {
                        int.TryParse(k.GetValue("WallpaperStyle", "10") as string, out style);
                        int.TryParse(k.GetValue("TileWallpaper", "0") as string, out tile);
                    }

                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                byte[] bytes = ReadWithRetry(path);
                if (bytes == null) return bmp;

                try
                {
                    using (var ms = new MemoryStream(bytes))
                    using (var img = Image.FromStream(ms, false, false))
                    {
                        if (style == 22) DrawStyled(g, img, virtRect, virtRect, 10);
                        else foreach (var s in screens) DrawStyled(g, img, s.Bounds, virtRect, tile == 1 ? 10 : style);
                    }
                }
                catch { }
            }
            return bmp;
        }

        // TranscodedWallpaper is written before the registry updates, so prefer it.
        static string FindWallpaper()
        {
            var sb = new StringBuilder(520);
            bool hasSpi = SystemParametersInfo(SPI_GETDESKWALLPAPER, sb.Capacity, sb, 0);
            string spiPath = hasSpi ? sb.ToString() : "";

            string transcoded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (File.Exists(transcoded)) return transcoded;

            if (spiPath.Length > 0 && File.Exists(spiPath)) return spiPath;
            return null;
        }

        static byte[] ReadWithRetry(string path)
        {
            for (int i = 0; i < 3; i++)
            {
                try { return File.ReadAllBytes(path); }
                catch (IOException) { if (i < 2) Thread.Sleep(30); }
            }
            return null;
        }

        // WallpaperStyle: 10 fill, 6 fit, 2 stretch, 0 centre (tile is treated as fill).
        static void DrawStyled(Graphics g, Image img, Rectangle screen, Rectangle virtRect, int style)
        {
            float sx = screen.Width / (float)img.Width, sy = screen.Height / (float)img.Height;
            float scale = style == 6 ? Math.Min(sx, sy) : style == 0 ? 1f : Math.Max(sx, sy);
            float w = style == 2 ? screen.Width : img.Width * scale, h = style == 2 ? screen.Height : img.Height * scale;
            float x = screen.X + (screen.Width - w) / 2 - virtRect.X, y = screen.Y + (screen.Height - h) / 2 - virtRect.Y;
            var state = g.Save();
            g.SetClip(new RectangleF((screen.X - virtRect.X) / (float)Shrink, (screen.Y - virtRect.Y) / (float)Shrink, screen.Width / (float)Shrink, screen.Height / (float)Shrink));
            g.DrawImage(img, new RectangleF(x / Shrink, y / Shrink, w / Shrink, h / Shrink),
                             new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
            g.Restore(state);
        }
    }
}
