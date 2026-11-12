using static Candeia.Audio.Synth;

namespace Candeia.Audio;

// Every sound of the night, made by the game itself: the house, the old computer and him. Each one is made
// once, the first time it is asked for, and kept.
public sealed class SoundBank
{
    public static readonly string[] Names =
    [
        // The score, low under everything, and its tense layer for when he is near.
        "score", "score-tension",
        // The house.
        "rain", "crickets", "creak",
        // The CANDEIA system.
        "key1", "key2", "key3", "relay", "lamp-on", "lamp-out", "pass", "error", "surge",
        // Him.
        "cane", "gate", "psiu", "steps-grass", "steps-wood", "steps-away", "wrapper", "breath", "cough", "door", "thunder", "candle-out", "sack",
    ];

    // Each sound is made once, on its own: asking for a click never waits for the thunder being made in
    // the background.
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Sound>> made = new();

    public Sound this[string name] => made.GetOrAdd(name, n => new Lazy<Sound>(() => Make(n))).Value;

    // Whether a sound is already made, so asking for it costs nothing.
    public bool Ready(string name) => made.TryGetValue(name, out var sound) && sound.IsValueCreated;

    // Makes every sound now, the score first (the night's introduction calls this in the background).
    public void Prepare()
    {
        foreach (var name in Names) _ = this[name];
    }

    static Sound Make(string name) => name switch
    {
        "score" => Score.Calm(),
        "score-tension" => Score.Tension(),
        "rain" => Rain(),
        "crickets" => Crickets(),
        "creak" => new(name, Edges(Normalize(Creak(0.9, 520, 7), 0.8f))),
        "key1" => new(name, Edges(Normalize(Key(1, 1.0), 0.6f))),
        "key2" => new(name, Edges(Normalize(Key(2, 0.9), 0.6f))),
        "key3" => new(name, Edges(Normalize(Key(3, 1.1), 0.6f))),
        "relay" => new(name, Edges(Normalize(Relay(), 0.7f))),
        "lamp-on" => new(name, Edges(Normalize(LampOn(), 0.7f))),
        "lamp-out" => new(name, Edges(Normalize(LampOut(), 0.7f))),
        "pass" => new(name, Edges(Normalize(Pass(), 0.7f))),
        "error" => new(name, Edges(Normalize(Error(), 0.6f))),
        "surge" => new(name, Edges(Normalize(Surge(), 0.7f))),
        "cane" => new(name, Edges(Normalize(Cane(), 0.85f))),
        "gate" => new(name, Edges(Normalize(Gate(), 0.85f))),
        "psiu" => new(name, Edges(Normalize(Psiu(), 0.7f))),
        "steps-grass" => new(name, Edges(Normalize(Steps(4, 0.45, 1200, 320, 21, 1), 0.8f))),
        "steps-wood" => new(name, Edges(Normalize(Steps(3, 0.6, 700, 90, 22, 1), 0.85f))),
        "steps-away" => new(name, Edges(Normalize(Steps(4, 0.62, 650, 90, 23, -1), 0.8f))),
        "wrapper" => new(name, Edges(Normalize(Wrapper(), 0.85f))),
        "breath" => new(name, Normalize(Breath(), 0.7f), loop: true),
        "cough" => new(name, Edges(Normalize(Cough(), 0.8f))),
        "door" => new(name, Edges(Normalize(Creak(1.3, 380, 31), 0.8f))),
        "thunder" => new(name, Edges(Normalize(Thunder(), 0.9f), 0.05)),
        "candle-out" => new(name, Edges(Normalize(CandleOut(), 0.7f))),
        "sack" => new(name, Edges(Normalize(Sack(), 0.85f))),
        _ => throw new ArgumentException($"No sound named {name}.", nameof(name)),
    };

