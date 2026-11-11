using System.Text.Json;

namespace Candeia.Execution;

// One lamp of a night: the arguments the house system passes to the player's method and the answer it
// expects, as neutral JSON values (numbers, booleans, text, lists, records), so any language can use it.
public sealed record TestCase(JsonElement[] Args, JsonElement Expected, bool Hidden = false);

// Limits of one run. Defaults are the Act 1 values of the technical plan.
public sealed record RunLimits(
    long StepsPerTest = 1_000_000,
    int OutputChars = 8 * 1024,
    int WallClockMilliseconds = 3_000,
    int CpuMilliseconds = 5_000,
    long MemoryBytes = 256L * 1024 * 1024)
{
    // The garbage collector stops before the Job Object does, so running out of memory becomes an
    // exception the game can explain instead of a killed process.
    public long GcHeapBytes => MemoryBytes * 3 / 4;
}

public sealed record RunRequest(byte[] Assembly, byte[]? Pdb, string EntryType, string EntryMethod,
    IReadOnlyList<TestCase> Tests, RunLimits Limits, double Tolerance = 1e-9);

// Why a run ended.
public enum RunStop { Completed, StepLimit, Timeout, StackOverflow, OutOfMemory, ProcessExit, EntryNotFound, RunnerFailure }

// An exception thrown by the player's code, with the line where it happened.
public sealed record RuntimeError(string Type, string Message, int? Line);

public sealed record TestResult(int Index, bool Passed, string? Actual, string Expected, long Steps, string Output, RuntimeError? Error);

public sealed record RunResult(
    IReadOnlyList<TestResult> Tests,
    IReadOnlyDictionary<string, long> Marks,
    IReadOnlyDictionary<string, long> Calls,
    RunStop Stop,
    string? Detail,
    TimeSpan Elapsed)
{
    public int Passed => Tests.Count(t => t.Passed);
    public bool AllPassed => Stop == RunStop.Completed && Tests.Count > 0 && Tests.All(t => t.Passed);
}
