using System.Diagnostics;
using System.Globalization;
using System.Text;
using Candeia.Challenges;
using Candeia.Night;
using Candeia.Panels;
using Candeia.Scene;

namespace Candeia.Screens;

// Automatic runs of the night screen, in a borderless 1920×1080 window:
//   Images: the moments of night 1, one picture each (the LEIA-ME, a compile error, the starter's run, the
//           hints, a candle lost, the win), for visual review.
//   Measure: typing in the real editor while the night runs (with a run in the middle), the frame rate and
//           the processor, against the criteria of the rendering prototype.
static class NightBench
{
    public static int Run(IReadOnlyList<NightDefinition> nights, string target, bool images)
    {
        string folder = images ? target : Path.GetDirectoryName(Path.GetFullPath(target))!;
        Directory.CreateDirectory(folder);
        using var screen = new NightScreen(nights[0], nights, benchmark: true);
        int exit = 3;
        screen.Shown += async (_, _) =>
        {
            try { exit = images ? await Images(screen, folder) : await Measure(screen, target); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "erro.txt"), ex.ToString()); exit = 3; }
            finally { screen.Close(); }
        };
        Application.Run(screen);
        return exit;
    }

    static async Task<int> Images(NightScreen screen, string folder)
    {
        var night = screen.Session.Night;
        await Task.Delay(2500);
        SceneBench.Capture(screen, Path.Combine(folder, "1-leia-me.png"));

        screen.Session.Start();
        screen.Editor.Load(night.Starter.Replace("return watts;", "return watts"));
        screen.Run();
        await Settle(screen);
        SceneBench.Capture(screen, Path.Combine(folder, "2-erro.png"));

        screen.Editor.Load(night.Starter);
        screen.Run();
        await Settle(screen);
        SceneBench.Capture(screen, Path.Combine(folder, "3-uma-lampada.png"));

        screen.Command(Keys.Control | Keys.D3);
        screen.Command(Keys.Control | Keys.D3);
        await Task.Delay(300);
        SceneBench.Capture(screen, Path.Combine(folder, "4-dicas.png"));
        screen.Command(Keys.Control | Keys.D2);

        // Time runs out: a candle goes and the night gives a breath.
        screen.Session.Tick(screen.Session.Clock.Remaining + 1);
        await Task.Delay(600);
        SceneBench.Capture(screen, Path.Combine(folder, "5-vela-apagada.png"));

        if (Solution(night) is { } solution)
        {
            await Task.Delay((int)(screen.Session.Penalties.Rules.RespiteSeconds * 1000) + 500);
            screen.Editor.Load(solution);
            screen.Run();
            await Settle(screen);
            await Task.Delay(1500);
            SceneBench.Capture(screen, Path.Combine(folder, "6-vitoria.png"));
        }
        return 0;
    }

    // The reference solution of a night, read from the repository when the game runs from it. It is never
    // shipped with the game.
    static string? Solution(NightDefinition night)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "tests", "Candeia.Tests", "Solutions", night.Id + ".cs");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        return null;
    }

    static async Task Settle(NightScreen screen)
    {
        var watch = Stopwatch.StartNew();
        do await Task.Delay(50);
        while (screen.Session.Busy && watch.Elapsed.TotalSeconds < 20);
        await Task.Delay(300);
    }

    static async Task<int> Measure(NightScreen screen, string reportPath)
    {
        using var probe = new TypingProbe();
        await Task.Delay(2500);
        screen.Session.Start();
        var editor = screen.Editor;
        editor.Attach(probe, editor.GetFirstCharIndexFromLine(1) - 1);
        using var process = Process.GetCurrentProcess();
        await Task.Delay(1000);

        screen.Stats.Reset();
        probe.Take();
        process.Refresh();
        var cpuStart = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        await Task.Delay(8000);
        // A run in the middle: the check, the runner and the lamps, while the probe keeps typing.
        editor.Detach();
        screen.Run();
        await Settle(screen);
        editor.Attach(probe, editor.GetFirstCharIndexFromLine(1) - 1);
        await Task.Delay(8000);
        process.Refresh();
        double seconds = wall.Elapsed.TotalSeconds;
        double cpu = (process.TotalProcessorTime - cpuStart).TotalMilliseconds / (seconds * 1000) * 100;
        var frames = screen.Stats.Summarize(seconds);
        var (typing, lost) = probe.Take();
        editor.Detach();
        var spread = Spread.Of(typing);

        var pt = CultureInfo.GetCultureInfo("pt-BR");
        string F(double v, string f = "0.0") => v.ToString(f, pt);
        bool fpsOk = frames.Fps >= 29.75, cpuOk = cpu <= 35, typingOk = spread.P95 < 16 && lost == 0;
        var text = new StringBuilder();
        text.AppendLine("# Noite 1: medições na tela de verdade");
        text.AppendLine();
        text.AppendLine($"- Janela de {screen.ClientSize.Width}×{screen.ClientSize.Height}, {F(seconds, "0")} s com o relógio correndo, uma execução (F5) no meio.");
        text.AppendLine($"- Energia: {(SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online ? "na tomada" : "na bateria (o Windows reduz a velocidade do processador)")}; som mixado e mudo.");
        text.AppendLine($"- Quadros por segundo: {F(frames.Fps)} (trabalho do quadro: mediana {F(frames.Work.Median)} ms, p95 {F(frames.Work.P95)} ms, máx. {F(frames.Work.Max)} ms): {(fpsOk ? "PASS" : "FALHA")}");
        text.AppendLine($"- Processador: {F(cpu, "0")}% de um núcleo (máximo 35%): {(cpuOk ? "PASS" : "FALHA")}");
        text.AppendLine($"- Digitação no editor com cores: mediana {F(spread.Median)} ms, p95 {F(spread.P95)} ms, máx. {F(spread.Max)} ms, {spread.Count} teclas, {lost} perdidas (p95 abaixo de 16 ms): {(typingOk ? "PASS" : "FALHA")}");
        text.AppendLine($"- Resultado da execução: {screen.Session.Output.FirstOrDefault()?.Text}");
        File.WriteAllText(reportPath, text.ToString(), new UTF8Encoding(false));
        return fpsOk && cpuOk && typingOk ? 0 : 1;
    }
}
