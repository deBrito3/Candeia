using Candeia.Creature;
using Candeia.Night;

namespace Candeia.Audio;

// A caption for an important sound, with where it comes from (-1 left, the window; +1 right, the door).
public sealed record Caption(string Pt, string En, double Pan, double Until)
{
    // "← [gate creaking]" when it comes from the left, "[breathing] →" from the right.
    public string Text(bool english)
    {
        string text = $"[{(english ? En : Pt)}]";
        return Pan < -0.3 ? "← " + text : Pan > 0.3 ? text + " →" : text;
    }
}

// Turns what happens in the night into sound: the house's ambience, the system answering the player, and
// him, placed where he is (the window to the left, the door to the right, far sounds duller). It only
// decides; the Mixer plays. Captions name the sounds that matter, for players without sound.
public sealed class SoundDirector
{
    public const double CaptionSeconds = 3.5;

    // Where each station sounds from, and how far: the outside is to the left, through the window.
    static readonly (double Pan, double Distance)[] Places =
    [
        (-0.75, 0.85), // street
        (-0.75, 0.65), // gate
        (-0.7, 0.4),   // yard
        (-0.85, 0.08), // window
        (0.8, 0.12),   // door
        (0.35, 0.0),   // beside
    ];

    readonly Mixer mixer;
    readonly SoundBank bank;
    readonly List<Caption> captions = [];
    readonly List<(double At, Action Act)> later = [];
    int crickets, breath, score, tense;
    bool scoreWaiting;
    double tenseLevel = -1;
    double cricketsBackAt = -1;
    int keys;
    IReadOnlyList<LampState> lamps = [];
    bool wasBusy, won;

    public SoundDirector(Mixer mixer, SoundBank bank)
    {
        this.mixer = mixer;
        this.bank = bank;
    }

    public IReadOnlyList<Caption> Captions(double now)
    {
        captions.RemoveAll(c => c.Until <= now);
        return captions;
    }

    // The house at night, from the moment the night opens: soft rain on the roof, low, and the crickets.
    // Nothing else plays all the time: a fan and a hum tired the ear, and were taken out.
    public void Begin()
    {
        End();
        mixer.Play(bank["rain"], Channel.House, 0.25, fadeIn: 3);
        crickets = mixer.Play(bank["crickets"], Channel.House, 0.45, fadeIn: 2);
        // The score starts as soon as it is made (it is made in the background, on the first night).
        scoreWaiting = true;
        score = tense = 0;
        tenseLevel = -1;
        lamps = [];
        wasBusy = won = false;
        cricketsBackAt = -1;
    }

    public void End()
    {
        mixer.StopAll(0.3);
        captions.Clear();
        later.Clear();
        breath = 0;
        scoreWaiting = false;
    }

    // A key typed in the editor: one of three soft clicks.
    public void Key() => mixer.Play(bank[$"key{1 + keys++ % 3}"], Channel.Interface, 0.45, pan: 0.05 * (keys % 3 - 1));

    // Called every frame with what the night did.
    public void Update(double now, SessionTick tick, CreatureState creature, IReadOnlyList<LampState> lampStates, bool busy)
    {
        if (later.Count > 0)
        {
            foreach (var (at, act) in later.Where(l => l.At <= now).ToList()) act();
            later.RemoveAll(l => l.At <= now);
        }

        // F5: the relay closes.
        if (busy && !wasBusy) mixer.Play(bank["relay"], Channel.System, 0.8);
        wasBusy = busy;
        Lamps(lampStates);

        foreach (var cue in tick.Cues) Creature(cue, now);
        foreach (var e in tick.Events) Clock(e, now);

        if (scoreWaiting && bank.Ready("score") && bank.Ready("score-tension"))
        {
            scoreWaiting = false;
            score = mixer.Play(bank["score"], Channel.Music, 0.9, fadeIn: 6);
            tense = mixer.Play(bank["score-tension"], Channel.Music, 0, fadeIn: 0);
        }

        // He is near: the score's tense layer rises, and from the door on, he breathes.
        double tension = won ? 0 : creature.Tension;
        if (tense != 0 && Math.Abs(tension - tenseLevel) > 0.01)
        {
            tenseLevel = tension;
            mixer.Set(tense, volume: 0.9 * Math.Pow(tension, 1.5), seconds: 3);
            mixer.Set(score, volume: 0.9 * (1 - 0.35 * tension), seconds: 3);
        }
        bool near = !won && creature.Station >= Station.Door;
        if (near && (breath == 0 || !mixer.Playing(breath)))
        {
            var (pan, distance) = Places[(int)creature.Station];
            breath = mixer.Play(bank["breath"], Channel.Creature, 0.5, pan, distance: distance, fadeIn: 2);
            Say("respiração", "breathing", pan, now);
        }
        else if (near && (int)(now * 4) != (int)((now - 1 / 60.0) * 4))
        {
            // A few times a second is enough for the breathing to follow him.
            var (pan, distance) = Places[(int)creature.Station];
            mixer.Set(breath, volume: 0.45 + 0.4 * creature.Closeness + (creature.Station == Station.Beside ? 0.2 : 0), pan: pan, distance: distance, seconds: 1);
        }
        else if (breath != 0)
        {
            mixer.Stop(breath, 1.5);
            breath = 0;
        }

        if (cricketsBackAt > 0 && now >= cricketsBackAt)
        {
            mixer.Set(crickets, volume: 0.45, seconds: 2.5);
            cricketsBackAt = -1;
        }
    }

