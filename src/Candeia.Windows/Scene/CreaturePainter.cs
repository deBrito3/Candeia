using System.Drawing.Drawing2D;
using Candeia.Night;

namespace Candeia.Scene;

// The Papa-Figo: an old man with a hat, hunched, in a dark paletó, with a cane and a sack over one
// shoulder. The wrongness is in the details: grey skin, fingers too long, red eyes glowing under the
// brim, the breathing. He is never fully lit. Colours are before the room's light.
static class CreaturePainter
{
    static readonly Color HatColor = Color.FromArgb(16, 14, 16), HatBand = Color.FromArgb(36, 32, 34);
    static readonly Color CoatColor = Color.FromArgb(32, 29, 33), CoatEdge = Color.FromArgb(50, 46, 50);
    static readonly Color FaceColor = Color.FromArgb(30, 31, 33), ChinColor = Color.FromArgb(108, 112, 112);
    // Grey skin lit by the candles, and the same skin seen outside, lit only by the night.
    static readonly Color SkinColor = Color.FromArgb(124, 128, 126), NightSkin = Color.FromArgb(66, 70, 72);
    static readonly Color CreaseColor = Color.FromArgb(76, 80, 80), NailColor = Color.FromArgb(146, 146, 136);
    // The eyes make their own light: a deep red with a brighter core.
    static readonly Color EyeColor = Color.FromArgb(214, 22, 16), EyeCore = Color.FromArgb(255, 120, 90);
    static readonly Color SackColor = Color.FromArgb(52, 44, 32), RopeColor = Color.FromArgb(82, 72, 50);
    static readonly Color TrousersColor = Color.FromArgb(22, 20, 24), ShoeColor = Color.FromArgb(12, 10, 10), CaneColor = Color.FromArgb(62, 44, 28);

    // Breathing, the sway of the coat and the twitch of the fingers, each in -1..1.
    public readonly record struct Pose(float Breath, float Sway, float Twitch);

    public static Pose PoseAt(double seconds) => new(
        (float)Math.Sin(2 * Math.PI * seconds / 3.8),
        (float)Math.Sin(2 * Math.PI * seconds / 5.3 + 1),
        (float)Math.Sin(2 * Math.PI * seconds * 0.7));

    // Where he stands outside, behind the window glass: feet and height in design coordinates.
    static (PointF Feet, float Height) Placement(Station station) => station switch
    {
        Station.Street => (new PointF(272, 514), 72),
        Station.Gate => (new PointF(280, 538), 122),
        Station.Yard => (new PointF(318, 714), 312),
        Station.Window => (new PointF(306, 1900), 1500),
        Station.Door => (new PointF(1622, 1010), 800),
        _ => (new PointF(1840, 2350), 2150),
    };

    // His two red eyes under the brim, in design coordinates, and the size of one eye. They make their
    // own light, so the renderer draws them after the light pass.
    public static (PointF Left, PointF Right, SizeF Eye) Eyes(Station station, Pose pose)
    {
        var (feet, height) = Placement(station);
        float y = feet.Y + height * (-0.885f - 0.0045f * pose.Breath);
        float spread = 0.019f * height;
        var eye = new SizeF(Math.Max(1.8f, 0.014f * height), Math.Max(1.2f, 0.0075f * height));
        return (new PointF(feet.X - spread, y), new PointF(feet.X + spread, y), eye);
    }

    // Now and then he blinks.
    public static bool Blinking(double seconds) => seconds / 4.7 % 1 < 0.035;

    // The area one eye and its red halo cover, around the eye's centre.
    public static RectangleF EyeBounds(SizeF eye) => RectangleF.FromLTRB(-eye.Width * 2.4f, -eye.Height * 3.4f, eye.Width * 2.4f, eye.Height * 3.4f);

    // One eye centred at the origin: the halo, the red eye and, up close, a brighter core.
    public static void DrawEye(Graphics g, SizeF eye, bool near)
    {
        StreetPainter.Glow(g, PointF.Empty, eye.Width * 2.4f, eye.Height * 3.4f, Color.FromArgb(near ? 110 : 150, EyeColor));
        using (var red = new SolidBrush(EyeColor)) g.FillEllipse(red, -eye.Width / 2, -eye.Height / 2, eye.Width, eye.Height);
        if (near) using (var core = new SolidBrush(EyeCore)) g.FillEllipse(core, -eye.Width * 0.18f, -eye.Height * 0.25f, eye.Width * 0.36f, eye.Height * 0.5f);
    }

