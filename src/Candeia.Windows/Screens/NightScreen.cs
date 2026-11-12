using System.Diagnostics;
using Candeia.Audio;
using Candeia.Challenges;
using Candeia.Creature;
using Candeia.Execution;
using Candeia.Languages.CSharp;
using Candeia.Night;
using Candeia.Panels;
using Candeia.Progress;
using Candeia.Scene;

namespace Candeia.Screens;

// The screen of a night: the candle-lit room around the old monitor, and the CANDEIA system on its glass
// (title bar with the presence sensors, the lamps, the editor, the LEIA-ME and the output). The night
// itself is a NightSession; this screen only shows it and passes on the keys:
//   F5 run · Esc pause · Ctrl+1 LEIA-ME · Ctrl+2 SAÍDA · Ctrl+3 DICAS (again: take a sweet, the next hint)
//   Ctrl+4 CADERNO · F1 the notebook page of the word under the caret · Ctrl+5 REVISÃO
//   Enter start, next night, or start over · Esc pause, and there M the CANDEIA menu
//   Ctrl+= and Ctrl+- the editor's font · F11 full screen
// The campaign keeps the player's code (at every run and a few seconds after each edit), the hint layers
// and the wins.
sealed class NightScreen : Form
{
    static readonly Bilingual[] Places =
    [
        new("RUA", "STREET"), new("PORTÃO", "GATE"), new("QUINTAL", "YARD"),
        new("JANELA", "WINDOW"), new("PORTA", "DOOR"), new("AO SEU LADO", "BESIDE YOU"),
    ];

    readonly CSharpRuntime runtime;
    readonly RunnerHost host;
    readonly NightRules rules;
    readonly List<NightDefinition> nights;
    readonly FrameLoop loop;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly SceneTimings timings = new();
    readonly System.Windows.Forms.Timer rebuild = new() { Interval = 150 };
    readonly System.Windows.Forms.Timer keep = new() { Interval = 3000 };
    readonly Campaign campaign;
    Difficulty openedAt;
    bool highContrast;
    double fontScale = 1;
    readonly CodeEditor editor = new();
    readonly CodeGutter gutter;
    readonly CodeCompletion completion;
    SystemStyle style = new(1);
    readonly TitleBar title;
    readonly LampStrip lampStrip;
    readonly InfoPane info;
    SceneRenderer? renderer;
    NightSession session = null!;
    int outputVersion = -1, lampsVersion = -1, hintsShown = -1, reviewVersion = -1;
    bool hintsOffered;
    readonly IReadOnlyList<NotebookPage> notebook;
    double last, arrivedAt = -10, apparitionUntil, flashAt = -10, houseLight;
    Station apparition;
    bool paused;
    readonly Mixer mixer = new();
    readonly SoundBank bank = new();
    readonly SoundDirector sound;
    readonly WaveOutPlayer player;

    // Without a campaign, one kept only in memory: the tests and the benches never touch the real save.
    public NightScreen(NightDefinition first, IEnumerable<NightDefinition> all, bool benchmark = false, Campaign? campaign = null)
    {
        this.campaign = campaign ?? new Campaign(all.ToList(), store: null);
        Text = GameInfo.Name;
        BackColor = Color.Black;
        KeyPreview = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        runtime = new CSharpRuntime();
        host = new RunnerHost(Environment.ProcessPath ?? throw new InvalidOperationException("The game needs its own path to start the runner."));
        rules = NightRules.Load();
        notebook = Notebook.Load().Pages;
        nights = all.OrderBy(n => n.Act).ThenBy(n => n.Order).ToList();
        gutter = new CodeGutter(editor);
        title = new TitleBar(style);
        lampStrip = new LampStrip(style);
        info = new InfoPane(style);
        Controls.AddRange([title, lampStrip, gutter, editor, info]);
        completion = new CodeCompletion(this, editor);
        sound = new SoundDirector(mixer, bank);
        player = new WaveOutPlayer(mixer);
        // The measurements and checks run with the sound mixed but muted.
        if (benchmark) mixer.Master = 0;
        editor.KeyPress += (_, _) =>
        {
            if (session.Clock.State == ClockState.Intro) session.Start();
            if (!editor.ReadOnly) sound.Key();
        };
        info.LineChosen += GoToLine;
        info.FixChosen += ApplyFix;
        info.PageChosen += p => info.ShowNotebook(notebook, this.campaign.Marks, p);
        info.ShowNotebook(notebook, this.campaign.Marks, null);
        // A few seconds after the last edit, the code goes to the save.
        editor.TextChanged += (_, _) => { keep.Stop(); keep.Start(); };
        keep.Tick += (_, _) => { keep.Stop(); KeepCode(); };
        Options.Changed += ApplySettings;
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
        highContrast = CodeEditor.HighContrast = Options.Settings.HighContrast;
        editor.Recolour();
        SetLevels();
        Open(first);
        _ = Task.Run(() =>
        {
            try { runtime.Warm(); host.Warm(); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException) { }
            bank.Prepare();
        });
    }

