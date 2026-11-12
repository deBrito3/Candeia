using System.Drawing.Drawing2D;
using Candeia.Night;

namespace Candeia.Panels;

// The system's test panel: one lamp per test. A lamp not run yet is a dim outline, a lit lamp glows, a
// lamp that failed is dark with a red rim. A hidden test carries a question mark until it lights.
sealed class LampStrip : Control
{
    SystemStyle style;
    IReadOnlyList<LampState> lamps = [];
    IReadOnlyList<bool> hidden = [];

    public LampStrip(SystemStyle style)
    {
        this.style = style;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, false);
        BackColor = SystemStyle.Glass;
    }

    public void Restyle(SystemStyle value) { style = value; Invalidate(); }

    public void Show(IReadOnlyList<LampState> states, IReadOnlyList<bool> hiddenTests)
    {
        lamps = states.ToArray();
        hidden = hiddenTests;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SystemStyle.Glass);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int pad = style.Px(10);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        string label = "LÂMPADAS";
        var labelSize = TextRenderer.MeasureText(label, style.Text);
        TextRenderer.DrawText(g, label, style.Text, new Rectangle(pad, 0, labelSize.Width, Height), SystemStyle.Dim, flags);
        float d = Height * 0.52f, gap = d * 0.85f, x = pad + labelSize.Width + pad * 1.5f, y = (Height - d) / 2;
        using var lit = new SolidBrush(SystemStyle.Bright);
        using var dark = new SolidBrush(Color.FromArgb(36, 22, 12));
        using var outline = new Pen(SystemStyle.Dim, Math.Max(1, style.Scale * 1.5f));
        using var failed = new Pen(SystemStyle.Error, Math.Max(1, style.Scale * 2f));
        for (int i = 0; i < lamps.Count; i++)
        {
            var bulb = new RectangleF(x, y, d, d);
            switch (lamps[i])
            {
                case LampState.Lit:
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(RectangleF.Inflate(bulb, d * 0.45f, d * 0.45f));
                        using var halo = new PathGradientBrush(path) { CenterColor = Color.FromArgb(120, SystemStyle.Pass), SurroundColors = [Color.FromArgb(0, SystemStyle.Pass)] };
                        g.FillPath(halo, path);
                    }
                    g.FillEllipse(lit, bulb);
                    break;
                case LampState.Out:
                    g.FillEllipse(dark, bulb);
                    g.DrawEllipse(failed, bulb);
                    break;
                default:
                    g.DrawEllipse(outline, bulb);
                    break;
            }
            if (i < hidden.Count && hidden[i] && lamps[i] != LampState.Lit)
                TextRenderer.DrawText(g, "?", style.Text, Rectangle.Round(bulb), SystemStyle.Dim, TextFormatFlags.HorizontalCenter | flags);
            x += d + gap;
        }
        int on = lamps.Count(l => l == LampState.Lit);
        TextRenderer.DrawText(g, $"{on} de {lamps.Count}", style.Text, new Rectangle(pad, 0, Width - pad * 2, Height), SystemStyle.Ink, flags | TextFormatFlags.Right);
    }
}
