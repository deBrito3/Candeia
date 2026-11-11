using System.Drawing.Drawing2D;

namespace Candeia.Scene;

// What the bedroom window shows, in final colours (the outside does not depend on the candles): the
// yard with the mango tree, the wall with the iron gate, the street lamp and the houses across the street.
// From far to near: the street behind the gate, the gate, the yard, the window.
static class StreetPainter
{
    public const float Horizon = 462;

    public static void Paint(Graphics g)
    {
        var o = SceneLayout.WindowOpening;
        Sky(g, o);
        Houses(g);
        Lamp(g);
        Street(g, o);
        Wall(g, o);
        DrawGateBars(g);
        Yard(g, o);
        Tree(g);
    }

    static void Sky(Graphics g, RectangleF o)
    {
        var sky = RectangleF.FromLTRB(o.Left, o.Top, o.Right, Horizon + 2);
        using (var brush = new LinearGradientBrush(sky, Color.FromArgb(12, 15, 26), Color.FromArgb(40, 38, 48), LinearGradientMode.Vertical)) g.FillRectangle(brush, sky);
        Glow(g, new PointF(250, 470), 230, 90, Color.FromArgb(70, 72, 50, 38));
        using var cloud = new SolidBrush(Color.FromArgb(60, 48, 50, 62));
        g.FillEllipse(cloud, 96, 188, 190, 48);
        g.FillEllipse(cloud, 214, 204, 210, 44);
        g.FillEllipse(cloud, 132, 246, 160, 34);
        g.FillEllipse(cloud, 296, 236, 150, 40);
        g.FillEllipse(cloud, 176, 300, 170, 28);
    }

