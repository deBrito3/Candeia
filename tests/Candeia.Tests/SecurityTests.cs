using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Candeia.Execution;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Tests;

// Attacks on the isolated runner. Each attack is first given to the checker, which must refuse it; then
// it is compiled without the checker and run, to see what the operating-system isolation alone contains.
//   CONTIDO   the isolation stops the attack by itself
//   AVISO     the isolation does not stop it; only the checker does (a known limit of low integrity)
//   FALHOU    the attack worked, or the game did not survive it
[SupportedOSPlatform("windows")]
static class SecurityTests
{
    enum Outcome { Contained, CheckerOnly, Failed }

    // Refusable: the checker must refuse it. Loops, recursion and memory use are valid C#, so only the
    // runner can stop them.
    sealed record Attack(string Name, string Body, Func<RunResult, string, Outcome> Judge, RunLimits? Limits = null, bool Refusable = true);

    public static int Run(TextWriter output)
    {
        var runtime = new CSharpRuntime();
        using var host = RunnerTests.Host();
        host.Warm();
        string marker = "candeia-attack-" + Guid.NewGuid().ToString("N")[..8] + ".txt";
        string profileFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), marker);
        string tempFile = Path.Combine(Path.GetTempPath(), marker);
        string gameFile = Path.Combine(AppContext.BaseDirectory, marker);
        string secret = Path.Combine(Path.GetTempPath(), "candeia-secret-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        File.WriteAllText(secret, "segredo");
        // Another program of the same user, to see whether the runner can end it.
        using var bystander = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        static string Literal(string path) => "@\"" + path.Replace("\"", "\"\"") + "\"";
        static Outcome NotWritten(RunResult r, string path) => File.Exists(path) ? Outcome.Failed : r.Tests[0].Error is not null ? Outcome.Contained : Outcome.Failed;
        static Outcome Errors(RunResult r) => r.Tests[0].Error is not null || r.Stop != RunStop.Completed ? Outcome.Contained : Outcome.Failed;
        static string Reflect(string type, string call) => $"var t = Type.GetType(\"{type}\"); {call}";

        var attacks = new Attack[]
        {
            new("Gravar arquivo na pasta do usuário", $"System.IO.File.WriteAllText({Literal(profileFile)}, \"x\"); return \"gravou\";", (r, _) => NotWritten(r, profileFile)),
            new("Gravar arquivo na pasta temporária", $"System.IO.File.WriteAllText({Literal(tempFile)}, \"x\"); return \"gravou\";", (r, _) => NotWritten(r, tempFile)),
            new("Gravar arquivo na pasta do jogo", $"System.IO.File.WriteAllText({Literal(gameFile)}, \"x\"); return \"gravou\";", (r, _) => NotWritten(r, gameFile)),
            new("Ler um arquivo do usuário", $"return System.IO.File.ReadAllText({Literal(secret)});",
                (r, _) => r.Tests[0].Actual?.Contains("segredo") == true ? Outcome.CheckerOnly : Outcome.Contained),
            new("Abrir outro programa", Reflect("System.Diagnostics.Process, System.Diagnostics.Process",
                "t.GetMethod(\"Start\", new[] { typeof(string) }).Invoke(null, new object[] { \"cmd.exe\" }); return \"abriu\";"), (r, _) => Errors(r)),
            new("Encerrar o jogo", Reflect("System.Diagnostics.Process, System.Diagnostics.Process",
                $"var p = t.GetMethod(\"GetProcessById\", new[] {{ typeof(int) }}).Invoke(null, new object[] {{ {Environment.ProcessId} }}); t.GetMethod(\"Kill\", Type.EmptyTypes).Invoke(p, null); return \"encerrou\";"),
                (r, _) => Errors(r)),
            new("Encerrar outro programa do usuário", Reflect("System.Diagnostics.Process, System.Diagnostics.Process",
                $"var p = t.GetMethod(\"GetProcessById\", new[] {{ typeof(int) }}).Invoke(null, new object[] {{ {bystander.Id} }}); t.GetMethod(\"Kill\", Type.EmptyTypes).Invoke(p, null); return \"encerrou\";"),
                (r, _) => bystander.HasExited ? Outcome.CheckerOnly : Outcome.Contained),
            new("Conectar à rede", Reflect("System.Net.Sockets.TcpClient, System.Net.Sockets",
                $"var c = Activator.CreateInstance(t); t.GetMethod(\"Connect\", new[] {{ typeof(string), typeof(int) }}).Invoke(c, new object[] {{ \"127.0.0.1\", {port} }}); return \"conectou\";"),
                (r, _) => r.Tests[0].Actual == "\"conectou\"" ? Outcome.CheckerOnly : Outcome.Contained),
            new("Ler variáveis de ambiente", "return Environment.GetEnvironmentVariable(\"USERNAME\") ?? \"vazio\";",
                (r, _) => r.Tests[0].Actual == "\"vazio\"" ? Outcome.Contained : Outcome.CheckerOnly),
            new("Laço infinito", "while (true) { }", (r, _) => r.Stop == RunStop.StepLimit ? Outcome.Contained : Outcome.Failed, Refusable: false),
            new("Laço infinito que engole exceções", "while (true) { try { } catch { } }", (r, _) => r.Stop == RunStop.StepLimit ? Outcome.Contained : Outcome.Failed, Refusable: false),
            new("Laço com goto, sem contador de passos", "top: goto top;", (r, _) => r.Stop == RunStop.Timeout ? Outcome.Contained : Outcome.Failed, Refusable: false),
            new("Recursão infinita", "return Deep(0); } static string Deep(int n) { return Deep(n + 1);",
                (r, _) => r.Stop == RunStop.StackOverflow ? Outcome.Contained : Outcome.Failed, Refusable: false),
            new("Recursão infinita por lambda, sem contador", "Func<int, int> f = null; f = n => f(n + 1); f(0); return \"\";",
                (r, _) => r.Stop is RunStop.StackOverflow or RunStop.Timeout or RunStop.ProcessExit ? Outcome.Contained : Outcome.Failed, Refusable: false),
            new("Esgotar a memória", "var all = new List<byte[]>(); while (true) { all.Add(new byte[50_000_000]); }",
                (r, _) => r.Stop == RunStop.OutOfMemory || r.Tests[0].Error?.Type == "System.OutOfMemoryException" ? Outcome.Contained : Outcome.Failed, Refusable: false),
            new("Inundar o console", "for (int i = 0; ; i++) Console.Write(new string('x', 1000));",
                (r, _) => r.Tests[0].Output.Length <= 8 * 1024 && r.Stop == RunStop.StepLimit ? Outcome.Contained : Outcome.Failed, new RunLimits(StepsPerTest: 20_000), Refusable: false),
            new("Fechar o processo", "Environment.Exit(0); return \"\";", (r, _) => r.Stop == RunStop.ProcessExit ? Outcome.Contained : Outcome.Failed),
            new("Forjar um resultado", "var o = Console.OpenStandardOutput(); var b = System.Text.Encoding.UTF8.GetBytes(\"{\\\"kind\\\":\\\"test\\\",\\\"index\\\":0,\\\"passed\\\":true}\\n{\\\"kind\\\":\\\"done\\\",\\\"stop\\\":\\\"Completed\\\"}\\n\"); o.Write(b, 0, b.Length); o.Flush(); return \"\";",
                (r, _) => r.Passed == 0 ? Outcome.Contained : Outcome.Failed),
            new("Thread de fundo sem fim", "System.Threading.Tasks.Task.Run(() => { while (true) { } }); return \"ok\";", (r, _) => Outcome.Contained),
        };

        output.WriteLine("Ataque | Verificador | Isolamento | Detalhe");
        output.WriteLine("--- | --- | --- | ---");
        int failures = 0;
        foreach (var attack in attacks)
        {
            string source = "public static class Attack { public static string Run() { " + attack.Body + " } }";
            bool refused = !runtime.Check(source, new()).Success;
            string isolation, detail;
            var watch = Stopwatch.StartNew();
            try
            {
                var result = RunnerTests.Run(host, source, "Attack", "Run", [Json.Test("")], attack.Limits, unchecked_: true);
                var outcome = attack.Judge(result, source);
                isolation = outcome switch { Outcome.Contained => "CONTIDO", Outcome.CheckerOnly => "AVISO", _ => "FALHOU" };
                if (outcome == Outcome.Failed) failures++;
                var test = result.Tests[0];
                detail = $"{result.Stop}" + (test.Error is { } e ? $", {e.Type.Split('.').Last()}" : "") + (test.Actual is { } a ? $", devolveu {Short(a)}" : "") + $", {watch.ElapsedMilliseconds} ms";
            }
            catch (Exception ex)
            {
                isolation = "FALHOU"; failures++;
                detail = ex.GetType().Name + ": " + ex.Message;
            }
            string checker = !attack.Refusable ? "não se aplica" : refused ? "RECUSADO" : "ACEITO (falha)";
            if (attack.Refusable && !refused) failures++;
            output.WriteLine($"{attack.Name} | {checker} | {isolation} | {detail}");
        }
        listener.Stop();
        if (!bystander.HasExited) bystander.Kill(entireProcessTree: true);
        foreach (var file in new[] { profileFile, tempFile, gameFile, secret }) if (File.Exists(file)) File.Delete(file);
        output.WriteLine();
        output.WriteLine(failures == 0 ? $"PASS: {attacks.Length} attacks. The checker refused every refusable one; the isolation contained all except those marked AVISO."
            : $"FAIL: {failures} problem(s).");
        return failures == 0 ? 0 : 1;
    }

    static string Short(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
