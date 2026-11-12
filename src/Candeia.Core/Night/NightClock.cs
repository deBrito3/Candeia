using Candeia.Execution;

namespace Candeia.Night;

public enum ClockState { Intro, Coding, Results, Respite, Captured, Won }

public enum ClockEventKind
{
    Started, LampsLit, LampsOut, Bonus, NoProgressCost, CrashCost, StreakAdvanceCost, SweetCost,
    Listened, ConceptsMissing, SweetOffered, NearCapture, RespiteOver, Captured, Won,
}

// Something that happened to the night. Seconds is the time given back (positive) or taken (negative);
// Count is the number of lamps, the run streak or the candles left, depending on the kind.
public sealed record ClockEvent(ClockEventKind Kind, double Seconds = 0, int Count = 0, string? Detail = null);

// What a run did, in the clock's terms: which lamps are lit now, whether the code froze or crashed, and
// which required concepts the code still misses.
public sealed record RunOutcome(IReadOnlyList<bool> Passed, bool Crashed, IReadOnlyList<string> MissingConcepts)
{
    public static RunOutcome From(RunResult result, IReadOnlyList<string> missingConcepts) =>
        new(result.Tests.Select(t => t.Passed).ToList(), PenaltyRules.Crashed(result.Stop), missingConcepts);
}

// The marks of a won night.
public sealed record NightMarks(bool Unharmed, bool NoSweet, bool Distance);

// The elastic time of one night. The game calls Advance with the real time that passed; the clock only
// runs while the player is coding. Every call returns what happened, so the scene, the sound and the
// output panel can react, and the tests can replay a night second by second.
public sealed class NightClock
{
    readonly PenaltyRules penalties;
    readonly int lamps;
    readonly HashSet<int> everLit = [], lit = [];
    double respiteLeft, sinceProgress;
    int streakSinceOffer;

    public NightClock(PenaltyRules penalties, int lamps)
    {
        this.penalties = penalties;
        this.lamps = lamps;
        Remaining = StartSeconds;
        CandlesLeft = penalties.Candles;
    }

    public double StartSeconds => penalties.StartSeconds;
    public double Remaining { get; private set; }
    public double FractionLeft => Remaining / StartSeconds;
    public ClockState State { get; private set; } = ClockState.Intro;
    public bool Paused { get; private set; }
    public int CandlesLeft { get; private set; }
    public int CandlesLost => penalties.Candles - CandlesLeft;
    public int NoProgressStreak { get; private set; }
    public int SweetsAccepted { get; private set; }
    public bool SweetPending { get; private set; }
    public IReadOnlyCollection<int> Lit => lit;
    public PenaltyRules Penalties => penalties;

    public NightMarks Marks => new(CandlesLost == 0, SweetsAccepted == 0, State == ClockState.Won && FractionLeft >= penalties.Rules.DistanceMark - 1e-9);

    public IReadOnlyList<ClockEvent> BeginCoding()
    {
        if (State != ClockState.Intro) return [];
        State = ClockState.Coding;
        return [new(ClockEventKind.Started, Remaining)];
    }

    public void Pause() => Paused = true;
    public void Resume() => Paused = false;

    public IReadOnlyList<ClockEvent> Advance(double seconds)
    {
        if (Paused || seconds <= 0) return [];
        var events = new List<ClockEvent>();
        if (State == ClockState.Respite)
        {
            respiteLeft -= seconds;
            if (respiteLeft <= 0) { State = ClockState.Coding; events.Add(new(ClockEventKind.RespiteOver)); }
            return events;
        }
        if (State != ClockState.Coding) return events;
        Remaining = Math.Max(0, Remaining - seconds);
        sinceProgress += seconds;
        if (sinceProgress >= StartSeconds * penalties.Rules.SweetAfterFractionWithoutLamp) Offer(events);
        if (Remaining <= 0) TimeUp(events);
        return events;
    }

    // The player pressed F5 and the code compiled: the clock stops while the lamps light one by one.
    public void BeginRun()
    {
        if (State == ClockState.Coding) State = ClockState.Results;
    }

    // The code did not compile. He listens, but does not move.
    public IReadOnlyList<ClockEvent> Listened() => State is ClockState.Coding or ClockState.Results ? [new(ClockEventKind.Listened)] : [];

