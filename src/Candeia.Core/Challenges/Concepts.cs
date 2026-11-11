namespace Candeia.Challenges;

// How a night's requirement is named to the player, for messages such as
// "the night is lit, but the system still needs a variable of type double".
public static class Concepts
{
    public static Bilingual Describe(string concept)
    {
        if (concept.StartsWith("declare:", StringComparison.Ordinal))
        {
            string type = concept["declare:".Length..];
            return new($"uma variável do tipo `{type}`", $"a variable of type `{type}`");
        }
        if (concept.StartsWith("operator:", StringComparison.Ordinal))
        {
            string op = concept["operator:".Length..];
            return new($"o operador `{op}`", $"the `{op}` operator");
        }
        return concept switch
        {
            "conditional" => new("o operador `?:` rodando de verdade", "the `?:` operator really running"),
            _ => new($"um `{concept}` rodando de verdade", $"a `{concept}` really running"),
        };
    }
}
