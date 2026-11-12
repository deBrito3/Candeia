using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Candeia.Languages.CSharp;

public enum TokenKind { Keyword, Type, Text, Number, Comment, Name }

public readonly record struct CodeToken(int Start, int Length, TokenKind Kind);

// Splits C# into the pieces the editor colours and the completion reads. It is a light, forgiving scan
// for unfinished code as it is typed, not the compiler's parser: an open string or comment simply runs to
// the end of its line (or of the text, for a block comment).
public static partial class CSharpTokenizer
{
    public static readonly FrozenSet<string> Keywords = new[]
    {
        "abstract", "as", "base", "break", "case", "catch", "checked", "class", "const", "continue", "default", "delegate",
        "do", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "for", "foreach", "goto", "if",
        "implicit", "in", "interface", "internal", "is", "lock", "namespace", "new", "null", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "record", "ref", "return", "sealed", "sizeof", "stackalloc",
        "static", "struct", "switch", "this", "throw", "true", "try", "typeof", "unchecked", "unsafe", "using", "virtual",
        "volatile", "while", "when", "where", "yield", "get", "set", "init", "and", "or", "not",
    }.ToFrozenSet();

    public static readonly FrozenSet<string> Types = new[]
    {
        "bool", "byte", "char", "decimal", "double", "float", "int", "long", "object", "sbyte", "short", "string",
        "uint", "ulong", "ushort", "void", "var",
    }.ToFrozenSet();

    [GeneratedRegex("""
        (?<comment>//[^\n]*|/\*[\s\S]*?(?:\*/|\z))
        |(?<text>@"(?:""|[^"])*(?:"|\z)|\$?"(?:\\.|[^"\\\n])*(?:"|$)|'(?:\\.|[^'\\\n])*(?:'|$))
        |(?<number>\b\d[\d_]*(?:\.\d[\d_]*)?(?:[eE][+-]?\d+)?[fFdDmMlLuU]*\b)
        |(?<word>\b[A-Za-z_][A-Za-z0-9_]*\b)
        """, RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Pattern();

    public static IEnumerable<CodeToken> Tokenize(string text)
    {
        foreach (Match m in Pattern().Matches(text))
        {
            if (m.Groups["comment"].Success) yield return new(m.Index, m.Length, TokenKind.Comment);
            else if (m.Groups["text"].Success) yield return new(m.Index, m.Length, TokenKind.Text);
            else if (m.Groups["number"].Success) yield return new(m.Index, m.Length, TokenKind.Number);
            else yield return new(m.Index, m.Length, Keywords.Contains(m.Value) ? TokenKind.Keyword : Types.Contains(m.Value) ? TokenKind.Type : TokenKind.Name);
        }
    }

    // Whether the position falls inside a comment or a string, where completion has nothing to offer.
    public static bool InCommentOrText(string text, int position)
    {
        foreach (var token in Tokenize(text))
        {
            if (token.Start >= position) return false;
            int end = token.Start + token.Length;
            bool inside = token.Kind is TokenKind.Comment or TokenKind.Text && position > token.Start
                && (position < end || position == end && !Closed(text, token));
            if (inside) return true;
        }
        return false;
    }

    // An unfinished comment or string still covers the position right after it.
    static bool Closed(string text, CodeToken token)
    {
        string value = text.Substring(token.Start, token.Length);
        if (value.StartsWith("//", StringComparison.Ordinal)) return false;
        if (value.StartsWith("/*", StringComparison.Ordinal)) return value.Length >= 4 && value.EndsWith("*/", StringComparison.Ordinal);
        char quote = value[^1];
        return value.Length >= 2 && (quote == '"' || quote == '\'') && value[^2] != '\\';
    }

    // The names the player declared in the code: classes, methods, variables and parameters.
    public static IReadOnlyList<string> DeclaredNames(string text)
    {
        var names = new List<string>();
        foreach (Match m in Declaration().Matches(text))
        {
            string name = m.Groups["name"].Value;
            if (!Keywords.Contains(name) && !Types.Contains(name) && !names.Contains(name)) names.Add(name);
        }
        return names;
    }

    [GeneratedRegex(@"\b(?:class|struct|record|interface|enum)\s+(?<name>[A-Za-z_]\w*)|\b(?:[A-Za-z_][\w<>,\[\]?]*)\s+(?<name>[A-Za-z_]\w*)\s*(?=[=;,()])")]
    private static partial Regex Declaration();
}