    public NightSession Session => session;
    public CodeEditor Editor => editor;
    public InfoPane Info => info;
    public TitleBar Title => title;
    public IReadOnlyList<NightDefinition> Nights => nights;
    public FrameStats Stats { get; } = new();
    public double Seconds => clock.Elapsed.TotalSeconds;

    public Campaign Campaign => campaign;
    // The player asked for the CANDEIA menu (M in the pause, or Enter after the act's last night).
    public bool ToMenu { get; private set; }

    // Opens a night with the player's own code and the hint layers it had open.
    internal void Open(NightDefinition night)
    {
        campaign.Opened(night);
        openedAt = Options.Difficulty;
        session = new NightSession(night, rules, openedAt, runtime, host.Run, Options.English);
        session.RestoreHints(campaign.Record(night).HintsShown);
        if (campaign.Next(night) is null)
            session.AfterWin = new("Este é o fim do Ato 1. Enter volta ao menu do CANDEIA.", "This is the end of Act 1. Enter goes back to the CANDEIA menu.");
        editor.ReadOnly = false;
        editor.Load(campaign.CodeFor(night));
        keep.Stop();
        ShowReadme();
        info.ShowOutput([]);
        info.Choose(InfoPane.Tab.Readme);
        outputVersion = lampsVersion = hintsShown = reviewVersion = -1;
        info.ShowReview(null);
        houseLight = 0;
        arrivedAt = -10;
        apparitionUntil = 0;
        paused = false;
        sound.Begin();
        scareAt = -10;
        UpdatePanels();
        editor.Focus();
    }

    void ShowReadme() =>
        info.ShowReadme(Options.Of(session.Night.ReadmeFor(openedAt)) + Options.T("\n\n**Enter** começa a noite. Ler não gasta tempo.", "\n\n**Enter** starts the night. Reading costs no time."));

    // The code goes to the save: at each run, a few seconds after an edit, and when the night is left.
    void KeepCode()
    {
        if (session is not null) campaign.KeepCode(session.Night, editor.Text);
    }

    // The player changed a setting: the font, the colours, the language, the volumes, from now on.
    void ApplySettings()
    {
        if (IsDisposed) return;
        var settings = Options.Settings;
        if (settings.HighContrast != highContrast)
        {
            highContrast = CodeEditor.HighContrast = settings.HighContrast;
            editor.Recolour();
            gutter.Invalidate();
        }
        if (settings.FontScale != fontScale) Rebuild();
        SetLevels();
        if (session.English != settings.English)
        {
            session.English = settings.English;
            ShowReadme();
            hintsShown = reviewVersion = lampsVersion = -1;
            info.ShowNotebook(notebook, campaign.Marks, null);
            info.Invalidate();
            lampStrip.Invalidate();
            UpdatePanels();
        }
    }

    void SetLevels()
    {
        var s = Options.Settings;
        sound.SetLevels(s.HouseVolume, s.SystemVolume, s.CreatureVolume, s.MusicVolume);
    }

    // A won night goes to the campaign, and the output says what it brought: a better result, a page of the
    // notebook opened by the marks.
    void Won()
    {
        var night = session.Night;
        bool wonBefore = campaign.Won(night);
        int marksBefore = campaign.Marks;
        bool best = campaign.RecordWin(night, session.Marks, session.Clock.Remaining, session.Attempt);
        if (wonBefore && best) session.Note(OutputKind.Win, "Melhor resultado desta noite até agora.", "Your best result on this night so far.");
        foreach (var page in notebook.Where(p => p.Secret > marksBefore && p.Secret <= campaign.Marks))
            session.Note(OutputKind.Win, $"Uma página apareceu no fim do caderno: {page.Title.Pt} (Ctrl+4).", $"A page appeared at the end of the notebook: {page.Title.En} (Ctrl+4).");
        info.ShowNotebook(notebook, campaign.Marks, null);
    }

