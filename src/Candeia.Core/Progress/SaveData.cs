using System.Text.Json;
using System.Text.Json.Serialization;

namespace Candeia.Progress;

// Everything the game keeps between sessions, in one file (see SaveStore).
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public Settings Settings { get; set; } = new();
    // The difficulty was chosen and the campaign began.
    public bool Started { get; set; }
    // The night Continue opens.
    public string? Current { get; set; }
    // Each night's code, hint layer and best result, by the night's id.
    public Dictionary<string, NightRecord> Nights { get; set; } = [];

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    // Reads a save, or throws InvalidDataException (or JsonException) when it cannot be trusted.
    public static SaveData FromJson(string text)
    {
        var data = JsonSerializer.Deserialize<SaveData>(text, Json) ?? throw new InvalidDataException("empty save");
        if (data.Version < 1 || data.Version > CurrentVersion) throw new InvalidDataException($"save version {data.Version} is not known");
        data.Settings = (data.Settings ?? new()).Checked();
        data.Nights ??= [];
        foreach (var record in data.Nights.Values)
        {
            if (record is null) throw new InvalidDataException("a night's record is empty");
            record.HintsShown = Math.Clamp(record.HintsShown, 0, 3);
        }
        return data;
    }
}

public sealed class NightRecord
{
    // The player's code, kept at every run and a few seconds after each edit; null until the first.
    public string? Code { get; set; }
    // How many hint layers are open (paid for, or free after a capture).
    public int HintsShown { get; set; }
    public bool Won { get; set; }
    public NightResult? Best { get; set; }
}

// How a won night went. The best is the one with more marks; with as many, the one with more time left.
public sealed record NightResult(bool Unharmed, bool NoSweet, bool Distance, double SecondsLeft, int Attempts)
{
    [JsonIgnore]
    public int Marks => (Unharmed ? 1 : 0) + (NoSweet ? 1 : 0) + (Distance ? 1 : 0);

    public bool BetterThan(NightResult? other) =>
        other is null || Marks > other.Marks || Marks == other.Marks && SecondsLeft > other.SecondsLeft + 1e-6;
}