    // Rain on the colonial roof, heard from inside: a soft, dark wash that swells and settles slowly, with
    // no drops or clicks in it. Darker than a hiss on purpose: high, steady noise tires the ear.
    static Sound Rain()
    {
        float[] Side(int seed, double swell)
        {
            var noise = new Noise(seed);
            var low = new LowPass(800);
            var lower = new LowPass(1200);
            var high = new HighPass(90);
            var buffer = Render(12.5, (i, t) =>
            {
                float wash = high.Process(low.Process(lower.Process(noise.Next())));
                return wash * (0.8f + 0.2f * Sine(t / 12.0 + swell));
            });
            return Normalize(Seamless(buffer, 0.5), 0.3f);
        }
        return new("rain", Side(1, 0), Side(2, 0.5), loop: true);
    }

    // Crickets on both sides, and now and then a frog.
    static Sound Crickets()
    {
        float[] Side(int seed, double offset)
        {
            var noise = new Noise(seed);
            var frog = new Resonator(190, 6);
            double nextChirp = offset, nextCroak = 1.5 + offset * 3;
            var buffer = Render(8.5, (i, t) =>
            {
                if (t > nextChirp + 0.11) nextChirp += 0.55 + noise.Uniform() * 0.9;
                double c = t - nextChirp;
                float chirp = c >= 0 && c < 0.1 && c % 0.033 < 0.02 ? Sine(4400 * t) * Window(c % 0.033, 0.02, 0.003, 0.006) : 0;
                if (t > nextCroak + 0.35) nextCroak += 2.5 + noise.Uniform() * 3;
                double f = t - nextCroak;
                float croak = f >= 0 && f < 0.3 ? frog.Process(noise.Next() * (0.5f + 0.5f * Sine(28 * t))) * Window(f, 0.3, 0.02, 0.12) * 3 : frog.Process(0);
                return chirp * 0.35f + croak * 0.8f;
            });
            return Normalize(Seamless(buffer, 0.5), 0.35f);
        }
        return new("crickets", Side(4, 0.1), Side(5, 0.4), loop: true);
    }

    // Old wood under strain: a stick-slip train of pulses through the wood's resonance.
    static float[] Creak(double length, double body, int seed)
    {
        var noise = new Noise(seed);
        var wood = new Resonator(body, 9);
        double phase = 0;
        return Render(length, (i, t) =>
        {
            double rate = 28 + 30 * Math.Sin(Math.PI * t / length) + noise.Uniform() * 6;
            double before = phase;
            phase += rate / Rate;
            float pulse = Math.Floor(phase) != Math.Floor(before) ? 1 + (float)noise.Uniform() : 0;
            return wood.Process(pulse + noise.Next() * 0.02f) * Window(t, length, 0.08, 0.25);
        });
    }

    // A key of the old keyboard: a sharp click and a small thump.
    static float[] Key(int seed, double pitch)
    {
        var noise = new Noise(10 + seed);
        var click = new HighPass(2500);
        return Render(0.05, (i, t) => click.Process(noise.Next()) * Env(t, 0.0005, 0.004) + 0.6f * Sine(140 * pitch * t) * Env(t, 0.001, 0.02));
    }

    // F5: a relay closes, and the power hums for a moment.
    static float[] Relay()
    {
        var noise = new Noise(12);
        var click = new HighPass(1800);
        return Render(0.3, (i, t) =>
            click.Process(noise.Next()) * (Env(t, 0.0005, 0.006) + Env(t - 0.06, 0.0005, 0.006) * 0.7f)
            + 0.3f * Math.Sign(Sine(50 * t)) * Window(t - 0.05, 0.12, 0.01, 0.05));
    }

    // A lamp lights: a click and a bright tink.
    static float[] LampOn()
    {
        var noise = new Noise(13);
        var click = new HighPass(2000);
        return Render(0.6, (i, t) => click.Process(noise.Next()) * Env(t, 0.0005, 0.005) + 0.5f * Bell(t, 2600) * Env(t - 0.01, 0.002, 0.35) + 0.1f * Sine(100 * t) * Window(t, 0.6, 0.02, 0.3));
    }

    // A lamp stays dark: a dull thunk.
    static float[] LampOut()
    {
        var noise = new Noise(14);
        var low = new LowPass(600);
        return Render(0.4, (i, t) => Sine(90 * t) * Env(t, 0.002, 0.12) + low.Process(noise.Next()) * Env(t, 0.001, 0.03) * 0.8f);
    }

