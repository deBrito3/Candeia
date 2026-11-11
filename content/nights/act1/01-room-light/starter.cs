// CANDEIA · room light
// The house asks the lamp four questions; each method answers one.
// Watts still works. Use it as a model for the other three.
public static class RoomLight
{
    // How many watts the lamp uses.
    public static int Watts()
    {
        int watts = 60;
        return watts;
    }

    // The voltage of the wall socket.
    public static double Voltage()
    {
        double volts = 0;
        return volts;
    }

    // Whether the lamp starts lit.
    public static bool StartsOn()
    {
        return false;
    }

    // The model printed on the lamp's box.
    public static string Model()
    {
        return "";
    }
}
