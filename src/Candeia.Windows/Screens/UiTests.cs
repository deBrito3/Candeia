using System.Diagnostics;
using System.Text;
using Candeia.Challenges;
using Candeia.Night;
using Candeia.Panels;

namespace Candeia.Screens;

// Checks of the night screen that need real windows: the editor's colours, indentation and undo, the
// completion, the LEIA-ME and the night's keys. Opens the screen for a few seconds and writes PASS or
// the failures to a file.
static class UiTests
{
    public static int Run(IReadOnlyList<NightDefinition> nights, string reportPath)
    {
        using var screen = new NightScreen(nights[0], nights, benchmark: true);
        var failures = new List<string>();
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (condition) passed++;
            else failures.Add(name);
        }
        screen.Shown += async (_, _) =>
        {
            try { await Checks(screen, Check); }
            catch (Exception ex) { failures.Add($"{ex.GetType().Name}: {ex.Message}"); }
            finally { screen.Close(); }
        };
        Application.Run(screen);
        string report = failures.Count == 0 ? $"PASS: {passed} UI checks." : $"FAIL: {failures.Count} of {passed + failures.Count} UI checks.\n" + string.Join("\n", failures.Select(f => "  - " + f));
        File.WriteAllText(reportPath, report + "\n", new UTF8Encoding(false));
        return failures.Count == 0 ? 0 : 1;
    }

    static async Task Checks(NightScreen screen, Action<bool, string> check)
    {
        await Task.Delay(1500);
        var editor = screen.Editor;
        var night = screen.Session.Night;
        string text = editor.Text;
        Color At(string piece, int nth = 0)
        {
            int at = -1;
            for (int i = 0; i <= nth; i++) at = text.IndexOf(piece, at + 1, StringComparison.Ordinal);
            editor.Select(at, 1);
            return editor.SelectionColor;
        }
        check(At("public").ToArgb() == CodeEditor.KeywordColor.ToArgb(), "keywords are coloured");
        check(At("int ").ToArgb() == CodeEditor.TypeColor.ToArgb(), "types are coloured");
        check(At("60").ToArgb() == CodeEditor.NumberColor.ToArgb(), "numbers are coloured");
        check(At("// CANDEIA").ToArgb() == CodeEditor.CommentColor.ToArgb(), "comments are dim");
        check(At("RoomLight").ToArgb() == CodeEditor.NameColor.ToArgb(), "names keep the plain colour");

        // Colouring keeps the selection and stays out of the undo history.
        editor.Select(text.IndexOf("RoomLight", StringComparison.Ordinal), 4);
        editor.Highlight();
        check(editor.SelectionLength == 4, "colouring keeps the selection");
        editor.Select(editor.TextLength, 0);
        editor.SelectedText = "\n// x";
        editor.Highlight();
        editor.Undo();
        check(editor.Text == text, "undo removes the typing, not the colours");

        // Indentation follows braces.
        editor.Load("class A\n{");
        editor.Select(editor.TextLength, 0);
        editor.NewLine();
        check(editor.Text == "class A\n{\n    ", $"Enter after a brace indents: '{editor.Text.Replace("\n", "\\n")}'");
        editor.NewLine();
        check(editor.Text.EndsWith("\n    \n    ", StringComparison.Ordinal), "Enter keeps the indentation");
        check(editor.CloseBrace() && editor.Text.EndsWith("\n}", StringComparison.Ordinal), "a closing brace steps back");
        editor.Load("x");
        editor.Select(1, 0);
        editor.InsertTab();
        check(editor.Text == "x   ", "Tab goes to the next stop of four");

        // Completion.
        check(CodeCompletion.Matches("Ma", "").Contains("Math.Max") && CodeCompletion.Matches("ret", "").SequenceEqual(["return"]), "completion offers the language and Math");
        check(CodeCompletion.Matches("wa", "int watts = 60;").Contains("watts"), "completion offers the names in the code");
        check(CodeCompletion.Prefix("// Ma", 5, out _) == "" && CodeCompletion.Prefix("s = \"Ma", 7, out _) == "" && CodeCompletion.Prefix("x = Math.M", 10, out _) == "Math.M", "no completion inside comments or strings");

        // The LEIA-ME shows its table without the Markdown marks.
        string readme = screen.Info.ReadmeBox.Text;
        check(readme.Contains("Método") && readme.Contains("Watts()") && !readme.Contains('`') && !readme.Contains("**"), "the LEIA-ME is shown without Markdown marks");

        // The night's keys.
        editor.Load(night.Starter);
        check(screen.Session.Clock.State == ClockState.Intro, "the night waits in its introduction");
        check(screen.Command(Keys.Enter) && screen.Session.Clock.State == ClockState.Coding, "Enter starts the night");
        check(screen.Command(Keys.Escape) && screen.Paused && screen.Session.Clock.Paused && editor.ReadOnly, "Esc pauses the night and the editor");
        await Task.Delay(300);
        double remaining = screen.Session.Clock.Remaining;
        await Task.Delay(500);
        check(screen.Session.Clock.Remaining == remaining, "a paused night keeps its time");
        check(screen.Command(Keys.Escape) && !screen.Paused && !editor.ReadOnly, "Esc again resumes");
        check(screen.Command(Keys.F5) && screen.Session.Busy && screen.Info.Current == InfoPane.Tab.Output, "F5 runs and shows the output");
        var watch = Stopwatch.StartNew();
        while (screen.Session.Busy && watch.Elapsed.TotalSeconds < 15) await Task.Delay(50);
        check(screen.Info.OutputBox.Text.Contains("1 de 4 lâmpadas acesas"), "the output tells the result");
        check(screen.Command(Keys.Control | Keys.D1) && screen.Info.Current == InfoPane.Tab.Readme, "Ctrl+1 shows the LEIA-ME");
        check(screen.Command(Keys.Control | Keys.D2) && screen.Info.Current == InfoPane.Tab.Output, "Ctrl+2 shows the output");

        // The hints open one layer at a time, with their code in colour and no Markdown marks.
        check(screen.Command(Keys.Control | Keys.D3) && screen.Info.Current == InfoPane.Tab.Hints && screen.Session.HintsShown == 1, "Ctrl+3 opens the first hint");
        string first = screen.Info.HintsBox.Text;
        check(first.Contains("Dica 1 de 3") && first.Contains("Ctrl+3 abre a dica 2") && !first.Contains("Dica 2 de 3"), "only the first layer shows");
        screen.Command(Keys.Control | Keys.D3);
        screen.Command(Keys.Control | Keys.D3);
        string all = screen.Info.HintsBox.Text;
        check(screen.Session.HintsShown == 3 && all.Contains("Dica 3 de 3") && !all.Contains("Ctrl+3 abre"), "each press opens the next layer, up to three");
        check(!all.Contains("```") && !all.Contains('`') && all.Contains("double volts = ___;"), "code blocks show as code, without Markdown marks");
        var hintsBox = screen.Info.HintsBox;
        int keyword = hintsBox.Text.IndexOf("double volts", StringComparison.Ordinal);
        hintsBox.Select(keyword, 6);
        check(hintsBox.SelectionColor.ToArgb() == CodeEditor.TypeColor.ToArgb(), "code in the hints is coloured like the editor");
    }
}
