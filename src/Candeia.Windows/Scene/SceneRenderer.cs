using System.Diagnostics;
using System.Drawing.Drawing2D;
using Candeia.Night;

namespace Candeia.Scene;

public enum SceneQuality { High, Reduced }

// What the scene shows at one moment. SinceArrival counts the seconds since he reached this station.
readonly record struct SceneState(double Seconds, Station Station, double SinceArrival, SceneQuality Quality);

// How long each layer of the last frame took, in milliseconds.
sealed class SceneTimings
{
    public double Copy, Figures, LightMap, Light, Flames, Present;
    public double Render => Copy + Figures + LightMap + Light + Flames;
}

// Draws the room, layer by layer, into a frame the window presents:
//   1. the cached room (painted once per size), kept clean;
//   2. a working copy of the room with what moves drawn on it: rain, the creature. Each frame only the
//      part the last frame drew on is restored from the clean room;
//   3. the light map, from the candles, the monitor and the window;
//   4. the light pass, from the working copy into the frame, with vignette and grain, around the editor;
//   5. what makes light: the flames, the power LED, his red eyes; and the monitor glass around the editor.
sealed class SceneRenderer : IDisposable
{
    readonly float[] flicker = new float[Candles.All.Length];
    readonly PointLight[] points = new PointLight[Candles.All.Length + 2];
    readonly AreaLight[] areas = new AreaLight[2];
    readonly Random grainOffsets = new(5);
    PixelBuffer room = null!, stage = null!, frame = null!;
    Graphics stageGraphics = null!, graphics = null!;
    Rectangle[] lastDirty = [];
    Bitmap? eyeSprite;
    Station eyeStation;
    Lighting lighting = null!;
    Matrix transform = null!;
    GraphicsPath lowerGlass = null!, allGlass = null!;
    Bitmap glow = null!, glowDim = null!, ledGlow = null!;
    (int Start, int Length)[] screenRing = [];
    Rectangle editor;

    public SceneRenderer(Size size) => Build(size);

    public SceneLayout Layout { get; private set; } = null!;
    public double RoomMilliseconds { get; private set; }

    public void Resize(Size size)
    {
        if (size == Layout.Size) return;
        Free();
        Build(size);
    }