    public IReadOnlyList<ClockEvent> ApplyRun(RunOutcome outcome)
    {
        if (State is not (ClockState.Coding or ClockState.Results)) return [];
        var events = new List<ClockEvent>();
        var passed = Enumerable.Range(0, Math.Min(lamps, outcome.Passed.Count)).Where(i => outcome.Passed[i]).ToHashSet();
        int fresh = passed.Count(i => !everLit.Contains(i));
        int lost = lit.Count(i => !passed.Contains(i));
        lit.Clear(); lit.UnionWith(passed);
        everLit.UnionWith(passed);

        if (fresh > 0)
        {
            events.Add(new(ClockEventKind.LampsLit, Count: fresh));
            events.Add(new(ClockEventKind.Bonus, Give(penalties.LampBonus(fresh, lamps))));
            NoProgressStreak = 0;
            sinceProgress = 0;
            streakSinceOffer = 0;
        }
        else
        {
            NoProgressStreak++;
            streakSinceOffer++;
            events.Add(outcome.Crashed
                ? new(ClockEventKind.CrashCost, -Take(penalties.CrashCost), NoProgressStreak)
                : new(ClockEventKind.NoProgressCost, -Take(penalties.NoProgressCost(NoProgressStreak)), NoProgressStreak));
            double advance = penalties.StreakAdvanceCost(NoProgressStreak);
            if (advance > 0) events.Add(new(ClockEventKind.StreakAdvanceCost, -Take(advance), NoProgressStreak));
        }
        if (lost > 0) events.Add(new(ClockEventKind.LampsOut, Count: lost));

        if (lit.Count == lamps)
        {
            if (outcome.MissingConcepts.Count > 0) events.Add(new(ClockEventKind.ConceptsMissing, Detail: string.Join(",", outcome.MissingConcepts)));
            else
            {
                State = ClockState.Won;
                events.Add(new(ClockEventKind.Won, Remaining));
                return events;
            }
        }
        State = ClockState.Coding;
        if (streakSinceOffer >= penalties.Rules.SweetAfterNoProgressRuns) Offer(events);
        if (Remaining <= 0) TimeUp(events);
        return events;
    }

    // Taking a sweet: offered when the player is stuck, but it can be asked for at any time (decided in
    // delivery 8: the hints are always within reach, at the sweet's price).
    public IReadOnlyList<ClockEvent> AcceptSweet()
    {
        if (!penalties.SweetAvailable || State is ClockState.Won or ClockState.Captured) return [];
        SweetPending = false;
        SweetsAccepted++;
        ResetOffer();
        var events = new List<ClockEvent> { new(ClockEventKind.SweetCost, -Take(penalties.SweetCost), SweetsAccepted) };
        if (Remaining <= 0 && State == ClockState.Coding) TimeUp(events);
        return events;
    }

    public void DeclineSweet()
    {
        if (!SweetPending) return;
        SweetPending = false;
        ResetOffer();
    }

    void Offer(List<ClockEvent> events)
    {
        if (!penalties.SweetAvailable || SweetPending) return;
        SweetPending = true;
        events.Add(new(ClockEventKind.SweetOffered));
    }

    void ResetOffer() { sinceProgress = 0; streakSinceOffer = 0; }

    // Time ran out: a candle goes. With candles left he is pushed back to the window and the player gets a
    // breath; with the last one, he takes the player.
    void TimeUp(List<ClockEvent> events)
    {
        CandlesLeft--;
        if (CandlesLeft <= 0)
        {
            State = ClockState.Captured;
            events.Add(new(ClockEventKind.Captured));
            return;
        }
        Remaining = penalties.NearCaptureSeconds;
        respiteLeft = penalties.Rules.RespiteSeconds;
        State = ClockState.Respite;
        SweetPending = false;
        ResetOffer();
        events.Add(new(ClockEventKind.NearCapture, Remaining, CandlesLeft));
    }

    double Give(double seconds)
    {
        double given = Math.Min(seconds, StartSeconds - Remaining);
        Remaining += given;
        return given;
    }

    double Take(double seconds)
    {
        double taken = Math.Min(seconds, Remaining);
        Remaining -= taken;
        return taken;
    }
}