    // Everything seen through the glass; the renderer has clipped to the panes.
    public static void DrawOutside(Graphics g, Station station, Pose pose)
    {
        if (station > Station.Window) return;
        var (feet, height) = Placement(station);
        Color? rim = station == Station.Window ? null : Color.FromArgb(station == Station.Yard ? 90 : 120, 190, 150, 96);
        DrawFigure(g, feet, height, pose, rim, details: station >= Station.Yard, sack: station != Station.Window, skinColor: NightSkin);
        if (station == Station.Street) StreetPainter.DrawGateBars(g);
        if (station == Station.Window) HandOnGlass(g, pose);
    }

    // Everything inside the room, drawn without the glass clip.
    public static void DrawInside(Graphics g, Station station, Pose pose)
    {
        switch (station)
        {
            case Station.Window:
                DrawCandy(g, new PointF(300, 792), -12, 1);
                break;
            case Station.Door:
            {
                var (feet, height) = Placement(station);
                var state = g.Save();
                g.SetClip(SceneLayout.DoorGap);
                DrawFigure(g, feet, height, pose, null, details: true, sack: false, skinColor: SkinColor);
                g.Restore(state);
                FingersOnDoor(g, pose);
                break;
            }
            case Station.Beside:
            {
                var (feet, height) = Placement(station);
                DrawFigure(g, feet, height, pose, Color.FromArgb(60, 255, 170, 90), details: true, sack: false, skinColor: SkinColor);
                Candles.DrawBody(g, 1);
                HandOnDesk(g, pose);
                break;
            }
        }
    }

