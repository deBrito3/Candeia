// CANDEIA · yard light
// The yard is his way to the house. While its light is on, he waits in the
// dark by the gate. The light has not come on for years. IsDark still
// works: use it as a model of an if.
public static class YardLight
{
    // Dark is less than 50 lux outside.
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

    // On only in the dark, and never from 5 am until 6 pm (5 to 17): he
    // does not come by day, and the battery is short. The hour goes from 0
    // to 23.
    public static bool ShouldLight(int lux, int hour)
    {
        return false;
    }

    // The panel's text: "ON" when the light is on, "OFF" when it is off.
    public static string Label(bool on)
    {
        return "";
    }
}
