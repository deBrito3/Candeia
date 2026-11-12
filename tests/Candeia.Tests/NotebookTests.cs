using Candeia.Challenges;

namespace Candeia.Tests;

// The notebook: the word under the caret that F1 looks up, and the page it opens.
static class NotebookTests
{
    public static void Run(TestRunner t)
    {
        string code = "if (hour >= 18 || hour < 6 && !open) return x == 2;";
        int At(string piece, int offset = 0) => code.IndexOf(piece, StringComparison.Ordinal) + offset;
        t.Check(Notebook.WordAt(code, At("if", 1)) == "if" && Notebook.WordAt(code, At("hour", 4)) == "hour", "a name or keyword under the caret");
        t.Check(Notebook.WordAt(code, At(">=")) == ">=" && Notebook.WordAt(code, At(">=", 1)) == ">=" && Notebook.WordAt(code, At(">=", 2)) == ">=", "a two-character operator, with the caret before, inside or after it");
        t.Check(Notebook.WordAt(code, At("||", 1)) == "||" && Notebook.WordAt(code, At("&&", 1)) == "&&" && Notebook.WordAt(code, At("==", 1)) == "==", "&&, || and == are read whole");
        t.Check(Notebook.WordAt(code, At("< 6")) == "<" && Notebook.WordAt(code, At("!open")) == "!", "a single < or !");
        t.Check(Notebook.WordAt("x = 5", 3) == "=" && Notebook.WordAt("a % b", 2) == "%" && Notebook.WordAt("  ", 1) == "", "= and % alone, and nothing on blanks");

        var notebook = Notebook.Load();
        t.Check(notebook.Errors.Count == 0, "the notebook loads: " + string.Join("; ", notebook.Errors));
        string? Title(string word) => Notebook.For(notebook.Pages, word)?.Title.Pt;
        t.Check(Title("if")?.StartsWith("if") == true && Title("else")?.StartsWith("else") == true, "if and else have their pages");
        t.Check(Title(">=") == Title("==") && Title("==")?.StartsWith("Compara") == true && Title("||") == Title("!") && Title("&&")?.StartsWith("E, ou") == true, "comparisons and logic have their pages");
        t.Check(Title("=") != Title("=="), "= (store) and == (compare) open different pages");
        var secrets = notebook.Pages.Where(p => p.Secret > 0).ToList();
        t.Check(secrets.Select(p => p.Secret).SequenceEqual([6, 12]) && secrets.All(p => p.Keywords.Count == 0 && p.Text.Pt.Length > 200 && p.Text.En.Length > 200), "two secret pages, opened by 6 and 12 marks, with no F1 keywords");
        t.Check(notebook.Pages.SkipWhile(p => p.Secret == 0).All(p => p.Secret > 0), "the secret pages come last in the index");
    }
}
