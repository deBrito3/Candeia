using Candeia.Challenges;
using Candeia.Night;
using Candeia.Languages;
using Candeia.Languages.CSharp;

namespace Candeia.Tests;

// The night format: the real content loads, and broken nights are refused with a reason.
static class NightLoaderTests
{
    const string Valid = """
        {
          "id": "test-night", "act": 1, "order": 3, "type": "repair",
          "title": { "pt": "Teste", "en": "Test" },
          "learned": { "pt": "Um if decide.", "en": "An if decides." },
          "class": "Yard",
          "methods": [ { "name": "ShouldLightYard", "returns": "bool", "params": [ { "name": "light", "type": "int" }, { "name": "hour", "type": "int" } ] } ],
          "concepts": ["if"],
          "tests": [
            { "args": [10, 12], "expected": true },
            { "args": [80, 12], "expected": false, "hidden": true },
            { "args": [80, 20], "expected": true, "hidden": true }
          ]
        }
        """;

    public static void Run(TestRunner t)
    {
        var all = NightLoader.LoadAll();
        t.Check(all.Count >= 5 && all.All(r => r.Ok), "the content folder loads: " + string.Join("; ", all.SelectMany(r => r.Errors)));
        t.Check(all.Select(r => r.Night?.Id).SequenceEqual(["act1-01-room-light", "act1-02-system-battery", "act1-03-yard-light", "act1-04-gate-lock", "act1-05-house-wakes"]), "the five nights of Act 1 load in order");
        t.Check(all[3].Night!.Type == "corrupted" && all[4].Night!.Type == "siege", "night 4 is corrupted code and night 5 the siege");
        var first = all[0].Night!;
        t.Check(first.Intro && first.Tests.Count == 4 && first.Tests[1].Method == "Voltage", "a test can name its method");
        t.Check(first.Hints.Count == 3 && first.Hints[2].Pt.Contains("___") && first.Hints[2].En.Contains("___"), "the third hint layer is a skeleton with blanks");
        t.Check(first.Events is [{ On: "first-new-lamp", Effect: "silhouette", Where: "yard" }], "events load");
        // Vigil reads a part from zero before the first night's LEIA-ME; the other difficulty does not.
        t.Check(first.Beginner is not null && first.ReadmeFor(Difficulty.Vigil).Pt.StartsWith("# Do zero", StringComparison.Ordinal)
            && first.ReadmeFor(Difficulty.Vigil).En.StartsWith("# From zero", StringComparison.Ordinal) && first.ReadmeFor(Difficulty.Vigil).Pt.Contains(first.Readme.Pt),
            "in Vigil, the first night's LEIA-ME starts from zero");
        t.Check(first.ReadmeFor(Difficulty.Night) == first.Readme && all[1].Night!.Beginner is null && all[1].Night!.ReadmeFor(Difficulty.Vigil) == all[1].Night!.Readme, "in Night, and on nights without that part, the LEIA-ME is the same");

        t.Check(Load(Valid).Ok, "a valid night loads");
        (string Name, string Json, string Expected)[] broken =
        [
            ("bad type", Valid.Replace("\"repair\"", "\"puzzle\""), "type"),
            ("unknown method", Valid.Replace("\"args\": [10, 12]", "\"method\": \"Nope\", \"args\": [10, 12]"), "unknown method"),
            ("wrong argument count", Valid.Replace("[10, 12]", "[10]"), "arguments"),
            ("argument of the wrong type", Valid.Replace("[10, 12]", "[\"dez\", 12]"), "does not fit int"),
            ("expected of the wrong type", Valid.Replace("\"expected\": true }", "\"expected\": 1 }"), "does not fit bool"),
            ("too few hidden tests", Valid.Replace("\"expected\": true, \"hidden\": true", "\"expected\": true"), "hidden"),
            ("unknown concept", Valid.Replace("[\"if\"]", "[\"magic\"]"), "unknown concept"),
            ("unsupported type", Valid.Replace("\"returns\": \"bool\"", "\"returns\": \"Stream\""), "unsupported"),
            ("invalid json", Valid.Replace("}", ""), "invalid JSON"),
        ];
        foreach (var (name, json, expected) in broken)
        {
            var result = Load(json);
            t.Check(!result.Ok && result.Errors.Any(e => e.Contains(expected, StringComparison.Ordinal)), $"refuses a night with {name}: {string.Join("; ", result.Errors)}");
        }
        var noHints = Load(Valid, hints: "## 1\nidea\n## 2\ncommands");
        t.Check(!noHints.Ok && noHints.Errors.Any(e => e.Contains("three")), "refuses hints without three layers");

        var runtime = new CSharpRuntime();
        var marks = new Dictionary<string, long> { ["if"] = 0 };
        string code = "public static class C { public static int F(int a) { var x = a % 2; double y = a / 2.0; return x; } }";
        t.Check(runtime.MissingConcepts(code, ["declare:int", "declare:double", "operator:%", "operator:/"], marks, new()).Count == 0, "declared types (var included) and operators are found");
        t.Check(runtime.MissingConcepts(code, ["declare:bool", "operator:*", "if"], marks, new()).SequenceEqual(["declare:bool", "operator:*", "if"]), "missing concepts are listed");
        t.Check(runtime.MissingConcepts(code, ["if"], new Dictionary<string, long> { ["if"] = 3 }, new()).Count == 0, "a mark counts once it ran");
    }

    static NightLoader.LoadResult Load(string json, string hints = "## 1\nidea\n## 2\ncommands\n## 3\nskeleton ___")
    {
        string folder = Path.Combine(Path.GetTempPath(), "candeia-night-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "night.json"), json);
            File.WriteAllText(Path.Combine(folder, "starter.cs"), "public static class Yard { }");
            File.WriteAllText(Path.Combine(folder, "readme.pt.md"), "Leia-me");
            File.WriteAllText(Path.Combine(folder, "readme.en.md"), "Readme");
            File.WriteAllText(Path.Combine(folder, "hints.pt.md"), hints);
            File.WriteAllText(Path.Combine(folder, "hints.en.md"), hints);
            return NightLoader.Load(folder);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
