using System.Runtime.Versioning;
using Candeia.Challenges;
using Candeia.Creature;
using Candeia.Languages.CSharp;
using Candeia.Night;

namespace Candeia.Tests;

// Night 1 played through NightSession, with the real compiler and the isolated runner, without a screen.
[SupportedOSPlatform("windows")]
static class NightSessionTests
{
    const double Frame = 1 / 60.0;

    public static void Run(TestRunner t)
    {
        var loaded = NightLoader.Load(Path.Combine(NightLoader.DefaultRoot, "act1", "01-room-light"));
        if (!loaded.Ok) { t.Check(false, "night 1 loads: " + string.Join("; ", loaded.Errors)); return; }
        var night = loaded.Night!;
        var rules = NightRules.Load();
        var runtime = new CSharpRuntime();
        using var host = RunnerTests.Host();
        host.Warm();
        NightSession New() => new(night, rules, Difficulty.Night, runtime, host.Run);
        string solution = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Solutions", night.Id + ".cs"));

        // The introduction does not run the clock; F5 starts the night.
        var starter = New();
        starter.Tick(30);
        t.Check(starter.Clock.State == ClockState.Intro && starter.Clock.Remaining == starter.Clock.StartSeconds, "reading the LEIA-ME costs no time");
        t.Check(starter.Execute(night.Starter), "F5 in the introduction starts the night and runs the code");
        t.Check(!starter.Execute(night.Starter), "only one run at a time");
        var ticks = Play(starter);
        t.Check(ticks.Any(e => e.Kind == ClockEventKind.Started), "the night starts");
        t.Check(starter.Lamps.SequenceEqual([LampState.Lit, LampState.Out, LampState.Out, LampState.Out]), $"the starter lights only Watts: {string.Join(",", starter.Lamps)}");
        t.Check(starter.Clock.State == ClockState.Coding && starter.Output[0].Text.StartsWith("1 de 4"), $"the output counts the lamps: {starter.Output.FirstOrDefault()?.Text}");
        t.Check(starter.Output.Any(l => l.Kind == OutputKind.Fail && l.Text.Contains("Voltage() = 0") && l.Text.Contains("esperado 220")), "a failed test shows what came back and what was expected");
        t.Check(starter.Output.Any(l => l.Kind == OutputKind.Time && l.Text.StartsWith('+')), "a new lamp gives time back, said in the output");
        t.Check(Math.Abs(starter.HouseLight - 0.25) < 1e-9, "the house is lit by the share of lamps");

        // Each hint layer is a sweet: it costs 5% of the time and the No sweet mark.
        var helped = New();
        helped.Start();
        helped.Tick(1);
        double beforeSweet = helped.Clock.Remaining;
        t.Check(helped.HintAvailable && helped.ShowHint() && helped.HintsShown == 1, "the first hint opens");
        helped.Tick(0);
        t.Check(Math.Abs(beforeSweet - helped.Clock.Remaining - helped.Penalties.SweetCost) < 1e-6 && helped.Clock.SweetsAccepted == 1 && !helped.Clock.Marks.NoSweet,
            $"and costs the sweet's price: {beforeSweet - helped.Clock.Remaining:0.0} s");
        t.Check(helped.Output.Any(l => l.Kind == OutputKind.Time && l.Text.Contains("um doce aceito")), "the output says what the sweet cost");
        t.Check(helped.ShowHint() && helped.ShowHint() && !helped.ShowHint() && !helped.HintAvailable && helped.HintsShown == 3, "three layers, then no more");
        var vigil = new NightSession(night, rules, Difficulty.Vigil, runtime, host.Run);
        vigil.Start();
        double vigilBefore = vigil.Clock.Remaining;
        vigil.ShowHint();
        vigil.Tick(0);
        t.Check(vigil.HintsShown == 1 && vigil.Clock.Remaining == vigilBefore, "in Vigil the sweet is free");
        t.Check(!new NightSession(night, rules, Difficulty.Nightmare, runtime, host.Run).HintAvailable, "in Nightmare there is no sweet");

        // The lamps light one at a time, with the clock stopped.
        var reveal = New();
        reveal.Execute(solution);
        while (reveal.Busy && reveal.Lamps.All(l => l == LampState.Unknown)) { reveal.Tick(0.01); Thread.Sleep(2); }
        double before = reveal.Clock.Remaining;
        t.Check(reveal.Lamps.Count(l => l != LampState.Unknown) == 1, "the first lamp lights alone");
        reveal.Tick(NightSession.LampSeconds);
        t.Check(reveal.Lamps.Count(l => l != LampState.Unknown) == 2 && reveal.Clock.Remaining == before, "the next lamp follows, and the clock waits");

        // The reference solution wins.
        var won = New();
        won.Execute(solution);
        var winning = Play(won);
        t.Check(won.Clock.State == ClockState.Won && winning.Any(e => e.Kind == ClockEventKind.Won), "the reference solution wins the night");
        t.Check(won.HouseLight == 1 && won.Output.Any(l => l.Kind == OutputKind.Win && l.Text.StartsWith("NOITE VENCIDA")), "the house lights up and the output says so");
        t.Check(!won.Execute(solution), "nothing runs after the night is won");
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (won.Review is null && waited.Elapsed.TotalSeconds < 20) { won.Tick(Frame); Thread.Sleep(5); }
        var review = won.Review;
        int returnLine = solution.Split('\n').ToList().FindIndex(l => l.Contains("return watts;")) + 1;
        t.Check(review is not null && review.Code == solution && review.Marks.Unharmed && review.Learned.Pt.Length > 0, "a won night has its review");
        t.Check(review is not null && returnLine > 0 && returnLine < review.Lines.Count && review.Lines[returnLine] == 1, $"the review counts how many times each line ran: line {returnLine}");

        // Every lamp lit without the required concept is not a win.
        var shortcut = New();
        shortcut.Execute(solution.Replace("bool on = true;\n        return on;", "return true;").Replace("bool on = true;\r\n        return on;", "return true;"));
        Play(shortcut);
        t.Check(shortcut.Clock.State == ClockState.Coding && shortcut.Output.Any(l => l.Kind == OutputKind.Warning && l.Text.Contains("`bool`")), "lit without a bool variable: the LEIA-ME still asks for it");

        // A compile error makes him listen, marks the line and costs no time.
        var broken = New();
        broken.Start();
        broken.Tick(5);
        double remaining = broken.Clock.Remaining;
        broken.Execute(night.Starter.Replace("return watts;", "return watts"));
        double spent = 0;
        var heard = new List<ClockEvent>();
        var cues = new List<CreatureCue>();
        while (broken.Busy) { var tick = broken.Tick(Frame); heard.AddRange(tick.Events); cues.AddRange(tick.Cues); spent += Frame; Thread.Sleep(1); }
        t.Check(heard.Any(e => e.Kind == ClockEventKind.Listened) && cues.Any(c => c.Kind == CueKind.Listened), "a compile error: he listens");
        t.Check(!heard.Any(e => e.Kind is ClockEventKind.NoProgressCost or ClockEventKind.CrashCost), "a compile error costs no time");
        t.Check(Math.Abs(broken.Clock.Remaining - (remaining - spent)) < 1e-6, "the clock keeps running while the code compiles");
        t.Check(broken.ErrorLine == 10 && broken.Output.Any(l => l.Kind == OutputKind.Error && l.Line == 10), $"the error points to its line: {broken.ErrorLine}");

        // Time running out costs a candle, gives a breath, and the last candle is the capture.
        var late = New();
        late.Start();
        var first = late.Tick(late.Clock.StartSeconds + 1);
        t.Check(first.Events.Any(e => e.Kind == ClockEventKind.NearCapture) && late.Clock.CandlesLeft == 2, "time up: a candle goes out");
        t.Check(late.Output.Any(l => l.Text.Contains("restam 2")), "the output says how many candles are left");
        t.Check(!late.Execute(night.Starter), "no run during the breath");
        late.Tick(10);
        late.Tick(1000);
        late.Tick(10);
        late.Tick(1000);
        t.Check(late.Clock.State == ClockState.Captured && late.Output.Any(l => l.Text.Contains("Ele te pegou")), "the last candle: he takes you");
        t.Check(!late.ShowHint(), "no sweet once he took you");
        late.Restart();
        t.Check(late.HintsShown == 1 && late.Output.Any(l => l.Text.Contains("página nova no caderno")), "after a capture, a new notebook page: the next hint, free");
        t.Check(late.Attempt == 2 && late.Clock.State == ClockState.Intro && late.Clock.CandlesLeft == 3 && late.Lamps.All(l => l == LampState.Unknown), "the night starts over with every candle");
        t.Check(late.Execute(night.Starter), "and the code can run again");
        Play(late);

        // Winning after a capture: the night was not unharmed, and its review counts every attempt.
        late.Execute(solution);
        Play(late);
        var after = System.Diagnostics.Stopwatch.StartNew();
        while (late.Review is null && after.Elapsed.TotalSeconds < 20) { late.Tick(Frame); Thread.Sleep(5); }
        t.Check(late.Clock.State == ClockState.Won && !late.Marks.Unharmed && late.Marks.NoSweet, "won after a capture: not Ileso, still Sem doce");
        t.Check(late.Review is { CandlesLost: 3, Attempts: 2, Marks.Unharmed: false }, "the review counts the candles of every attempt");
        t.Check(late.Output.Any(l => l.Kind == OutputKind.Win && l.Text.Contains("○ Ileso")), "the win says the night was not unharmed");
    }

    // Ticks at 60 frames a second until the run and its lamps are done.
    static List<ClockEvent> Play(NightSession session)
    {
        var events = new List<ClockEvent>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            events.AddRange(session.Tick(Frame).Events);
            Thread.Sleep(1);
        }
        while (session.Busy && watch.Elapsed.TotalSeconds < 20);
        return events;
    }
}
