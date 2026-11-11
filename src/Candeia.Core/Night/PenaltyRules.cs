using Candeia.Execution;

namespace Candeia.Night;

// "The house makes noise": how much time each thing gives back or costs, for one night.
// Every percentage is a fraction of the night's starting time (the act's time times the difficulty's).
public sealed class PenaltyRules
{
    readonly NightRules rules;
    readonly ActRules act;
    readonly DifficultyRules difficulty;

    public PenaltyRules(NightRules rules, int act, Difficulty difficulty, bool siege = false)
    {
        this.rules = rules;
        this.act = rules.Acts[act];
        this.difficulty = rules.Difficulties[difficulty];
        StartSeconds = (siege ? this.act.SiegeSeconds : this.act.NightSeconds) * this.difficulty.Time;
    }

    public NightRules Rules => rules;
    public int Act => act.Act;
    public double StartSeconds { get; }
    public int Candles => difficulty.Candles;
    public bool SweetAvailable => difficulty.Sweet != SweetPolicy.None;
    public Station VisibleUntil => act.VisibleUntil;

    double CostScale => StartSeconds * act.CostMultiplier * difficulty.Costs;

    // The act's lamp pool, shared equally by the night's lamps.
    public double LampBonus(int newLamps, int totalLamps) => totalLamps <= 0 ? 0 : StartSeconds * act.LampPool * newLamps / totalLamps;

    // The n-th run in a row without a new lamp (n from 1): 2%, 4%, then 6% each, in Act 1.
    public double NoProgressCost(int streak) => CostScale * rules.NoProgressCosts[Math.Clamp(streak, 1, rules.NoProgressCosts.Count) - 1];

    // A run that froze or crashed, instead of the no-progress cost.
    public double CrashCost => CostScale * rules.CrashCost;

    // From Act 2: an extra advance once the runs without progress pile up.
    public double StreakAdvanceCost(int streak) => act.StreakAdvance is { } advance && streak >= advance.After ? CostScale * advance.Cost : 0;

    public double SweetCost => difficulty.Sweet == SweetPolicy.Paid ? CostScale * rules.SweetCost : 0;

    public double NearCaptureSeconds => StartSeconds * rules.NearCaptureReturn;

    // Runs that crash or freeze are charged as such; a missing method or a failed runner is plain no progress.
    public static bool Crashed(RunStop stop) => stop is RunStop.StepLimit or RunStop.Timeout or RunStop.StackOverflow or RunStop.OutOfMemory or RunStop.ProcessExit;
}
