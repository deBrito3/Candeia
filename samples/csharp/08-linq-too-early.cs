// LINQ before Act 3. Expected: CS1061 explaining that LINQ opens in Act 3. With --linq: OK.
public static class Lamps
{
    public static int CountOn(List<int> watts)
    {
        return watts.Where(w => w > 0).Count();
    }
}
