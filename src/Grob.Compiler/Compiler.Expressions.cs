using System.Diagnostics.CodeAnalysis;
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
    /// <remarks>
    /// The parser never produces <see cref="StringLiteralExpr"/> nodes — all double-quoted
    /// strings are emitted as <see cref="InterpolatedStringExpr"/>. This visitor is kept
    /// for forward-compatibility but is currently unreachable.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "Parser never produces StringLiteralExpr; all double-quoted strings become InterpolatedStringExpr.")]
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
    /// Sprint 3E: String interpolation.
    /// <para>
    /// No-slot optimisation: when the string contains only literal text parts
    /// (no <c>${}</c> expression slots), all parts are decoded and concatenated
    /// into a single constant — no <see cref="OpCode.BuildString"/> is emitted.
    /// </para>
    /// <para>
    /// General case: each part is emitted in source order — text parts as string
    /// constants, expression slots as their compiled values — then a single
    /// <see cref="OpCode.BuildString"/> with the fragment count (1-byte operand)
    /// concatenates them at runtime. The VM calls <c>ToString()</c> on each
    /// fragment so non-string values are automatically converted. (D-279)
    /// </para>
    /// <para>
    /// Escape sequences in text parts are decoded here:
    /// <c>\n \r \t \\ \" \$</c>. The lexer preserves the raw escape sequences
    /// in the lexeme; the compiler resolves them to their character values when
    /// creating string constants.
    /// </para>
    /// </remarks>
    public override object? VisitInterpolatedString(InterpolatedStringExpr node) {
        bool hasSlots = node.Parts.Any(p => p is StringExpressionPart);

        if (!hasSlots) {
            // Optimisation: all parts are literal text — decode and concatenate into
            // a single constant. The type checker already confirmed there are no slots.
            var sb = new System.Text.StringBuilder();
            foreach (StringTextPart text in node.Parts.OfType<StringTextPart>()) {
                sb.Append(DecodeStringEscapes(text.Text));
            }
            EmitConstant(GrobValue.FromString(sb.ToString()), node.Range.Start.Line);
            return null;
        }

        // General case: emit each fragment in source order, then BuildString N.
        int fragmentCount = 0;
        int line = node.Range.Start.Line;
        foreach (StringInterpolationPart part in node.Parts) {
            switch (part) {
                case StringTextPart text:
                    EmitConstant(GrobValue.FromString(DecodeStringEscapes(text.Text)), line);
                    fragmentCount++;
                    break;
                case StringExpressionPart exprPart:
                    Visit(exprPart.Expression);
                    fragmentCount++;
                    break;
            }
        }

        _chunk.WriteOpCode(OpCode.BuildString, line);
        _chunk.WriteByte(ToByteOperand(fragmentCount, "BuildString fragment count"), line);
        return null;
    }

    /// <summary>
    /// Decodes the Grob string escape sequences stored raw in a <see cref="StringTextPart"/> lexeme.
    /// Recognised sequences: <c>\n \r \t \\ \" \$</c>.
    /// Any other <c>\x</c> is passed through unchanged (the lexer already emitted E2006 for those).
    /// </summary>
    private static string DecodeStringEscapes(string raw) {
        if (!raw.Contains('\\')) return raw;

        var sb = new System.Text.StringBuilder(raw.Length);
        int i = 0;
        while (i < raw.Length) {
            if (raw[i] == '\\' && i + 1 < raw.Length) {
                char next = raw[i + 1];
                switch (next) {
                    case 'n': sb.Append('\n'); i += 2; break;
                    case 'r': sb.Append('\r'); i += 2; break;
                    case 't': sb.Append('\t'); i += 2; break;
                    case '\\': sb.Append('\\'); i += 2; break;
                    case '"': sb.Append('"'); i += 2; break;
                    case '$': sb.Append('$'); i += 2; break;
                    default:
                        // Unknown escape: pass both chars through; lexer already
                        // emitted a diagnostic, so this path is error-recovery only.
                        sb.Append(raw[i]);
                        i++;
                        break;
                }
            } else {
                sb.Append(raw[i]);
                i++;
            }
        }
        return sb.ToString();
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
            EmitConstant(FetchCachedConst(cd, node.Name), node.Range.Start.Line);
            return null;
        }
        EmitLoad(node.Name, node.Range.Start.Line);
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Emits each element in source order, then a single <see cref="OpCode.NewArray"/>
    /// (1-byte element count) which pops the elements and pushes the new array.
    /// </remarks>
    public override object? VisitArrayLiteral(ArrayLiteralExpr node) {
        foreach (Expression element in node.Elements) Visit(element);
        _chunk.WriteOpCode(OpCode.NewArray, node.Range.Start.Line);
        _chunk.WriteByte(ToByteOperand(node.Elements.Count, "array literal length"), node.Range.Start.Line);
        return null;
    }

    /// <summary>
    /// Returns the cached <see cref="GrobValue"/> for a <see cref="ConstDecl"/>.
    /// <see cref="VisitConstDecl"/> always caches the value before any reference site is
    /// emitted, so a cache miss indicates a compiler invariant violation.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Cache miss is unreachable: VisitConstDecl always runs before any reference to the same const.")]
    private GrobValue FetchCachedConst(ConstDecl decl, string name) =>
        _constValues.TryGetValue(decl, out GrobValue cv)
            ? cv
            : throw new GrobInternalException(
                $"Const '{name}' was not cached before emission.");

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
            case UnaryOperator.Not:
                _chunk.WriteOpCode(OpCode.Not, line);
                break;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Binary
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Dispatches each binary operator to a dedicated emission helper. Nil-coalesce,
    /// logical <c>&amp;&amp;</c>/<c>||</c> and comparison each have their own jump or
    /// opcode shape; the remaining operators are string concatenation or arithmetic.
    /// </remarks>
    public override object? VisitBinary(BinaryExpr node) {
        int line = node.Range.Start.Line;
        GrobType lt = GetExprType(node.Left);
        GrobType rt = GetExprType(node.Right);

        switch (node.Operator) {
            case BinaryOperator.NilCoalesce:
                EmitNilCoalesce(node, line);
                return null;
            case BinaryOperator.And:
                EmitLogicalAnd(node, line);
                return null;
            case BinaryOperator.Or:
                EmitLogicalOr(node, line);
                return null;
        }

        if (IsComparisonOperator(node.Operator)) {
            EmitComparison(node, lt, rt, line);
            return null;
        }

        if (node.Operator == BinaryOperator.Add && lt == GrobType.String && rt == GrobType.String) {
            Visit(node.Left);
            Visit(node.Right);
            _chunk.WriteOpCode(OpCode.Concat, line);
            return null;
        }

        EmitArithmetic(node, lt, rt, line);
        return null;
    }

    // Nil coalescing — eager: compile both operands then NilCoalesce opcode (D-271).
    // No short-circuit jumps — the '??' operator always evaluates both sides.
    private void EmitNilCoalesce(BinaryExpr node, int line) {
        Visit(node.Left);
        Visit(node.Right);
        _chunk.WriteOpCode(OpCode.NilCoalesce, line);
    }

    // Logical AND — short-circuit via JumpIfFalse (no dedicated And opcode).
    // Stack discipline: JumpIfFalse pops the condition.
    //
    //   evaluate left
    //   JumpIfFalse → false_label      // pops left; skip right when false
    //   evaluate right                 // left was true; right is the result
    //   Jump → end
    //   false_label: False             // synthesised false for the short-circuit path
    //   end:
    private void EmitLogicalAnd(BinaryExpr node, int line) {
        Visit(node.Left);
        int falseJump = EmitJump(OpCode.JumpIfFalse, line);
        Visit(node.Right);
        int endJump = EmitJump(OpCode.Jump, line);
        PatchJump(falseJump);
        _chunk.WriteOpCode(OpCode.False, line);  // false_label
        PatchJump(endJump);
    }

    // Logical OR — short-circuit via JumpIfTrue (no dedicated Or opcode).
    // Stack discipline: JumpIfTrue peeks (leaves the value on the stack).
    //
    //   evaluate left
    //   JumpIfTrue → end               // peeks left; left stays on stack as result if true
    //   Pop                            // discard the peeked false value
    //   evaluate right                 // right is the result
    //   end:
    private void EmitLogicalOr(BinaryExpr node, int line) {
        Visit(node.Left);
        int endJump = EmitJump(OpCode.JumpIfTrue, line);
        _chunk.WriteOpCode(OpCode.Pop, line);
        Visit(node.Right);
        PatchJump(endJump);
    }

    // Comparison — emit both operands (coercing int → float for mixed numeric
    // operands) then the typed comparison opcode.
    private void EmitComparison(BinaryExpr node, GrobType lt, GrobType rt, int line) {
        Visit(node.Left);
        if (lt == GrobType.Int && rt == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, node.Left.Range.Start.Line);
        Visit(node.Right);
        if (rt == GrobType.Int && lt == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, node.Right.Range.Start.Line);

        // String '<=' and '>=' have no dedicated opcodes — the closed enum provides
        // only LessString/GreaterString (the strict forms). Lower them to the strict
        // comparison followed by Not:  a <= b  ≡  !(a > b);  a >= b  ≡  !(a < b).
        if (lt == GrobType.String &&
            (node.Operator == BinaryOperator.LessEqual || node.Operator == BinaryOperator.GreaterEqual)) {
            OpCode strict = node.Operator == BinaryOperator.LessEqual ? OpCode.GreaterString : OpCode.LessString;
            _chunk.WriteOpCode(strict, line);
            _chunk.WriteOpCode(OpCode.Not, line);
            return;
        }

        _chunk.WriteOpCode(GetComparisonOpCode(node.Operator, lt, rt), line);
    }

    // Arithmetic — coerce either operand from int → float as needed, then the typed op.
    private void EmitArithmetic(BinaryExpr node, GrobType lt, GrobType rt, int line) {
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

        _chunk.WriteOpCode(GetArithmeticOpCode(node.Operator, resultType), line);
    }

    // -----------------------------------------------------------------------
    // Ternary expression
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Emits the ternary <c>cond ? then : else</c> using forward-jump backpatching:
    /// condition → <see cref="OpCode.JumpIfFalse"/> to else-arm → then-arm →
    /// <see cref="OpCode.Jump"/> past else-arm → else-arm.
    /// Exactly one arm is evaluated per condition value.  When the arms unify to a
    /// wider type (<c>int</c>/<c>float</c> → <c>float</c>) the narrower arm is coerced
    /// with <see cref="OpCode.IntToFloat"/> on its own branch, so whichever arm runs
    /// leaves a value of the unified type on the stack.
    /// </remarks>
    public override object? VisitTernary(TernaryExpr node) {
        int line = node.Range.Start.Line;
        GrobType resultType = GetTernaryResultType(node);

        // Condition; JumpIfFalse pops and jumps to the else-arm when false.
        Visit(node.Condition);
        int falseJump = EmitJump(OpCode.JumpIfFalse, line);

        // Then-arm, coerced to the unified type if it is the narrower numeric arm.
        Visit(node.Then);
        CoerceArmToFloat(node.Then, resultType);
        int endJump = EmitJump(OpCode.Jump, line);

        // Else-arm, coerced likewise.
        PatchJump(falseJump);
        Visit(node.Else);
        CoerceArmToFloat(node.Else, resultType);

        // Both arms converge here.
        PatchJump(endJump);
        return null;
    }

    // Emits IntToFloat when the unified ternary type is float but this arm is int, so
    // the runtime value matches the static type the parent expression compiled against.
    private void CoerceArmToFloat(Expression arm, GrobType resultType) {
        if (resultType == GrobType.Float && GetExprType(arm) == GrobType.Int) {
            _chunk.WriteOpCode(OpCode.IntToFloat, arm.Range.Start.Line);
        }
    }

    // -----------------------------------------------------------------------
    // Member access (optional chaining)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// For <c>?.</c> access: compiles the receiver, emits <see cref="OpCode.IsNil"/>,
    /// then a forward <see cref="OpCode.JumpIfTrue"/> (short-circuit path — leaves
    /// the nil receiver on the stack and skips the field-access body). When the receiver
    /// is not nil, execution falls through to a <see cref="OpCode.GetProperty"/>
    /// which resolves the named member at runtime.
    /// <para>
    /// <see cref="OpCode.IsNil"/> peeks the top of stack (does not pop), so the
    /// resulting bool sits above the receiver. <see cref="OpCode.JumpIfTrue"/> also
    /// peeks. Two <see cref="OpCode.Pop"/>s are emitted (one on each path) to
    /// discard the bool before the paths converge.
    /// </para>
    /// <para>For plain <c>.</c> access the same <see cref="OpCode.GetProperty"/> is
    /// emitted directly; the type checker has already verified the receiver is
    /// non-nullable.</para>
    /// <para>Struct member types are deferred to Sprint 5; for now
    /// <see cref="OpCode.GetProperty"/> throws at runtime on non-struct receivers.</para>
    /// </remarks>
    public override object? VisitMemberAccess(MemberAccessExpr node) {
        int line = node.Range.Start.Line;
        Visit(node.Target);

        int nameIdx = _chunk.AddConstant(GrobValue.FromString(node.Member));

        if (node.IsOptional) {
            // Stack after Visit(Target):       [... receiver]
            // IsNil peeks — pushes bool:       [... receiver, isNil]
            // JumpIfTrue peeks: if nil, jump to nil_label (bool and receiver remain).
            _chunk.WriteOpCode(OpCode.IsNil, line);
            int nilSite = EmitJump(OpCode.JumpIfTrue, line);

            // Non-nil path: pop the false bool, then access the property.
            _chunk.WriteOpCode(OpCode.Pop, line);           // pop false
            _chunk.WriteOpCode(OpCode.GetProperty, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, "property name"), line);
            int skipSite = EmitJump(OpCode.Jump, line);     // skip nil cleanup

            // nil_label: pop the true bool; receiver (nil) stays as result.
            PatchJump(nilSite);
            _chunk.WriteOpCode(OpCode.Pop, line);           // pop true

            // end: both paths converge here.
            PatchJump(skipSite);
        } else {
            _chunk.WriteOpCode(OpCode.GetProperty, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, "property name"), line);
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Type helpers
    // -----------------------------------------------------------------------

    private static bool IsComparisonOperator(BinaryOperator op) => op switch {
        BinaryOperator.Equal or BinaryOperator.NotEqual or
        BinaryOperator.Less or BinaryOperator.LessEqual or
        BinaryOperator.Greater or BinaryOperator.GreaterEqual => true,
        _ => false,
    };

    /// <summary>
    /// Selects the typed comparison opcode for <paramref name="op"/> based on the
    /// operand category (int, float or string). For mixed int/float operands the
    /// left is already coerced to float by the caller before emitting this opcode.
    /// String <c>&lt;=</c>/<c>&gt;=</c> are lowered to strict-plus-<c>Not</c> by the
    /// caller and never reach this switch — the closed enum has no string opcode for them.
    /// </summary>
    private static OpCode GetComparisonOpCode(BinaryOperator op, GrobType lt, GrobType rt) =>
        (op, ComparisonCategory(lt, rt)) switch {
            (BinaryOperator.Equal, _) => OpCode.Equal,
            (BinaryOperator.NotEqual, _) => OpCode.NotEqual,
            (BinaryOperator.Less, GrobType.Float) => OpCode.LessFloat,
            (BinaryOperator.Less, GrobType.String) => OpCode.LessString,
            (BinaryOperator.Less, _) => OpCode.LessInt,
            (BinaryOperator.LessEqual, GrobType.Float) => OpCode.LessEqualFloat,
            (BinaryOperator.LessEqual, _) => OpCode.LessEqualInt,
            (BinaryOperator.Greater, GrobType.Float) => OpCode.GreaterFloat,
            (BinaryOperator.Greater, GrobType.String) => OpCode.GreaterString,
            (BinaryOperator.Greater, _) => OpCode.GreaterInt,
            (BinaryOperator.GreaterEqual, GrobType.Float) => OpCode.GreaterEqualFloat,
            (BinaryOperator.GreaterEqual, _) => OpCode.GreaterEqualInt,
            _ => ThrowUnsupportedBinaryOp(op, GrobType.Bool)
        };

    /// <summary>
    /// Reduces a pair of comparison operand types to a single category —
    /// <see cref="GrobType.Float"/> (any float operand, mixed numerics included),
    /// <see cref="GrobType.String"/> (string operands) or <see cref="GrobType.Int"/>.
    /// </summary>
    private static GrobType ComparisonCategory(GrobType lt, GrobType rt) {
        if (lt == GrobType.Float || rt == GrobType.Float) return GrobType.Float;
        if (lt == GrobType.String) return GrobType.String;
        return GrobType.Int;
    }

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
        _ => ThrowUnsupportedBinaryOp(op, type)
    };

    /// <summary>
    /// Throws for operator/type combinations that the type checker prevents in valid
    /// programs. Reached only if a future sprint adds operators before updating this switch.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "The type checker rejects unsupported operator/type combos before emission.")]
    private static OpCode ThrowUnsupportedBinaryOp(BinaryOperator op, GrobType type) =>
        throw new InvalidOperationException(
            $"Operator {op} with result type {type} is not supported in Sprint 2.");

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
        ArrayLiteralExpr => GrobType.Array,
        GroupingExpr g => GetExprType(g.Inner),
        UnaryExpr u => GetUnaryResultType(u),
        BinaryExpr b => GetBinaryResultType(b),
        TernaryExpr t => GetTernaryResultType(t),
        _ => GrobType.Unknown
    };

    /// <summary>
    /// The result type of a ternary is its two arms unified — not the then-arm alone.
    /// Mirrors the type checker's <c>UnifyTernaryArms</c> widening (int/float → float,
    /// T/T? → T?) so parent opcode selection sees the wider type. The program has
    /// already type-checked clean by emission time, so the arms are known to unify;
    /// the then-arm type is the conservative fallback.
    /// </summary>
    private static GrobType GetTernaryResultType(TernaryExpr node) {
        GrobType thenType = GetExprType(node.Then);
        GrobType elseType = GetExprType(node.Else);
        if (thenType == elseType) return thenType;
        if ((thenType == GrobType.Int && elseType == GrobType.Float) ||
            (thenType == GrobType.Float && elseType == GrobType.Int)) return GrobType.Float;
        if (GrobTypeHelpers.ToNullable(thenType) == elseType) return elseType;
        if (GrobTypeHelpers.ToNullable(elseType) == thenType) return thenType;
        return thenType;
    }

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
