using System.Runtime.Loader;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Tests;

// The C# checker: what the nights accept, what the house system refuses, and how errors are explained.
static class CSharpCheckerTests
{
    static readonly CSharpRuntime Runtime = new();

    // Wraps statements in a method of a class, as the early nights do.
    static string InMethod(string body, string returnType = "int") => $$"""
        public static class Night
        {
            public static {{returnType}} Solve(int light, int hour)
            {
        {{body}}
            }
        }
        """;

    static CheckResult Check(string source, CheckOptions? options = null) => Runtime.Check(source, options ?? new());

    public static void Allowed(TestRunner t)
    {
        var programs = new (string Name, string Source, CheckOptions? Options)[]
        {
            ("night method", "public static class Yard { public static bool ShouldLightYard(int light, int hour) { return light < 30 || hour >= 18; } }", null),
            ("variables and casts", InMethod("double ratio = Math.Round(7 / 2.0, 1);\nint whole = (int)ratio;\nlong big = whole * 1000L;\nreturn whole;"), null),
            ("loops and arrays", InMethod("int[] values = { 3, 1, 2 };\nint sum = 0;\nfor (int i = 0; i < values.Length; i++) sum += values[i];\nforeach (int v in values) sum += v;\nwhile (sum > 100) sum--;\nreturn sum;"), null),
            ("collections", InMethod("var list = new List<int> { 1, 2 };\nvar map = new Dictionary<string, int> { [\"a\"] = 1 };\nvar seen = new HashSet<int>();\nvar queue = new Queue<int>(); queue.Enqueue(1);\nvar stack = new Stack<int>(); stack.Push(2);\nforeach (var pair in map) seen.Add(pair.Value);\nreturn list.Count + map[\"a\"] + queue.Dequeue() + stack.Pop() + seen.Count;"), null),
            ("strings", InMethod("string s = \"Recife\";\nstring up = s.ToUpper().Substring(0, 3).Replace(\"R\", \"r\");\nstring[] parts = \"a,b\".Split(',');\nstring joined = string.Join(\"-\", parts) + $\"{light}:{hour}\";\nvar sb = new StringBuilder(); sb.Append(up).Append(joined);\nreturn s.Contains(\"ci\") && char.IsLetter(s[0]) ? sb.ToString().Length : int.Parse(\"7\");"), null),
            ("console output", InMethod("Console.WriteLine($\"luz: {light}\");\nConsole.Write(hour);\nreturn 0;"), null),
            ("classes and records", "public record Lamp(string Room, int Watts);\npublic interface IDevice { bool On { get; } }\npublic class Lock : IDevice { readonly string code; public Lock(string code) { this.code = code; } public bool On { get; private set; } public bool TryOpen(string attempt) { On = attempt == code; return On; } public override string ToString() => $\"Lock {On}\"; }", null),
            ("lambdas and delegates", InMethod("var list = new List<int> { 3, 1, 2 };\nlist.Sort((a, b) => a - b);\nvar big = list.FindAll(x => x > 1);\nFunc<int, int> twice = x => x * 2;\nAction<int> log = x => Console.WriteLine(x);\nlog(twice(big.Count));\nreturn list[0];"), null),
            ("patterns and tuples", InMethod("(int a, int b) = (light, hour);\nstring label = hour switch { < 6 => \"madrugada\", < 18 => \"dia\", _ => \"noite\" };\nif (label is \"noite\" && a > 0) return b;\nreturn a;"), null),
            ("exceptions", InMethod("try { if (light < 0) throw new ArgumentException(\"negativo\"); return 1 / hour; }\ncatch (DivideByZeroException e) { Console.WriteLine(e.Message); return 0; }\ncatch (Exception) { return -1; }"), null),
            ("collection expressions", InMethod("List<int> list = [1, 2, 3];\nint[] array = [light, hour];\nreturn list.Count + array.Length;"), null),
            ("parsing and chars", InMethod("bool ok = int.TryParse(\"42\", out int value);\nreturn ok && !string.IsNullOrEmpty(\"x\") && char.IsDigit('4') ? value : 0;"), null),
            ("own enum", "public enum Circuit { Room, Yard }\npublic static class Board { public static Circuit Pick(bool rain) => rain ? Circuit.Room : Circuit.Yard; public static string Name(Circuit c) => c.ToString(); }", null),
            ("linq when allowed", InMethod("var list = new List<int> { 1, 2, 3 };\nvar doubled = list.Where(x => x > 1).Select(x => x * 2).ToList();\nvar query = from x in list where x > 1 orderby x descending select x;\nreturn doubled.Sum() + query.First();"), new CheckOptions(AllowLinq: true)),
            ("radio program", "Console.WriteLine(\"Rádio Candeia\");\nfor (int i = 0; i < 3; i++) Console.WriteLine(i);", new CheckOptions(ConsoleProgram: true)),
            ("using directives that are allowed", "using System;\nusing System.Collections.Generic;\nusing System.Text;\nusing static System.Math;\npublic static class M { public static double R(double x) => Sqrt(x); }", null),
        };
        foreach (var (name, source, options) in programs)
        {
            var result = Check(source, options);
            t.Check(result.Success, $"accepts {name}: {string.Join(" | ", result.Errors.Select(e => e.Code + " L" + e.Line + " " + e.MessageEn))}");
        }
    }

