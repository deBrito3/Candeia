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

// One line of the system's output panel. Line is the code line it points to, when there is one.
public sealed record OutputLine(OutputKind Kind, string Text, int? Line = null);

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
    readonly bool english;
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
        this.english = english;
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

    public IReadOnlyList<LampState> Lamps => lamps;
    public IReadOnlyList<OutputLine> Output => output;

    // Each grows when its part changed, so the screen only redraws what changed.
    public int OutputVersion { get; private set; }
    public int LampsVersion { get; private set; }

    // How many layers of the night's hints the player opened: the idea, the commands, the skeleton. They
    // stay open after a capture. For now they are free; the sweet's price comes with the notebook.
    public int HintsShown { get; private set; }

    // Opens the next layer. False when every layer is already open.
    public bool ShowHint()
    {
        if (HintsShown >= Night.Hints.Count) return false;
        HintsShown++;
        return true;
    }

    // The line the editor marks: the first error of the last check or run, or null.
    public int? ErrorLine { get; private set; }

    // A check or run is under way, or its lamps are still lighting.
    public bool Busy => compiling is not null || running is not null || revealing is not null;

    // How lit the house is, 0 to 1: the share of lamps lit right now.
    public double HouseLight => lamps.Length == 0 ? 0 : lamps.Count(l => l == LampState.Lit) / (double)lamps.Length;

    string T(string pt, string en) => english ? en : pt;

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
        Clock = new NightClock(Penalties, Night.Tests.Count);
        Director = new CreatureDirector(Penalties, Night.Events);
        lamps = new LampState[Night.Tests.Count];
        LampsVersion++;
        ErrorLine = null;
        Say([new(OutputKind.Heading, T($"Tentativa {Attempt}. O seu código continua aqui.", $"Attempt {Attempt}. Your code is still here."))]);
    }

    public SessionTick Tick(double seconds)
    {
        var events = new List<ClockEvent>(queued);
        queued.Clear();
        events.AddRange(Clock.Advance(seconds));
        if (compiling is { IsCompleted: true } compiled) Compiled(compiled.Result, events);
        if (running is { IsCompleted: true } finished) Finished(finished.Result);
        if (revealing is not null) Reveal(seconds, events);
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
            lines.AddRange(result.Errors.Select(e => new OutputLine(OutputKind.Error, e.Format(english), e.Line)));
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
    }

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
                    string needs = string.Join(T(" e ", " and "), (e.Detail ?? "").Split(',').Select(c => Concepts.Describe(c).For(english)));
                    lines.Add(new(OutputKind.Warning, T($"A casa acendeu, mas o LEIA-ME ainda pede {needs}.", $"The house is lit, but the README still asks for {needs}.")));
                    break;
                case ClockEventKind.Won:
                    var marks = Clock.Marks;
                    string Mark(bool got, string pt, string en) => $"{(got ? "●" : "○")} {T(pt, en)}";
                    lines.Add(new(OutputKind.Win, T("NOITE VENCIDA. A casa acendeu, e ele foi embora.", "NIGHT WON. The house is lit, and he went away.")));
                    lines.Add(new(OutputKind.Win, $"{Mark(marks.Unharmed, "Ileso", "Unharmed")}   {Mark(marks.NoSweet, "Sem doce", "No sweet")}   {Mark(marks.Distance, "Distância", "Distance")}"));
                    lines.Add(new(OutputKind.Plain, T("Enter: a próxima noite.", "Enter: the next night.")));
                    break;
                case ClockEventKind.NearCapture:
                    lines.Add(new(OutputKind.Warning, e.Count == 1
                        ? T("O tempo acabou. Uma vela apagou: resta uma. Respire.", "Time ran out. A candle went out: one is left. Breathe.")
                        : T($"O tempo acabou. Uma vela apagou: restam {e.Count}. Respire.", $"Time ran out. A candle went out: {e.Count} are left. Breathe.")));
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

    // Adds to the output of the current run.
    void Add(List<OutputLine> lines)
    {
        output.AddRange(lines);
        OutputVersion++;
    }

}
