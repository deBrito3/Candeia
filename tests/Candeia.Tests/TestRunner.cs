namespace Candeia.Tests;

// A minimal check collector: every suite records named checks, and the run fails if any check fails.
sealed class TestRunner
{
    readonly List<string> failures = [];
    public int Passed { get; private set; }
    public IReadOnlyList<string> Failures => failures;

    public void Check(bool condition, string name)
    {
        if (condition) Passed++;
        else failures.Add(name);
    }

    // Runs a suite; an exception counts as one failure named after the suite.
    public void Suite(string name, Action<TestRunner> suite)
    {
        try { suite(this); }
        catch (Exception ex) { failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
    }

    public int Report(TextWriter output)
    {
        if (failures.Count == 0)
        {
            output.WriteLine($"PASS: {Passed} checks.");
            return 0;
        }
        output.WriteLine($"FAIL: {failures.Count} of {Passed + failures.Count} checks.");
        foreach (var failure in failures) output.WriteLine("  - " + failure);
        return 1;
    }
}