    public static void Refused(TestRunner t)
    {
        var programs = new (string Name, string Source, string? Code, CheckOptions? Options)[]
        {
            ("file delete", InMethod("System.IO.File.Delete(\"save.json\");\nreturn 0;"), "CND001", null),
            ("file read with using", "using System.IO;\npublic static class X { public static string R() => File.ReadAllText(\"a\"); }", "CND011", null),
            ("file through an alias", "using F = System.IO.File;\npublic static class X { public static bool R() => F.Exists(\"a\"); }", null, null),
            ("global-qualified file", InMethod("return global::System.IO.File.Exists(\"x\") ? 1 : 0;"), "CND001", null),
            ("directory", InMethod("return System.IO.Directory.GetFiles(\"C:\\\\\").Length;"), "CND001", null),
            ("process start", InMethod("System.Diagnostics.Process.Start(\"calc\");\nreturn 0;"), null, null),
            ("environment exit", InMethod("Environment.Exit(0);\nreturn 0;"), "CND001", null),
            ("environment variable", InMethod("return Environment.GetEnvironmentVariable(\"PATH\").Length;"), "CND001", null),
            ("fail fast", InMethod("Environment.FailFast(\"x\");\nreturn 0;"), "CND001", null),
            ("app domain", InMethod("return AppDomain.CurrentDomain.Id;"), "CND001", null),
            ("typeof assembly", InMethod("return typeof(string).Assembly.GetTypes().Length;"), "CND008", null),
            ("get type and methods", InMethod("return \"abc\".GetType().GetMethods().Length;"), "CND001", null),
            ("activator", InMethod("object o = Activator.CreateInstance<object>();\nreturn 0;"), "CND001", null),
            ("type by name", InMethod("var type = Type.GetType(\"System.IO.File\");\nreturn 0;"), "CND001", null),
            ("delegate method", InMethod("Func<int> f = () => 1;\nvar m = f.Method;\nreturn 0;"), "CND001", null),
            ("delegate dynamic invoke", InMethod("Func<int> f = () => 1;\nreturn (int)f.DynamicInvoke();"), "CND001", null),
            ("exception target site", InMethod("var e = new Exception();\nvar site = e.TargetSite;\nreturn 0;"), "CND001", null),
            ("array by type", InMethod("var a = Array.CreateInstance(typeof(int), 3);\nreturn 0;"), null, null),
            ("http client", InMethod("var client = new System.Net.Http.HttpClient();\nreturn 0;"), null, null),
            ("thread", InMethod("var t = new System.Threading.Thread(() => { });\nreturn 0;"), null, null),
            ("task run", InMethod("System.Threading.Tasks.Task.Run(() => { });\nreturn 0;"), "CND006", null),
            ("gc", InMethod("GC.Collect();\nreturn 0;"), "CND001", null),
            ("console read", InMethod("return Console.ReadLine().Length;"), "CND001", null),
            ("console set out", InMethod("Console.SetOut(null);\nreturn 0;"), "CND001", null),
            ("console out", InMethod("Console.Out.WriteLine(1);\nreturn 0;"), "CND001", null),
            ("unsafe pointer", "public static class X { public static unsafe int R() { int v = 1; int* p = &v; return *p; } }", "CND003", null),
            ("stackalloc", InMethod("System.Span<int> s = stackalloc int[4];\nreturn s.Length;"), "CND003", null),
            ("dll import", "public static class X { [System.Runtime.InteropServices.DllImport(\"kernel32\")] static extern void Beep(); }", "CND004", null),
            ("extern", "public static class X { static extern void Beep(); }", "CND010", null),
            ("dynamic", InMethod("dynamic d = 1;\nreturn d;"), "CND005", null),
            ("async await", "public static class X { public static async System.Threading.Tasks.Task R() { await System.Threading.Tasks.Task.Delay(1); } }", "CND006", null),
            ("lock", InMethod("object gate = new object();\nlock (gate) { }\nreturn 0;"), "CND006", null),
            ("reserved name", InMethod("int __steps = 1;\nreturn __steps;"), "CND007", null),
            ("destructor", "public class Ghost { ~Ghost() { } }", "CND009", null),
            ("random", InMethod("return new Random().Next();"), "CND001", null),
            ("clock", InMethod("return DateTime.Now.Hour;"), "CND001", null),
            ("unsafe class", InMethod("return System.Runtime.CompilerServices.Unsafe.SizeOf<int>();"), "CND001", null),
            ("line directive", "#line 1 \"other.cs\"\npublic static class X { }", "CND013", null),
            ("forbidden base class", "public class S : System.IO.MemoryStream { }", "CND001", null),
            ("linq too early", InMethod("var list = new List<int> { 1 };\nreturn list.Where(x => x > 0).Count();"), "CS1061", null),
            ("linq qualified too early", InMethod("var list = new List<int> { 1 };\nreturn System.Linq.Enumerable.Count(list);"), "CND002", null),
            ("linq namespace too early", "using System.Linq;\npublic static class X { }", "CND011", null),
            ("empty", "   ", "CND000", null),
        };
        foreach (var (name, source, code, options) in programs)
        {
            var result = Check(source, options);
            t.Check(!result.Success, $"refuses {name}");
            if (code is not null)
                t.Check(result.Errors.Any(e => e.Code == code), $"refuses {name} with {code}: {string.Join(" | ", result.Errors.Select(e => e.Code))}");
            t.Check(result.Errors.All(e => e.MessagePt.Length > 0 && e.MessageEn.Length > 0 && e.Line >= 1), $"refusal of {name} is explained in both languages");
        }
    }

