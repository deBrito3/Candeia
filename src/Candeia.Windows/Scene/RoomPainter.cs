using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Candeia.Scene;

// The room as it would look in full light: lime-washed walls with a painted dado, the window to the yard,
// the door to the corridor, and the desk with the old monitor, the keyboard, the notebook and the candles.
// Painted once per window size into a cache; the light pass darkens it every frame.
static class RoomPainter
{
    static readonly Color Wood = Color.FromArgb(92, 58, 40), WoodLight = Color.FromArgb(110, 70, 48), WoodDark = Color.FromArgb(60, 36, 24);
    // The bars inside the window opening share the outside's light, so they are painted darker to read
    // as wood against the night.
    static readonly Color WindowBars = Color.FromArgb(50, 32, 22);

    public static void Paint(Graphics g, SceneLayout layout)
    {
        g.Clear(Color.FromArgb(104, 116, 98));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using (var transform = layout.Transform()) g.Transform = transform;
        Wall(g);
        Window(g);
        Door(g);
        Wiring(g);
        Desk(g);
        Monitor(g);
        Keyboard(g);
        Notebook(g);
        for (int i = 0; i < Candles.All.Length; i++) Candles.DrawBody(g, i);
        g.ResetTransform();
    }

    static void Wall(Graphics g)
    {
        using (var plaster = new SolidBrush(Color.FromArgb(112, 124, 102))) g.FillRectangle(plaster, RectangleF.FromLTRB(-900, -900, 2820, 1900));
        using (var ceiling = new SolidBrush(Color.FromArgb(70, 72, 66))) g.FillRectangle(ceiling, RectangleF.FromLTRB(-900, -900, 2820, 0));
        using (var cornice = new SolidBrush(Color.FromArgb(150, 154, 136))) g.FillRectangle(cornice, RectangleF.FromLTRB(-900, 0, 2820, 30));
        using (var line = new Pen(Color.FromArgb(84, 88, 76), 3))
        {
            g.DrawLine(line, -900, 30, 2820, 30);
            g.DrawLine(line, -900, 12, 2820, 12);
        }

        var random = new Random(7);
        for (int i = 0; i < 80; i++)
        {
            float x = random.Next(-300, 2220), y = random.Next(30, 640), w = random.Next(60, 320);
            float h = w * (0.4f + (float)random.NextDouble() * 0.8f);
            bool dark = random.NextDouble() < 0.75;
            using var stain = new SolidBrush(dark ? Color.FromArgb(random.Next(10, 30), 60, 70, 52) : Color.FromArgb(random.Next(10, 24), 168, 174, 150));
            g.FillEllipse(stain, x, y, w, h);
        }
        // Damp running down from the cornice by the door.
        for (int i = 0; i < 9; i++)
        {
            float x = 1866 + i * 6 + random.Next(0, 4), length = 120 + random.Next(0, 260);
            var streak = RectangleF.FromLTRB(x, 30, x + 4 + random.Next(0, 6), 30 + length);
            using var brush = new LinearGradientBrush(streak, Color.FromArgb(50, 52, 60, 46), Color.FromArgb(0, 52, 60, 46), LinearGradientMode.Vertical);
            g.FillRectangle(brush, streak);
        }
        using (var crack = new Pen(Color.FromArgb(120, 58, 62, 52), 1.6f))
            for (int i = 0; i < 6; i++)
            {
                var points = new PointF[6];
                points[0] = new(random.Next(0, 1920), random.Next(40, 600));
                for (int p = 1; p < points.Length; p++) points[p] = new(points[p - 1].X + random.Next(-26, 26), points[p - 1].Y + random.Next(8, 34));
                g.DrawLines(crack, points);
            }

        // The dado: dark green oil paint with a red stripe, chipped here and there.
        using (var dado = new SolidBrush(Color.FromArgb(48, 72, 60))) g.FillRectangle(dado, RectangleF.FromLTRB(-900, 640, 2820, 1900));
        using (var stripe = new SolidBrush(Color.FromArgb(98, 54, 44))) g.FillRectangle(stripe, RectangleF.FromLTRB(-900, 634, 2820, 642));
        using var chip = new SolidBrush(Color.FromArgb(90, 112, 124, 102));
        for (int i = 0; i < 40; i++) g.FillEllipse(chip, random.Next(-200, 2120), random.Next(650, 930), random.Next(4, 18), random.Next(3, 10));
    }

