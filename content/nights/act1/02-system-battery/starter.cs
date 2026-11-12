// CANDEIA · system battery
// When the power goes out, the house runs on this battery. The panel shows
// 0% even when the battery is full. Something here is wrong.
public static class Battery
{
    // The charge as a whole percentage of the capacity, rounded down.
    public static int Percent(int charge, int capacity)
    {
        int percent = charge / capacity * 100;
        return percent;
    }

    // How many hours the battery lasts: the charge (watt-hours) divided by
    // the load (watts), with decimals.
    public static double Hours(int charge, int load)
    {
        return 0;
    }

    // The hours turned into whole minutes, rounded to the nearest minute.
    public static int Minutes(double hours)
    {
        return 0;
    }

    // The minutes left over after the whole hours: 125 minutes is 2 hours
    // and 5 minutes, so 5.
    public static int LeftoverMinutes(int minutes)
    {
        return 0;
    }
}
