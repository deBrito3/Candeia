// Night 3, "The yard light": a correct solution. Expected: OK.
public static class Yard
{
    public static bool ShouldLightYard(int light, int hour)
    {
        if (light < 30)
        {
            return true;
        }
        return hour >= 18 || hour < 6;
    }
}
