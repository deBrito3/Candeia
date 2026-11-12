using Candeia.Execution;

namespace Candeia.Challenges;

// Text in both game languages.
public sealed record Bilingual(string Pt, string En)
{
    public string For(bool english) => english ? En : Pt;
}

// A parameter or return type in neutral terms: int, long, double, bool, string, char, arrays and lists of
// those, and dictionaries from text to a number. Each language maps them to its own types.
public sealed record NightParameter(string Name, string Type);

public sealed record NightMethod(string Name, string Returns, IReadOnlyList<NightParameter> Parameters);

// Something the night shows or plays at a moment of the night, carried out by the scene (delivery 6).
public sealed record NightEvent(string On, string Effect, string Where, double Seconds);

// One night, as read from content/nights/<act>/<night>/.
public sealed record NightDefinition(
    string Id,
    int Act,
    int Order,
    string Type,
    bool Intro,
    Bilingual Title,
    string Class,
    IReadOnlyList<NightMethod> Methods,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<TestCase> Tests,
    double Tolerance,
    RunLimits Limits,
    IReadOnlyList<NightEvent> Events,
    string Starter,
    Bilingual Readme,
    IReadOnlyList<Bilingual> Hints,
    string Folder)
{
    // What the night taught, in one line for the notebook after it is won.
    public Bilingual Learned { get; init; } = new("", "");

    // For Vigil, the difficulty for people who never programmed: a part read before the LEIA-ME that
    // explains from zero. Only some nights have one.
    public Bilingual? Beginner { get; init; }

    // The LEIA-ME as the player reads it in a difficulty: with the part from zero first, in Vigil.
    public Bilingual ReadmeFor(Night.Difficulty difficulty) =>
        difficulty == Night.Difficulty.Vigil && Beginner is { } b ? new(b.Pt + "\n\n" + Readme.Pt, b.En + "\n\n" + Readme.En) : Readme;

    public int HiddenTests => Tests.Count(t => t.Hidden);

    public RunRequest Request(byte[] assembly, byte[]? pdb) =>
        new(assembly, pdb, Class, Methods[0].Name, Tests, Limits, Tolerance);
}
