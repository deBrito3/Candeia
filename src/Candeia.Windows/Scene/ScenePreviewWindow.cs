using System.Diagnostics;
using Candeia.Night;
using Candeia.Panels;

namespace Candeia.Scene;

// The rendering prototype: the candle-lit room with the editor on the monitor, the Papa-Figo at any
// station, and the measurements on screen. Keys:
//   F2 / Shift+F2  next / previous station      F3  measurements on screen
//   F4  quality (high / reduced)                F5  he approaches by himself, one station every 6 s
//   F6  target of 60 or 30 frames per second    F11 full screen
sealed class ScenePreviewWindow : Form
{
    static readonly string[] StationNames = ["rua", "portão", "quintal", "janela", "porta", "ao seu lado"];

    readonly CodeEditor editor;
    readonly FrameLoop loop;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly SceneTimings timings = new();
    readonly System.Windows.Forms.Timer rebuild = new() { Interval = 150 };
    readonly Process process = Process.GetCurrentProcess();
    SceneRenderer? renderer;
    Font? editorFont;
    double arrivedAt = -10, nextApproachAt;
    bool approaching, showStats;
    FormWindowState windowedState = FormWindowState.Maximized;

    // The measurements panel, refreshed once a second.
    double panelAt, panelCpu;
    TimeSpan panelCpuTime;
    readonly List<double> panelWork = [];
    string panel = "";

    public ScenePreviewWindow(bool benchmark)
    {
        Text = $"{GameInfo.Name} · protótipo de desenho";
        BackColor = Color.Black;
        KeyPreview = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        editor = new CodeEditor { ScrollBars = RichTextBoxScrollBars.None };
        editor.Load(StarterCode());
        Controls.Add(editor);

        if (benchmark)
        {
            // Exactly 1920×1080, borderless, on the main screen, above other windows while it measures.
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            Bounds = new Rectangle(screen.X, screen.Y, 1920, 1080);
            TopMost = true;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1600, 900);
            WindowState = FormWindowState.Maximized;
            showStats = true;
        }
        loop = new FrameLoop(this, Frame);
        rebuild.Tick += (_, _) => { rebuild.Stop(); Rebuild(); };
    }

    public CodeEditor Editor => editor;
    public FrameStats Stats { get; } = new();
    public Station Station { get; private set; } = Station.Street;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public SceneQuality Quality { get; set; } = SceneQuality.High;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double TargetFps { get => loop.TargetFps; set => loop.TargetFps = value; }
    public double RefreshMilliseconds => loop.RefreshMilliseconds;
    public double RoomMilliseconds => renderer?.RoomMilliseconds ?? 0;
    public double Seconds => clock.Elapsed.TotalSeconds;

    public void SetStation(Station station)
    {
        if (station == Station) return;
        Station = station;
        arrivedAt = Seconds;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x02000000; // WS_CLIPCHILDREN: the frame never paints over the editor
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MainWindow.DarkTitleBar(Handle, Palette.Night);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Rebuild();
        loop.Start();
        editor.Focus();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (renderer is null) return;
        rebuild.Stop();
        rebuild.Start();
    }

    void Rebuild()
    {
        if (WindowState == FormWindowState.Minimized || ClientSize.Width < 64 || ClientSize.Height < 64) return;
        if (renderer is null) renderer = new SceneRenderer(ClientSize);
        else renderer.Resize(ClientSize);
        editor.Bounds = renderer.Layout.PanelBounds();
        var font = new Font("Consolas", 21 * renderer.Layout.Scale, GraphicsUnit.Pixel);
        editor.Font = font;
        editorFont?.Dispose();
        editorFont = font;
        Invalidate();
    }

    void Frame()
    {
        if (renderer is null || WindowState == FormWindowState.Minimized || renderer.Layout.Size != ClientSize) return;
        double now = Seconds;
        if (approaching && now >= nextApproachAt)
        {
            SetStation(Station == Station.Beside ? Station.Street : Station + 1);
            nextApproachAt = now + 6;
        }
        renderer.Render(new SceneState(now, Station, now - arrivedAt, Quality), timings);
        if (showStats) renderer.DrawOverlay(Panel(now));
        long start = Stopwatch.GetTimestamp();
        IntPtr hdc = Native.GetDC(Handle);
        try { renderer.Present(hdc); }
        finally { Native.ReleaseDC(Handle, hdc); }
        timings.Present = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Stats.Add(timings, now);
        panelWork.Add(timings.Render + timings.Present);
    }

    string Panel(double now)
    {
        if (now - panelAt >= 1)
        {
            process.Refresh();
            var cpu = process.TotalProcessorTime;
            if (panelAt > 0) panelCpu = (cpu - panelCpuTime).TotalMilliseconds / ((now - panelAt) * 1000) * 100;
            var work = Spread.Of(panelWork);
            double fps = panelWork.Count / Math.Max(0.001, now - panelAt);
            panel = $"{fps:0} quadros/s · quadro {work.Median:0.0} ms (p95 {work.P95:0.0}) · processador {panelCpu:0}% de um núcleo · {StationNames[(int)Station]} · " +
                $"{(Quality == SceneQuality.High ? "qualidade alta" : "qualidade reduzida")} · meta {TargetFps:0}\n" +
                "F2 parada · F3 painel · F4 qualidade · F5 aproximação · F6 meta · F11 tela cheia";
            panelAt = now;
            panelCpuTime = cpu;
            panelWork.Clear();
        }
        return panel;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (renderer is null || renderer.Layout.Size != ClientSize)
        {
            e.Graphics.Clear(Color.Black);
            return;
        }
        IntPtr hdc = e.Graphics.GetHdc();
        try { renderer.Present(hdc); }
        finally { e.Graphics.ReleaseHdc(hdc); }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F2: SetStation(Station == Station.Beside ? Station.Street : Station + 1); return true;
            case Keys.Shift | Keys.F2: SetStation(Station == Station.Street ? Station.Beside : Station - 1); return true;
            case Keys.F3: showStats = !showStats; return true;
            case Keys.F4: Quality = Quality == SceneQuality.High ? SceneQuality.Reduced : SceneQuality.High; return true;
            case Keys.F5: approaching = !approaching; nextApproachAt = Seconds + 6; return true;
            case Keys.F6: TargetFps = TargetFps > 45 ? 30 : 60; return true;
            case Keys.F11: ToggleFullScreen(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = windowedState;
        }
        else
        {
            windowedState = WindowState;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        loop.Dispose();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            loop.Dispose();
            rebuild.Dispose();
            renderer?.Dispose();
            editorFont?.Dispose();
            process.Dispose();
        }
        base.Dispose(disposing);
    }

    // The code of the first night, as the player finds it, so the editor shows real text.
    static string StarterCode()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "content", "nights", "act1", "01-room-light", "starter.cs");
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "// CANDEIA\n";
    }
}
