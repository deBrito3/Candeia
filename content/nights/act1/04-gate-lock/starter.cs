// CANDEIA · gate lock
// The iron gate has an electric lock with a combination. It opens only with
// the combination, and never at night, not even with it. Someone has been
// in this code: the gate opens for anyone.
public static class GateLock
{
    // The dangerous hours: from 6 pm (18) until before 6 am. The hour goes
    // from 0 to 23.
    public static bool IsDangerousHour(int hour)
    {
        return hour >= 18 && hour < 6;
    }

    // The gate opens with the combination "3107", and only outside the
    // dangerous hours.
    public static bool CanOpen(string code, int hour)
    {
        return code == "3107" || !IsDangerousHour(hour);
    }

    // What the panel by the gate shows: "DANGEROUS HOUR" in the dangerous
    // hours, even with the right combination; otherwise "WRONG CODE" for a
    // wrong combination; otherwise "OPEN".
    public static string Status(string code, int hour)
    {
        return "OPEN";
    }
}
