using Candeia.Audio;
using Candeia.Creature;
using Candeia.Night;

namespace Candeia.Tests;

// Writes every sound to a WAV file, and a 40-second night mixed by the real director, so the sounds can be
// heard without playing: --sounds <folder>.
static class SoundFiles
{
    public static int Run(string folder, TextWriter output)
    {
        Directory.CreateDirectory(folder);
        var bank = new SoundBank();
        bank.Prepare();
        foreach (var name in SoundBank.Names)
        {
            var sound = bank[name];
            var stereo = new float[sound.Frames * 2];
            for (int i = 0; i < sound.Frames; i++)
            {
                stereo[i * 2] = sound.Left[i];
                stereo[i * 2 + 1] = sound.Right[i];
            }
            // Loops are written twice over, so the seam can be heard.
            if (sound.Loop) stereo = [.. stereo, .. stereo];
            Write(Path.Combine(folder, name + ".wav"), stereo);
        }
        Write(Path.Combine(folder, "noite.wav"), Night(bank, out var timeline));
        File.WriteAllLines(Path.Combine(folder, "noite.txt"), timeline);
        output.WriteLine($"{SoundBank.Names.Length} sounds and noite.wav written to {Path.GetFullPath(folder)}");
        foreach (var line in timeline) output.WriteLine(line);
        return 0;
    }

    // A scripted night: he comes from the street to beside you, the player gets a compile error and a run
    // with one lamp, the house creaks, and he is pushed back.
    static float[] Night(SoundBank bank, out List<string> timeline)
    {
        var mixer = new Mixer();
        var director = new SoundDirector(mixer, bank);
        var lines = new List<string>();
        Station station = Station.Street;
        IReadOnlyList<LampState> shown = Lamps(0);
        (double At, string What, Action<double> Act)[] script =
        [
            (0, "a casa à noite: chuva baixa no telhado e grilos", _ => director.Begin()),
            (3, "ele se aproxima do portão", now => Cue(now, CueKind.Approaching, Station.Gate)),
            (5, "chega ao portão (e o “psiu”)", now => Cue(now, CueKind.Arrived, Station.Gate)),
            (9, "erro de compilação: os grilos se calam", now => Cue(now, CueKind.Listened, Station.Gate)),
            (12, "F5 e as lâmpadas", _ => { }),
            (15, "nenhuma lâmpada nova: a casa estala", now => director.Update(now, new([new(ClockEventKind.NoProgressCost, -7)], []), State(Station.Gate), shown, false)),
            (17, "chega ao quintal", now => Cue(now, CueKind.Arrived, Station.Yard)),
            (20, "se aproxima da janela", now => Cue(now, CueKind.Approaching, Station.Window)),
            (22, "na janela: papel de bala", now => Cue(now, CueKind.Arrived, Station.Window)),
            (25, "na porta: a porta range, a respiração", now => Cue(now, CueKind.Arrived, Station.Door)),
            (30, "ao seu lado", now => Cue(now, CueKind.Arrived, Station.Beside)),
            (35, "uma lâmpada nova: ele recua", now => Cue(now, CueKind.Retreated, Station.Street, Station.Beside)),
        ];
        void Cue(double now, CueKind kind, Station where, Station? from = null)
        {
            if (kind is CueKind.Arrived or CueKind.Retreated) station = where;
            director.Update(now, new([], [new CreatureCue(kind, where, from)]), State(station), shown, false);
        }
        CreatureState State(Station s) => new(s, 0.5, true, (int)s / 5.0);
        IReadOnlyList<LampState> Lamps(int lit) => Enumerable.Range(0, 4).Select(i => i < lit ? LampState.Lit : i < 4 && lit > 0 ? LampState.Out : LampState.Unknown).ToArray();

        const int block = 1102;
        const double seconds = 40;
        var all = new List<float>();
        var buffer = new float[block * 2];
        int next = 0;
        for (int frame = 0; frame < seconds * Synth.Rate; frame += block)
        {
            double now = frame / (double)Synth.Rate;
            while (next < script.Length && script[next].At <= now)
            {
                lines.Add($"{script[next].At,4:0}s  {script[next].What}");
                script[next].Act(now);
                next++;
            }
            // The run of 12 s: busy, then one lamp lit and three dark, one by one.
            bool busy = now is >= 12 and < 14;
            int reveal = now < 12.5 ? 0 : Math.Min(4, (int)((now - 12.5) / 0.35) + 1);
            var lamps = Enumerable.Range(0, 4).Select(i => i >= reveal ? LampState.Unknown : i == 0 ? LampState.Lit : LampState.Out).ToArray();
            shown = now >= 12 ? lamps : Lamps(0);
            director.Update(now, new([], []), State(station), shown, busy);
            mixer.Mix(buffer);
            all.AddRange(buffer);
        }
        timeline = lines;
        return [.. all];
    }

    // 16-bit stereo PCM at the mixer's rate.
    static void Write(string path, float[] stereo)
    {
        using var file = new BinaryWriter(File.Create(path));
        int bytes = stereo.Length * 2;
        file.Write("RIFF"u8);
        file.Write(36 + bytes);
        file.Write("WAVEfmt "u8);
        file.Write(16);
        file.Write((short)1);
        file.Write((short)2);
        file.Write(Synth.Rate);
        file.Write(Synth.Rate * 4);
        file.Write((short)4);
        file.Write((short)16);
        file.Write("data"u8);
        file.Write(bytes);
        foreach (float s in stereo) file.Write((short)Math.Clamp(Math.Round(s * 32767), -32768, 32767));
    }
}
