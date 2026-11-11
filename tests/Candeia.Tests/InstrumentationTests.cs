using Candeia.Languages;
using Candeia.Languages.CSharp;
using Microsoft.CodeAnalysis.CSharp;

namespace Candeia.Tests;

// The probe calls must keep every program compiling and every line where it was.
static class InstrumentationTests
{
    static readonly CSharpRuntime Runtime = new();

    public static void Run(TestRunner t)
    {
        foreach (var (name, source, options) in CSharpCheckerTests.AllowedPrograms())
        {
            var program = Runtime.CompileForRun(source, options ?? new());
            t.Check(program.Success, $"instrumented {name} compiles: {string.Join(" | ", program.Errors.Select(e => e.Code + " L" + e.Line + " " + e.MessageEn))}");
        }

        const string tricky = """
            public class Tricky
            {
                int field;
                public Tricky(int v) => field = v;
                public int Value { get { return field; } set { field = value; } }
                public int Twice => field * 2;
                public static int Sign(int x) => x > 0 ? 1 : x < 0 ? -1 : 0;
                public static void Nothing() => System.Console.Write("");
                public static int Fail() => throw new System.InvalidOperationException();
                public static Tricky operator +(Tricky a, Tricky b) => new Tricky(a.field + b.field);
                public static string Name(int hour) => hour switch { < 6 => "madrugada", < 18 => "dia", _ => "noite" };
                public static int Loops(int n)
                {
                    int total = 0;
                    for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) total++;
                    while (total > 100) total--;
                    do total++; while (total < 3);
                    foreach (var c in "ab") if (c == 'a') total++; else if (c == 'b') total += 2; else total = 0;
                    for (;;) break;
                    int Local(int x) => x + 1;
                    System.Func<int, int> f = x => { return x * Local(x); };
                    switch (n) { case 0: total = f(1); break; default: if (n > 10) { total++; } break; }
                    again: if (total < 0) goto again;
                    return total;
                }
            }
            """;
        var result = StepInstrumenter.Instrument(CSharpSyntaxTree.ParseText(tricky));
        t.Check(result.Source.Split('\n').Length == tricky.Split('\n').Length, "instrumentation keeps the number of lines");
        t.Check(CSharpSyntaxTree.ParseText(result.Source).GetDiagnostics().All(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error), "instrumented tricky code still parses");
        t.Check(result.Methods.SequenceEqual(["Tricky", "Sign", "Nothing", "Fail", "Name", "Loops", "Local"]), $"declared methods are counted: {string.Join(",", result.Methods)}");
        var compiled = Runtime.CompileForRun(tricky, new());
        t.Check(compiled.Success, $"instrumented tricky code compiles: {string.Join(" | ", compiled.Errors.Select(e => e.Code + " L" + e.Line + " " + e.MessageEn))}");
        var lines = result.Source.Split('\n');
        t.Check(lines[14].Contains("Mark(3)") && lines[14].Contains("Step()"), "nested for loops get marks and steps on their own line");
        t.Check(lines[17].Contains("Mark(0)") && lines[17].Contains("Mark(4)"), "an if inside foreach and an else-if chain are marked");
        t.Check(lines[6].Contains("MarkBool(2"), "the conditional operator is marked");
        t.Check(lines[10].Contains("MarkValue(1"), "the switch expression is marked");
    }
}
