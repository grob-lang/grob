using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class Compiler {
    // -----------------------------------------------------------------------
    // Literals
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitIntLiteral(IntLiteralExpr node) {
        EmitConstant(GrobValue.FromInt(node.Value), node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitFloatLiteral(FloatLiteralExpr node) {
        EmitConstant(GrobValue.FromFloat(node.Value), node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitStringLiteral(StringLiteralExpr node) {
        EmitConstant(GrobValue.FromString(node.Value), node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitRawStringLiteral(RawStringLiteralExpr node) {
        EmitConstant(GrobValue.FromString(node.Value), node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 2: only handles plain strings (all-text parts, no <c>${ }</c>).
    /// Interpolated strings with embedded expressions are deferred to Sprint 4.
    /// </remarks>
    public override object? VisitInterpolatedString(InterpolatedStringExpr node) {
        // Concatenate all text parts into one value. For Sprint 2 programs the
        // parser may still produce an InterpolatedStringExpr even for plain
        // double-quoted strings with no ${ } interpolations.
        // OfType<StringTextPart> filters-and-casts in one step; avoids the
        // S3267 LINQ-vs-foreach finding while keeping the code readable.
        var sb = new System.Text.StringBuilder();
        foreach (StringTextPart text in node.Parts.OfType<StringTextPart>()) {
            sb.Append(text.Text);
            // StringExpressionPart (interpolation) — deferred to Sprint 4.
        }
        EmitConstant(GrobValue.FromString(sb.ToString()), node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitBoolLiteral(BoolLiteralExpr node) {
        _chunk.WriteOpCode(node.Value ? OpCode.True : OpCode.False, node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitNilLiteral(NilLiteralExpr node) {
        _chunk.WriteOpCode(OpCode.Nil, node.Range.Start.Line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Grouping
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitGrouping(GroupingExpr node) {
        Visit(node.Inner);
        return null;
    }

    // -----------------------------------------------------------------------
    // Identifier (variable read)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitIdentifier(IdentifierExpr node) {
        // const references are inlined as direct constant pool loads (D-293).
        if (node.Declaration is ConstDecl cd) {
            if (!_constValues.TryGetValue(cd, out GrobValue cv))
                throw new GrobInternalException(
                    $"Const '{node.Name}' was not cached before emission.");
            EmitConstant(cv, node.Range.Start.Line);
            return null;
        }
        EmitLoad(node.Name, node.Range.Start.Line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Unary
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitUnary(UnaryExpr node) {
        Visit(node.Operand);
        int line = node.Range.Start.Line;
        switch (node.Operator) {
            case UnaryOperator.Negate:
                _chunk.WriteOpCode(
                    GetExprType(node.Operand) == GrobType.Float
                        ? OpCode.NegateFloat
                        : OpCode.NegateInt,
                    line);
                break;
                // UnaryOperator.Not — deferred to Sprint 3 (boolean/comparison support)
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Binary
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitBinary(BinaryExpr node) {
        GrobType lt = GetExprType(node.Left);
        GrobType rt = GetExprType(node.Right);

        // String concatenation via Add.
        if (node.Operator == BinaryOperator.Add && lt == GrobType.String && rt == GrobType.String) {
            Visit(node.Left);
            Visit(node.Right);
            _chunk.WriteOpCode(OpCode.Concat, node.Range.Start.Line);
            return null;
        }

        // Arithmetic — determine whether either operand needs int→float coercion.
        bool leftNeedsCoerce = lt == GrobType.Int && rt == GrobType.Float;
        bool rightNeedsCoerce = rt == GrobType.Int && lt == GrobType.Float;
        GrobType resultType = (lt == GrobType.Float || rt == GrobType.Float)
            ? GrobType.Float
            : lt;

        Visit(node.Left);
        if (leftNeedsCoerce) {
            _chunk.WriteOpCode(OpCode.IntToFloat, node.Left.Range.Start.Line);
        }

        Visit(node.Right);
        if (rightNeedsCoerce) {
            _chunk.WriteOpCode(OpCode.IntToFloat, node.Right.Range.Start.Line);
        }

        _chunk.WriteOpCode(GetArithmeticOpCode(node.Operator, resultType), node.Range.Start.Line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Type helpers
    // -----------------------------------------------------------------------

    private static OpCode GetArithmeticOpCode(BinaryOperator op, GrobType type) => (op, type) switch {
        (BinaryOperator.Add, GrobType.Int) => OpCode.AddInt,
        (BinaryOperator.Add, GrobType.Float) => OpCode.AddFloat,
        (BinaryOperator.Subtract, GrobType.Int) => OpCode.SubtractInt,
        (BinaryOperator.Subtract, GrobType.Float) => OpCode.SubtractFloat,
        (BinaryOperator.Multiply, GrobType.Int) => OpCode.MultiplyInt,
        (BinaryOperator.Multiply, GrobType.Float) => OpCode.MultiplyFloat,
        (BinaryOperator.Divide, GrobType.Int) => OpCode.DivideInt,
        (BinaryOperator.Divide, GrobType.Float) => OpCode.DivideFloat,
        (BinaryOperator.Modulo, GrobType.Int) => OpCode.ModuloInt,
        (BinaryOperator.Modulo, GrobType.Float) => OpCode.ModuloFloat,
        _ => throw new InvalidOperationException(
            $"Operator {op} with result type {type} is not supported in Sprint 2.")
    };

    /// <summary>
    /// Statically determines the <see cref="GrobType"/> that <paramref name="node"/>
    /// evaluates to, using information that has already been annotated onto the
    /// AST by the type checker.
    /// </summary>
    private static GrobType GetExprType(Expression node) => node switch {
        IntLiteralExpr => GrobType.Int,
        FloatLiteralExpr => GrobType.Float,
        StringLiteralExpr => GrobType.String,
        RawStringLiteralExpr => GrobType.String,
        InterpolatedStringExpr => GrobType.String,
        BoolLiteralExpr => GrobType.Bool,
        NilLiteralExpr => GrobType.Nil,
        IdentifierExpr id => id.ResolvedType,
        GroupingExpr g => GetExprType(g.Inner),
        UnaryExpr u => GetUnaryResultType(u),
        BinaryExpr b => GetBinaryResultType(b),
        _ => GrobType.Unknown
    };

    private static GrobType GetUnaryResultType(UnaryExpr node) => node.Operator switch {
        UnaryOperator.Negate => GetExprType(node.Operand) == GrobType.Float
            ? GrobType.Float
            : GrobType.Int,
        UnaryOperator.Not => GrobType.Bool,
        _ => GrobType.Unknown
    };

    private static GrobType GetBinaryResultType(BinaryExpr node) {
        GrobType lt = GetExprType(node.Left);
        GrobType rt = GetExprType(node.Right);
        return node.Operator switch {
            BinaryOperator.Add
                or BinaryOperator.Subtract
                or BinaryOperator.Multiply
                or BinaryOperator.Divide
                or BinaryOperator.Modulo
                => ArithmeticResultType(lt, rt),
            BinaryOperator.Equal
                or BinaryOperator.NotEqual
                or BinaryOperator.Less
                or BinaryOperator.LessEqual
                or BinaryOperator.Greater
                or BinaryOperator.GreaterEqual
                or BinaryOperator.And
                or BinaryOperator.Or
                => GrobType.Bool,
            _ => GrobType.Unknown
        };
    }

    private static GrobType ArithmeticResultType(GrobType left, GrobType right) {
        if (left == GrobType.Float || right == GrobType.Float) return GrobType.Float;
        if (left == GrobType.String) return GrobType.String;
        return left;
    }
}
