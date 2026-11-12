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
        string last = "none";
        bool finished = false;
        void Check(bool condition, string name)
        {
            if (condition) passed++;
            else failures.Add(name);
            last = name;
        }
        screen.Shown += async (_, _) =>
        {
            try { await Checks(screen, Check); finished = true; }
            catch (Exception ex) { failures.Add($"{ex.GetType().Name}: {ex.Message}"); }
            finally { screen.Close(); }
        };
        Application.Run(screen);
        // The window can close before the checks end (an error in a frame, say): that is a failure too.
        if (!finished && failures.Count == 0) failures.Add($"the checks stopped before the end, after “{last}”");
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

        // The sweet: the first Ctrl+3 shows the tab and the price; pressing again, there, takes it.
        var session = screen.Session;
        check(screen.Command(Keys.Control | Keys.D3) && screen.Info.Current == InfoPane.Tab.Hints && session.HintsShown == 0, "Ctrl+3 shows the hints without taking a sweet");
        string price = screen.Info.HintsBox.Text;
        check(price.Contains("aceita o doce: a dica 1 de 3, por −") && price.Contains("Sem doce") && price.Contains("CADERNO"), "the hints tell the sweet's price and that the notebook is free");
        double before = session.Clock.Remaining;
        check(screen.Command(Keys.Control | Keys.D3) && session.HintsShown == 1 && session.Clock.SweetsAccepted == 1, "Ctrl+3 again takes the sweet and opens the first hint");
        check(session.HintCost > 0 && Math.Abs(before - session.Clock.Remaining - session.HintCost) < 0.5, "the sweet costs its price on the clock");
        string first = screen.Info.HintsBox.Text;
        check(first.Contains("Dica 1 de 3") && first.Contains("a dica 2 de 3, por −") && !first.Contains("Dica 2 de 3"), "only the first layer shows");

        // The notebook: F1 opens the page of the word under the caret, Ctrl+4 the index, a double click a page.
        editor.Select(editor.Text.IndexOf("return watts", StringComparison.Ordinal) + 2, 0);
        check(screen.Command(Keys.F1) && screen.Info.Current == InfoPane.Tab.Notebook && screen.Info.NotebookBox.Text.StartsWith("return: a resposta", StringComparison.Ordinal), "F1 opens the notebook page of the word under the caret");
        check(screen.Command(Keys.Control | Keys.D4) && screen.Info.NotebookBox.Text.StartsWith("O caderno", StringComparison.Ordinal), "Ctrl+4 shows the notebook's index");
        var index = screen.Info.NotebookBox.Text.Split('\n');
        screen.Info.ChoosePage(Array.FindIndex(index, l => l.Contains("double: números")));
        check(screen.Info.NotebookBox.Text.StartsWith("double: números", StringComparison.Ordinal), "a line of the index opens its page");
        check(session.Clock.SweetsAccepted == 1, "the notebook costs nothing");

        // CORRIGIR: an error with a fix gets it with a double click on its line, as one undoable edit.
        string starter = editor.Text, broken = starter.Replace("return watts;", "return watts", StringComparison.Ordinal);
        editor.Load(broken);
        check(screen.Command(Keys.F5), "F5 checks the broken code");
        watch.Restart();
        while (session.Busy && watch.Elapsed.TotalSeconds < 15) await Task.Delay(50);
        var shown = screen.Info.OutputBox.Text.Split('\n');
        int fixLine = Array.FindIndex(shown, l => l.Contains("[CORRIGIR: duplo clique]"));
        check(fixLine >= 0, "a missing semicolon offers CORRIGIR");
        screen.Info.ChooseOutput(fixLine);
        check(editor.Text == starter, "CORRIGIR puts the semicolon back");
        editor.Undo();
        check(editor.Text == broken, "CORRIGIR is undone in one step");
        editor.Load(starter);

        // The scare timeline: the capture takes the whole screen for a moment; the soft one never does, and
        // strikes no match.
        check(NightScreen.Scare(2.5, capture: true, soft: false) is { Panels: false, Lit: 1, Sack: > 0 } && NightScreen.Scare(4.5, true, false).Panels, "the capture hides the panels while it strikes, then brings them back");
        check(Enumerable.Range(0, 60).All(i => NightScreen.Scare(i * 0.1, true, true).Panels && NightScreen.Scare(i * 0.1, false, true).Match == 0), "soft scares keep the panels and strike no match");
        check(NightScreen.Scare(1.5, capture: false, soft: false).Match > 0.5f && NightScreen.Scare(0.8, false, false).Apparition == Station.Beside, "the near capture shows him beside you, then strikes a match");

        // Every candle goes out: the screen is his for a moment, then the message; Enter starts over with the
        // code and a free page of hints.
        watch.Restart();
        while (session.Clock.State != ClockState.Captured && watch.Elapsed.TotalSeconds < 30)
        {
            if (session.Clock.State is ClockState.Coding or ClockState.Respite && !session.Busy)
                screen.Skip(Math.Max(session.Clock.Remaining, session.Penalties.Rules.RespiteSeconds) + 0.5);
            await Task.Delay(150);
        }
        check(session.Clock.State == ClockState.Captured, "losing every candle ends in the capture");
        await Task.Delay(2300);
        check(!editor.Visible && !screen.Info.Visible, "the capture takes the whole screen");
        await Task.Delay(2200);
        check(editor.Visible && screen.Info.OutputBox.Text.Contains("Ele te pegou"), "then the panels come back with the message");
        screen.Command(Keys.Control | Keys.D3);
        screen.Command(Keys.Control | Keys.D3);
        check(!session.HintAvailable && session.HintsShown == 1, "no sweet once captured");
        check(screen.Command(Keys.Enter) && session.Clock.State == ClockState.Intro && session.Attempt == 2 && editor.Text == starter, "Enter starts the night over, with the code");
        await Task.Delay(200);
        check(session.HintsShown == 2, "the capture leaves a free page of hints");
        check(screen.Info.OutputBox.Text.Contains("Uma página nova no caderno"), "the output tells of the free page");
        check(screen.Command(Keys.Enter) && session.Clock.State == ClockState.Coding, "Enter starts the new attempt");

        // The last layer, with its code in colour and no Markdown marks.
        if (screen.Info.Current != InfoPane.Tab.Hints) screen.Command(Keys.Control | Keys.D3);
        screen.Command(Keys.Control | Keys.D3);
        string all = screen.Info.HintsBox.Text;
        check(session.HintsShown == 3 && all.Contains("Dica 3 de 3") && !all.Contains("aceita o doce"), "each sweet opens the next layer, up to three");
        check(!all.Contains("```") && !all.Contains('`') && all.Contains("double volts = ___;"), "code blocks show as code, without Markdown marks");
        var hintsBox = screen.Info.HintsBox;
        int keyword = hintsBox.Text.IndexOf("double volts", StringComparison.Ordinal);
        hintsBox.Select(keyword, 6);
        check(hintsBox.SelectionColor.ToArgb() == CodeEditor.TypeColor.ToArgb(), "code in the hints is coloured like the editor");

        // A win: the review shows the marks, what the night taught, and how many times each line ran.
        if (NightBench.Solution(night) is not { } solution)
        {
            check(false, "the reference solution is found");
            return;
        }
        editor.Load(solution);
        check(screen.Command(Keys.F5), "F5 runs the solution");
        watch.Restart();
        while (session.Review is null && watch.Elapsed.TotalSeconds < 30) await Task.Delay(50);
        await Task.Delay(200);
        string review = screen.Info.ReviewBox.Text;
        check(session.Clock.State == ClockState.Won, "the solution wins the night");
        check(screen.Info.Current == InfoPane.Tab.Review && review.Contains("Revisão da noite") && review.Contains("Tentativas: 2") && review.Contains("Doces: 2") && review.Contains("Velas perdidas: 3"), "the review opens with the night's numbers, every attempt counted");
        check(review.Contains("○ Ileso") && review.Contains("○ Sem doce"), "after a capture and two sweets, neither Ileso nor Sem doce");
        check(review.Contains(night.Learned.Pt[..40]) && review.Contains("1× │") && review.Contains("return watts;"), "the review shows what the night taught and the code with its line counts");
    }
}
