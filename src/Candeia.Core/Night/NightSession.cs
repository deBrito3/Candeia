using System.Globalization;
using Candeia.Challenges;
using Candeia.Creature;
using Candeia.Execution;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Night;

// A test's lamp on the system panel: not run yet, lit, or out.
public enum LampState { Unknown, Lit, Out }

public enum OutputKind { Plain, Heading, Pass, Fail, Error, Time, Program, Win, Warning }

// One line of the system's output panel. Line is the code line it points to, when there is one; Fix is
// that line corrected, when the checker knows the fix (the CORRIGIR button).
public sealed record OutputLine(OutputKind Kind, string Text, int? Line = null, string? Fix = null);

// The review of a won night: the winning code, how many times each of its lines ran (index = line), the
// marks, the time left, the candles lost, the attempts, the sweets taken and what the night taught.
public sealed record NightReview(string Code, IReadOnlyList<long> Lines, NightMarks Marks, double SecondsLeft, int CandlesLost, int Attempts, int Sweets, Bilingual Learned);

// What one call to Tick produced: the clock's events and the creature's cues, for the scene and the sound.
public sealed record SessionTick(IReadOnlyList<ClockEvent> Events, IReadOnlyList<CreatureCue> Cues);

// One night as it is played, without a screen: the clock, the creature and the runs. The screen calls
// Execute when the player presses F5 and Tick on every frame; everything else follows from those two.
// Checking, compiling and running happen on a background thread; every change to the night happens in
// Tick, on the caller's thread.
//
// A run goes: compile (the clock keeps running; a compile error only makes him listen) → run in the
// isolated runner (the clock stops) → the lamps light one by one → the clock applies the result.
public sealed class NightSession
{
    public const double LampSeconds = 0.35, SettleSeconds = 0.4;

    readonly CSharpRuntime runtime;
    readonly Func<RunRequest, RunResult> run;
    readonly Difficulty difficulty;
    readonly CheckOptions options;
    readonly List<ClockEvent> queued = [];
    readonly List<OutputLine> output = [];
    LampState[] lamps;
    Task<CompileResult>? compiling;
    Task<(RunResult Result, IReadOnlyList<string> Missing)>? running;
    string source = "";
    RunResult? revealing;
    IReadOnlyList<string> missing = [];
    double revealTime;
    int revealed;

    public NightSession(NightDefinition night, NightRules rules, Difficulty difficulty, CSharpRuntime runtime, Func<RunRequest, RunResult> run, bool english = false)
    {
        Night = night;
        this.runtime = runtime;
        this.run = run;
        this.difficulty = difficulty;
        English = english;
        options = new CheckOptions(AllowLinq: night.Act >= 3);
        Penalties = new PenaltyRules(rules, night.Act, difficulty, siege: night.Type == "siege");
        Clock = new NightClock(Penalties, night.Tests.Count);
        Director = new CreatureDirector(Penalties, night.Events);
        lamps = new LampState[night.Tests.Count];
    }

    public NightDefinition Night { get; }
    public Difficulty Difficulty => difficulty;
    public PenaltyRules Penalties { get; }
    public NightClock Clock { get; private set; }
    public CreatureDirector Director { get; private set; }

    // How many times the night was started over after a capture, plus one.
    public int Attempt { get; private set; } = 1;

    // The whole night, every attempt counted: a capture cost every candle, and a sweet taken before it is
    // still a sweet taken. Distância is the winning attempt's own (how much of its time was left).
    int candlesBefore, sweetsBefore;
    public int CandlesLost => candlesBefore + Clock.CandlesLost;
    public int SweetsAccepted => sweetsBefore + Clock.SweetsAccepted;
    public NightMarks Marks => Clock.Marks with { Unharmed = CandlesLost == 0, NoSweet = SweetsAccepted == 0 };

    public IReadOnlyList<LampState> Lamps => lamps;
    public IReadOnlyList<OutputLine> Output => output;

    // Each grows when its part changed, so the screen only redraws what changed.
    public int OutputVersion { get; private set; }
    public int LampsVersion { get; private set; }

