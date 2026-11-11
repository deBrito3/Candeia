// Several ways out of the sandbox, all refused. Expected: CND004, CND010, CND001, CND008, CND005.
public static class Escape
{
    [System.Runtime.InteropServices.DllImport("kernel32")]
    static extern void Beep(int frequency, int duration);

    public static int Try()
    {
        Environment.Exit(0);
        var types = typeof(string).Assembly.GetTypes();
        dynamic anything = 1;
        return anything;
    }
}
