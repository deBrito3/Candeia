using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Candeia.Languages.CSharp;

// Parts of the C# language the nights never need and that would open a way around the API allow-list
// or the isolated runner: unsafe code and pointers, attributes (DllImport), extern, dynamic code paths,
// threads and await, reflection keywords, finalizers and names reserved for the step counters.
static class LanguageRestrictions
{
    public static IEnumerable<CodeDiagnostic> Find(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            if (token.IsKind(SyntaxKind.UnsafeKeyword)) yield return Unsafe(token.GetLocation());
            else if (token.IsKind(SyntaxKind.ExternKeyword)) yield return At("CND010", token.GetLocation(),
                "`extern` não está disponível: o seu código não pode chamar programas de fora do jogo.",
                "`extern` is not available: your code cannot call programs outside the game.");
            else if (token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText.StartsWith("__", StringComparison.Ordinal))
                yield return At("CND007", token.GetLocation(),
                    "nomes que começam com dois sublinhados (`__`) são reservados pelo sistema. Escolha outro nome.",
                    "names that start with two underscores (`__`) are reserved by the system. Choose another name.");
        }
        foreach (var node in root.DescendantNodes(descendIntoTrivia: true))
        {
            switch (node)
            {
                case PointerTypeSyntax or FunctionPointerTypeSyntax or FixedStatementSyntax
                    or StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax
                    or RefTypeExpressionSyntax or MakeRefExpressionSyntax or RefValueExpressionSyntax:
                    yield return Unsafe(node.GetLocation()); break;
                case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.AddressOfExpression) || prefix.IsKind(SyntaxKind.PointerIndirectionExpression):
                    yield return Unsafe(node.GetLocation()); break;
                case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.PointerMemberAccessExpression):
                    yield return Unsafe(node.GetLocation()); break;
                case ExpressionSyntax expression when expression.IsKind(SyntaxKind.ArgListExpression):
                    yield return Unsafe(node.GetLocation()); break;
                case AttributeListSyntax:
                    yield return At("CND004", node.GetLocation(),
                        "atributos como `[...]` não são usados nas noites. Remova o atributo.",
                        "attributes such as `[...]` are not used in the nights. Remove the attribute.");
                    break;
                case AwaitExpressionSyntax:
                    yield return Single("await", node.GetLocation()); break;
                case LockStatementSyntax:
                    yield return Single("lock", node.GetLocation()); break;
                case TypeOfExpressionSyntax:
                    yield return Keyword("typeof", node.GetLocation()); break;
                case SizeOfExpressionSyntax:
                    yield return Keyword("sizeof", node.GetLocation()); break;
                case DestructorDeclarationSyntax destructor:
                    yield return At("CND009", destructor.Identifier.GetLocation(),
                        $"destrutores (`~{destructor.Identifier.ValueText}`) não são usados nas noites.",
                        $"destructors (`~{destructor.Identifier.ValueText}`) are not used in the nights.");
                    break;
                case LineDirectiveTriviaSyntax or LineSpanDirectiveTriviaSyntax:
                    yield return At("CND013", node.GetLocation(),
                        "a diretiva `#line` não é aceita, porque muda a numeração das linhas.",
                        "the `#line` directive is not accepted, because it changes the line numbers.");
                    break;
            }
        }
    }

    static CodeDiagnostic Unsafe(Location location) => At("CND003", location,
        "código `unsafe`, ponteiros e acesso direto à memória não existem no sistema da casa. Use variáveis comuns.",
        "`unsafe` code, pointers and direct memory access do not exist in the house system. Use ordinary variables.");

    static CodeDiagnostic Single(string keyword, Location location) => At("CND006", location,
        $"`{keyword}` não está disponível. O sistema da casa roda o seu código numa única linha de execução.",
        $"`{keyword}` is not available. The house system runs your code on a single thread.");

    static CodeDiagnostic Keyword(string keyword, Location location) => At("CND008", location,
        $"`{keyword}` não está disponível nas noites.",
        $"`{keyword}` is not available in the nights.");

    internal static CodeDiagnostic At(string code, Location location, string pt, string en, CodeFix? fix = null)
    {
        var start = location.GetLineSpan().StartLinePosition;
        return new(code, start.Line + 1, start.Character + 1, pt, en, fix);
    }
}