    // How many layers of the night's hints the player opened: the idea, the commands, the skeleton. Each
    // layer is a sweet from him: it costs time and the "No sweet" mark (free in Vigil, none in Nightmare).
    // After a capture, the next layer comes free, as a new page of the notebook. Open layers stay open.
    public int HintsShown { get; private set; }

    // Whether the next layer can be had, and what it costs in seconds (0 when free).
    public bool HintAvailable => Penalties.SweetAvailable && HintsShown < Night.Hints.Count && Clock.State is not (ClockState.Won or ClockState.Captured);
    public double HintCost => Penalties.SweetCost;

    // The hint layers this night had open when the player left it, opened again without cost: they were paid
    // for then, or came free after a capture. Only before the night begins.
    public void RestoreHints(int shown)
    {
        if (Clock.State != ClockState.Intro || Attempt > 1) return;
        HintsShown = Math.Clamp(shown, 0, Night.Hints.Count);
    }

    // Takes a sweet: the next layer opens and the clock pays for it. False when there is nothing to give.
    public bool ShowHint()
    {
        if (!HintAvailable) return false;
        HintsShown++;
        queued.AddRange(Clock.AcceptSweet());
        return true;
    }

    // The review of a won night: the code with how many times each line ran, filled in the background.
    public NightReview? Review { get; private set; }
    Task<IReadOnlyList<long>>? reviewing;
    string won = "";

    // The line the editor marks: the first error of the last check or run, or null.
    public int? ErrorLine { get; private set; }

    // A check or run is under way, or its lamps are still lighting.
    public bool Busy => compiling is not null || running is not null || revealing is not null;

    // How lit the house is, 0 to 1: the share of lamps lit right now.
    public double HouseLight => lamps.Length == 0 ? 0 : lamps.Count(l => l == LampState.Lit) / (double)lamps.Length;

    // What the win tells the player to do next: the next night, or, after the act's last, the menu.
    public Bilingual AfterWin { get; set; } = new("Enter: a próxima noite.", "Enter: the next night.");

    // The language of the lines written from now on (the player may switch it in the middle of a night).
    public bool English { get; set; }

    string T(string pt, string en) => English ? en : pt;

    // The player closed the LEIA-ME or started typing: the night begins.
    public void Start()
    {
        if (Clock.State != ClockState.Intro) return;
        queued.AddRange(Clock.BeginCoding());
    }

    // F5. Returns false when a run cannot start now (one is under way, the night is over, or a breath).
    public bool Execute(string code)
    {
        if (Busy) return false;
        Start();
        if (Clock.State != ClockState.Coding || Clock.Paused) return false;
        source = code;
        ErrorLine = null;
        Say([new(OutputKind.Heading, T("Executando…", "Running…"))]);
        compiling = Task.Run(() => runtime.CompileForRun(code, options));
        return true;
    }

    public void Pause(bool paused)
    {
        if (paused) Clock.Pause();
        else Clock.Resume();
    }

    // After a capture: the night starts over from its introduction, and the code stays in the editor.
    public void Restart()
    {
        if (Clock.State != ClockState.Captured) return;
        Attempt++;
        // A new page of the notebook: the next hint, free.
        bool page = HintsShown < Night.Hints.Count;
        if (page) HintsShown++;
        candlesBefore += Clock.CandlesLost;
        sweetsBefore += Clock.SweetsAccepted;
        Clock = new NightClock(Penalties, Night.Tests.Count);
        Director = new CreatureDirector(Penalties, Night.Events);
        lamps = new LampState[Night.Tests.Count];
        LampsVersion++;
        ErrorLine = null;
        var lines = new List<OutputLine> { new(OutputKind.Heading, T($"Tentativa {Attempt}. O seu código continua aqui.", $"Attempt {Attempt}. Your code is still here.")) };
        if (page) lines.Add(new(OutputKind.Plain, T($"Uma página nova no caderno: a dica {HintsShown} abriu sem custo (Ctrl+3).", $"A new page in the notebook: hint {HintsShown} opened at no cost (Ctrl+3).")));
        Say(lines);
    }

