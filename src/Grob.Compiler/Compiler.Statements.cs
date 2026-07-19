using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class Compiler {
    // Label used as the operand-name argument to ToByteOperand for global-name indices.
    private const string GlobalNameLabel = "global name";

    // -----------------------------------------------------------------------
    // Block — open scope, emit body, emit PopN on exit.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitBlock(BlockStmt node) {
        _localScopes.Push([]);

        foreach (Statement stmt in node.Statements) {
            Visit(stmt);
        }

        List<LocalVar> scope = _localScopes.Pop();
        if (scope.Count > 0) {
            EmitScopeCleanup(scope, node.Range.End.Line);
            _nextSlot -= scope.Count;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Variable declarations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitVarDecl(VarDeclStmt node) {
        int line = node.Range.Start.Line;
        Visit(node.Initializer);   // value is now on top of stack

        if (IsGlobalScope) {
            int nameIdx = GetOrCreateGlobalNameIndex(node.Name);
            _chunk.WriteOpCode(OpCode.DefineGlobal, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, GlobalNameLabel), line);
        } else {
            // Local: the value on the stack IS the local — record the slot.
            if ((uint)_nextSlot > byte.MaxValue)
                throw new GrobInternalException(
                    $"Local variable count overflow: slot {_nextSlot} for '{node.Name}' " +
                    $"exceeds the 1-byte limit of {byte.MaxValue}.");
            _localScopes.Peek().Add(new LocalVar(node.Name, _nextSlot++));
        }
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitReadonlyDecl(ReadonlyDecl node) {
        // Immutability is enforced at compile time by the type checker (E0202).
        // The runtime path is identical to a mutable := global binding.
        // Block-level readonly is deferred: Declaration does not extend Statement,
        // so ReadonlyDecl cannot appear inside a BlockStmt in the current design.
        int line = node.Range.Start.Line;
        Visit(node.Value);
        int nameIdx = GetOrCreateGlobalNameIndex(node.Name);
        _chunk.WriteOpCode(OpCode.DefineGlobal, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, GlobalNameLabel), line);
        return null;
    }

    /// <inheritdoc/>
    public override object? VisitConstDecl(ConstDecl node) {
        // Evaluate the RHS to a compile-time constant and cache it (D-289, D-293).
        // No bytecode is emitted for the declaration itself; every reference site
        // inlines the value via EmitConstant instead of GetGlobal/GetLocal.
        _constValues[node] = EvalConstantExpr(node.Value);
        return null;
    }

    // -----------------------------------------------------------------------
    // Function declarations and returns (Sprint 5 Increment A)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles the body into its own <see cref="BytecodeFunction"/>, stores that
    /// function as a constant in the enclosing chunk, then binds it to its name as
    /// a global. A forward call resolves at runtime because every top-level <c>fn</c>
    /// is defined before the script body that calls them runs (and the type checker's
    /// pass-1 registration already made the name resolvable at compile time).
    /// </remarks>
    public override object? VisitFnDecl(FnDecl node) {
        int line = node.Range.Start.Line;
        BytecodeFunction fn = CompileFunction(node);
        EmitConstant(GrobValue.FromFunction(fn), line);
        int nameIdx = GetOrCreateGlobalNameIndex(node.Name);
        _chunk.WriteOpCode(OpCode.DefineGlobal, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, GlobalNameLabel), line);
        return null;
    }

    /// <summary>
    /// Compiles a <see cref="FnDecl"/> body into a standalone
    /// <see cref="BytecodeFunction"/>. Parameters occupy the first local slots
    /// (slot 0 = first parameter), addressed against the call frame's stack base at
    /// runtime. The body always ends with an implicit <c>Nil</c> + <c>Return</c> so
    /// a path that falls off the end returns nil rather than running past the chunk
    /// (all-paths-return analysis is not a v1 rule — the spec is silent).
    /// </summary>
    private BytecodeFunction CompileFunction(FnDecl node) {
        var sub = new Compiler(_constValues);

        // Parameters live in the function's outermost scope; the body opens its own
        // nested scope (VisitBlock), so a body local may shadow a parameter.
        sub._localScopes.Push([]);
        foreach (Parameter p in node.Parameters) {
            if ((uint)sub._nextSlot > byte.MaxValue)
                throw new GrobInternalException(
                    $"Parameter slot overflow: function '{node.Name}' exceeds the 1-byte slot limit of {byte.MaxValue}.");
            sub._localScopes.Peek().Add(new LocalVar(p.Name, sub._nextSlot++));
        }

        sub.Visit(node.Body);

        // Safety-net return: a body that does not return on every path falls through
        // to here and returns nil. Frame cleanup on Return discards locals.
        int endLine = node.Range.End.Line;
        sub._chunk.WriteOpCode(OpCode.Nil, endLine);
        sub._chunk.WriteOpCode(OpCode.Return, endLine);

        return new BytecodeFunction(
            node.Name, node.Parameters.Count, sub._chunk,
            parameterTypes: SignatureTypes(node.Parameters),
            returnType: SignatureType(node.ReturnType));
    }

    /// <summary>
    /// Maps a parameter list to the erased <see cref="GrobType"/> kinds carried on the
    /// <see cref="BytecodeFunction"/> for display (D-336). A parameter with no type
    /// annotation (a lambda parameter — inferred, untyped in v1) maps to
    /// <see cref="GrobType.Unknown"/>.
    /// </summary>
    private static IReadOnlyList<GrobType> SignatureTypes(IReadOnlyList<Parameter> parameters) =>
        parameters.Select(p => SignatureType(p.Type)).ToList();

    /// <summary>
    /// Maps a syntactic <see cref="TypeRef"/> to the erased <see cref="GrobType"/> kind
    /// used for a function value's display signature (D-336). This is display metadata
    /// only — the runtime function type is erased (D-326) — so a user-defined named type
    /// collapses to <see cref="GrobType.Struct"/> and the nested structural shape of a
    /// function-typed position is not carried here.
    /// </summary>
    private static GrobType SignatureType(TypeRef? typeRef) {
        if (typeRef is null) return GrobType.Unknown;
        GrobType baseType = typeRef switch {
            FunctionTypeRef => GrobType.Function,
            ArrayTypeRef => GrobType.Array,
            _ => NamedSignatureType(typeRef.Name),
        };
        return typeRef.IsNullable ? GrobTypeHelpers.ToNullable(baseType) : baseType;
    }

    private static GrobType NamedSignatureType(string name) => name switch {
        "int" => GrobType.Int,
        "float" => GrobType.Float,
        "string" => GrobType.String,
        "bool" => GrobType.Bool,
        "nil" => GrobType.Nil,
        "array" => GrobType.Array,
        "map" => GrobType.Map,
        _ => GrobType.Struct,   // a user-defined named type
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Emits the return value (or <c>Nil</c> for a bare <c>return</c>) then
    /// <see cref="OpCode.Return"/>, which the VM uses to pop the call frame and
    /// resume the caller. A top-level <c>return</c> is rejected by the type checker
    /// (E2203) and so never reaches a valid compile.
    /// </remarks>
    public override object? VisitReturn(ReturnStmt node) {
        int line = node.Range.Start.Line;
        if (node.Value is not null)
            Visit(node.Value);
        else
            _chunk.WriteOpCode(OpCode.Nil, line);

        // A return crosses every enclosing try/finally up to the function boundary,
        // so run each one's finally — innermost to outermost — before Return (D-275).
        // The return value is already on top of the operand stack; reserve its slot
        // (a single _nextSlot bump) so a finally body's own locals are allocated
        // above it and cannot collide with the parked value. The VM's Return bulk-
        // discards the frame, so no explicit local cleanup is needed here — only the
        // finally bodies must run. (_tryFinallyContexts is per-function, so every
        // entry is legitimately crossed by this return.)
        if (_tryFinallyContexts.Count > 0) {
            int savedSlot = _nextSlot;
            _nextSlot++; // park the return value below any finally-body locals
            foreach (TryFinallyContext tf in _tryFinallyContexts) // innermost first
                Visit(tf.FinallyBody);
            _nextSlot = savedSlot;
        }

        _chunk.WriteOpCode(OpCode.Return, line);
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles the operand (typically a <see cref="StructConstructionExpr"/>,
    /// reusing the Sprint 6B <see cref="OpCode.NewStruct"/> emission verbatim — no
    /// second construction path) then emits <see cref="OpCode.Throw"/>.
    /// </remarks>
    public override object? VisitThrow(ThrowStmt node) {
        int line = node.Range.Start.Line;
        Visit(node.Value);
        _chunk.WriteOpCode(OpCode.Throw, line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Expression statements (print, exit, and other calls)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitExpressionStmt(ExpressionStmt node) {
        if (node.Expression is CallExpr call &&
            call.Callee is IdentifierExpr callee) {
            int callLine = call.Range.Start.Line;

            if (callee.Name == "print" && call.Arguments.Count == 1) {
                // Emit the single argument then the Print opcode.
                Visit(call.Arguments[0].Value);
                _chunk.WriteOpCode(OpCode.Print, callLine);
                return null;
            }

            if (callee.Name == "exit") {
                // D-110: exit(n) or exit() — emit code then Exit opcode.
                // exit() with no argument exits with code 0.
                if (call.Arguments.Count == 1) {
                    Visit(call.Arguments[0].Value);
                } else {
                    EmitConstant(GrobValue.FromInt(0), callLine);
                }
                _chunk.WriteOpCode(OpCode.Exit, callLine);
                return null;
            }
        }

        // All other expression statements leave a value on the stack; pop it.
        Visit(node.Expression);
        _chunk.WriteOpCode(OpCode.Pop, node.Range.Start.Line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Assignment: x = expr  →  emit expr, then SetGlobal/SetLocal
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitAssignment(AssignmentStmt node) {
        if (node.Target is MemberAccessExpr memberTarget) {
            // An optional-chained member used as an assignment target is rejected by the
            // type checker with error E0206, so a well-formed compilation never reaches
            // this guard. It stays as defence in depth: emit nothing if one slips through.
            if (memberTarget.IsOptional) return null;
            int line = node.Range.Start.Line;
            Visit(memberTarget.Target);
            Visit(node.Value);
            int nameIdx = _chunk.AddConstant(GrobValue.FromString(memberTarget.Member));
            _chunk.WriteOpCode(OpCode.SetProperty, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, "property name"), line);
            return null;
        }

        if (node.Target is IndexExpr indexTarget) {
            // Sprint 9 Increment A2 (D-350): array/map index-store on the assignment-
            // target path. Emit the receiver — Visit re-enters VisitIndex for a chained
            // target (matrix[r][c]), emitting the inner read(s) first, exactly as the
            // read-side emission (D-348) already does for nested indexing — then the
            // index expression, then the value, then the existing OpCode.SetIndex
            // (already in the closed enum; this is its first emitter). No push: an
            // assignment statement leaves nothing on the stack, mirroring SetProperty.
            int indexAssignLine = node.Range.Start.Line;
            Visit(indexTarget.Target);
            Visit(indexTarget.Index);
            Visit(node.Value);
            _chunk.WriteOpCode(OpCode.SetIndex, indexAssignLine);
            return null;
        }

        if (node.Target is not IdentifierExpr target) {
            // No other assignment-target shape exists in the current grammar.
            return null;
        }

        int assignLine = node.Range.Start.Line;
        Visit(node.Value);
        EmitStore(target.Name, assignLine);
        return null;
    }

    // -----------------------------------------------------------------------
    // Compound assignment: x += y  →  load x, eval y, binary op, store x
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 9 Increment A4 (D-359): an <see cref="IndexExpr"/> target
    /// (<c>arr[i] += v</c>) is delegated to <see cref="EmitIndexReadModifyWrite"/>. Sprint 9
    /// Increment A4b (D-360): a <see cref="MemberAccessExpr"/> target (<c>obj.field += v</c>)
    /// is delegated to <see cref="EmitMemberReadModifyWrite"/> — the identifier-target path
    /// below is otherwise unchanged.
    /// </remarks>
    public override object? VisitCompoundAssignment(CompoundAssignmentStmt node) {
        if (node.Target is MemberAccessExpr memberTarget) {
            EmitMemberReadModifyWrite(
                memberTarget, memberTarget.ResolvedFieldType, node.Operator, node.Value, node.Range.Start.Line);
            return null;
        }

        if (node.Target is IndexExpr indexTarget) {
            EmitIndexReadModifyWrite(
                indexTarget, indexTarget.ElementType, node.Operator, node.Value, node.Range.Start.Line);
            return null;
        }

        if (node.Target is not IdentifierExpr target) return null;

        int line = node.Range.Start.Line;
        GrobType lt = target.ResolvedType;
        GrobType rt = GetExprType(node.Value);

        // Load the current value of the target; coerce to float immediately if needed.
        EmitLoad(target.Name, line);
        if (lt == GrobType.Int && rt == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        // Evaluate the RHS; coerce to float if needed.
        Visit(node.Value);
        if (lt == GrobType.Float && rt == GrobType.Int)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        // Emit the binary opcode.
        bool floatResult = lt == GrobType.Float || rt == GrobType.Float;
        _chunk.WriteOpCode(EmitCompoundBinaryOpCode(node.Operator, lt, floatResult), line);

        // Store the result back.
        EmitStore(target.Name, line);
        return null;
    }

    /// <summary>
    /// Sprint 9 Increment A4 (D-359): the evaluate-once read-modify-write shared by
    /// index-target compound assignment and increment/decrement (<c>arr[i]++</c> lowers
    /// to <c>arr[i] += 1</c> at the call site, an int literal <paramref name="valueExpr"/>
    /// and <see cref="CompoundAssignmentOperator.PlusAssign"/>/<c>MinusAssign</c>). The
    /// receiver and index expressions are each evaluated exactly once — stashed in
    /// reserved temp locals in their own scope — then re-read via <see cref="OpCode.GetLocal"/>
    /// for both <see cref="OpCode.SetIndex"/>'s eventual operands and the current-value
    /// <see cref="OpCode.GetIndex"/> read. No new opcode; the temp locals are released with
    /// the same <see cref="EmitScopeCleanup"/> a block uses, never lambda-capturable so
    /// this always resolves to a plain <see cref="OpCode.PopN"/>.
    /// </summary>
    private void EmitIndexReadModifyWrite(
            IndexExpr indexTarget, GrobType elementType, CompoundAssignmentOperator op,
            Expression valueExpr, int line) {
        _localScopes.Push([]);
        int scopeBase = _nextSlot;

        Visit(indexTarget.Target);
        int receiverSlot = DeclareLocalSlot("$idxRecv");
        Visit(indexTarget.Index);
        int indexSlot = DeclareLocalSlot("$idxIdx");

        // SetIndex's eventual receiver/index operands — pushed first so they sit
        // below the computed result.
        EmitGetLocal(receiverSlot, line);
        EmitGetLocal(indexSlot, line);

        // Read the current value: target[index].
        EmitGetLocal(receiverSlot, line);
        EmitGetLocal(indexSlot, line);
        _chunk.WriteOpCode(OpCode.GetIndex, line);

        GrobType rt = GetExprType(valueExpr);
        if (elementType == GrobType.Int && rt == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        Visit(valueExpr);
        if (elementType == GrobType.Float && rt == GrobType.Int)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        bool floatResult = elementType == GrobType.Float || rt == GrobType.Float;
        _chunk.WriteOpCode(EmitCompoundBinaryOpCode(op, elementType, floatResult), line);
        _chunk.WriteOpCode(OpCode.SetIndex, line);

        List<LocalVar> tempScope = _localScopes.Pop();
        EmitScopeCleanup(tempScope, line);
        _nextSlot = scopeBase;
    }

    /// <summary>
    /// Sprint 9 Increment A4b (D-360): the evaluate-once read-modify-write for a struct
    /// field target (<c>obj.field op= v</c>, and <c>obj.field++</c>/<c>--</c> lowering to
    /// <c>+= 1</c>/<c>-= 1</c> at the call site) — the member-target sibling of <see
    /// cref="EmitIndexReadModifyWrite"/>. The receiver is evaluated exactly once, stashed
    /// in a single reserved temp local (there is no index subexpression to stash — the
    /// field name is a static inline operand, so one <see cref="OpCode.Constant"/> serves
    /// both the read and the write). The temp local is released with the same
    /// <see cref="EmitScopeCleanup"/> a block uses.
    /// </summary>
    private void EmitMemberReadModifyWrite(
            MemberAccessExpr memberTarget, GrobType fieldType, CompoundAssignmentOperator op,
            Expression valueExpr, int line) {
        _localScopes.Push([]);
        int scopeBase = _nextSlot;

        Visit(memberTarget.Target);
        int receiverSlot = DeclareLocalSlot("$memRecv");
        int nameIdx = _chunk.AddConstant(GrobValue.FromString(memberTarget.Member));

        // SetProperty's eventual receiver operand — pushed first so it sits below the
        // computed result.
        EmitGetLocal(receiverSlot, line);

        // Read the current value: target.field.
        EmitGetLocal(receiverSlot, line);
        _chunk.WriteOpCode(OpCode.GetProperty, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, "property name"), line);

        GrobType rt = GetExprType(valueExpr);
        if (fieldType == GrobType.Int && rt == GrobType.Float)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        Visit(valueExpr);
        if (fieldType == GrobType.Float && rt == GrobType.Int)
            _chunk.WriteOpCode(OpCode.IntToFloat, line);

        bool floatResult = fieldType == GrobType.Float || rt == GrobType.Float;
        _chunk.WriteOpCode(EmitCompoundBinaryOpCode(op, fieldType, floatResult), line);
        _chunk.WriteOpCode(OpCode.SetProperty, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, "property name"), line);

        List<LocalVar> tempScope = _localScopes.Pop();
        EmitScopeCleanup(tempScope, line);
        _nextSlot = scopeBase;
    }

    /// <summary>
    /// Sprint 9 Increment A4b (D-360): <c>obj.field++</c>/<c>--</c> lowers to
    /// <c>obj.field += 1</c>/<c>-= 1</c> (int literal) and delegates to <see
    /// cref="EmitMemberReadModifyWrite"/>. Split out of <see cref="VisitIncrement"/> to keep
    /// that method's cognitive complexity under the analyser bar (mirrors why the
    /// index-target and identifier-target branches already sit in their own emission paths).
    /// </summary>
    private void EmitMemberIncrement(IncrementStmt node, MemberAccessExpr memberTarget) {
        GrobType fieldType = memberTarget.ResolvedFieldType;
        // Type errors (float++/string++) are already rejected by the type checker; a
        // receiver whose type could not be resolved still permissively emits Unknown
        // (int at runtime), mirroring the index-target map latitude.
        if (fieldType != GrobType.Int && fieldType != GrobType.Unknown) return;

        CompoundAssignmentOperator op = node.Kind == IncrementKind.Increment
            ? CompoundAssignmentOperator.PlusAssign
            : CompoundAssignmentOperator.MinusAssign;
        EmitMemberReadModifyWrite(
            memberTarget, fieldType, op, new IntLiteralExpr(node.Range, 1L), node.Range.Start.Line);
    }

    private static OpCode EmitCompoundBinaryOpCode(
        CompoundAssignmentOperator op, GrobType leftType, bool floatResult) => op switch {
            CompoundAssignmentOperator.PlusAssign when leftType == GrobType.String => OpCode.Concat,
            CompoundAssignmentOperator.PlusAssign when floatResult => OpCode.AddFloat,
            CompoundAssignmentOperator.PlusAssign => OpCode.AddInt,
            CompoundAssignmentOperator.MinusAssign when floatResult => OpCode.SubtractFloat,
            CompoundAssignmentOperator.MinusAssign => OpCode.SubtractInt,
            CompoundAssignmentOperator.StarAssign when floatResult => OpCode.MultiplyFloat,
            CompoundAssignmentOperator.StarAssign => OpCode.MultiplyInt,
            CompoundAssignmentOperator.SlashAssign when floatResult => OpCode.DivideFloat,
            CompoundAssignmentOperator.SlashAssign => OpCode.DivideInt,
            CompoundAssignmentOperator.PercentAssign when floatResult => OpCode.ModuloFloat,
            CompoundAssignmentOperator.PercentAssign => OpCode.ModuloInt,
            _ => throw new GrobInternalException($"Unknown compound assignment operator: {op}"),
        };

    // -----------------------------------------------------------------------
    // Increment / decrement:
    //   Local int:  IncrementInt/DecrementInt slot
    //   Global int: GetGlobal, Constant(1), AddInt/SubtractInt, SetGlobal
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 9 Increment A4 (D-359): an <see cref="IndexExpr"/> target
    /// (<c>arr[i]++</c>/<c>--</c>) lowers to <c>arr[i] += 1</c>/<c>-= 1</c> and is
    /// delegated to <see cref="EmitIndexReadModifyWrite"/> — there is no dedicated
    /// index-local fast path (that opcode pair only exists for a true stack local). Sprint 9
    /// Increment A4b (D-360): a <see cref="MemberAccessExpr"/> target (<c>obj.field++</c>/
    /// <c>--</c>) lowers the same way and is delegated to <see cref="EmitMemberReadModifyWrite"/>.
    /// </remarks>
    public override object? VisitIncrement(IncrementStmt node) {
        if (node.Target is MemberAccessExpr memberTarget) {
            EmitMemberIncrement(node, memberTarget);
            return null;
        }

        if (node.Target is IndexExpr indexTarget) {
            GrobType elementType = indexTarget.ElementType;
            // Type errors (float++/string++) are already rejected by the type checker;
            // a map's permissive Unknown element still emits (int at runtime).
            if (elementType != GrobType.Int && elementType != GrobType.Unknown) return null;

            CompoundAssignmentOperator op = node.Kind == IncrementKind.Increment
                ? CompoundAssignmentOperator.PlusAssign
                : CompoundAssignmentOperator.MinusAssign;
            EmitIndexReadModifyWrite(
                indexTarget, elementType, op, new IntLiteralExpr(node.Range, 1L), node.Range.Start.Line);
            return null;
        }

        if (node.Target is not IdentifierExpr target) return null;
        // Type errors (float++, const++) are already rejected by the type checker;
        // only emit for valid int targets.
        if (target.ResolvedType != GrobType.Int) return null;

        int line = node.Range.Start.Line;
        int slot = FindLocalSlot(target.Name);

        if (slot >= 0) {
            // Local: use the dedicated opcode (in-place slot update).
            _chunk.WriteOpCode(
                node.Kind == IncrementKind.Increment ? OpCode.IncrementInt : OpCode.DecrementInt,
                line);
            _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
        } else {
            // Global: load-operate-store sequence.
            int nameIdx = GetOrCreateGlobalNameIndex(target.Name);
            _chunk.WriteOpCode(OpCode.GetGlobal, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, GlobalNameLabel), line);

            EmitConstant(GrobValue.FromInt(1L), line);

            _chunk.WriteOpCode(
                node.Kind == IncrementKind.Increment ? OpCode.AddInt : OpCode.SubtractInt,
                line);

            _chunk.WriteOpCode(OpCode.SetGlobal, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, GlobalNameLabel), line);
        }
        return null;
    }
}
