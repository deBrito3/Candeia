namespace Candeia.Languages;

// One problem found in the player's code, explained in both game languages. Line and Column are 1-based.
public sealed record CodeDiagnostic(string Code, int Line, int Column, string MessagePt, string MessageEn, CodeFix? Fix = null)
{
    public string Message(bool english) => english ? MessageEn : MessagePt;
    public string Format(bool english) => (english ? $"Line {Line}: " : $"Linha {Line}: ") + Message(english);
}

// A corrected version of one line (1-based), offered when the intent of a mistake is clear.
public sealed record CodeFix(int Line, string Text);

// What a night allows. LINQ opens in Act 3; radio nights are console programs instead of a class.
public sealed record CheckOptions(bool AllowLinq = false, bool ConsoleProgram = false);

public sealed record CheckResult(IReadOnlyList<CodeDiagnostic> Errors, TimeSpan Elapsed)
{
    public bool Success => Errors.Count == 0;
}

// The compiled player code and its line information, ready to be sent to the isolated runner.
public sealed record CompileResult(IReadOnlyList<CodeDiagnostic> Errors, byte[]? Assembly, byte[]? Pdb, TimeSpan Elapsed)
{
    public bool Success => Errors.Count == 0 && Assembly is not null;
}
