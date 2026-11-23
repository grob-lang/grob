using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Torture.Mutation;

/// <summary>
/// What an operator reads to find its candidates: the source, where its tokens sit and the tree
/// the parser returned. The tree is the parser's, not the checker's, so an operator that needs a
/// name or a type reads it from a declaration's own syntax.
/// </summary>
internal sealed class MutationContext {
    private const string FileName = "mutation.grob";

    private readonly Dictionary<object, object?> _parents = new(ReferenceEqualityComparer.Instance);

    private MutationContext(string source, TokenSpans tokens, CompilationUnit unit, IReadOnlyList<AstEntry> nodes) {
        Source = source;
        Tokens = tokens;
        Unit = unit;
        Nodes = nodes;
        foreach (AstEntry entry in nodes) {
            _parents[entry.Node] = entry.Parent;
        }
    }

    /// <summary>The program text.</summary>
    public string Source { get; }

    /// <summary>Where each token sits.</summary>
    public TokenSpans Tokens { get; }

    /// <summary>The parsed program.</summary>
    public CompilationUnit Unit { get; }

    /// <summary>Every node of the tree, parents first.</summary>
    public IReadOnlyList<AstEntry> Nodes { get; }

    /// <summary>
    /// Lexes and parses <paramref name="source"/>, or returns <see langword="null"/> when either
    /// phase throws. A seed the front end cannot read is still a seed for the oracle, but an
    /// operator has no tree to read, and the oracle reports the exception from the seed itself.
    /// </summary>
    public static MutationContext? TryCreate(string source) {
        try {
            var bag = new DiagnosticBag();
            IReadOnlyList<Token> tokens = Lexer.Scan(source, bag, FileName);
            CompilationUnit unit = Parser.Parse(tokens, bag);
            return new MutationContext(source, new TokenSpans(source, tokens), unit, AstNodes.Enumerate(unit));
        } catch (Exception) {
            return null;
        }
    }

    /// <summary>Every node of type <typeparamref name="T"/>, with its parent.</summary>
    public IEnumerable<(T Node, object? Parent)> Of<T>() where T : class =>
        Nodes.Where(e => e.Node is T).Select(e => ((T)e.Node, e.Parent));

    /// <summary>The nearest statement or declaration that holds <paramref name="node"/>, itself included, or <see langword="null"/>.</summary>
    public AstNode? EnclosingStatement(object node) {
        for (object? current = node; current is not null; _parents.TryGetValue(current, out current)) {
            if (current is Statement or Declaration) return (AstNode)current;
        }
        return null;
    }

    /// <summary>The node that holds <paramref name="node"/>, or <see langword="null"/> for the compilation unit.</summary>
    public object? ParentOf(object node) => _parents.GetValueOrDefault(node);

    /// <summary>The index of the token at which <paramref name="node"/> starts, or -1.</summary>
    public int FirstToken(object node) =>
        AstNodes.RangeOf(node) is { } range ? Tokens.IndexAt(range.Start) : -1;

    /// <summary>
    /// The first and last token of the expression <paramref name="expression"/>, or
    /// <see langword="null"/> when its start is not a token start or no token is left for it.
    /// </summary>
    public (int First, int Last)? ExpressionTokens(Expression expression) {
        int first = FirstToken(expression);
        if (first < 0) return null;
        int last = LastToken(expression, first);
        return last < first ? null : (first, last);
    }

    // A node's Range.End is not its end (R-74): a binary or a call ends at the start of its last
    // token, and a literal's range is a point. So the end comes from the tokens. The left operand of
    // a binary ends before its operator, which sits before the right operand's start, which the
    // parser records exactly; the right operand ends where the binary does; anything else runs to
    // the next newline, comma or closing bracket at its own nesting.
    private int LastToken(Expression expression, int first) {
        if (ParentOf(expression) is not BinaryExpr parent) return Tokens.ExpressionLast(first);
        if (!ReferenceEquals(parent.Left, expression)) {
            int parentFirst = FirstToken(parent);
            return parentFirst < 0 ? -1 : LastToken(parent, parentFirst);
        }
        int rightFirst = FirstToken(parent.Right);
        return rightFirst - 2;
    }

    /// <summary>The source offsets <c>[start, end)</c> of an expression's extent.</summary>
    public (int Start, int End)? ExpressionSpan(Expression expression) =>
        ExpressionTokens(expression) is var (first, last) ? (Tokens.Start(first), Tokens.End(last)) : null;

    /// <summary>
    /// Every value position an operator may edit: a binding's initialiser, a call argument, a
    /// returned value and a field's value.
    /// </summary>
    public IEnumerable<Expression> ValuePositions() {
        foreach (AstEntry entry in Nodes) {
            switch (entry.Node) {
                case VarDeclStmt v: yield return v.Initializer; break;
                case CallArgument a: yield return a.Value; break;
                case ReturnStmt { Value: { } value }: yield return value; break;
                case FieldInit f: yield return f.Value; break;
            }
        }
    }

    /// <summary>The names the program declares: bindings, constants, readonly bindings, parameters and functions.</summary>
    public IReadOnlyList<string> DeclaredNames() => [.. Nodes
        .Select(e => e.Node switch {
            VarDeclStmt v => v.Name,
            ConstDecl c => c.Name,
            ReadonlyDecl r => r.Name,
            Parameter p => p.Name,
            FnDecl f => f.Name,
            _ => null,
        })
        .OfType<string>()
        .Distinct(StringComparer.Ordinal)];
}