    public static void Explained(TestRunner t)
    {
        CodeDiagnostic First(string source, string code)
        {
            var result = Check(source);
            return result.Errors.FirstOrDefault(e => e.Code == code)
                ?? throw new Exception($"expected {code}, got {string.Join(" | ", result.Errors.Select(e => e.Code + " " + e.MessageEn))}");
        }

        var semicolon = First(InMethod("int a = 1\nreturn a;"), "CS1002");
        t.Check(semicolon.Line == 5 && semicolon.MessagePt.Contains("ponto e vírgula"), "missing semicolon is explained on its line");
        t.Check(semicolon.Fix is { Line: 5 } fix && fix.Text.TrimEnd().EndsWith("int a = 1;"), "missing semicolon has a fix");

        var typo = First(InMethod("int light2 = light;\nreturn lihgt;"), "CS0103");
        t.Check(typo.MessagePt.Contains("`light`"), "a typo suggests the closest name");
        t.Check(typo.Fix is { } typoFix && typoFix.Text.Contains("return light;"), "a typo has a fix");

        var print = First(InMethod("print(light);\nreturn 0;"), "CS0103");
        t.Check(print.MessagePt.Contains("Console.WriteLine"), "print suggests Console.WriteLine");

        var assign = First(InMethod("if (hour = 18) return 1;\nreturn 0;"), "CS0029");
        t.Check(assign.MessagePt.Contains("`==`"), "assignment in a condition suggests ==");
        t.Check(assign.Fix is { } assignFix && assignFix.Text.Contains("hour == 18"), "assignment in a condition has a fix");

        var narrowing = First(InMethod("int half = light / 2.0;\nreturn half;"), "CS0266");
        t.Check(narrowing.MessagePt.Contains("(int)") && narrowing.MessagePt.Contains("double"), "double to int explains the explicit conversion");

        var text = First(InMethod("int n = \"5\";\nreturn n;"), "CS0029");
        t.Check(text.MessagePt.Contains("int.Parse"), "string to int suggests int.Parse");

        var paths = First(InMethod("if (light > 0) return 1;", "int"), "CS0161");
        t.Check(paths.MessagePt.Contains("`Solve`"), "missing return names the method");

        var unassigned = First(InMethod("int total;\nreturn total;"), "CS0165");
        t.Check(unassigned.MessagePt.Contains("`total`"), "unassigned variable is named");

        var member = First(InMethod("return (int)Math.Sqr(light);"), "CS0117");
        t.Check(member.MessagePt.Contains("`Sqrt`"), "misspelt member suggests the right one");

        var length = First(InMethod("string s = \"abc\";\nreturn s.length;"), "CS1061");
        t.Check(length.MessagePt.Contains("`Length`"), "wrong case member suggests Length");

        var useless = First(InMethod("int x = 0;\nx == 5;\nreturn x;"), "CS0201");
        t.Check(useless.MessagePt.Contains("único `=`") && useless.Fix is { } uselessFix && uselessFix.Text.Contains("x = 5;"), "== as a statement suggests =");

        var linq = First(InMethod("var list = new List<int> { 1 };\nreturn list.Where(x => x > 0).Count();"), "CS1061");
        t.Check(linq.MessagePt.Contains("LINQ") && linq.MessagePt.Contains("Ato 3"), "LINQ before Act 3 is explained");

        var type = First("public static class X { public static Integer N() => 1; }", "CS0246");
        t.Check(type.MessagePt.Contains("`int`"), "unknown type suggests int");

        var loose = Check("Console.WriteLine(1);");
        t.Check(loose.Errors.Any(e => e.Code == "CS8805"), "loose statements are explained when the night expects a class");

        var fallback = First(InMethod("return Math.Max(\"a\", 1);"), "CS1503");
        t.Check(fallback.MessagePt.Length > 0 && fallback.MessagePt != fallback.MessageEn, "other errors fall back to the compiler's Portuguese message");

        var many = Check(InMethod(string.Join("\n", Enumerable.Range(0, 30).Select(i => $"int v{i} = \"x\";")) + "\nreturn 0;"));
        t.Check(many.Errors.Count == CSharpRuntime.MaxErrors, "at most 10 errors are shown");
        t.Check(many.Errors.Select(e => e.Line).SequenceEqual(many.Errors.Select(e => e.Line).Order()), "errors are sorted by line");
    }

