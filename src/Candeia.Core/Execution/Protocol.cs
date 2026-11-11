using System.Text.Json;
using System.Text.Json.Serialization;

namespace Candeia.Execution;

// JSON lines between the game and the isolated runner, one object per line.
//   runner → game: {"kind":"ready"} as soon as it can take a request (a spare process is warm)
//   game → runner: one RunRequestMessage
//   runner → game: one {"kind":"test"} per test, as each finishes, then {"kind":"done"}
// The game trusts nothing it reads: kinds, indexes and sizes are validated, and anything else ends the run.
// Each request carries a random nonce that every reply must repeat, so code that writes to the real
// output without reaching into the runner cannot forge results.
static class Protocol
{
    public const int MaxLineChars = 256 * 1024;

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string line) => JsonSerializer.Deserialize<T>(line, Options);
}

sealed class RunRequestMessage
{
    public string Nonce { get; set; } = "";
    public string Assembly { get; set; } = "";
    public string? Pdb { get; set; }
    public string EntryType { get; set; } = "";
    public string EntryMethod { get; set; } = "";
    public List<TestCaseMessage> Tests { get; set; } = [];
    public long StepsPerTest { get; set; }
    public int OutputChars { get; set; }
    public double Tolerance { get; set; }
}

sealed class TestCaseMessage
{
    public string? Method { get; set; }
    public JsonElement[] Args { get; set; } = [];
    public JsonElement Expected { get; set; }
}

sealed class RunnerMessage
{
    public string Kind { get; set; } = "";
    public string? Nonce { get; set; }
    public int Index { get; set; }
    public bool Passed { get; set; }
    public string? Actual { get; set; }
    public string? Expected { get; set; }
    public long Steps { get; set; }
    public string? Output { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorLine { get; set; }
    public Dictionary<string, long>? Marks { get; set; }
    public Dictionary<string, long>? Calls { get; set; }
    public string? Stop { get; set; }
    public string? Detail { get; set; }
}
