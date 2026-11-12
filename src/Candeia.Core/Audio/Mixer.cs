namespace Candeia.Audio;

// The groups of sounds, each with its own volume: the house's ambience, the CANDEIA system, him, the radio
// (kept for later), the interface and the score.
public enum Channel { House, System, Creature, Radio, Interface, Music }

// What a voice is doing, for the tests and the developer tools.
public sealed record VoiceInfo(int Id, string Sound, Channel Channel, double Volume, double Pan, double Rate, double Distance, bool Loop, bool Stopping);

// Mixes many voices into one stereo stream, without knowing anything about the speakers: the player pulls
// samples from Mix on its own thread, the game starts and changes voices from its thread. Each voice has a
// volume, a place left to right, a speed, and a distance that dulls it; every change glides instead of
// jumping, so nothing clicks. A soft limiter on the sum keeps the stream below full scale.
public sealed class Mixer
{
    const double Glide = 0.05; // seconds for a change of volume, place or speed when none is given

    readonly object gate = new();
    readonly List<Voice> voices = [];
    readonly Ramp[] channels = Enum.GetValues<Channel>().Select(_ => new Ramp(1)).ToArray();
    int nextId = 1;

    public Mixer()
    {
        SetChannel(Channel.House, 0.55, 0);
        SetChannel(Channel.System, 0.6, 0);
        SetChannel(Channel.Creature, 1.0, 0);
        SetChannel(Channel.Radio, 0.6, 0);
        SetChannel(Channel.Interface, 0.6, 0);
        // The score stays low, under the house and the system.
        SetChannel(Channel.Music, 0.4, 0);
    }

    public double Master { get; set; } = 0.85;

    public int Play(Sound sound, Channel channel, double volume = 1, double pan = 0, double rate = 1, double distance = 0, bool? loop = null, double fadeIn = 0)
    {
        var voice = new Voice(sound, channel, loop ?? sound.Loop)
        {
            Volume = new Ramp(fadeIn > 0 ? 0 : volume),
            Pan = new Ramp(pan),
            Rate = new Ramp(rate),
            Distance = new Ramp(distance),
        };
        if (fadeIn > 0) voice.Volume.To(volume, fadeIn);
        lock (gate)
        {
            voice.Id = nextId++;
            voices.Add(voice);
        }
        return voice.Id;
    }

    // Changes a voice; each value glides to its new level over `seconds`.
    public void Set(int id, double? volume = null, double? pan = null, double? rate = null, double? distance = null, double seconds = Glide)
    {
        lock (gate)
        {
            var voice = voices.Find(v => v.Id == id);
            if (voice is null) return;
            if (volume is { } v) voice.Volume.To(v, seconds);
            if (pan is { } p) voice.Pan.To(p, seconds);
            if (rate is { } r) voice.Rate.To(r, seconds);
            if (distance is { } d) voice.Distance.To(d, seconds);
        }
    }

    // Fades a voice out and lets it go.
    public void Stop(int id, double fadeOut = Glide)
    {
        lock (gate)
        {
            var voice = voices.Find(v => v.Id == id);
            if (voice is null) return;
            voice.Volume.To(0, fadeOut);
            voice.Stopping = true;
        }
    }

    public void StopAll(double fadeOut = Glide)
    {
        lock (gate)
            foreach (var voice in voices)
            {
                voice.Volume.To(0, fadeOut);
                voice.Stopping = true;
            }
    }

    public void SetChannel(Channel channel, double volume, double seconds = Glide)
    {
        lock (gate) channels[(int)channel].To(volume, seconds);
    }

    public double ChannelVolume(Channel channel)
    {
        lock (gate) return channels[(int)channel].Target;
    }

    public bool Playing(int id)
    {
        lock (gate) return voices.Exists(v => v.Id == id);
    }

    public IReadOnlyList<VoiceInfo> Voices()
    {
        lock (gate)
            return voices.Select(v => new VoiceInfo(v.Id, v.Sound.Name, v.Channel, v.Volume.Target, v.Pan.Target, v.Rate.Target, v.Distance.Target, v.Loop, v.Stopping)).ToList();
    }

