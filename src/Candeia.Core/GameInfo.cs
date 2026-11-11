namespace Candeia;

// Identity of the game. The game and the house system it simulates share the same name.
public static class GameInfo
{
    public const string Name = "Candeia";
    public const string SystemName = "CANDEIA";

    // Major.minor.patch from the assembly version set in Directory.Build.props.
    public static string Version
    {
        get
        {
            var version = typeof(GameInfo).Assembly.GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
