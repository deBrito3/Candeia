using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Candeia.Languages.CSharp;

// The API allow-list. Every symbol the player's code names must be the player's own, or belong to a type
// on this list. Anything else (files, network, processes, reflection, environment, threads, randomness,
// the clock) is refused before the code is compiled. The list is deliberately an allow-list: a type that
// is not named here is refused, so a new .NET API never becomes reachable by accident.
sealed class ApiPolicy
{
    static readonly HashSet<string> AllowedNamespaces = ["System", "System.Collections", "System.Collections.Generic", "System.Text"];

    // Metadata names (generic arity after the backtick). Nested types of an allowed type are allowed too,
    // so enumerators and key/value collections work.
    static readonly HashSet<string> AllowedTypes =
    [
        "System.Object", "System.String", "System.Char", "System.Boolean", "System.Void",
        "System.SByte", "System.Byte", "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
        "System.Int64", "System.UInt64", "System.Single", "System.Double", "System.Decimal",
        "System.Math", "System.MathF", "System.Array", "System.Nullable`1", "System.Enum", "System.ValueType",
        "System.Index", "System.Range", "System.CharEnumerator", "System.Span`1", "System.ReadOnlySpan`1",
        "System.StringComparison", "System.StringSplitOptions", "System.MidpointRounding",
        "System.IComparable", "System.IComparable`1", "System.IEquatable`1", "System.IDisposable",
        "System.Predicate`1", "System.Comparison`1", "System.Console", "System.Convert",
        "System.Exception", "System.ArgumentException", "System.ArgumentNullException", "System.ArgumentOutOfRangeException",
        "System.InvalidOperationException", "System.NotImplementedException", "System.NotSupportedException",
        "System.IndexOutOfRangeException", "System.DivideByZeroException", "System.FormatException",
        "System.OverflowException", "System.ArithmeticException", "System.NullReferenceException",
        "System.Text.StringBuilder",
        "System.Collections.IEnumerable", "System.Collections.IEnumerator",
        "System.Collections.Generic.List`1", "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.Queue`1", "System.Collections.Generic.Stack`1", "System.Collections.Generic.LinkedList`1",
        "System.Collections.Generic.LinkedListNode`1", "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2", "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.KeyValuePair`2", "System.Collections.Generic.KeyNotFoundException",
        "System.Collections.Generic.IEnumerable`1", "System.Collections.Generic.IEnumerator`1",
        "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IList`1", "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyCollection`1", "System.Collections.Generic.IReadOnlyList`1",
        "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IComparer`1", "System.Collections.Generic.IEqualityComparer`1",
        "System.Collections.Generic.Comparer`1", "System.Collections.Generic.EqualityComparer`1",
    ];

    static readonly HashSet<string> LinqTypes =
    [
        "System.Linq.Enumerable", "System.Linq.IOrderedEnumerable`1", "System.Linq.IGrouping`2", "System.Linq.ILookup`2",
    ];

    // Delegate and tuple families of every arity.
    static readonly string[] AllowedPrefixes = ["System.Func`", "System.Action", "System.ValueTuple"];

    // Members of allowed types that still lead out of the sandbox or to reflection.
    static readonly HashSet<string> DeniedMembers = ["System.Object.GetType"];
    static readonly HashSet<string> ConsoleMembers = ["Write", "WriteLine"];

    readonly CSharpCompilation compilation;
    readonly SemanticModel model;
    readonly CheckOptions options;

    ApiPolicy(CSharpCompilation compilation, SyntaxTree tree, CheckOptions options)
    {
        this.compilation = compilation;
        model = compilation.GetSemanticModel(tree);
        this.options = options;
    }

    public static IEnumerable<CodeDiagnostic> Find(CSharpCompilation compilation, SyntaxTree tree, CheckOptions options) =>
        new ApiPolicy(compilation, tree, options).Walk(tree.GetRoot());

    // One refusal per line is enough: "System.IO.File.Delete" would otherwise report the namespace, the
    // type and the method separately.
    IEnumerable<CodeDiagnostic> Walk(SyntaxNode root)
    {
        var reported = new HashSet<int>();
        foreach (var node in root.DescendantNodes())
        {
            foreach (var symbol in SymbolsOf(node))
            {
                var violation = Violation(symbol);
                if (violation is null) continue;
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
                if (!reported.Add(line)) break;
                yield return Describe(violation, node.GetLocation());
                break;
            }
        }
    }