    // Leaves the night for the CANDEIA menu; the code stays in the save.
    void ReturnToMenu()
    {
        KeepCode();
        ToMenu = true;
        Close();
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
        player.Start();
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
        fontScale = Options.Settings.FontScale;
        style = new SystemStyle(layout.Scale, fontScale);
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
        double now = Seconds, dt = Math.Clamp(now - last, 0, 0.25) + skip;
        last = now;
        skip = 0;
        var tick = session.Tick(dt);
        sound.Soft = Options.SoftScares;
        sound.Update(now, tick, session.Director.State, session.Lamps, session.Busy);
        foreach (var e in tick.Events)
        {
            if (e.Kind == ClockEventKind.NearCapture) { scareAt = now; capture = false; }
            if (e.Kind == ClockEventKind.Captured) { scareAt = now; capture = true; }
            if (e.Kind == ClockEventKind.Won) Won();
        }
        foreach (var cue in tick.Cues) React(cue, now);
        UpdatePanels();
        title.Animate(now);
        if (renderer is null || WindowState == FormWindowState.Minimized || renderer.Layout.Size != ClientSize) return;

        var creature = session.Director.State;
        houseLight += (session.HouseLight - houseLight) * Math.Min(1, dt * 3);
        var scare = Scare(now);
        ShowPanels(scare.Panels);
        var state = new SceneState(now, creature.Station, now - arrivedAt, SceneQuality.High,
            Visible: creature.Visible && session.Clock.State != ClockState.Won,
            HouseLight: (float)houseLight,
            CandlesOut: Math.Clamp(session.Clock.CandlesLost, 0, Candles.All.Length),
            Flash: Options.SoftScares ? 0 : Flash(now),
            Apparition: scare.Apparition ?? (now < apparitionUntil ? apparition : null),
            Lit: scare.Lit, Match: scare.Match, Sack: scare.Sack, Dark: scare.Dark,
            Candy: session.Clock.SweetPending && session.HintAvailable,
            Panels: scare.Panels);
        renderer.Render(state, timings);
        if (Options.Settings.Captions) renderer.DrawCaptions(sound.Captions(now), now);
        long start = Stopwatch.GetTimestamp();
        IntPtr hdc = Native.GetDC(Handle);
        try { renderer.Present(hdc); }
        finally { Native.ReleaseDC(Handle, hdc); }
        timings.Present = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Stats.Add(timings, now);
    }

    // The scares, second by second after the clock ran out (see the GDD, Derrota):
    //   near capture: the room darkens and goes silent; he is beside you for a moment; a match is struck and
    //     he is gone, with a candle;
    //   capture: darkness and silence for a second and a half; then he is beside you, lit for the first
    //     and only time, and the sack closes over the screen. The system's panels leave the screen while it
    //     strikes and come back on the black, with the message. With soft scares: no flash, a slow fade, a
    //     dimmer figure, and the panels never leave.
    double scareAt = -10, skip;
    bool capture, panelsShown = true;

    // For the tests and the bench: the next frame moves the night on by this many seconds more, so the
    // screen sees the clock's events as it would in play.
    internal void Skip(double seconds) => skip = seconds;

    (Station? Apparition, float Lit, float Match, float Sack, float Dark, bool Panels) Scare(double now) =>
        scareAt < 0 || now < scareAt ? (null, 0, 0, 0, 0, true) : Scare(now - scareAt, capture, Options.SoftScares);

    // The scare t seconds after it began.
    internal static (Station? Apparition, float Lit, float Match, float Sack, float Dark, bool Panels) Scare(double t, bool capture, bool soft)
    {
        if (!capture)
        {
            if (t > 2.6) return (null, 0, 0, 0, 0, true);
            float dark = (float)(t < 1.4 ? 0.35 * Math.Min(1, t / 0.3) : 0.35 * Math.Max(0, 1 - (t - 1.4) / 0.6));
            bool beside = t is >= 0.5 and < 1.4;
            float match = soft || t < 1.4 ? 0 : (float)Math.Exp(-(t - 1.4) * 3.5);
            return (beside ? Station.Beside : null, beside ? 0.2f : 0, match, 0, dark, true);
        }
        float gloom = (float)Math.Min(0.45, 0.45 * t / 1.5);
        if (t < 1.5) return (null, 0, 0, 0, gloom, true);
        double closing = soft ? 3.0 : 1.4;
        float sack = (float)Math.Clamp((t - 1.75) / closing, 0, 1);
        bool panels = soft || t > 1.75 + closing + 0.4;
        // The strike cuts the darkness away at once; the soft one keeps it and fades.
        return (Station.Beside, soft ? 0.4f : 1f, 0, soft ? sack * 0.9f : sack, soft ? Math.Min(1, gloom + 0.3f * sack) : 0, panels);
    }

