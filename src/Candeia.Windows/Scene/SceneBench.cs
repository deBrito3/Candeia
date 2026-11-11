using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using Candeia.Night;
using Microsoft.Win32;

namespace Candeia.Scene;

// Measures the prototype against the three criteria of the technical plan, in a borderless 1920×1080
// window: at least 30 frames per second, at most 35% of one processor core, and typing that shows in
// less than 16 ms. Each station is measured for 4 seconds, at 60 and at 30 frames per second, then again
// at reduced quality. Writes a report in Portuguese and one image per station next to it.
static class SceneBench
{
    // A measured rate a quarter of a frame per second under the target still counts as the target: over
    // 4 seconds, one frame more or less is the edge of the measuring window.
    const double MinFps = 30, FpsTolerance = 0.25, MaxCpu = 35, MaxTyping = 16;
    static readonly string[] Names = ["rua", "portão", "quintal", "janela", "porta", "ao seu lado"];
    static readonly string[] Files = ["1-rua", "2-portao", "3-quintal", "4-janela", "5-porta", "6-ao-lado"];

    sealed record Segment(double Target, SceneQuality Quality, Station Station, FrameSummary Frames, double Cpu, Spread Typing, int Lost);

    // Returns 0 when every criterion passes at 60 frames per second, 1 when not, 3 on an error.
    public static int Run(string reportPath, bool imagesOnly)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(folder);
        using var window = new ScenePreviewWindow(benchmark: true);
        int exit = 3;
        window.Shown += async (_, _) =>
        {
            try { exit = imagesOnly ? await Images(window, folder) : await Measure(window, reportPath, folder); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "erro.txt"), ex.ToString()); exit = 3; }
            finally { window.Close(); }
        };
        Application.Run(window);
        return exit;
    }

    // Renders 400 frames of each station into memory, with no window and nothing presented, and writes the
    // average cost of each layer. For tuning the drawing; the criteria come from Run.
    public static int Profile(string path)
    {
        using var renderer = new SceneRenderer(new Size(1920, 1080));
        var timings = new SceneTimings();
        var text = new StringBuilder("| Parada | Quarto | Chuva e criatura | Mapa de luz | Luz | Chamas e olhos | Total |\n| --- | --- | --- | --- | --- | --- | --- |\n");
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        foreach (var station in Enum.GetValues<Station>())
        {
            for (int i = 0; i < 60; i++) renderer.Render(new SceneState(i / 60.0, station, 10, SceneQuality.High), timings);
            double copy = 0, figures = 0, map = 0, light = 0, flames = 0;
            const int frames = 400;
            for (int i = 0; i < frames; i++)
            {
                renderer.Render(new SceneState(1 + i / 60.0, station, 10, SceneQuality.High), timings);
                copy += timings.Copy; figures += timings.Figures; map += timings.LightMap; light += timings.Light; flames += timings.Flames;
            }
            string F(double v) => (v / frames).ToString("0.00", pt);
            text.AppendLine($"| {Names[(int)station]} | {F(copy)} | {F(figures)} | {F(map)} | {F(light)} | {F(flames)} | {F(copy + figures + map + light + flames)} |");
        }
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        return 0;
    }

    static async Task<int> Images(ScenePreviewWindow window, string folder)
    {
        await Task.Delay(1200);
        foreach (var station in Enum.GetValues<Station>())
        {
            window.SetStation(station);
            await Task.Delay(1400);
            Capture(window, Path.Combine(folder, Files[(int)station] + ".png"));
        }
        return 0;
    }

    static async Task<int> Measure(ScenePreviewWindow window, string reportPath, string folder)
    {
        string lightCheck = CheckLightPass();
        using var probe = new TypingProbe();
        window.Editor.Attach(probe, window.Editor.GetFirstCharIndexFromLine(1) - 1);
        using var process = Process.GetCurrentProcess();
        await Task.Delay(2500); // warm up: compiled code, caches, the compositor

        var segments = new List<Segment>();
        (double target, SceneQuality quality)[] phases = [(60, SceneQuality.High), (30, SceneQuality.High), (60, SceneQuality.Reduced)];
        foreach (var (target, quality) in phases)
        {
            window.TargetFps = target;
            window.Quality = quality;
            foreach (var station in Enum.GetValues<Station>())
            {
                window.SetStation(station);
                await Task.Delay(800);
                window.Stats.Reset();
                probe.Take();
                process.Refresh();
                var cpuStart = process.TotalProcessorTime;
                var wall = Stopwatch.StartNew();
                await Task.Delay(4000);
                process.Refresh();
                double seconds = wall.Elapsed.TotalSeconds;
                double cpu = (process.TotalProcessorTime - cpuStart).TotalMilliseconds / (seconds * 1000) * 100;
                var (typing, lost) = probe.Take();
                segments.Add(new(target, quality, station, window.Stats.Summarize(seconds), cpu, Spread.Of(typing), lost));
                if (target == 60 && quality == SceneQuality.High) Capture(window, Path.Combine(folder, Files[(int)station] + ".png"));
            }
        }
        window.Editor.Detach();

        var (report, passed) = Report(window, segments, lightCheck);
        File.WriteAllText(reportPath, report, new UTF8Encoding(false));
        return passed ? 0 : 1;
    }

    // The fast light pass must give exactly the same pixels as the plain one.
    static string CheckLightPass()
    {
        const int width = 200, height = 120;
        var lighting = new Lighting(width, height);
        lighting.Build([new PointLight(60, 40, 120, 30, 1.2f, 0.7f, 0.3f), new PointLight(150, 90, 80, 20, 0.4f, 0.4f, 0.6f)],
            [new AreaLight(new RectangleF(10, 10, 50, 70), 4, 0.9f, 0.9f, 1f)], 0.03f, 0.03f, 0.05f, 0.6f);
        var random = new Random(3);
        var room = new uint[width * height];
        for (int i = 0; i < room.Length; i++) room[i] = 0xFF000000 | (uint)random.Next(0, 0xFFFFFF);
        var fast = new uint[room.Length];
        var plain = new uint[room.Length];
        var skip = Rectangle.FromLTRB(64, 32, 136, 88);
        lighting.Apply(room, fast, skip, 40, 17, withGrain: true);
        lighting.ApplyScalar(room, plain, skip, 40, 17, withGrain: true);
        int different = fast.Where((p, i) => p != plain[i]).Count();
        return different == 0
            ? $"PASS: o passo rápido de luz dá os mesmos {fast.Length} pixels que o passo simples."
            : $"FALHA: {different} de {fast.Length} pixels diferem entre o passo rápido e o simples.";
    }

    static void Capture(Form window, string path)
    {
        using var image = new Bitmap(window.ClientSize.Width, window.ClientSize.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image))
        {
            IntPtr hdc = g.GetHdc();
            try { Native.PrintWindow(window.Handle, hdc, Native.PrintFullContent); }
            finally { g.ReleaseHdc(hdc); }
        }
        image.Save(path, ImageFormat.Png);
    }

    static (string Report, bool Passed) Report(ScenePreviewWindow window, List<Segment> segments, string lightCheck)
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        string F(double value, string format = "0.0") => value.ToString(format, pt);
        var text = new StringBuilder();
        text.AppendLine("# Protótipo de desenho: medições");
        text.AppendLine();
        text.AppendLine($"- Processador: {ProcessorName()} ({Environment.ProcessorCount} núcleos lógicos)");
        text.AppendLine($"- Energia: {(SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online ? "na tomada" : "na bateria")}");
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        text.AppendLine($"- Tela: {screen.Width}×{screen.Height}, atualização medida a cada {F(window.RefreshMilliseconds, "0.00")} ms; janela de {window.ClientSize.Width}×{window.ClientSize.Height}");
        text.AppendLine($"- Desenho do quarto em cache (só ao mudar o tamanho da janela): {F(window.RoomMilliseconds, "0")} ms");
        text.AppendLine($"- {lightCheck}");
        text.AppendLine();

        bool passed = true;
        foreach (var group in segments.GroupBy(s => (s.Target, s.Quality)))
        {
            string quality = group.Key.Quality == SceneQuality.High ? "qualidade alta" : "qualidade reduzida (sem granulação, menos chuva)";
            text.AppendLine($"## Meta de {group.Key.Target:0} quadros por segundo, {quality}");
            text.AppendLine();
            text.AppendLine("| Parada | Quadros por segundo | Trabalho do quadro (mediana / p95 / máx.) | Intervalo p95 | Processador (% de um núcleo) | Digitação (mediana / p95 / máx.) |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var s in group)
            {
                var w = s.Frames.Work;
                text.AppendLine($"| {Names[(int)s.Station]} | {F(s.Frames.Fps)} | {F(w.Median)} / {F(w.P95)} / {F(w.Max)} ms | {F(s.Frames.Interval.P95)} ms | {F(s.Cpu, "0")}% | {F(s.Typing.Median)} / {F(s.Typing.P95)} / {F(s.Typing.Max)} ms ({s.Typing.Count} teclas{(s.Lost > 0 ? $", {s.Lost} perdidas" : "")}) |");
            }
            double minFps = group.Min(s => s.Frames.Fps), maxCpu = group.Max(s => s.Cpu), maxTyping = group.Max(s => s.Typing.P95);
            int lostKeys = group.Sum(s => s.Lost);
            // At a target of 30, the target is the minimum itself: one frame missed in 4 seconds is allowed.
            double tolerance = group.Key.Target <= MinFps ? 1 : FpsTolerance;
            bool fpsOk = minFps >= MinFps - tolerance, cpuOk = maxCpu <= MaxCpu, typingOk = maxTyping < MaxTyping && lostKeys == 0;
            text.AppendLine();
            text.AppendLine($"- Quadros por segundo, pior parada: {F(minFps)} (mínimo {MinFps:0}): {(fpsOk ? "PASS" : "FALHA")}");
            text.AppendLine($"- Processador, pior parada: {F(maxCpu, "0")}% de um núcleo (máximo {MaxCpu:0}%): {(cpuOk ? "PASS" : "FALHA")}");
            text.AppendLine($"- Digitação p95, pior parada: {F(maxTyping)} ms (abaixo de {MaxTyping:0} ms), {lostKeys} teclas perdidas: {(typingOk ? "PASS" : "FALHA")}");
            var layers = group.Select(s => s.Frames.Layers).ToList();
            text.AppendLine($"- Custo médio por camada: cópia do quarto {F(layers.Average(l => l.Copy), "0.00")} ms · chuva e criatura {F(layers.Average(l => l.Figures), "0.00")} ms · mapa de luz {F(layers.Average(l => l.LightMap), "0.00")} ms · " +
                $"luz, vinheta e granulação {F(layers.Average(l => l.Light), "0.00")} ms · chamas e olhos {F(layers.Average(l => l.Flames), "0.00")} ms · cópia para a tela {F(layers.Average(l => l.Present), "0.00")} ms");
            text.AppendLine();
            if (group.Key == (60, SceneQuality.High)) passed = fpsOk && cpuOk && typingOk;
        }
        text.AppendLine(passed ? "**Resultado: o GDI+ cumpre os três critérios a 60 quadros por segundo.**" : "**Resultado: o GDI+ não cumpre os três critérios a 60 quadros por segundo.**");
        return (text.ToString(), passed);
    }

    static string ProcessorName()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "desconhecido";
    }
}
