namespace Candeia.Scene;

// The rain outside the window: falling streaks and a few drops sliding down the glass. Drawn in design
// coordinates, inside the glass clip the renderer sets.
static class Rain
{
    public static void DrawStreaks(Graphics g, double seconds, int count)
    {
        var area = SceneLayout.WindowOpening;
        // Thin, fast streaks: smoothing their edges costs more than it shows.
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        using var pen = new Pen(Color.FromArgb(70, 170, 186, 212), 1.3f);
        for (int i = 0; i < count; i++)
        {
            float u = Hash(i * 2 + 1), v = Hash(i * 2 + 2);
            float speed = 900 + 500 * Hash(i * 7 + 3);
            float fall = (float)((v * area.Height + seconds * speed) % (area.Height + 40)) - 20;
            float x = area.Left + (u * area.Width + fall * 0.22f) % area.Width;
            float y = area.Top + fall;
            g.DrawLine(pen, x, y, x - 5, y - 24);
        }
        g.SmoothingMode = smoothing;
    }

    public static void DrawDrops(Graphics g, double seconds, int count)
    {
        var area = SceneLayout.WindowOpening;
        using var drop = new SolidBrush(Color.FromArgb(90, 200, 210, 230));
        using var trail = new Pen(Color.FromArgb(30, 200, 210, 230), 2);
        for (int i = 0; i < count; i++)
        {
            float x = area.Left + Hash(100 + i) * area.Width;
            float slide = (float)((Hash(200 + i) * area.Height + seconds * (12 + 30 * Hash(300 + i))) % area.Height);
            float y = area.Top + slide;
            g.DrawLine(trail, x + 2, Math.Max(area.Top, y - 26), x + 2, y);
            g.FillEllipse(drop, x, y, 4, 5.5f);
        }
    }

    // A repeatable pseudo-random number in 0..1 for an index, so the rain needs no state.
    static float Hash(int n)
    {
        uint h = (uint)n * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xFFFFFF) / 16777216f;
    }
}
