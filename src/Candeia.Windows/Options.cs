using Candeia.Challenges;
using Candeia.Night;
using Candeia.Progress;

namespace Candeia;

// The player's settings for the running game. They live in the campaign's save; every screen reads them
// here and hears when they change.
static class Options
{
    static Campaign? campaign;

    public static Settings Settings { get; private set; } = new();
    public static event Action? Changed;

    public static bool SoftScares => Settings.SoftScares;
    public static Difficulty Difficulty => Settings.Difficulty;
    public static bool English => Settings.English;

    // The campaign whose save keeps the settings from now on.
    public static void Use(Campaign value)
    {
        campaign = value;
        Settings = value.Settings;
        Changed?.Invoke();
    }

    public static void Change(Settings value)
    {
        Settings = value.Checked();
        campaign?.ChangeSettings(Settings);
        Changed?.Invoke();
    }

    // The interface's words in the player's language.
    public static string T(string pt, string en) => Settings.English ? en : pt;
    public static string Of(Bilingual text) => Settings.English ? text.En : text.Pt;
}
