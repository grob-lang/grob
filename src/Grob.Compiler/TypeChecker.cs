using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Two-pass type checker (D-166). Annotates the AST in place — sets
/// <see cref="IdentifierExpr.ResolvedType"/> and <see cref="IdentifierExpr.Declaration"/>
/// on every identifier node — and accumulates all type diagnostics into the
/// <see cref="DiagnosticBag"/> without stopping at the first error.
/// Emits no bytecode; that is Increment D.
/// </summary>
/// <remarks>
/// <para><b>Pass 1 — registration.</b> Walks every top-level <c>fn</c> and <c>type</c>
/// declaration and registers the name in the global scope. This is what lets a
/// function body reference a function declared later in the same file (D-166).</para>
/// <para><b>Pass 2 — validation.</b> Walks all top-level items in source order,
/// resolving names, inferring types, checking arithmetic and comparison rules, and
/// reporting every violation via the diagnostic bag.</para>
/// <para><b>Cascade suppression.</b> When a sub-expression already produced an error,
/// its parent resolves to the compiler-internal <see cref="GrobType.Error"/> sentinel,
/// which is universally assignable. This ensures a single mistake does not cascade
/// into a storm of derived diagnostics.</para>
/// </remarks>
public sealed partial class TypeChecker : AstVisitor<GrobType> {
    private readonly DiagnosticBag _diagnostics;
    private readonly Stack<Dictionary<string, Symbol>> _scopes = new();

