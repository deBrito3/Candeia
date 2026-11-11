using System.Drawing.Drawing2D;

namespace Candeia.Scene;

// The three candles on the desk: where they stand, how their flames move and how they are drawn.
// Positions are in design coordinates; Base is where the wax meets the saucer and Top is the wick.
static class Candles
{
    public readonly record struct Candle(float X, float Base, float Top, float Width, float Strength, float Reach, double Seed);

    public static readonly Candle[] All =
    [
        new(395, 930, 792, 34, 1.15f, 700, 0.0),
        new(1497, 930, 832, 30, 1.00f, 720, 1.7),
        new(1370, 1036, 990, 36, 0.85f, 520, 3.1),
    ];

    // Where the light comes from: a little above the wick, in the brightest part of the flame.
    public static PointF LightAt(int index, float flicker)
    {
        var c = All[index];
        return new(c.X + flicker * 1.5f, c.Top - 16);
    }

    // A smooth wobble in -1..1, different for each candle. Agitation (0..1) adds faster, deeper gusts,
    // for when he is near.
    public static float Flicker(int index, double seconds, float agitation)
    {
        double s = All[index].Seed;
        double calm = 0.55 * Math.Sin(2 * Math.PI * 1.3 * seconds + s)
            + 0.30 * Math.Sin(2 * Math.PI * 2.9 * seconds + 2.3 * s)
            + 0.15 * Math.Sin(2 * Math.PI * 6.1 * seconds + 4.1 * s);
        double gust = Math.Sin(2 * Math.PI * 9.7 * seconds + s) * Math.Max(0, Math.Sin(2 * Math.PI * 0.37 * seconds + s));
        return (float)Math.Clamp(calm * (1 - agitation * 0.4) + gust * agitation, -1, 1);
    }

    // The halo around each flame, in design pixels. The renderer keeps it as a ready-made image.
    public const float GlowRadius = 72;
    public static readonly Color GlowColor = Color.FromArgb(62, 255, 172, 80);

    // Where the halo is centred.
    public static PointF GlowAt(int index) => new(All[index].X, All[index].Top - 23);

    // The flame, drawn after the light pass because it makes light. Dim (0..1) shrinks it, as when a
    // draught announces him.
    public static void DrawFlame(Graphics g, int index, float flicker, double seconds, float dim)
    {
        var c = All[index];
        float x = c.X, y = c.Top - 3;
        float height = 34 * (1 + 0.22f * flicker) * (1 - 0.45f * dim);
        float sway = 3.5f * (float)Math.Sin(2 * Math.PI * 0.8 * seconds + c.Seed * 2) + 2f * flicker;

        using (var path = new GraphicsPath())
        {
            path.AddBezier(new PointF(x, y + 3), new PointF(x - 9, y - 2), new PointF(x - 6.5f, y - height * 0.55f), new PointF(x + sway, y - height));
            path.AddBezier(new PointF(x + sway, y - height), new PointF(x + 6.5f, y - height * 0.55f), new PointF(x + 9, y - 2), new PointF(x, y + 3));
            path.CloseFigure();
            using var body = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(x + sway * 0.15f, y - height * 0.3f),
                CenterColor = Color.FromArgb(255, 255, 250, 220),
                SurroundColors = [Color.FromArgb(60, 255, 146, 36)],
            };
            g.FillPath(body, path);
        }
        using (var blue = new SolidBrush(Color.FromArgb(120, 96, 126, 255))) g.FillEllipse(blue, x - 3.5f, y - 4, 7, 6);
        using var wick = new Pen(Color.FromArgb(230, 34, 22, 16), 2f);
        g.DrawLine(wick, x, y + 3, x + sway * 0.12f, y - 5);
    }

    // The saucer and the wax, with its drips. Part of the cached room; drawn again over a figure that
    // stands behind a candle.
    public static void DrawBody(Graphics g, int index)
    {
        var c = All[index];
        float half = c.Width / 2;
        using (var saucer = new SolidBrush(Color.FromArgb(150, 146, 136))) g.FillEllipse(saucer, c.X - c.Width * 1.6f, c.Base - 10, c.Width * 3.2f, 20);
        using (var rim = new Pen(Color.FromArgb(104, 100, 92), 2)) g.DrawEllipse(rim, c.X - c.Width * 1.6f, c.Base - 10, c.Width * 3.2f, 20);
        using (var pool = new SolidBrush(Color.FromArgb(222, 212, 180))) g.FillEllipse(pool, c.X - half * 1.6f, c.Base - 7, half * 3.2f, 10);

        var wax = RectangleF.FromLTRB(c.X - half, c.Top, c.X + half, c.Base);
        using (var brush = new LinearGradientBrush(new RectangleF(wax.X - 1, wax.Y, wax.Width + 2, wax.Height), Color.FromArgb(238, 228, 196), Color.FromArgb(168, 156, 124), LinearGradientMode.Horizontal))
            g.FillRectangle(brush, wax);
        using var drip = new SolidBrush(Color.FromArgb(236, 226, 194));
        float length = c.Base - c.Top;
        (float at, float reach)[] drips = [(-0.75f, 0.30f), (0.05f, 0.55f), (0.55f, 0.18f)];
        foreach (var (at, reach) in drips)
        {
            float dx = c.X + at * half;
            g.FillRectangle(drip, dx - 2.5f, c.Top, 5, length * reach);
            g.FillEllipse(drip, dx - 3.5f, c.Top + length * reach - 3, 7, 7);
        }
        using (var top = new SolidBrush(Color.FromArgb(246, 238, 212))) g.FillEllipse(top, c.X - half, c.Top - 4, c.Width, 8);
        using (var hollow = new SolidBrush(Color.FromArgb(200, 188, 156))) g.FillEllipse(hollow, c.X - half * 0.55f, c.Top - 2, c.Width * 0.55f, 4);
    }
}