    static void Window(Graphics g)
    {
        var casing = SceneLayout.WindowCasing;
        var o = SceneLayout.WindowOpening;
        SoftShadow(g, casing, 18, 70);
        using (var wood = new SolidBrush(Wood)) g.FillRectangle(wood, casing);
        using (var molding = new Pen(WoodDark, 3)) g.DrawRectangle(molding, casing.X + 9, casing.Y + 9, casing.Width - 18, casing.Height - 18);

        var state = g.Save();
        g.SetClip(o);
        StreetPainter.Paint(g);
        g.Restore(state);

        using var frame = new SolidBrush(WindowBars);
        g.FillRectangle(frame, RectangleF.FromLTRB(o.Left, o.Top, o.Right, 190));
        g.FillRectangle(frame, RectangleF.FromLTRB(o.Left, 268, o.Right, 292));
        g.FillRectangle(frame, RectangleF.FromLTRB(o.Left, 190, 102, o.Bottom));
        g.FillRectangle(frame, RectangleF.FromLTRB(398, 190, o.Right, o.Bottom));
        g.FillRectangle(frame, RectangleF.FromLTRB(242, 282, 258, o.Bottom));
        g.FillRectangle(frame, RectangleF.FromLTRB(102, 442, 398, 452));
        g.FillRectangle(frame, RectangleF.FromLTRB(102, 604, 398, 614));
        g.FillRectangle(frame, RectangleF.FromLTRB(o.Left, 758, o.Right, o.Bottom));
        using (var seam = new Pen(Color.FromArgb(30, 18, 12), 2))
        {
            g.DrawLine(seam, 250, 282, 250, o.Bottom);
            g.DrawLine(seam, o.Left, 282, o.Right, 282);
        }

        // The fan of the transom (bandeira).
        state = g.Save();
        g.SetClip(SceneLayout.Transom);
        using (var fan = new Pen(WindowBars, 5))
            foreach (double degrees in new[] { 25.0, 55, 90, 125, 155 })
            {
                double a = degrees * Math.PI / 180;
                g.DrawLine(fan, 250, 268, 250 + 190 * (float)Math.Cos(a), 268 - 190 * (float)Math.Sin(a));
            }
        g.FillEllipse(frame, 224, 244, 52, 48);
        g.Restore(state);

        // Old glass: a faint sheen across the panes.
        state = g.Save();
        using (var glass = SceneLayout.AllGlass()) g.SetClip(glass);
        using (var sheen = new SolidBrush(Color.FromArgb(12, 230, 236, 245)))
        {
            g.FillPolygon(sheen, [new(90, 420), new(410, 180), new(410, 250), new(90, 490)]);
            g.FillPolygon(sheen, [new(90, 640), new(410, 400), new(410, 420), new(90, 660)]);
        }
        g.Restore(state);

        // The latch, the sill.
        using (var iron = new SolidBrush(Color.FromArgb(40, 38, 36))) g.FillRectangle(iron, 244, 512, 12, 30);
        using (var sillTop = new SolidBrush(Color.FromArgb(118, 78, 54))) g.FillRectangle(sillTop, RectangleF.FromLTRB(46, 786, 454, 802));
        using (var sillFront = new SolidBrush(Color.FromArgb(76, 46, 30))) g.FillRectangle(sillFront, RectangleF.FromLTRB(46, 802, 454, 816));
    }

