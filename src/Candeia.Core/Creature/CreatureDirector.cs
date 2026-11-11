using Candeia.Challenges;
using Candeia.Night;

namespace Candeia.Creature;

public enum CueKind { Approaching, Arrived, Retreated, Listened, Scripted, Captured, Fled }

// A moment the scene and the sound must play. Station is where he is (or is about to be); From is where he
// came from; Effect, Where and Seconds describe a scripted event from the night's file.
public sealed record CreatureCue(CueKind Kind, Station Station, Station? From = null, string? Effect = null, Station? Where = null, double Seconds = 0);

// Where he is and how he is shown. Closeness runs from 0 (just arrived at this station) to 1 (about to
// reach the next); Visible is false where the act only lets him be heard; Tension grows from 0 to 1 as
// the time runs out.
public sealed record CreatureState(Station Station, double Closeness, bool Visible, double Tension);

// Decides where the Papa-Figo is and what he does; the scene and the mixer only show and play it.
// He follows the clock. Every arrival is announced by a sound a little before it happens, so a player
// always gets the chance to react, and never loses to a surprise.
public sealed class CreatureDirector
{
    readonly NightRules rules;
    readonly Station visibleUntil;
    readonly IReadOnlyList<NightEvent> events;
    Station current = Station.Street;
    bool announced, firstLamp;

    public CreatureDirector(PenaltyRules penalties, IReadOnlyList<NightEvent>? events = null)
    {
        rules = penalties.Rules;
        visibleUntil = penalties.VisibleUntil;
        this.events = events ?? [];
    }

    public CreatureState State { get; private set; } = new(Station.Street, 0, true, 0);

    public IReadOnlyList<CreatureCue> Update(NightClock clock, IReadOnlyList<ClockEvent> happened)
    {
        var cues = new List<CreatureCue>();
        foreach (var e in happened)
        {
            switch (e.Kind)
            {
                case ClockEventKind.Started: Scripted("start", cues); break;
                case ClockEventKind.LampsLit when !firstLamp: firstLamp = true; Scripted("first-new-lamp", cues); break;
                case ClockEventKind.Listened: cues.Add(new(CueKind.Listened, current)); break;
                case ClockEventKind.Won: Scripted("all-lamps", cues); cues.Add(new(CueKind.Fled, current)); break;
                case ClockEventKind.Captured: cues.Add(new(CueKind.Captured, Station.Beside)); break;
            }
        }
        if (clock.State is ClockState.Captured or ClockState.Won)
        {
            State = State with { Tension = clock.State == ClockState.Captured ? 1 : State.Tension };
            return cues;
        }

        double fraction = clock.FractionLeft;
        var station = rules.StationAt(fraction);
        if (station > current) { cues.Add(new(CueKind.Arrived, station, current)); current = station; announced = false; }
        else if (station < current) { cues.Add(new(CueKind.Retreated, station, current)); current = station; announced = false; }

        // The warning re-arms whenever he is pushed back out of its range, even without changing station,
        // so every approach is announced.
        if (current < Station.Beside)
        {
            var next = current + 1;
            double secondsUntil = (fraction - rules.StartOf(next)) * clock.StartSeconds;
            if (secondsUntil > rules.TelegraphSeconds) announced = false;
            else if (!announced && clock.State == ClockState.Coding) { cues.Add(new(CueKind.Approaching, next, current)); announced = true; }
        }

        double from = rules.StartOf(current), to = current == Station.Beside ? 0 : rules.StartOf(current + 1);
        State = new(current, Math.Clamp((from - fraction) / (from - to), 0, 1), current <= visibleUntil, Math.Clamp(1 - fraction, 0, 1));
        return cues;
    }

    void Scripted(string trigger, List<CreatureCue> cues)
    {
        foreach (var e in events.Where(e => e.On == trigger))
            cues.Add(new(CueKind.Scripted, current, Effect: e.Effect, Where: Place(e.Where), Seconds: e.Seconds));
    }

    static Station Place(string name) => Enum.GetValues<Station>().First(s => string.Equals(s.ToString(), name, StringComparison.OrdinalIgnoreCase));
}
