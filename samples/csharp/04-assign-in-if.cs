// Expected: CS0029 on line 6, explaining `==`, with a fix.
public static class Gate
{
    public static bool Opens(int code)
    {
        if (code = 1234)
        {
            return true;
        }
        return false;
    }
}