    static void Door(Graphics g)
    {
        var casing = SceneLayout.DoorCasing;
        var way = SceneLayout.Doorway;
        SoftShadow(g, casing, 18, 70);
        using (var wood = new SolidBrush(Wood)) g.FillRectangle(wood, casing);
        using (var molding = new Pen(WoodDark, 3)) g.DrawRectangle(molding, casing.X + 10, casing.Y + 10, casing.Width - 20, casing.Height - 10);

        // The corridor beyond, almost black.
        using (var corridor = new LinearGradientBrush(way, Color.FromArgb(22, 20, 22), Color.FromArgb(8, 8, 10), LinearGradientMode.Vertical)) g.FillRectangle(corridor, way);
        // At the far end of the corridor, a window lets in a little moonlight: whoever stands in the
        // gap is seen against it.
        using (var moon = new SolidBrush(Color.FromArgb(112, 122, 150))) g.FillRectangle(moon, 1562, 262, 40, 250);
        using (var bars = new Pen(Color.FromArgb(20, 20, 24), 3))
        {
            g.DrawLine(bars, 1582, 262, 1582, 512);
            g.DrawLine(bars, 1562, 380, 1602, 380);
        }
        using (var far = new Pen(Color.FromArgb(28, 26, 28), 2))
        {
            g.DrawRectangle(far, 1558, 250, 50, 640);
            g.DrawLine(far, 1556, 884, 1632, 880);
        }

        // Transom over the door.
        var transom = RectangleF.FromLTRB(way.Left, way.Top, way.Right, 224);
        using (var glass = new SolidBrush(Color.FromArgb(26, 28, 34))) g.FillRectangle(glass, transom);
        var state = g.Save();
        g.SetClip(transom);
        using (var fan = new Pen(WoodLight, 4))
            for (int i = 1; i < 6; i++) g.DrawLine(fan, 1695, 224, way.Left + i * (way.Width / 6), way.Top);
        g.Restore(state);
        using var light = new SolidBrush(WoodLight);
        g.FillRectangle(light, RectangleF.FromLTRB(way.Left, 224, way.Right, 234));

        // The leaf, ajar, with two raised panels.
        var leaf = SceneLayout.DoorLeaf;
        using (var face = new SolidBrush(Color.FromArgb(100, 64, 44))) g.FillRectangle(face, leaf);
        using var bevelLight = new Pen(Color.FromArgb(132, 90, 62), 3);
        using var bevelDark = new Pen(WoodDark, 3);
        using var panel = new SolidBrush(Color.FromArgb(92, 58, 40));
        foreach (var p in new[] { RectangleF.FromLTRB(leaf.Left + 20, 262, leaf.Right - 18, 590), RectangleF.FromLTRB(leaf.Left + 20, 626, leaf.Right - 18, 968) })
        {
            g.FillRectangle(panel, p);
            g.DrawLine(bevelLight, p.Left, p.Top, p.Right, p.Top);
            g.DrawLine(bevelLight, p.Left, p.Top, p.Left, p.Bottom);
            g.DrawLine(bevelDark, p.Right, p.Top, p.Right, p.Bottom);
            g.DrawLine(bevelDark, p.Left, p.Bottom, p.Right, p.Bottom);
        }
        using (var edge = new SolidBrush(Color.FromArgb(130, 88, 60))) g.FillRectangle(edge, RectangleF.FromLTRB(1632, leaf.Top, leaf.Left, leaf.Bottom));
        using (var shade = new Pen(Color.FromArgb(40, 24, 16), 2)) g.DrawLine(shade, leaf.Left, leaf.Top, leaf.Left, leaf.Bottom);
        using (var brass = new SolidBrush(Color.FromArgb(124, 100, 54)))
        {
            g.FillRectangle(brass, 1648, 592, 14, 52);
            g.FillEllipse(brass, 1650, 600, 16, 16);
        }
        using (var keyhole = new SolidBrush(Color.FromArgb(20, 16, 12))) g.FillEllipse(keyhole, 1652, 626, 6, 10);
    }

