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
    // The Liquid Glass backdrop: widgets sample the desktop wallpaper behind them at 1:1 native resolution.
    // Preserves razor-sharp quality, vibrant contrast, and applies a silky-smooth soft optical blur.
    static class DesktopBackdrop
    {
        const int Shrink = 1;
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

        // A pristine blurred copy of the wallpaper behind screenRect, at the given size. Caller disposes it.
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

                float sx = screenRect.X - virt.X;
                float sy = screenRect.Y - virt.Y;
                float sw = screenRect.Width;
                float sh = screenRect.Height;

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

            // Apply soft optical refraction blur
            SoftBlur(bmp, 2);

            return bmp;
        }

        // Fast separable 2-pass soft blur operating directly on 32bpp PARGB bytes in memory
        static void SoftBlur(Bitmap bmp, int radius)
        {
            if (radius <= 0) return;
            int w = bmp.Width, h = bmp.Height;
            if (w < 2 || h < 2) return;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int stride = data.Stride;
                byte[] src = new byte[stride * h];
                byte[] dst = new byte[stride * h];
                Marshal.Copy(data.Scan0, src, 0, src.Length);

                // Horizontal pass
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0, count = 0;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int px = x + k;
                            if (px < 0) px = 0;
                            else if (px >= w) px = w - 1;
                            int idx = row + px * 4;
                            b += src[idx];
                            g += src[idx + 1];
                            r += src[idx + 2];
                            a += src[idx + 3];
                            count++;
                        }
                        int outIdx = row + x * 4;
                        dst[outIdx] = (byte)(b / count);
                        dst[outIdx + 1] = (byte)(g / count);
                        dst[outIdx + 2] = (byte)(r / count);
                        dst[outIdx + 3] = (byte)(a / count);
                    }
                }

                // Vertical pass
                for (int x = 0; x < w; x++)
                {
                    int col = x * 4;
                    for (int y = 0; y < h; y++)
                    {
                        int r = 0, g = 0, b = 0, a = 0, count = 0;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int py = y + k;
                            if (py < 0) py = 0;
                            else if (py >= h) py = h - 1;
                            int idx = py * stride + col;
                            b += dst[idx];
                            g += dst[idx + 1];
                            r += dst[idx + 2];
                            a += dst[idx + 3];
                            count++;
                        }
                        int outIdx = y * stride + col;
                        src[outIdx] = (byte)(b / count);
                        src[outIdx + 1] = (byte)(g / count);
                        src[outIdx + 2] = (byte)(r / count);
                        src[outIdx + 3] = (byte)(a / count);
                    }
                }

                Marshal.Copy(src, 0, data.Scan0, src.Length);
            }
            catch { }
            finally
            {
                bmp.UnlockBits(data);
            }
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
            int bw = Math.Max(1, virtRect.Width);
            int bh = Math.Max(1, virtRect.Height);
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
            g.SetClip(new Rectangle(screen.X - virtRect.X, screen.Y - virtRect.Y, screen.Width, screen.Height));
            g.DrawImage(img, new RectangleF(x, y, w, h),
                             new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
            g.Restore(state);
        }
    }
}
