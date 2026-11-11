using Candeia.Challenges;
using Candeia.Creature;
using Candeia.Night;

namespace Candeia.Tests;

// The night's rules against the numbers approved in the game design document.
static class NightRulesTests
{
    static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

    public static void Run(TestRunner t)
    {
        var rules = NightRules.Load();
        var act1 = new PenaltyRules(rules, 1, Difficulty.Night);
        t.Check(act1.StartSeconds == 360 && act1.Candles == 3, "Act 1 nights start with 6 minutes and 3 candles");
        t.Check(new PenaltyRules(rules, 1, Difficulty.Vigil).StartSeconds == 540 && new PenaltyRules(rules, 1, Difficulty.Nightmare).StartSeconds == 270, "Vigil adds 50% and Nightmare takes 25%");
        t.Check(new PenaltyRules(rules, 1, Difficulty.Night, siege: true).StartSeconds == 540, "the Act 1 siege lasts 9 minutes");
        t.Check(Near(act1.LampBonus(1, 13), 360 * 0.60 / 13) && Near(act1.LampBonus(13, 13), 216), "the lamps share 60% of the time in Act 1");
        t.Check(Near(act1.NoProgressCost(1), 7.2) && Near(act1.NoProgressCost(2), 14.4) && Near(act1.NoProgressCost(3), 21.6) && Near(act1.NoProgressCost(7), 21.6),
            "runs without progress cost 2%, 4%, then 6% each");
        t.Check(Near(act1.CrashCost, 18) && act1.StreakAdvanceCost(5) == 0, "a crash costs 5%; Act 1 has no extra advance");
        var act4 = new PenaltyRules(rules, 4, Difficulty.Night);
        t.Check(Near(act4.NoProgressCost(3), 480 * 0.12), "in Act 4 the third run without progress costs 12%");
        t.Check(new PenaltyRules(rules, 2, Difficulty.Night).StreakAdvanceCost(3) > 0 && new PenaltyRules(rules, 2, Difficulty.Night).StreakAdvanceCost(2) == 0,
            "from Act 2, the third run without progress adds an extra advance");
        var vigil = new PenaltyRules(rules, 1, Difficulty.Vigil);
        t.Check(Near(vigil.NoProgressCost(1), 540 * 0.02 * 0.5) && vigil.SweetCost == 0 && vigil.Candles == 4, "Vigil halves the costs, gives the sweet for free and 4 candles");
        t.Check(!new PenaltyRules(rules, 1, Difficulty.Nightmare).SweetAvailable, "Nightmare has no sweet");
        t.Check(Near(act1.NearCaptureSeconds, 108), "a near capture gives back 30% of the time");

        t.Check(rules.StationAt(1) == Station.Street && rules.StationAt(0.81) == Station.Street && rules.StationAt(0.80) == Station.Gate
            && rules.StationAt(0.6) == Station.Yard && rules.StationAt(0.4) == Station.Window && rules.StationAt(0.2) == Station.Door
            && rules.StationAt(0.08) == Station.Beside && rules.StationAt(0) == Station.Beside, "the stations follow the bands of the design");

        // The clock.
        var clock = new NightClock(act1, lamps: 4);
        t.Check(clock.Advance(10).Count == 0 && clock.Remaining == 360, "the clock does not run before the night starts");
        t.Check(clock.BeginCoding() is [{ Kind: ClockEventKind.Started }], "the night starts");
        clock.Advance(10);
        t.Check(Near(clock.Remaining, 350), "the clock runs while coding");
        clock.Pause(); clock.Advance(30); clock.Resume();
        t.Check(Near(clock.Remaining, 350), "a paused clock does not run");
        clock.BeginRun(); clock.Advance(30);
        t.Check(Near(clock.Remaining, 350), "the clock waits while the lamps light");
        var lit = clock.ApplyRun(new([true, false, false, false], false, []));
        t.Check(lit.Any(e => e.Kind == ClockEventKind.LampsLit && e.Count == 1) && Near(clock.Remaining, 360), "a new lamp gives back time, up to the night's start");
        var miss1 = clock.ApplyRun(new([true, false, false, false], false, []));
        t.Check(miss1.Any(e => e.Kind == ClockEventKind.NoProgressCost && Near(e.Seconds, -7.2)) && clock.NoProgressStreak == 1, "the first run without a new lamp costs 2%");
        var miss2 = clock.ApplyRun(new([false, false, false, false], false, []));
        t.Check(miss2.Any(e => e.Kind == ClockEventKind.NoProgressCost && Near(e.Seconds, -14.4)), "the second costs 4%");
        t.Check(miss2.Any(e => e.Kind == ClockEventKind.LampsOut && e.Count == 1), "a lamp that goes out is reported");
        t.Check(miss2.Any(e => e.Kind == ClockEventKind.SweetOffered) && clock.SweetPending, "two runs in a row without progress bring the sweet");
        var sweet = clock.AcceptSweet();
        t.Check(sweet is [{ Kind: ClockEventKind.SweetCost }] && Near(sweet[0].Seconds, -18) && clock.SweetsAccepted == 1, "the sweet costs 5%");
        var relit = clock.ApplyRun(new([true, true, false, false], false, []));
        t.Check(relit.Any(e => e.Kind == ClockEventKind.LampsLit && e.Count == 1) && clock.NoProgressStreak == 0, "a lamp lit again gives no time a second time; only the new one counts");
        var crash = clock.ApplyRun(new([true, true, false, false], true, []));
        t.Check(crash.Any(e => e.Kind == ClockEventKind.CrashCost && Near(e.Seconds, -18)), "a crash costs 5% instead of the no-progress cost");
        var almost = clock.ApplyRun(new([true, true, true, true], false, ["declare:bool"]));
        t.Check(almost.Any(e => e.Kind == ClockEventKind.ConceptsMissing && e.Detail == "declare:bool") && clock.State == ClockState.Coding, "all lamps lit without the required concept is not a win");
        var won = clock.ApplyRun(new([true, true, true, true], false, []));
        t.Check(won.Any(e => e.Kind == ClockEventKind.Won) && clock.State == ClockState.Won, "all lamps and the concepts win the night");
        t.Check(clock.Marks == new NightMarks(true, false, true), $"marks: unharmed, a sweet was taken, he stayed far: {clock.Marks}");

        // Candles and capture.
        var stuck = new NightClock(act1, lamps: 4);
        stuck.BeginCoding();
        var first = stuck.Advance(400);
        t.Check(first.Any(e => e.Kind == ClockEventKind.NearCapture && e.Count == 2) && Near(stuck.Remaining, 108) && stuck.State == ClockState.Respite, "time up loses a candle and gives back 30%");
        stuck.Advance(3);
        t.Check(Near(stuck.Remaining, 108), "the respite stops the clock");
        t.Check(stuck.Advance(3).Any(e => e.Kind == ClockEventKind.RespiteOver), "the respite lasts 5 seconds");
        stuck.Advance(200); stuck.Advance(10); stuck.Advance(200);
        t.Check(stuck.State == ClockState.Captured && stuck.CandlesLeft == 0, "the third time up, with the last candle, is the capture");
        t.Check(!stuck.Marks.Unharmed, "losing a candle loses the Unharmed mark");

        var idle = new NightClock(act1, lamps: 4);
        idle.BeginCoding();
        t.Check(idle.Advance(119).Count == 0 && idle.Advance(2).Any(e => e.Kind == ClockEventKind.SweetOffered), "a third of the night without a new lamp brings the sweet");

        // The creature.
        var director = new CreatureDirector(act1, [new NightEvent("first-new-lamp", "silhouette", "yard", 0.6)]);
        var night = new NightClock(act1, lamps: 4);
        var cues = director.Update(night, night.BeginCoding()).ToList();
        for (int second = 0; second < 69; second++) cues.AddRange(director.Update(night, night.Advance(1)));
        t.Check(!cues.Any(c => c.Kind == CueKind.Approaching), "no warning far from the gate");
        var warning = director.Update(night, night.Advance(1));
        t.Check(warning.Any(c => c.Kind == CueKind.Approaching && c.Station == Station.Gate), "he is heard 2 seconds before reaching the gate");
        director.Update(night, night.Advance(1));
        t.Check(director.Update(night, night.Advance(1)).Any(c => c.Kind == CueKind.Arrived && c.Station == Station.Gate), "then he arrives at the gate");
        var back = director.Update(night, night.ApplyRun(new([true, true, false, false], false, [])));
        t.Check(back.Any(c => c.Kind == CueKind.Retreated && c.Station == Station.Street), "new lamps push him back");
        t.Check(back.Count(c => c.Kind == CueKind.Scripted && c.Effect == "silhouette" && c.Where == Station.Yard) == 1, "the first new lamp plays the scripted silhouette");
        t.Check(!director.Update(night, night.ApplyRun(new([true, true, true, false], false, []))).Any(c => c.Kind == CueKind.Scripted), "the scripted event plays once");
        while (night.FractionLeft > 0.39) director.Update(night, night.Advance(1));
        t.Check(director.State.Station == Station.Window && !director.State.Visible, "in Act 1 he is only heard from the window on");
        // A warning, then a lamp pushes him back without changing station: the next approach is announced again.
        var rearm = new CreatureDirector(act1);
        var again = new NightClock(act1, lamps: 4);
        rearm.Update(again, again.BeginCoding());
        var heard = new List<CreatureCue>();
        for (int second = 0; second < 70; second++) heard.AddRange(rearm.Update(again, again.Advance(1)));
        heard.AddRange(rearm.Update(again, again.ApplyRun(new([true, false, false, false], false, []))));
        for (int second = 0; second < 120; second++) heard.AddRange(rearm.Update(again, again.Advance(1)));
        t.Check(heard.Count(c => c.Kind == CueKind.Approaching && c.Station == Station.Gate) == 2, $"every approach is announced, even after a push back: {heard.Count(c => c.Kind == CueKind.Approaching)}");

        var actFour = new CreatureDirector(new PenaltyRules(rules, 4, Difficulty.Night));
        var late = new NightClock(new PenaltyRules(rules, 4, Difficulty.Night), lamps: 1);
        late.BeginCoding();
        late.Advance(470);
        actFour.Update(late, []);
        t.Check(actFour.State.Station == Station.Beside && actFour.State.Visible, "in Act 4 he is seen beside you");
    }
}
