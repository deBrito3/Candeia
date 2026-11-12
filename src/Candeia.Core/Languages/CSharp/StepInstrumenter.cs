using Candeia.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Candeia.Languages.CSharp;

// Inserts calls to the probe into the player's checked code:
//   - a step at the start of every loop body, method, constructor, local function, accessor and lambda
//     block, so an endless loop or call chain runs out of steps instead of freezing the night;
//   - a mark before every if, switch, ?:, for, foreach, while and do, so a night can require that a
//     construction really ran;
//   - a call count per method the player declared;
//   - with countLines, a count before every statement, by line, for the post-night review.
// The calls are inserted as text without line breaks, so every line keeps its number and errors still
// point at the player's own lines.
static class StepInstrumenter
{
    public sealed record Result(string Source, IReadOnlyList<string> Methods, int Lines);

    public static Result Instrument(SyntaxTree tree, bool countLines = false)
    {
        var edits = new Edits();
        var methods = new List<string>();
        var text = tree.GetText();
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            if (countLines && node is StatementSyntax counted and not BlockSyntax and not LocalFunctionStatementSyntax and not EmptyStatementSyntax)
                CountLine(edits, counted, text.Lines.GetLineFromPosition(counted.SpanStart).LineNumber + 1);
            switch (node)
            {
                case MethodDeclarationSyntax method:
                    Body(edits, method.Body, method.ExpressionBody, method.SemicolonToken, Enter(methods, method.Identifier.ValueText), IsVoid(method.ReturnType));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    Body(edits, constructor.Body, constructor.ExpressionBody, constructor.SemicolonToken, Enter(methods, constructor.Identifier.ValueText), isVoid: true);
                    break;
                case LocalFunctionStatementSyntax local:
                    Body(edits, local.Body, local.ExpressionBody, local.SemicolonToken, Enter(methods, local.Identifier.ValueText), IsVoid(local.ReturnType));
                    break;
                case OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax:
                    var op = (BaseMethodDeclarationSyntax)node;
                    Body(edits, op.Body, op.ExpressionBody, op.SemicolonToken, Step, isVoid: false);
                    break;
                case AccessorDeclarationSyntax { Body: { } accessorBody }:
                    edits.Open(accessorBody.OpenBraceToken.Span.End, Step);
                    break;
                case AnonymousFunctionExpressionSyntax { Block: { } lambdaBody }:
                    edits.Open(lambdaBody.OpenBraceToken.Span.End, Step);
                    break;
                case ForStatementSyntax loop: Loop(edits, loop, loop.Statement, "for"); break;
                case CommonForEachStatementSyntax loop: Loop(edits, loop, loop.Statement, "foreach"); break;
                case WhileStatementSyntax loop: Loop(edits, loop, loop.Statement, "while"); break;
                case DoStatementSyntax loop: Loop(edits, loop, loop.Statement, "do"); break;
                case IfStatementSyntax statement: Wrap(edits, statement, "if"); break;
                case SwitchStatementSyntax statement: Wrap(edits, statement, "switch"); break;
                case ConditionalExpressionSyntax conditional:
                    edits.Open(conditional.Condition.SpanStart, $"{Probe.Call}MarkBool({Probe.Mark("conditional")}, ");
                    edits.Close(conditional.Condition.Span.End, ")");
                    break;
                case SwitchExpressionSyntax expression:
                    edits.Open(expression.GoverningExpression.SpanStart, $"{Probe.Call}MarkValue({Probe.Mark("switch")}, ");
                    edits.Close(expression.GoverningExpression.Span.End, ")");
                    break;
            }
        }
        return new(edits.Apply(text), methods, text.Lines.Count);
    }

    // A statement in a block or a switch section gets its count in front; one standing alone (the body of
    // an if or an else) also gets braces, so the count does not become the body in its place.
    static void CountLine(Edits edits, StatementSyntax statement, int line)
    {
        string count = $"{Probe.Call}Line({line});";
        if (statement.Parent is BlockSyntax or SwitchSectionSyntax) edits.Open(statement.SpanStart, count);
        else
        {
            edits.Open(statement.SpanStart, "{" + count);
            edits.Close(statement.Span.End, "}");
        }
    }

    static readonly string Step = $"{Probe.Call}Step();";

    static string Enter(List<string> methods, string name)
    {
        methods.Add(name);
        return $"{Probe.Call}Enter({methods.Count - 1});";
    }

    static bool IsVoid(TypeSyntax type) => type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    // A block body gets the call after its "{". An expression body "=> x;" becomes "{ call; return x; }".
    static void Body(Edits edits, BlockSyntax? block, ArrowExpressionClauseSyntax? arrow, SyntaxToken semicolon, string call, bool isVoid)
    {
        if (block is not null) { edits.Open(block.OpenBraceToken.Span.End, call); return; }
        if (arrow is null || semicolon.IsMissing) return;
        bool statement = isVoid || arrow.Expression is ThrowExpressionSyntax;
        edits.Replace(arrow.ArrowToken.Span, "{" + call + (statement ? "" : "return "));
        edits.Replace(semicolon.Span, ";}");
    }

    static void Loop(Edits edits, StatementSyntax loop, StatementSyntax body, string mark)
    {
        Wrap(edits, loop, mark);
        if (body is BlockSyntax block) edits.Open(block.OpenBraceToken.Span.End, Step);
        else { edits.Open(body.SpanStart, "{" + Step); edits.Close(body.Span.End, "}"); }
    }

    static void Wrap(Edits edits, StatementSyntax statement, string mark)
    {
        edits.Open(statement.SpanStart, $"{{{Probe.Call}Mark({Probe.Mark(mark)});");
        edits.Close(statement.Span.End, "}");
    }

    // Insertions grouped by position. Nodes are visited outer first, so at one position an outer opening
    // comes before an inner one and an inner closing before an outer one; closings come before openings.
    sealed class Edits
    {
        readonly SortedDictionary<int, (List<string> Closes, List<string> Opens)> inserts = [];
        readonly List<TextChange> replacements = [];

        (List<string> Closes, List<string> Opens) At(int position)
        {
            if (!inserts.TryGetValue(position, out var slot)) inserts[position] = slot = ([], []);
            return slot;
        }

        public void Open(int position, string text) => At(position).Opens.Add(text);
        public void Close(int position, string text) => At(position).Closes.Insert(0, text);
        public void Replace(TextSpan span, string text) => replacements.Add(new(span, text));

        public string Apply(SourceText source)
        {
            var changes = inserts.Select(pair => new TextChange(new TextSpan(pair.Key, 0), string.Concat(pair.Value.Closes) + string.Concat(pair.Value.Opens)))
                .Concat(replacements)
                .OrderBy(change => change.Span.Start).ThenBy(change => change.Span.Length);
            return source.WithChanges(changes).ToString();
        }
    }
}
