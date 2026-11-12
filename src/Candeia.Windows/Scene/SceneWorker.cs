using System.Diagnostics;
using Candeia.Audio;

namespace Candeia.Scene;

// Draws the scene on its own thread, so the window's thread (the editor, the keys, the night itself) never
// waits for it. At each frame the window's thread hands over what the scene shows; this thread draws it
// and puts it on the screen itself, through the window's device context. Only the newest frame handed over
// is drawn: one that arrives while the last is still being drawn replaces any that waited. Resizing and the
// window's own painting take the same lock as drawing, so they never meet a half-drawn frame.
sealed class SceneWorker : IDisposable
{
    readonly SceneRenderer renderer;
    readonly FrameStats stats;
    readonly SceneTimings timings = new();
    readonly Thread thread;
    readonly AutoResetEvent wake = new(false);
    readonly object drawing = new(), waiting = new();
    Job? job;
    volatile bool stopping, failNext;
    int disposed;
    IntPtr window;

    sealed record Job(SceneState State, IReadOnlyList<Caption> Captions, Size Size);

    // Window may be zero (nothing is put on a screen: the tests).
    public SceneWorker(Size size, IntPtr window, FrameStats stats)
    {
        renderer = new SceneRenderer(size);
        this.window = window;
        this.stats = stats;
        thread = new Thread(Run) { IsBackground = true, Name = "Candeia scene" };
        thread.Start();
    }

    // The window's handle changes when it is created again (full screen switches the border).
    public IntPtr Window
    {
        get => Volatile.Read(ref window);
        set => Volatile.Write(ref window, value);
    }

    // What went wrong on the scene's thread, if anything: the window's thread reports it.
    public Exception? Failure { get; private set; }

    // How many frames this thread has drawn (the tests wait on it).
    public int Drawn => Volatile.Read(ref drawn);
    int drawn;

    public SceneLayout Layout
    {
        get { lock (drawing) return renderer.Layout; }
    }

    public void Resize(Size size)
    {
        lock (drawing) renderer.Resize(size);
    }

    // The frame to draw next, for a window of this size; a frame for another size is dropped.
    public void Submit(in SceneState state, IReadOnlyList<Caption> captions, Size size)
    {
        if (Volatile.Read(ref disposed) == 1) return;
        lock (waiting) job = new Job(state, captions, size);
        wake.Set();
    }

    // The window paints (a WM_PAINT, a capture): the last frame drawn, when it fits the window.
    public bool Present(IntPtr hdc, Size size)
    {
        lock (drawing)
        {
            if (renderer.Layout.Size != size) return false;
            renderer.Present(hdc);
            return true;
        }
    }

    // For the tests: the next frame throws, as a fault in the drawing would.
    internal void FailNextFrame() => failNext = true;

    void Run()
    {
        try
        {
            while (!stopping)
            {
                wake.WaitOne(250);
                if (stopping) break;
                Job? next;
                lock (waiting) { next = job; job = null; }
                if (next is null) continue;
                lock (drawing) Draw(next);
                Interlocked.Increment(ref drawn);
            }
        }
        catch (Exception ex) when (ex is not ThreadInterruptedException)
        {
            Failure = ex;
        }
    }

    void Draw(Job next)
    {
        if (failNext)
        {
            failNext = false;
            throw new InvalidOperationException("A fault in the scene's drawing, caused on purpose by the tests.");
        }
        if (renderer.Layout.Size != next.Size) return;
        renderer.Render(next.State, timings);
        renderer.DrawCaptions(next.Captions, next.State.Seconds);
        long start = Stopwatch.GetTimestamp();
        IntPtr handle = Window;
        if (handle != IntPtr.Zero)
        {
            IntPtr hdc = Native.GetDC(handle);
            if (hdc != IntPtr.Zero)
            {
                try { renderer.Present(hdc); }
                finally { Native.ReleaseDC(handle, hdc); }
            }
        }
        timings.Present = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        stats.Add(timings, next.State.Seconds);
    }

    // Safe to call more than once (a form is disposed when it closes, and again by its owner).
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1) return;
        stopping = true;
        wake.Set();
        thread.Join(2000);
        lock (drawing) renderer.Dispose();
        wake.Dispose();
    }
}
