using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Watchlist", Description = "Stock prices and daily changes")]
public sealed class Watchlist : CardWidget
{
    sealed class Quote
    {
        public string Symbol, Name;
        public double Price, ChangePercent;
    }

    List<Quote> quotes;
    string error;
    bool busy;
    readonly System.Windows.Forms.Timer refresh;

    public Watchlist(Section settings) : base(settings, true)
    {
        refresh = new System.Windows.Forms.Timer();
        refresh.Interval = 5 * 60 * 1000;
        refresh.Tick += delegate { Fetch(); };
        LoadCache();
    }

    string[] GetSymbols()
    {
        string raw = Settings.Get("symbols", "AAPL,MSFT,TSLA,NVDA");
        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    protected override void OnStart()
    {
        refresh.Start();
        Fetch();
    }

    void LoadCache()
    {
        string path = CacheFile("watchlist");
        if (path == null || !File.Exists(path)) return;
        try { quotes = ParseCache(File.ReadAllText(path)); }
        catch (Exception) { }
    }

    void SaveCache()
    {
        string path = CacheFile("watchlist");
        if (path == null || quotes == null) return;
        try
        {
            var list = new List<Dictionary<string, object>>();
            foreach (Quote q in quotes)
            {
                var d = new Dictionary<string, object>();
                d["s"] = q.Symbol;
                d["n"] = q.Name;
                d["p"] = q.Price;
                d["c"] = q.ChangePercent;
                list.Add(d);
            }
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(list));
        }
        catch (Exception) { }
    }

    static List<Quote> ParseCache(string json)
    {
        var arr = (object[])new JavaScriptSerializer().DeserializeObject(json);
        var result = new List<Quote>();
        foreach (Dictionary<string, object> item in arr)
        {
            result.Add(new Quote
            {
                Symbol = (string)item["s"],
                Name = (string)item["n"],
                Price = Convert.ToDouble(item["p"]),
                ChangePercent = Convert.ToDouble(item["c"])
            });
        }
        return result;
    }

