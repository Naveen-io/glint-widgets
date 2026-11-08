using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DeskWidgets;
using DeskWidgets.Widgets;

[DeskWidget(Name = "Liquid Glass Test", Description = "Reference Liquid Glass widget with GlassCard, GlassButton, and GlassSwitch")]
public sealed class GlassTestWidget : CardWidget
{
    bool switchOn = true;
    int buttonClicks = 0;

    int hoverState = 0; // 1 = button, 2 = switch
    int pressedState = 0;

    public GlassTestWidget(Section settings) : base(settings, false)
    {
        switchOn = Settings.GetBool("switch", true);
    }

    RectangleF ButtonBox
    {
        get
        {
            float m = P(16);
            return new RectangleF(m, H - m - P(32), P(82), P(28));
        }
    }

    RectangleF SwitchBox
    {
        get
        {
            float m = P(16);
            return new RectangleF(W - m - P(44), H - m - P(29), P(44), P(24));
        }
    }

    protected override bool IsButton(Point p)
    {
        return ButtonBox.Contains(p) || SwitchBox.Contains(p);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = ButtonBox.Contains(e.Location) ? 1 : SwitchBox.Contains(e.Location) ? 2 : 0;
        if (hit != hoverState)
        {
            hoverState = hit;
            Render();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverState = 0;
        pressedState = 0;
        Render();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            int hit = ButtonBox.Contains(e.Location) ? 1 : SwitchBox.Contains(e.Location) ? 2 : 0;
            if (hit != 0)
            {
                pressedState = hit;
                Render();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (pressedState != 0)
        {
            pressedState = 0;
            Render();
        }
    }

    protected override void OnWidgetClick(Point p)
    {
        if (ButtonBox.Contains(p))
        {
            buttonClicks++;
            Render();
        }
        else if (SwitchBox.Contains(p))
        {
            switchOn = !switchOn;
            Settings.Set("switch", switchOn);
            Render();
        }
    }

    protected override void DrawContent(Graphics g, Palette pal)
    {
        float m = P(16), w = W - m * 2;

        // Title
        Label(g, "Liquid Glass", F(14, "semibold"), pal.Fg, m, m - P(1));

        // Subtitle tag at top right
        float tagW = P(54), tagH = P(18);
        var tagRect = new RectangleF(W - m - tagW, m - P(2), tagW, tagH);
        DrawLiquidGlassWell(g, tagRect, tagH / 2f, IsDark);
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            using (var b = new SolidBrush(pal.Fg2))
                g.DrawString(IsClear ? "Clear" : "Regular", F(9, "semibold"), b, tagRect, sf);
        }

        // Descriptive text element
        string desc = buttonClicks == 0
            ? "Real Snell's law refraction over your desktop wallpaper."
            : string.Format("Button tapped {0} {1}!", buttonClicks, buttonClicks == 1 ? "time" : "times");
        var descRect = new RectangleF(m, m + P(24), w, P(48));
        Paragraph(g, desc, F(11, "regular"), pal.Fg2, descRect);

        // GlassButton: interactive pill button with radial hover bloom
        RectangleF btnRect = ButtonBox;
        bool btnHover = hoverState == 1;
        bool btnPress = pressedState == 1;
        DrawLiquidGlassButton(g, btnRect, "Tap me", F(11, "semibold"), pal.Fg, pal.Fill, false, btnHover, btnPress, IsDark);

        // GlassSwitch: interactive toggle with refracting lens knob
        RectangleF swRect = SwitchBox;
        bool swHover = hoverState == 2;
        DrawLiquidGlassSwitch(g, swRect, switchOn, swHover, IsDark);
    }

    protected override void BuildWidgetMenu(PopupMenu menu)
    {
        menu.Add(switchOn ? "Turn Switch Off" : "Turn Switch On", delegate
        {
            switchOn = !switchOn;
            Settings.Set("switch", switchOn);
            Render();
        });
        menu.Add("Reset Tap Counter", delegate
        {
            buttonClicks = 0;
            Render();
        });
    }
}
