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
            _chunk.WriteOpCode(OpCode.PopN, node.Range.End.Line);
            _chunk.WriteByte(ToByteOperand(scope.Count, "PopN count"), node.Range.End.Line);
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
        if (node.Target is not IdentifierExpr target) {
            // Field/index targets — deferred (Sprint 6 / collections).
            return null;
        }

        int line = node.Range.Start.Line;
        Visit(node.Value);
        EmitStore(target.Name, line);
        return null;
    }

    // -----------------------------------------------------------------------
    // Compound assignment: x += y  →  load x, eval y, binary op, store x
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitCompoundAssignment(CompoundAssignmentStmt node) {
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
    public override object? VisitIncrement(IncrementStmt node) {
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