    // The house system's wiring: a cloth wire from the back of the monitor up to the cornice, on porcelain
    // knobs, to the dead bulb over the door.
    static void Wiring(Graphics g)
    {
        using var wire = new Pen(Color.FromArgb(40, 34, 30), 3);
        g.DrawLines(wire, [new(1440, 240), new(1466, 220), new(1466, 46), new(1695, 46), new(1695, 66)]);
        using var porcelain = new SolidBrush(Color.FromArgb(196, 190, 176));
        for (float x = 1466; x <= 1695; x += 76) g.FillEllipse(porcelain, x - 5, 40, 10, 12);
        using (var socket = new SolidBrush(Color.FromArgb(54, 46, 40))) g.FillRectangle(socket, 1686, 64, 18, 20);
        using (var bulb = new SolidBrush(Color.FromArgb(150, 146, 124))) g.FillEllipse(bulb, 1680, 82, 30, 34);
        using (var filament = new Pen(Color.FromArgb(70, 62, 50), 1.2f)) g.DrawBezier(filament, 1689, 90, 1692, 104, 1698, 104, 1701, 90);
    }

    static void Desk(Graphics g)
    {
        var top = RectangleF.FromLTRB(-900, SceneLayout.DeskTop, 2820, 1900);
        using (var wood = new LinearGradientBrush(RectangleF.FromLTRB(0, SceneLayout.DeskTop, 10, 1090), Color.FromArgb(62, 40, 26), Color.FromArgb(86, 56, 36), LinearGradientMode.Vertical))
        {
            wood.WrapMode = WrapMode.TileFlipXY;
            g.FillRectangle(wood, top);
        }
        var random = new Random(3);
        using (var grain = new Pen(Color.FromArgb(80, 46, 28, 18), 1.5f))
            for (int i = 0; i < 46; i++)
            {
                float y = SceneLayout.DeskTop + 6 + (float)random.NextDouble() * 150;
                float x = -100 + (float)random.NextDouble() * 1600;
                g.DrawBezier(grain, x, y, x + 200, y - 4, x + 420, y + 5, x + 640, y + random.Next(-3, 4));
            }
        using (var edge = new Pen(Color.FromArgb(30, 20, 14), 3)) g.DrawLine(edge, -900, SceneLayout.DeskTop, 2820, SceneLayout.DeskTop);
        var contact = RectangleF.FromLTRB(-900, SceneLayout.DeskTop, 2820, SceneLayout.DeskTop + 26);
        using var shadow = new LinearGradientBrush(contact, Color.FromArgb(120, 10, 6, 4), Color.FromArgb(0, 10, 6, 4), LinearGradientMode.Vertical);
        g.FillRectangle(shadow, contact);
    }

