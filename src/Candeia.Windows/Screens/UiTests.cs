using System.Diagnostics;
using System.Text;
using Candeia.Challenges;
using Candeia.Night;
using Candeia.Panels;
using Candeia.Scene;

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
        if (finished) Windows(nights, Check, failures);
        string report = failures.Count == 0 ? $"PASS: {passed} UI checks." : $"FAIL: {failures.Count} of {passed + failures.Count} UI checks.\n" + string.Join("\n", failures.Select(f => "  - " + f));
        File.WriteAllText(reportPath, report + "\n", new UTF8Encoding(false));
        return failures.Count == 0 ? 0 : 1;
    }

    // The real windows of a campaign, with a save in a temporary folder and the sound at zero: the menu
    // opens a night in its own window and hides; M in the pause brings the menu back; the save keeps the code.
    static void Windows(IReadOnlyList<NightDefinition> nights, Action<bool, string> check, List<string> failures)
    {
        string folder = Path.Combine(Path.GetTempPath(), "candeia-ui-save-" + Guid.NewGuid().ToString("N")[..8]);
        var store = new Progress.SaveStore(folder);
        var (campaign, _) = Progress.Campaign.Load(nights, store);
        campaign.ChangeSettings(campaign.Settings with { HouseVolume = 0, SystemVolume = 0, CreatureVolume = 0, MusicVolume = 0 });
        Options.Use(campaign);
        bool finished = false;
        using var window = new MainWindow(campaign);
        window.Shown += async (_, _) =>
        {
            try
            {
                var menu = window.CandeiaMenu!;
                menu.Key(Keys.Enter);
                menu.Key(Keys.Enter);
                await Task.Delay(2500);
                var night = Application.OpenForms.OfType<NightScreen>().FirstOrDefault();
                check(night is not null && night.Visible && !window.Visible && campaign.Started, "the menu opens the night in its own window and hides");
                if (night is null) return;
                night.Command(Keys.Enter);
                night.Editor.Select(0, 0);
                night.Editor.SelectedText = "// from the window test\n";
                night.Run();
                var watch = Stopwatch.StartNew();
                while (night.Session.Busy && watch.Elapsed.TotalSeconds < 15) await Task.Delay(50);
                check(night.Command(Keys.Escape) && night.Paused && night.Title.PresenceText.Contains("M menu"), "Esc pauses, and the pause shows M for the menu");
                night.Command(Keys.M);
                await Task.Delay(800);
                check(window.Visible && menu.Current == CandeiaMenu.Page.Main && night.IsDisposed, "M goes back to the menu, and the night's window closes");
                campaign.Flush();
                var (saved, problem) = store.Load();
                check(problem is null && saved.Started && saved.Nights.TryGetValue(nights[0].Id, out var record) && record.Code!.StartsWith("// from the window test", StringComparison.Ordinal),
                    "the save on disk keeps the campaign and the night's code");
                finished = true;
            }
            catch (Exception ex) { failures.Add($"windows: {ex.GetType().Name}: {ex.Message}"); }
            finally { window.Close(); }
        };
        Application.Run(window);
        if (!finished && !failures.Any(f => f.StartsWith("windows", StringComparison.Ordinal))) failures.Add("the window checks stopped before the end");
        Options.Use(new Progress.Campaign(nights, store: null));
        try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
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
        check(readme.Contains("Watts()") && readme.Contains("Quantos watts a lâmpada usa?") && !readme.Contains('`') && !readme.Contains("**"), "the LEIA-ME is shown without Markdown marks");

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

        // A LEIA-ME table too wide for the panel reads as one block per row.
        screen.Open(screen.Nights[2]);
        await Task.Delay(300);
        string yard = screen.Info.ReadmeBox.Text;
        check(yard.Contains("\n  O que devolve: Se está escuro lá fora") && !yard.Contains("Método"), "a wide table reads as one block per row");

        // Vigil: the first night starts from zero, with four candles and free hints.
        Options.Change(Options.Settings with { Difficulty = Difficulty.Vigil });
        screen.Open(screen.Nights[0]);
        await Task.Delay(300);
        check(screen.Info.ReadmeBox.Text.StartsWith("Do zero", StringComparison.Ordinal) && screen.Info.ReadmeBox.Text.Contains("A caixa da lâmpada"), "in Vigil, the LEIA-ME starts from zero and keeps the night's own");
        check(screen.Session.Clock.CandlesLeft == 4 && screen.Session.HintCost == 0, "Vigil has four candles and free hints");
        Options.Change(Options.Settings with { Difficulty = Difficulty.Night });

        await Campaign(screen, check);
        await Settings(screen, check);
        Menu(check);
        await Faults(check);
    }

    // A fault in the scene's thread is caught and handed to the window's thread; the crash report says what
    // a tester can send back, and never the player's code.
    static async Task Faults(Action<bool, string> check)
    {
        var size = new Size(1280, 720);
        using (var worker = new SceneWorker(size, IntPtr.Zero, new FrameStats()))
        {
            var state = new SceneState(0, Station.Street, 0, SceneQuality.High);
            worker.Submit(state, [], size);
            var watch = Stopwatch.StartNew();
            while (worker.Drawn == 0 && watch.Elapsed.TotalSeconds < 5) await Task.Delay(20);
            check(worker.Drawn > 0 && worker.Failure is null, "the scene's thread draws the frames it is handed");
            worker.FailNextFrame();
            worker.Submit(state, [], size);
            watch.Restart();
            while (worker.Failure is null && watch.Elapsed.TotalSeconds < 5) await Task.Delay(20);
            check(worker.Failure is InvalidOperationException, "a fault in the scene's thread is kept for the window's thread to report");
        }
        string folder = Path.Combine(Path.GetTempPath(), "candeia-crash-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Exception error;
            try { throw new InvalidOperationException("A test fault."); }
            catch (InvalidOperationException ex) { error = ex; }
            string path = CrashReport.Write(error, folder, new DateTime(2026, 11, 12, 21, 5, 9));
            string text = File.ReadAllText(path);
            check(Path.GetFileName(path) == "erro-20261112-210509.txt" && text.StartsWith($"{GameInfo.Name} {GameInfo.Version}", StringComparison.Ordinal)
                && text.Contains("A test fault.") && text.Contains(nameof(Faults)) && text.Contains("Windows: "), "the crash report has the version, the Windows, the error and where it happened");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    // The campaign behind the screen: the code kept at each run and after edits, the hint layers, the win
    // and the notebook's secret pages.
    static async Task Campaign(NightScreen screen, Action<bool, string> check)
    {
        var campaign = screen.Campaign;
        var first = screen.Nights[0];
        check(campaign.Won(first) && campaign.Record(first).Best is { Marks: > 0 } && campaign.Unlocked(screen.Nights[1]), "the won night is kept with its best result, and the next one opens");
        check(campaign.CodeFor(first) == screen.Editor.Text, "the code that won is kept for the night");
        check(campaign.Record(first).HintsShown == 3, "the hint layers opened are kept");
        string index = (screen.Command(Keys.Control | Keys.D4) ? screen.Info.NotebookBox.Text : "");
        check(index.Contains($"Página secreta: abre com 6 marcas (você tem {campaign.Marks})") && index.Contains("abre com 12 marcas"), "the secret pages show how many marks open them");

        // A few seconds after an edit, the code goes to the campaign.
        screen.Open(screen.Nights[1]);
        await Task.Delay(300);
        screen.Editor.Select(0, 0);
        screen.Editor.SelectedText = "// kept\n";
        await Task.Delay(3600);
        check(campaign.CodeFor(screen.Nights[1]).StartsWith("// kept", StringComparison.Ordinal), "an edit reaches the save a few seconds later");
        screen.Open(screen.Nights[2]);
        screen.Open(screen.Nights[1]);
        check(screen.Editor.Text.StartsWith("// kept", StringComparison.Ordinal), "a night opens again with the player's code");
    }

    // The settings, applied at once: the language, the editor's font and the high contrast.
    static async Task Settings(NightScreen screen, Action<bool, string> check)
    {
        float font = screen.Editor.Font.Size;
        check(screen.Command(Keys.Control | Keys.Oemplus) && Options.Settings.FontStep == 2, "Ctrl+= makes the editor's font bigger");
        await Task.Delay(200);
        check(screen.Editor.Font.Size > font, $"the editor's font grows: {font:0.0} to {screen.Editor.Font.Size:0.0}");
        screen.Command(Keys.Control | Keys.OemMinus);
        await Task.Delay(200);
        check(Math.Abs(screen.Editor.Font.Size - font) < 0.01 && Options.Settings.FontStep == 1, "Ctrl+- brings it back");

        Options.Change(Options.Settings with { HighContrast = true });
        check(screen.Editor.BackColor.ToArgb() == Color.Black.ToArgb() && CodeEditor.KeywordColor.ToArgb() != Color.FromArgb(255, 224, 156).ToArgb(), "high contrast turns the editor black, with strong colours");
        Options.Change(Options.Settings with { HighContrast = false });
        check(screen.Editor.BackColor.ToArgb() == CodeEditor.Back.ToArgb() && CodeEditor.Back.ToArgb() != Color.Black.ToArgb(), "and back to the amber glass");

        Options.Change(Options.Settings with { Language = "en" });
        await Task.Delay(200);
        check(screen.Info.ReadmeBox.Text.StartsWith("Night 2", StringComparison.Ordinal) && screen.Info.ReadmeBox.Text.Contains("starts the night"), "in English, the LEIA-ME is the README");
        check(screen.Title.TitleText.Contains("Night 2 · The system battery") && screen.Title.PresenceText.StartsWith("PRESENCE", StringComparison.Ordinal), "the title bar and the sensors speak English");
        screen.Command(Keys.Control | Keys.D3);
        check(screen.Info.HintsBox.Text.Contains("The hints open one at a time") && screen.Info.HintsBox.Text.Contains("for −"), "the hints speak English, with their price");
        screen.Command(Keys.Control | Keys.D4);
        check(screen.Info.NotebookBox.Text.StartsWith("The notebook", StringComparison.Ordinal) && screen.Info.NotebookBox.Text.Contains("Secret page"), "and the notebook");
        screen.Command(Keys.Enter);
        screen.Run();
        var watch = Stopwatch.StartNew();
        while (screen.Session.Busy && watch.Elapsed.TotalSeconds < 15) await Task.Delay(50);
        check(screen.Info.OutputBox.Text.Contains("lamps lit"), "the output speaks English");
        Options.Change(Options.Settings with { Language = "pt" });
        await Task.Delay(200);
        check(screen.Title.TitleText.Contains("Noite 2"), "and back to Portuguese");
    }

    // The CANDEIA menu, driven without a window: a new campaign, the difficulty, the options and the nights.
    static void Menu(Action<bool, string> check)
    {
        var nights = NightLoader.LoadAll().Where(r => r.Ok).Select(r => r.Night!).ToList();
        var campaign = new Progress.Campaign(nights, store: null);
        var menu = new CandeiaMenu(campaign);
        (NightDefinition Night, bool Fresh)? played = null;
        menu.Play += (n, fresh) => played = (n, fresh);
        check(menu.Current == CandeiaMenu.Page.Boot && menu.Key(Keys.Enter) && menu.Current == CandeiaMenu.Page.Difficulty, "a new campaign asks for the difficulty first");
        check(menu.Lines().Count == 2 && menu.Lines()[0].StartsWith("Vigília", StringComparison.Ordinal) && menu.Selected == 1, "Vigil is listed first, and Night, the default, comes chosen");
        menu.Key(Keys.Up);
        menu.Key(Keys.Enter);
        check(campaign.Started && Options.Settings.Difficulty == Difficulty.Vigil && played is { Night.Order: 1, Fresh: false }, "choosing Vigil begins the campaign with night 1");
        check(menu.Current == CandeiaMenu.Page.Main && menu.Lines()[0].Contains("Noite 1, A luz do quarto"), "back from the night, Continue points to it");

        menu.Key(Keys.Down);
        menu.Key(Keys.Enter);
        var lines = menu.Lines();
        check(menu.Current == CandeiaMenu.Page.Nights && lines.Count == nights.Count && lines[0].Contains("aberta") && lines[1].Contains("vença a noite anterior"), "the nights: the first open, the others waiting for the one before");
        played = null;
        menu.Key(Keys.Down);
        menu.Key(Keys.Enter);
        check(played is null, "a locked night does not open");
        campaign.RecordWin(nights[0], new NightMarks(true, false, true), 150, 1);
        menu.Show(CandeiaMenu.Page.Nights);
        check(menu.Selected == 1 && menu.Lines()[0].Contains("● ○ ●") && menu.Lines()[0].Contains("2:30") && menu.Lines()[1].Contains("aberta"), "a won night shows its marks and time, and the next one opens");
        menu.Key(Keys.Up);
        menu.Key(Keys.R);
        check(played is null && menu.Notice is not null, "R once only warns");
        menu.Key(Keys.R);
        check(played is { Night.Order: 1, Fresh: true }, "R again replays from the original code");

        menu.Key(Keys.Escape);
        menu.Key(Keys.Down);
        menu.Key(Keys.Down);
        menu.Key(Keys.Enter);
        check(menu.Current == CandeiaMenu.Page.Options && menu.Lines().Count == 11, "the options have their eleven rows");
        menu.Key(Keys.Right);
        check(Options.Settings.English && menu.Lines()[0] == "Language: English" && campaign.Settings.English, "the language changes at once, and the campaign keeps it");
        menu.Key(Keys.Down);
        menu.Key(Keys.Right);
        check(Options.Settings.Difficulty == Difficulty.Night, "the difficulty changes in the options");
        for (int i = 0; i < 7; i++) menu.Key(Keys.Down);
        menu.Key(Keys.Left);
        menu.Key(Keys.Left);
        check(Math.Abs(Options.Settings.CreatureVolume - 0.8) < 1e-9 && menu.Lines()[8].EndsWith("80%", StringComparison.Ordinal), "a volume goes down in steps of ten");
        menu.Key(Keys.Up);
        menu.Key(Keys.Up);
        menu.Key(Keys.Up);
        menu.Key(Keys.Right);
        check(Options.Settings.SoftScares, "soft scares from the options");
        for (int i = 0; i < 5; i++) menu.Key(Keys.Down);
        menu.Key(Keys.Enter);
        check(!Options.Settings.BatterySaver && menu.Lines()[10].EndsWith("no", StringComparison.Ordinal), "the battery saver can be turned off");
        Options.Change(new Progress.Settings());
        menu.Key(Keys.Escape);
        check(menu.Current == CandeiaMenu.Page.Main, "Esc goes back to the menu");
    }
}
