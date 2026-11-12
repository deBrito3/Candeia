using System.Diagnostics;
using Candeia.Challenges;
using Candeia.Creature;
using Candeia.Execution;
using Candeia.Languages.CSharp;
using Candeia.Night;
using Candeia.Panels;
using Candeia.Scene;

namespace Candeia.Screens;

// The screen of a night: the candle-lit room around the old monitor, and the CANDEIA system on its glass
// (title bar with the presence sensors, the lamps, the editor, the LEIA-ME and the output). The night
// itself is a NightSession; this screen only shows it and passes on the keys:
//   F5 run · Esc pause · Ctrl+1 LEIA-ME · Ctrl+2 SAÍDA · Ctrl+3 DICAS (again: the next hint)
//   Enter start, next night, or start over · F11 full screen
sealed class NightScreen : Form
{
    static readonly string[] Places = ["RUA", "PORTÃO", "QUINTAL", "JANELA", "PORTA", "AO SEU LADO"];

    readonly CSharpRuntime runtime;
    readonly RunnerHost host;
    readonly NightRules rules;
    readonly List<NightDefinition> nights;
    readonly FrameLoop loop;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly SceneTimings timings = new();
    readonly System.Windows.Forms.Timer rebuild = new() { Interval = 150 };
    readonly CodeEditor editor = new();
    readonly CodeGutter gutter;
    readonly CodeCompletion completion;
    SystemStyle style = new(1);
    readonly TitleBar title;
    readonly LampStrip lampStrip;
    readonly InfoPane info;
    SceneRenderer? renderer;
    NightSession session = null!;
    int outputVersion = -1, lampsVersion = -1, hintsShown = -1;
    double last, arrivedAt = -10, apparitionUntil, flashAt = -10, houseLight;
    Station apparition;
    bool paused;

    public NightScreen(NightDefinition first, IEnumerable<NightDefinition> all, bool benchmark = false)
    {
        Text = GameInfo.Name;
        BackColor = Color.Black;
        KeyPreview = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        runtime = new CSharpRuntime();
        host = new RunnerHost(Environment.ProcessPath ?? throw new InvalidOperationException("The game needs its own path to start the runner."));
        rules = NightRules.Load();
        nights = all.OrderBy(n => n.Act).ThenBy(n => n.Order).ToList();
        gutter = new CodeGutter(editor);
        title = new TitleBar(style);
        lampStrip = new LampStrip(style);
        info = new InfoPane(style);
        Controls.AddRange([title, lampStrip, gutter, editor, info]);
        completion = new CodeCompletion(this, editor);
        editor.KeyPress += (_, _) => { if (session.Clock.State == ClockState.Intro) session.Start(); };
        info.LineChosen += GoToLine;
        if (benchmark)
        {
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
        }
        loop = new FrameLoop(this, Frame);
        rebuild.Tick += (_, _) => { rebuild.Stop(); Rebuild(); };
        Open(first);
        _ = Task.Run(() =>
        {
            try { runtime.Warm(); host.Warm(); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException) { }
        });
    }

    public NightSession Session => session;
    public CodeEditor Editor => editor;
    public InfoPane Info => info;
    public FrameStats Stats { get; } = new();
    public double Seconds => clock.Elapsed.TotalSeconds;