    static void Monitor(Graphics g)
    {
        var m = SceneLayout.Monitor;
        SoftShadow(g, m, 26, 90);
        using (var stand = new SolidBrush(Color.FromArgb(138, 130, 106))) g.FillPolygon(stand, [new(820, 900), new(1100, 900), new(1140, 938), new(780, 938)]);

        using (var body = SceneLayout.RoundedRect(m, 42))
        using (var plastic = new LinearGradientBrush(m, Color.FromArgb(188, 178, 150), Color.FromArgb(148, 138, 112), LinearGradientMode.Vertical))
        using (var outline = new Pen(Color.FromArgb(96, 90, 72), 3))
        {
            g.FillPath(plastic, body);
            g.DrawPath(outline, body);
        }
        var random = new Random(5);
        for (int i = 0; i < 10; i++)
        {
            using var yellow = new SolidBrush(Color.FromArgb(random.Next(8, 20), 170, 140, 60));
            g.FillEllipse(yellow, random.Next(480, 1300), random.Next(80, 800), random.Next(80, 260), random.Next(40, 160));
        }

        var bevel = RectangleF.FromLTRB(518, 108, 1402, 816);
        using (var inner = SceneLayout.RoundedRect(bevel, 46))
        using (var face = new SolidBrush(Color.FromArgb(156, 148, 122)))
        using (var line = new Pen(Color.FromArgb(118, 110, 90), 2))
        {
            g.FillPath(face, inner);
            g.DrawPath(line, inner);
        }
        using (var screen = SceneLayout.RoundedRect(SceneLayout.Screen, SceneLayout.ScreenRadius))
        using (var glass = new SolidBrush(Palette.ScreenGlass))
        using (var lip = new Pen(Color.FromArgb(70, 64, 52), 4))
        {
            g.FillPath(glass, screen);
            g.DrawPath(lip, screen);
        }

        using (var vent = new SolidBrush(Color.FromArgb(110, 102, 84)))
            for (int i = 0; i < 16; i++) g.FillRectangle(vent, 690 + i * 34, 82, 22, 5);
        using (var font = new Font("Consolas", 22, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var shine = new SolidBrush(Color.FromArgb(200, 192, 164)))
        using (var ink = new SolidBrush(Color.FromArgb(122, 114, 92)))
        {
            var size = g.MeasureString(GameInfo.SystemName, font);
            g.DrawString(GameInfo.SystemName, font, shine, 960 - size.Width / 2 + 1, 830 + 1);
            g.DrawString(GameInfo.SystemName, font, ink, 960 - size.Width / 2, 830);
        }
        using (var knob = new SolidBrush(Color.FromArgb(118, 110, 90)))
        using (var notch = new Pen(Color.FromArgb(70, 64, 52), 2))
            foreach (float x in new float[] { 1290, 1330 })
            {
                g.FillEllipse(knob, x - 11, 841, 22, 22);
                g.DrawLine(notch, x, 843, x + 4, 851);
            }
        using (var housing = new SolidBrush(Color.FromArgb(70, 58, 40))) g.FillEllipse(housing, SceneLayout.Led.X - 6, SceneLayout.Led.Y - 6, 12, 12);
    }

    static void Keyboard(Graphics g)
    {
        var k = SceneLayout.Keyboard;
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(shadow, k.Left + 10, k.Bottom - 12, k.Width - 20, 24);
        using (var body = SceneLayout.RoundedRect(k, 10))
        using (var plastic = new SolidBrush(Color.FromArgb(166, 156, 128)))
        using (var outline = new Pen(Color.FromArgb(108, 100, 82), 2))
        {
            g.FillPath(plastic, body);
            g.DrawPath(outline, body);
        }
        using var key = new SolidBrush(Color.FromArgb(198, 188, 160));
        using var keyEdge = new SolidBrush(Color.FromArgb(138, 130, 106));
        for (int r = 0; r < 5; r++)
        {
            float y = k.Top + 10 + r * 18;
            for (float x = k.Left + 14 + r * 5; x + 15 <= k.Right - 14; x += 19)
            {
                if (r == 4 && x > 800 && x < 1100) continue;
                g.FillRectangle(keyEdge, x, y + 2, 15, 14);
                g.FillRectangle(key, x, y, 15, 12);
            }
        }
        g.FillRectangle(keyEdge, 820, k.Top + 84, 270, 14);
        g.FillRectangle(key, 820, k.Top + 82, 270, 12);
    }

    static void Notebook(Graphics g)
    {
        var state = g.Save();
        g.TranslateTransform(265, 1020);
        g.RotateTransform(-5);
        using (var pages = new SolidBrush(Color.FromArgb(214, 204, 178))) g.FillRectangle(pages, -150, -46, 306, 98);
        using (var cover = new SolidBrush(Color.FromArgb(98, 42, 34))) g.FillRectangle(cover, -155, -50, 304, 96);
        using (var worn = new SolidBrush(Color.FromArgb(120, 70, 58))) g.FillPolygon(worn, [new(-155, -50), new(-135, -50), new(-155, -30)]);
        using (var label = new SolidBrush(Color.FromArgb(172, 160, 132))) g.FillRectangle(label, -50, -24, 96, 26);
        using (var band = new SolidBrush(Color.FromArgb(30, 24, 22))) g.FillRectangle(band, 118, -50, 8, 96);
        g.Restore(state);
    }

    // A soft dark halo around an object standing against the wall.
    static void SoftShadow(Graphics g, RectangleF bounds, float spread, int strength)
    {
        int steps = 8;
        for (int i = steps; i >= 1; i--)
        {
            float grow = spread * i / steps;
            using var path = SceneLayout.RoundedRect(RectangleF.Inflate(bounds, grow, grow), 30 + grow);
            using var brush = new SolidBrush(Color.FromArgb(strength / steps, 0, 0, 0));
            g.FillPath(brush, path);
        }
    }
}
