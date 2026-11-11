using Candeia.Tests;

// Command-line test suites and developer tools, as in Projeto Aurora:
//   --self-test           every suite
//   --check <file.cs>     checks a C# file as a night would; add --en for English, --linq to allow LINQ
//   --bench               compile times: the first (cold) check and the average of warm checks
if (args.Length == 0 || args.Contains("--self-test"))
{
    var runner = new TestRunner();
    runner.Suite("Core smoke", CoreSmokeTests.Run);
    runner.Suite("C# checker: allowed programs", CSharpCheckerTests.Allowed);
    runner.Suite("C# checker: refused programs", CSharpCheckerTests.Refused);
    runner.Suite("C# checker: explained errors", CSharpCheckerTests.Explained);
    runner.Suite("C# checker: compilation", CSharpCheckerTests.Compilation);
    return runner.Report(Console.Out);
}

int check = Array.IndexOf(args, "--check");
if (check >= 0)
{
    if (check + 1 >= args.Length) { Console.Error.WriteLine("Pass the file after --check."); return 2; }
    return Tools.Check(args[check + 1], english: args.Contains("--en"), linq: args.Contains("--linq"), Console.Out);
}

if (args.Contains("--bench")) return Tools.Bench(Console.Out);

Console.Error.WriteLine($"Unknown option: {string.Join(' ', args)}. Use --self-test, --check <file.cs> or --bench.");
return 2;