    // Every lamp lit: three rising bells.
    static float[] Pass() => Render(1.4, (i, t) =>
        Bell(t, 1047) * Env(t, 0.003, 0.5) + Bell(t, 1319) * Env(t - 0.12, 0.003, 0.5) + Bell(t, 1568) * Env(t - 0.24, 0.003, 0.7));

    // The code did not compile: a low, rough double buzz.
    static float[] Error() => Render(0.6, (i, t) =>
        (float)Math.Tanh(Sine(110 * t) * 4) * (Window(t, 0.2, 0.01, 0.04) + Window(t - 0.28, 0.25, 0.01, 0.08)));

    // The code froze: the power flickers, buzzing and crackling.
    static float[] Surge()
    {
        var noise = new Noise(15);
        var crackle = new HighPass(3000);
        return Render(1.2, (i, t) =>
        {
            float flutter = 0.5f + 0.5f * Sine(7 * t + 3 * Sine(1.3 * t));
            float spark = noise.Uniform() < 0.004 ? 1 : 0;
            return (0.6f * (float)Math.Tanh(Sine(50 * t) * 6) * flutter + crackle.Process(noise.Next() * spark * 4)) * Window(t, 1.2, 0.02, 0.4);
        });
    }

    // His cane on the cobblestones: a wooden tip striking stone, four times, unhurried.
    static float[] Cane()
    {
        var noise = new Noise(16);
        var tip = new Resonator(1800, 10);
        var stone = new Resonator(720, 14);
        double[] taps = [0.0, 0.42, 0.95, 1.36];
        return Render(1.7, (i, t) =>
        {
            float hit = 0;
            foreach (double at in taps) if (i == (int)(at * Rate)) hit = 4;
            float x = hit + noise.Next() * 0.01f;
            return tip.Process(x) * 0.6f + stone.Process(x) * 0.5f;
        });
    }

    // The iron gate: a squeal gliding up and down, and the latch at the end.
    static float[] Gate()
    {
        var noise = new Noise(17);
        var metal = new Resonator(1500, 20);
        double phase = 0;
        return Render(1.7, (i, t) =>
        {
            double rate = t < 1.2 ? 420 + 220 * Math.Sin(Math.PI * t / 1.2) + 30 * Math.Sin(13 * t) : 0;
            double before = phase;
            phase += rate / Rate;
            float pulse = Math.Floor(phase) != Math.Floor(before) ? 1 : 0;
            float squeal = metal.Process(pulse + noise.Next() * 0.05f) * Window(t, 1.2, 0.1, 0.3);
            float latch = (Sine(900 * t) + 0.6f * Sine(1410 * t) + 0.4f * Sine(2130 * t)) * Env(t - 1.3, 0.001, 0.25);
            return squeal + latch * 0.5f;
        });
    }

    // "Psiu": a hiss, then a breathy whistle going up and down.
    static float[] Psiu()
    {
        var noise = new Noise(18);
        var hiss = new HighPass(4000);
        var breathy = new LowPass(3000);
        double phase = 0;
        return Render(0.9, (i, t) =>
        {
            float s = hiss.Process(noise.Next()) * Window(t, 0.28, 0.03, 0.1);
            double f = t < 0.55 ? 900 + 600 * (t - 0.3) / 0.25 : 1500 - 400 * (t - 0.55) / 0.3;
            phase += Math.Max(0, f) / Rate;
            float whistle = (Sine(phase) * 0.7f + breathy.Process(noise.Next()) * 0.3f) * Window(t - 0.3, 0.55, 0.05, 0.2);
            return s * 0.8f + whistle * 0.6f;
        });
    }

