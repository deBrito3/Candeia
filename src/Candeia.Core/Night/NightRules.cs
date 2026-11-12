using System.Text.Json;

namespace Candeia.Night;

// Where the Papa-Figo is, from the farthest to the closest.
public enum Station { Street, Gate, Yard, Window, Door, Beside }

public enum Difficulty { Vigil, Night, Nightmare }

public enum SweetPolicy { Free, Paid, None }

public sealed record StreakAdvance(int After, double Cost);

// VisibleUntil is the closest station where he can be seen in the act's nights; SiegeVisibleUntil, in its
// siege night (the same unless the act says otherwise).
public sealed record ActRules(int Act, double NightSeconds, double SiegeSeconds, double LampPool, double CostMultiplier, Station VisibleUntil, StreakAdvance? StreakAdvance, Station SiegeVisibleUntil);

public sealed record DifficultyRules(Difficulty Difficulty, double Time, double Costs, int Candles, SweetPolicy Sweet);

public sealed record StationBand(Station Station, double From);

// The night's balance, read from content/rules.json (the values approved in the game design document).
public sealed record NightRules(
    IReadOnlyDictionary<int, ActRules> Acts,
    IReadOnlyList<double> NoProgressCosts,
    double CrashCost,
    double SweetCost,
    double NearCaptureReturn,
    double RespiteSeconds,
    double DistanceMark,
    int SweetAfterNoProgressRuns,
    double SweetAfterFractionWithoutLamp,
    double TelegraphSeconds,
    IReadOnlyDictionary<Difficulty, DifficultyRules> Difficulties,
    IReadOnlyList<StationBand> Stations)
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "content", "rules.json");

    // The station for a fraction of time left. A band's start belongs to it: at exactly 80% he is at the gate.
    public Station StationAt(double fractionLeft)
    {
        var station = Stations[0].Station;
        foreach (var band in Stations)
            if (fractionLeft <= band.From + 1e-12) station = band.Station;
        return station;
    }

    // The fraction of time left at which a station begins.
    public double StartOf(Station station) => Stations.First(b => b.Station == station).From;

    public static NightRules Load(string? path = null)
    {
        path ??= DefaultPath;
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = document.RootElement;
        var problems = new List<string>();

        var acts = new Dictionary<int, ActRules>();
        for (int act = 1; act <= 4; act++)
        {
            if (!root.GetProperty("acts").TryGetProperty(act.ToString(), out var value)) { problems.Add($"acts.{act} is missing"); continue; }
            StreakAdvance? streak = value.TryGetProperty("streakAdvance", out var s)
                ? new(s.GetProperty("after").GetInt32(), Fraction(s.GetProperty("cost"), $"acts.{act}.streakAdvance.cost", problems)) : null;
            var visible = StationNamed(value.GetProperty("visibleUntil").GetString(), problems);
            var siegeVisible = value.TryGetProperty("siegeVisibleUntil", out var sv) ? StationNamed(sv.GetString(), problems) : visible;
            if (siegeVisible < visible) problems.Add($"acts.{act}.siegeVisibleUntil cannot hide him more than visibleUntil");
            acts[act] = new(act, Positive(value.GetProperty("nightSeconds"), $"acts.{act}.nightSeconds", problems),
                Positive(value.GetProperty("siegeSeconds"), $"acts.{act}.siegeSeconds", problems),
                Fraction(value.GetProperty("lampPool"), $"acts.{act}.lampPool", problems),
                Positive(value.GetProperty("costMultiplier"), $"acts.{act}.costMultiplier", problems),
                visible, streak, siegeVisible);
        }

        var difficulties = new Dictionary<Difficulty, DifficultyRules>();
        foreach (var (name, difficulty) in new[] { ("vigil", Difficulty.Vigil), ("night", Difficulty.Night), ("nightmare", Difficulty.Nightmare) })
        {
            if (!root.GetProperty("difficulties").TryGetProperty(name, out var value)) { problems.Add($"difficulties.{name} is missing"); continue; }
            var sweet = value.GetProperty("sweet").GetString() switch { "free" => SweetPolicy.Free, "paid" => SweetPolicy.Paid, "none" => SweetPolicy.None, var other => Bad(other) };
            int candles = value.GetProperty("candles").GetInt32();
            if (candles < 1) problems.Add($"difficulties.{name}.candles must be at least 1");
            difficulties[difficulty] = new(difficulty, Positive(value.GetProperty("time"), $"difficulties.{name}.time", problems),
                Positive(value.GetProperty("costs"), $"difficulties.{name}.costs", problems), candles, sweet);
        }

        var stations = root.GetProperty("stations").EnumerateArray()
            .Select(b => new StationBand(StationNamed(b.GetProperty("id").GetString(), problems), b.GetProperty("from").GetDouble())).ToList();
        if (!stations.Select(b => b.Station).SequenceEqual(Enum.GetValues<Station>())) problems.Add("stations must list street, gate, yard, window, door and beside, in this order");
        if (stations.Count > 0 && Math.Abs(stations[0].From - 1) > 1e-9) problems.Add("the first station must start at 1.0");
        for (int i = 1; i < stations.Count; i++)
            if (stations[i].From >= stations[i - 1].From || stations[i].From <= 0) problems.Add($"station {stations[i].Station} must start below {stations[i - 1].Station} and above 0");

        var costs = root.GetProperty("noProgressCosts").EnumerateArray().Select(c => Fraction(c, "noProgressCosts[]", problems)).ToList();
        if (costs.Count == 0) problems.Add("noProgressCosts is empty");

        var rules = new NightRules(acts, costs,
            Fraction(root.GetProperty("crashCost"), "crashCost", problems),
            Fraction(root.GetProperty("sweetCost"), "sweetCost", problems),
            Fraction(root.GetProperty("nearCaptureReturn"), "nearCaptureReturn", problems),
            Positive(root.GetProperty("respiteSeconds"), "respiteSeconds", problems),
            Fraction(root.GetProperty("distanceMark"), "distanceMark", problems),
            root.GetProperty("sweetAfterNoProgressRuns").GetInt32(),
            Fraction(root.GetProperty("sweetAfterFractionWithoutLamp"), "sweetAfterFractionWithoutLamp", problems),
            Positive(root.GetProperty("telegraphSeconds"), "telegraphSeconds", problems),
            difficulties, stations);
        if (problems.Count > 0) throw new InvalidDataException("rules.json: " + string.Join("; ", problems));
        return rules;

        SweetPolicy Bad(string? value) { problems.Add($"sweet \"{value}\" must be free, paid or none"); return SweetPolicy.None; }
    }

    static Station StationNamed(string? name, List<string> problems)
    {
        foreach (var station in Enum.GetValues<Station>())
            if (string.Equals(station.ToString(), name, StringComparison.OrdinalIgnoreCase)) return station;
        problems.Add($"unknown station \"{name}\"");
        return Station.Street;
    }

    static double Fraction(JsonElement value, string name, List<string> problems)
    {
        double number = value.GetDouble();
        if (number <= 0 || number > 1) problems.Add($"{name} must be a fraction above 0 and at most 1");
        return number;
    }

    static double Positive(JsonElement value, string name, List<string> problems)
    {
        double number = value.GetDouble();
        if (number <= 0) problems.Add($"{name} must be above 0");
        return number;
    }
}
