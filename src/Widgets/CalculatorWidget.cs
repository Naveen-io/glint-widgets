using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Calculator", Description = "Minimalist liquid glass desk calculator with exact evaluation")]
public sealed class CalculatorWidget : CardWidget
{
    string display = "0";
    string formula = "";
    bool clearOnNextDigit = true;
    bool errorState = false;

    // Repeated equals tracking
    string lastOp = null;
    decimal lastOperand = 0;
    bool lastWasEquals = false;

    // Interactive button hover/pressed tracking
    int hoverRow = -1, hoverCol = -1;
    int pressedRow = -1, pressedCol = -1;

    static readonly string[,] Keys =
    {
        { "C", "(", ")", "÷" },
        { "7", "8", "9", "×" },
        { "4", "5", "6", "−" },
        { "1", "2", "3", "+" },
        { "+/-", "0", ".", "=" }
    };

    public CalculatorWidget(Section settings) : base(settings, false) { }

    protected override Size Measure()
    {
        int cw = Settings.GetInt("custom_w", 0);
        int ch = Settings.GetInt("custom_h", 0);
        if (cw > 0 && ch > 0) return new Size(cw, ch);
        return new Size(Px(184), Px(248));
    }

    RectangleF KeyBox(int row, int col)
    {
        float m = P(12);
        float gridW = W - m * 2;
        float top = m + P(46);
        float gridH = H - top - m;
        float cellW = (gridW - P(5) * 3) / 4;
        float cellH = (gridH - P(5) * 4) / 5;

        return new RectangleF(m + col * (cellW + P(5)), top + row * (cellH + P(5)), cellW, cellH);
    }

    protected override bool IsButton(Point p)
    {
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 4; c++)
                if (KeyBox(r, c).Contains(p)) return true;

        // Also display well (clicking copies or focuses)
        float m = P(12);
        if (new RectangleF(m, m, W - m * 2, P(42)).Contains(p)) return true;

