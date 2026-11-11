namespace Candeia.Languages;

// One programming language the player can write in. A language checks the player's code (syntax, the
// API allow-list and its own rules) and, from delivery 2 on, runs it in the isolated runner. Nights are
// described in neutral terms, so adding a language means adding one implementation of this contract.
public interface ILanguageRuntime
{
    string Id { get; }
    CheckResult Check(string source, CheckOptions options);
}
