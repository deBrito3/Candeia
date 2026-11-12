namespace Candeia.Scene;

// Percentiles of a set of measurements, in milliseconds.
readonly record struct Spread(int Count, double Median, double P95, double Max)
{
    public static Spread Of(List<double> values)
    {
        if (values.Count == 0) return new(0, 0, 0, 0);
        var sorted = values.Order().ToArray();
        return new(sorted.Length, At(sorted, 0.5), At(sorted, 0.95), sorted[^1]);
    }

    static double At(double[] sorted, double p) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(p * sorted.Length) - 1)];
}

// Average cost of each layer, in milliseconds.
readonly record struct LayerCosts(double Copy, double Figures, double LightMap, double Light, double Flames, double Present);

readonly record struct FrameSummary(int Frames, double Fps, Spread Work, Spread Interval, LayerCosts Layers);

// Collects what each frame cost to draw, and the time between frames. The scene's thread adds; the
// measurements read; a lock keeps them apart.
sealed class FrameStats
{
    readonly object gate = new();
    readonly List<double> work = [], interval = [];
    double copy, figures, lightMap, light, flames, present;
    double lastAt = -1;

    public void Add(SceneTimings t, double at)
    {
        lock (gate) AddLocked(t, at);
    }

    void AddLocked(SceneTimings t, double at)
    {
        work.Add(t.Render + t.Present);
        if (lastAt >= 0) interval.Add((at - lastAt) * 1000);
        lastAt = at;
        copy += t.Copy;
        figures += t.Figures;
        lightMap += t.LightMap;
        light += t.Light;
        flames += t.Flames;
        present += t.Present;
    }

    public void Reset()
    {
        lock (gate) ResetLocked();
    }

    void ResetLocked()
    {
        work.Clear();
        interval.Clear();
        copy = figures = lightMap = light = flames = present = 0;
        lastAt = -1;
    }

    public FrameSummary Summarize(double seconds)
    {
        lock (gate) return SummarizeLocked(seconds);
    }

    FrameSummary SummarizeLocked(double seconds)
    {
        int n = Math.Max(1, work.Count);
        return new(work.Count, work.Count / seconds, Spread.Of(work), Spread.Of(interval),
            new(copy / n, figures / n, lightMap / n, light / n, flames / n, present / n));
    }
}
