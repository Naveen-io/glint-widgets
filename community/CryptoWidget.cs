using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Crypto", Description = "Live crypto price with sparkline")]
public sealed class CryptoWidget : CardWidget
{
    // { binance symbol, ticker, display name, accent hex }
    static readonly string[][] Coins = {
        new[] { "BTCUSDT", "BTC", "Bitcoin", "F7931A" },
        new[] { "ETHUSDT", "ETH", "Ethereum", "627EEA" },
        new[] { "SOLUSDT", "SOL", "Solana", "9945FF" },
        new[] { "DOGEUSDT", "DOGE", "Dogecoin", "C2A633" },
        new[] { "XRPUSDT", "XRP", "XRP", "0085C0" },
        new[] { "ADAUSDT", "ADA", "Cardano", "0033AD" },
        new[] { "LTCUSDT", "LTC", "Litecoin", "345D9D" },
        new[] { "BNBUSDT", "BNB", "BNB", "F3BA2F" },
        new[] { "AVAXUSDT", "AVAX", "Avalanche", "E84142" },
        new[] { "DOTUSDT", "DOT", "Polkadot", "E6007A" },
    };

    double price, changePct, volumeUsd;
    double[] sparkline;
    string error;
    bool busy;
    readonly System.Windows.Forms.Timer refresh;

    public CryptoWidget(Section settings) : base(settings, false)
    {
        refresh = new System.Windows.Forms.Timer();
        refresh.Interval = 5 * 60 * 1000;
        refresh.Tick += delegate { Fetch(); };
        LoadCache();
    }

    string[] Coin
    {
        get
        {
            string sym = Settings.Get("coin", "BTCUSDT");
            foreach (string[] c in Coins)
                if (c[0] == sym) return c;
            string ticker = sym.EndsWith("USDT") ? sym.Substring(0, sym.Length - 4) : sym;
            string name = Settings.Get("coin_name", ticker);
            string color = Settings.Get("coin_color", "98989F");
            return new[] { sym, ticker, name, color };
        }
    }

    protected override void OnStart()
    {
        refresh.Start();
        Fetch();
    }

    void LoadCache()
    {
        string path = CacheFile("crypto");
        if (path == null || !File.Exists(path)) return;
        try
        {
            var ser = new JavaScriptSerializer();
            var d = (Dictionary<string, object>)ser.DeserializeObject(File.ReadAllText(path));
            price = Convert.ToDouble(d["p"]);
            changePct = Convert.ToDouble(d["c"]);
            object vc;
            if (d.TryGetValue("qv", out vc)) volumeUsd = Convert.ToDouble(vc);
            object sp;
            if (d.TryGetValue("s", out sp) && sp is object[])
            {
                var arr = (object[])sp;
                sparkline = new double[arr.Length];
                for (int i = 0; i < arr.Length; i++) sparkline[i] = Convert.ToDouble(arr[i]);
            }
        }
        catch (Exception) { }
    }

