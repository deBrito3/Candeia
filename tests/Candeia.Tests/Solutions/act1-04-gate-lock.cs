// Reference solution: lights every lamp and uses every required concept. Not shipped with the game.
public static class GateLock
{
    public static bool IsDangerousHour(int hour)
    {
        return hour >= 18 || hour < 6;
    }

    public static bool CanOpen(string code, int hour)
    {
        return code == "3107" && !IsDangerousHour(hour);
    }

    public static string Status(string code, int hour)
    {
        if (IsDangerousHour(hour))
        {
            return "DANGEROUS HOUR";
        }
        else if (code != "3107")
        {
            return "WRONG CODE";
        }
        else
        {
            return "OPEN";
        }
    }
}
