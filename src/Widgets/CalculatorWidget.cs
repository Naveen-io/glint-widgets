using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Calculator", Description = "Minimalist desk calculator with immediate feedback")]
public sealed class CalculatorWidget : CardWidget
{
    string display = "0";
    double storedValue = 0;
    string pendingOp = null;
    bool clearOnNextDigit = true;

    static readonly string[,] Keys =
    {
        { "C", "+/-", "%", "/" },
        { "7", "8", "9", "*" },
        { "4", "5", "6", "-" },
        { "1", "2", "3", "+" },
        { "0", ".", "=", "" }
    };

    public CalculatorWidget(Section settings) : base(settings, false) { }

    RectangleF KeyBox(int row, int col)
    {
        float m = P(14);
        float gridW = W - m * 2;
        float gridH = H - m * 2 - P(32);
        float cellW = (gridW - P(6) * 3) / 4;
        float cellH = (gridH - P(5) * 4) / 5;
        float top = m + P(32);

        if (row == 4 && col == 0) // wide '0' key
            return new RectangleF(m, top + row * (cellH + P(5)), cellW * 2 + P(6), cellH);

        return new RectangleF(m + col * (cellW + P(6)), top + row * (cellH + P(5)), cellW, cellH);
    }

    protected override bool IsButton(Point p)
    {
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                if (r == 4 && c == 1) continue; // swallowed by wide 0
                string key = Keys[r, c];
                if (key.Length > 0 && KeyBox(r, c).Contains(p)) return true;
            }
        }
        return false;
    }

    protected override void OnWidgetClick(Point p)
    {
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                if (r == 4 && c == 1) continue;
                string key = Keys[r, c];
                if (key.Length > 0 && KeyBox(r, c).Contains(p))
                {
                    PressKey(key);
                    return;
                }
            }
        }
    }

    void PressKey(string key)
    {
        double current;
        double.TryParse(display, NumberStyles.Any, CultureInfo.InvariantCulture, out current);

        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            if (clearOnNextDigit || display == "0")
            {
                display = key;
                clearOnNextDigit = false;
            }
            else if (display.Length < 12)
            {
                display += key;
            }
        }
        else if (key == ".")
        {
            if (clearOnNextDigit)
            {
                display = "0.";
                clearOnNextDigit = false;
            }
            else if (!display.Contains("."))
            {
                display += ".";
            }
        }
        else if (key == "C")
        {
            display = "0";
            storedValue = 0;
            pendingOp = null;
            clearOnNextDigit = true;
        }
        else if (key == "+/-")
        {
            if (display.StartsWith("-")) display = display.Substring(1);
            else if (display != "0") display = "-" + display;
        }
        else if (key == "%")
        {
            display = (current / 100.0).ToString(CultureInfo.InvariantCulture);
            clearOnNextDigit = true;
        }
        else if (key == "=")
        {
            if (pendingOp != null)
            {
                display = Evaluate(storedValue, current, pendingOp);
                pendingOp = null;
                clearOnNextDigit = true;
            }
        }
        else // Operators +, -, *, /
        {
            if (pendingOp != null && !clearOnNextDigit)
            {
                display = Evaluate(storedValue, current, pendingOp);
                double.TryParse(display, NumberStyles.Any, CultureInfo.InvariantCulture, out storedValue);
            }
            else
            {
                storedValue = current;
            }
            pendingOp = key;
            clearOnNextDigit = true;
        }
        Render();
    }

    string Evaluate(double a, double b, string op)
    {
        double res = a;
        if (op == "+") res = a + b;
        else if (op == "-") res = a - b;
        else if (op == "*") res = a * b;
        else if (op == "/") res = b != 0 ? a / b : 0;

        string s = Math.Round(res, 8).ToString(CultureInfo.InvariantCulture);
        return s.Length > 12 ? res.ToString("G6", CultureInfo.InvariantCulture) : s;
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(14);
        // Header Display
        Font df = F(display.Length > 8 ? 16 : 22, "semibold");
        Label(g, display, df, pal.Fg, m, m, W - m * 2, StringAlignment.Far);

        // Keypad grid
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                if (r == 4 && c == 1) continue;
                string key = Keys[r, c];
                if (key.Length == 0) continue;

                RectangleF b = KeyBox(r, c);
                bool isOp = key == "+" || key == "-" || key == "*" || key == "/" || key == "=";
                bool isFn = key == "C" || key == "+/-" || key == "%";

                Color bg = isOp ? pal.Orange : isFn ? pal.Fill : pal.Fill;
                Color fg = isOp ? pal.OnAccent : pal.Fg;

                FillRound(g, bg, b, P(6));
                Font kf = F(11, isOp ? "bold" : "semibold");
                Label(g, key, kf, fg, b.X, b.Y + (b.Height - kf.GetHeight()) / 2, b.Width, StringAlignment.Center);
            }
        }
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        menu.Add("Copy Result (" + display + ")", delegate
        {
            try { Clipboard.SetText(display); } catch { }
        });
        menu.Add("Clear", delegate { PressKey("C"); });
    }
}
