// CANDEIA · fuse box
// The storm took the power, as it does every year. The lights hold on the
// battery; the rest of the house waits at the fuse box, one circuit at a
// time, never past what the mains can carry.
public static class FuseBox
{
    // The whole load, in watts: the lights, the fridge and the water pump
    // together.
    public static int TotalLoad(int lights, int fridge, int pump)
    {
        return 0;
    }

    // How much of the capacity the load uses, as a percentage with
    // decimals: 500 of 2000 is 25.0.
    public static double Usage(int load, int capacity)
    {
        return 0;
    }

    // Whether "extra" more watts fit on top of the load without going past
    // the capacity. Reaching the capacity exactly still fits.
    public static bool CanTurnOn(int load, int extra, int capacity)
    {
        return false;
    }

    // The next circuit to turn back on, in the fuse box's order: with rain,
    // the water pump ("PUMP", 800 W) first, if it fits; otherwise the
    // fridge ("FRIDGE", 300 W), if it fits; otherwise "WAIT".
    public static string NextCircuit(int load, int capacity, bool raining)
    {
        return "";
    }
}