    void Open(NightDefinition night)
    {
        session = new NightSession(night, rules, Difficulty.Night, runtime, host.Run);
        editor.ReadOnly = false;
        editor.Load(night.Starter);
        info.ShowReadme(night.Readme.Pt + "\n\n**Enter** começa a noite. Ler não gasta tempo.");
        info.ShowOutput([]);
        info.Choose(InfoPane.Tab.Readme);
        outputVersion = lampsVersion = hintsShown = -1;
        houseLight = 0;
        arrivedAt = -10;
        apparitionUntil = 0;
        paused = false;
        UpdatePanels();
        editor.Focus();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x02000000; // WS_CLIPCHILDREN: the frame never paints over the panels
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
        last = Seconds;
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

    // Lays the panels out on the monitor glass, in the scene's scale.
    void Rebuild()
    {
        if (WindowState == FormWindowState.Minimized || ClientSize.Width < 64 || ClientSize.Height < 64) return;
        if (renderer is null) renderer = new SceneRenderer(ClientSize);
        else renderer.Resize(ClientSize);
        var layout = renderer.Layout;
        var old = style;
        style = new SystemStyle(layout.Scale);
        title.Restyle(style);
        lampStrip.Restyle(style);
        editor.Font = style.Code;
        info.Restyle(style);
        old.Dispose();

        var area = layout.PanelBounds();
        int titleHeight = style.Px(30), lampsHeight = style.Px(34);
        int rest = area.Height - titleHeight - lampsHeight;
        int editorHeight = (int)(rest * 0.62);
        int y = area.Top;
        title.Bounds = new Rectangle(area.Left, y, area.Width, titleHeight);
        y += titleHeight;
        lampStrip.Bounds = new Rectangle(area.Left, y, area.Width, lampsHeight);
        y += lampsHeight;
        int gutterWidth = gutter.PreferredWidth();
        gutter.Bounds = new Rectangle(area.Left, y, gutterWidth, editorHeight);
        int gap = style.Px(8);
        editor.Bounds = new Rectangle(area.Left + gutterWidth + gap, y, area.Width - gutterWidth - gap, editorHeight);
        y += editorHeight;
        info.Bounds = new Rectangle(area.Left, y, area.Width, area.Bottom - y);
        Invalidate();
    }

    void Frame()
    {
        double now = Seconds, dt = Math.Clamp(now - last, 0, 0.25);
        last = now;
        var tick = session.Tick(dt);
        foreach (var cue in tick.Cues) React(cue, now);
        UpdatePanels();
        title.Animate(now);
        if (renderer is null || WindowState == FormWindowState.Minimized || renderer.Layout.Size != ClientSize) return;

        var creature = session.Director.State;
        houseLight += (session.HouseLight - houseLight) * Math.Min(1, dt * 3);
        var state = new SceneState(now, creature.Station, now - arrivedAt, SceneQuality.High,
            Visible: creature.Visible && session.Clock.State != ClockState.Won,
            HouseLight: (float)houseLight,
            CandlesOut: Math.Clamp(session.Clock.CandlesLost, 0, Candles.All.Length),
            Flash: Flash(now),
            Apparition: now < apparitionUntil ? apparition : null);
        renderer.Render(state, timings);
        long start = Stopwatch.GetTimestamp();
        IntPtr hdc = Native.GetDC(Handle);
        try { renderer.Present(hdc); }
        finally { Native.ReleaseDC(Handle, hdc); }
        timings.Present = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Stats.Add(timings, now);
    }

    // Two short lightning flashes, under the three-a-second limit for flashing light.
    float Flash(double now)
    {
        double t = now - flashAt;
        return t is >= 0 and < 0.07 || t is >= 0.18 and < 0.27 ? 1 : 0;
    }

    void React(CreatureCue cue, double now)
    {
        switch (cue.Kind)
        {
            case CueKind.Approaching:
                title.Approaching(Presence(cue.Station), now, session.Penalties.Rules.TelegraphSeconds);
                break;
            case CueKind.Arrived:
                arrivedAt = now;
                title.Arrived(now);
                break;
            case CueKind.Retreated:
                title.Arrived(now);
                break;
            case CueKind.Scripted when cue.Effect == "silhouette" && cue.Where is { } where:
                apparition = where;
                apparitionUntil = now + cue.Seconds;
                flashAt = now;
                break;
        }
    }

    string Presence(Station station) =>
        $"PRESENÇA: {Places[(int)station]} · {Minutes(session.Clock.Remaining)}";

    static string Minutes(double seconds)
    {
        int total = (int)Math.Ceiling(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    void UpdatePanels()
    {
        var night = session.Night;
        var clockState = session.Clock.State;
        string presence = clockState switch
        {
            ClockState.Won => "PRESENÇA: NINGUÉM",
            ClockState.Captured => "PRESENÇA: AO SEU LADO",
            _ when paused => "PAUSA · Esc continua",
            ClockState.Respite => Presence(session.Director.State.Station) + " · RESPIRE",
            _ => Presence(session.Director.State.Station),
        };
        title.Show($"{GameInfo.SystemName} · Noite {night.Order} · {night.Title.Pt}", presence);
        if (session.LampsVersion != lampsVersion)
        {
            lampsVersion = session.LampsVersion;
            lampStrip.Show(session.Lamps, night.Tests.Select(t => t.Hidden).ToList());
        }
        if (session.OutputVersion != outputVersion)
        {
            outputVersion = session.OutputVersion;
            info.ShowOutput(session.Output);
            editor.MarkLine(session.ErrorLine ?? 0);
        }
        if (session.HintsShown != hintsShown)
        {
            hintsShown = session.HintsShown;
            info.ShowHints(night.Hints, hintsShown);
        }
        bool over = clockState is ClockState.Won or ClockState.Captured;
        editor.ReadOnly = over || paused;
    }

    void GoToLine(int line)
    {
        int at = editor.GetFirstCharIndexFromLine(line - 1);
        if (at < 0) return;
        editor.Select(at, 0);
        editor.ScrollToCaret();
        editor.Focus();
    }

    public void Run()
    {
        if (session.Execute(editor.Text)) info.Choose(InfoPane.Tab.Output);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (editor.Focused && completion.Visible && keyData is Keys.Up or Keys.Down or Keys.Tab or Keys.Enter or Keys.Escape && completion.HandleKey(keyData)) return true;
        return Command(keyData) || base.ProcessCmdKey(ref msg, keyData);
    }

    // The night's keys. True when the key was one of them.
    public bool Command(Keys keyData)
    {
        var state = session.Clock.State;
        switch (keyData)
        {
            case Keys.F5:
                Run();
                return true;
            case Keys.Escape when state is ClockState.Coding or ClockState.Respite or ClockState.Results:
                paused = !paused;
                session.Pause(paused);
                UpdatePanels();
                return true;
            case Keys.Control | Keys.D1:
                info.Choose(InfoPane.Tab.Readme);
                return true;
            case Keys.Control | Keys.D2:
                info.Choose(InfoPane.Tab.Output);
                return true;
            case Keys.Control | Keys.D3:
                // The first press shows the tab (and the first hint, if none is open); then each press opens
                // the next layer.
                if (info.Current != InfoPane.Tab.Hints) { info.Choose(InfoPane.Tab.Hints); if (session.HintsShown == 0) session.ShowHint(); }
                else session.ShowHint();
                UpdatePanels();
                return true;
            case Keys.Enter when state == ClockState.Intro:
                session.Start();
                editor.Focus();
                return true;
            case Keys.Enter when state == ClockState.Won:
                NextNight();
                return true;
            case Keys.Enter when state == ClockState.Captured:
                session.Restart();
                lampsVersion = -1;
                info.Choose(InfoPane.Tab.Readme);
                editor.Focus();
                return true;
            case Keys.F11:
                ToggleFullScreen();
                return true;
        }
        return false;
    }

    public bool Paused => paused;

    void NextNight()
    {
        int index = nights.FindIndex(n => n.Id == session.Night.Id);
        if (index >= 0 && index + 1 < nights.Count) Open(nights[index + 1]);
        else info.ShowOutput([.. session.Output, new OutputLine(OutputKind.Plain, "Esta versão de teste termina aqui. Obrigado por jogar.")]);
    }

    FormWindowState windowedState = FormWindowState.Maximized;

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
            completion.Dispose();
            renderer?.Dispose();
            host.Dispose();
            style.Dispose();
        }
        base.Dispose(disposing);
    }
}