    public static void Compilation(TestRunner t)
    {
        t.Check(CSharpRuntime.ReferenceNames.Order().SequenceEqual(["System.Collections.dll", "System.Console.dll", "System.Linq.dll", "System.Runtime.dll"]),
            "the four reference assemblies are embedded");
        var result = Runtime.Compile("public static class Yard { public static bool ShouldLightYard(int light, int hour) => light < 30 || hour >= 18; }", new());
        t.Check(result.Success && result.Assembly!.Length > 0 && result.Pdb!.Length > 0,
            $"a valid night compiles to an assembly and line information: {string.Join(" | ", result.Errors.Select(e => e.Code + " " + e.MessageEn))}");
        if (!result.Success) return;
        // Loads the compiled metadata without running anything, in a context that is unloaded right after.
        var context = new AssemblyLoadContext("check", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(result.Assembly!));
            t.Check(assembly.GetType("Yard")?.GetMethod("ShouldLightYard") is not null, "the compiled assembly exposes the night's method");
        }
        finally { context.Unload(); }
        var refused = Runtime.Compile(InMethod("System.IO.File.Delete(\"x\");\nreturn 0;"), new());
        t.Check(!refused.Success && refused.Assembly is null, "refused code produces no assembly");
        Runtime.Warm();
        t.Check(Runtime.Check(InMethod("return light;"), new()).Elapsed < TimeSpan.FromSeconds(5), "a warm check is fast");
    }
}
