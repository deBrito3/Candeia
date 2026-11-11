using Candeia.Tests;

// Command-line test suites, as in Projeto Aurora:
//   dotnet run --project tests/Candeia.Tests -- --self-test
if (args.Length == 0 || args.Contains("--self-test"))
{
    var runner = new TestRunner();
    runner.Suite("Core smoke", CoreSmokeTests.Run);
    return runner.Report(Console.Out);
}

Console.Error.WriteLine($"Unknown option: {string.Join(' ', args)}. Use --self-test.");
return 2;
