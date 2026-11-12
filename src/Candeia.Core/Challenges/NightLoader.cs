using System.Text.Json;
using Candeia.Execution;

namespace Candeia.Challenges;

// Reads a night's folder and checks it against the format. Messages are for whoever writes the content,
// so they are in English and name the file and the field.
//   night.json      what the house system calls and expects
//   starter.cs      the code the player finds on the old computer
//   readme.pt.md    the LEIA-ME, in each language
//   readme.en.md
//   hints.pt.md     three hint layers ("## 1", "## 2", "## 3"): the idea, the commands, a skeleton
//   hints.en.md
public static class NightLoader
{
    public static readonly string[] Types = ["repair", "corrupted", "decode", "radio", "siege"];
    public static readonly string[] ValueTypes =
    [
        "int", "long", "double", "bool", "string", "char", "int[]", "long[]", "double[]", "bool[]", "string[]", "char[]",
        "List<int>", "List<double>", "List<string>", "Dictionary<string,int>", "Dictionary<string,double>",
    ];
    public static readonly string[] Operators = ["+", "-", "*", "/", "%", "==", "!=", "<", ">", "<=", ">=", "&&", "||", "!"];
    public static readonly string[] Triggers = ["start", "first-new-lamp", "all-lamps"];
    public static readonly string[] Effects = ["silhouette", "sound"];
    public static readonly string[] Places = ["street", "gate", "yard", "window", "door", "beside"];

    public sealed record LoadResult(string Folder, NightDefinition? Night, IReadOnlyList<string> Errors)
    {
        public bool Ok => Night is not null && Errors.Count == 0;
    }

    public static string DefaultRoot => Path.Combine(AppContext.BaseDirectory, "content", "nights");

    // Every folder under root that holds a night.json, in path order (act1/01-..., act1/02-...).
    public static IReadOnlyList<LoadResult> LoadAll(string? root = null)
    {
        root ??= DefaultRoot;
        if (!Directory.Exists(root)) return [new(root, null, [$"content folder not found: {root}"])];
        var results = Directory.EnumerateFiles(root, "night.json", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName).OfType<string>().Order(StringComparer.Ordinal).Select(Load).ToList();
        foreach (var duplicate in results.Where(r => r.Night is not null).GroupBy(r => r.Night!.Id).Where(g => g.Count() > 1))
            results.Add(new(duplicate.First().Folder, null, [$"id {duplicate.Key} is used by {duplicate.Count()} nights"]));
        return results;
    }

