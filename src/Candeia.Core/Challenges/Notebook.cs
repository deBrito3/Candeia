using System.Text.Json;

namespace Candeia.Challenges;

// A page of the notebook left beside the keyboard: a concept explained in the voice of the person who
// wrote CANDEIA. Keywords are the words of the code that open it with F1.
public sealed record NotebookPage(string Id, int Act, IReadOnlyList<string> Keywords, Bilingual Title, Bilingual Text);

// The notebook, read from content/notebook/: pages.json lists the pages and their keywords; each page is
// <id>.pt.md and <id>.en.md, starting with its "# Title". Remembering a concept never costs anything.
public static class Notebook
{
    public sealed record LoadResult(IReadOnlyList<NotebookPage> Pages, IReadOnlyList<string> Errors)
    {
        public bool Ok => Errors.Count == 0 && Pages.Count > 0;
    }

    public static string DefaultRoot => Path.Combine(AppContext.BaseDirectory, "content", "notebook");

    public static LoadResult Load(string? root = null)
    {
        root ??= DefaultRoot;
        var errors = new List<string>();
        var pages = new List<NotebookPage>();
        string index = Path.Combine(root, "pages.json");
        if (!File.Exists(index)) return new([], [$"pages.json: missing in {root}"]);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(index), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            foreach (var entry in document.RootElement.GetProperty("pages").EnumerateArray())
            {
                string id = entry.GetProperty("id").GetString() ?? "";
                int act = entry.TryGetProperty("act", out var a) ? a.GetInt32() : 1;
                var keywords = entry.TryGetProperty("keywords", out var k) ? k.EnumerateArray().Select(w => w.GetString() ?? "").Where(w => w.Length > 0).ToList() : [];
                var pt = Page(root, id, "pt", errors);
                var en = Page(root, id, "en", errors);
                if (pt is null || en is null) continue;
                pages.Add(new(id, act, keywords, new(pt.Value.Title, en.Value.Title), new(pt.Value.Text, en.Value.Text)));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            errors.Add($"pages.json: {ex.Message}");
        }
        foreach (var repeated in pages.SelectMany(p => p.Keywords.Select(k => (k, p.Id))).GroupBy(p => p.k).Where(g => g.Count() > 1))
            errors.Add($"keyword {repeated.Key} opens more than one page: {string.Join(", ", repeated.Select(r => r.Id))}");
        foreach (var repeated in pages.GroupBy(p => p.Id).Where(g => g.Count() > 1))
            errors.Add($"page {repeated.Key} is listed {repeated.Count()} times");
        return new(pages, errors);
    }

    static (string Title, string Text)? Page(string root, string id, string language, List<string> errors)
    {
        string file = $"{id}.{language}.md";
        string path = Path.Combine(root, file);
        if (!File.Exists(path)) { errors.Add($"{file}: missing"); return null; }
        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || !lines[0].StartsWith("# ", StringComparison.Ordinal)) { errors.Add($"{file}: must start with \"# Title\""); return null; }
        string text = string.Join("\n", lines.Skip(1)).Trim();
        if (text.Length == 0) { errors.Add($"{file}: empty page"); return null; }
        return (lines[0][2..].Trim(), text);
    }

    // The page a word of the code opens, if any: "int", "return", "Math", or an operator such as "%".
    public static NotebookPage? For(IReadOnlyList<NotebookPage> pages, string word) =>
        word.Length == 0 ? null : pages.FirstOrDefault(p => p.Keywords.Contains(word, StringComparer.Ordinal));

    static readonly string[] TwoCharacterOperators = ["&&", "||", "==", "!=", "<=", ">="];

    // The word of the code around a position: a name or keyword, or the operator there.
    public static string WordAt(string code, int position)
    {
        if (code.Length == 0) return "";
        position = Math.Clamp(position, 0, code.Length);
        static bool Letter(char c) => char.IsLetterOrDigit(c) || c == '_';
        int start = position, end = position;
        while (start > 0 && Letter(code[start - 1])) start--;
        while (end < code.Length && Letter(code[end])) end++;
        if (end > start) return code[start..end];
        // An operator: the two-character ones first, with the caret before, inside or after them.
        foreach (int at in new[] { position - 1, position, position - 2 })
            if (at >= 0 && at + 2 <= code.Length && TwoCharacterOperators.Contains(code.Substring(at, 2))) return code.Substring(at, 2);
        foreach (int at in new[] { position, position - 1 })
            if (at >= 0 && at < code.Length && "+-*/%=\"<>!".Contains(code[at])) return code[at].ToString();
        return "";
    }
}