    IEnumerable<ISymbol> SymbolsOf(SyntaxNode node)
    {
        switch (node)
        {
            case ExpressionSyntax expression:
                if (model.GetSymbolInfo(expression).Symbol is { } symbol) yield return symbol;
                if (expression is InitializerExpressionSyntax initializer && initializer.IsKind(SyntaxKind.CollectionInitializerExpression))
                    foreach (var item in initializer.Expressions)
                        if (model.GetCollectionInitializerSymbolInfo(item).Symbol is { } add) yield return add;
                break;
            case ConstructorInitializerSyntax constructor:
                if (model.GetSymbolInfo(constructor).Symbol is { } baseConstructor) yield return baseConstructor;
                break;
            case QueryClauseSyntax clause:
                var info = model.GetQueryClauseInfo(clause);
                if (info.OperationInfo.Symbol is { } operation) yield return operation;
                if (info.CastInfo.Symbol is { } cast) yield return cast;
                break;
            case SelectOrGroupClauseSyntax selectOrGroup:
                if (model.GetSymbolInfo(selectOrGroup).Symbol is { } select) yield return select;
                break;
        }
    }

    sealed record Refusal(string Name, string Category);

    Refusal? Violation(ISymbol symbol) => symbol switch
    {
        ILocalSymbol or IParameterSymbol or ILabelSymbol or IRangeVariableSymbol or IDiscardSymbol or IPreprocessingSymbol => null,
        IAliasSymbol alias => Violation(alias.Target),
        INamespaceSymbol ns => NamespaceAllowed(ns) ? null : new(ns.ToDisplayString(), "namespace"),
        ITypeSymbol type => TypeViolation(type),
        IMethodSymbol method => MemberViolation(method.ReducedFrom ?? method),
        IPropertySymbol or IFieldSymbol or IEventSymbol => MemberViolation(symbol),
        _ => new(symbol.ToDisplayString(), "api"),
    };