    // Footsteps: soft and wet in the grass, heavy on the floorboards. Going away, each is softer and duller.
    static float[] Steps(int count, double gap, double brightness, double body, int seed, int direction)
    {
        var noise = new Noise(seed);
        var low = new LowPass(brightness);
        var thump = new Resonator(body, 4);
        var times = Enumerable.Range(0, count).Select(n => n * gap + noise.Uniform() * 0.06).ToArray();
        double length = times[^1] + 0.5;
        return Render(length, (i, t) =>
        {
            float sum = 0;
            for (int n = 0; n < times.Length; n++)
            {
                double s = t - times[n];
                if (s < 0 || s > 0.35) continue;
                float loud = direction >= 0 ? 0.75f + 0.25f * n / Math.Max(1, count - 1) : 1 - 0.7f * n / Math.Max(1, count - 1);
                sum += loud * (noise.Next() * Env(s, 0.005, 0.08) + (i == (int)(times[n] * Rate) ? 6 : 0));
            }
            if (direction < 0) low.Cutoff(Math.Max(250, brightness * (1 - t / length)));
            return low.Process(sum) * 0.8f + thump.Process(sum) * 0.6f;
        });
    }

    // The candy wrapper, his signature: cellophane crackling in bursts as it is slowly unwrapped.
    static float[] Wrapper()
    {
        var noise = new Noise(19);
        var crisp = new HighPass(2500);
        var shine = new Resonator(5200, 3);
        return Render(1.6, (i, t) =>
        {
            double activity = 0.3 + 0.7 * Math.Pow(Math.Max(0, Math.Sin(Math.PI * t / 0.4)), 2);
            float crack = noise.Uniform() < 260 * activity / Rate ? (float)(0.3 + noise.Uniform()) * 6 : 0;
            float x = crisp.Process(crack + noise.Next() * 0.03f);
            return (x * 0.7f + shine.Process(x) * 0.4f) * Window(t, 1.6, 0.02, 0.3);
        });
    }

    // His breathing, slow and wet: an inhale, a pause, a rasping exhale.
    static float[] Breath()
    {
        var noise = new Noise(20);
        var inhale = new Resonator(1300, 2);
        var exhale = new LowPass(900);
        var buffer = Render(4.2, (i, t) =>
        {
            float a = inhale.Process(noise.Next()) * Window(t, 1.4, 0.6, 0.4) * 0.8f;
            float rasp = 0.6f + 0.4f * Sine(32 * t);
            float b = exhale.Process(noise.Next()) * rasp * Window(t - 1.7, 1.8, 0.2, 1.0) * 1.4f;
            return a + b;
        });
        return Seamless(buffer, 0.2);
    }

    // A dry cough, twice.
    static float[] Cough()
    {
        var noise = new Noise(24);
        var throat = new Resonator(360, 3);
        var air = new LowPass(1600);
        return Render(1.0, (i, t) =>
        {
            float shape = Env(t, 0.008, 0.16) + 0.6f * Env(t - 0.32, 0.008, 0.14);
            float x = air.Process(noise.Next()) * shape;
            return x * 0.6f + throat.Process(x) * 0.8f;
        });
    }

    // Thunder after the flash: a crack, then a long rumble.
    static float[] Thunder()
    {
        var noise = new Noise(25);
        var rumble = new LowPass(160);
        var crack = new HighPass(1500);
        float brown = 0;
        return Render(4.0, (i, t) =>
        {
            brown = Math.Clamp(brown + noise.Next() * 0.02f, -1, 1) * 0.999f;
            float roll = rumble.Process(brown) * (0.6f + 0.4f * Sine(1.7 * t + 2 * Sine(0.6 * t)));
            return crack.Process(noise.Next()) * Env(t, 0.005, 0.15) * 0.5f + roll * 8 * Window(t, 4.0, 0.15, 2.5);
        });
    }

    // A candle blown out: a short whoosh falling in pitch.
    static float[] CandleOut()
    {
        var noise = new Noise(26);
        var band = new Resonator(2000, 2);
        return Render(0.6, (i, t) =>
        {
            band.Tune(2000 - 1500 * t / 0.6);
            return band.Process(noise.Next()) * Window(t, 0.6, 0.03, 0.35);
        });
    }

    // The sack: cloth rustling, then closing with a thud.
    static float[] Sack()
    {
        var noise = new Noise(27);
        var cloth = new LowPass(2000);
        return Render(1.4, (i, t) =>
            cloth.Process(noise.Next()) * (0.5f + 0.5f * Sine(8 * t)) * Window(t, 1.0, 0.05, 0.2)
            + Sine(65 * t) * Env(t - 1.0, 0.003, 0.25) * 1.2f);
    }
}
