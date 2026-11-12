using System.Diagnostics;
using System.Drawing.Drawing2D;
using Candeia.Night;

namespace Candeia.Scene;

public enum SceneQuality { High, Reduced }

// What the scene shows at one moment. SinceArrival counts the seconds since he reached this station.
// Visible is false where the act only lets him be heard; Apparition is a scripted glimpse of him somewhere
// else (a silhouette in the yard). HouseLight (0 to 1) is the room's bulb, lit by the lamps; CandlesOut
// counts the candles that went out; Flash (0 to 1) is a lightning flash.
// For the scares: Lit (0 to 1) lights him up, the only time he is seen whole; Match (0 to 1) is a match
// struck on the desk; Sack (0 to 1) is the sack closing over the screen; Dark (0 to 1) darkens everything.
readonly record struct SceneState(double Seconds, Station Station, double SinceArrival, SceneQuality Quality,
    bool Visible = true, float HouseLight = 0, int CandlesOut = 0, float Flash = 0, Station? Apparition = null,
    float Lit = 0, float Match = 0, float Sack = 0, float Dark = 0, bool Candy = false, bool Panels = true);

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
    readonly PointLight[] points = new PointLight[Candles.All.Length + 5];
    readonly List<Station> drawn = new(2);
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
    Font? captionFont;

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
        captionFont?.Dispose();
        captionFont = null;
        stageGraphics = Graphics.FromImage(stage.Bitmap);
        graphics = Graphics.FromImage(frame.Bitmap);
        lighting = new Lighting(frame.Stride, frame.Height);
        transform = Layout.Transform();
        lowerGlass = SceneLayout.LowerGlass();
        allGlass = SceneLayout.AllGlass();
        editor = Layout.PanelBounds();
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
        drawn.Clear();
        if (state.Visible) drawn.Add(state.Station);
        if (state.Apparition is { } glimpse && !drawn.Contains(glimpse)) drawn.Add(glimpse);
        lastDirty = drawn.Concat(state.Candy ? [Station.Window] : []).DefaultIfEmpty(Station.Street).SelectMany(Dirty).Distinct().ToArray();
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
        foreach (var station in drawn) CreaturePainter.DrawOutside(g, station, pose);
        g.SetClip(allGlass);
        if (state.Station != Station.Window) Rain.DrawStreaks(g, state.Seconds, high ? 90 : 30);
        Rain.DrawDrops(g, state.Seconds, high ? 9 : 4);
        g.Restore(saved);
        foreach (var station in drawn) if (station >= Station.Window) CreaturePainter.DrawInside(g, station, pose);
        // His sweet, left on the windowsill while the offer stands.
        if (state.Candy && !drawn.Contains(Station.Window)) CreaturePainter.DrawCandy(g, new PointF(300, 792), -12, 1);
        long t2 = Stopwatch.GetTimestamp();

        float tension = Tension(state.Station);
        // A draught makes the flames bow when he arrives somewhere.
        float dim = state.SinceArrival is >= 0 and < 3 ? 0.55f * (float)Math.Exp(-state.SinceArrival * 2.2) : 0;
        BuildLights(state, tension, dim);
        long t3 = Stopwatch.GetTimestamp();

        int grainX = grainOffsets.Next(0, 32) * Lighting.Cell, grainY = grainOffsets.Next(0, 256);
        // Without the panels (the capture takes the whole screen), the light pass covers the monitor too.
        lighting.Apply(stage.Pixels, frame.Pixels, state.Panels ? editor : Rectangle.Empty, grainX, grainY, withGrain: high);
        long t4 = Stopwatch.GetTimestamp();
        g = graphics;

        // The monitor glass around the editor goes back to its own dark colour: the light pass works in
        // whole cells and lit part of it.
        if (state.Panels) foreach (var (start, length) in screenRing) Array.Copy(room.Pixels, start, frame.Pixels, start, length);
        g.ResetTransform();
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        int lit = Candles.All.Length - Math.Clamp(state.CandlesOut, 0, Candles.All.Length);
        for (int i = 0; i < lit; i++) DrawSprite(g, dim > 0.2f ? glowDim : glow, Candles.GlowAt(i));
        DrawSprite(g, ledGlow, SceneLayout.Led);
        g.PixelOffsetMode = PixelOffsetMode.Default;
        g.Transform = transform;
        for (int i = 0; i < lit; i++) Candles.DrawFlame(g, i, flicker[i], state.Seconds, dim);
        g.FillEllipse(ledBrush, SceneLayout.Led.X - 3.5f, SceneLayout.Led.Y - 3.5f, 7, 7);
        if (state.HouseLight > 0.01f) DrawBulb(g, state.HouseLight);
        if (state.Sack > 0 || state.Dark > 0) Cover(g, state.Sack, state.Dark);
        if (state.Match > 0.03f)
        {
            g.Transform = transform;
            DrawMatch(g, state.Match, state.Seconds);
        }
        // His eyes, in the same clip as the figure: the glass, or the door's gap, where only one eye shows
        // past the edge of the leaf. Once the sack is over them, they are gone.
        float sackEdge = state.Sack > 0 ? state.Sack * Layout.Size.Height * 1.15f - 30 * Layout.Scale : float.MinValue;
        if (!CreaturePainter.Blinking(state.Seconds))
            foreach (var station in drawn)
            {
                var eyes = CreaturePainter.Eyes(station, pose);
                if (Layout.ToPixels(eyes.Left).Y < sackEdge) continue;
                var clip = g.Save();
                g.Transform = transform;
                if (station <= Station.Window) g.SetClip(lowerGlass);
                else if (station == Station.Door) g.SetClip(SceneLayout.DoorGap);
                g.ResetTransform();
                g.PixelOffsetMode = PixelOffsetMode.Half;
                var sprite = EyeSprite(station);
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

    // The match struck on the desk, in front of the keyboard: the stick, its head and a flame that flares
    // and then burns down.
    static void DrawMatch(Graphics g, float level, double seconds)
    {
        const float x = 1180, y = 960;
        float height = 26 + 46 * level;
        float sway = 2.5f * (float)Math.Sin(seconds * 13);
        StreetPainter.Glow(g, new PointF(x, y - height * 0.4f), 60 + 150 * level, 60 + 150 * level, Color.FromArgb((int)(190 * level), 255, 214, 150));
        using (var stick = new Pen(Color.FromArgb(255, 170, 128, 80), 4f)) g.DrawLine(stick, x + 34, y + 38, x, y);
        using (var head = new SolidBrush(Color.FromArgb(255, 70, 26, 18))) g.FillEllipse(head, x - 5, y - 5, 10, 10);
        using var path = new GraphicsPath();
        path.AddBezier(new PointF(x, y + 2), new PointF(x - 11, y - 3), new PointF(x - 8, y - height * 0.55f), new PointF(x + sway, y - height));
        path.AddBezier(new PointF(x + sway, y - height), new PointF(x + 8, y - height * 0.55f), new PointF(x + 11, y - 3), new PointF(x, y + 2));
        path.CloseFigure();
        using var body = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(x, y - height * 0.3f),
            CenterColor = Color.FromArgb((int)(255 * Math.Min(1, level * 2)), 255, 252, 230),
            SurroundColors = [Color.FromArgb((int)(90 * Math.Min(1, level * 2)), 255, 150, 40)],
        };
        g.FillPath(body, path);
    }

    // The sack closing over the screen from the top, its edge uneven, the weave showing; and a darkening.
    void Cover(Graphics g, float sack, float dark)
    {
        g.ResetTransform();
        int width = Layout.Size.Width, height = Layout.Size.Height;
        if (sack > 0)
        {
            float bottom = sack * height * 1.15f;
            var edge = new List<PointF> { new(0, -10), new(width, -10) };
            for (int x = width; x >= 0; x -= 40) edge.Add(new PointF(x, bottom + 26 * Layout.Scale * (float)Math.Sin(x / (130.0 * Layout.Scale) + sack * 3)));
            using (var cloth = new SolidBrush(Color.FromArgb(255, 26, 20, 13))) g.FillPolygon(cloth, edge.ToArray());
            using var weave = new Pen(Color.FromArgb(255, 14, 11, 7), Math.Max(1, 2 * Layout.Scale));
            for (float y = 6 * Layout.Scale; y < bottom - 10; y += 9 * Layout.Scale) g.DrawLine(weave, 0, y, width, y + 3 * Layout.Scale);
        }
        if (dark > 0)
            using (var shade = new SolidBrush(Color.FromArgb((int)(255 * Math.Clamp(dark, 0, 1)), 0, 0, 0))) g.FillRectangle(shade, 0, 0, width, height);
    }

    // The room's bulb, lit as the lamps of the night light: a glowing glass and its halo.
    static void DrawBulb(Graphics g, float level)
    {
        var at = SceneLayout.Bulb;
        StreetPainter.Glow(g, at, 90 * level + 20, 90 * level + 20, Color.FromArgb((int)(150 * level), 255, 226, 170));
        using var glass = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1, level * 1.5f)), 255, 236, 196));
        g.FillEllipse(glass, at.X - 13, at.Y - 15, 26, 30);
    }

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

    void BuildLights(in SceneState state, float tension, float dim)
    {
        double seconds = state.Seconds;
        var station = state.Station;
        float scale = Layout.Scale;
        int lit = Candles.All.Length - Math.Clamp(state.CandlesOut, 0, Candles.All.Length);
        for (int i = 0; i < Candles.All.Length; i++)
        {
            var c = Candles.All[i];
            float f = flicker[i] = Candles.Flicker(i, seconds, 0.15f + 0.85f * tension);
            float strength = i < lit ? c.Strength * (1 + (0.10f + 0.10f * tension) * f) * (1 - dim) : 0;
            var at = Layout.ToPixels(Candles.LightAt(i, f));
            points[i] = new PointLight(at.X, at.Y, c.Reach * scale, 120 * scale, 1.0f * strength, 0.62f * strength, 0.30f * strength);
        }
        int n = Candles.All.Length;
        var screen = Layout.ToPixels(new PointF(960, 462));
        points[n] = new PointLight(screen.X, screen.Y, 1200 * scale, 560 * scale, 0.34f, 0.235f, 0.11f);
        // A match struck on the desk, and the light on him when he is seen whole.
        var match = Layout.ToPixels(new PointF(1180, 960));
        points[n + 3] = new PointLight(match.X, match.Y, 1100 * scale, 220 * scale, 1.6f * state.Match, 1.1f * state.Match, 0.55f * state.Match);
        var face = Layout.ToPixels(new PointF(1760, 520));
        points[n + 4] = new PointLight(face.X, face.Y, 1000 * scale, 300 * scale, 1.3f * state.Lit, 1.25f * state.Lit, 1.1f * state.Lit);
        // The room's bulb fills the whole room as the lamps light.
        var bulb = Layout.ToPixels(SceneLayout.Bulb);
        float house = 1.3f * state.HouseLight;
        points[n + 1] = new PointLight(bulb.X, bulb.Y, 1700 * scale, 420 * scale, 1.0f * house, 0.86f * house, 0.62f * house);
        float moon = 1 - 0.3f * tension;
        var spill = Layout.ToPixels(new PointF(250, 520));
        points[n + 2] = new PointLight(spill.X, spill.Y, 460 * scale, 220 * scale, 0.10f * moon, 0.13f * moon, 0.20f * moon);
        // The outside through the window, and the moonlit corridor through the door. Kept a cell inside
        // the openings, so the frames around them stay in the room's light.
        float outside = 1 - 0.12f * tension;
        float flash = 2.2f * state.Flash;
        areas[0] = new AreaLight(Layout.ToPixels(RectangleF.Inflate(SceneLayout.WindowOpening, -6, -6)), 4 * scale, 0.84f * outside + flash, 0.86f * outside + flash, 0.92f * outside + flash * 1.1f);
        // Beside you, he stands in front of the door and hides the corridor's light.
        float corridor = station == Station.Beside ? 0 : 0.32f * outside;
        areas[1] = new AreaLight(Layout.ToPixels(RectangleF.Inflate(SceneLayout.DoorGap, -4, -6)), 4 * scale, corridor * 0.9f, corridor, corridor * 1.2f);
        float ambient = 1 - 0.35f * tension, glare = 0.25f * state.Flash;
        lighting.Build(points, areas, 0.028f * ambient + glare, 0.032f * ambient + glare, 0.048f * ambient + glare * 1.2f, (0.55f + 0.3f * tension) * (1 - 0.5f * state.HouseLight));
    }

    // The captions of the important sounds, at the bottom of the screen over the keyboard, newest last; each
    // fades out in its last half second.
    public void DrawCaptions(IReadOnlyList<Candeia.Audio.Caption> captions, double now)
    {
        if (captions.Count == 0) return;
        var g = graphics;
        g.ResetTransform();
        captionFont ??= new Font("Consolas", Math.Max(12, 17 * Layout.Scale), FontStyle.Bold, GraphicsUnit.Pixel);
        var font = captionFont;
        float y = Layout.ToPixels(new PointF(0, 1062)).Y, centre = Layout.Size.Width / 2f;
        for (int i = captions.Count - 1; i >= 0; i--)
        {
            var caption = captions[i];
            double left = caption.Until - now;
            int alpha = (int)(255 * Math.Clamp(left / 0.5, 0, 1));
            string text = caption.Text(english: false);
            var size = g.MeasureString(text, font);
            y -= size.Height + 6 * Layout.Scale;
            var box = new RectangleF(centre - size.Width / 2 - 10 * Layout.Scale, y - 3 * Layout.Scale, size.Width + 20 * Layout.Scale, size.Height + 6 * Layout.Scale);
            using (var back = new SolidBrush(Color.FromArgb(alpha * 190 / 255, 0, 0, 0))) g.FillRectangle(back, box);
            using var ink = new SolidBrush(Color.FromArgb(alpha, 255, 224, 170));
            g.DrawString(text, font, ink, centre - size.Width / 2, y);
        }
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
        captionFont?.Dispose();
        ledGlow?.Dispose();
    }

    public void Dispose() => Free();
}
