using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class Compiler {
    // -----------------------------------------------------------------------
    // while / break / continue  (Sprint 4 Increment B)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles a <c>while</c> statement using the loop-context stack and the
    /// backward-jump <see cref="EmitLoop"/> helper:
    /// <list type="number">
    ///   <item><description>Record <c>loopStart</c> (the chunk offset of the first
    ///     condition byte) and push a new <see cref="LoopContext"/>.</description></item>
    ///   <item><description>Emit the condition expression.</description></item>
    ///   <item><description>Emit <see cref="OpCode.JumpIfFalse"/> (forward, backpatched)
    ///     to exit the loop when the condition is false.</description></item>
    ///   <item><description>Emit the body.  <see cref="VisitBlock"/> handles <c>PopN</c>
    ///     for locals declared inside the body on the normal exit path.</description></item>
    ///   <item><description>Emit <see cref="OpCode.Loop"/> (backward to <c>loopStart</c>)
    ///     to re-evaluate the condition.</description></item>
    ///   <item><description>Patch the exit <see cref="OpCode.JumpIfFalse"/> to
    ///     land here (the first instruction past the loop).</description></item>
    ///   <item><description>Pop the loop context and backpatch every recorded
    ///     <c>break</c> jump to the same exit target.</description></item>
    /// </list>
    /// </remarks>
    public override object? VisitWhile(WhileStmt node) {
        int line = node.Range.Start.Line;

        // Record the loop top: the first byte of the condition expression.
        int loopStart = _chunk.Count;

        // Push a loop context so that break/continue inside the body resolve here.
        var ctx = new LoopContext(continueTarget: loopStart, baseSlot: _nextSlot);
        _loopContexts.Push(ctx);

        // Evaluate the condition; JumpIfFalse pops it and exits when false.
        Visit(node.Condition);
        int exitJump = EmitJump(OpCode.JumpIfFalse, line);

        // Body — VisitBlock opens a scope, emits body statements, emits PopN on normal exit.
        Visit(node.Body);

        // End of body: jump back to the condition.
        EmitLoop(loopStart, line);

        // Patch the exit jump to land here (first instruction after the loop).
        PatchJump(exitJump);

        // Pop the loop context and patch all break jumps to the same exit target.
        _loopContexts.Pop();
        foreach (int breakSite in ctx.BreakSites)
            PatchJump(breakSite);

        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Emits a forward <see cref="OpCode.Jump"/> that exits the innermost loop,
    /// after popping any locals declared inside the loop body above this point.
    /// The jump site is recorded on the current <see cref="LoopContext"/> and
    /// backpatched when the loop closes in <see cref="VisitWhile"/> (or the
    /// equivalent for...in lowering in Increment C).
    /// </remarks>
    public override object? VisitBreak(BreakStmt node) {
        if (_loopContexts.Count == 0)
            throw new GrobInternalException(
                "break outside loop reached the compiler. The type checker should have rejected this source.");

        LoopContext ctx = _loopContexts.Peek();
        int line = node.Range.Start.Line;

        // Pop locals declared inside the loop above this break — they are not
        // cleaned up by VisitBlock because the break jumps past the block exit.
        int localsToPop = _nextSlot - ctx.BaseSlot;
        if (localsToPop > 0) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(localsToPop, "break locals pop"), line);
        }

        // Forward jump; patch site is recorded and resolved when the loop closes.
        int site = EmitJump(OpCode.Jump, line);
        ctx.RecordBreak(site);
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Jumps backward to the loop's continue target via <see cref="OpCode.Loop"/>,
    /// after popping any locals declared inside the loop body above this point.
    /// For <c>while</c> the target is the loop top (the condition).  The target is
    /// taken from <see cref="LoopContext.ContinueTarget"/> rather than hard-coded,
    /// so the <c>for...in</c> lowering in Increment C can point <c>continue</c> at
    /// the increment step instead.
    /// </remarks>
    public override object? VisitContinue(ContinueStmt node) {
        if (_loopContexts.Count == 0)
            throw new GrobInternalException(
                "continue outside loop reached the compiler. The type checker should have rejected this source.");

        LoopContext ctx = _loopContexts.Peek();
        int line = node.Range.Start.Line;

        // Pop locals declared inside the loop above this continue — they are not
        // cleaned up by VisitBlock because the continue jumps past the block exit.
        int localsToPop = _nextSlot - ctx.BaseSlot;
        if (localsToPop > 0) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(localsToPop, "continue locals pop"), line);
        }

        if (ctx.HasForwardContinue) {
            // for...in: the increment step sits after the body, so continue must
            // forward-jump to it. The site is backpatched in VisitForIn.
            int site = EmitJump(OpCode.Jump, line);
            ctx.RecordContinue(site);
        } else {
            // while: continue re-evaluates the condition at the loop top.
            EmitLoop(ctx.ContinueTarget, line);
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // for...in  (Sprint 4 Increment C)
    //
    // Every form lowers to the while machine over the Increment B loop-context
    // model. The synthetic iteration locals (counter, limit, step, keys array,
    // map) live in a scope spanning the loop; the visible iteration variables
    // (item, k, v) are re-bound in a fresh body scope each iteration so they are
    // immutable per-iteration bindings. 'continue' targets the increment step via
    // a forward jump (HasForwardContinue) so the counter always advances.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitForIn(ForInStmt node) {
        int line = node.Range.Start.Line;

        // The iteration locals must be stack locals even at top level, so a scope
        // is opened for them. GetLocal/SetLocal/IncrementInt then address them.
        _localScopes.Push([]);
        int forScopeBase = _nextSlot;

        if (node.Iterable is NumericRangeExpr range)
            EmitRangeForIn(node, range, line);
        else if (GetExprType(node.Iterable) == GrobType.Map)
            EmitMapForIn(node, line);
        else
            EmitArrayForIn(node, line);

        _localScopes.Pop();
        _nextSlot = forScopeBase;
        return null;
    }

    /// <summary>
    /// Numeric-range lowering. The counter is the visible loop variable, held in a
    /// for-scope local; the end bound and optional step are synthetic for-scope
    /// locals. Ascending iteration compares with <c>&lt;=</c> (inclusive) and steps
    /// by <c>++</c> or <c>+= step</c>; a negative literal step compares with
    /// <c>&gt;=</c> and steps by <c>+= step</c>.
    /// </summary>
    private void EmitRangeForIn(ForInStmt node, NumericRangeExpr range, int line) {
        Visit(range.Start);
        int counter = DeclareLocalSlot(node.Variables[0]);
        Visit(range.End);
        int limit = DeclareLocalSlot("$hi");

        bool descending = IsNegativeStepLiteral(range.Step);
        bool hasStep = range.Step is not null;
        int stepSlot = -1;
        if (hasStep) {
            Visit(range.Step!);
            stepSlot = DeclareLocalSlot("$step");
        }

        int syntheticCount = hasStep ? 3 : 2;
        OpCode compare = descending ? OpCode.GreaterEqualInt : OpCode.LessEqualInt;

        EmitForInLoop(node, syntheticCount, line,
            emitCondition: () => {
                EmitGetLocal(counter, line);
                EmitGetLocal(limit, line);
                _chunk.WriteOpCode(compare, line);
            },
            emitIteratorBindings: () => { /* counter is the visible variable */ },
            emitIncrement: () => {
                if (hasStep) {
                    EmitGetLocal(counter, line);
                    EmitGetLocal(stepSlot, line);
                    _chunk.WriteOpCode(OpCode.AddInt, line);
                    EmitSetLocal(counter, line);
                } else {
                    EmitIncrementLocal(counter, line);
                }
            });
    }

    /// <summary>
    /// Array lowering for both the single form (<c>for item in arr</c>) and the
    /// index form (<c>for i, item in arr</c>). A synthetic array local and counter
    /// drive <c>i &lt; arr.length</c>; <c>item</c> is re-bound from <c>arr[i]</c>
    /// each iteration. In the index form the counter is the visible <c>i</c>.
    /// </summary>
    private void EmitArrayForIn(ForInStmt node, int line) {
        bool indexForm = node.Variables.Count == 2;
        Visit(node.Iterable);
        int array = DeclareLocalSlot("$arr");

        EmitConstant(GrobValue.FromInt(0L), line);
        int counter = DeclareLocalSlot(indexForm ? node.Variables[0] : "$i");
        string itemName = indexForm ? node.Variables[1] : node.Variables[0];

        EmitForInLoop(node, syntheticCount: 2, line,
            emitCondition: () => {
                EmitGetLocal(counter, line);
                EmitGetLocal(array, line);
                EmitGetProperty("length", line);
                _chunk.WriteOpCode(OpCode.LessInt, line);
            },
            emitIteratorBindings: () => {
                EmitGetLocal(array, line);
                EmitGetLocal(counter, line);
                _chunk.WriteOpCode(OpCode.GetIndex, line);
                DeclareLocalSlot(itemName);
            },
            emitIncrement: () => EmitIncrementLocal(counter, line));
    }

    /// <summary>
    /// Map lowering (<c>for k, v in m</c>). The map and its insertion-order keys
    /// array are materialised once into for-scope locals before the loop; the
    /// counter walks the keys array. Each iteration binds <c>k = keys[i]</c> then
    /// <c>v = m[k]</c>.
    /// </summary>
    private void EmitMapForIn(ForInStmt node, int line) {
        Visit(node.Iterable);
        int map = DeclareLocalSlot("$map");

        EmitGetLocal(map, line);
        EmitGetProperty("keys", line); // materialise the keys array exactly once
        int keys = DeclareLocalSlot("$keys");

        EmitConstant(GrobValue.FromInt(0L), line);
        int counter = DeclareLocalSlot("$i");

        string keyName = node.Variables[0];
        string valueName = node.Variables[1];

        EmitForInLoop(node, syntheticCount: 3, line,
            emitCondition: () => {
                EmitGetLocal(counter, line);
                EmitGetLocal(keys, line);
                EmitGetProperty("length", line);
                _chunk.WriteOpCode(OpCode.LessInt, line);
            },
            emitIteratorBindings: () => {
                EmitGetLocal(keys, line);
                EmitGetLocal(counter, line);
                _chunk.WriteOpCode(OpCode.GetIndex, line);
                int keySlot = DeclareLocalSlot(keyName);

                EmitGetLocal(map, line);
                EmitGetLocal(keySlot, line);
                _chunk.WriteOpCode(OpCode.GetIndex, line);
                DeclareLocalSlot(valueName);
            },
            emitIncrement: () => EmitIncrementLocal(counter, line));
    }

    /// <summary>
    /// The shared while machine for every <c>for...in</c> form. Emits the
    /// condition, the body (in its own scope so per-iteration bindings are popped),
    /// the increment step (the <c>continue</c> target) and the backward loop jump,
    /// then patches the exit and the recorded <c>break</c> sites and pops the
    /// synthetic for-scope locals.
    /// </summary>
    private void EmitForInLoop(
        ForInStmt node, int syntheticCount, int line,
        Action emitCondition, Action emitIteratorBindings, Action emitIncrement) {
        // Base slot for break/continue cleanup: just above the synthetic locals.
        int baseSlot = _nextSlot;
        var ctx = new LoopContext(continueTarget: 0, baseSlot: baseSlot, hasForwardContinue: true);
        _loopContexts.Push(ctx);

        int loopStart = _chunk.Count;
        emitCondition();
        int exitJump = EmitJump(OpCode.JumpIfFalse, line);

        // Body scope: iterator variables plus any body locals, popped each iteration.
        _localScopes.Push([]);
        emitIteratorBindings();
        foreach (Statement stmt in node.Body.Statements) Visit(stmt);
        List<LocalVar> bodyScope = _localScopes.Pop();
        if (bodyScope.Count > 0) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(bodyScope.Count, "for...in body PopN"), line);
            _nextSlot -= bodyScope.Count;
        }

        // Increment step — the continue target. Backpatch every continue jump here.
        foreach (int site in ctx.ContinueSites) PatchJump(site);
        emitIncrement();
        EmitLoop(loopStart, line);

        // Exit — condition-false and break both land here, before the synthetic pop.
        PatchJump(exitJump);
        _loopContexts.Pop();
        foreach (int site in ctx.BreakSites) PatchJump(site);

        if (syntheticCount > 0) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(syntheticCount, "for...in synthetic PopN"), line);
            _nextSlot -= syntheticCount;
        }
    }

    /// <summary>
    /// Declares a stack local at the next free slot, recording it in the current
    /// scope so name lookups resolve it. The value is expected to already be on the
    /// top of the operand stack — that stack position is the slot.
    /// </summary>
    private int DeclareLocalSlot(string name) {
        if ((uint)_nextSlot > byte.MaxValue)
            throw new GrobInternalException(
                $"Local slot overflow declaring '{name}' in for...in lowering.");
        int slot = _nextSlot++;
        _localScopes.Peek().Add(new LocalVar(name, slot));
        return slot;
    }

    private void EmitGetLocal(int slot, int line) {
        _chunk.WriteOpCode(OpCode.GetLocal, line);
        _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
    }

    private void EmitSetLocal(int slot, int line) {
        _chunk.WriteOpCode(OpCode.SetLocal, line);
        _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
    }

    private void EmitIncrementLocal(int slot, int line) {
        _chunk.WriteOpCode(OpCode.IncrementInt, line);
        _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
    }

    private void EmitGetProperty(string name, int line) {
        int nameIdx = GetOrCreateGlobalNameIndex(name);
        _chunk.WriteOpCode(OpCode.GetProperty, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, "property name"), line);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="step"/> is a negative
    /// integer literal — written as a unary minus over an int literal, the only
    /// form the parser produces for a negative number.
    /// </summary>
    private static bool IsNegativeStepLiteral(Expression? step) =>
        step is UnaryExpr { Operator: UnaryOperator.Negate, Operand: IntLiteralExpr };

    // -----------------------------------------------------------------------
    // if / else if / else
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles an <c>if</c> statement using forward-jump backpatching (EmitJump / PatchJump):
    /// <list type="number">
    ///   <item><description>Emit the condition.</description></item>
    ///   <item><description>Emit <see cref="OpCode.JumpIfFalse"/> with a placeholder offset
    ///     to skip the then-block.  JumpIfFalse pops the condition.</description></item>
    ///   <item><description>Emit the then-block body.</description></item>
    ///   <item><description>If there is an else clause, emit an unconditional
    ///     <see cref="OpCode.Jump"/> to skip the else-block, patch the false-jump to the
    ///     else-block start, emit the else-block (which may itself be another
    ///     <c>IfStmt</c> for <c>else if</c> chains), then patch the exit jump.</description></item>
    ///   <item><description>If there is no else clause, patch the false-jump immediately
    ///     after the then-block.</description></item>
    /// </list>
    /// <para>
    /// <c>else if</c> chains are nested <see cref="IfStmt"/> nodes in the
    /// <see cref="IfStmt.Else"/> field.  The recursive visit call handles
    /// them naturally; each arm contributes its own pair of jumps.
    /// </para>
    /// </remarks>
    public override object? VisitIf(IfStmt node) {
        int line = node.Range.Start.Line;

        // Emit condition; JumpIfFalse pops and jumps when false.
        Visit(node.Condition);
        int falseJump = EmitJump(OpCode.JumpIfFalse, line);

        // Then-block.
        Visit(node.Then);

        if (node.Else is not null) {
            // Unconditional jump at the end of the then-block to skip the else-block.
            int exitJump = EmitJump(OpCode.Jump, line);

            // Patch the false-jump to land here (start of else-block).
            PatchJump(falseJump);

            // Else-block or nested IfStmt (else if).
            Visit(node.Else);

            // Patch the exit jump to land here (past the else-block).
            PatchJump(exitJump);
        } else {
            // No else: patch the false-jump to land immediately after the then-block.
            PatchJump(falseJump);
        }

        return null;
    }
}