    bool Own(ISymbol symbol) => SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);

    bool NamespaceAllowed(INamespaceSymbol ns)
    {
        if (ns.IsGlobalNamespace) return true;
        string name = ns.ToDisplayString();
        return AllowedNamespaces.Contains(name) || options.AllowLinq && name == "System.Linq";
    }

    Refusal? TypeViolation(ITypeSymbol type)
    {
        if (type is IErrorTypeSymbol or ITypeParameterSymbol || Own(type)) return null;
        switch (type)
        {
            case IArrayTypeSymbol array: return TypeViolation(array.ElementType);
            case IDynamicTypeSymbol: return new("dynamic", "dynamic");
            case IPointerTypeSymbol or IFunctionPointerTypeSymbol: return new(type.ToDisplayString(), "api");
            case INamedTypeSymbol named:
                if (named.IsTupleType)
                {
                    foreach (var element in named.TupleElements)
                        if (TypeViolation(element.Type) is { } elementViolation) return elementViolation;
                    return null;
                }
                var definition = named.OriginalDefinition;
                if (LinqTypes.Contains(MetadataName(definition)) && !options.AllowLinq) return new(named.Name, "linq");
                if (!DefinitionAllowed(definition)) return new(Display(named), Category(named));
                foreach (var argument in named.TypeArguments)
                    if (TypeViolation(argument) is { } argumentViolation) return argumentViolation;
                return null;
            default: return new(type.ToDisplayString(), "api");
        }
    }

    bool DefinitionAllowed(INamedTypeSymbol definition)
    {
        if (definition.ContainingType is { } outer) return DefinitionAllowed(outer.OriginalDefinition);
        string name = MetadataName(definition);
        return AllowedTypes.Contains(name)
            || AllowedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
            || options.AllowLinq && LinqTypes.Contains(name);
    }

    Refusal? MemberViolation(ISymbol member)
    {
        if (Own(member)) return null;
        var type = member.ContainingType;
        string memberName = $"{type.Name}.{member.Name}";
        if (TypeViolation(type) is { } typeViolation)
            return typeViolation.Category == "linq" ? new(member.Name, "linq") : typeViolation with { Name = memberName };
        string full = MetadataName(type.OriginalDefinition) + "." + member.Name;
        if (DeniedMembers.Contains(full)) return new(memberName, "reflection");
        if (MetadataName(type.OriginalDefinition) == "System.Console" && !ConsoleMembers.Contains(member.Name)) return new(memberName, "console");
        if (SignatureTypes(member).Any(Reflective)) return new(memberName, "reflection");
        return null;
    }

    static IEnumerable<ITypeSymbol> SignatureTypes(ISymbol member) => member switch
    {
        IMethodSymbol method => method.Parameters.Select(p => p.Type).Prepend(method.ReturnType),
        IPropertySymbol property => property.Parameters.Select(p => p.Type).Prepend(property.Type),
        IFieldSymbol field => [field.Type],
        IEventSymbol e => [e.Type],
        _ => [],
    };

    // Types that only exist to inspect or call code by name.
    static bool Reflective(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return Reflective(array.ElementType);
        if (type is not INamedTypeSymbol named) return false;
        string ns = named.ContainingNamespace?.ToDisplayString() ?? "";
        return MetadataName(named.OriginalDefinition) is "System.Type" or "System.Delegate" or "System.RuntimeTypeHandle"
            || ns.StartsWith("System.Reflection", StringComparison.Ordinal)
            || ns.StartsWith("System.Runtime.InteropServices", StringComparison.Ordinal);
    }

    static string MetadataName(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } outer) return MetadataName(outer) + "+" + type.MetadataName;
        var ns = type.ContainingNamespace;
        return ns is null || ns.IsGlobalNamespace ? type.MetadataName : ns.ToDisplayString() + "." + type.MetadataName;
    }

    static string Display(INamedTypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    static string Category(INamedTypeSymbol type)
    {
        string name = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (name.StartsWith("System.IO", StringComparison.Ordinal) || name.StartsWith("System.Net", StringComparison.Ordinal)
            || name.StartsWith("System.Diagnostics", StringComparison.Ordinal) || type.Name is "Environment" or "AppDomain" or "GC")
            return "outside";
        if (name.StartsWith("System.Reflection", StringComparison.Ordinal) || type.Name is "Type" or "Activator" or "Delegate" or "MulticastDelegate")
            return "reflection";
        if (name.StartsWith("System.Threading", StringComparison.Ordinal)) return "threads";
        if (type.Name is "Random" or "DateTime" or "DateTimeOffset" or "TimeProvider" or "Guid") return "random";
        return "api";
    }

    static CodeDiagnostic Describe(Refusal refusal, Location location)
    {
        string name = refusal.Name;
        return refusal.Category switch
        {
            "namespace" => LanguageRestrictions.At("CND011", location,
                $"o namespace `{name}` não está disponível no sistema da casa.",
                $"the `{name}` namespace is not available in the house system."),
            "linq" => LanguageRestrictions.At("CND002", location,
                $"`{name}` faz parte do LINQ, que só é liberado no Ato 3. Por enquanto, use laços como `for` e `foreach`.",
                $"`{name}` is part of LINQ, which only opens in Act 3. For now, use loops such as `for` and `foreach`."),
            "dynamic" => LanguageRestrictions.At("CND005", location,
                "`dynamic` não está disponível. Declare o tipo da variável, como `int` ou `string`.",
                "`dynamic` is not available. Declare the variable's type, such as `int` or `string`."),
            "console" => LanguageRestrictions.At("CND001", location,
                $"`{name}` não está disponível. Do `Console`, o sistema da casa só aceita `Console.Write` e `Console.WriteLine`.",
                $"`{name}` is not available. From `Console`, the house system only accepts `Console.Write` and `Console.WriteLine`."),
            "outside" => LanguageRestrictions.At("CND001", location,
                $"`{name}` não está disponível. O sistema da casa não deixa o seu código mexer em arquivos, na rede ou em outros programas.",
                $"`{name}` is not available. The house system does not let your code touch files, the network or other programs."),
            "reflection" => LanguageRestrictions.At("CND001", location,
                $"`{name}` não está disponível. O sistema da casa não deixa o código inspecionar nem chamar outro código pelo nome.",
                $"`{name}` is not available. The house system does not let code inspect or call other code by name."),
            "threads" => LanguageRestrictions.At("CND006", location,
                $"`{name}` não está disponível. O sistema da casa roda o seu código numa única linha de execução.",
                $"`{name}` is not available. The house system runs your code on a single thread."),
            "random" => LanguageRestrictions.At("CND001", location,
                $"`{name}` não está disponível. Nas noites, o mesmo código precisa dar sempre o mesmo resultado, então não há sorteios nem relógio.",
                $"`{name}` is not available. In the nights the same code must always give the same result, so there is no randomness or clock."),
            _ => LanguageRestrictions.At("CND001", location,
                $"`{name}` não está disponível no sistema da casa. Nas noites você pode usar tipos simples, textos, `Math`, coleções e `Console.WriteLine`.",
                $"`{name}` is not available in the house system. The nights allow simple types, strings, `Math`, collections and `Console.WriteLine`."),
        };
    }
}
