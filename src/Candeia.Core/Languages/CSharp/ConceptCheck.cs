using System.Text;
using Candeia.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Candeia.Languages.CSharp;

// Whether the player's code really uses what the night teaches. A mark ("if", "for"...) must have run at
// least once in the night's tests; "declare:<type>" needs a local variable of that type (var counts by
// its inferred type); "operator:<op>" needs the operator in an expression or a compound assignment.
static class ConceptCheck
{
    public static IReadOnlyList<string> Missing(string source, IEnumerable<string> concepts, IReadOnlyDictionary<string, long> marks,
        CSharpParseOptions parseOptions, Func<SyntaxTree, CSharpCompilation> compile)
    {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions, CSharpRuntime.PlayerFileName);
        var root = tree.GetRoot();
        SemanticModel? model = null;
        var missing = new List<string>();
        foreach (var concept in concepts)
        {
            bool present;
            if (Probe.MarkNames.Contains(concept)) present = marks.GetValueOrDefault(concept) > 0;
            else if (concept.StartsWith("declare:", StringComparison.Ordinal))
            {
                model ??= compile(tree).GetSemanticModel(tree);
                present = Declares(root, model, concept["declare:".Length..]);
            }
            else if (concept.StartsWith("operator:", StringComparison.Ordinal)) present = Uses(root, concept["operator:".Length..]);
            else present = false;
            if (!present) missing.Add(concept);
        }
        return missing;
    }

    static bool Declares(SyntaxNode root, SemanticModel model, string type)
    {
        string wanted = type.Replace(" ", "");
        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            if (declaration.Parent is not (LocalDeclarationStatementSyntax or ForStatementSyntax or UsingStatementSyntax)) continue;
            var declared = model.GetTypeInfo(declaration.Type).Type;
            if (declared is not null && declared.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Replace(" ", "") == wanted) return true;
        }
        return false;
    }

    static bool Uses(SyntaxNode root, string op) => root.DescendantNodes().Any(node => node switch
    {
        BinaryExpressionSyntax binary => binary.OperatorToken.Text == op,
        AssignmentExpressionSyntax assignment => assignment.OperatorToken.Text == op + "=",
        PrefixUnaryExpressionSyntax prefix => op == "!" && prefix.IsKind(SyntaxKind.LogicalNotExpression),
        _ => false,
    });
}
