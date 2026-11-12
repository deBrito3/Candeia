// Reference solution: lights every lamp and uses every required concept. Not shipped with the game.
public static class FuseBox
{
    public static int TotalLoad(int lights, int fridge, int pump)
    {
        return lights + fridge + pump;
    }

    public static double Usage(int load, int capacity)
    {
        return (double)load * 100 / capacity;
    }

    public static bool CanTurnOn(int load, int extra, int capacity)
    {
        return load + extra <= capacity;
    }

    public static string NextCircuit(int load, int capacity, bool raining)
    {
        if (raining && CanTurnOn(load, 800, capacity))
        {
            return "PUMP";
        }
        else if (CanTurnOn(load, 300, capacity))
        {
            return "FRIDGE";
        }
        else
        {
            return "WAIT";
        }
    }
}
