namespace Candeia.Audio;

// A sound ready to play: one buffer per side at the mixer's rate. A mono sound uses the same buffer for
// both sides. Loop sounds join their end to their start without a click.
public sealed class Sound
{
    public Sound(string name, float[] left, float[]? right = null, bool loop = false)
    {
        Name = name;
        Left = left;
        Right = right ?? left;
        Loop = loop;
    }

    public string Name { get; }
    public float[] Left { get; }
    public float[] Right { get; }
    public bool Loop { get; }
    public int Frames => Left.Length;
    public double Seconds => Frames / (double)Synth.Rate;
}

// The building blocks of every sound, partly from Projeto Aurora's SoundEffects: oscillators, envelopes,
// noise and simple filters. Everything is deterministic: a sound made twice is the same sound.
public static class Synth
{
    public const int Rate = 44100;

    public static float Sine(double phase) => (float)Math.Sin(phase * 2 * Math.PI);

    // Three slightly inharmonic partials: a small bell.
    public static float Bell(double t, double frequency) => Sine(frequency * t) + 0.35f * Sine(2.01 * frequency * t) + 0.12f * Sine(3.02 * frequency * t);

    // Attack, then an exponential decay that falls to a tenth over `release` seconds.
    public static float Env(double t, double attack, double release)
    {
        if (t < 0) return 0;
        double up = attack <= 0 ? 1 : Math.Min(1, t / attack);
        return (float)(up * Math.Exp(-Math.Max(0, t - attack) / Math.Max(0.001, release) * 2.3));
    }

    // Rises over `attack`, holds, and falls over `release` to zero at `length`.
    public static float Window(double t, double length, double attack, double release)
    {
        if (t < 0 || t > length) return 0;
        double up = attack <= 0 ? 1 : Math.Min(1, t / attack);
        double down = release <= 0 ? 1 : Math.Min(1, (length - t) / release);
        return (float)(up * down);
    }

    public static float[] Render(double seconds, Func<int, double, float> sample)
    {
        var buffer = new float[(int)(seconds * Rate)];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = sample(i, i / (double)Rate);
        return buffer;
    }

    // Scales the buffer so its loudest sample is `peak`.
    public static float[] Normalize(float[] buffer, float peak)
    {
        float max = 0;
        foreach (float s in buffer) max = Math.Max(max, Math.Abs(s));
        if (max > 0) for (int i = 0; i < buffer.Length; i++) buffer[i] *= peak / max;
        return buffer;
    }

    // Short fades at both ends, so a one-shot never starts or stops with a click.
    public static float[] Edges(float[] buffer, double seconds = 0.004)
    {
        int fade = Math.Min(buffer.Length / 4, (int)(seconds * Rate));
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            buffer[i] *= k;
            buffer[^(i + 1)] *= k;
        }
        return buffer;
    }

    // Makes a loop: the last `seconds` are faded into the start, and the result is that much shorter, so
    // playing it over and over has no seam.
    public static float[] Seamless(float[] buffer, double seconds)
    {
        int fade = Math.Min(buffer.Length / 2, (int)(seconds * Rate));
        int length = buffer.Length - fade;
        var loop = new float[length];
        Array.Copy(buffer, loop, length);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            loop[i] = buffer[i] * k + buffer[length + i] * (1 - k);
        }
        return loop;
    }

    // White noise from a seed.
    public sealed class Noise(int seed)
    {
        readonly Random random = new(seed);
        public float Next() => (float)(random.NextDouble() * 2 - 1);
        public double Uniform() => random.NextDouble();
    }

    // One-pole low-pass filter: lets through what is below the cutoff.
    public struct LowPass(double cutoff)
    {
        float a = (float)(1 - Math.Exp(-2 * Math.PI * cutoff / Rate)), y;
        public float Process(float x) => y += a * (x - y);
        public void Cutoff(double value) => a = (float)(1 - Math.Exp(-2 * Math.PI * value / Rate));
    }

    // One-pole high-pass filter: lets through what is above the cutoff.
    public struct HighPass(double cutoff)
    {
        LowPass low = new(cutoff);
        public float Process(float x) => x - low.Process(x);
    }

    // A resonant band-pass (a two-pole state-variable filter), for creaks, knocks and squeals.
    public struct Resonator(double frequency, double q)
    {
        float f = (float)(2 * Math.Sin(Math.PI * Math.Min(frequency, Rate / 6.0) / Rate)), damp = (float)(1 / q), low, band;
        public float Process(float x)
        {
            low += f * band;
            float high = x - low - damp * band;
            band += f * high;
            return band;
        }
        public void Tune(double value) => f = (float)(2 * Math.Sin(Math.PI * Math.Min(value, Rate / 6.0) / Rate));
    }
}
