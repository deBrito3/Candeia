// Reference solution: lights every lamp and uses every required concept. Not shipped with the game.
public static class Battery
{
    public static int Percent(int charge, int capacity)
    {
        int percent = charge * 100 / capacity;
        return percent;
    }

    public static double Hours(int charge, int load)
    {
        double hours = (double)charge / load;
        return hours;
    }

    public static int Minutes(double hours)
    {
        int minutes = (int)Math.Round(hours * 60);
        return minutes;
    }

    public static int LeftoverMinutes(int minutes)
    {
        int leftover = minutes % 60;
        return leftover;
    }
}
