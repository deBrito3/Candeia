using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Candeia.Execution;

// Runs inside the isolated process (the game started with --runner). It reads one request, runs the
// player's method once per test, streams one result per test and exits; the process is never reused.
// Before loading the player's code it fixes the culture and replaces the console, so the code sees
// neither the machine's settings nor the real output, which only this class writes to.
public static class Runner
{
    // The command-line argument that turns the game (or the test executable) into the runner.
    public const string Argument = "--runner";
    const int StepLimitExitCode = 3, FailureExitCode = 4;

    public static int Main(Stream input, Stream output)
    {
        var protocol = new StreamWriter(output, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetIn(TextReader.Null);
            Console.SetError(TextWriter.Null);
            Send(protocol, new RunnerMessage { Kind = "ready" });
            using var reader = new StreamReader(input, Encoding.UTF8);
            string? line = reader.ReadLine();
            if (line is null) return 0; // A spare process the game no longer needs.
            var request = Protocol.Deserialize<RunRequestMessage>(line) ?? throw new InvalidDataException("empty request");
            nonce = request.Nonce;
            return new Session(protocol, request).Run();
        }
        catch (Exception ex)
        {
            Send(protocol, new RunnerMessage { Kind = "done", Stop = nameof(RunStop.RunnerFailure), Detail = ex.GetType().Name + ": " + ex.Message });
            return FailureExitCode;
        }
    }

    static string? nonce;
    static readonly object SendLock = new();

    // Locked: a thread started by the player's code may reach the step limit while the main one writes.
    static void Send(StreamWriter protocol, RunnerMessage message)
    {
        message.Nonce = nonce;
        lock (SendLock) protocol.WriteLine(Protocol.Serialize(message));
    }

    sealed class Session(StreamWriter protocol, RunRequestMessage request)
    {
        readonly CappedWriter console = new(request.OutputChars);
        FieldInfo steps = null!;
        Type probe = null!;
        int current = -1;
        string currentExpected = "";

        public int Run()
        {
            Console.SetOut(console);
            var context = new AssemblyLoadContext("player");
            var assembly = context.LoadFromStream(new MemoryStream(System.Convert.FromBase64String(request.Assembly)),
                request.Pdb is null ? null : new MemoryStream(System.Convert.FromBase64String(request.Pdb)));
            probe = assembly.GetType(Probe.TypeName) ?? throw new InvalidDataException("the probe is missing");
            steps = probe.GetField("Steps")!;
            probe.GetField("Limit")!.SetValue(null, request.StepsPerTest);
            probe.GetField("Exceeded")!.SetValue(null, (Action)StepLimitReached);
            probe.GetField("StackExhausted")!.SetValue(null, (Action)StackExhausted);

            // Every test's method must exist before any test runs: a missing one is the night's first message.
            var publicMethods = assembly.GetType(request.EntryType)?.GetMethods(BindingFlags.Public | BindingFlags.Static) ?? [];
            var methods = new MethodInfo[request.Tests.Count];
            for (int i = 0; i < request.Tests.Count; i++)
            {
                string name = request.Tests[i].Method ?? request.EntryMethod;
                var found = publicMethods.FirstOrDefault(m => m.Name == name && m.GetParameters().Length == request.Tests[i].Args.Length);
                if (found is null)
                {
                    Send(protocol, new RunnerMessage { Kind = "done", Stop = nameof(RunStop.EntryNotFound), Detail = $"{request.EntryType}.{name}" });
                    return 0;
                }
                methods[i] = found;
            }
            for (int i = 0; i < request.Tests.Count; i++)
            {
                var test = request.Tests[i];
                var method = methods[i];
                var parameters = method.GetParameters();
                current = i;
                object?[] args = parameters.Select((p, index) => TestValues.Convert(test.Args[index], p.ParameterType)).ToArray();
                object? expected = method.ReturnType == typeof(void) ? null : TestValues.Convert(test.Expected, method.ReturnType);
                currentExpected = TestValues.Format(expected);
                steps.SetValue(null, 0L);
                console.BeginTest();
                var message = new RunnerMessage { Kind = "test", Index = i, Expected = currentExpected };
                try
                {
                    object? actual = method.Invoke(null, args);
                    message.Actual = TestValues.Format(actual);
                    message.Passed = TestValues.Equal(actual, expected, request.Tolerance);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is { } error)
                {
                    message.ErrorType = error.GetType().FullName;
                    message.ErrorMessage = error.Message;
                    message.ErrorLine = PlayerLine(error);
                }
                message.Steps = (long)steps.GetValue(null)!;
                message.Output = console.TestOutput();
                Send(protocol, message);
            }
            Finish(nameof(RunStop.Completed));
            Environment.Exit(0);
            return 0;
        }

        // Called from inside the player's code when a test runs out of steps. Exiting the process is the
        // one thing the player's own try/catch cannot stop.
        void StepLimitReached() => Stop(RunStop.StepLimit, request.StepsPerTest);

        void StackExhausted() => Stop(RunStop.StackOverflow, (long)steps.GetValue(null)!);

        void Stop(RunStop stop, long used)
        {
            Send(protocol, new RunnerMessage { Kind = "test", Index = current, Expected = currentExpected, Steps = used, Output = console.TestOutput() });
            Finish(stop.ToString());
            Environment.Exit(StepLimitExitCode);
        }

        void Finish(string stop)
        {
            var marks = (long[])probe.GetField("Marks")!.GetValue(null)!;
            var calls = (long[])probe.GetField("Calls")!.GetValue(null)!;
            var methods = (string[])probe.GetField("Methods")!.GetValue(null)!;
            Send(protocol, new RunnerMessage
            {
                Kind = "done", Stop = stop,
                Marks = Probe.MarkNames.Select((name, i) => (name, i)).ToDictionary(p => p.name, p => marks[p.i]),
                Calls = methods.Select((name, i) => (name, i)).GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.Sum(p => calls[p.i])),
            });
            protocol.Flush();
        }

        static int? PlayerLine(Exception error)
        {
            foreach (var frame in new StackTrace(error, fNeedFileInfo: true).GetFrames())
                if (frame.GetFileName() is { } file && file.EndsWith(Languages.CSharp.CSharpRuntime.PlayerFileName, StringComparison.Ordinal) && frame.GetFileLineNumber() > 0)
                    return frame.GetFileLineNumber();
            return null;
        }
    }

    // The player's console: everything written is kept up to the limit, per test, and the rest is dropped.
    sealed class CappedWriter(int limit) : TextWriter
    {
        readonly StringBuilder text = new();
        int start, total;
        public override Encoding Encoding => Encoding.UTF8;
        public void BeginTest() { start = text.Length; }
        public string TestOutput() => text.ToString(start, text.Length - start);
        public override void Write(char value)
        {
            if (total >= limit) return;
            text.Append(value); total++;
        }
        public override void Write(string? value)
        {
            if (value is null || total >= limit) return;
            int room = Math.Min(value.Length, limit - total);
            text.Append(value, 0, room); total += room;
        }
    }
}
