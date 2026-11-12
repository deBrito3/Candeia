using System.Diagnostics;
using Candeia.Panels;

namespace Candeia.Scene;

// Measures how long a key takes to appear in the editor while the scene runs. A background thread posts
// one letter at a time to the editor, at a typing pace, as the keyboard would, and waits until the editor
// reports it inserted and painted before posting the next; a letter not shown within half a second counts
// as lost. After every eight letters the editor puts its text back as it was, without a measured key.
// Posted letters carry a marker in the scan-code field, so a real key press is never counted.
sealed class TypingProbe : IDisposable
{
    const int Marker = 0x7E;
    readonly List<double> samples = [];
    readonly object gate = new();
    Thread? thread;
    volatile bool running;
    CodeEditor? editor;
    IntPtr target;
    long inFlight; // when the letter now on its way was posted; 0 when none
    int lost;

    public static bool IsProbe(IntPtr lParam) => ((lParam.ToInt64() >> 16) & 0xFF) == Marker;

    public void Start(CodeEditor editor)
    {
        this.editor = editor;
        target = editor.Handle;
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "Candeia typing probe" };
        thread.Start();
    }

    void Run()
    {
        var random = new Random(9);
        const string word = "acendeu!";
        int typed = 0;
        while (running)
        {
            Thread.Sleep(random.Next(70, 150));
            if (typed == word.Length)
            {
                typed = 0;
                try { editor!.BeginInvoke(editor.RestoreText); }
                catch (InvalidOperationException) { return; }
                continue;
            }
            long at = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref inFlight, at);
            Native.PostMessage(target, Native.WM_CHAR, (IntPtr)word[typed++], (IntPtr)(1 | (Marker << 16)));
            long deadline = at + Stopwatch.Frequency / 2;
            while (running && Interlocked.Read(ref inFlight) == at && Stopwatch.GetTimestamp() < deadline) Thread.Sleep(1);
            // A letter still on its way when the probe stops is not lost: the measurement just ended.
            if (Interlocked.CompareExchange(ref inFlight, 0, at) == at && running) Interlocked.Increment(ref lost);
        }
    }

    // The letter the editor has just received, or 0 when none is expected.
    public long InFlight => Interlocked.Read(ref inFlight);

    // The editor shows the letter posted at postedAt: record the delay, unless it already timed out.
    public void Shown(long postedAt)
    {
        if (postedAt == 0 || Interlocked.CompareExchange(ref inFlight, 0, postedAt) != postedAt) return;
        double ms = (Stopwatch.GetTimestamp() - postedAt) * 1000.0 / Stopwatch.Frequency;
        lock (gate) samples.Add(ms);
    }

    public (List<double> Samples, int Lost) Take()
    {
        lock (gate)
        {
            var copy = samples.ToList();
            samples.Clear();
            return (copy, Interlocked.Exchange(ref lost, 0));
        }
    }

    public void Dispose()
    {
        running = false;
        thread?.Join(1000);
    }
}
