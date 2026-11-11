using Microsoft.CodeAnalysis.CSharp;

namespace Candeia.Execution;

// The counters compiled together with the player's code. The instrumented code calls them; the runner
// reads and resets them by reflection. Entering a method also checks the stack, so endless recursion is
// caught before the stack overflows and takes the process down slowly. Their names start with "__", which the player's own code may not
// use, so the player can neither call nor change them.
static class Probe
{
    public const string TypeName = "__Candeia.__Probe";
    public const string Call = "global::__Candeia.__Probe.";

    // Constructions the nights can require; Marks counts how many times each one ran.
    public static readonly string[] MarkNames = ["if", "switch", "conditional", "for", "foreach", "while", "do"];
    public static int Mark(string name) => Array.IndexOf(MarkNames, name);

    public static string Source(IReadOnlyList<string> methods) => $$"""
        namespace __Candeia
        {
            public static class __Probe
            {
                public static long Steps;
                public static long Limit = long.MaxValue;
                public static global::System.Action Exceeded;
                public static global::System.Action StackExhausted;
                public static long[] Marks = new long[{{MarkNames.Length}}];
                public static long[] Calls = new long[{{Math.Max(1, methods.Count)}}];
                public static string[] Methods = new string[] { {{string.Join(", ", methods.Select(m => SymbolDisplay.FormatLiteral(m, quote: true)))}} };
                public static void Step() { if (++Steps > Limit) Exceeded(); }
                public static void Enter(int method)
                {
                    Calls[method]++;
                    if (!global::System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack()) StackExhausted();
                    Step();
                }
                public static void Mark(int kind) { Marks[kind]++; }
                public static bool MarkBool(int kind, bool value) { Marks[kind]++; return value; }
                public static T MarkValue<T>(int kind, T value) { Marks[kind]++; return value; }
            }
        }
        """;
}