    // Fills `stereo` (left, right, left, right…) with the next frames of every voice.
    public void Mix(Span<float> stereo)
    {
        stereo.Clear();
        int frames = stereo.Length / 2;
        lock (gate)
        {
            for (int c = 0; c < channels.Length; c++) channels[c].Prepare(frames);
            foreach (var voice in voices) voice.Render(stereo, frames, channels[(int)voice.Channel]);
            voices.RemoveAll(v => v.Finished);
            foreach (var channel in channels) channel.Advance(frames);
        }
        float master = (float)Master;
        for (int i = 0; i < stereo.Length; i++) stereo[i] = Limit(stereo[i] * master);
    }

    // Linear below 0.8, then bending smoothly towards 0.98, never past it.
    static float Limit(float x)
    {
        float a = Math.Abs(x);
        if (a <= 0.8f) return x;
        float bent = 0.8f + 0.18f * (float)Math.Tanh((a - 0.8f) / 0.18f);
        return Math.Sign(x) * bent;
    }

    // A value that glides to a target over a number of frames.
    sealed class Ramp(double value)
    {
        double current = value, target = value, step;
        double atStart;

        public double Target => target;
        public double Current => current;
        public bool Moving => step != 0;

        public void To(double value, double seconds)
        {
            target = value;
            int frames = (int)(seconds * Synth.Rate);
            step = frames <= 0 ? 0 : (target - current) / frames;
            if (frames <= 0) current = target;
        }

        // The value at frame `n` of the next block, and moving on after the block.
        public void Prepare(int frames) => atStart = current;
        public double At(int n) => step == 0 ? current : step > 0 ? Math.Min(target, atStart + step * n) : Math.Max(target, atStart + step * n);

        public void Advance(int frames)
        {
            if (step == 0) return;
            current = At(frames);
            if (current == target) step = 0;
        }
    }

    sealed class Voice(Sound sound, Channel channel, bool loop)
    {
        public int Id;
        public Sound Sound = sound;
        public Channel Channel = channel;
        public bool Loop = loop, Stopping, Finished;
        public Ramp Volume = new(1), Pan = new(0), Rate = new(1), Distance = new(0);
        double position;
        float lowLeft, lowRight;

        public void Render(Span<float> stereo, int frames, Ramp channel)
        {
            Volume.Prepare(frames);
            Pan.Prepare(frames);
            Rate.Prepare(frames);
            Distance.Prepare(frames);
            int length = Sound.Frames;
            // Distance dulls the sound: a low-pass from the full band down to about 700 Hz far away.
            double far = Math.Clamp(Distance.Current, 0, 1);
            float a = (float)(1 - Math.Exp(-2 * Math.PI * (18000 * Math.Pow(700 / 18000.0, far)) / Synth.Rate));
            // The place left to right only needs its sine and cosine per sample while it glides.
            double still = (Math.Clamp(Pan.Current, -1, 1) + 1) * Math.PI / 4;
            float leftGain = (float)Math.Cos(still), rightGain = (float)Math.Sin(still);
            bool panning = Pan.Moving;
            for (int n = 0; n < frames; n++)
            {
                if (position >= length)
                {
                    if (!Loop) { Finished = true; break; }
                    position -= length;
                }
                int i = (int)position;
                float frac = (float)(position - i);
                int j = i + 1 < length ? i + 1 : Loop ? 0 : i;
                float left = Sound.Left[i] + (Sound.Left[j] - Sound.Left[i]) * frac;
                float right = Sound.Right[i] + (Sound.Right[j] - Sound.Right[i]) * frac;
                lowLeft += a * (left - lowLeft);
                lowRight += a * (right - lowRight);
                if (panning)
                {
                    double angle = (Math.Clamp(Pan.At(n), -1, 1) + 1) * Math.PI / 4;
                    leftGain = (float)Math.Cos(angle);
                    rightGain = (float)Math.Sin(angle);
                }
                float gain = (float)(Volume.At(n) * channel.At(n));
                stereo[n * 2] += lowLeft * gain * leftGain;
                stereo[n * 2 + 1] += lowRight * gain * rightGain;
                position += Math.Max(0.05, Rate.At(n));
            }
            Volume.Advance(frames);
            Pan.Advance(frames);
            Rate.Advance(frames);
            Distance.Advance(frames);
            if (Stopping && Volume.Current <= 0.0001) Finished = true;
        }
    }
}
