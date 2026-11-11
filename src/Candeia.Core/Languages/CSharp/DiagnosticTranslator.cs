using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Candeia.Languages.CSharp;

// Turns the compiler's errors into explanations for someone still learning C#. The most common mistakes
// get an explanation of their own, with a one-click fix when the intent is clear; every other error
// falls back to the compiler's official Portuguese and English messages.
static class DiagnosticTranslator
{
    static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR"), English = CultureInfo.GetCultureInfo("en-US");

    static readonly string[] TypeKeywords = ["int", "long", "double", "float", "decimal", "bool", "char", "string", "object", "void", "var"];
    static readonly string[] LibraryTypes = ["Math", "Console", "List", "Dictionary", "HashSet", "Queue", "Stack", "StringBuilder", "Exception"];
    static readonly Dictionary<string, string> TypeAliases = new(StringComparer.Ordinal)
    {
        ["Int"] = "int", ["Integer"] = "int", ["integer"] = "int", ["boolean"] = "bool", ["Bool"] = "bool",
        ["Str"] = "string", ["str"] = "string", ["Float"] = "float", ["list"] = "List", ["dictionary"] = "Dictionary",
    };

    sealed record Context(Diagnostic Diagnostic, SemanticModel Model, SyntaxNode Node, string Word, string LineText, int Line, int Column);
    sealed record Friendly(string Pt, string En, CodeFix? Fix = null);

    public static CodeDiagnostic Translate(Diagnostic diagnostic, SemanticModel model)
    {
        var tree = model.SyntaxTree;
        var text = tree.GetText();
        var span = diagnostic.Location.SourceSpan;
        var start = diagnostic.Location.GetLineSpan().StartLinePosition;
        var node = tree.GetRoot().FindNode(span.Length == 0 && span.Start > 0 ? new(span.Start - 1, 1) : span, getInnermostNodeForTie: true);
        var context = new Context(diagnostic, model, node, text.ToString(span), text.Lines[start.Line].ToString(), start.Line + 1, start.Character + 1);
        Friendly? friendly = diagnostic.Id switch
        {
            "CS1002" => Semicolon(context),
            "CS1513" => new("faltou fechar uma chave `}`. Cada `{` precisa de uma `}` correspondente.",
                "a closing brace `}` is missing. Every `{` needs a matching `}`."),
            "CS1514" => new("faltou abrir uma chave `{`.", "an opening brace `{` is missing."),
            "CS1026" => new("faltou fechar um parêntese `)`.", "a closing parenthesis `)` is missing."),
            "CS1010" => new("um texto entre aspas não foi fechado nesta linha. Feche-o com `\"`.",
                "a text in quotes is not closed on this line. Close it with `\"`."),
            "CS0103" => UnknownName(context),
            "CS0246" => UnknownType(context),
            "CS0029" => Conversion(context),
            "CS0266" => ExplicitConversion(context),
            "CS0161" => new($"o método `{context.Word}` nem sempre devolve um valor. Todo caminho do método precisa terminar em `return`.",
                $"the method `{context.Word}` does not always return a value. Every path through the method must end in `return`."),
            "CS0165" => new($"a variável `{context.Word}` está sendo usada antes de receber um valor.",
                $"the variable `{context.Word}` is used before it gets a value."),
            "CS0128" => new($"já existe uma variável chamada `{context.Word}` neste trecho. Escolha outro nome ou use a variável que já existe.",
                $"there is already a variable called `{context.Word}` here. Choose another name or use the existing variable."),
            "CS0841" => new($"a variável `{context.Word}` é usada antes da linha em que é declarada. Declare-a antes de usá-la.",
                $"the variable `{context.Word}` is used before the line where it is declared. Declare it before using it."),
            "CS0127" => new("este método é `void` e não devolve nada, então o `return` não pode ter um valor.",
                "this method is `void` and returns nothing, so `return` cannot have a value."),
            "CS0126" => new("este `return` precisa devolver um valor do tipo do método.",
                "this `return` must give back a value of the method's type."),
            "CS0201" => UselessStatement(context),
            "CS1061" or "CS0117" => MissingMember(context),
            "CS8805" => new("nesta noite o seu código fica dentro de uma classe. Comandos soltos, fora de um método, não são aceitos.",
                "in this night your code lives inside a class. Loose statements outside a method are not accepted."),
            _ => null,
        };
        if (friendly is not null) return new(diagnostic.Id, context.Line, context.Column, friendly.Pt, friendly.En, friendly.Fix);
        return new(diagnostic.Id, context.Line, context.Column, diagnostic.GetMessage(Portuguese), diagnostic.GetMessage(English));
    }

