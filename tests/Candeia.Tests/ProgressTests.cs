using Candeia.Challenges;
using Candeia.Night;
using Candeia.Progress;

namespace Candeia.Tests;

// The save file and the campaign: safe writing with a backup, recovery from a damaged save, the order in
// which nights open, the best result per night, and the marks that open the secret pages.
static class ProgressTests
{
    public static void Run(TestRunner t)
    {
        // Settings put right what a hand-edited save may get wrong.
        var odd = new Settings { Language = "fr", Difficulty = Difficulty.Nightmare, FontStep = 9, MusicVolume = 3, HouseVolume = double.NaN }.Checked();
        t.Check(odd is { Language: "pt", Difficulty: Difficulty.Night, FontStep: 4, MusicVolume: 1, HouseVolume: 1 }, "settings out of range are put right");
        t.Check(new Settings().FontScale == 1 && new Settings { FontStep = 0 }.FontScale < 1 && new Settings { Language = "en" }.English, "the font scale and the language");

        string folder = Path.Combine(Path.GetTempPath(), "candeia-save-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var store = new SaveStore(folder);
            t.Check(store.Load() is { Data.Started: false, Problem: null }, "no save yet: a new game, with nothing to report");

            var data = new SaveData { Started = true, Current = "act1-02-system-battery", Settings = new Settings { Language = "en", FontStep = 3, HighContrast = true } };
            data.Nights["act1-01-room-light"] = new NightRecord { Code = "// mine\n", HintsShown = 2, Won = true, Best = new NightResult(true, false, true, 123.4, 1) };
            t.Check(store.Write(data.ToJson()) && File.Exists(store.SavePath) && !File.Exists(store.BackupPath), "the first save writes save.json");
            var (read, problem) = store.Load();
            t.Check(problem is null && read.Started && read.Current == "act1-02-system-battery" && read.Settings is { English: true, FontStep: 3, HighContrast: true }, "the save reads back: the campaign and the settings");
            t.Check(read.Nights["act1-01-room-light"] is { Code: "// mine\n", HintsShown: 2, Won: true, Best: { Marks: 2, SecondsLeft: 123.4 } }, "a night's code, hint layer and best result read back");
            t.Check(File.ReadAllText(store.SavePath).Contains("\"Difficulty\": \"Night\""), "the save is readable text, enums by name");

            data.Current = "act1-03-yard-light";
            store.Write(data.ToJson());
            t.Check(File.Exists(store.BackupPath) && SaveData.FromJson(File.ReadAllText(store.BackupPath)).Current == "act1-02-system-battery", "the save it replaces becomes the backup");

            // A damaged save: the backup is used, and the next write keeps the damaged file aside.
            File.WriteAllText(store.SavePath, "{ not json");
            var (recovered, why) = store.Load();
            t.Check(recovered.Current == "act1-02-system-battery" && why == SaveProblem.RecoveredFromBackup, "a damaged save falls back to the backup and says so");
            store.Write(recovered.ToJson());
            t.Check(Directory.GetFiles(folder, "save.json.invalid-*").Length == 1 && SaveData.FromJson(File.ReadAllText(store.BackupPath)).Current == "act1-02-system-battery", "the damaged save is kept aside, and the good backup stays");
            File.WriteAllText(store.SavePath, "[]");
            File.WriteAllText(store.BackupPath, "{\"Version\": 99}");
            t.Check(store.Load() is { Data.Started: false, Problem: SaveProblem.Lost }, "with both damaged, a new game, and the player is told");
            t.Check(!store.Write("{ broken"), "a broken snapshot is never written");

            // Background writes: the last snapshot wins, whatever order the threads run in.
            for (int i = 0; i < 20; i++) store.WriteInBackground(new SaveData { Current = $"night-{i}" }.ToJson());
            store.Flush();
            t.Check(store.Load().Data.Current == "night-19", "background writes end with the last snapshot");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }

        // The campaign.
        var nights = NightLoader.LoadAll().Where(r => r.Ok).Select(r => r.Night!).ToList();
        var campaign = new Campaign(nights, store: null);
        t.Check(!campaign.Started && campaign.Unlocked(nights[0]) && !campaign.Unlocked(nights[1]), "only the first night is open at the start");
        campaign.Begin(Difficulty.Vigil);
        t.Check(campaign.Started && campaign.Settings.Difficulty == Difficulty.Vigil && campaign.Continue.Id == nights[0].Id, "beginning sets the difficulty and Continue opens night 1");

        t.Check(campaign.CodeFor(nights[0]) == nights[0].Starter, "a night opens with its starter until the player writes");
        campaign.KeepCode(nights[0], "// my code");
        campaign.KeepHints(nights[0], 2);
        t.Check(campaign.CodeFor(nights[0]) == "// my code" && campaign.Record(nights[0]).HintsShown == 2, "the player's code and hint layer are kept");

        bool best = campaign.RecordWin(nights[0], new NightMarks(true, false, true), 200, 1);
        t.Check(best && campaign.Won(nights[0]) && campaign.Unlocked(nights[1]) && !campaign.Unlocked(nights[2]) && campaign.Continue.Id == nights[1].Id, "a win opens the next night, and Continue goes there");
        t.Check(campaign.Marks == 2 && campaign.MarksPossible == nights.Count * 3, $"the marks add up: {campaign.Marks} of {campaign.MarksPossible}");
        t.Check(!campaign.RecordWin(nights[0], new NightMarks(false, false, false), 300, 2) && campaign.Record(nights[0]).Best!.Marks == 2, "a worse result never replaces the best");
        t.Check(!campaign.RecordWin(nights[0], new NightMarks(true, true, false), 10, 3) && campaign.Record(nights[0]).Best!.SecondsLeft == 200, "as many marks with less time left is not better");
        t.Check(campaign.RecordWin(nights[0], new NightMarks(true, true, false), 250, 3) && campaign.Record(nights[0]).Best is { Marks: 2, SecondsLeft: 250 }, "as many marks with more time left is better");
        t.Check(campaign.RecordWin(nights[0], new NightMarks(true, true, true), 5, 1) && campaign.Marks == 3, "more marks is better, whatever the time");

        campaign.Opened(nights[0]);
        t.Check(campaign.Continue.Id == nights[1].Id, "a won night replayed does not hold Continue back");
        campaign.ForgetCode(nights[0]);
        t.Check(campaign.CodeFor(nights[0]) == nights[0].Starter, "replaying from the original code lets the player's code go");

        foreach (var night in nights) campaign.RecordWin(night, new NightMarks(true, true, true), 1, 1);
        t.Check(campaign.AllWon && campaign.Continue.Id == nights[^1].Id && campaign.Marks == campaign.MarksPossible, "every night won: Continue opens the last, with every mark");

        // Secret pages open with marks.
        var pages = new List<NotebookPage>
        {
            new("if", 1, ["if"], new("if", "if"), new("", "")),
            new("secret-a", 1, [], new("A", "A"), new("", ""), Secret: 6),
            new("secret-b", 1, [], new("B", "B"), new("", ""), Secret: 99),
        };
        t.Check(campaign.OpenPages(pages).Select(p => p.Id).SequenceEqual(["if", "secret-a"]), "a secret page opens once the marks reach it");
        t.Check(new Campaign(nights, null).OpenPages(pages).Select(p => p.Id).SequenceEqual(["if"]), "without marks, only the ordinary pages");
    }
}