    void Build(Size size)
    {
        long start = Stopwatch.GetTimestamp();
        Layout = new SceneLayout(size);
        room = new PixelBuffer(size.Width, size.Height);
        frame = new PixelBuffer(size.Width, size.Height);
        using (var g = Graphics.FromImage(room.Bitmap)) RoomPainter.Paint(g, Layout);
        stage = new PixelBuffer(size.Width, size.Height);
        room.CopyTo(stage);
        room.CopyTo(frame); // until the first frame, the window shows the unlit room, never garbage
        lastDirty = [];
        eyeSprite?.Dispose();
        eyeSprite = null;
        stageGraphics = Graphics.FromImage(stage.Bitmap);
        graphics = Graphics.FromImage(frame.Bitmap);
        lighting = new Lighting(frame.Stride, frame.Height);
        transform = Layout.Transform();
        lowerGlass = SceneLayout.LowerGlass();
        allGlass = SceneLayout.AllGlass();
        editor = Layout.EditorBounds();
        screenRing = ScreenRing();
        glow = Sprite(Candles.GlowRadius, Candles.GlowColor);
        glowDim = Sprite(Candles.GlowRadius, Color.FromArgb(Candles.GlowColor.A / 2, Candles.GlowColor));
        ledGlow = Sprite(12, Color.FromArgb(90, 255, 170, 60));
        RoomMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    // How close he feels, from 0 in the street to 1 beside you.
    public static float Tension(Station station) => station switch
    {
        Station.Street => 0f,
        Station.Gate => 0.12f,
        Station.Yard => 0.28f,
        Station.Window => 0.5f,
        Station.Door => 0.72f,
        _ => 1f,
    };

    public void Render(in SceneState state, SceneTimings timings)
    {
        long t0 = Stopwatch.GetTimestamp();
        foreach (var area in lastDirty) room.CopyRect(stage, area);
        lastDirty = Dirty(state.Station);
        long t1 = Stopwatch.GetTimestamp();

        // Everything that moves stays inside the dirty rectangles (Dirty lists where each station draws),
        // so the next frame knows what to clean. It is not set as a clip: on the large figure beside you,
        // the clip measured slower than drawing freely.
        var g = stageGraphics;
        g.ResetClip();
        g.Transform = transform;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool high = state.Quality == SceneQuality.High;
        var pose = CreaturePainter.PoseAt(state.Seconds);
        var saved = g.Save();
        g.SetClip(allGlass);
        if (state.Station == Station.Window) Rain.DrawStreaks(g, state.Seconds, high ? 90 : 30);
        g.SetClip(lowerGlass);
        CreaturePainter.DrawOutside(g, state.Station, pose);
        g.SetClip(allGlass);
        if (state.Station != Station.Window) Rain.DrawStreaks(g, state.Seconds, high ? 90 : 30);
        Rain.DrawDrops(g, state.Seconds, high ? 9 : 4);
        g.Restore(saved);
        CreaturePainter.DrawInside(g, state.Station, pose);
        long t2 = Stopwatch.GetTimestamp();

        float tension = Tension(state.Station);
        // A draught makes the flames bow when he arrives somewhere.
        float dim = state.SinceArrival is >= 0 and < 3 ? 0.55f * (float)Math.Exp(-state.SinceArrival * 2.2) : 0;
        BuildLights(state.Seconds, state.Station, tension, dim);
        long t3 = Stopwatch.GetTimestamp();

        int grainX = grainOffsets.Next(0, 32) * Lighting.Cell, grainY = grainOffsets.Next(0, 256);
        lighting.Apply(stage.Pixels, frame.Pixels, editor, grainX, grainY, withGrain: high);
        long t4 = Stopwatch.GetTimestamp();
        g = graphics;

        // The monitor glass around the editor goes back to its own dark colour: the light pass works in
        // whole cells and lit part of it.
        foreach (var (start, length) in screenRing) Array.Copy(room.Pixels, start, frame.Pixels, start, length);
        g.ResetTransform();
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        for (int i = 0; i < Candles.All.Length; i++) DrawSprite(g, dim > 0.2f ? glowDim : glow, Candles.GlowAt(i));
        DrawSprite(g, ledGlow, SceneLayout.Led);
        g.PixelOffsetMode = PixelOffsetMode.Default;
        g.Transform = transform;
        for (int i = 0; i < Candles.All.Length; i++) Candles.DrawFlame(g, i, flicker[i], state.Seconds, dim);
        g.FillEllipse(ledBrush, SceneLayout.Led.X - 3.5f, SceneLayout.Led.Y - 3.5f, 7, 7);
        // His eyes, in the same clip as the figure: the glass, or the door's gap, where only one eye shows
        // past the edge of the leaf.
        if (!CreaturePainter.Blinking(state.Seconds))
        {
            var clip = g.Save();
            if (state.Station <= Station.Window) g.SetClip(lowerGlass);
            else if (state.Station == Station.Door) g.SetClip(SceneLayout.DoorGap);
            g.ResetTransform();
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var eyes = CreaturePainter.Eyes(state.Station, pose);
            var sprite = EyeSprite(state.Station);
            DrawSprite(g, sprite, eyes.Left);
            DrawSprite(g, sprite, eyes.Right);
            g.Restore(clip);
        }
        long t5 = Stopwatch.GetTimestamp();

        timings.Copy = Milliseconds(t0, t1);
        timings.Figures = Milliseconds(t1, t2);
        timings.LightMap = Milliseconds(t2, t3);
        timings.Light = Milliseconds(t3, t4);
        timings.Flames = Milliseconds(t4, t5);
    }

    static readonly SolidBrush ledBrush = new(Color.FromArgb(255, 186, 80));

    // Where the moving things can draw at a station, in pixels: always the window (the rain), plus the
    // sill, the door or the space beside the desk. Kept apart: their union would cover most of the screen.
    Rectangle[] Dirty(Station station)
    {
        var window = Bounded(Layout.ToPixels(SceneLayout.WindowOpening));
        RectangleF? extra = station switch
        {
            Station.Window => Layout.ToPixels(RectangleF.FromLTRB(46, 776, 454, 816)),
            Station.Door => Layout.ToPixels(RectangleF.FromLTRB(1556, 234, 1704, 1000)),
            // Beside you: from his coat's edge (x 1440 at the bottom of the screen) and his hat (y 190).
            Station.Beside => RectangleF.FromLTRB(Layout.ToPixels(new PointF(1430, 0)).X, Layout.ToPixels(new PointF(0, 170)).Y, frame.Width, frame.Height),
            _ => null,
        };
        return extra is { } more ? [window, Bounded(more)] : [window];
    }

    Rectangle Bounded(RectangleF area) => Rectangle.Intersect(Rectangle.Inflate(Rectangle.Round(area), 4, 4), new Rectangle(0, 0, frame.Width, frame.Height));

    // One red eye with its halo, drawn once per station (its size depends on how close he is).
    Bitmap EyeSprite(Station station)
    {
        if (eyeSprite is not null && eyeStation == station) return eyeSprite;
        eyeSprite?.Dispose();
        var size = CreaturePainter.Eyes(station, default).Eye;
        var bounds = CreaturePainter.EyeBounds(size);
        int width = (int)Math.Ceiling(bounds.Width * Layout.Scale) + 2, height = (int)Math.Ceiling(bounds.Height * Layout.Scale) + 2;
        var sprite = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(sprite))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(width / 2f, height / 2f);
            g.ScaleTransform(Layout.Scale, Layout.Scale);
            CreaturePainter.DrawEye(g, size, near: station >= Station.Window);
        }
        eyeStation = station;
        return eyeSprite = sprite;
    }

    // A soft halo drawn once per window size, to be copied over the frame.
    Bitmap Sprite(float designRadius, Color color)
    {
        int radius = Math.Max(2, (int)Math.Ceiling(designRadius * Layout.Scale));
        var sprite = new Bitmap(radius * 2, radius * 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(sprite);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        StreetPainter.Glow(g, new PointF(radius, radius), radius, radius, color);
        return sprite;
    }

    void DrawSprite(Graphics g, Bitmap sprite, PointF designCenter)
    {
        var at = Layout.ToPixels(designCenter);
        g.DrawImage(sprite, new Rectangle((int)(at.X - sprite.Width / 2f), (int)(at.Y - sprite.Height / 2f), sprite.Width, sprite.Height));
    }

    // The rows of the monitor glass that lie outside the editor's untouched cells, as runs of pixels.
    (int Start, int Length)[] ScreenRing()
    {
        var screen = Layout.ToPixels(SceneLayout.Screen);
        float radius = SceneLayout.ScreenRadius * Layout.Scale;
        int skipFrom = (editor.Left + Lighting.Cell - 1) / Lighting.Cell * Lighting.Cell, skipTo = editor.Right / Lighting.Cell * Lighting.Cell;
        var runs = new List<(int, int)>();
        int top = (int)Math.Ceiling(screen.Top), bottom = (int)Math.Floor(screen.Bottom);
        for (int y = Math.Max(0, top); y < Math.Min(frame.Height, bottom); y++)
        {
            float inset = 0, centre = y + 0.5f;
            float fromTop = centre - screen.Top, fromBottom = screen.Bottom - centre;
            float edge = Math.Min(fromTop, fromBottom);
            if (edge < radius) inset = radius - MathF.Sqrt(Math.Max(0, radius * radius - (radius - edge) * (radius - edge)));
            int x0 = Math.Max(0, (int)Math.Ceiling(screen.Left + inset)), x1 = Math.Min(frame.Width, (int)Math.Floor(screen.Right - inset));
            if (x1 <= x0) continue;
            int line = y * frame.Stride;
            bool skipped = y >= editor.Top && y < editor.Bottom && skipTo > skipFrom;
            if (!skipped) { runs.Add((line + x0, x1 - x0)); continue; }
            if (skipFrom > x0) runs.Add((line + x0, Math.Min(skipFrom, x1) - x0));
            if (x1 > skipTo) runs.Add((line + Math.Max(skipTo, x0), x1 - Math.Max(skipTo, x0)));
        }
        return runs.ToArray();
    }

    void BuildLights(double seconds, Station station, float tension, float dim)
    {
        float scale = Layout.Scale;
        for (int i = 0; i < Candles.All.Length; i++)
        {
            var c = Candles.All[i];
            float f = flicker[i] = Candles.Flicker(i, seconds, 0.15f + 0.85f * tension);
            float strength = c.Strength * (1 + (0.10f + 0.10f * tension) * f) * (1 - dim);
            var at = Layout.ToPixels(Candles.LightAt(i, f));
            points[i] = new PointLight(at.X, at.Y, c.Reach * scale, 120 * scale, 1.0f * strength, 0.62f * strength, 0.30f * strength);
        }
        var screen = Layout.ToPixels(new PointF(960, 462));
        points[^2] = new PointLight(screen.X, screen.Y, 1200 * scale, 560 * scale, 0.34f, 0.235f, 0.11f);
        float moon = 1 - 0.3f * tension;
        var spill = Layout.ToPixels(new PointF(250, 520));
        points[^1] = new PointLight(spill.X, spill.Y, 460 * scale, 220 * scale, 0.10f * moon, 0.13f * moon, 0.20f * moon);
        // The outside through the window, and the moonlit corridor through the door. Kept a cell inside
        // the openings, so the frames around them stay in the room's light.
        float outside = 1 - 0.12f * tension;
        areas[0] = new AreaLight(Layout.ToPixels(RectangleF.Inflate(SceneLayout.WindowOpening, -6, -6)), 4 * scale, 0.84f * outside, 0.86f * outside, 0.92f * outside);
        // Beside you, he stands in front of the door and hides the corridor's light.
        float corridor = station == Station.Beside ? 0 : 0.32f * outside;
        areas[1] = new AreaLight(Layout.ToPixels(RectangleF.Inflate(SceneLayout.DoorGap, -4, -6)), 4 * scale, corridor * 0.9f, corridor, corridor * 1.2f);
        float ambient = 1 - 0.35f * tension;
        lighting.Build(points, areas, 0.028f * ambient, 0.032f * ambient, 0.048f * ambient, 0.55f + 0.3f * tension);
    }

    // Writes a line of text in the top-left corner of the frame (the measurements panel).
    public void DrawOverlay(string text)
    {
        var g = graphics;
        g.ResetTransform();
        using var font = new Font("Consolas", Math.Max(11, 15 * Layout.Scale), GraphicsUnit.Pixel);
        var size = g.MeasureString(text, font);
        using (var back = new SolidBrush(Color.FromArgb(180, 0, 0, 0))) g.FillRectangle(back, 8, 8, size.Width + 12, size.Height + 8);
        using var ink = new SolidBrush(Palette.Amber);
        g.DrawString(text, font, ink, 14, 12);
    }

    public void Present(IntPtr hdc) => frame.Present(hdc);

    static double Milliseconds(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    void Free()
    {
        graphics?.Dispose();
        stageGraphics?.Dispose();
        stage?.Dispose();
        room?.Dispose();
        frame?.Dispose();
        transform?.Dispose();
        lowerGlass?.Dispose();
        allGlass?.Dispose();
        glow?.Dispose();
        glowDim?.Dispose();
        eyeSprite?.Dispose();
        ledGlow?.Dispose();
    }

    public void Dispose() => Free();
}