    static Friendly Semicolon(Context c)
    {
        string trimmed = c.LineText.TrimEnd();
        var fix = trimmed.Length > 0 && !trimmed.EndsWith(';') && !c.LineText.Contains("//", StringComparison.Ordinal) ? new CodeFix(c.Line, trimmed + ";") : null;
        return new("faltou o ponto e vírgula (`;`) no fim da linha.", "the semicolon (`;`) at the end of the line is missing.", fix);
    }

    static Friendly UnknownName(Context c)
    {
        string? suggestion = c.Word is "print" or "println" or "printf" ? "Console.WriteLine"
            : NameSuggestions.Closest(c.Word, ScopeNames(c).Concat(["true", "false", "null", .. LibraryTypes]));
        if (suggestion is null)
            return new($"não conheço `{c.Word}`. Confira se o nome está escrito certo e se a variável foi declarada antes de ser usada.",
                $"I don't know `{c.Word}`. Check the spelling and that the variable was declared before it is used.");
        return new($"não conheço `{c.Word}`. Você quis dizer `{suggestion}`?", $"I don't know `{c.Word}`. Did you mean `{suggestion}`?", Replace(c, suggestion));
    }

    static Friendly UnknownType(Context c)
    {
        string? suggestion = TypeAliases.TryGetValue(c.Word, out var alias) ? alias
            : NameSuggestions.Closest(c.Word, TypeKeywords.Concat(LibraryTypes).Concat(OwnTypeNames(c)));
        if (suggestion is null)
            return new($"não conheço o tipo `{c.Word}`.", $"I don't know the type `{c.Word}`.");
        return new($"não conheço o tipo `{c.Word}`. Você quis dizer `{suggestion}`?", $"I don't know the type `{c.Word}`. Did you mean `{suggestion}`?", Replace(c, suggestion));
    }

    static Friendly? Conversion(Context c)
    {
        // "if (hour = 18)": a single = assigns; comparing needs ==.
        var assignment = c.Node.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
        if (assignment is not null && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && IsCondition(assignment))
        {
            var op = assignment.OperatorToken.GetLocation().GetLineSpan().StartLinePosition;
            CodeFix? fix = op.Line + 1 == c.Line ? new(c.Line, c.LineText.Insert(op.Character, "=")) : null;
            return new("para comparar dois valores use `==`. Um único `=` guarda um valor na variável.",
                "to compare two values use `==`. A single `=` stores a value in the variable.", fix);
        }
        if (Types(c) is not { } types) return null;
        var (from, to) = types;
        string hintPt = from == "string" && to is "int" or "double" ? $" Para transformar um texto em número, use `{to}.Parse(texto)`." : "";
        string hintEn = from == "string" && to is "int" or "double" ? $" To turn a text into a number, use `{to}.Parse(text)`." : "";
        return new($"aqui é preciso um valor do tipo `{to}`, mas este valor é do tipo `{from}`.{hintPt}",
            $"a value of type `{to}` is needed here, but this value is of type `{from}`.{hintEn}");
    }

    static Friendly? ExplicitConversion(Context c)
    {
        if (Types(c) is not { } types) return null;
        var (from, to) = types;
        return new($"um `{from}` não vira `{to}` sozinho, porque pode perder informação. Se o valor pode ter casas decimais, declare a variável como `{from}`. Se você quer mesmo converter, escreva `({to})` antes do valor.",
            $"a `{from}` does not become `{to}` by itself, because information could be lost. If the value may have decimals, declare the variable as `{from}`. If you really want to convert, write `({to})` before the value.");
    }

    static Friendly UselessStatement(Context c)
    {
        var comparison = c.Node.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().FirstOrDefault(b => b.IsKind(SyntaxKind.EqualsExpression));
        if (comparison is not null)
        {
            var op = comparison.OperatorToken.GetLocation().GetLineSpan().StartLinePosition;
            CodeFix? fix = op.Line + 1 == c.Line ? new(c.Line, c.LineText.Remove(op.Character, 1)) : null;
            return new("para guardar um valor numa variável use um único `=`. O `==` só compara.",
                "to store a value in a variable use a single `=`. `==` only compares.", fix);
        }
        return new("esta linha calcula algo, mas não faz nada com o resultado. Guarde-o numa variável, use-o num `return` ou chame um método.",
            "this line computes something but does nothing with the result. Store it in a variable, use it in a `return` or call a method.");
    }

