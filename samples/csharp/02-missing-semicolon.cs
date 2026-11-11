// Expected: CS1002 on line 6, with a fix.
public static class Battery
{
    public static int Percent(int charge, int capacity)
    {
        int percent = charge * 100 / capacity
        return percent;
    }
}
