using System.Runtime.Versioning;
using Candeia.Execution;
using Candeia.Execution.Isolation;

namespace Candeia.Tests;

// Starts one runner process and reports what the operating system says about it, without running code.
[SupportedOSPlatform("windows")]
static class IsolationProbe
{
    public static int Run(TextWriter output)
    {
        var limits = new RunLimits();
        using var process = IsolatedProcess.Start(Environment.ProcessPath!, [Runner.Argument], limits, lowIntegrity: true,
            new Dictionary<string, string> { ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows" });
        output.WriteLine($"runner pid {process.Id}: integrity 0x{process.IntegrityLevel:X4}");
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        output.WriteLine($"game pid {Environment.ProcessId}");
        return 0;
    }
}
