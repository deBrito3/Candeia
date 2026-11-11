namespace Candeia.Tests;

// The core library loads and identifies the game.
static class CoreSmokeTests
{
    public static void Run(TestRunner t)
    {
        t.Check(GameInfo.Name == "Candeia", "Game name is Candeia");
        t.Check(GameInfo.SystemName == GameInfo.Name.ToUpperInvariant(), "System name is the game name in capitals");
        var parts = GameInfo.Version.Split('.');
        t.Check(parts.Length == 3 && parts.All(p => int.TryParse(p, out _)), "Version is major.minor.patch");
        t.Check(GameInfo.Version != "0.0.0", "Version comes from the assembly");
    }
}