    void ShowPanels(bool shown)
    {
        if (shown == panelsShown) return;
        panelsShown = shown;
        foreach (Control panel in new Control[] { title, lampStrip, gutter, editor, info }) panel.Visible = shown;
        if (shown) editor.Focus();
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
        $"{Options.T("PRESENÇA", "PRESENCE")}: {Options.Of(Places[(int)station])} · {Minutes(session.Clock.Remaining)}";

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
            _ when paused => Options.T("PAUSA · Esc continua · M menu", "PAUSED · Esc resumes · M menu"),
            ClockState.Won => Options.T("PRESENÇA: NINGUÉM", "PRESENCE: NOBODY"),
            ClockState.Captured => Options.T("PRESENÇA: AO SEU LADO", "PRESENCE: BESIDE YOU"),
            ClockState.Respite => Presence(session.Director.State.Station) + Options.T(" · RESPIRE", " · BREATHE"),
            _ => Presence(session.Director.State.Station),
        };
        title.Show($"{GameInfo.SystemName} · {Options.T("Noite", "Night")} {night.Order} · {Options.Of(night.Title)}", presence);
        if (session.LampsVersion != lampsVersion)
        {
            lampsVersion = session.LampsVersion;
            lampStrip.Show(session.Lamps, night.Tests.Select(t => t.Hidden).ToList());
        }
        if (session.OutputVersion != outputVersion)
        {
            outputVersion = session.OutputVersion;
            info.ShowOutput([.. session.Output]);
            editor.MarkLine(session.ErrorLine ?? 0);
        }
        bool offered = session.Clock.SweetPending;
        if (session.HintsShown != hintsShown || offered != hintsOffered)
        {
            hintsShown = session.HintsShown;
            hintsOffered = offered;
            campaign.KeepHints(night, hintsShown);
            info.ShowHints(night.Hints, hintsShown, session.HintAvailable, session.HintCost);
        }
        if (session.ReviewVersion != reviewVersion)
        {
            reviewVersion = session.ReviewVersion;
            info.ShowReview(session.Review);
            if (session.Review is not null) info.Choose(InfoPane.Tab.Review);
        }
        bool over = clockState is ClockState.Won or ClockState.Captured;
        editor.ReadOnly = over || paused;
    }

    // F1: the notebook page of a word of the code, or the notebook's index when no page has that word.
    public void OpenNotebook(string word)
    {
        info.ShowNotebook(notebook, campaign.Marks, Notebook.For(notebook, word));
        info.Choose(InfoPane.Tab.Notebook);
    }

    // CORRIGIR: the line the checker knows how to fix gets its fix, as one undoable edit.
    public void ApplyFix(int line, string fixedText)
    {
        if (editor.ReadOnly) return;
        int start = editor.GetFirstCharIndexFromLine(line - 1);
        if (start < 0) return;
        string text = editor.Text;
        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        editor.Select(start, end - start);
        editor.SelectedText = fixedText;
        editor.Select(start + fixedText.Length, 0);
        editor.Focus();
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
        if (!session.Execute(editor.Text)) return;
        KeepCode();
        info.Choose(InfoPane.Tab.Output);
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
            case Keys.Escape:
                paused = !paused;
                session.Pause(paused);
                UpdatePanels();
                return true;
            case Keys.M when paused:
                ReturnToMenu();
                return true;
            case Keys.Control | Keys.Oemplus or Keys.Control | Keys.Add:
                Options.Change(Options.Settings with { FontStep = Options.Settings.FontStep + 1 });
                return true;
            case Keys.Control | Keys.OemMinus or Keys.Control | Keys.Subtract:
                Options.Change(Options.Settings with { FontStep = Options.Settings.FontStep - 1 });
                return true;
            case Keys.Control | Keys.D1:
                info.Choose(InfoPane.Tab.Readme);
                return true;
            case Keys.Control | Keys.D2:
                info.Choose(InfoPane.Tab.Output);
                return true;
            case Keys.Control | Keys.D3:
                // The first press shows the tab and the price; pressing again, there, takes the sweet.
                if (info.Current != InfoPane.Tab.Hints) info.Choose(InfoPane.Tab.Hints);
                else session.ShowHint();
                UpdatePanels();
                return true;
            case Keys.Control | Keys.D4:
                info.ShowNotebook(notebook, campaign.Marks, null);
                info.Choose(InfoPane.Tab.Notebook);
                return true;
            case Keys.Control | Keys.D5:
                info.Choose(InfoPane.Tab.Review);
                return true;
            case Keys.F1:
                OpenNotebook(Notebook.WordAt(editor.Text, editor.SelectionStart));
                return true;
            case Keys.Enter when state == ClockState.Intro && !paused:
                session.Start();
                editor.Focus();
                return true;
            case Keys.Enter when state == ClockState.Won && !paused:
                NextNight();
                return true;
            case Keys.Enter when state == ClockState.Captured && !paused:
                session.Restart();
                scareAt = -10;
                ShowPanels(true);
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

    // After a win: the next night, or the menu after the act's last.
    void NextNight()
    {
        if (campaign.Next(session.Night) is { } next && campaign.Unlocked(next)) Open(next);
        else ReturnToMenu();
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
        KeepCode();
        campaign.Flush();
        loop.Dispose();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Options.Changed -= ApplySettings;
            loop.Dispose();
            rebuild.Dispose();
            keep.Dispose();
            completion.Dispose();
            renderer?.Dispose();
            host.Dispose();
            player.Dispose();
            style.Dispose();
        }
        base.Dispose(disposing);
    }
}