        return false;
    }

    protected override void OnWidgetClick(Point p)
    {
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                if (KeyBox(r, c).Contains(p))
                {
                    PressKey(Keys[r, c]);
                    return;
                }
            }
        }

        // Clicking display copies result
        float m = P(12);
        if (new RectangleF(m, m, W - m * 2, P(42)).Contains(p))
        {
            try { Clipboard.SetText(display); Toast.Show("Copied", display); } catch { }
        }
    }

    public void PressKey(string key)
    {
        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            if (clearOnNextDigit || display == "0" || errorState)
            {
                display = key;
                clearOnNextDigit = false;
                errorState = false;
            }
            else if (display.Length < 16)
            {
                display += key;
            }
            lastWasEquals = false;
        }
        else if (key == ".")
        {
            if (clearOnNextDigit || errorState)
            {
                display = "0.";
                clearOnNextDigit = false;
                errorState = false;
            }
            else if (!display.Contains("."))
            {
                display += ".";
            }
            lastWasEquals = false;
        }
        else if (key == "+/-")
        {
            if (errorState) return;
            if (display.StartsWith("-")) display = display.Substring(1);
            else if (display != "0") display = "-" + display;
        }
        else if (key == "C")
        {
            display = "0";
            formula = "";
            clearOnNextDigit = true;
            errorState = false;
            lastOp = null;
            lastWasEquals = false;
        }
        else if (key == "(")
        {
            if (errorState) { errorState = false; display = "0"; formula = ""; }
            formula += "(";
            clearOnNextDigit = true;
            lastWasEquals = false;
        }
        else if (key == ")")
        {
            if (errorState) return;
            if (!clearOnNextDigit) formula += display;
            formula += ")";
            clearOnNextDigit = true;
            lastWasEquals = false;
        }
        else if (key == "=")
        {
            ExecuteEquals();
        }
        else // Operators: +, −, ×, ÷
        {
            if (errorState) { errorState = false; display = "0"; formula = ""; }

            string opSymbol = key == "÷" || key == "/" ? "÷" : key == "×" || key == "*" ? "×" : key == "−" || key == "-" ? "-" : "+";

            if (lastWasEquals)
            {
                formula = display + " " + opSymbol + " ";
                clearOnNextDigit = true;
                lastWasEquals = false;
            }
            else
            {
                if (!clearOnNextDigit)
                {
                    formula += display + " " + opSymbol + " ";
                }
                else if (formula.Length >= 3 && IsTrailingOp(formula))
                {
                    // Replace trailing operator if user changed their mind
                    formula = formula.Substring(0, formula.Length - 3) + " " + opSymbol + " ";
                }
                else
                {
                    formula += display + " " + opSymbol + " ";
                }
                clearOnNextDigit = true;
            }
            lastOp = opSymbol;
        }

        Render();
    }

    bool IsTrailingOp(string f)
    {
        string trimmed = f.TrimEnd();
        return trimmed.EndsWith("+") || trimmed.EndsWith("-") || trimmed.EndsWith("×") || trimmed.EndsWith("÷");
    }

    void ExecuteEquals()
    {
        if (errorState) return;

        if (lastWasEquals && lastOp != null)
        {
            // Repeated equals execution: repeat the last operation on the current display
            string repeatExpr = display + " " + lastOp + " " + lastOperand.ToString(CultureInfo.InvariantCulture);
            EvaluateAndDisplay(repeatExpr);
            return;
        }

        string fullExpr = formula;
        if (!clearOnNextDigit || fullExpr.Length == 0)
        {
            fullExpr += display;
        }

        // Remember last operand for repeated equals
        decimal curVal;
        if (decimal.TryParse(display, NumberStyles.Any, CultureInfo.InvariantCulture, out curVal))
            lastOperand = curVal;

        EvaluateAndDisplay(fullExpr);
        lastWasEquals = true;
    }

    void EvaluateAndDisplay(string expr)
    {
        decimal result;
        string error;

        if (ExpressionParser.TryEvaluate(expr, out result, out error))
        {
            formula = "";
            display = ExpressionParser.FormatResult(result);
            clearOnNextDigit = true;
            errorState = false;
        }
        else
        {
            formula = "";
            display = error;
            clearOnNextDigit = true;
            errorState = true;
        }
    }

    public void Backspace()
    {
        if (errorState || clearOnNextDigit) return;
        if (display.Length > 1)
        {
            display = display.Substring(0, display.Length - 1);
            if (display == "-") display = "0";
        }
        else
        {
            display = "0";
            clearOnNextDigit = true;
        }
        Render();
    }

    public void Percentage()
    {
        if (errorState) return;
        decimal v;
        if (decimal.TryParse(display, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
        {
            display = ExpressionParser.FormatResult(v / 100m);
            clearOnNextDigit = true;
            Render();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int rHit = -1, cHit = -1;
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                if (KeyBox(r, c).Contains(e.Location))
                {
                    rHit = r; cHit = c; break;
                }
            }
            if (rHit >= 0) break;
        }

        if (rHit != hoverRow || cHit != hoverCol)
        {
            hoverRow = rHit;
            hoverCol = cHit;
            Render();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverRow = -1;
        hoverCol = -1;
        pressedRow = -1;
        pressedCol = -1;
        Render();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    if (KeyBox(r, c).Contains(e.Location))
                    {
                        pressedRow = r;
                        pressedCol = c;
                        Render();
                        return;
                    }
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (pressedRow >= 0)
        {
            pressedRow = -1;
            pressedCol = -1;
            Render();
        }
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(12);

        // 1. Recessed Liquid Glass Display Well
        RectangleF dispRect = new RectangleF(m, m, W - m * 2, P(42));
        DrawLiquidGlassWell(g, dispRect, P(8), IsDark);

        // Formula line (upper)
        if (!string.IsNullOrEmpty(formula))
        {
            using (var sfFormula = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                using (var fb = new SolidBrush(pal.Fg2))
                {
                    var formRect = new RectangleF(dispRect.X + P(8), dispRect.Y + P(2), dispRect.Width - P(16), P(14));
                    g.DrawString(formula, F(10, "regular"), fb, formRect, sfFormula);
                }
            }
        }

        // Primary Display Number / Result line (lower)
        float numSize = display.Length > 12 ? 14 : display.Length > 9 ? 16 : 19;
        using (var sfNum = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None
        })
        {
            Color numColor = errorState ? pal.Red : pal.Fg;
            using (var nb = new SolidBrush(numColor))
            {
                float numY = string.IsNullOrEmpty(formula) ? dispRect.Y : dispRect.Y + P(14);
                float numH = string.IsNullOrEmpty(formula) ? dispRect.Height : dispRect.Height - P(16);
                var numRect = new RectangleF(dispRect.X + P(8), numY, dispRect.Width - P(16), numH);
                g.DrawString(display, F(numSize, "bold"), nb, numRect, sfNum);
            }
        }

        // 2. Keypad Grid of Liquid Glass Buttons
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                string key = Keys[r, c];
                RectangleF b = KeyBox(r, c);

                bool isOp = key == "+" || key == "−" || key == "×" || key == "÷" || key == "=";
                bool isFn = key == "C" || key == "+/-" || key == "(" || key == ")";
                bool hover = r == hoverRow && c == hoverCol;
                bool pressed = r == pressedRow && c == pressedCol;

                Color textCol = isOp ? pal.OnAccent : (key == "C" && hover ? pal.Red : pal.Fg);
                Font kf = F(isOp ? 13 : 12, isOp ? "bold" : "semibold");

                DrawLiquidGlassButton(g, b, key, kf, textCol, isOp ? pal.Orange : pal.Fill, isOp, hover, pressed, IsDark);
            }
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_CHAR = 0x0102;
        const int WM_KEYDOWN = 0x0100;

        if (m.Msg == WM_CHAR)
        {
            char ch = (char)m.WParam.ToInt32();
            if (ch >= '0' && ch <= '9') PressKey(ch.ToString());
            else if (ch == '.') PressKey(".");
            else if (ch == '+') PressKey("+");
            else if (ch == '-') PressKey("−");
            else if (ch == '*' || ch == 'x' || ch == 'X') PressKey("×");
            else if (ch == '/') PressKey("÷");
            else if (ch == '(') PressKey("(");
            else if (ch == ')') PressKey(")");
            else if (ch == '=' || ch == '\r') PressKey("=");
            else if (ch == '\b') Backspace();
            else if (ch == 27) PressKey("C"); // Escape
            else if (ch == '%') Percentage();
            return;
        }
        else if (m.Msg == WM_KEYDOWN)
        {
            Keys key = (Keys)m.WParam.ToInt32();
            if (key == System.Windows.Forms.Keys.Back) { Backspace(); return; }
            if (key == System.Windows.Forms.Keys.Escape) { PressKey("C"); return; }
            if (key == System.Windows.Forms.Keys.Enter) { PressKey("="); return; }
        }

        base.WndProc(ref m);
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        menu.Add("Copy Result (" + display + ")", delegate
        {
            try { Clipboard.SetText(display); } catch { }
        });
        menu.Add("Percentage (%)", Percentage);
        menu.Add("Backspace (⌫)", Backspace);
        menu.Add("Clear (C)", delegate { PressKey("C"); });
    }

    // --- RECURSIVE DESCENT ARITHMETIC PARSER ---
    public static class ExpressionParser
    {
        enum TokenType { Number, Operator, OpenParen, CloseParen }

        class Token
        {
            public TokenType Type;
            public decimal Number;
            public char Op;
        }

        public static string FormatResult(decimal res)
        {
            if (res == Math.Floor(res))
                return res.ToString("0", CultureInfo.InvariantCulture);

            // Output clean decimal representation without redundant trailing zeros
            string s = res.ToString("0.##########", CultureInfo.InvariantCulture);
            return s.Length > 16 ? res.ToString("G8", CultureInfo.InvariantCulture) : s;
        }

        public static bool TryEvaluate(string expr, out decimal result, out string error)
        {
            result = 0;
            error = null;
            if (string.IsNullOrEmpty(expr)) { error = "0"; return false; }

            try
            {
                var tokens = Tokenize(expr);
                if (tokens.Count == 0) { error = "0"; return false; }
                int pos = 0;
                result = ParseExpression(tokens, ref pos);
                if (pos < tokens.Count) { error = "Syntax error"; return false; }
                return true;
            }
            catch (DivideByZeroException)
            {
                error = "Cannot divide by zero";
                return false;
            }
            catch (OverflowException)
            {
                error = "Overflow";
                return false;
            }
            catch
            {
                error = "Error";
                return false;
            }
        }

        static List<Token> Tokenize(string expr)
        {
            var list = new List<Token>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                    decimal num;
                    decimal.TryParse(expr.Substring(start, i - start), NumberStyles.Any, CultureInfo.InvariantCulture, out num);
                    list.Add(new Token { Type = TokenType.Number, Number = num });
                    continue;
                }

                if (c == '(') { list.Add(new Token { Type = TokenType.OpenParen, Op = c }); i++; continue; }
                if (c == ')') { list.Add(new Token { Type = TokenType.CloseParen, Op = c }); i++; continue; }

                if (c == '+' || c == '-' || c == '−' || c == '*' || c == '×' || c == '/' || c == '÷')
                {
                    char op = (c == '×') ? '*' : (c == '÷') ? '/' : (c == '−') ? '-' : c;
                    list.Add(new Token { Type = TokenType.Operator, Op = op });
                    i++;
                    continue;
                }

                i++;
            }
            return list;
        }

        static decimal ParseExpression(List<Token> tokens, ref int pos)
        {
            decimal left = ParseTerm(tokens, ref pos);
            while (pos < tokens.Count && tokens[pos].Type == TokenType.Operator && (tokens[pos].Op == '+' || tokens[pos].Op == '-'))
            {
                char op = tokens[pos++].Op;
                decimal right = ParseTerm(tokens, ref pos);
                if (op == '+') left += right;
                else left -= right;
            }
            return left;
        }

        static decimal ParseTerm(List<Token> tokens, ref int pos)
        {
            decimal left = ParseFactor(tokens, ref pos);
            while (pos < tokens.Count && tokens[pos].Type == TokenType.Operator && (tokens[pos].Op == '*' || tokens[pos].Op == '/'))
            {
                char op = tokens[pos++].Op;
                decimal right = ParseFactor(tokens, ref pos);
                if (op == '*') left *= right;
                else
                {
                    if (right == 0) throw new DivideByZeroException();
                    left /= right;
                }
            }
            return left;
        }

        static decimal ParseFactor(List<Token> tokens, ref int pos)
        {
            if (pos >= tokens.Count) throw new Exception("Unexpected end of expression");

            // Unary plus or minus
            if (tokens[pos].Type == TokenType.Operator && (tokens[pos].Op == '-' || tokens[pos].Op == '+'))
            {
                char op = tokens[pos++].Op;
                decimal val = ParsePrimary(tokens, ref pos);
                return op == '-' ? -val : val;
            }

            return ParsePrimary(tokens, ref pos);
        }

        static decimal ParsePrimary(List<Token> tokens, ref int pos)
        {
            if (pos >= tokens.Count) throw new Exception("Unexpected end of expression");
            var t = tokens[pos++];
            if (t.Type == TokenType.Number) return t.Number;
            if (t.Type == TokenType.OpenParen)
            {
                decimal val = ParseExpression(tokens, ref pos);
                if (pos < tokens.Count && tokens[pos].Type == TokenType.CloseParen)
                    pos++;
                return val;
            }
            throw new Exception("Unexpected token");
        }
    }
}
