using Candeia.Challenges;
using Candeia.Night;

namespace Candeia.Progress;

// The campaign: which nights are open, where Continue goes, each night's code, hint layer and best result,
// and the marks that open the notebook's secret pages. Every change is saved at once, in the background.
public sealed class Campaign
{
    readonly SaveStore? store;

    // Store null: a campaign kept only in memory (the tests and the benches never touch the real save).
    public Campaign(IReadOnlyList<NightDefinition> nights, SaveStore? store, SaveData? data = null)
    {
        Nights = nights.OrderBy(n => n.Act).ThenBy(n => n.Order).ToList();
        this.store = store;
        Data = data ?? new SaveData();
    }

    public IReadOnlyList<NightDefinition> Nights { get; }
    public SaveData Data { get; }
    public Settings Settings => Data.Settings;
    public bool Started => Data.Started;

    // Loads the save from its folder; Problem tells the player when the save could not be read.
    public static (Campaign Campaign, SaveProblem? Problem) Load(IReadOnlyList<NightDefinition> nights, SaveStore store)
    {
        var (data, problem) = store.Load();
        return (new Campaign(nights, store, data), problem);
    }

    public NightRecord Record(NightDefinition night)
    {
        if (!Data.Nights.TryGetValue(night.Id, out var record)) Data.Nights[night.Id] = record = new NightRecord();
        return record;
    }

    // The first night is always open; each of the others, once the one before it is won.
    public bool Unlocked(NightDefinition night)
    {
        int index = IndexOf(night);
        return index == 0 || index > 0 && Won(Nights[index - 1]);
    }

    public bool Won(NightDefinition night) => Data.Nights.TryGetValue(night.Id, out var record) && record.Won;

    public bool AllWon => Nights.All(Won);

    // The night Continue opens: the one last played, while it is open and not won; otherwise the first open
    // night not won yet; when every night is won, the last one.
    public NightDefinition Continue
    {
        get
        {
            var current = Nights.FirstOrDefault(n => n.Id == Data.Current);
            if (current is not null && Unlocked(current) && !Won(current)) return current;
            return Nights.FirstOrDefault(n => Unlocked(n) && !Won(n)) ?? Nights[^1];
        }
    }

    // A new campaign, at the difficulty the player chose.
    public void Begin(Difficulty difficulty)
    {
        Data.Started = true;
        Data.Settings = Data.Settings with { Difficulty = difficulty };
        Data.Current = Nights[0].Id;
        Save();
    }

    public void ChangeSettings(Settings settings)
    {
        Data.Settings = settings.Checked();
        Save();
    }

    public void Opened(NightDefinition night)
    {
        Data.Current = night.Id;
        Save();
    }

    // The code the night opens with: the player's last code, or the night's own starter.
    public string CodeFor(NightDefinition night) => Data.Nights.TryGetValue(night.Id, out var r) && r.Code is { } code ? code : night.Starter;

    public void KeepCode(NightDefinition night, string code)
    {
        var record = Record(night);
        if (record.Code == code) return;
        record.Code = code;
        Save();
    }

    // Replaying from the original code: the player's code for that night is let go.
    public void ForgetCode(NightDefinition night)
    {
        Record(night).Code = null;
        Save();
    }

    public void KeepHints(NightDefinition night, int shown)
    {
        var record = Record(night);
        if (record.HintsShown == shown) return;
        record.HintsShown = shown;
        Save();
    }

    // A won night: kept as won, its best result kept (a worse one never replaces it), and the next night
    // becomes the one Continue opens. Returns whether this was the best result so far.
    public bool RecordWin(NightDefinition night, NightMarks marks, double secondsLeft, int attempts)
    {
        var record = Record(night);
        var result = new NightResult(marks.Unharmed, marks.NoSweet, marks.Distance, Math.Round(secondsLeft, 1), attempts);
        bool best = result.BetterThan(record.Best);
        if (best) record.Best = result;
        record.Won = true;
        int index = IndexOf(night);
        if (index >= 0 && index + 1 < Nights.Count) Data.Current = Nights[index + 1].Id;
        Save();
        return best;
    }

    // Every mark of every night's best result.
    public int Marks => Data.Nights.Values.Sum(r => r.Best?.Marks ?? 0);
    public int MarksPossible => Nights.Count * 3;

    // The notebook as this campaign sees it: the secret pages its marks have opened.
    public IReadOnlyList<NotebookPage> OpenPages(IReadOnlyList<NotebookPage> pages) => pages.Where(p => p.Secret <= Marks).ToList();

    public NightDefinition? Next(NightDefinition night)
    {
        int index = IndexOf(night);
        return index >= 0 && index + 1 < Nights.Count ? Nights[index + 1] : null;
    }

    int IndexOf(NightDefinition night)
    {
        for (int i = 0; i < Nights.Count; i++) if (Nights[i].Id == night.Id) return i;
        return -1;
    }

    void Save() => store?.WriteInBackground(Data.ToJson());

    // Waits for the writes under way (the game is closing).
    public void Flush() => store?.Flush();
}