    void SaveCache()
    {
        string path = CacheFile("crypto");
        if (path == null) return;
        try
        {
            var d = new Dictionary<string, object>();
            d["p"] = price;
            d["c"] = changePct;
            d["qv"] = volumeUsd;
            if (sparkline != null) d["s"] = sparkline;
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(d));
        }
        catch (Exception) { }
    }

    void Fetch()
    {
        if (busy) return;
        busy = true;
        string sym = Coin[0];
        ThreadPool.QueueUserWorkItem(delegate
        {
            string err = null;
            double p = 0, c = 0, v = 0;
            double[] sp = null;
            try
            {
                var ser = new JavaScriptSerializer();

                // 24h ticker: price, change %, traded value
                string tickerJson = Http.Get("https://api.binance.com/api/v3/ticker/24hr?symbol=" + sym);
                var ticker = (Dictionary<string, object>)ser.DeserializeObject(tickerJson);
                p = Convert.ToDouble(ticker["lastPrice"]);
                c = Convert.ToDouble(ticker["priceChangePercent"]);
                // quoteVolume is the 24h value traded in USDT. The ticker carries no earlier
                // volume, so a change against the previous day can't be derived from it.
                v = Convert.ToDouble(ticker["quoteVolume"]);

                // Klines for sparkline (last 48 hours, 1h candles)
                string klinesJson = Http.Get("https://api.binance.com/api/v3/klines?symbol=" + sym + "&interval=1h&limit=48");
                var klines = (object[])ser.DeserializeObject(klinesJson);
                sp = new double[klines.Length];
                for (int i = 0; i < klines.Length; i++)
                {
                    var candle = (object[])klines[i];
                    sp[i] = Convert.ToDouble(candle[4]); // close price
                }
            }
            catch (System.Net.WebException we)
            {
                var resp = we.Response as System.Net.HttpWebResponse;
                if (resp != null && (int)resp.StatusCode == 429)
                    err = "Rate limited — retrying";
                else if (resp != null)
                    err = "Error " + (int)resp.StatusCode;
                else
                    err = "Offline";
            }
            catch (Exception) { err = "Couldn't load price"; }

            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    busy = false;
                    if (err == null)
                    {
                        price = p;
                        changePct = c;
                        volumeUsd = v;
                        sparkline = sp;
                        error = null;
                        SaveCache();
                    }
                    else if (price == 0)
                    {
                        error = err;
                    }
                    Render();
                });
            }
            catch (InvalidOperationException) { }
        });
    }

    static readonly double[] SampleSparkline = {
        62100,62400,62000,61500,61200,61800,62500,63200,63800,64500,64100,63600,
        64200,65000,65800,66500,66200,67000,67800,67200,66800,67500,68200,68420
    };

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16), w = W - m * 2;
        string[] coin = Coin;
        Color accent = Rgb(Convert.ToInt32(coin[3], 16));

        bool sample = !IsHandleCreated;
        double drawPrice = sample ? 68420 : price;
        double drawChange = sample ? 2.52 : changePct;
        double drawVolume = sample ? 1.42e9 : volumeUsd;
        double[] drawSparkline = sample ? SampleSparkline : sparkline;

        // Top-right: coin icon
        float iconR = P(16);
        float iconX = W - m - iconR, iconY = m + iconR;
        DrawCoinLogo(g, coin, accent, iconX, iconY, iconR);

        // Top-left: ticker/pair
        string pair = coin[1] + "/USD";
        Label(g, pair, F(13, "semibold"), pal.Fg2, m, m);

        if (drawPrice == 0)
        {
            string msg = error != null ? error : "Loading…";
            Label(g, msg, F(13, "regular"), pal.Fg2, W / 2, H / 2 - P(7), w, StringAlignment.Center);
            return;
        }

        // Price below the pair label
        string priceStr = FormatPrice(drawPrice);
        float pricePt = W > P(200) ? 22 : 18;
        Label(g, priceStr, F(pricePt, "bold"), pal.Fg, m, m + P(18), w - iconR * 2 - P(8), StringAlignment.Near);

        // Sparkline in the middle area
        float spTop = m + P(48);
        float spBottom = H - m - P(36);
        float spH = spBottom - spTop;

        if (drawSparkline != null && drawSparkline.Length > 1 && spH > P(10))
            DrawSparkline(g, drawSparkline, m, spTop, w, spH, drawChange >= 0 ? pal.Green : pal.Red);

        // Bottom row: 24h volume + 24h change
        float bottomY = H - m - P(28);
        float halfW = w / 2;

        Label(g, "24h volume", F(10, "regular"), pal.Fg2, m, bottomY);
        Label(g, FormatVolume(drawVolume), F(12, "semibold"), pal.Fg, m, bottomY + P(14));

        Label(g, "24h change", F(10, "regular"), pal.Fg2, m + halfW, bottomY);
        bool chgUp = drawChange >= 0;
        string chgArrow = chgUp ? "▲" : "▼";
        string chgStr = chgArrow + Math.Abs(drawChange).ToString("0.00") + "%";
        Color chgCol = chgUp ? pal.Green : pal.Red;
        Label(g, chgStr, F(12, "semibold"), chgCol, m + halfW, bottomY + P(14));
    }

    void DrawCoinLogo(Graphics g, string[] coin, Color accent, float cx, float cy, float r)
    {
        FillCircle(g, accent, cx, cy, r);
        string ticker = coin[1];
        float k = r / 16f;

        if (ticker == "BTC")
        {
            // Bitcoin ₿ with serifs
            using (var pen = new Pen(Color.White, 2.2f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float bx = cx - 4 * k, by = cy - 8 * k;
                g.DrawLine(pen, bx, by, bx, cy + 8 * k);
                g.DrawLine(pen, bx, by, bx + 5 * k, by);
                g.DrawArc(pen, bx + 1 * k, by - 0.5f * k, 8 * k, 9 * k, -90, 180);
                g.DrawLine(pen, bx, cy, bx + 5 * k, cy);
                g.DrawArc(pen, bx + 1 * k, cy - 0.5f * k, 9 * k, 9.5f * k, -90, 180);
                g.DrawLine(pen, bx, cy + 8 * k, bx + 5 * k, cy + 8 * k);
                // Vertical bars
                g.DrawLine(pen, bx + 2 * k, by - 2.5f * k, bx + 2 * k, by);
                g.DrawLine(pen, bx + 6 * k, by - 2.5f * k, bx + 6 * k, by);
                g.DrawLine(pen, bx + 2 * k, cy + 8 * k, bx + 2 * k, cy + 10.5f * k);
                g.DrawLine(pen, bx + 6 * k, cy + 8 * k, bx + 6 * k, cy + 10.5f * k);
            }
        }
        else if (ticker == "ETH")
        {
            // Ethereum diamond
            using (var b = new SolidBrush(Color.White))
            {
                PointF top = new PointF(cx, cy - 10 * k);
                PointF mid = new PointF(cx, cy + 1 * k);
                PointF bot = new PointF(cx, cy + 11 * k);
                PointF left = new PointF(cx - 7 * k, cy);
                PointF right = new PointF(cx + 7 * k, cy);
                g.FillPolygon(b, new[] { top, right, mid });
                g.FillPolygon(new SolidBrush(Color.FromArgb(200, 255, 255, 255)), new[] { top, left, mid });
                g.FillPolygon(b, new[] { mid, right, bot });
                g.FillPolygon(new SolidBrush(Color.FromArgb(200, 255, 255, 255)), new[] { mid, left, bot });
            }
        }
        else
        {
            Label(g, ticker.Substring(0, Math.Min(ticker.Length, 3)), F(r > P(14) ? 13 : 11, "bold"), Color.White, cx, cy - P(7), r * 2, StringAlignment.Center);
        }
    }

    void DrawSparkline(Graphics g, double[] pts, float x, float y, float w, float h, Color color)
    {
        if (pts.Length < 2) return;
        double min = pts[0], max = pts[0];
        for (int i = 1; i < pts.Length; i++)
        {
            if (pts[i] < min) min = pts[i];
            if (pts[i] > max) max = pts[i];
        }
        double range = max - min;
        if (range < 0.01) range = 1;

        var points = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            points[i] = new PointF(
                x + (float)i / (pts.Length - 1) * w,
                y + h - (float)((pts[i] - min) / range) * h
            );
        }

        using (var area = new GraphicsPath())
        {
            area.AddLines(points);
            area.AddLine(points[points.Length - 1], new PointF(x + w, y + h));
            area.AddLine(new PointF(x + w, y + h), new PointF(x, y + h));
            area.CloseFigure();
            using (var grad = new LinearGradientBrush(new PointF(0, y), new PointF(0, y + h),
                Color.FromArgb(50, color), Color.FromArgb(0, color)))
                g.FillPath(grad, area);
        }
        using (var pen = new Pen(color, P(1.5f)))
        {
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, points);
        }
    }

    // 24h value traded, which runs to billions: ".42B", "8.6M".
    static string FormatVolume(double v)
    {
        if (v <= 0) return "—";
        if (v >= 1e12) return "$" + (v / 1e12).ToString("0.##") + "T";
        if (v >= 1e9) return "$" + (v / 1e9).ToString("0.##") + "B";
        if (v >= 1e6) return "$" + (v / 1e6).ToString("0.##") + "M";
        if (v >= 1e3) return "$" + (v / 1e3).ToString("0.#") + "K";
        return "$" + v.ToString("N0");
    }

    static string FormatPrice(double p)
    {
        if (p >= 10000) return "$" + p.ToString("N0");
        if (p >= 1) return "$" + p.ToString("N2");
        if (p >= 0.01) return "$" + p.ToString("0.####");
        return "$" + p.ToString("0.######");
    }

    void SwitchCoin(string sym)
    {
        Settings.Set("coin", sym);
        Settings.Remove("coin_name");
        Settings.Remove("coin_color");
        price = 0;
        changePct = 0;
        volumeUsd = 0;
        sparkline = null;
        error = null;
        Fetch();
    }

    void CustomCoin()
    {
        string cur = Settings.Get("coin", "BTCUSDT");
        string display = cur.EndsWith("USDT") ? cur.Substring(0, cur.Length - 4) : cur;
        var values = InputDialog.Ask("Custom Coin",
            "Enter a coin ticker (e.g. LINK, MATIC, TRX, PEPE).\nIt will be paired with USDT on Binance.",
            new[] { new InputDialog.Field { Label = "Ticker", Value = display } },
            "Set", null, null);
        if (values == null || values[0].Trim().Length == 0) return;
        string ticker = values[0].Trim().ToUpper();
        if (!ticker.EndsWith("USDT")) ticker = ticker + "USDT";
        SwitchCoin(ticker);
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        menu.Add("Refresh Now", Fetch);
        var sub = menu.AddSubmenu("Coin");
        string cur = Settings.Get("coin", "BTCUSDT");
        bool isPreset = false;
        foreach (string[] c in Coins)
        {
            string sym = c[0], name = c[2] + " (" + c[1] + ")";
            if (cur == sym) isPreset = true;
            sub.AddChoice(name, cur == sym, delegate { SwitchCoin(sym); });
        }
        sub.AddSeparator();
        sub.AddChoice("Other…", !isPreset, CustomCoin);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) refresh.Dispose();
        base.Dispose(disposing);
    }
}
