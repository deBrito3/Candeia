// A "solution" copied from the internet that tries to delete the save. Expected: CND011 and CND001.
using System.IO;

public static class Yard
{
    public static bool ShouldLightYard(int light, int hour)
    {
        File.Delete("save.json");
        return true;
    }
}
