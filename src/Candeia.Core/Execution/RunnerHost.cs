using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Candeia.Execution.Isolation;

namespace Candeia.Execution;

// The game's side of the isolated runner. It keeps one spare runner process started and waiting, so a run
// does not pay the process start-up; sends the request; reads one result per test; and ends the process
// when the run is done, out of time or misbehaving. Every process runs one request and is thrown away.
// Memory and processor limits are fixed when a process starts (the host's limits); steps, output and the
// wall clock come from each request.
[SupportedOSPlatform("windows")]
public sealed class RunnerHost : IDisposable
{
    const int StartMilliseconds = 10_000;
    const int StackOverflowExitCode = unchecked((int)0xC00000FD);

    readonly string executable;
    readonly bool lowIntegrity;
    readonly RunLimits limits;
    readonly object sync = new();
    Worker? spare;
    bool disposed;

    public RunnerHost(string executable, bool lowIntegrity = true, RunLimits? limits = null)
    {
        this.executable = executable;
        this.lowIntegrity = lowIntegrity;
        this.limits = limits ?? new();
        if (lowIntegrity) SelfProtection.Apply();
    }

    // Starts the spare process now, during the night's introduction.
    public void Warm()
    {
        lock (sync) { if (!disposed) spare ??= Worker.Start(executable, lowIntegrity, limits); }
    }

