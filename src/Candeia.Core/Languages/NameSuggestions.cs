namespace Candeia.Languages;

// "Did you mean ...?" for misspelt names: the closest candidate by edit distance, ignoring case first.
static class NameSuggestions
{
    public static string? Closest(string typed, IEnumerable<string> candidates)
    {
        string? best = null;
        int bestDistance = int.MaxValue;
        int limit = typed.Length <= 3 ? 1 : 2;
        foreach (var candidate in candidates.Distinct())
        {
            if (candidate == typed || candidate.Length == 0) continue;
            int distance = string.Equals(candidate, typed, StringComparison.OrdinalIgnoreCase) ? 0 : Distance(typed, candidate);
            if (distance <= limit && distance < bestDistance) { best = candidate; bestDistance = distance; }
        }
        return best;
    }

    // Optimal string alignment distance: insertions, deletions, substitutions and swaps of neighbours,
    // so "lihgt" is one step from "light".
    public static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}
