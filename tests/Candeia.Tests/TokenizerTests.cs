using Candeia.Languages.CSharp;

namespace Candeia.Tests;

// The light scan the editor uses for colours and completion.
static class TokenizerTests
{
    public static void Run(TestRunner t)
    {
        const string code = "public static int Watts() // 60\n{\n    double v = 220.5;\n    string s = \"if\";\n    char c = 'x';\n    return 60;\n}";
        var tokens = CSharpTokenizer.Tokenize(code).ToList();
        string Text(CodeToken token) => code.Substring(token.Start, token.Length);
        TokenKind? Kind(string piece) => tokens.Where(k => Text(k) == piece).Select(k => (TokenKind?)k.Kind).FirstOrDefault();
        t.Check(Kind("public") == TokenKind.Keyword && Kind("return") == TokenKind.Keyword, "keywords");
        t.Check(Kind("int") == TokenKind.Type && Kind("double") == TokenKind.Type && Kind("string") == TokenKind.Type, "built-in types");
        t.Check(Kind("Watts") == TokenKind.Name && Kind("v") == TokenKind.Name, "names");
        t.Check(Kind("220.5") == TokenKind.Number && Kind("60") == TokenKind.Number, "numbers");
        t.Check(Kind("\"if\"") == TokenKind.Text && Kind("'x'") == TokenKind.Text, "a keyword inside a string is text");
        t.Check(Kind("// 60") == TokenKind.Comment, "a line comment runs to the end of the line");
        t.Check(CSharpTokenizer.Tokenize("/* open\nint x").Single().Kind == TokenKind.Comment, "an open block comment runs to the end");
        t.Check(CSharpTokenizer.Tokenize("s = \"open\nint x").Any(k => k.Kind == TokenKind.Type), "an open string stops at the end of its line");

        t.Check(CSharpTokenizer.InCommentOrText("int x; // re", 12) && CSharpTokenizer.InCommentOrText("s = \"re", 7), "completion is quiet inside a comment or an open string");
        t.Check(!CSharpTokenizer.InCommentOrText("s = \"a\"; re", 11) && !CSharpTokenizer.InCommentOrText("int re", 6), "and works after a closed string and in code");
        t.Check(CSharpTokenizer.DeclaredNames(code).SequenceEqual(["Watts", "v", "s", "c"]), $"declared names: {string.Join(",", CSharpTokenizer.DeclaredNames(code))}");
    }
}
