using System.Diagnostics;

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
            if (Interlocked.CompareExchange(ref inFlight, 0, at) == at) Interlocked.Increment(ref lost);
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

// The editor of the prototype: a plain rich text box on the monitor glass. The real editor, with colours
// and completion, comes in the first playable night. With a probe attached, it reports when each posted
// letter has been inserted and drawn.
sealed class CodeEditor : RichTextBox
{
    long waiting;
    int baseline = -1, caret;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TypingProbe? Probe { get; private set; }

    // Starts measuring with the caret at position: letters are typed there and removed again.
    public void Attach(TypingProbe probe, int position)
    {
        baseline = TextLength;
        caret = position;
        Select(caret, 0);
        Probe = probe;
        probe.Start(this);
    }

    public void Detach()
    {
        Probe?.Dispose();
        Probe = null;
        RestoreText();
    }

    // Removes what the probe typed since the last time.
    public void RestoreText()
    {
        if (baseline < 0 || TextLength <= baseline) return;
        Select(caret, TextLength - baseline);
        SelectedText = "";
        Select(caret, 0);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_CHAR && Probe is { } probe && TypingProbe.IsProbe(m.LParam))
        {
            long postedAt = probe.InFlight;
            base.WndProc(ref m);
            // The text box usually draws the new letter at once; if it left a repaint pending, the letter
            // only counts as shown once that paint is done.
            if (Native.GetUpdateRect(Handle, IntPtr.Zero, false)) waiting = postedAt;
            else probe.Shown(postedAt);
            return;
        }
        if (m.Msg == Native.WM_PAINT && waiting != 0 && Probe is { } painter)
        {
            base.WndProc(ref m);
            painter.Shown(waiting);
            waiting = 0;
            return;
        }
        base.WndProc(ref m);
    }
}
