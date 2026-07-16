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

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 9 Increment A (D-345, D-348). Emits the receiver then the index
    /// expression, then <see cref="OpCode.GetIndex"/> — the VM handler resolves
    /// array (bounds-checked, <c>E5101</c> via the Sprint-7 handler table) versus
    /// map (nil-on-miss) dynamically at runtime, so one emission shape covers both.
    /// A chained form (<c>matrix[r][c]</c>) needs no special handling: the target
    /// of the outer <see cref="IndexExpr"/> is itself an <see cref="IndexExpr"/>,
    /// so visiting it re-enters this override and emits the inner <c>GetIndex</c>
    /// first.
    /// </remarks>
    public override object? VisitIndex(IndexExpr node) {
        Visit(node.Target);
        Visit(node.Index);
        _chunk.WriteOpCode(OpCode.GetIndex, node.Range.Start.Line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Lambda expression  (Sprint 5 Increment C/D — categories 1–4)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles the lambda into its own <see cref="BytecodeFunction"/> (using the same
    /// sub-compiler pattern as <see cref="VisitFnDecl"/>). A non-capturing lambda emits a
    /// single <see cref="OpCode.Constant"/> in the enclosing chunk so the value is pushed
    /// onto the stack; a lambda that captures an enclosing-function local instead emits an
    /// <see cref="OpCode.Closure"/> with its upvalue descriptors. Either way the lambda is
    /// an opaque callable that the caller stores, passes, or immediately uses as an argument.
    ///
    /// <para><b>Category 1–3 resolution.</b> The sub-compiler inherits the root's
    /// <c>_constValues</c> cache, so top-level <c>const</c> references inside the body
    /// are inlined (category 1). Any other identifier resolves via
    /// <see cref="EmitLoad"/>, which falls through to <see cref="OpCode.GetGlobal"/> for
    /// names not found in the sub-compiler's local scopes — correct for top-level
    /// <c>readonly</c> (category 2) and mutable (category 3). Writes inside block-body
    /// lambdas similarly emit <see cref="OpCode.SetGlobal"/>. Category 4 (enclosing-
    /// function-local capture) emits <see cref="OpCode.GetUpvalue"/>/<see cref="OpCode.SetUpvalue"/>
    /// and wraps the function in a <see cref="OpCode.Closure"/> instruction (Sprint 5 Increment D).</para>
    ///
    /// <para><b>Block-body return semantics (D-276).</b> When the body is a
    /// <see cref="LambdaBlockBody"/>, compilation proceeds statement-by-statement. The
    /// last statement is special: if it is an <see cref="ExpressionStmt"/>, its inner
    /// expression is compiled without the trailing <see cref="OpCode.Pop"/> so the value
    /// stays on the stack as the implicit return value, and <see cref="OpCode.Return"/>
    /// follows immediately. Any other last statement compiles normally and falls through
    /// to the safety-net <see cref="OpCode.Nil"/> + <see cref="OpCode.Return"/>.</para>
    /// </remarks>
    public override object? VisitLambda(LambdaExpr node) {
        int line = node.Range.Start.Line;
        // Pass `this` as the enclosing compiler so ResolveUpvalue can find
        // locals of the current function (category 4, D-296 / D-115).
        var sub = new Compiler(_constValues, enclosing: this);

        // Lambda parameters occupy the first local slots (slot 0, 1, …).
        sub._localScopes.Push([]);
        foreach (Parameter p in node.Parameters) {
            if ((uint)sub._nextSlot > byte.MaxValue)
                throw new GrobInternalException(
                    $"Parameter slot overflow: lambda exceeds the 1-byte slot limit of {byte.MaxValue}.");
            sub._localScopes.Peek().Add(new LocalVar(p.Name, sub._nextSlot++));
        }

        switch (node.Body) {
            case LambdaExpressionBody exprBody:
                if (IsBuiltinVoidCall(exprBody.Expression)) {
                    // print/exit are void — no return value on the stack.  Route via
                    // VisitExpressionStmt (which has the built-in opcode mapping) so the
                    // lambda's chunk gets the correct Print/Exit opcode rather than a
                    // GetGlobal.  The safety-net Nil+Return below supplies nil as the
                    // implicit return value (which callers like 'each' discard).
                    sub.VisitExpressionStmt(
                        new ExpressionStmt(exprBody.Expression.Range, exprBody.Expression));
                    // Fall through to safety-net Nil+Return.
                } else {
                    // Non-void expression body: value stays on stack → Return.
                    sub.Visit(exprBody.Expression);
                    sub._chunk.WriteOpCode(OpCode.Return, line);
                }
                break;

            case LambdaBlockBody blockBody:
                CompileLambdaBlock(sub, blockBody);
                break;
        }

        // Safety-net return: a block body (or empty expression body path) that does not
        // return on every path falls through to here and returns nil.
        sub._chunk.WriteOpCode(OpCode.Nil, line);
        sub._chunk.WriteOpCode(OpCode.Return, line);

        int uvCount = sub._upvalues.Count;
        // Lambda parameter types are inferred (untyped in v1) and the inferred return
        // type lives in the type checker, not reachable here — so the display signature
        // carries Unknown kinds rather than a synthesised placeholder (D-336).
        var fn = new BytecodeFunction(
            string.Empty, node.Parameters.Count, sub._chunk, uvCount,
            parameterTypes: SignatureTypes(node.Parameters),
            returnType: GrobType.Unknown);

        if (uvCount == 0) {
            // No captures — the cheaper non-closure path (categories 1–3).
            EmitConstant(GrobValue.FromFunction(fn), line);
        } else {
            // Category-4 captures present: emit Closure opcode + descriptor bytes.
            // Layout: Closure <poolIdx:1> (<isLocal:1> <index:1>) × uvCount
            int poolIdx = _chunk.AddConstant(GrobValue.FromFunction(fn));
            _chunk.WriteOpCode(OpCode.Closure, line);
            _chunk.WriteByte(ToByteOperand(poolIdx, "closure fn index"), line);
            foreach (UpvalueDescriptor uv in sub._upvalues) {
                _chunk.WriteByte((byte)(uv.IsLocal ? 1 : 0), line);
                _chunk.WriteByte(ToByteOperand(uv.Index, "upvalue index"), line);
            }
        }
        return null;
    }

    /// <summary>
    /// Compiles the statements of a <see cref="LambdaBlockBody"/> into
    /// <paramref name="sub"/>'s chunk with D-276 semantics: the last statement's
    /// expression (if it is an <see cref="ExpressionStmt"/>) is compiled without a
    /// trailing <see cref="OpCode.Pop"/> so it stays on the stack as the implicit
    /// return value, followed by <see cref="OpCode.Return"/>. All other last statements
    /// compile normally and fall through to the caller's safety-net Nil + Return.
    /// </summary>
    private static void CompileLambdaBlock(Compiler sub, LambdaBlockBody blockBody) {
        // Open a scope for block-body locals (mirrors VisitBlock but without a PopN at
        // the end — Return handles frame cleanup in the VM).
        sub._localScopes.Push([]);

        IReadOnlyList<AstNode> stmts = blockBody.Block.Statements;
        if (stmts.Count == 0) {
            sub._localScopes.Pop();
            return; // safety-net Nil+Return is emitted by the caller
        }

        // Compile all statements except the last normally.
        for (int i = 0; i < stmts.Count - 1; i++)
            sub.Visit(stmts[i]);

        // Compile the last statement with D-276 semantics.
        AstNode last = stmts[stmts.Count - 1];
        if (last is ExpressionStmt exprStmt) {
            int stmtLine = exprStmt.Range.Start.Line;
            if (IsBuiltinVoidCall(exprStmt.Expression)) {
                // print/exit are void — emit via VisitExpressionStmt (which carries the
                // built-in opcode mapping) so the chunk gets Print/Exit rather than a
                // GetGlobal.  No explicit Return needed — fall through to safety-net.
                sub.VisitExpressionStmt(exprStmt);
                // Fall through to caller's safety-net Nil+Return.
            } else {
                // Non-void expression: skip VisitExpressionStmt (which would emit Pop)
                // and visit the inner expression directly so the value stays on the stack.
                sub.Visit(exprStmt.Expression);
                sub._chunk.WriteOpCode(OpCode.Return, stmtLine);
            }
        } else {
            sub.Visit(last);
            // Fall through to caller's safety-net.
        }

        // Pop the block-body scope from the compiler's scope stack.  The VM's Return
        // handles the actual runtime stack cleanup; PopN is only needed for fall-through
        // paths inside the block, none of which exist after an explicit Return.
        sub._localScopes.Pop();
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
        // resultType: Float if either operand is float; Unknown operands (lambda
        // parameters) default to Int — the same optimistic convention used in
        // ComparisonCategory. A lambda that passes a float array gets a VM-level
        // type fault; typed parameter inference (Increment D) will fix this.
        // When baseType is Unknown both operands are non-float by construction, so the
        // Unknown fallback can only ever be Int.
        GrobType baseType = (lt == GrobType.Float || rt == GrobType.Float) ? GrobType.Float : lt;
        GrobType resultType = baseType == GrobType.Unknown ? GrobType.Int : baseType;

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
    // Switch expression
    //
    // Jump-based, value-leaving: the subject is evaluated once into a synthetic
    // local; each non-final arm re-loads it, applies the pattern test and
    // JumpIfFalse skips to the next arm; a matched arm evaluates its result and
    // stores it back into the subject slot with SetLocal — leaving exactly one
    // value on the stack — then Jumps to the end. The final arm is the untested
    // fall-through tail (exhaustiveness, proven by the type checker, guarantees it
    // matches). Unlike select, no trailing PopN: the result value is the
    // expression's value and stays on the stack.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitSwitchExpr(SwitchExprNode node) {
        int line = node.Range.Start.Line;
        GrobType subjectType = GetExprType(node.Subject);
        GrobType resultType = GetSwitchExprResultType(node);

        _localScopes.Push([]);
        int scopeBase = _nextSlot;
        Visit(node.Subject);
        int subjectSlot = DeclareLocalSlot("$subject");

        var endJumps = new List<int>();
        int armCount = node.Arms.Count;
        for (int i = 0; i < armCount; i++) {
            SwitchArm arm = node.Arms[i];
            bool isTail = i == armCount - 1;
            // The last arm — and any catch-all — is the untested fall-through.
            bool tested = !isTail && arm.Pattern is not CatchAllPattern;

            int nextArmJump = -1;
            if (tested) {
                EmitGetLocal(subjectSlot, line);
                EmitPatternTest(arm.Pattern, subjectType, line);
                nextArmJump = EmitJump(OpCode.JumpIfFalse, line);
            }

            Visit(arm.Result);
            CoerceArmToFloat(arm.Result, resultType);
            EmitSetLocal(subjectSlot, line); // store result into the subject slot

            if (!isTail) {
                endJumps.Add(EmitJump(OpCode.Jump, line));
            }
            if (tested) {
                PatchJump(nextArmJump);
            }
        }

        foreach (int site in endJumps) PatchJump(site);

        _localScopes.Pop();
        _nextSlot = scopeBase; // the result is an unnamed operand-stack temporary at scopeBase
        return null;
    }

    // Emits a boolean pattern test with the subject already loaded as the left operand.
    private void EmitPatternTest(SwitchPattern pattern, GrobType subjectType, int line) {
        if (pattern is ValuePattern vp) {
            Visit(vp.Value);
            _chunk.WriteOpCode(OpCode.Equal, line); // type-agnostic, mirrors select
        } else if (pattern is RelationalPattern rp) {
            EmitRelationalPatternTest(rp, subjectType, line);
        }
        // CatchAllPattern is never tested — the caller treats it as an untested arm.
    }

    // Emits a relational pattern test: subject (left, already loaded) compared with the
    // operand (right). Mirrors EmitComparison — int↔float coercion and the string
    // '<='/'>=' lowering to strict-plus-Not (the closed enum has no string opcode for them).
    private void EmitRelationalPatternTest(RelationalPattern rp, GrobType subjectType, int line) {
        GrobType operandType = GetExprType(rp.Operand);
        if (subjectType == GrobType.Int && operandType == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);
        Visit(rp.Operand);
        if (operandType == GrobType.Int && subjectType == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        if (subjectType == GrobType.String &&
            (rp.Op == BinaryOperator.LessEqual || rp.Op == BinaryOperator.GreaterEqual)) {
            OpCode strict = rp.Op == BinaryOperator.LessEqual ? OpCode.GreaterString : OpCode.LessString;
            _chunk.WriteOpCode(strict, line);
            _chunk.WriteOpCode(OpCode.Not, line);
            return;
        }

        _chunk.WriteOpCode(GetComparisonOpCode(rp.Op, subjectType, operandType), line);
    }

    /// <summary>
    /// The result type of a switch expression is all its arm results unified, mirroring
    /// the type checker's <see cref="TypeChecker.UnifyTernaryArms"/> widening so parent
    /// opcode selection sees the unified type. The program has type-checked clean by
    /// emission time, so the arms are known to unify.
    /// </summary>
    private static GrobType GetSwitchExprResultType(SwitchExprNode node) {
        GrobType result = GrobType.Error;
        bool have = false;
        foreach (GrobType armType in node.Arms.Select(arm => GetExprType(arm.Result))) {
            result = have ? WidenArmTypes(result, armType) : armType;
            have = true;
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Call (Sprint 5 Increment A — positional)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Emits the callee, then the arguments in source order, then
    /// <see cref="OpCode.Call"/> with the argument count as its 1-byte operand. The
    /// pushed arguments become the callee's first locals over the new frame base.
    /// Built-in <c>print</c>/<c>exit</c> calls in statement position are intercepted
    /// by <see cref="VisitExpressionStmt"/> and never reach here.
    /// </remarks>
    public override object? VisitCall(CallExpr node) {
        int line = node.Range.Start.Line;

        // Sprint 8 Increment E: a formatAs.table/list/csv call — function form
        // (formatAs.table(items)) or chained form (items.formatAs.table()), both
        // resolved by the checker's ResolveFormatAsCall (TypeChecker.Expressions.cs),
        // which leaves the derived column list on the node. Bypasses the generic
        // Visit(node.Callee)-then-argument-loop shape below entirely: the chained form's
        // receiver is never a literal argument the loop could visit, and the compiler
        // needs to inject a synthesised second argument (the columns array) neither
        // form's Arguments carries.
        if (node.ResolvedFormatAsColumns is IReadOnlyList<string> formatAsColumns) {
            EmitFormatAsCall(node, formatAsColumns, line);
            return null;
        }

        Visit(node.Callee);

        // Sprint 8 Increment C: input() is the one no-namespace native needing a
        // default-argument fill (D-342) — the runtime native's own arity is always 1
        // (Grob.Stdlib.IoPlugin), so a 0-argument script-level call has its missing
        // prompt filled with the constant "" here, at the call site, before the ordinary
        // GetGlobal-then-Call shape below. A 1-argument call needs no special handling —
        // it already takes the plain positional path unchanged. Deliberately a one-off
        // arm, not a general defaulted-native mechanism: input() is the only case that
        // needs it in v1.
        if (node.Callee is IdentifierExpr { Name: "input", Declaration: BuiltinDecl } &&
                node.Arguments.Count == 0) {
            EmitConstant(GrobValue.FromString(string.Empty), line);
            _chunk.WriteOpCode(OpCode.Call, line);
            _chunk.WriteByte(1, line);
            return null;
        }

        // Named arguments, or a positional call that omits a defaulted parameter,
        // need reorder-and-fill so the callee receives a fully-bound positional list
        // in parameter declaration order. A pure positional call that supplies every
        // parameter takes the fast path, emitting arguments in source order.
        if (node.Callee is IdentifierExpr { Declaration: FnDecl fn } &&
            (node.Arguments.Any(a => a.Name is not null) || node.Arguments.Count != fn.Parameters.Count)) {
            EmitReorderedArguments(node, fn);
            _chunk.WriteOpCode(OpCode.Call, line);
            _chunk.WriteByte(ToByteOperand(fn.Parameters.Count, "call argument count"), line);
            return null;
        }

        foreach (CallArgument arg in node.Arguments) Visit(arg.Value);
        _chunk.WriteOpCode(OpCode.Call, line);
        _chunk.WriteByte(ToByteOperand(node.Arguments.Count, "call argument count"), line);
        return null;
    }

    /// <summary>
    /// Emits a validated <c>formatAs.table</c>/<c>list</c>/<c>csv</c> call (Sprint 8
    /// Increment E): <c>GetGlobal "formatAs.&lt;method&gt;"</c> (the callee, pushed first —
    /// the same shape every namespace-qualified native call already uses), the receiver
    /// (the chain's inner receiver, or the function form's own first argument), the
    /// synthesised columns array (one <c>Constant</c> per name plus <see
    /// cref="OpCode.NewArray"/> — no reflection over the value at runtime), then <see
    /// cref="OpCode.Call"/> with a fixed operand of 2. The runtime native's arity is
    /// therefore always 2 regardless of which source form or overload the user wrote.
    /// </summary>
    private void EmitFormatAsCall(CallExpr node, IReadOnlyList<string> columns, int line) {
        var callee = (MemberAccessExpr)node.Callee;
        Expression receiverExpr = TryDetectFormatAsChainReceiver(callee, out Expression chainReceiver)
            ? chainReceiver
            : node.Arguments[0].Value;

        int qualifiedIdx = _chunk.AddConstant(GrobValue.FromString($"formatAs.{callee.Member}"));
        _chunk.WriteOpCode(OpCode.GetGlobal, line);
        _chunk.WriteByte(ToByteOperand(qualifiedIdx, "namespace member name"), line);

        Visit(receiverExpr);
        foreach (string column in columns) EmitConstant(GrobValue.FromString(column), line);
        _chunk.WriteOpCode(OpCode.NewArray, line);
        _chunk.WriteByte(ToByteOperand(columns.Count, "formatAs columns length"), line);

        _chunk.WriteOpCode(OpCode.Call, line);
        _chunk.WriteByte(2, line);
    }

    /// <summary>
    /// Detects the chained form's inner receiver — mirrors the type checker's own
    /// <c>TryDetectFormatAsChainReceiver</c> (TypeChecker.Expressions.cs), duplicated here
    /// rather than shared because <see cref="Compiler"/> and <c>TypeChecker</c> are
    /// separate passes with no shared instance state, the same reason the namespace-receiver
    /// check a few members up is independently re-derived rather than threaded across.
    /// </summary>
    private static bool TryDetectFormatAsChainReceiver(MemberAccessExpr callee, out Expression receiverExpr) {
        if (callee.Target is MemberAccessExpr { Member: "formatAs" } inner) {
            receiverExpr = inner.Target;
            return true;
        }
        receiverExpr = null!;
        return false;
    }

    /// <summary>
    /// Emits the arguments of <paramref name="node"/> in parameter declaration order:
    /// positionals into the leading slots, named arguments into their parameters, and
    /// omitted defaults materialised into the remaining slots. The default expression
    /// compiles at the call site. The type checker has already validated the binding,
    /// so every slot resolves to a positional or a default here.
    /// </summary>
    private void EmitReorderedArguments(CallExpr node, FnDecl fn) {
        var boundExprs = new Expression?[fn.Parameters.Count];

        int positional = 0;
        foreach (CallArgument arg in node.Arguments) {
            if (arg.Name is null) {
                if (positional < boundExprs.Length) boundExprs[positional] = arg.Value;
                positional++;
            } else {
                int p = ParameterIndex(fn, arg.Name);
                if (p >= 0) boundExprs[p] = arg.Value;
            }
        }

        for (int i = 0; i < boundExprs.Length; i++) {
            Expression value = boundExprs[i] ?? fn.Parameters[i].DefaultValue!;
            Visit(value);
        }
    }

    /// <summary>
    /// Returns the index of the parameter named <paramref name="name"/> in
    /// <paramref name="fn"/>, or <c>-1</c> when no parameter has that name.
    /// </summary>
    private static int ParameterIndex(FnDecl fn, string name) {
        for (int i = 0; i < fn.Parameters.Count; i++) {
            if (fn.Parameters[i].Name == name) return i;
        }
        return -1;
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

        // Namespace-qualified access (D-342): a namespace constant (math.pi) or a
        // namespace-qualified native's callee (math.sqrt, reached here because
        // VisitCall's generic Visit(node.Callee) routes a MemberAccessExpr callee
        // through this method) — no runtime module value, no GetProperty, no new
        // opcode. Compiles to a bare GetGlobal against the qualified name; the
        // corresponding NativeFunction/constant was registered into VM globals by
        // the stdlib plugin's Register(vm) call before any script bytecode runs
        // (the same _globals[name] write path RegisterNative already uses for every
        // other native — no DefineGlobal ordering hazard). Checked BEFORE
        // Visit(node.Target) — visiting the bare namespace identifier generically
        // would emit a meaningless GetGlobal against the namespace's own name,
        // which nothing registers a value under.
        //
        // Reads the type checker's own resolution (node.Target.Declaration is
        // NamespaceDecl) rather than re-deriving "is this a namespace" from the bare
        // identifier name via NamespaceRegistry.IsNamespace — the checker's
        // TryAnnotateNamespaceReceiver already resolves the receiver through
        // LookupSymbol, so a local variable or parameter that shadows a namespace
        // name (e.g. a Config-typed parameter called 'math') carries its own real
        // Declaration here, not NamespaceDecl, and correctly falls through to the
        // ordinary GetProperty path below (PR #127 review — the previous name-only
        // check emitted GetGlobal for a shadowed local too).
        if (node.Target is IdentifierExpr { Declaration: NamespaceDecl } id) {
            string namespaceName = id.Name;
            string qualifiedName = $"{namespaceName}.{node.Member}";
            int qualifiedIdx = _chunk.AddConstant(GrobValue.FromString(qualifiedName));
            _chunk.WriteOpCode(OpCode.GetGlobal, line);
            _chunk.WriteByte(ToByteOperand(qualifiedIdx, "namespace member name"), line);
            return null;
        }

        // Sprint 8 Increment D: the two-level namespace chain guid.namespaces.dns.
        // Mirrors the checker's TryFlattenNestedNamespaceMember (TypeChecker.Expressions.cs)
        // — node.Target is itself a namespace-rooted MemberAccessExpr (guid.namespaces),
        // so the whole three-segment chain compiles to one GetGlobal against the fully
        // qualified name, exactly as GuidPlugin registers it ("guid.namespaces.dns"). The
        // registered-key gate is what distinguishes this from an ordinary instance-member
        // access on a namespace constant's value (guid.empty.isEmpty — "empty.isEmpty" is
        // not a registered key, so it correctly falls through to the ordinary
        // Visit(node.Target)+GetProperty path below instead of a meaningless GetGlobal).
        if (node.Target is MemberAccessExpr { Target: IdentifierExpr { Declaration: NamespaceDecl } innerId } inner &&
                NamespaceRegistry.TryGetMember(innerId.Name, $"{inner.Member}.{node.Member}") is not null) {
            string qualifiedName = $"{innerId.Name}.{inner.Member}.{node.Member}";
            int qualifiedIdx = _chunk.AddConstant(GrobValue.FromString(qualifiedName));
            _chunk.WriteOpCode(OpCode.GetGlobal, line);
            _chunk.WriteByte(ToByteOperand(qualifiedIdx, "namespace member name"), line);
            return null;
        }

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
    // Named struct construction (§10, Sprint 6B)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Emits field values in declaration order (supplied values or default expressions),
    /// then <see cref="OpCode.NewStruct"/> with the type-table index. Mirrors the
    /// Sprint 5B call-site default pattern (<see cref="EmitReorderedArguments"/>).
    /// </remarks>
    public override object? VisitStructConstruction(StructConstructionExpr node) {
        int line = node.Range.Start.Line;
        TypeDecl typeDecl = node.ResolvedTypeDecl!;

        Dictionary<string, Expression> suppliedByName = node.Fields
            .ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);

        List<string> fieldNames = new(typeDecl.Fields.Count);
        foreach (TypeField field in typeDecl.Fields) {
            fieldNames.Add(field.Name);
            Expression value = suppliedByName.TryGetValue(field.Name, out Expression? supplied)
                ? supplied
                : field.DefaultValue!;
            Visit(value);
        }

        // Cache per-chunk to avoid repeated registration of the same type (256-slot limit).
        if (!_structTypeIndices.TryGetValue(typeDecl, out byte typeIndex)) {
            typeIndex = _chunk.AddStructType(new StructTypeDescriptor(typeDecl.Name, fieldNames));
            _structTypeIndices[typeDecl] = typeIndex;
        }
        _chunk.WriteOpCode(OpCode.NewStruct, line);
        _chunk.WriteByte(typeIndex, line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Anonymous struct construction (§10, Sprint 6D).
    //
    // For each field in source order: emit the field-name string constant then
    // the field-value expression. Finish with NewAnonStruct(field-count).
    // The VM pops field-count name/value pairs from the stack in LIFO order.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitAnonStruct(AnonStructExpr node) {
        int line = node.Range.Start.Line;
        foreach (FieldInit fi in node.Fields) {
            int nameIdx = _chunk.AddConstant(GrobValue.FromString(fi.Name));
            _chunk.WriteOpCode(OpCode.Constant, line);
            _chunk.WriteByte((byte)nameIdx, line);
            Visit(fi.Value);
        }
        _chunk.WriteOpCode(OpCode.NewAnonStruct, line);
        _chunk.WriteByte((byte)node.Fields.Count, line);
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
        SwitchExprNode s => GetSwitchExprResultType(s),
        // A call to a user function resolves to that function's declared return type,
        // so a surrounding operator selects the right typed opcode. Built-in and
        // unresolved callees stay Unknown.
        CallExpr { Callee: IdentifierExpr { Declaration: FnDecl fn } }
            => TypeChecker.ResolveTypeRef(fn.ReturnType),
        MemberAccessExpr ma => ma.ResolvedFieldType,
        _ => GrobType.Unknown
    };

    /// <summary>
    /// The result type of a ternary is its two arms unified — not the then-arm alone.
    /// Mirrors the type checker's <c>UnifyTernaryArms</c> widening (int/float → float,
    /// T/T? → T?) so parent opcode selection sees the wider type. The program has
    /// already type-checked clean by emission time, so the arms are known to unify;
    /// the then-arm type is the conservative fallback.
    /// </summary>
    private static GrobType GetTernaryResultType(TernaryExpr node) =>
        WidenArmTypes(GetExprType(node.Then), GetExprType(node.Else));

    /// <summary>
    /// Widens two arm types to their unified type — the int/float and <c>T</c>/<c>T?</c>
    /// promotions the type checker's <see cref="TypeChecker.UnifyTernaryArms"/> applies.
    /// Used by both the ternary and the switch expression at emission time, where the
    /// arms are already known to unify, so the first arm is the conservative fallback.
    /// </summary>
    private static GrobType WidenArmTypes(GrobType first, GrobType second) {
        // Match UnifyTernaryArms' cascade handling so emission-time widening agrees with
        // the checker when an arm is Error (a prior failure) or Unknown (e.g. a call).
        if (first == GrobType.Error || second == GrobType.Error) return GrobType.Error;
        if (first == GrobType.Unknown || second == GrobType.Unknown) return GrobType.Unknown;
        if (first == second) return first;
        if ((first == GrobType.Int && second == GrobType.Float) ||
            (first == GrobType.Float && second == GrobType.Int)) return GrobType.Float;
        if (GrobTypeHelpers.ToNullable(first) == second) return second;
        if (GrobTypeHelpers.ToNullable(second) == first) return first;
        return first;
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

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="expr"/> is a call to a
    /// built-in void function (<c>print</c> or <c>exit</c>).
    ///
    /// <para>These built-ins are handled by <see cref="VisitExpressionStmt"/> and
    /// emit dedicated VM opcodes (<see cref="OpCode.Print"/> / <see cref="OpCode.Exit"/>)
    /// rather than a <see cref="OpCode.GetGlobal"/> + <see cref="OpCode.Call"/>. In
    /// expression context (e.g. a lambda expression body) the caller must route through
    /// <see cref="VisitExpressionStmt"/> so the correct opcode is emitted; the safety-net
    /// <see cref="OpCode.Nil"/> + <see cref="OpCode.Return"/> in <see cref="VisitLambda"/>
    /// supplies the implicit nil return value (which <c>each</c>-style callers discard).</para>
    /// </summary>
    private static bool IsBuiltinVoidCall(Expression expr) =>
        expr is CallExpr { Callee: IdentifierExpr { Name: "print" or "exit" } };
}