    public RunResult Run(RunRequest request)
    {
        var watch = Stopwatch.StartNew();
        Worker worker;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            worker = spare ?? Worker.Start(executable, lowIntegrity, limits);
            spare = null;
        }
        try { return worker.Run(request, watch); }
        finally
        {
            worker.Dispose();
            _ = Task.Run(() => { try { Warm(); } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException) { } });
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            spare?.Dispose();
            spare = null;
        }
    }

    // What the runner process may see of the game's environment: only what Windows and .NET need to start.
    static IReadOnlyDictionary<string, string> ChildEnvironment(RunLimits limits)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "SystemRoot", "windir", "SystemDrive", "TEMP", "TMP", "DOTNET_ROOT", "DOTNET_BUNDLE_EXTRACT_BASE_DIR" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value) environment[name] = value;
        environment["DOTNET_GCHeapHardLimit"] = limits.GcHeapBytes.ToString("X");
        environment["DOTNET_EnableDiagnostics"] = "0";
        environment["DOTNET_DbgEnableMiniDump"] = "0";
        return environment;
    }

    sealed class Worker : IDisposable
    {
        readonly IsolatedProcess process;
        readonly long memoryLimit;
        readonly BlockingCollection<string> lines = new(512);
        readonly StringBuilder errors = new();

        Worker(IsolatedProcess process, long memoryLimit)
        {
            this.process = process;
            this.memoryLimit = memoryLimit;
            _ = Task.Run(ReadOutput);
            _ = Task.Run(ReadErrors);
        }

        public static Worker Start(string executable, bool lowIntegrity, RunLimits limits) =>
            new(IsolatedProcess.Start(executable, [Runner.Argument], limits, lowIntegrity, ChildEnvironment(limits)), limits.MemoryBytes);

        public RunResult Run(RunRequest request, Stopwatch watch)
        {
            var results = new TestResult?[request.Tests.Count];
            var marks = new Dictionary<string, long>();
            var calls = new Dictionary<string, long>();
            long[] lineCounts = [];
            RunResult Result(RunStop stop, string? detail)
            {
                var tests = results.Select((r, i) => r ?? new TestResult(i, false, null, TestValues.Format(request.Tests[i].Expected), 0, "", null)).ToList();
                return new(tests, marks, calls, stop, detail, watch.Elapsed) { Lines = lineCounts };
            }

            // 1. The process says it is ready.
            var ready = Next(StartMilliseconds);
            if (ready is null || Parse(ready)?.Kind != "ready") return Result(RunStop.RunnerFailure, "the runner did not start. " + Errors());

            // 2. The request, with a fresh nonce every reply must repeat.
            string nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
            try
            {
                var message = new RunRequestMessage
                {
                    Nonce = nonce,
                    Assembly = Convert.ToBase64String(request.Assembly),
                    Pdb = request.Pdb is null ? null : Convert.ToBase64String(request.Pdb),
                    EntryType = request.EntryType, EntryMethod = request.EntryMethod,
                    Tests = request.Tests.Select(t => new TestCaseMessage { Method = t.Method, Args = t.Args, Expected = t.Expected }).ToList(),
                    StepsPerTest = request.Limits.StepsPerTest, OutputChars = request.Limits.OutputChars, Tolerance = request.Tolerance,
                };
                var bytes = new UTF8Encoding(false).GetBytes(Protocol.Serialize(message) + "\n");
                process.Input.Write(bytes);
                process.Input.Flush();
                process.Input.Dispose();
            }
            catch (IOException) { return Result(Exited(), "the runner closed before the request. " + Errors()); }

            // 3. One result per test, in order, then "done", within the wall clock.
            var deadline = Stopwatch.StartNew();
            int expected = 0;
            while (true)
            {
                int remaining = request.Limits.WallClockMilliseconds - (int)deadline.ElapsedMilliseconds;
                if (remaining <= 0) { process.Kill(); return Result(RunStop.Timeout, null); }
                var line = Next(remaining);
                if (line is null)
                {
                    if (lines.IsCompleted) return Result(Exited(), Errors());
                    process.Kill();
                    return Result(RunStop.Timeout, null);
                }
                var reply = Parse(line);
                if (reply is null || reply.Nonce != nonce) { process.Kill(); return Result(RunStop.RunnerFailure, "invalid reply from the runner"); }
                if (reply.Kind == "test" && reply.Index == expected && expected < results.Length)
                {
                    RuntimeError? error = reply.ErrorType is null ? null : new(reply.ErrorType, Cap(reply.ErrorMessage ?? "", 2000), reply.ErrorLine);
                    results[expected] = new TestResult(expected, reply.Passed && error is null, reply.Actual is null ? null : Cap(reply.Actual, 2000),
                        Cap(reply.Expected ?? TestValues.Format(request.Tests[expected].Expected), 2000), Math.Max(0, reply.Steps), Cap(reply.Output ?? "", request.Limits.OutputChars), error);
                    expected++;
                }
                else if (reply.Kind == "done")
                {
                    foreach (var (name, count) in reply.Marks ?? []) marks[Cap(name, 64)] = count;
                    foreach (var (name, count) in (reply.Calls ?? []).Take(512)) calls[Cap(name, 128)] = count;
                    lineCounts = (reply.Lines ?? []).Take(5000).Select(n => Math.Max(0, n)).ToArray();
                    var stop = Enum.TryParse<RunStop>(reply.Stop, out var parsed) ? parsed : RunStop.RunnerFailure;
                    if (stop == RunStop.Completed && expected != results.Length) return Result(RunStop.RunnerFailure, "the runner skipped tests");
                    return Result(stop, reply.Detail is null ? null : Cap(reply.Detail, 500));
                }
                else { process.Kill(); return Result(RunStop.RunnerFailure, "unexpected reply from the runner"); }
            }
        }

        // The process ended without saying "done": a stack overflow, the memory limit or the code itself.
        RunStop Exited()
        {
            process.WaitForExit(1000);
            int? code = process.ExitCode;
            if (code == StackOverflowExitCode) return RunStop.StackOverflow;
            if (process.PeakMemory >= memoryLimit * 9 / 10 || Errors().Contains("OutOfMemory", StringComparison.Ordinal)) return RunStop.OutOfMemory;
            return RunStop.ProcessExit;
        }

        string? Next(int milliseconds) => lines.TryTake(out var line, milliseconds) ? line : null;

        static RunnerMessage? Parse(string line)
        {
            if (line.Length >= Protocol.MaxLineChars) return null;
            try { return Protocol.Deserialize<RunnerMessage>(line); }
            catch (System.Text.Json.JsonException) { return null; }
        }

        static string Cap(string text, int limit) => text.Length <= limit ? text : text[..limit];

        string Errors() { lock (errors) return errors.ToString(); }

        void ReadOutput()
        {
            try
            {
                using var reader = new StreamReader(process.Output, Encoding.UTF8);
                var line = new StringBuilder();
                int c;
                while ((c = reader.Read()) >= 0)
                {
                    if (c == '\n')
                    {
                        if (!lines.TryAdd(line.ToString(), 1000)) break;
                        line.Clear();
                    }
                    else if (line.Length < Protocol.MaxLineChars) line.Append((char)c);
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
            finally { lines.CompleteAdding(); }
        }

        void ReadErrors()
        {
            try
            {
                using var reader = new StreamReader(process.Error, Encoding.UTF8);
                var buffer = new char[1024];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    lock (errors) if (errors.Length < 8 * 1024) errors.Append(buffer, 0, read);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }

        public void Dispose() => process.Dispose();
    }
}