    // LINQ methods look like missing members while the night has not opened LINQ (no System.Linq using).
    static readonly HashSet<string> LinqMethods =
    [
        "Where", "Select", "SelectMany", "OrderBy", "OrderByDescending", "ThenBy", "GroupBy", "First", "FirstOrDefault",
        "Last", "LastOrDefault", "Single", "Any", "All", "Sum", "Average", "Min", "Max", "Distinct", "Take", "Skip",
        "ToList", "ToArray", "ToDictionary", "Aggregate", "Zip", "Reverse",
    ];

    static Friendly MissingMember(Context c)
    {
        if (LinqMethods.Contains(c.Word) && !c.Model.Compilation.SyntaxTrees.Any(t => t.ToString().Contains("System.Linq", StringComparison.Ordinal)))
            return new($"`{c.Word}` faz parte do LINQ, que só é liberado no Ato 3. Por enquanto, use laços como `for` e `foreach`.",
                $"`{c.Word}` is part of LINQ, which only opens in Act 3. For now, use loops such as `for` and `foreach`.");
        ITypeSymbol? type = null;
        if (c.Node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>() is { } access)
            type = c.Model.GetTypeInfo(access.Expression).Type ?? c.Model.GetSymbolInfo(access.Expression).Symbol as ITypeSymbol;
        string typeName = type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "?";
        List<string> members = type is null ? [] : MemberNames(type).ToList();
        string? suggestion = c.Word.ToLowerInvariant() is "size" or "len" or "length" or "count"
            ? members.FirstOrDefault(m => m is "Length" or "Count")
            : null;
        suggestion ??= NameSuggestions.Closest(c.Word, members);
        if (suggestion is null)
            return new($"`{typeName}` não tem `{c.Word}`.", $"`{typeName}` has no `{c.Word}`.");
        return new($"`{typeName}` não tem `{c.Word}`. Você quis dizer `{suggestion}`?", $"`{typeName}` has no `{c.Word}`. Did you mean `{suggestion}`?", Replace(c, suggestion));
    }

    static (string From, string To)? Types(Context c)
    {
        var expression = c.Node as ExpressionSyntax ?? c.Node.FirstAncestorOrSelf<ExpressionSyntax>();
        if (expression is null) return null;
        var info = c.Model.GetTypeInfo(expression);
        if (info.Type is null || info.ConvertedType is null || info.Type is IErrorTypeSymbol || info.ConvertedType is IErrorTypeSymbol) return null;
        return (Name(info.Type), Name(info.ConvertedType));
    }

    static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    static bool IsCondition(ExpressionSyntax expression) => expression.Parent switch
    {
        IfStatementSyntax s => s.Condition == expression,
        WhileStatementSyntax s => s.Condition == expression,
        DoStatementSyntax s => s.Condition == expression,
        ForStatementSyntax s => s.Condition == expression,
        ConditionalExpressionSyntax s => s.Condition == expression,
        ParenthesizedExpressionSyntax p => IsCondition(p),
        _ => false,
    };

    static IEnumerable<string> ScopeNames(Context c) => c.Model.LookupSymbols(c.Diagnostic.Location.SourceSpan.Start)
        .Where(s => s.Kind is SymbolKind.Local or SymbolKind.Parameter or SymbolKind.Field or SymbolKind.Property or SymbolKind.Method or SymbolKind.RangeVariable
            || s is INamedTypeSymbol t && SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, c.Model.Compilation.Assembly))
        .Select(s => s.Name);

    static IEnumerable<string> OwnTypeNames(Context c) => c.Model.LookupSymbols(c.Diagnostic.Location.SourceSpan.Start)
        .OfType<INamedTypeSymbol>()
        .Where(t => SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, c.Model.Compilation.Assembly))
        .Select(t => t.Name);

    static IEnumerable<string> MemberNames(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers())
                if (member.DeclaredAccessibility == Accessibility.Public && member.CanBeReferencedByName && member is not INamedTypeSymbol)
                    yield return member.Name;
    }

    // Replaces the word the diagnostic points at, when it sits on one line.
    static CodeFix? Replace(Context c, string replacement)
    {
        int at = c.Column - 1;
        if (c.Word.Length == 0 || at < 0 || at + c.Word.Length > c.LineText.Length || c.LineText.Substring(at, c.Word.Length) != c.Word) return null;
        return new(c.Line, c.LineText[..at] + replacement + c.LineText[(at + c.Word.Length)..]);
    }
}