    static void Houses(Graphics g)
    {
        // Facades across the street, with their parapets (platibandas).
        (float left, float right, float top, Color color)[] facades =
        [
            (90, 172, 372, Color.FromArgb(26, 27, 35)),
            (172, 236, 392, Color.FromArgb(31, 31, 39)),
            (236, 318, 360, Color.FromArgb(24, 25, 32)),
            (318, 410, 384, Color.FromArgb(29, 30, 37)),
        ];
        using var window = new SolidBrush(Color.FromArgb(16, 16, 22));
        foreach (var (left, right, top, color) in facades)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, RectangleF.FromLTRB(left, top, right, Horizon));
            // Parapet: a raised middle and small finials.
            float mid = (left + right) / 2;
            g.FillPolygon(brush, [new PointF(mid - 18, top), new PointF(mid - 10, top - 10), new PointF(mid + 10, top - 10), new PointF(mid + 18, top)]);
            g.FillRectangle(brush, left + 4, top - 8, 4, 8);
            g.FillRectangle(brush, right - 8, top - 8, 4, 8);
            for (float x = left + 10; x + 12 < right - 6; x += 24)
                for (float y = top + 16; y + 20 < Horizon - 8; y += 34)
                    g.FillRectangle(window, x, y, 12, 20);
        }
        // One window still lit across the street.
        using var lit = new SolidBrush(Color.FromArgb(104, 82, 46));
        g.FillRectangle(lit, 194, 414, 12, 20);
        Glow(g, new PointF(200, 424), 20, 20, Color.FromArgb(50, 140, 104, 56));
    }

    // The street lamp stands just beyond the gate, so whoever waits under it is seen against its light.
    public const float LampX = 286;

    static void Lamp(Graphics g)
    {
        using var iron = new SolidBrush(Color.FromArgb(20, 20, 24));
        g.FillRectangle(iron, LampX - 2, 300, 5, Horizon - 300);
        using (var arm = new Pen(Color.FromArgb(20, 20, 24), 3)) g.DrawBezier(arm, LampX, 302, LampX + 2, 290, LampX + 10, 288, LampX + 16, 292);
        g.FillPolygon(iron, [new PointF(LampX + 9, 292), new PointF(LampX + 23, 292), new PointF(LampX + 20, 302), new PointF(LampX + 12, 302)]);
        Glow(g, new PointF(LampX + 16, 304), 74, 74, Color.FromArgb(150, 255, 186, 108));
        using var bulb = new SolidBrush(Color.FromArgb(255, 236, 186));
        g.FillEllipse(bulb, LampX + 12, 299, 8, 7);
    }

    static void Street(Graphics g, RectangleF o)
    {
        var street = RectangleF.FromLTRB(o.Left, Horizon, o.Right, 526);
        using (var brush = new SolidBrush(Color.FromArgb(32, 31, 34))) g.FillRectangle(brush, street);
        using var stone = new SolidBrush(Color.FromArgb(42, 41, 44));
        int row = 0;
        for (float y = Horizon + 3; y < 524; y += 7, row++)
            for (float x = o.Left + (row % 2) * 5; x < o.Right; x += 11)
                g.FillEllipse(stone, x, y, 9, 4.5f);
        // The lamp's pool of light on the wet stones, and its long reflection.
        Glow(g, new PointF(LampX + 10, 494), 84, 26, Color.FromArgb(120, 170, 112, 60));
        var streak = RectangleF.FromLTRB(LampX + 6, Horizon + 4, LampX + 22, 526);
        using var reflection = new LinearGradientBrush(streak, Color.FromArgb(130, 210, 146, 74), Color.FromArgb(0, 210, 146, 74), LinearGradientMode.Vertical);
        g.FillRectangle(reflection, streak);
    }

    static void Wall(Graphics g, RectangleF o)
    {
        using var plaster = new SolidBrush(Color.FromArgb(50, 52, 58));
        using var coping = new SolidBrush(Color.FromArgb(68, 70, 76));
        g.FillRectangle(plaster, RectangleF.FromLTRB(o.Left, 448, 186, 526));
        g.FillRectangle(plaster, RectangleF.FromLTRB(316, 448, o.Right, 526));
        g.FillRectangle(coping, RectangleF.FromLTRB(o.Left, 442, 186, 450));
        g.FillRectangle(coping, RectangleF.FromLTRB(316, 442, o.Right, 450));
        using var stain = new SolidBrush(Color.FromArgb(40, 30, 32, 36));
        g.FillEllipse(stain, 110, 470, 50, 40);
        g.FillEllipse(stain, 350, 480, 40, 36);
        // The lamp behind warms the top of the wall.
        Glow(g, new PointF(LampX + 20, 446), 110, 30, Color.FromArgb(70, 150, 112, 70));
        // Gate posts with their caps.
        using var post = new SolidBrush(Color.FromArgb(62, 64, 68));
        foreach (float x in new float[] { 184, 304 })
        {
            g.FillRectangle(post, x, 430, 14, 96);
            g.FillRectangle(coping, x - 4, 424, 22, 8);
            g.FillEllipse(coping, x + 2, 414, 10, 10);
        }
    }

    // The iron gate. Also drawn again over a figure standing behind it, in the street.
    public static void DrawGateBars(Graphics g)
    {
        using var bar = new Pen(Color.FromArgb(16, 16, 18), 2.4f);
        using var rail = new Pen(Color.FromArgb(16, 16, 18), 3.2f);
        using var tip = new SolidBrush(Color.FromArgb(16, 16, 18));
        g.DrawLine(rail, 198, 462, 304, 462);
        g.DrawLine(rail, 198, 512, 304, 512);
        for (int i = 0; i < 12; i++)
        {
            float x = 203 + i * 8.6f;
            g.DrawLine(bar, x, 452, x, 524);
            g.FillPolygon(tip, [new PointF(x - 2.5f, 453), new PointF(x, 446), new PointF(x + 2.5f, 453)]);
        }
        using var scroll = new Pen(Color.FromArgb(16, 16, 18), 1.6f);
        for (int i = 0; i < 6; i++) g.DrawEllipse(scroll, 205 + i * 17, 464, 12, 10);
    }

    static void Yard(Graphics g, RectangleF o)
    {
        var ground = RectangleF.FromLTRB(o.Left, 524, o.Right, o.Bottom);
        using (var brush = new LinearGradientBrush(ground, Color.FromArgb(26, 30, 26), Color.FromArgb(12, 15, 12), LinearGradientMode.Vertical)) g.FillRectangle(brush, ground);
        // Flat stones from the gate to the house, growing as they come closer.
        using var flat = new SolidBrush(Color.FromArgb(44, 44, 46));
        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f, w = 16 + t * 70, h = 4 + t * 16;
            float x = 252 + t * 70 - w / 2, y = 532 + t * t * 210;
            g.FillEllipse(flat, x, y, w, h);
        }
        var random = new Random(11);
        using var grass = new Pen(Color.FromArgb(20, 27, 20), 1.4f);
        for (int i = 0; i < 160; i++)
        {
            float x = o.Left + (float)random.NextDouble() * o.Width;
            float y = 530 + (float)Math.Pow(random.NextDouble(), 0.7) * (o.Bottom - 530);
            float size = 3 + (y - 524) / 22;
            g.DrawLine(grass, x, y, x + (float)(random.NextDouble() - 0.5) * size, y - size);
        }
        // Puddles that mirror the sky and the lamp.
        using var puddle = new SolidBrush(Color.FromArgb(160, 44, 48, 60));
        g.FillEllipse(puddle, 168, 598, 76, 14);
        g.FillEllipse(puddle, 300, 680, 88, 18);
        Glow(g, new PointF(350, 689), 30, 7, Color.FromArgb(70, 200, 140, 72));
    }

    static void Tree(Graphics g)
    {
        using var bark = new SolidBrush(Color.FromArgb(18, 16, 14));
        g.FillPolygon(bark, [new(106, 650), new(134, 650), new(140, 540), new(152, 430), new(140, 428), new(126, 530), new(112, 590)]);
        using (var branch = new Pen(Color.FromArgb(18, 16, 14), 5)) { g.DrawBezier(branch, 146, 450, 170, 410, 190, 380, 220, 360); g.DrawBezier(branch, 142, 470, 126, 420, 110, 390, 96, 372); }
        var random = new Random(23);
        using var leavesDark = new SolidBrush(Color.FromArgb(13, 19, 15));
        using var leavesLight = new SolidBrush(Color.FromArgb(19, 27, 21));
        for (int i = 0; i < 26; i++)
        {
            float x = 84 + (float)random.NextDouble() * 170, y = 176 + (float)random.NextDouble() * 220;
            float w = 40 + (float)random.NextDouble() * 70;
            g.FillEllipse(i % 3 == 0 ? leavesLight : leavesDark, x - w / 2, y - w * 0.35f, w, w * 0.7f);
        }
        for (int i = 0; i < 60; i++)
        {
            float x = 84 + (float)random.NextDouble() * 190, y = 176 + (float)random.NextDouble() * 260;
            g.FillEllipse(leavesDark, x, y, 9, 4);
        }
    }

    // A soft round light: colour at the centre, fading to nothing at the edge.
    public static void Glow(Graphics g, PointF center, float radiusX, float radiusY, Color color)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(center.X - radiusX, center.Y - radiusY, radiusX * 2, radiusY * 2);
        using var brush = new PathGradientBrush(path) { CenterColor = color, SurroundColors = [Color.FromArgb(0, color)] };
        g.FillPath(brush, path);
    }
}
