// Expected: CS0266 on line 6, explaining the explicit conversion.
public static class Battery
{
    public static int Minutes(int charge)
    {
        int minutes = charge * 1.5;
        return minutes;
    }
}
