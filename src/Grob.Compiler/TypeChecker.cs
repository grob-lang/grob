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
public sealed class TypeChecker : AstVisitor<GrobType> {
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
    // Literals — return the exact scalar type so Increment D can choose opcodes.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitIntLiteral(IntLiteralExpr node) => GrobType.Int;

    /// <inheritdoc/>
    public override GrobType VisitFloatLiteral(FloatLiteralExpr node) => GrobType.Float;

    /// <inheritdoc/>
    public override GrobType VisitStringLiteral(StringLiteralExpr node) => GrobType.String;

    /// <inheritdoc/>
    public override GrobType VisitRawStringLiteral(RawStringLiteralExpr node) => GrobType.String;

    /// <inheritdoc/>
    public override GrobType VisitBoolLiteral(BoolLiteralExpr node) => GrobType.Bool;

    /// <inheritdoc/>
    public override GrobType VisitNilLiteral(NilLiteralExpr node) => GrobType.Nil;

    /// <inheritdoc/>
    public override GrobType VisitRegexLiteral(RegexLiteralExpr node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitInterpolatedString(InterpolatedStringExpr node) {
        foreach (StringInterpolationPart part in node.Parts) {
            if (part is StringExpressionPart expr) {
                Visit(expr.Expression);
            }
        }
        return GrobType.String;
    }

    // -----------------------------------------------------------------------
    // Identifier resolution (§3.1.1).
    // Sets ResolvedType and Declaration on the node; emits E1001 if undefined.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitIdentifier(IdentifierExpr node) {
        Symbol? symbol = LookupSymbol(node.Name);
        if (symbol is null) {
            EmitError("E1001", $"Undefined identifier '{node.Name}'.", node.Range);
            node.ResolvedType = GrobType.Error;
            // node.Declaration remains null — no declaring node exists.
            return GrobType.Error;
        }
        node.ResolvedType = symbol.Type;
        node.Declaration = symbol.DeclarationNode;
        return symbol.Type;
    }

    // -----------------------------------------------------------------------
    // Unary expressions.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitUnary(UnaryExpr node) {
        GrobType operand = Visit(node.Operand);
        if (operand == GrobType.Error) return GrobType.Error; // cascade suppression

