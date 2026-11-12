using Candeia.Audio;
using Candeia.Creature;
using Candeia.Night;

namespace Candeia.Tests;

// The sounds, the mixer and the sound director, all without touching the speakers.
static class AudioTests
{
    static readonly string[] Loops = ["score", "score-tension", "rain", "crickets", "breath"];

    public static void Run(TestRunner t)
    {
        var bank = new SoundBank();
        foreach (var name in SoundBank.Names)
        {
            var sound = bank[name];
            var all = sound.Left.Concat(sound.Right).ToArray();
            double rms = Math.Sqrt(all.Average(s => (double)s * s));
            float peak = all.Max(Math.Abs);
            t.Check(sound.Frames > 0 && all.All(float.IsFinite), $"{name}: valid samples");
            t.Check(peak <= 0.95f, $"{name}: no clipping (peak {peak:0.00})");
            t.Check(rms > 0.003, $"{name}: not silent (rms {rms:0.0000})");
            t.Check(sound.Loop == Loops.Contains(name), $"{name}: loops only if it is ambience");
            if (sound.Loop)
                foreach (var side in new[] { sound.Left, sound.Right })
                {
                    float jump = 0;
                    for (int i = 1; i < side.Length; i++) jump = Math.Max(jump, Math.Abs(side[i] - side[i - 1]));
                    float seam = Math.Abs(side[0] - side[^1]);
                    t.Check(seam <= jump * 1.5f + 0.01f, $"{name}: the loop has no seam ({seam:0.000} against {jump:0.000})");
                }
        }

        // The mixer, with a steady test tone.
        var tone = new Sound("tone", Enumerable.Repeat(0.5f, Synth.Rate).ToArray());
        float[] Mix(Mixer mixer, int frames = 256)
        {
            var buffer = new float[frames * 2];
            mixer.Mix(buffer);
            return buffer;
        }
        var centre = new Mixer { Master = 1 };
        centre.Play(tone, Channel.Creature);
        var c = Mix(centre);
        t.Check(Math.Abs(c[100] - c[101]) < 1e-6 && Math.Abs(c[100] - 0.5 * Math.Cos(Math.PI / 4)) < 1e-3, $"a centred voice is equal on both sides: {c[100]:0.000}");
        var left = new Mixer { Master = 1 };
        left.Play(tone, Channel.Creature, pan: -1);
        var l = Mix(left);
        t.Check(l[100] > 0.49 && Math.Abs(l[101]) < 1e-3, "a voice to the left is only on the left");
        var two = new Mixer { Master = 1 };
        two.Play(tone, Channel.Creature, 0.5);
        two.Play(tone, Channel.Creature, 0.5);
        t.Check(Math.Abs(Mix(two)[100] - c[100]) < 1e-3, "voices add up");
        var loud = new Mixer { Master = 1 };
        for (int i = 0; i < 20; i++) loud.Play(tone, Channel.Creature, 1);
        var limited = Mix(loud);
        t.Check(limited.All(s => Math.Abs(s) < 1) && limited[100] > 0.8, "the limiter keeps twenty loud voices under full scale");
        var silent = new Mixer { Master = 1 };
        silent.SetChannel(Channel.House, 0, 0);
        silent.Play(tone, Channel.House);
        t.Check(Mix(silent).All(s => s == 0), "a channel at zero is silent");
        var shortSound = new Sound("short", Enumerable.Repeat(0.3f, 100).ToArray());
        var ends = new Mixer();
        ends.Play(shortSound, Channel.System);
        Mix(ends, 512);
        t.Check(ends.Voices().Count == 0, "a one-shot is dropped when it ends");
        var looping = new Mixer();
        looping.Play(shortSound, Channel.House, loop: true);
        Mix(looping, 512);
        t.Check(looping.Voices().Count == 1, "a loop keeps going");
        var stopped = new Mixer();
        int id = stopped.Play(tone, Channel.Creature);
        stopped.Stop(id, 0.01);
        Mix(stopped, 1024);
        t.Check(stopped.Voices().Count == 0, "a stopped voice fades and goes");
        var fading = new Mixer { Master = 1 };
        fading.Play(tone, Channel.Creature, fadeIn: 1);
        var start = Mix(fading, 64);
        t.Check(Math.Abs(start[0]) < 1e-3 && start[126] > start[2], "a fade-in starts from silence");
        var fast = new Mixer();
        fast.Play(new Sound("thousand", Enumerable.Repeat(0.3f, 1000).ToArray()), Channel.System, rate: 2);
        Mix(fast, 520);
        t.Check(fast.Voices().Count == 0, "at double speed a sound ends in half the time");
        var noise = new Synth.Noise(1);
        var hiss = new Sound("hiss", Enumerable.Range(0, Synth.Rate).Select(_ => noise.Next() * 0.5f).ToArray());
        double Roughness(double distance)
        {
            var mixer = new Mixer { Master = 1 };
            mixer.Play(hiss, Channel.Creature, distance: distance);
            var b = Mix(mixer, 4096);
            double sum = 0;
            for (int i = 2; i < b.Length; i += 2) sum += Math.Abs(b[i] - b[i - 2]);
            return sum;
        }
        t.Check(Roughness(1) < Roughness(0) * 0.3, "distance dulls a sound");

        // The director.
        var mixer = new Mixer();
        var director = new SoundDirector(mixer, bank);
        Func<IReadOnlyList<VoiceInfo>> voices = mixer.Voices;
        director.Begin();
        t.Check(voices().Count(v => v.Loop) == 2 && voices().Any(v => v.Sound == "rain" && v.Volume <= 0.3) && voices().Any(v => v.Sound == "crickets") && !voices().Any(v => v.Sound is "hum" or "fan" or "drips"), "the house's ambience starts with the night: low rain and crickets, no fan, no hum");
        var street = new CreatureState(Station.Street, 0, true, 0);
        var lamps = new[] { LampState.Unknown, LampState.Unknown };
        director.Update(0, Tick(cues: [new(CueKind.Arrived, Station.Gate, Station.Street)]), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "score" && v.Channel == Channel.Music && v.Loop) && voices().First(v => v.Sound == "score-tension").Volume == 0, "the score plays low, its tense layer silent while he is far");
        t.Check(mixer.ChannelVolume(Channel.Music) < mixer.ChannelVolume(Channel.House), "the score sits under the house");
        var gateVoice = voices().FirstOrDefault(v => v.Sound == "gate");
        t.Check(gateVoice is { Channel: Channel.Creature, Pan: < 0 }, "the gate creaks from the left, through the window");
        t.Check(director.Captions(0).Any(cap => cap.Text(false) == "← [portão rangendo]"), "with a caption pointing left");
        director.Update(2, Tick(), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "psiu"), "then, a moment later, “psiu”");
        director.Update(3, Tick(cues: [new(CueKind.Approaching, Station.Door, Station.Window)]), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "steps-wood" && v.Pan > 0) && director.Captions(3).Any(cap => cap.Text(false).EndsWith('→')), "steps towards the door come from the right");
        director.Update(4, Tick(cues: [new(CueKind.Listened, Station.Street)]), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "error") && voices().First(v => v.Sound == "crickets").Volume == 0, "a compile error: the error tone, and the crickets go quiet");
        director.Update(6.5, Tick(), street, lamps, false);
        t.Check(voices().First(v => v.Sound == "crickets").Volume > 0.4, "and come back two seconds later");
        director.Update(7, Tick(), street, lamps, true);
        t.Check(voices().Any(v => v.Sound == "relay"), "F5: the relay closes");
        director.Update(7.3, Tick(), street, [LampState.Lit, LampState.Unknown], true);
        t.Check(voices().Any(v => v.Sound == "lamp-on"), "a lamp that lights clicks");
        director.Update(7.6, Tick(), street, [LampState.Lit, LampState.Out], true);
        t.Check(voices().Any(v => v.Sound == "lamp-out"), "a lamp that stays dark thunks");
        director.Update(8, Tick(), street, [LampState.Lit, LampState.Lit], false);
        director.Update(8.1, Tick(), street, [LampState.Lit, LampState.Lit], false);
        t.Check(voices().Any(v => v.Sound == "pass"), "every lamp lit rings");
        director.Update(9, Tick(events: [new(ClockEventKind.NoProgressCost, -7)]), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "creak") && director.Captions(9).Any(cap => cap.Pt == "a casa estala"), "the house makes noise, literally");
        director.Update(10, Tick(), new CreatureState(Station.Door, 0.5, false, 0.75), lamps, false);
        t.Check(voices().Any(v => v.Sound == "breath" && !v.Stopping && v.Pan > 0), "at the door, he breathes, to the right");
        t.Check(voices().First(v => v.Sound == "score-tension").Volume > 0.5, "the score's tense layer rises as he comes closer");
        director.Update(11, Tick(), street, lamps, false);
        t.Check(voices().Where(v => v.Sound == "breath").All(v => v.Stopping), "back in the street, the breathing stops");
        t.Check(voices().First(v => v.Sound == "score-tension").Volume < 0.01, "and the tense layer goes quiet");
        t.Check(!director.Captions(11 + SoundDirector.CaptionSeconds + 1).Any(), "captions go away after a few seconds");
        director.Key();
        t.Check(voices().Any(v => v.Sound.StartsWith("key", StringComparison.Ordinal) && v.Channel == Channel.Interface), "typing clicks");

        // The scares come after a silence. Time ran out with candles left: he comes close, a match, a candle.
        double house = mixer.ChannelVolume(Channel.House), music = mixer.ChannelVolume(Channel.Music);
        director.Update(20, Tick(events: [new(ClockEventKind.NearCapture)]), street, lamps, false);
        t.Check(mixer.ChannelVolume(Channel.House) < house * 0.1 && mixer.ChannelVolume(Channel.Music) < music * 0.1 && !voices().Any(v => v.Sound is "match" or "candle-out"), "time runs out: first, silence");
        director.Update(21.45, Tick(), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "match") && Math.Abs(mixer.ChannelVolume(Channel.House) - house) < 1e-9 && director.Captions(21.45).Any(cap => cap.Pt == "um fósforo é riscado"), "then a match is struck, and the house comes back");
        director.Update(21.8, Tick(), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "candle-out") && director.Captions(21.8).Any(cap => cap.Pt == "uma vela se apaga"), "and a candle goes out");

        // The last candle: silence, he is beside you, then the sack.
        director.Update(30, Tick(cues: [new(CueKind.Captured, Station.Beside)]), street, lamps, false);
        t.Check(mixer.ChannelVolume(Channel.Creature) < 0.1 && !voices().Any(v => v.Sound is "sting" or "sack"), "the capture starts in silence");
        director.Update(31.55, Tick(), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "sting") && !voices().Any(v => v.Sound == "sack") && director.Captions(31.55).Any(cap => cap.Pt == "ele está ao seu lado"), "then the sting: he is beside you");
        director.Update(31.85, Tick(), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "sack"), "then the sack closes");

        // A sweet: a wrapper on the windowsill, to the left.
        director.Update(40, Tick(events: [new(ClockEventKind.SweetOffered)]), street, lamps, false);
        t.Check(voices().Any(v => v.Sound == "wrapper" && v.Pan < 0) && director.Captions(40).Any(cap => cap.Pt == "papel de bala no parapeito"), "a sweet: the wrapper on the windowsill");

        // Closing the night in the middle of a silence gives the channels back.
        director.Update(50, Tick(events: [new(ClockEventKind.NearCapture)]), street, lamps, false);
        director.End();
        t.Check(Math.Abs(mixer.ChannelVolume(Channel.House) - house) < 1e-9, "closing the night during a silence ends the silence");

        // Soft scares: the sting and the sack are quieter.
        var softMixer = new Mixer();
        var soft = new SoundDirector(softMixer, bank) { Soft = true };
        soft.Begin();
        soft.Update(0, Tick(cues: [new(CueKind.Captured, Station.Beside)]), street, lamps, false);
        soft.Update(1.9, Tick(), street, lamps, false);
        director.Begin();
        director.Update(0, Tick(cues: [new(CueKind.Captured, Station.Beside)]), street, lamps, false);
        director.Update(1.9, Tick(), street, lamps, false);
        double Loud(Mixer m, string sound) => m.Voices().Where(v => v.Sound == sound).Select(v => v.Volume).DefaultIfEmpty(0).Max();
        t.Check(Loud(softMixer, "sting") > 0 && Loud(softMixer, "sting") < Loud(mixer, "sting") * 0.5 && Loud(softMixer, "sack") < Loud(mixer, "sack"), "soft scares: a quieter sting and sack");
    }

    static SessionTick Tick(IReadOnlyList<ClockEvent>? events = null, IReadOnlyList<CreatureCue>? cues = null) => new(events ?? [], cues ?? []);
}
