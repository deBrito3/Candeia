using static Candeia.Audio.Synth;

namespace Candeia.Audio;

// The score: original music made by the game, to sit low under the house and the system.
//   Calm: 48 seconds that loop. A soft low pad moves through D harmonic minor (Dm, B♭, Gm, A), over a
//         drone on D; a music box plays a few slow notes, with echoes, alternating left and right.
//   Tension: 24 seconds that loop, faded in as he comes closer. A tritone beating in the bass, a high
//         bowed scrape that swells and fades, and a slow, muffled pulse.
static class Score
{
    const double ChordSeconds = 12, Overlap = 2.5;

    // Each chord's notes in hertz: Dm, B♭, Gm, A (the A major of harmonic minor, the one that unsettles).
    static readonly double[][] Chords =
    [
        [146.83, 174.61, 220.00],
        [116.54, 146.83, 174.61],
        [98.00, 116.54, 146.83],
        [110.00, 138.59, 164.81],
    ];

    // The music box: (when, pitch). Notes of each chord's moment, sparse and slow.
    static readonly (double At, double Hz)[] Box =
    [
        (2.0, 587.33), (5.5, 698.46), (7.2, 880.00),
        (14.0, 932.33), (17.5, 698.46), (19.3, 587.33),
        (26.0, 783.99), (29.5, 932.33), (31.4, 587.33),
        (38.0, 1108.73), (41.5, 880.00), (43.4, 659.26),
    ];

    public static Sound Calm()
    {
        float[] Side(bool right)
        {
            double detune = right ? 1.0031 : 0.9972;
            var pad = new LowPass(700);
            var buffer = Render(4 * ChordSeconds + Overlap, (i, t) =>
            {
                // The pad: three soft triangle voices per note, the chord fading into the next.
                double chords = 0;
                for (int c = 0; c < Chords.Length + 1; c++)
                {
                    double start = c * ChordSeconds - Overlap / 2;
                    double local = t - start;
                    if (local < 0 || local > ChordSeconds + Overlap) continue;
                    double level = Math.Min(1, local / 3) * Math.Min(1, (ChordSeconds + Overlap - local) / 3);
                    foreach (double hz in Chords[c % Chords.Length])
                        chords += level * (Triangle(hz * t) + Triangle(hz * detune * t) * 0.7 + Triangle(hz * 2.003 * t) * 0.15);
                }
                pad.Cutoff(450 + 350 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 16)));
                float bed = pad.Process((float)(chords * 0.12));
                // The drone on D, breathing slowly.
                float drone = (float)((Sine(73.42 * t) * 0.5 + Sine(73.42 * detune * t) * 0.3) * (0.8 + 0.2 * Math.Sin(2 * Math.PI * t / 8)));
                // The music box, every other note on this side, the rest as an echo.
                float box = 0;
                for (int n = 0; n < Box.Length; n++)
                {
                    bool near = (n % 2 == 0) != right;
                    foreach (var (delay, gain) in new[] { (0.0, near ? 1.0 : 0.35), (0.41, 0.35), (0.93, 0.18) })
                    {
                        double local = t - Box[n].At - delay;
                        if (local < 0 || local > 3.5) continue;
                        box += (float)(gain * Bell(local, Box[n].Hz) * Env(local, 0.004, 2.2));
                    }
                }
                return bed + drone * 0.22f + box * 0.06f;
            });
            return Normalize(Seamless(buffer, Overlap), 0.45f);
        }
        return new("score", Side(false), Side(true), loop: true);
    }

    public static Sound Tension()
    {
        float[] Side(int seed, bool right)
        {
            var noise = new Noise(seed);
            var bow = new Resonator(right ? 2300 : 2050, 12);
            var thump = new LowPass(120);
            var buffer = Render(25.5, (i, t) =>
            {
                // D against A♭: the tritone, beating.
                float low = (float)(Sine(73.42 * t) * 0.5 + Sine(103.83 * t) * 0.45 + Sine(103.83 * 1.004 * t) * 0.25);
                // A bowed scrape high up, swelling every six seconds.
                double swell = Math.Pow(Math.Max(0, Math.Sin(Math.PI * t / 6 + (right ? 0.8 : 0))), 3);
                float scrape = bow.Process(noise.Next()) * (float)swell;
                // A slow, muffled pulse, like a heart behind a wall.
                double beat = t % 1.5;
                float pulse = thump.Process((float)(Sine(52 * t) * (Env(beat, 0.01, 0.12) + 0.6 * Env(beat - 0.28, 0.01, 0.12))));
                return low * 0.35f + scrape * 0.9f + pulse * 0.8f;
            });
            return Normalize(Seamless(buffer, 1.5), 0.45f);
        }
        return new("score-tension", Side(41, false), Side(42, true), loop: true);
    }

    static double Triangle(double phase)
    {
        double p = phase - Math.Floor(phase);
        return 4 * Math.Abs(p - 0.5) - 1;
    }
}