        return node.Operator switch {
            UnaryOperator.Negate when operand == GrobType.Int => GrobType.Int,
            UnaryOperator.Negate when operand == GrobType.Float => GrobType.Float,
            UnaryOperator.Not when operand == GrobType.Bool => GrobType.Bool,
            UnaryOperator.Negate => EmitErrorAndReturn("E0002",
                $"Operator '-' cannot be applied to type '{TypeName(operand)}'.", node.Range),
            UnaryOperator.Not => EmitErrorAndReturn("E0002",
                $"Operator '!' cannot be applied to type '{TypeName(operand)}'.", node.Range),
            _ => GrobType.Unknown,
        };
    }

    // -----------------------------------------------------------------------
    // Binary expressions — arithmetic, comparison, logical.
    // The resolved type is exact (Int vs Float vs String vs Bool) so Increment D
    // can select AddInt / AddFloat / Concat without any further inspection.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitBinary(BinaryExpr node) {
        GrobType left = Visit(node.Left);
        GrobType right = Visit(node.Right);

        // Cascade suppression: if either side already errored, suppress derived diagnostics.
        if (left == GrobType.Error || right == GrobType.Error) return GrobType.Error;

        if (IsComparisonOperator(node.Operator)) return ResolveComparison(node, left, right);
        if (node.Operator == BinaryOperator.And || node.Operator == BinaryOperator.Or) return ResolveLogical(node, left, right);
        if (node.Operator == BinaryOperator.NilCoalesce) return GrobType.Unknown; // Sprint 5+

        return ResolveArithmetic(node, left, right);
    }

    private GrobType ResolveArithmetic(BinaryExpr node, GrobType left, GrobType right) {
        // string + string → string (Concat)
        if (node.Operator == BinaryOperator.Add && left == GrobType.String && right == GrobType.String) {
            return GrobType.String;
        }

        // int op int → int (including int / int → int, truncating)
        if (left == GrobType.Int && right == GrobType.Int) return GrobType.Int;

        // float op float → float
        if (left == GrobType.Float && right == GrobType.Float) return GrobType.Float;

        // int op float or float op int → float (only implicit conversion in Grob)
        if ((left == GrobType.Int && right == GrobType.Float) ||
            (left == GrobType.Float && right == GrobType.Int)) {
            return GrobType.Float;
        }

        // All other combinations are type errors — e.g. int + string.
        return EmitErrorAndReturn("E0002",
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    private GrobType ResolveComparison(BinaryExpr node, GrobType left, GrobType right) {
        // == and != accept same-type operands or mixed numeric operands.
        if (node.Operator == BinaryOperator.Equal || node.Operator == BinaryOperator.NotEqual) {
            if (left == right || BothNumeric(left, right)) return GrobType.Bool;
            return EmitErrorAndReturn("E0002",
                $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
                node.Range);
        }

        // <, <=, >, >= require numeric (int/float, mixed ok) or same-string operands.
        if (BothNumeric(left, right) || (left == GrobType.String && right == GrobType.String)) {
            return GrobType.Bool;
        }

        return EmitErrorAndReturn("E0002",
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    private GrobType ResolveLogical(BinaryExpr node, GrobType left, GrobType right) {
        if (left == GrobType.Bool && right == GrobType.Bool) return GrobType.Bool;
        string sym = node.Operator == BinaryOperator.And ? "&&" : "||";
        if (left != GrobType.Bool) {
            return EmitErrorAndReturn("E0002",
                $"Operator '{sym}' cannot be applied to type '{TypeName(left)}'.", node.Left.Range);
        }
        return EmitErrorAndReturn("E0002",
            $"Operator '{sym}' cannot be applied to type '{TypeName(right)}'.", node.Right.Range);
    }

    // -----------------------------------------------------------------------
    // Grouping — transparent wrapper; result type is the inner expression's type.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitGrouping(GroupingExpr node) => Visit(node.Inner);

    // -----------------------------------------------------------------------
    // Deferred expressions (Sprint 5+) — visit children to keep identifier
    // resolution working even inside deferred constructs.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitCall(CallExpr node) {
        Visit(node.Callee);
        foreach (CallArgument arg in node.Arguments) Visit(arg.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitMemberAccess(MemberAccessExpr node) {
        Visit(node.Target);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitIndex(IndexExpr node) {
        Visit(node.Target);
        Visit(node.Index);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitArrayLiteral(ArrayLiteralExpr node) {
        foreach (Expression element in node.Elements) Visit(element);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitTernary(TernaryExpr node) {
        Visit(node.Condition);
        Visit(node.Then);
        Visit(node.Else);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitNumericRange(NumericRangeExpr node) {
        Visit(node.Start);
        Visit(node.End);
        if (node.Step is not null) Visit(node.Step);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitLambda(LambdaExpr node) => GrobType.Unknown; // Sprint 5+

    // -----------------------------------------------------------------------
    // Statements — visit children; return Unknown (statements have no type).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitBlock(BlockStmt node) {
        _scopes.Push(new Dictionary<string, Symbol>());
        foreach (Statement stmt in node.Statements) Visit(stmt);
        _scopes.Pop();
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitVarDecl(VarDeclStmt node) {
        GrobType initType = Visit(node.Initializer);
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Initializer.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitExpressionStmt(ExpressionStmt node) {
        Visit(node.Expression);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitReturn(ReturnStmt node) {
        if (node.Value is not null) Visit(node.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitAssignment(AssignmentStmt node) {
        Visit(node.Target);
        Visit(node.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitCompoundAssignment(CompoundAssignmentStmt node) {
        Visit(node.Target);
        Visit(node.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitIf(IfStmt node) {
        Visit(node.Condition);
        Visit(node.Then);
        if (node.Else is not null) Visit(node.Else);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitWhile(WhileStmt node) {
        Visit(node.Condition);
        Visit(node.Body);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitForIn(ForInStmt node) {
        Visit(node.Iterable);
        Visit(node.Body);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitIncrement(IncrementStmt node) {
        Visit(node.Target);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitSelect(SelectStmt node) {
        Visit(node.Subject);
        foreach (CaseClause c in node.Cases) {
            foreach (Expression pattern in c.Patterns) Visit(pattern);
            Visit(c.Body);
        }
        if (node.Default is not null) Visit(node.Default);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitTry(TryStmt node) {
        Visit(node.Body);
        foreach (CatchClause c in node.Catches) Visit(c.Body);
        if (node.Finally is not null) Visit(node.Finally);
        return GrobType.Unknown;
    }

    // -----------------------------------------------------------------------
    // Declarations — Pass 2 validation.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitFnDecl(FnDecl node) {
        // The fn name was already registered in pass 1; don't re-register here.
        // Push a scope for parameters, then visit the body (which pushes its own scope).
        _scopes.Push(new Dictionary<string, Symbol>());
        foreach (Parameter p in node.Parameters) {
            GrobType paramType = p.Type is not null ? ResolveTypeRef(p.Type) : GrobType.Unknown;
            // Use the owning FnDecl as the declaring node — Parameter is not an AstNode.
            RegisterSymbol(p.Name, paramType, p.Range.Start, node);
            if (p.DefaultValue is not null) Visit(p.DefaultValue);
        }
        Visit(node.Body);
        _scopes.Pop();
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitTypeDecl(TypeDecl node) => GrobType.Unknown; // Sprint 6

    /// <inheritdoc/>
    public override GrobType VisitConstDecl(ConstDecl node) {
        GrobType initType = Visit(node.Value);
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Value.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitReadonlyDecl(ReadonlyDecl node) {
        GrobType initType = Visit(node.Value);
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Value.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitParamBlockDecl(ParamBlockDecl node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitImportDecl(ImportDecl node) => GrobType.Unknown;

    // -----------------------------------------------------------------------
    // Private helpers.
    // -----------------------------------------------------------------------

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
            EmitError("E0001",
                $"Cannot assign value of type '{TypeName(initType)}' to binding of type '{TypeName(annotated)}'.",
                initRange);
            return GrobType.Error;
        }

        // Annotation wins (e.g. int → float widening is recorded as float).
        return annotated;
    }

    /// <summary>Maps a syntactic <see cref="TypeRef"/> to a <see cref="GrobType"/>.</summary>
    private static GrobType ResolveTypeRef(TypeRef typeRef) => typeRef.Name switch {
        "int" => GrobType.Int,
        "float" => GrobType.Float,
        "string" => GrobType.String,
        "bool" => GrobType.Bool,
        "nil" => GrobType.Nil,
        _ => GrobType.Unknown, // void, user-defined types, generics — deferred Sprint 5+
    };

    /// <summary>
    /// Returns <see langword="true"/> when a value of <paramref name="from"/> can
    /// be used where <paramref name="to"/> is expected. The only implicit
    /// conversion in Grob is <c>int → float</c> (D-178).
    /// </summary>
    private static bool TypesAreAssignable(GrobType from, GrobType to) {
        if (from == GrobType.Error || to == GrobType.Error) return true; // Error is universal
        if (from == to) return true;
        if (from == GrobType.Int && to == GrobType.Float) return true; // only implicit conversion
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
    private GrobType EmitErrorAndReturn(string code, string message, SourceRange range) {
        _diagnostics.Add(new Diagnostic(code, message, range, Severity.Error));
        return GrobType.Error;
    }

    private void EmitError(string code, string message, SourceRange range) {
        _diagnostics.Add(new Diagnostic(code, message, range, Severity.Error));
    }

    private static string TypeName(GrobType type) => type switch {
        GrobType.Int => "int",
        GrobType.Float => "float",
        GrobType.String => "string",
        GrobType.Bool => "bool",
        GrobType.Nil => "nil",
        GrobType.Error => "<error>",
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
