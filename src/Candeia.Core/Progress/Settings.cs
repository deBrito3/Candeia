using Candeia.Night;

namespace Candeia.Progress;

// The player's choices, kept in the save and changed on the options screen.
public sealed record Settings
{
    // "pt" or "en": the interface, the nights' texts and the notebook. C# stays in English.
    public string Language { get; init; } = "pt";
    // The difficulty of the nights opened from now on.
    public Difficulty Difficulty { get; init; } = Difficulty.Night;
    // No flashes, a quieter capture that fades instead of striking, and the panels always on screen.
    public bool SoftScares { get; init; }
    public bool Captions { get; init; } = true;
    // The editor in white and strong colours on black.
    public bool HighContrast { get; init; }
    // On battery, the scene is drawn 30 times a second instead of 60: half the processor's work.
    public bool BatterySaver { get; init; } = true;
    // The editor's font, as an index into FontScales; 1 is the normal size.
    public int FontStep { get; init; } = 1;
    // The volume of each layer of sound, 0 to 1.
    public double HouseVolume { get; init; } = 1;
    public double SystemVolume { get; init; } = 1;
    public double CreatureVolume { get; init; } = 1;
    public double MusicVolume { get; init; } = 1;

    public static readonly double[] FontScales = [0.85, 1.0, 1.15, 1.3, 1.5];

    public bool English => Language == "en";
    public double FontScale => FontScales[Math.Clamp(FontStep, 0, FontScales.Length - 1)];

    // What a save written by hand, or by an older version, may get wrong, put right.
    public Settings Checked() => this with
    {
        Language = Language == "en" ? "en" : "pt",
        Difficulty = Difficulty == Difficulty.Nightmare ? Difficulty.Night : Difficulty,
        FontStep = Math.Clamp(FontStep, 0, FontScales.Length - 1),
        HouseVolume = Volume(HouseVolume),
        SystemVolume = Volume(SystemVolume),
        CreatureVolume = Volume(CreatureVolume),
        MusicVolume = Volume(MusicVolume),
    };

    static double Volume(double value) => double.IsFinite(value) ? Math.Round(Math.Clamp(value, 0, 1), 2) : 1;
}