    // Each lamp that lights clicks; each that stays dark thunks; all of them lit ring.
    void Lamps(IReadOnlyList<LampState> now)
    {
        if (now.Count == lamps.Count && now.SequenceEqual(lamps)) return;
        if (now.Count == lamps.Count)
        {
            for (int i = 0; i < now.Count; i++)
            {
                if (now[i] == lamps[i] || now[i] == LampState.Unknown) continue;
                mixer.Play(bank[now[i] == LampState.Lit ? "lamp-on" : "lamp-out"], Channel.System, 0.8, pan: -0.2 + 0.4 * i / Math.Max(1, now.Count - 1));
            }
            if (now.Count > 0 && now.All(l => l == LampState.Lit) && !lamps.All(l => l == LampState.Lit))
                later.Add((0, () => mixer.Play(bank["pass"], Channel.System, 0.8)));
        }
        lamps = now.ToArray();
    }

    void Creature(CreatureCue cue, double now)
    {
        var (pan, distance) = Places[(int)cue.Station];
        switch (cue.Kind)
        {
            // The two seconds before he arrives somewhere: you hear him coming.
            case CueKind.Approaching:
                var (sound, pt, en) = cue.Station switch
                {
                    Station.Gate or Station.Street => ("cane", "bengala nas pedras", "cane on the cobblestones"),
                    Station.Yard or Station.Window => ("steps-grass", "passos no quintal", "footsteps in the yard"),
                    _ => ("steps-wood", "passos no assoalho", "footsteps on the floorboards"),
                };
                Play(sound, pan, distance, 0.9);
                Say(pt, en, pan, now);
                break;
            // Each station has its mark.
            case CueKind.Arrived:
                switch (cue.Station)
                {
                    case Station.Street: Play("cane", pan, distance, 0.8); Say("bengala nas pedras", "cane on the cobblestones", pan, now); break;
                    case Station.Gate:
                        Play("gate", pan, distance, 1);
                        Say("portão rangendo", "gate creaking", pan, now);
                        later.Add((now + 1.9, () => { Play("psiu", pan, distance, 0.9); Say("“psiu”", "“psst”", pan, now + 1.9); }));
                        break;
                    case Station.Yard: Play("cough", pan, distance, 0.9); Say("tosse", "a cough", pan, now); break;
                    case Station.Window: Play("wrapper", pan, distance, 1.6); Say("papel de bala", "candy wrapper", pan, now); break;
                    case Station.Door: Play("door", pan, distance, 1); Say("a porta range", "the door creaks", pan, now); break;
                    case Station.Beside: Play("wrapper", pan, distance, 1.8); Say("papel de bala, bem perto", "candy wrapper, very close", pan, now); break;
                }
                break;
            case CueKind.Retreated:
                var (fromPan, fromDistance) = Places[(int)(cue.From ?? cue.Station)];
                Play("steps-away", fromPan, fromDistance, 0.8);
                Say("passos se afastando", "footsteps going away", fromPan, now);
                break;
            // A compile error: the crickets go quiet. He is listening.
            case CueKind.Listened:
                mixer.Play(bank["error"], Channel.System, 0.8);
                mixer.Set(crickets, volume: 0, seconds: 0.3);
                cricketsBackAt = now + 2;
                Say("os grilos se calam", "the crickets go quiet", 0, now);
                break;
            case CueKind.Scripted when cue.Effect == "silhouette":
                var (glimpsePan, _) = Places[(int)(cue.Where ?? cue.Station)];
                later.Add((now + 0.9, () => { mixer.Play(bank["thunder"], Channel.House, 1, glimpsePan * 0.5); Say("trovão", "thunder", glimpsePan * 0.5, now + 0.9); }));
                break;
            case CueKind.Captured:
                Play("sack", Places[(int)Station.Beside].Pan, 0, 1.2);
                Say("um saco se fecha", "a sack closes", Places[(int)Station.Beside].Pan, now);
                break;
            case CueKind.Fled:
                won = true;
                Play("steps-away", pan, distance, 0.7);
                Say("passos se afastando", "footsteps going away", pan, now);
                break;
        }
    }

    void Clock(ClockEvent e, double now)
    {
        switch (e.Kind)
        {
            // "The house makes noise", literally.
            case ClockEventKind.NoProgressCost:
                mixer.Play(bank["creak"], Channel.House, 0.9, pan: 0.4);
                Say("a casa estala", "the house creaks", 0.4, now);
                break;
            case ClockEventKind.CrashCost:
                mixer.Play(bank["surge"], Channel.System, 0.9);
                Say("a energia oscila", "the power flickers", 0, now);
                break;
            case ClockEventKind.NearCapture:
                mixer.Play(bank["candle-out"], Channel.House, 1, pan: 0.2);
                Say("uma vela se apaga", "a candle goes out", 0.2, now);
                break;
            case ClockEventKind.Started:
                won = false;
                break;
        }
    }

    void Play(string sound, double pan, double distance, double volume) =>
        mixer.Play(bank[sound], Channel.Creature, volume, pan, distance: distance);

    void Say(string pt, string en, double pan, double now)
    {
        captions.RemoveAll(c => c.Pt == pt);
        captions.Add(new Caption(pt, en, pan, now + CaptionSeconds));
        if (captions.Count > 3) captions.RemoveAt(0);
    }
}
