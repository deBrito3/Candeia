using System.Diagnostics;
using Candeia.Execution;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Tests;

static class Tools
{
    public static int Check(string path, bool english, bool linq, TextWriter output)
    {
        if (!File.Exists(path)) { output.WriteLine($"File not found: {path}"); return 2; }
        var runtime = new CSharpRuntime();
        var result = runtime.Check(File.ReadAllText(path), new CheckOptions(AllowLinq: linq));
        output.WriteLine($"{Path.GetFileName(path)} · {result.Elapsed.TotalMilliseconds:0} ms");
        if (result.Success) { output.WriteLine(english ? "OK: no problems found." : "OK: nenhum problema encontrado."); return 0; }
        foreach (var error in result.Errors)
        {
            output.WriteLine($"  [{error.Code}] {error.Format(english)}");
            if (error.Fix is { } fix) output.WriteLine((english ? "      FIX → line " : "      CORRIGIR → linha ") + $"{fix.Line}: {fix.Text.Trim()}");
        }
        return 1;
    }

    // Run in a fresh process: the first check pays Roslyn's start-up, the following ones show the
    // cost the player feels on each F5.
    public static int Bench(TextWriter output)
    {
        const string source = """
            public static class Yard
            {
                public static bool ShouldLightYard(int light, int hour)
                {
                    if (light < 30) return true;
                    return hour >= 18 || hour < 6;
                }
            }
            """;
        var runtime = new CSharpRuntime();
        var cold = Stopwatch.StartNew();
        var first = runtime.Compile(source, new());
        cold.Stop();
        const int rounds = 20;
        var warm = Stopwatch.StartNew();
        for (int i = 0; i < rounds; i++) runtime.Compile(source, new());
        warm.Stop();
        var check = Stopwatch.StartNew();
        for (int i = 0; i < rounds; i++) runtime.Check(source, new());
        check.Stop();
        output.WriteLine($"cold compile: {cold.Elapsed.TotalMilliseconds:0} ms (ok: {first.Success})");
        output.WriteLine($"warm compile: {warm.Elapsed.TotalMilliseconds / rounds:0.0} ms average over {rounds}");
        output.WriteLine($"warm check:   {check.Elapsed.TotalMilliseconds / rounds:0.0} ms average over {rounds}");
        if (OperatingSystem.IsWindows()) BenchRunner(runtime, source, output);
        return first.Success ? 0 : 1;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    static void BenchRunner(CSharpRuntime runtime, string source, TextWriter output)
    {
        var compiled = runtime.CompileForRun(source, new());
        TestCase[] tests = [Json.Test(true, 10, 12), Json.Test(false, 80, 12), Json.Test(true, 80, 20), Json.Test(true, 80, 3)];
        var request = new RunRequest(compiled.Assembly!, compiled.Pdb, "Yard", "ShouldLightYard", tests, new());
        using var host = RunnerTests.Host();
        var cold = Stopwatch.StartNew();
        var first = host.Run(request);
        cold.Stop();
        const int rounds = 5;
        double total = 0;
        for (int i = 0; i < rounds; i++)
        {
            Thread.Sleep(800); // The spare process refills in the background, as it does while the player types.
            var warm = Stopwatch.StartNew();
            host.Run(request);
            total += warm.Elapsed.TotalMilliseconds;
        }
        output.WriteLine($"run, no spare:   {cold.Elapsed.TotalMilliseconds:0} ms (lamps: {first.Passed}/{tests.Length})");
        output.WriteLine($"run, warm spare: {total / rounds:0} ms average over {rounds}");
    }
}
