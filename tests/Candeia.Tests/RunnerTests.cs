using System.Diagnostics;
using System.Runtime.Versioning;
using Candeia.Execution;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Tests;

// Real runs of checked code in the isolated runner (this test executable started with --runner).
[SupportedOSPlatform("windows")]
static class RunnerTests
{
    static readonly CSharpRuntime Runtime = new();

    public static RunnerHost Host() => new(Environment.ProcessPath ?? throw new InvalidOperationException("no process path"));

    public static RunResult Run(RunnerHost host, string source, string type, string method, IReadOnlyList<TestCase> tests, RunLimits? limits = null, bool unchecked_ = false)
    {
        var compiled = unchecked_ ? Runtime.CompileUnchecked(source, new()) : Runtime.CompileForRun(source, new());
        if (!compiled.Success) throw new Exception("did not compile: " + string.Join(" | ", compiled.Errors.Select(e => e.Code + " " + e.MessageEn)));
        return host.Run(new RunRequest(compiled.Assembly!, compiled.Pdb, type, method, tests, limits ?? new()));
    }

    public static void Run(TestRunner t)
    {
        using (var process = Execution.Isolation.IsolatedProcess.Start(Environment.ProcessPath!, [Runner.Argument], new(), lowIntegrity: true,
            new Dictionary<string, string> { ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows" }))
            t.Check(process.IntegrityLevel == 0x1000, $"the runner process runs at low integrity: 0x{process.IntegrityLevel:X4}");

        using var host = Host();
        host.Warm();

        // For the post-night review: how many times each line ran, in a run compiled to count lines.
        const string counter = """
            public static class Counter
            {
                public static int Sum(int n)
                {
                    int total = 0;
                    for (int i = 0; i < n; i++)
                        total += i;
                    if (total > 1000)
                        return -1;
                    else
                        return total;
                }
            }
            """;
        var counting = Runtime.CompileForRun(counter, new(), countLines: true);
        var counted = host.Run(new RunRequest(counting.Assembly!, counting.Pdb, "Counter", "Sum", [Json.Test(10, 5), Json.Test(45, 10)], new()));
        long At(int line) => line < counted.Lines.Count ? counted.Lines[line] : -1;
        t.Check(counted.AllPassed && At(5) == 2 && At(6) == 2 && At(7) == 15 && At(8) == 2 && At(9) == 0 && At(11) == 2,
            $"line counts: {string.Join(",", counted.Lines.Select((n, i) => $"{i}:{n}").Where(p => !p.EndsWith(":0")))}");
        var plain = host.Run(new RunRequest(Runtime.CompileForRun(counter, new()).Assembly!, null, "Counter", "Sum", [Json.Test(10, 5)], new()));
        t.Check(plain.AllPassed && plain.Lines.Count == 0, "a normal run does not count lines");
        const string yard = """
            public static class Yard
            {
                public static bool ShouldLightYard(int light, int hour)
                {
                    if (light < 30) return true;
                    return hour >= 18 || hour < 6;
                }
            }
            """;
        TestCase[] yardTests = [Json.Test(true, 10, 12), Json.Test(false, 80, 12), Json.Test(true, 80, 20), Json.Test(true, 80, 3)];

        var good = Run(host, yard, "Yard", "ShouldLightYard", yardTests);
        t.Check(good.AllPassed && good.Passed == 4, $"a correct night lights every lamp: {good.Stop} {good.Detail} {good.Passed}/4");
        t.Check(good.Marks.GetValueOrDefault("if") == 4, $"the if ran once per test: {good.Marks.GetValueOrDefault("if")}");
        t.Check(good.Calls.GetValueOrDefault("ShouldLightYard") == 4, "the method was called once per test");
        t.Check(good.Tests.All(r => r.Steps >= 1), "steps are counted");

        var wrong = Run(host, yard.Replace("hour >= 18", "hour > 18"), "Yard", "ShouldLightYard", [.. yardTests, Json.Test(true, 80, 18)]);
        t.Check(wrong.Stop == RunStop.Completed && wrong.Passed == 4 && !wrong.Tests[4].Passed, "a wrong answer fails only its lamp");
        t.Check(wrong.Tests[4].Actual == "false" && wrong.Tests[4].Expected == "true", "a failed lamp shows the actual and expected values");

        const string division = """
            public static class Battery
            {
                public static int Minutes(int charge, int rate)
                {
                    int minutes = charge / rate;
                    return minutes;
                }
            }
            """;
        var crash = Run(host, division, "Battery", "Minutes", [Json.Test(5, 10, 2), Json.Test(0, 10, 0)]);
        t.Check(crash.Tests[0].Passed && crash.Tests[1].Error?.Type == "System.DivideByZeroException", "an exception fails only its test");
        t.Check(crash.Tests[1].Error?.Line == 5, $"the exception points at the player's line: {crash.Tests[1].Error?.Line}");

        const string loop = """
            public static class Stuck
            {
                public static int Forever(int x)
                {
                    while (x > 0) { x = x + 0; }
                    return x;
                }
            }
            """;
        var watch = Stopwatch.StartNew();
        var stuck = Run(host, loop, "Stuck", "Forever", [Json.Test(0, 1), Json.Test(0, 0)], new RunLimits(StepsPerTest: 100_000));
        t.Check(stuck.Stop == RunStop.StepLimit && !stuck.Tests[0].Passed, $"an endless loop stops at the step limit: {stuck.Stop}");
        t.Check(watch.Elapsed < TimeSpan.FromSeconds(3), $"the step limit stops it quickly: {watch.ElapsedMilliseconds} ms");

        const string talk = """
            public static class Radio
            {
                public static double Average(int[] values)
                {
                    double sum = 0;
                    foreach (var v in values) { sum += v; }
                    Console.WriteLine($"média de {values.Length}");
                    return sum / values.Length;
                }
            }
            """;
        var radio = Run(host, talk, "Radio", "Average", [Json.Test(2.5, new[] { 1, 2, 3, 4 }), Json.Test(1.0 / 3, new[] { 0, 0, 1 })]);
        t.Check(radio.AllPassed, $"arrays and doubles work, with tolerance: {radio.Stop} {radio.Detail} {string.Join(",", radio.Tests.Select(r => r.Actual))}");
        t.Check(radio.Tests[0].Output.Contains("média de 4"), "the console output is captured per test");
        t.Check(radio.Marks.GetValueOrDefault("foreach") == 2, "the foreach mark counts");

        var missing = Run(host, yard, "Yard", "ShouldLight", yardTests);
        t.Check(missing.Stop == RunStop.EntryNotFound && missing.Passed == 0, "a missing method is reported");

        const string lists = """
            public static class Board
            {
                public static List<string> On(Dictionary<string, int> watts)
                {
                    var on = new List<string>();
                    foreach (var pair in watts) if (pair.Value > 0) on.Add(pair.Key);
                    return on;
                }
            }
            """;
        var board = Run(host, lists, "Board", "On", [Json.Test(new[] { "quarto" }, new Dictionary<string, int> { ["quarto"] = 60, ["quintal"] = 0 })]);
        t.Check(board.AllPassed, $"lists and dictionaries work: {board.Stop} {board.Detail} {board.Tests[0].Actual}");

        var timing = Stopwatch.StartNew();
        var again = Run(host, yard, "Yard", "ShouldLightYard", yardTests);
        t.Check(again.AllPassed, "a second run works with the refilled spare");
        t.Check(timing.Elapsed < TimeSpan.FromSeconds(3), $"a run with a warm spare is quick: {timing.ElapsedMilliseconds} ms");
    }
}
