using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
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
        // D-279, E0102: interpolating a nullable expression is a compile error.
        // Each ${expr} slot is type-checked; if its resolved type is nullable the
        // slot is an error unless the expression itself has already resolved the
        // nullability (e.g. x ?? fallback → non-nullable).
        foreach (StringInterpolationPart part in node.Parts) {
            if (part is StringExpressionPart exprPart) {
                GrobType slotType = Visit(exprPart.Expression);
                if (GrobTypeHelpers.IsNullable(slotType)) {
                    EmitError(ErrorCatalog.E0102,
                        $"Interpolated expression has nullable type '{TypeName(slotType)}'; resolve with '??' before interpolating.",
                        exprPart.Range);
                }
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
            EmitError(ErrorCatalog.E1001, $"Undefined identifier '{node.Name}'.", node.Range);
            node.ResolvedType = GrobType.Error;
            node.Declaration = UnresolvedDecl.Instance; // §3.1.1 invariant: Declaration is never null after type-check (D-311).
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
            UnaryOperator.Negate => EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '-' cannot be applied to type '{TypeName(operand)}'.", node.Range),
            UnaryOperator.Not => EmitErrorAndReturn(ErrorCatalog.E0002,
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
        if (node.Operator == BinaryOperator.NilCoalesce) return ResolveNilCoalesce(node, left, right);

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
        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    private GrobType ResolveComparison(BinaryExpr node, GrobType left, GrobType right) {
        // == and != accept same-type operands or mixed numeric operands.
        if (node.Operator == BinaryOperator.Equal || node.Operator == BinaryOperator.NotEqual) {
            if (left == right || BothNumeric(left, right)) return GrobType.Bool;
            return EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
                node.Range);
        }

        // <, <=, >, >= require numeric (int/float, mixed ok) or same-string operands.
        if (BothNumeric(left, right) || (left == GrobType.String && right == GrobType.String)) {
            return GrobType.Bool;
        }

        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    private GrobType ResolveLogical(BinaryExpr node, GrobType left, GrobType right) {
        if (left == GrobType.Bool && right == GrobType.Bool) return GrobType.Bool;
        string sym = node.Operator == BinaryOperator.And ? "&&" : "||";
        if (left != GrobType.Bool) {
            return EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '{sym}' cannot be applied to type '{TypeName(left)}'.", node.Left.Range);
        }
        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{sym}' cannot be applied to type '{TypeName(right)}'.", node.Right.Range);
    }

    /// <summary>
    /// Resolves the type of a nil-coalescing expression <c>a ?? b</c>.
    /// <para>
    /// The <c>??</c> operator is eager (D-271): both operands are evaluated
    /// before the opcode runs — there is no short-circuit branching.
    /// </para>
    /// <para>Rules (Sprint 3 Increment D):</para>
    /// <list type="bullet">
    /// <item><description><c>T? ?? T  → T</c>  — left nullable, right non-nullable: result is the non-nullable element type.</description></item>
    /// <item><description><c>T? ?? T? → T?</c> — left nullable, right nullable: result stays nullable.</description></item>
    /// <item><description><c>T  ?? T  → T</c>  — left non-nullable (the ?? is a no-op at runtime): result is T.</description></item>
    /// </list>
    /// </summary>
    private GrobType ResolveNilCoalesce(BinaryExpr node, GrobType left, GrobType right) {
        // A nil literal on the left always yields the right operand's type —
        // the fallback always wins because nil ?? T ≡ T.
        if (left == GrobType.Nil) return right;

        // If the left side is nullable, the fallback (right) determines whether the
        // result is fully unwrapped or stays nullable.
        GrobType leftElem = GrobTypeHelpers.IsNullable(left)
            ? GrobTypeHelpers.ElementType(left)
            : left;

        // Right side must be the same base type or a nullable version of it.
        GrobType rightElem = GrobTypeHelpers.IsNullable(right)
            ? GrobTypeHelpers.ElementType(right)
            : right;

        if (leftElem != GrobType.Error && rightElem != GrobType.Error &&
            leftElem != GrobType.Unknown && rightElem != GrobType.Unknown &&
            leftElem != rightElem) {
            return EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '??' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}': element types '{TypeName(leftElem)}' and '{TypeName(rightElem)}' do not match.",
                node.Range);
        }

        // When both sides are Unknown (e.g. deferred member types), be permissive.
        if (leftElem == GrobType.Unknown || rightElem == GrobType.Unknown) return GrobType.Unknown;

        // If the right side is nullable the result stays nullable; otherwise it is non-nullable.
        return GrobTypeHelpers.IsNullable(right) ? left : leftElem;
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
        GrobType targetType = Visit(node.Target);

        // Cascade suppression: if the target already errored, don't pile on.
        if (targetType == GrobType.Error) return GrobType.Error;

        // Using '.' (non-optional) on a nullable receiver is a compile-time error (E0101).
        if (!node.IsOptional && GrobTypeHelpers.IsNullable(targetType)) {
            return EmitErrorAndReturn(ErrorCatalog.E0101,
                $"Member access via '.' on nullable type '{TypeName(targetType)}' may dereference nil. Use '?.' to chain or '??' to unwrap first.",
                node.Range);
        }

        // Member-type resolution for struct fields is deferred to Sprint 5.
        // For '?.' chains the result type is Unknown (nullable), so downstream '??'
        // operators treat it as a permissive Unknown and do not emit false positives.
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
    // Deferred to Sprint 5 (grob-lang/grob#44): lambda scoping, parameter type
    // inference, and closure capture are designed together. Partial traversal
    // now would register inferred-type parameters as Unknown — non-null but
    // semantically incorrect. Nothing downstream observes this gap today.
    public override GrobType VisitLambda(LambdaExpr node) => GrobType.Unknown;
}