    public SessionTick Tick(double seconds)
    {
        var events = new List<ClockEvent>(queued);
        queued.Clear();
        events.AddRange(Clock.Advance(seconds));
        if (compiling is { IsCompleted: true } compiled) Compiled(compiled.Result, events);
        if (running is { IsCompleted: true } finished) Finished(finished.Result);
        if (revealing is not null) Reveal(seconds, events);
        if (reviewing is { IsCompleted: true } review) Reviewed(review);
        Describe(events);
        var cues = Director.Update(Clock, events);
        return new(events, cues);
    }

    void Compiled(CompileResult result, List<ClockEvent> events)
    {
        compiling = null;
        if (Clock.State != ClockState.Coding)
        {
            // The time ran out while the code was compiling.
            Say([new(OutputKind.Warning, T("O tempo acabou antes de o código rodar.", "Time ran out before the code ran."))]);
            return;
        }
        if (!result.Success)
        {
            events.AddRange(Clock.Listened());
            var lines = new List<OutputLine> { new(OutputKind.Heading, T("O código não compilou. Ele escuta.", "The code did not compile. He listens.")) };
            lines.AddRange(result.Errors.Select(e => new OutputLine(OutputKind.Error, e.Format(English) + (e.Fix is null ? "" : T("  [CORRIGIR: duplo clique]", "  [FIX: double click]")), e.Line, e.Fix?.Text)));
            ErrorLine = result.Errors.Count > 0 ? result.Errors[0].Line : null;
            Say(lines);
            return;
        }
        Clock.BeginRun();
        var request = Night.Request(result.Assembly!, result.Pdb);
        string code = source;
        running = Task.Run(() =>
        {
            var outcome = run(request);
            IReadOnlyList<string> absent = outcome.AllPassed ? runtime.MissingConcepts(code, Night.Concepts, outcome.Marks, options) : [];
            return (outcome, absent);
        });
    }

    void Finished((RunResult Result, IReadOnlyList<string> Missing) finished)
    {
        running = null;
        revealing = finished.Result;
        missing = finished.Missing;
        revealTime = 0;
        revealed = 0;
        for (int i = 0; i < lamps.Length; i++) lamps[i] = LampState.Unknown;
        LampsVersion++;
        Say(Results(finished.Result));
    }

    void Reveal(double seconds, List<ClockEvent> events)
    {
        var result = revealing!;
        revealTime += seconds;
        while (revealed < lamps.Length && revealTime >= LampSeconds * (revealed + 1))
        {
            var test = result.Tests.FirstOrDefault(t => t.Index == revealed);
            lamps[revealed] = test is { Passed: true } ? LampState.Lit : LampState.Out;
            revealed++;
            LampsVersion++;
        }
        if (revealed < lamps.Length || revealTime < LampSeconds * lamps.Length + SettleSeconds) return;
        revealing = null;
        var passed = Enumerable.Range(0, lamps.Length).Select(i => lamps[i] == LampState.Lit).ToList();
        events.AddRange(Clock.ApplyRun(new RunOutcome(passed, PenaltyRules.Crashed(result.Stop), missing)));
        if (Clock.State == ClockState.Won) BeginReview();
    }

    // The winning code runs once more, compiled to count its lines, for the post-night review.
    void BeginReview()
    {
        won = source;
        string code = source;
        reviewing = Task.Run<IReadOnlyList<long>>(() =>
        {
            var compiled = runtime.CompileForRun(code, options, countLines: true);
            if (!compiled.Success) return [];
            return run(Night.Request(compiled.Assembly!, compiled.Pdb)).Lines;
        });
    }

    void Reviewed(Task<IReadOnlyList<long>> task)
    {
        reviewing = null;
        var lines = task.IsCompletedSuccessfully ? task.Result : [];
        Review = new NightReview(won, lines, Marks, Clock.Remaining, CandlesLost, Attempt, SweetsAccepted, Night.Learned);
        ReviewVersion++;
    }