    public static LoadResult Load(string folder)
    {
        var errors = new List<string>();
        string? Read(string file)
        {
            string path = Path.Combine(folder, file);
            if (File.Exists(path)) return File.ReadAllText(path);
            errors.Add($"{file}: missing");
            return null;
        }
        string? json = Read("night.json"), starter = Read("starter.cs"), readmePt = Read("readme.pt.md"), readmeEn = Read("readme.en.md"),
            hintsPt = Read("hints.pt.md"), hintsEn = Read("hints.en.md");
        if (json is null) return new(folder, null, errors);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var night = Parse(document.RootElement.Clone(), errors, folder, starter ?? "", readmePt ?? "", readmeEn ?? "", hintsPt ?? "", hintsEn ?? "");
            return new(folder, errors.Count == 0 ? night : null, errors);
        }
        catch (JsonException ex)
        {
            errors.Add($"night.json: invalid JSON: {ex.Message}");
            return new(folder, null, errors);
        }
    }

    static NightDefinition? Parse(JsonElement root, List<string> errors, string folder, string starter, string readmePt, string readmeEn, string hintsPt, string hintsEn)
    {
        string id = Text(root, "id", errors);
        int act = Number(root, "act", errors, 1, 4), order = Number(root, "order", errors, 1, 9);
        string type = Text(root, "type", errors);
        if (type.Length > 0 && !Types.Contains(type)) errors.Add($"night.json: type \"{type}\" is not one of {string.Join(", ", Types)}");
        bool intro = root.TryGetProperty("intro", out var introValue) && introValue.ValueKind == JsonValueKind.True;
        var title = new Bilingual(Text(Child(root, "title", errors), "pt", errors, "title.pt"), Text(Child(root, "title", errors), "en", errors, "title.en"));
        // One line for the notebook, after the night is won: what it taught.
        var learned = new Bilingual(Text(Child(root, "learned", errors), "pt", errors, "learned.pt"), Text(Child(root, "learned", errors), "en", errors, "learned.en"));
        string @class = Text(root, "class", errors);

        var methods = new List<NightMethod>();
        foreach (var method in Array(root, "methods", errors))
        {
            string name = Text(method, "name", errors, "methods[].name"), returns = Text(method, "returns", errors, "methods[].returns");
            if (returns.Length > 0 && returns != "void" && !ValueTypes.Contains(returns)) errors.Add($"night.json: method {name} returns unsupported type {returns}");
            var parameters = new List<NightParameter>();
            foreach (var parameter in Array(method, "params", errors))
            {
                string parameterType = Text(parameter, "type", errors, $"{name}.params[].type");
                if (!ValueTypes.Contains(parameterType)) errors.Add($"night.json: parameter of {name} has unsupported type {parameterType}");
                parameters.Add(new(Text(parameter, "name", errors, $"{name}.params[].name"), parameterType));
            }
            methods.Add(new(name, returns, parameters));
        }
        if (methods.Count == 0) errors.Add("night.json: methods is empty");
        if (methods.GroupBy(m => m.Name).Any(g => g.Count() > 1)) errors.Add("night.json: two methods share a name");

        var concepts = Array(root, "concepts", errors, optional: true).Select(c => c.GetString() ?? "").ToList();
        foreach (var concept in concepts)
            if (!ConceptKnown(concept)) errors.Add($"night.json: unknown concept \"{concept}\"");

        var tests = new List<TestCase>();
        foreach (var test in Array(root, "tests", errors))
        {
            string method = test.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()! : methods.FirstOrDefault()?.Name ?? "";
            var target = methods.FirstOrDefault(x => x.Name == method);
            if (target is null) { errors.Add($"night.json: test {tests.Count} calls unknown method {method}"); continue; }
            var args = Array(test, "args", errors, optional: true).ToArray();
            if (args.Length != target.Parameters.Count) errors.Add($"night.json: test {tests.Count} passes {args.Length} arguments to {method}, which takes {target.Parameters.Count}");
            for (int i = 0; i < Math.Min(args.Length, target.Parameters.Count); i++)
                if (!Fits(args[i], target.Parameters[i].Type)) errors.Add($"night.json: test {tests.Count} argument {i} does not fit {target.Parameters[i].Type}");
            if (!test.TryGetProperty("expected", out var expected)) { errors.Add($"night.json: test {tests.Count} has no expected value"); continue; }
            if (target.Returns != "void" && !Fits(expected, target.Returns)) errors.Add($"night.json: test {tests.Count} expects a value that does not fit {target.Returns}");
            bool hidden = test.TryGetProperty("hidden", out var h) && h.ValueKind == JsonValueKind.True;
            tests.Add(new TestCase(args, expected, hidden, method));
        }
        if (tests.Count == 0) errors.Add("night.json: tests is empty");
        if (!intro && tests.Count(t => t.Hidden) < 2) errors.Add("night.json: a night needs at least two hidden tests (only the introduction night may have none)");
        if (tests.Count(t => !t.Hidden) == 0) errors.Add("night.json: every test is hidden; the player needs to see at least one");

        double tolerance = root.TryGetProperty("tolerance", out var tol) && tol.ValueKind == JsonValueKind.Number ? tol.GetDouble() : 1e-9;
        var limits = new RunLimits();
        if (root.TryGetProperty("limits", out var limitValues) && limitValues.ValueKind == JsonValueKind.Object)
        {
            if (limitValues.TryGetProperty("stepsPerTest", out var stepsValue) && stepsValue.TryGetInt64(out long steps) && steps > 0) limits = limits with { StepsPerTest = steps };
            if (limitValues.TryGetProperty("wallClockMs", out var wall) && wall.TryGetInt32(out int ms) && ms > 0) limits = limits with { WallClockMilliseconds = ms };
        }

        var events = new List<NightEvent>();
        foreach (var e in Array(root, "events", errors, optional: true))
        {
            string on = Text(e, "on", errors, "events[].on"), effect = Text(e, "effect", errors, "events[].effect"), where = Text(e, "where", errors, "events[].where");
            if (!Triggers.Contains(on)) errors.Add($"night.json: event trigger \"{on}\" is not one of {string.Join(", ", Triggers)}");
            if (!Effects.Contains(effect)) errors.Add($"night.json: event effect \"{effect}\" is not one of {string.Join(", ", Effects)}");
            if (!Places.Contains(where)) errors.Add($"night.json: event place \"{where}\" is not one of {string.Join(", ", Places)}");
            events.Add(new(on, effect, where, e.TryGetProperty("seconds", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetDouble() : 1));
        }

        if (starter.Trim().Length == 0) errors.Add("starter.cs: empty");
        if (readmePt.Trim().Length == 0 || readmeEn.Trim().Length == 0) errors.Add("readme: both languages need text");
        var pt = HintLayers(hintsPt, "hints.pt.md", errors);
        var en = HintLayers(hintsEn, "hints.en.md", errors);
        if (errors.Count > 0) return null;
        return new(id, act, order, type, intro, title, @class, methods, concepts, tests, tolerance, limits, events, starter,
            new(readmePt.Trim(), readmeEn.Trim()), pt.Zip(en, (p, e) => new Bilingual(p, e)).ToList(), folder) { Learned = learned };
    }

    // Requirements a night can make: a mark the code must run (if, for...), a local variable of a type it
    // must declare ("declare:double"), or an operator it must use ("operator:%").
    public static bool ConceptKnown(string concept) =>
        Probe.MarkNames.Contains(concept)
        || concept.StartsWith("declare:", StringComparison.Ordinal) && ValueTypes.Contains(concept["declare:".Length..])
        || concept.StartsWith("operator:", StringComparison.Ordinal) && Operators.Contains(concept["operator:".Length..]);

    static List<string> HintLayers(string text, string file, List<string> errors)
    {
        var layers = new List<string>();
        var current = new List<string>();
        bool started = false;
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (started) layers.Add(string.Join("\n", current).Trim());
                current.Clear();
                started = true;
            }
            else if (started) current.Add(line);
        }
        if (started) layers.Add(string.Join("\n", current).Trim());
        if (layers.Count != 3 || layers.Any(l => l.Length == 0)) errors.Add($"{file}: needs three non-empty layers (## 1, ## 2, ## 3), found {layers.Count}");
        return layers;
    }

    // Whether a JSON value can become the neutral type.
    static bool Fits(JsonElement value, string type)
    {
        if (type.EndsWith("[]", StringComparison.Ordinal)) return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(v => Fits(v, type[..^2]));
        if (type.StartsWith("List<", StringComparison.Ordinal)) return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(v => Fits(v, type[5..^1]));
        if (type.StartsWith("Dictionary<string,", StringComparison.Ordinal))
            return value.ValueKind == JsonValueKind.Object && value.EnumerateObject().All(p => Fits(p.Value, type["Dictionary<string,".Length..^1]));
        return type switch
        {
            "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "long" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "double" => value.ValueKind == JsonValueKind.Number,
            "bool" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "string" => value.ValueKind is JsonValueKind.String or JsonValueKind.Null,
            "char" => value.ValueKind == JsonValueKind.String && value.GetString()!.Length == 1,
            _ => false,
        };
    }

    static JsonElement Child(JsonElement element, string name, List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.Object) return child;
        if (!errors.Contains($"night.json: {name} must be an object")) errors.Add($"night.json: {name} must be an object");
        return default;
    }

    static string Text(JsonElement element, string name, List<string> errors, string? label = null)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()!.Trim().Length > 0)
            return value.GetString()!;
        if (element.ValueKind == JsonValueKind.Object) errors.Add($"night.json: {label ?? name} must be a non-empty string");
        return "";
    }

    static int Number(JsonElement element, string name, List<string> errors, int min, int max)
    {
        if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out int number) && number >= min && number <= max) return number;
        errors.Add($"night.json: {name} must be a whole number from {min} to {max}");
        return min;
    }

    static IEnumerable<JsonElement> Array(JsonElement element, string name, List<string> errors, bool optional = false)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().ToList();
        if (!optional || element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _)) errors.Add($"night.json: {name} must be an array");
        return [];
    }
}
