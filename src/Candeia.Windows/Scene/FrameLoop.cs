using System.Diagnostics;

namespace Candeia.Scene;

// Asks the window for a frame in step with the screen. A background thread waits for each refresh of the
// desktop compositor (about 60 times a second) and posts one frame to the window's thread, never a second
// before the first is done. A target of 30 frames per second skips every other refresh.
sealed class FrameLoop : IDisposable
{
    readonly Control owner;
    readonly Action frame;
    readonly Action tick;
    readonly Stopwatch clock = Stopwatch.StartNew();
    Thread? thread;
    volatile bool stopping;
    int queued;
    double lastFlush;

    public FrameLoop(Control owner, Action frame)
    {
        this.owner = owner;
        this.frame = frame;
        tick = Tick;
    }

    public double TargetFps { get; set; } = 60;

    // The measured time between two refreshes of the screen.
    public double RefreshMilliseconds { get; private set; }

    public void Start()
    {
        if (thread is not null) return;
        thread = new Thread(Run) { IsBackground = true, Name = "Candeia frame clock", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    void Run()
    {
        Native.timeBeginPeriod(1);
        try
        {
            double last = 0;
            while (!stopping)
            {
                double before = clock.Elapsed.TotalMilliseconds;
                bool waited = Native.DwmFlush() == 0 && clock.Elapsed.TotalMilliseconds - before > 1;
                double now = clock.Elapsed.TotalMilliseconds;
                double interval = 1000 / TargetFps;
                if (waited)
                {
                    double gap = now - lastFlush;
                    if (lastFlush > 0 && gap < 100) RefreshMilliseconds = RefreshMilliseconds == 0 ? gap : RefreshMilliseconds * 0.95 + gap * 0.05;
                    lastFlush = now;
                }
                else
                {
                    // The compositor did not wait (nothing to show, or no compositor): pace by the clock.
                    double next = last + interval;
                    if (next > now) Thread.Sleep((int)Math.Max(1, next - now));
                    now = clock.Elapsed.TotalMilliseconds;
                }
                if (now - last < interval - 3) continue;
                last = now;
                if (Interlocked.Exchange(ref queued, 1) == 1) continue;
                try { owner.BeginInvoke(tick); }
                catch (InvalidOperationException) { return; }
            }
        }
        finally { Native.timeEndPeriod(1); }
    }

    void Tick()
    {
        try { if (!stopping) frame(); }
        finally { Volatile.Write(ref queued, 0); }
    }

    public void Dispose()
    {
        stopping = true;
        thread?.Join(500);
    }
}
