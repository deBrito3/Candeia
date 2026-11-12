// Reference solution: lights every lamp and uses every required concept. Not shipped with the game.
public static class YardLight
{
    public static bool IsDark(int lux)
    {
        if (lux < 50)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool ShouldLight(int lux, int hour)
    {
        if (hour < 5)
        {
            return IsDark(lux);
        }
        if (hour >= 18)
        {
            return IsDark(lux);
        }
        return false;
    }

    public static string Label(bool on)
    {
        if (on)
        {
            return "ON";
        }
        else
        {
            return "OFF";
        }
    }
}