    /// <summary>Initialises a new <see cref="TypeChecker"/> that writes into <paramref name="diagnostics"/>.</summary>
    public TypeChecker(DiagnosticBag diagnostics) {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Runs the two-pass type check over <paramref name="unit"/>, mutating
    /// identifier nodes in-place and accumulating all diagnostics into the bag
    /// supplied at construction.
    /// </summary>
    public void Check(CompilationUnit unit) {
        ArgumentNullException.ThrowIfNull(unit);

        // Global scope lives for the whole compilation unit.
        _scopes.Push(new Dictionary<string, Symbol>());

        // Seed the global scope with built-in functions (D-270). Must run
        // before Pass 1 so that user-defined names cannot shadow built-ins
        // at the top-level scope, and so that call sites do not get E1001.
        RegisterBuiltins();

        // Pass 1 — register top-level fn and type declarations so that function
        // bodies can reference declarations appearing later in the same file (D-166).
        foreach (AstNode item in unit.TopLevel) {
            switch (item) {
                case FnDecl fn:
                    RegisterSymbol(fn.Name, GrobType.Unknown, fn.Range.Start, fn);
                    break;
                case TypeDecl td:
                    RegisterSymbol(td.Name, GrobType.Unknown, td.Range.Start, td);
                    break;
            }
        }

        // Pass 2 — validate all top-level items in source order.
        foreach (AstNode item in unit.TopLevel) {
            Visit(item);
        }

        _scopes.Pop();
    }

    // -----------------------------------------------------------------------
    // Fallback — return Unknown rather than throwing, so deferred node kinds
    // (Sprint 4+) are silently tolerated in Sprint 2 programs.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    protected override GrobType DefaultVisit(AstNode node) => GrobType.Unknown;

    // -----------------------------------------------------------------------
    // Error nodes — required abstract overrides (§29.2 contract).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitErrorExpr(ErrorExpr node) => GrobType.Error;

    /// <inheritdoc/>
    public override GrobType VisitErrorStmt(ErrorStmt node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitErrorDecl(ErrorDecl node) => GrobType.Unknown;

    // -----------------------------------------------------------------------
    // Private helpers.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Seeds the innermost scope with sentinel symbols for every Grob built-in
    /// function so that call sites resolve without emitting E1001.
    /// </summary>
    private void RegisterBuiltins() {
        RegisterBuiltinFn("print");
        RegisterBuiltinFn("exit");
        RegisterBuiltinFn("input");
    }

    private void RegisterBuiltinFn(string name) {
        BuiltinDecl decl = new(name);
        RegisterSymbol(name, GrobType.Unknown, SourceLocation.Unknown, decl);
    }

    /// <summary>
    /// Resolves a binding's final type from its optional annotation and its
    /// initializer's inferred type. Emits E0001 when annotation and initializer
    /// are incompatible.
    /// </summary>
    private GrobType ResolveBinding(TypeRef? annotation, GrobType initType, SourceRange initRange) {
        if (annotation is null) {
            // Pure type inference from the initializer.
            return initType;
        }

        GrobType annotated = ResolveTypeRef(annotation);
        if (annotated == GrobType.Unknown) return initType; // unrecognised annotation — be permissive

        if (initType == GrobType.Error) return GrobType.Error; // cascade suppression

        if (!TypesAreAssignable(initType, annotated)) {
            ErrorDescriptor descriptor =
                GrobTypeHelpers.IsNullable(initType) && !GrobTypeHelpers.IsNullable(annotated)
                    ? ErrorCatalog.E0104
                    : ErrorCatalog.E0001;
            EmitError(descriptor,
                $"Cannot assign value of type '{TypeName(initType)}' to binding of type '{TypeName(annotated)}'.",
                initRange);
            return GrobType.Error;
        }

        // Annotation wins (e.g. int → float widening is recorded as float).
        return annotated;
    }

    /// <summary>
    /// Maps a syntactic <see cref="TypeRef"/> to a <see cref="GrobType"/>,
    /// applying the nullable modifier when <see cref="TypeRef.IsNullable"/> is
    /// <c>true</c> (Sprint 3 Increment D — D-014).
    /// </summary>
    private static GrobType ResolveTypeRef(TypeRef typeRef) {
        GrobType baseType = typeRef.Name switch {
            "int" => GrobType.Int,
            "float" => GrobType.Float,
            "string" => GrobType.String,
            "bool" => GrobType.Bool,
            "nil" => GrobType.Nil,
            _ => GrobType.Unknown, // void, user-defined types, generics — deferred Sprint 5+
        };
        return typeRef.IsNullable ? GrobTypeHelpers.ToNullable(baseType) : baseType;
    }

    /// <summary>
    /// Returns <see langword="true"/> when a value of <paramref name="from"/> can
    /// be used where <paramref name="to"/> is expected.
    /// Rules (D-178, D-014):
    /// <list type="bullet">
    /// <item><description><c>int → float</c> is the only implicit widening conversion.</description></item>
    /// <item><description><c>nil</c> is assignable to any nullable type (<c>T?</c>).</description></item>
    /// <item><description>A non-nullable <c>T</c> is assignable to its nullable counterpart <c>T?</c>.</description></item>
    /// <item><description>A nullable <c>T?</c> is NOT assignable to non-nullable <c>T</c> (requires explicit unwrap via <c>??</c>).</description></item>
    /// </list>
    /// </summary>
    private static bool TypesAreAssignable(GrobType from, GrobType to) {
        if (from == GrobType.Error || to == GrobType.Error) return true; // Error is universal
        if (from == to) return true;
        if (from == GrobType.Int && to == GrobType.Float) return true; // only implicit widening
        // nil is assignable to any nullable type.
        if (from == GrobType.Nil && GrobTypeHelpers.IsNullable(to)) return true;
        // T is assignable to T? (non-null value into nullable slot).
        if (GrobTypeHelpers.ToNullable(from) == to) return true;
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when both operand types are numeric
    /// (<c>int</c> or <c>float</c> or mixed).
    /// </summary>
    private static bool BothNumeric(GrobType left, GrobType right) =>
        (left == GrobType.Int || left == GrobType.Float) &&
        (right == GrobType.Int || right == GrobType.Float);

    private static bool IsComparisonOperator(BinaryOperator op) => op switch {
        BinaryOperator.Equal or BinaryOperator.NotEqual or
        BinaryOperator.Less or BinaryOperator.LessEqual or
        BinaryOperator.Greater or BinaryOperator.GreaterEqual => true,
        _ => false,
    };

    /// <summary>Looks up <paramref name="name"/> in the scope stack, inner-most first.</summary>
    private Symbol? LookupSymbol(string name) {
        foreach (Dictionary<string, Symbol> scope in _scopes) {
            if (scope.TryGetValue(name, out Symbol? symbol)) return symbol;
        }
        return null;
    }

    private void RegisterSymbol(string name, GrobType type, SourceLocation declaredAt, AstNode declarationNode) {
        _scopes.Peek()[name] = new Symbol {
            Name = name,
            Type = type,
            DeclaredAt = declaredAt,
            DeclarationNode = declarationNode,
        };
    }

    /// <summary>Emits an error diagnostic and returns <see cref="GrobType.Error"/>.</summary>
    private GrobType EmitErrorAndReturn(ErrorDescriptor descriptor, string message, SourceRange range) {
        _diagnostics.Add(Diagnostic.Of(descriptor, range, message));
        return GrobType.Error;
    }

    private void EmitError(ErrorDescriptor descriptor, string message, SourceRange range) {
        _diagnostics.Add(Diagnostic.Of(descriptor, range, message));
    }

    private static string TypeName(GrobType type) => type switch {
        GrobType.Int => "int",
        GrobType.Float => "float",
        GrobType.String => "string",
        GrobType.Bool => "bool",
        GrobType.Nil => "nil",
        GrobType.Error => "<error>",
        GrobType.NullableInt => "int?",
        GrobType.NullableFloat => "float?",
        GrobType.NullableString => "string?",
        GrobType.NullableBool => "bool?",
        _ => "unknown",
    };

    private static string OperatorSymbol(BinaryOperator op) => op switch {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.LessEqual => "<=",
        BinaryOperator.Greater => ">",
        BinaryOperator.GreaterEqual => ">=",
        BinaryOperator.And => "&&",
        BinaryOperator.Or => "||",
        BinaryOperator.NilCoalesce => "??",
        _ => "?",
    };
}
