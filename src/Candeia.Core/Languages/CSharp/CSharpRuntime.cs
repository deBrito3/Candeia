using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Candeia.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Candeia.Languages.CSharp;

// Checks and compiles the player's C# inside the game process. Compiling never runs the player's code;
// running it is the isolated runner's job (delivery 2). A check goes through three filters, all reported
// together, sorted by line:
//   1. LanguageRestrictions: parts of the language the nights never need (unsafe, attributes, extern...);
//   2. the compiler's own errors, explained by DiagnosticTranslator;
//   3. ApiPolicy: the allow-list of types and members the player may use.
public sealed class CSharpRuntime : ILanguageRuntime
{
    public const string PlayerFileName = "Player.cs";
    public const int MaxSourceLength = 64_000, MaxErrors = 10;
    const string ResourcePrefix = "Candeia.PlayerReference.";

    public string Id => "csharp";

    static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest, DocumentationMode.None);
    static readonly Lazy<ImmutableArray<MetadataReference>> references = new(LoadReferences, isThreadSafe: true);
    internal static ImmutableArray<MetadataReference> References => references.Value;
    internal static IEnumerable<string> ReferenceNames => References.Select(r => r.Display ?? "");

    public CheckResult Check(string source, CheckOptions options)
    {
        var watch = Stopwatch.StartNew();
        var (_, errors) = Analyze(source, options);
        return new(errors, watch.Elapsed);
    }

    // Checks and compiles the code as written, without the probe: used to check that a night compiles.
    public CompileResult Compile(string source, CheckOptions options)
    {
        var watch = Stopwatch.StartNew();
        var (compilation, errors) = Analyze(source, options);
        if (errors.Count > 0) return new(errors, null, null, watch.Elapsed);
        return Emit(compilation, watch, []);
    }

    // Checks the code, then compiles it with the probe's step counters and marks, for the runner.
    // With countLines, the probe also counts the runs of each line (for the post-night review).
    public CompileResult CompileForRun(string source, CheckOptions options, bool countLines = false)
    {
        var watch = Stopwatch.StartNew();
        var (compilation, errors) = Analyze(source, options);
        if (errors.Count > 0) return new(errors, null, null, watch.Elapsed);
        return EmitInstrumented(compilation.SyntaxTrees.Last(), options, watch, countLines);
    }

    // Skips the language restrictions and the API allow-list. Only the security suite uses it, to prove
    // the isolated runner contains code that the checker would have refused.
    internal CompileResult CompileUnchecked(string source, CheckOptions options)
    {
        var watch = Stopwatch.StartNew();
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), ParseOptions, PlayerFileName);
        return EmitInstrumented(tree, options, watch);
    }

    // The night's required concepts that this code does not use; marks come from the run's result.
    public IReadOnlyList<string> MissingConcepts(string source, IEnumerable<string> concepts, IReadOnlyDictionary<string, long> marks, CheckOptions options) =>
        ConceptCheck.Missing(source, concepts, marks, ParseOptions,
            tree => CSharpCompilation.Create("PlayerCode", [Usings(options), tree], References, CompilationOptions(options)));

    // Compiles a tiny program once, during the night's introduction, so the player's first check does not
    // pay the compiler's start-up cost.
    public void Warm() => CompileForRun("public static class Warm { public static int Two(int x) { if (x > 0) return x + 1; return 2; } }", new());

    static CompileResult EmitInstrumented(SyntaxTree player, CheckOptions options, Stopwatch watch, bool countLines = false)
    {
        var instrumented = StepInstrumenter.Instrument(player, countLines);
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(instrumented.Source, Encoding.UTF8), ParseOptions, PlayerFileName);
        var probe = CSharpSyntaxTree.ParseText(SourceText.From(Probe.Source(instrumented.Methods, countLines ? instrumented.Lines : 0), Encoding.UTF8), ParseOptions, "Probe.cs");
        var compilation = CSharpCompilation.Create("PlayerCode", [Usings(options), probe, tree], References, CompilationOptions(options));
        return Emit(compilation, watch, instrumented.Methods);
    }

    static CompileResult Emit(CSharpCompilation compilation, Stopwatch watch, IReadOnlyList<string> methods)
    {
        using var assembly = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = compilation.Emit(assembly, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: "PlayerCode.pdb"));
        if (!emit.Success)
        {
            var tree = compilation.SyntaxTrees.Last();
            var model = compilation.GetSemanticModel(tree);
            var failures = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Location.SourceTree == tree)
                .Select(d => DiagnosticTranslator.Translate(d, model)).ToList();
            // An emit error outside the player's code is the game's fault: keep the compiler's words for the report.
            if (failures.Count == 0)
            {
                string detail = string.Join(" ", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Id + ": " + d.GetMessage()));
                failures.Add(new("CND099", 1, 1, "o código não pôde ser compilado. " + detail, "the code could not be compiled. " + detail));
            }
            return new(failures, null, null, watch.Elapsed);
        }
        return new([], assembly.ToArray(), pdb.ToArray(), watch.Elapsed, methods);
    }

    static (CSharpCompilation Compilation, List<CodeDiagnostic> Errors) Analyze(string? source, CheckOptions options)
    {
        source ??= "";
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), ParseOptions, PlayerFileName);
        var compilation = CSharpCompilation.Create("PlayerCode", [Usings(options), tree], References, CompilationOptions(options));
        var errors = new List<CodeDiagnostic>();
        if (string.IsNullOrWhiteSpace(source))
        {
            errors.Add(new("CND000", 1, 1, "escreva pelo menos uma linha de código.", "write at least one line of code."));
            return (compilation, errors);
        }
        if (source.Length > MaxSourceLength)
        {
            errors.Add(new("CND012", 1, 1, $"o código passou do limite de {MaxSourceLength} caracteres.", $"the code is over the {MaxSourceLength}-character limit."));
            return (compilation, errors);
        }
        errors.AddRange(LanguageRestrictions.Find(tree));
        var model = compilation.GetSemanticModel(tree);
        foreach (var diagnostic in compilation.GetDiagnostics())
            if (diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.SourceTree == tree)
                errors.Add(DiagnosticTranslator.Translate(diagnostic, model));
        errors.AddRange(ApiPolicy.Find(compilation, tree, options));
        var tidy = errors.GroupBy(e => (e.Line, e.Code)).Select(g => g.First())
            .OrderBy(e => e.Line).ThenBy(e => e.Column).Take(MaxErrors).ToList();
        return (compilation, tidy);
    }

    // Like .NET's implicit usings: the player does not have to write them. LINQ only when the night allows it.
    // Every tree needs an encoding, or the line information cannot be emitted.
    static SyntaxTree Usings(CheckOptions options) => CSharpSyntaxTree.ParseText(SourceText.From(
        "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Text;\n" + (options.AllowLinq ? "global using System.Linq;\n" : ""),
        Encoding.UTF8), ParseOptions, "ImplicitUsings.cs");

    static CSharpCompilationOptions CompilationOptions(CheckOptions options) =>
        new CSharpCompilationOptions(options.ConsoleProgram ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary)
            .WithOptimizationLevel(OptimizationLevel.Debug)
            .WithAllowUnsafe(false)
            .WithNullableContextOptions(NullableContextOptions.Disable)
            .WithOverflowChecks(false)
            .WithDeterministic(true);

    static ImmutableArray<MetadataReference> LoadReferences()
    {
        var assembly = typeof(CSharpRuntime).Assembly;
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing resource {name}.");
            builder.Add(MetadataReference.CreateFromStream(stream, filePath: name[ResourcePrefix.Length..]));
        }
        if (builder.Count == 0) throw new InvalidOperationException("The reference assemblies for the player's code are missing from Candeia.Core.");
        return builder.ToImmutable();
    }
}
