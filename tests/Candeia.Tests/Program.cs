using Candeia.Execution;
using Candeia.Tests;

// Command-line test suites and developer tools, as in Projeto Aurora:
//   --self-test           every quick suite, including real runs in the isolated runner
//   --content-test        every night of content/, with its starter and reference solution run for real
//   --security-test       attacks on the isolated runner with code the checker would refuse
//   --check <file.cs>     checks a C# file as a night would; add --en for English, --linq to allow LINQ
//   --bench               compile and run times
//   --simulate            scripted nights through the real clock and creature, as a timeline
//   --sounds <folder>     every sound, and a scripted night mixed by the real director, as WAV files
//   --runner              the isolated runner itself, started by the suites (never by hand)
if (args is [Runner.Argument]) return Runner.Main(Console.OpenStandardInput(), Console.OpenStandardOutput());

if (args.Length == 0 || args.Contains("--self-test"))
{
    var runner = new TestRunner();
    runner.Suite("Core smoke", CoreSmokeTests.Run);
    runner.Suite("C# checker: allowed programs", CSharpCheckerTests.Allowed);
    runner.Suite("C# checker: refused programs", CSharpCheckerTests.Refused);
    runner.Suite("C# checker: explained errors", CSharpCheckerTests.Explained);
    runner.Suite("C# checker: compilation", CSharpCheckerTests.Compilation);
    runner.Suite("Instrumentation", InstrumentationTests.Run);
    runner.Suite("Night format", NightLoaderTests.Run);
    runner.Suite("Notebook", NotebookTests.Run);
    runner.Suite("Progress", ProgressTests.Run);
    runner.Suite("Night rules", NightRulesTests.Run);
    runner.Suite("Editor tokens", TokenizerTests.Run);
    runner.Suite("Audio", AudioTests.Run);
    if (OperatingSystem.IsWindows()) runner.Suite("Isolated runner", RunnerTests.Run);
    if (OperatingSystem.IsWindows()) runner.Suite("Night session", NightSessionTests.Run);
    return runner.Report(Console.Out);
}

if (args.Contains("--content-test"))
{
    if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("The isolated runner needs Windows."); return 2; }
    return ContentTests.Run(Console.Out);
}

if (args.Contains("--security-test"))
{
    if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("The isolated runner needs Windows."); return 2; }
    return SecurityTests.Run(Console.Out);
}

int check = Array.IndexOf(args, "--check");
if (check >= 0)
{
    if (check + 1 >= args.Length) { Console.Error.WriteLine("Pass the file after --check."); return 2; }
    return Tools.Check(args[check + 1], english: args.Contains("--en"), linq: args.Contains("--linq"), Console.Out);
}

if (args.Contains("--bench")) return Tools.Bench(Console.Out);

if (args.Contains("--simulate")) return NightSimulation.Run(Console.Out);

int sounds = Array.IndexOf(args, "--sounds");
if (sounds >= 0) return SoundFiles.Run(sounds + 1 < args.Length ? args[sounds + 1] : "test-artifacts/som", Console.Out);

if (args.Contains("--isolation-probe") && OperatingSystem.IsWindows()) return IsolationProbe.Run(Console.Out);

Console.Error.WriteLine($"Unknown option: {string.Join(' ', args)}. Use --self-test, --security-test, --check <file.cs> or --bench.");
return 2;
