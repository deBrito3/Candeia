using System.Runtime.Versioning;
using Candeia.Challenges;
using Candeia.Execution;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Tests;

// Every night of content/, played for real in the isolated runner:
//   - the files are complete and follow the format, in both languages;
//   - the starter code compiles (a corrupted night's may not) and does not win the night by itself;
//   - the reference solution lights every lamp and uses every required concept.
[SupportedOSPlatform("windows")]
static class ContentTests
{
    public static int Run(TextWriter output)
    {
        var runtime = new CSharpRuntime();
        using var host = RunnerTests.Host();
        host.Warm();
        var loaded = NightLoader.LoadAll();
        int problems = 0;
        output.WriteLine("Noite | Título | Testes | Ocultos | Código inicial | Solução | Resultado");
        output.WriteLine("--- | --- | --- | --- | --- | --- | ---");
        foreach (var result in loaded)
        {
            if (!result.Ok)
            {
                problems += result.Errors.Count;
                output.WriteLine($"{Path.GetFileName(result.Folder)} | ? | | | | | FALHOU: {string.Join("; ", result.Errors)}");
                continue;
            }
            var night = result.Night!;
            var issues = new List<string>();

            string starterLamps = "não compila";
            var starter = runtime.CompileForRun(night.Starter, new());
            if (!starter.Success && night.Type != "corrupted") issues.Add("the starter must compile: " + string.Join(" | ", starter.Errors.Select(e => e.Code + " L" + e.Line)));
            if (starter.Success)
            {
                var run = host.Run(night.Request(starter.Assembly!, starter.Pdb));
                starterLamps = $"{run.Passed}/{night.Tests.Count}";
                if (run.AllPassed && runtime.MissingConcepts(night.Starter, night.Concepts, run.Marks, new()).Count == 0) issues.Add("the starter already wins the night");
            }

            string solutionLamps = "ausente";
            string solutionPath = Path.Combine(AppContext.BaseDirectory, "Solutions", night.Id + ".cs");
            if (!File.Exists(solutionPath)) issues.Add("no reference solution in tests/Candeia.Tests/Solutions");
            else
            {
                string source = File.ReadAllText(solutionPath);
                var solution = runtime.CompileForRun(source, new());
                if (!solution.Success) issues.Add("the solution does not compile: " + string.Join(" | ", solution.Errors.Select(e => e.Code + " L" + e.Line + " " + e.MessageEn)));
                else
                {
                    var run = host.Run(night.Request(solution.Assembly!, solution.Pdb));
                    solutionLamps = $"{run.Passed}/{night.Tests.Count}";
                    if (!run.AllPassed) issues.Add($"the solution lights {run.Passed} of {night.Tests.Count} lamps ({run.Stop} {run.Detail})");
                    var missing = runtime.MissingConcepts(source, night.Concepts, run.Marks, new());
                    if (missing.Count > 0) issues.Add("the solution misses " + string.Join(", ", missing));
                }
            }
            if (night.Hints.Count != 3) issues.Add("three hint layers are needed");
            // The editor shows about 80 columns at 1920x1080 without scrolling sideways.
            var wide = night.Starter.Replace("\r\n", "\n").Split('\n').Select((l, i) => (l, i)).Where(x => x.l.Length > 80).Select(x => x.i + 1).ToList();
            if (wide.Count > 0) issues.Add("starter lines wider than 80 columns: " + string.Join(", ", wide));
            if (night.Learned.Pt.Length < 20 || night.Learned.En.Length < 20) issues.Add("the learned line needs a sentence in both languages");
            problems += issues.Count;
            output.WriteLine($"{night.Id} | {night.Title.Pt} / {night.Title.En} | {night.Tests.Count} | {night.HiddenTests} | {starterLamps} | {solutionLamps} | " +
                (issues.Count == 0 ? "OK" : "FALHOU: " + string.Join("; ", issues)));
        }
        // The notebook: every page in both languages, each keyword opening one page only, and a page for every
        // concept a night asks for.
        var notebook = Notebook.Load();
        foreach (var night in loaded.Where(r => r.Ok).Select(r => r.Night!))
            foreach (var concept in night.Concepts)
            {
                string word = concept.Contains(':') ? concept[(concept.IndexOf(':') + 1)..] : concept;
                if (Notebook.For(notebook.Pages, word) is null) { output.WriteLine($"  FALHOU: {night.Id} asks for {concept}, and the notebook has no page for it"); problems++; }
            }
        output.WriteLine();
        output.WriteLine($"Caderno: {notebook.Pages.Count} páginas ({string.Join(", ", notebook.Pages.Select(p => p.Title.Pt))})");
        foreach (var error in notebook.Errors) output.WriteLine("  FALHOU: " + error);
        problems += notebook.Errors.Count + (notebook.Pages.Count == 0 ? 1 : 0);
        output.WriteLine();
        output.WriteLine(problems == 0 ? $"PASS: {loaded.Count} nights, {notebook.Pages.Count} notebook pages." : $"FAIL: {problems} problem(s).");
        return problems == 0 ? 0 : 1;
    }
}