    void Fetch()
    {
        if (busy) return;
        busy = true;
        string[] syms = GetSymbols();
        ThreadPool.QueueUserWorkItem(delegate
        {
            var result = new List<Quote>();
            string err = null;
            foreach (string raw in syms)
            {
                string sym = raw.Trim().ToUpper();
                if (sym.Length == 0) continue;
                try
                {
                    Quote q = FetchQuote(sym);
                    if (q != null) result.Add(q);
                }
                catch (WebException) { if (err == null) err = "Offline"; }
                catch (Exception) { if (err == null) err = "Couldn't load quotes"; }
            }
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    busy = false;
                    if (result.Count > 0)
                    {
                        quotes = result;
                        error = null;
                        SaveCache();
                    }
                    else if (quotes == null)
                    {
                        error = err;
                    }
                    Render();
                });
            }
            catch (InvalidOperationException) { }
        });
    }

    static Quote FetchQuote(string symbol)
    {
        string url = "https://query1.finance.yahoo.com/v8/finance/chart/" +
            Uri.EscapeDataString(symbol) + "?interval=1d&range=1d";
        string json = Http.Get(url);
        var ser = new JavaScriptSerializer();
        var root = (Dictionary<string, object>)ser.DeserializeObject(json);
        var chart = (Dictionary<string, object>)root["chart"];
        var results = (object[])chart["result"];
        if (results.Length == 0) return null;
        var meta = (Dictionary<string, object>)((Dictionary<string, object>)results[0])["meta"];

        double price = Convert.ToDouble(meta["regularMarketPrice"]);
        object prev;
        double prevClose = meta.TryGetValue("chartPreviousClose", out prev)
            ? Convert.ToDouble(prev)
            : (meta.TryGetValue("previousClose", out prev) ? Convert.ToDouble(prev) : price);
        double pct = prevClose > 0 ? ((price - prevClose) / prevClose) * 100 : 0;

        string name = symbol;
        object nameObj;
        if (meta.TryGetValue("shortName", out nameObj) && nameObj is string && ((string)nameObj).Length > 0)
            name = (string)nameObj;
        else if (meta.TryGetValue("longName", out nameObj) && nameObj is string && ((string)nameObj).Length > 0)
            name = (string)nameObj;

        return new Quote { Symbol = symbol, Name = name, Price = price, ChangePercent = pct };
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16);
        List<Quote> data = quotes;
        if (data == null && !IsHandleCreated) data = SampleData();

        Label(g, "WATCHLIST", F(11, "semibold"), pal.Fg2, m, m, W * 0.5f, StringAlignment.Near);
        if (data != null && data.Count > 0)
            Label(g, "Live", F(11, "regular"), pal.Fg2, W - m, m, P(40), StringAlignment.Far);

        if (data == null || data.Count == 0)
        {
            Label(g, error ?? "Loading…", F(13, "regular"), pal.Fg2,
                W / 2, H / 2 - P(7), W - m * 2, StringAlignment.Center);
            return;
        }

        float top = m + P(22);
        int count = Math.Min(data.Count, 4);
        float itemH = (H - top - m) / count;
        float symW = P(54);
        float chgW = P(58);
        float priceW = P(68);
        float nameLeft = m + symW;
        float nameW = W - nameLeft - priceW - chgW - P(8);

        for (int i = 0; i < count; i++)
        {
            Quote q = data[i];
            float y = top + i * itemH;
            float cy = y + itemH / 2;

            if (i > 0)
                using (var pen = new Pen(pal.Sep, 1f))
                    g.DrawLine(pen, m, y, W - m, y);

            Label(g, q.Symbol, F(14, "semibold"), pal.Fg, m, cy - P(7), symW, StringAlignment.Near);
            Label(g, q.Name, F(12, "regular"), pal.Fg2, nameLeft, cy - P(6), nameW, StringAlignment.Near);

            string priceStr = FormatPrice(q.Price);
            Label(g, priceStr, F(14, "semibold"), pal.Fg,
                W - m - chgW - P(6), cy - P(7), priceW, StringAlignment.Far);

            bool up = q.ChangePercent >= 0;
            string chgStr = (up ? "+" : "") + q.ChangePercent.ToString("0.00") + "%";
            Color chgCol = up ? pal.Green : pal.Red;
            Label(g, chgStr, F(12, "semibold"), chgCol, W - m, cy - P(6), chgW, StringAlignment.Far);
        }
    }

    static string FormatPrice(double price)
    {
        if (price >= 10000) return "$" + price.ToString("N0");
        if (price >= 1) return "$" + price.ToString("N2");
        return "$" + price.ToString("0.####");
    }

    static List<Quote> SampleData()
    {
        var list = new List<Quote>();
        list.Add(new Quote { Symbol = "AAPL", Name = "Apple Inc.", Price = 198.11, ChangePercent = 1.23 });
        list.Add(new Quote { Symbol = "MSFT", Name = "Microsoft", Price = 452.80, ChangePercent = -0.41 });
        list.Add(new Quote { Symbol = "TSLA", Name = "Tesla Inc.", Price = 312.45, ChangePercent = 3.17 });
        list.Add(new Quote { Symbol = "NVDA", Name = "NVIDIA", Price = 874.20, ChangePercent = 0.89 });
        return list;
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        menu.Add("Refresh Now", Fetch);
        menu.Add("Edit Symbols…", EditSymbols);
    }

    void EditSymbols()
    {
        string current = Settings.Get("symbols", "AAPL,MSFT,TSLA,NVDA");
        var values = InputDialog.Ask("Watchlist",
            "Enter up to 4 stock ticker symbols, separated by commas.",
            new[] { new InputDialog.Field { Label = "Symbols", Value = current } },
            "Save", null, null);
        if (values == null || values[0].Trim().Length == 0) return;
        Settings.Set("symbols", values[0].Trim().ToUpper());
        Fetch();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) refresh.Dispose();
        base.Dispose(disposing);
    }
}