    public int ReviewVersion { get; private set; }

    // The output of a finished run: each test, what the program printed, and why it stopped early.
    List<OutputLine> Results(RunResult result)
    {
        var lines = new List<OutputLine> { new(OutputKind.Heading, T($"{result.Passed} de {lamps.Length} lâmpadas acesas", $"{result.Passed} of {lamps.Length} lamps lit")) };
        ErrorLine = null;
        for (int i = 0; i < Night.Tests.Count; i++)
        {
            var test = Night.Tests[i];
            var outcome = result.Tests.FirstOrDefault(t => t.Index == i);
            string call = $"{test.Method ?? Night.Methods[0].Name}({string.Join(", ", test.Args.Select(a => a.GetRawText()))})";
            if (outcome is null)
            {
                lines.Add(new(OutputKind.Fail, $"○ {i + 1}  {(test.Hidden ? T("teste oculto", "hidden test") : call)}: {T("não chegou a rodar", "did not get to run")}"));
                continue;
            }
            if (outcome.Passed)
            {
                lines.Add(new(OutputKind.Pass, $"● {i + 1}  {(test.Hidden ? T("teste oculto", "hidden test") : $"{call} = {outcome.Actual}")}"));
                continue;
            }
            string hidden = test.Hidden ? T("teste oculto, ", "hidden test, ") : "";
            if (outcome.Error is { } error)
            {
                var (pt, en) = RunExplanations.Error(error);
                string where = error.Line is { } line ? T($"linha {line}: ", $"line {line}: ") : "";
                lines.Add(new(OutputKind.Fail, $"○ {i + 1}  {hidden}{call}: {where}{T(pt, en)}", error.Line));
                ErrorLine ??= error.Line;
            }
            else lines.Add(new(OutputKind.Fail, $"○ {i + 1}  {hidden}{call} = {outcome.Actual ?? "?"}, {T("esperado", "expected")} {outcome.Expected}"));
        }
        if (RunExplanations.Stop(result, Night.Limits.StepsPerTest) is { } stop) lines.Add(new(OutputKind.Error, T(stop.Pt, stop.En)));
        var printed = result.Tests.Where(t => t.Output.Length > 0).ToList();
        if (printed.Count > 0)
        {
            lines.Add(new(OutputKind.Heading, T("O que o programa escreveu", "What the program wrote")));
            foreach (var t in printed)
                foreach (var text in t.Output.TrimEnd('\n', '\r').Split('\n'))
                    lines.Add(new(OutputKind.Program, text.TrimEnd('\r')));
        }
        return lines;
    }