    // The whole figure in its own coordinates: feet at (0, 0), top of the hat at (0, -1).
    static void DrawFigure(Graphics g, PointF feet, float height, Pose pose, Color? rim, bool details, bool sack, Color skinColor)
    {
        var state = g.Save();
        g.TranslateTransform(feet.X, feet.Y);
        g.ScaleTransform(height, height);
        float breath = 0.0045f * pose.Breath, sway = 0.006f * pose.Sway;
        PointF P(float x, float y)
        {
            float torso = Math.Clamp((-y - 0.3f) / 0.55f, 0, 1);
            float hem = y > -0.44f && y < -0.2f ? 1 : 0;
            return new PointF(x + sway * hem, y - breath * torso);
        }

        using var hat = new SolidBrush(HatColor);
        using var coat = new SolidBrush(CoatColor);
        using var skin = new SolidBrush(skinColor);
        using var edge = new Pen(CoatEdge, 0.004f);

        // The sack, behind the left shoulder (out of frame when he is close).
        if (sack)
        using (var sackPath = new GraphicsPath())
        {
            sackPath.AddBezier(P(-0.09f, -0.86f), P(-0.12f, -0.95f), P(-0.20f, -0.97f), P(-0.25f, -0.91f));
            sackPath.AddBezier(P(-0.25f, -0.91f), P(-0.29f, -0.84f), P(-0.28f, -0.73f), P(-0.23f, -0.68f));
            sackPath.AddBezier(P(-0.23f, -0.68f), P(-0.19f, -0.65f), P(-0.15f, -0.72f), P(-0.09f, -0.86f));
            sackPath.CloseFigure();
            using var brush = new SolidBrush(SackColor);
            g.FillPath(brush, sackPath);
            if (details)
                using (var fold = new Pen(Color.FromArgb(40, 34, 24), 0.004f)) { g.DrawBezier(fold, P(-0.22f, -0.91f), P(-0.25f, -0.84f), P(-0.25f, -0.77f), P(-0.21f, -0.71f)); }
        }

        // Legs and shoes.
        using (var trousers = new SolidBrush(TrousersColor))
        {
            g.FillPolygon(trousers, [P(-0.085f, -0.31f), P(-0.03f, -0.31f), P(-0.036f, -0.02f), P(-0.074f, -0.02f)]);
            g.FillPolygon(trousers, [P(0.03f, -0.31f), P(0.085f, -0.31f), P(0.074f, -0.02f), P(0.036f, -0.02f)]);
        }
        using (var shoes = new SolidBrush(ShoeColor))
        {
            g.FillEllipse(shoes, -0.10f, -0.032f, 0.075f, 0.036f);
            g.FillEllipse(shoes, 0.025f, -0.032f, 0.075f, 0.036f);
        }

        // The coat: shoulders hunched up to the chin, flaring to the hem.
        using var body = new GraphicsPath();
        body.AddBezier(P(-0.045f, -0.855f), P(-0.09f, -0.86f), P(-0.15f, -0.845f), P(-0.17f, -0.80f));
        body.AddBezier(P(-0.17f, -0.80f), P(-0.19f, -0.74f), P(-0.185f, -0.55f), P(-0.19f, -0.42f));
        body.AddLine(P(-0.19f, -0.42f), P(-0.20f, -0.30f));
        body.AddBezier(P(-0.20f, -0.30f), P(-0.10f, -0.285f), P(0.10f, -0.285f), P(0.20f, -0.30f));
        body.AddLine(P(0.20f, -0.30f), P(0.185f, -0.42f));
        body.AddBezier(P(0.185f, -0.42f), P(0.18f, -0.55f), P(0.185f, -0.74f), P(0.165f, -0.80f));
        body.AddBezier(P(0.165f, -0.80f), P(0.145f, -0.845f), P(0.09f, -0.86f), P(0.045f, -0.855f));
        body.CloseFigure();
        g.FillPath(coat, body);
        if (details)
        {
            g.DrawLine(edge, P(-0.045f, -0.855f), P(0.0f, -0.62f));
            g.DrawLine(edge, P(0.045f, -0.855f), P(0.0f, -0.62f));
            g.DrawLine(edge, P(0.0f, -0.62f), P(0.012f, -0.29f));
            using var button = new SolidBrush(CoatEdge);
            foreach (float y in new[] { -0.56f, -0.47f })
            {
                var b = P(0.008f, y);
                g.FillEllipse(button, b.X - 0.006f, b.Y - 0.006f, 0.012f, 0.012f);
            }
        }

        // Right sleeve down to the cane.
        using (var sleeve = new GraphicsPath())
        {
            sleeve.AddBezier(P(0.15f, -0.79f), P(0.20f, -0.74f), P(0.22f, -0.62f), P(0.226f, -0.52f));
            sleeve.AddLine(P(0.226f, -0.52f), P(0.19f, -0.50f));
            sleeve.AddBezier(P(0.19f, -0.50f), P(0.185f, -0.60f), P(0.17f, -0.70f), P(0.13f, -0.76f));
            sleeve.CloseFigure();
            g.FillPath(coat, sleeve);
            if (details) g.DrawPath(edge, sleeve);
        }

        // The cane, with its crook under the hand.
        using (var cane = new Pen(CaneColor, 0.012f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(cane, P(0.212f, -0.50f), new PointF(0.245f, 0f));
            g.DrawBezier(cane, P(0.212f, -0.50f), P(0.212f, -0.53f), P(0.19f, -0.535f), P(0.182f, -0.515f));
        }

        // The head under the brim: mostly shadow, a grey chin. The eyes come after the light pass.
        var face = P(0, -0.875f);
        using (var shade = new SolidBrush(FaceColor)) g.FillEllipse(shade, face.X - 0.052f, face.Y - 0.045f, 0.104f, 0.088f);
        if (details)
            using (var chin = new SolidBrush(Color.FromArgb(45, ChinColor))) g.FillPie(chin, face.X - 0.02f, face.Y + 0.018f, 0.04f, 0.022f, 0, 180);

        // The hat: a wide brim and a dented crown.
        var brim = P(0, -0.917f);
        g.FillEllipse(hat, brim.X - 0.15f, brim.Y - 0.018f, 0.30f, 0.036f);
        if (details) using (var brimEdge = new Pen(HatBand, 0.003f)) g.DrawArc(brimEdge, brim.X - 0.15f, brim.Y - 0.018f, 0.30f, 0.036f, 10, 160);
        using (var crown = new GraphicsPath())
        {
            crown.AddLine(P(-0.075f, -0.915f), P(-0.07f, -0.975f));
            crown.AddBezier(P(-0.07f, -0.975f), P(-0.05f, -1.0f), P(-0.015f, -0.985f), P(0f, -0.99f));
            crown.AddBezier(P(0f, -0.99f), P(0.015f, -0.985f), P(0.05f, -1.0f), P(0.07f, -0.975f));
            crown.AddLine(P(0.07f, -0.975f), P(0.075f, -0.915f));
            crown.CloseFigure();
            g.FillPath(hat, crown);
            if (details) using (var band = new Pen(HatBand, 0.012f)) g.DrawLine(band, P(-0.073f, -0.935f), P(0.073f, -0.935f));
            if (rim is { } light)
            {
                using var glow = new Pen(light, 1.6f / height);
                g.DrawPath(glow, crown);
                g.DrawPath(glow, body);
            }
        }

        // The right hand over the cane, fingers too long.
        var hand = P(0.205f, -0.512f);
        g.FillEllipse(skin, hand.X - 0.02f, hand.Y - 0.014f, 0.04f, 0.03f);
        using (var finger = new Pen(skinColor, 0.0075f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int i = 0; i < 4; i++)
            {
                float x = 0.196f + i * 0.009f, curl = pose.Twitch * 0.002f * (i % 2 == 0 ? 1 : -1);
                g.DrawBezier(finger, P(x, -0.505f), P(x - 0.004f, -0.485f), P(x - 0.006f + curl, -0.46f), P(x - 0.01f + i * 0.002f + curl, -0.432f));
            }

        // The left hand holding the sack's rope over the shoulder.
        if (!sack)
        {
            g.Restore(state);
            return;
        }
        var grip = P(-0.138f, -0.848f);
        g.FillEllipse(skin, grip.X - 0.018f, grip.Y - 0.012f, 0.036f, 0.026f);
        using (var finger = new Pen(skinColor, 0.007f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int i = 0; i < 4; i++)
                g.DrawBezier(finger, P(-0.15f + i * 0.008f, -0.85f), P(-0.152f + i * 0.008f, -0.87f), P(-0.16f + i * 0.007f, -0.885f), P(-0.172f + i * 0.007f + pose.Twitch * 0.002f, -0.888f));
        using (var rope = new Pen(RopeColor, 0.006f)) g.DrawBezier(rope, P(-0.15f, -0.86f), P(-0.17f, -0.9f), P(-0.19f, -0.95f), P(-0.2f, -0.975f));

        g.Restore(state);
    }

    // At the window: his hand pressed flat on the glass, fogging it around, fingers too long.
    static void HandOnGlass(Graphics g, Pose pose)
    {
        using (var fog = new SolidBrush(Color.FromArgb(9, 210, 220, 235))) g.FillEllipse(fog, 104, 512, 132, 200);
        using (var palm = new SolidBrush(NightSkin)) g.FillEllipse(palm, 144, 630, 52, 60);
        float t = pose.Twitch * 1.2f;
        PointF[][] fingers =
        [
            [new(154, 640), new(148, 600), new(145, 572 + t), new(143, 548 + t)],
            [new(166, 636), new(164, 592), new(163, 560 + t), new(162, 530 + t)],
            [new(178, 638), new(181, 598), new(183, 568 - t), new(184, 542 - t)],
            [new(190, 646), new(196, 614), new(199, 592 - t), new(201, 574 - t)],
            [new(150, 668), new(134, 650), new(124, 634), new(118, 620)],
        ];
        foreach (var joints in fingers) DrawFinger(g, joints, 10, NightSkin, Color.FromArgb(44, 47, 48), Color.FromArgb(88, 92, 94), pressed: true);
    }

    // At the door: four long fingers curling around the edge of the leaf, from the dark of the gap.
    static void FingersOnDoor(Graphics g, Pose pose)
    {
        for (int i = 0; i < 4; i++)
        {
            float y = 552 + i * 19, reach = i == 1 ? 1.1f : i == 3 ? 0.8f : 1f, t = pose.Twitch * (i % 2 == 0 ? 1.2f : -1.2f);
            PointF[] joints = [new(1622, y), new(1622 + 24 * reach, y - 7), new(1622 + 42 * reach, y - 2 + t), new(1622 + 54 * reach, y + 9 + t)];
            DrawFinger(g, joints, 9, SkinColor, CreaseColor, NailColor, pressed: false);
        }
    }

    // Beside you: his hand on the desk, the sleeve coming from the dark, holding a sweet out to you.
    static void HandOnDesk(Graphics g, Pose pose)
    {
        using (var sleeve = new SolidBrush(CoatColor)) g.FillPolygon(sleeve, [new(1780, 872), new(1676, 914), new(1640, 966), new(1704, 994), new(1800, 944)]);
        using (var cuff = new Pen(CoatEdge, 3)) g.DrawLine(cuff, 1676, 916, 1642, 964);
        using (var back = new SolidBrush(SkinColor)) g.FillEllipse(back, 1584, 928, 70, 46);
        float t = pose.Twitch * 2;
        PointF[][] fingers =
        [
            [new(1590, 940), new(1550, 934), new(1520, 938 + t), new(1498, 948 + t)],
            [new(1592, 950), new(1548, 950), new(1514, 956), new(1490, 966)],
            [new(1596, 960), new(1556, 966), new(1526, 974), new(1506, 984)],
            [new(1604, 968), new(1572, 978), new(1548, 988), new(1532, 996)],
            [new(1612, 936), new(1586, 918), new(1558, 912), new(1532, 914)],
        ];
        foreach (var joints in fingers) DrawFinger(g, joints, 9, SkinColor, CreaseColor, NailColor, pressed: false);
        DrawCandy(g, new PointF(1514, 922), 8, 1.1f);
    }

    // A long bony finger through its joints (knuckle to tip): thinner at each joint, a crease at the
    // joints, and a nail at the tip, or a pale pad where it presses on glass.
    static void DrawFinger(Graphics g, PointF[] joints, float width, Color skin, Color crease, Color nail, bool pressed)
    {
        for (int i = 0; i < joints.Length - 1; i++)
        {
            using var pen = new Pen(skin, width * (1 - 0.16f * i)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, joints[i], joints[i + 1]);
        }
        using var line = new Pen(crease, Math.Max(1, width * 0.15f));
        for (int i = 1; i < joints.Length - 1; i++)
        {
            var (dx, dy) = Direction(joints[i - 1], joints[i + 1]);
            float half = width * 0.32f;
            g.DrawLine(line, joints[i].X - dy * half, joints[i].Y + dx * half, joints[i].X + dy * half, joints[i].Y - dx * half);
        }
        var tip = joints[^1];
        var (ux, uy) = Direction(joints[^2], tip);
        var state = g.Save();
        g.TranslateTransform(tip.X - ux * width * 0.3f, tip.Y - uy * width * 0.3f);
        g.RotateTransform((float)(Math.Atan2(uy, ux) * 180 / Math.PI));
        using (var brush = new SolidBrush(nail))
        {
            if (pressed) g.FillEllipse(brush, -width * 0.55f, -width * 0.42f, width * 1.0f, width * 0.84f);
            else g.FillEllipse(brush, -width * 0.2f, -width * 0.28f, width * 0.55f, width * 0.56f);
        }
        g.Restore(state);
    }

    static (float X, float Y) Direction(PointF from, PointF to)
    {
        float dx = to.X - from.X, dy = to.Y - from.Y, length = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
        return (dx / length, dy / length);
    }

    // A wrapped sweet: a red body and two twisted ends of paper.
    public static void DrawCandy(Graphics g, PointF center, float degrees, float scale)
    {
        var state = g.Save();
        g.TranslateTransform(center.X, center.Y);
        g.RotateTransform(degrees);
        g.ScaleTransform(scale, scale);
        using var paper = new SolidBrush(Color.FromArgb(220, 222, 210, 186));
        g.FillPolygon(paper, [new(-12, 0), new(-22, -8), new(-24, 0), new(-22, 8)]);
        g.FillPolygon(paper, [new(12, 0), new(22, -8), new(24, 0), new(22, 8)]);
        using (var body = new SolidBrush(Color.FromArgb(180, 54, 44))) g.FillEllipse(body, -13, -7, 26, 14);
        using (var shine = new SolidBrush(Color.FromArgb(150, 236, 150, 130))) g.FillEllipse(shine, -7, -5, 9, 4);
        g.Restore(state);
    }
}