    // What the clock's events mean, in the output panel.
    void Describe(List<ClockEvent> events)
    {
        var lines = new List<OutputLine>();
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case ClockEventKind.Bonus when e.Seconds > 0:
                    lines.Add(new(OutputKind.Time, $"+{Minutes(e.Seconds)}  {T("lâmpada nova: ele recua", "a new lamp: he backs away")}"));
                    break;
                case ClockEventKind.NoProgressCost:
                    lines.Add(new(OutputKind.Time, $"−{Minutes(-e.Seconds)}  {T("nenhuma lâmpada nova: a casa faz barulho", "no new lamp: the house makes noise")}"));
                    break;
                case ClockEventKind.CrashCost:
                    lines.Add(new(OutputKind.Time, $"−{Minutes(-e.Seconds)}  {T("o código travou: a casa faz barulho", "the code froze: the house makes noise")}"));
                    break;
                case ClockEventKind.StreakAdvanceCost:
                    lines.Add(new(OutputKind.Time, $"−{Minutes(-e.Seconds)}  {T("ele aproveitou e avançou mais", "he took the chance and came closer")}"));
                    break;
                case ClockEventKind.LampsOut:
                    lines.Add(new(OutputKind.Warning, e.Count == 1 ? T("Uma lâmpada apagou de novo.", "A lamp went out again.") : T($"{e.Count} lâmpadas apagaram de novo.", $"{e.Count} lamps went out again.")));
                    break;
                case ClockEventKind.ConceptsMissing:
                    string needs = string.Join(T(" e ", " and "), (e.Detail ?? "").Split(',').Select(c => Concepts.Describe(c).For(English)));
                    lines.Add(new(OutputKind.Warning, T($"A casa acendeu, mas o LEIA-ME ainda pede {needs}.", $"The house is lit, but the README still asks for {needs}.")));
                    break;
                case ClockEventKind.Won:
                    var marks = Marks;
                    string Mark(bool got, string pt, string en) => $"{(got ? "●" : "○")} {T(pt, en)}";
                    lines.Add(new(OutputKind.Win, T("NOITE VENCIDA. A casa acendeu, e ele foi embora.", "NIGHT WON. The house is lit, and he went away.")));
                    lines.Add(new(OutputKind.Win, $"{Mark(marks.Unharmed, "Ileso", "Unharmed")}   {Mark(marks.NoSweet, "Sem doce", "No sweet")}   {Mark(marks.Distance, "Distância", "Distance")}"));
                    lines.Add(new(OutputKind.Plain, T(AfterWin.Pt, AfterWin.En)));
                    break;
                case ClockEventKind.NearCapture:
                    lines.Add(new(OutputKind.Warning, e.Count == 1
                        ? T("O tempo acabou. Uma vela apagou: resta uma. Respire.", "Time ran out. A candle went out: one is left. Breathe.")
                        : T($"O tempo acabou. Uma vela apagou: restam {e.Count}. Respire.", $"Time ran out. A candle went out: {e.Count} are left. Breathe.")));
                    break;
                case ClockEventKind.SweetOffered when Penalties.SweetAvailable && HintsShown < Night.Hints.Count:
                    lines.Add(new(OutputKind.Warning, HintCost > 0
                        ? T($"Ele deixou um doce no parapeito: a próxima dica, por −{Minutes(HintCost)} e a marca Sem doce. Ctrl+3 na aba DICAS aceita.", $"He left a sweet on the windowsill: the next hint, for −{Minutes(HintCost)} and the No sweet mark. Ctrl+3 on the HINTS tab takes it.")
                        : T("Ele deixou um doce no parapeito: a próxima dica, de graça na Vigília. Ctrl+3 na aba DICAS aceita.", "He left a sweet on the windowsill: the next hint, free in Vigil. Ctrl+3 on the HINTS tab takes it.")));
                    break;
                case ClockEventKind.SweetCost:
                    lines.Add(new(OutputKind.Time, e.Seconds < 0
                        ? $"−{Minutes(-e.Seconds)}  {T($"um doce aceito: a dica {HintsShown} abriu", $"a sweet taken: hint {HintsShown} opened")}"
                        : T($"Um doce aceito: a dica {HintsShown} abriu.", $"A sweet taken: hint {HintsShown} opened.")));
                    break;
                case ClockEventKind.Captured:
                    lines.Add(new(OutputKind.Error, T("A última vela apagou. Ele te pegou.", "The last candle went out. He took you.")));
                    lines.Add(new(OutputKind.Plain, T("Enter: a noite recomeça, e o seu código fica.", "Enter: the night starts over, and your code stays.")));
                    break;
            }
        }
        if (lines.Count > 0) Add(lines);
    }

    static string Minutes(double seconds)
    {
        int total = (int)Math.Round(seconds);
        return string.Create(CultureInfo.InvariantCulture, $"{total / 60}:{total % 60:00}");
    }

    // Replaces the output with a new check or run.
    void Say(List<OutputLine> lines)
    {
        output.Clear();
        output.AddRange(lines);
        OutputVersion++;
    }

    // A line the screen adds to the output: the campaign's news (a best result, a page of the notebook).
    public void Note(OutputKind kind, string pt, string en) => Add([new OutputLine(kind, T(pt, en))]);

    // Adds to the output of the current run.
    void Add(List<OutputLine> lines)
    {
        output.AddRange(lines);
        OutputVersion++;
    }

}
