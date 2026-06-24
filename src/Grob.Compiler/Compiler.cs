using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Bytecode compiler for Grob. Walks a type-checked <see cref="CompilationUnit"/>
/// and emits instructions into a <see cref="Chunk"/> (D-307, Increment D).
/// </summary>
/// <remarks>
/// <para>The compiler is a single-pass <see cref="AstVisitor{T}"/> that inherits
/// the full visitor protocol. Implementation is split across partial classes:</para>
/// <list type="bullet">
///   <item><description><c>Compiler.Expressions.cs</c> — literal, unary, and binary expression emission.</description></item>
///   <item><description><c>Compiler.Statements.cs</c> — statement and declaration emission.</description></item>
/// </list>
/// <para>Call <see cref="Compile"/> to run the full pass.</para>
/// </remarks>
public sealed partial class Compiler : AstVisitor<object?> {
    private readonly Chunk _chunk = new();

    // -----------------------------------------------------------------------
    // Global name table (Sprint 3A).
    // Maps a global variable name to its constant-pool index (the slot used
    // by DefineGlobal / GetGlobal / SetGlobal).
    // -----------------------------------------------------------------------
    private readonly Dictionary<string, int> _globalNameIndices = new(StringComparer.Ordinal);

    // -----------------------------------------------------------------------
    // Local scope stack (Sprint 3A).
    // Each entry on the stack corresponds to one open block scope and records
    // the local variables declared inside it.  When the stack is empty we are
    // at the top-level (global) scope.
    // -----------------------------------------------------------------------
    private sealed class LocalVar {
        public string Name { get; }
        public int Slot { get; }

        /// <summary>
        /// Set when a nested lambda captures this local as an upvalue (D-115).
        /// At scope exit the compiler emits <see cref="OpCode.CloseUpvalue"/> for a
        /// captured local instead of a blind <see cref="OpCode.PopN"/>, so the
        /// upvalue migrates to the heap before the slot can be reused.
        /// </summary>
        public bool Captured { get; set; }

        public LocalVar(string name, int slot) {
            Name = name;
            Slot = slot;
        }
    }

    private readonly Stack<List<LocalVar>> _localScopes = new();
    private int _nextSlot;   // next available stack slot for a new local

    // -----------------------------------------------------------------------
    // Const value table (Sprint 3C).
    // Maps each ConstDecl node to its compile-time-evaluated GrobValue.
    // Every reference to a const identifier is inlined as a direct Constant
    // load rather than going through the global/local slot machinery (D-293).
    // -----------------------------------------------------------------------
    private readonly Dictionary<ConstDecl, GrobValue> _constValues;

    // -----------------------------------------------------------------------
    // Upvalue resolution (Sprint 5 Increment D — D-115, D-296 category 4).
    //
    // A lambda body that references a local of the immediately enclosing
    // function (category 4) captures it as an upvalue instead of falling
    // through to a GetGlobal lookup.
    //
    // _enclosing is the sub-compiler that compiled the enclosing function or
    // lambda body.  It is null at the root compiler and at the top-level
    // fn-declaration sub-compiler (top-level fns cannot be closures because
    // there is no enclosing function frame to capture from).
    //
    // _upvalues is the descriptor list built up as ResolveUpvalue walks the
    // chain.  Its count becomes BytecodeFunction.UpvalueCount and its entries
    // drive the Closure opcode's descriptor bytes.
    // -----------------------------------------------------------------------

    /// <summary>
    /// One descriptor entry written into the <see cref="OpCode.Closure"/>
    /// instruction stream.  <see cref="IsLocal"/> true means the opcode captures
    /// a local slot of the immediately enclosing function's stack frame;
    /// false means it copies an upvalue from the enclosing closure's upvalue array.
    /// </summary>
    private sealed record UpvalueDescriptor(bool IsLocal, int Index);

    /// <summary>
    /// The sub-compiler that compiled the enclosing function or lambda body,
    /// or <see langword="null"/> when this compiler is the root or is compiling
    /// a top-level <c>fn</c> declaration (which has no enclosing frame to capture from).
    /// </summary>
    private readonly Compiler? _enclosing;

    /// <summary>
    /// Upvalue descriptors accumulated for the function being compiled.
    /// Ordered by the upvalue slot index (descriptor at index 0 → upvalue 0, etc.).
    /// </summary>
    private readonly List<UpvalueDescriptor> _upvalues = [];

    // -----------------------------------------------------------------------
    // Loop-context stack (Sprint 4 Increment B).
    //
    // A LoopContext is pushed when compiling a while or for...in loop and popped
    // when the loop exits.  break and continue resolve to the top context.
    // select (Increment D) does NOT push a context — so break/continue inside a
    // select case fall through to the nearest enclosing loop context, giving
    // Increment D the correct semantics for free.
    // -----------------------------------------------------------------------
    private sealed class LoopContext {
        private readonly List<int> _breakSites = [];
        private readonly List<int> _continueSites = [];

        /// <summary>
        /// Chunk offset that a backward-jumping <c>continue</c> targets.  For
        /// <c>while</c> this is the loop top (the condition), set at loop entry.
        /// Unused when <see cref="HasForwardContinue"/> is <c>true</c> — the
        /// <c>for...in</c> lowering needs <c>continue</c> to reach the increment
        /// step, which sits <em>after</em> the body, so it forward-jumps and
        /// backpatches via <see cref="ContinueSites"/> instead.
        /// </summary>
        public int ContinueTarget { get; set; }

        /// <summary>
        /// Value of <c>_nextSlot</c> at loop entry (for <c>while</c>) or just above
        /// the synthetic iteration locals (for <c>for...in</c>).  Used to compute
        /// how many locals to pop before a <c>break</c> or <c>continue</c> that
        /// exits the current local scope without running the normal
        /// <see cref="VisitBlock"/> cleanup.
        /// </summary>
        public int BaseSlot { get; }

        /// <summary>
        /// When <c>true</c>, <c>continue</c> emits a forward <see cref="OpCode.Jump"/>
        /// (recorded on <see cref="ContinueSites"/> and backpatched to the increment
        /// step when the loop closes) rather than a backward <see cref="OpCode.Loop"/>.
        /// Set by the <c>for...in</c> lowering so the counter advances on every
        /// iteration, including one ended by <c>continue</c>.
        /// </summary>
        public bool HasForwardContinue { get; }

        public LoopContext(int continueTarget, int baseSlot, bool hasForwardContinue = false) {
            ContinueTarget = continueTarget;
            BaseSlot = baseSlot;
            HasForwardContinue = hasForwardContinue;
        }

        public void RecordBreak(int patchSite) => _breakSites.Add(patchSite);

        public IReadOnlyList<int> BreakSites => _breakSites;

        public void RecordContinue(int patchSite) => _continueSites.Add(patchSite);

        public IReadOnlyList<int> ContinueSites => _continueSites;
    }

    private readonly Stack<LoopContext> _loopContexts = new();

    private bool IsGlobalScope => _localScopes.Count == 0;

    /// <summary>Root compiler for a compilation unit — owns a fresh const cache.</summary>
    private Compiler() {
        _constValues = [];
    }

    /// <summary>
    /// Sub-compiler for a top-level <c>fn</c> declaration. Each function compiles
    /// into its own <see cref="Chunk"/> (fresh <c>_globalNameIndices</c>, keyed to
    /// that chunk's constant pool) but shares the root's compile-time
    /// <paramref name="constValues"/> cache so a body can inline a top-level
    /// <c>const</c> (D-293) declared before it.
    /// Top-level <c>fn</c>s are not closures — there is no enclosing frame to
    /// capture from — so <see cref="_enclosing"/> is left null.
    /// </summary>
    private Compiler(Dictionary<ConstDecl, GrobValue> constValues) {
        _constValues = constValues;
    }

    /// <summary>
    /// Sub-compiler for a lambda body that may capture enclosing-function locals
    /// (category 4, D-296). Shares the const cache and links to
    /// <paramref name="enclosing"/> so <see cref="ResolveUpvalue"/> can walk the
    /// chain to find the captured variable's home.
    /// </summary>
    private Compiler(Dictionary<ConstDecl, GrobValue> constValues, Compiler enclosing) {
        _constValues = constValues;
        _enclosing = enclosing;
    }

    /// <summary>
    /// Compiles a type-checked <paramref name="unit"/> into a <see cref="Chunk"/>
    /// ready for the VM.  The caller must ensure that
    /// <paramref name="diagnostics"/> has no errors before calling; if the bag
    /// already contains errors the returned chunk may be incomplete.
    /// </summary>
    /// <param name="unit">The AST produced by the parser and validated by the type checker.</param>
    /// <param name="diagnostics">Diagnostic bag (may receive additional compile-time errors).</param>
    /// <returns>A <see cref="Chunk"/> containing the emitted bytecode.</returns>
    public static Chunk Compile(CompilationUnit unit, DiagnosticBag diagnostics) {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(diagnostics);
        var compiler = new Compiler();
        compiler.Visit(unit);
        return compiler._chunk;
    }

    // -----------------------------------------------------------------------
    // Root
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Top-level items are emitted in three ordered phases (D-321):
    /// <list type="number">
    ///   <item><description><b>Const pre-pass.</b> Every <c>const</c> is folded and cached
    ///     (no bytecode). Running this first lets a hoisted <c>fn</c> body inline any
    ///     top-level <c>const</c> regardless of their relative source order (D-293).</description></item>
    ///   <item><description><b>Fn prologue.</b> Every top-level <c>fn</c> emits its
    ///     <see cref="OpCode.DefineGlobal"/> before any top-level statement runs, so the
    ///     binding is <c>Initialised</c> ahead of the code that may call it. Top-level
    ///     named functions capture no upvalues (D-296), so this is a plain binding
    ///     prologue with no capture-ordering hazard. This is what makes a call to a
    ///     function declared later in source resolve at runtime rather than raising
    ///     E5902 (§19.1).</description></item>
    ///   <item><description><b>Main body.</b> Every remaining top-level item runs in
    ///     source order. E5902 is therefore reachable only for a genuine value-binding
    ///     initialisation cycle.</description></item>
    /// </list>
    /// </remarks>
    public override object? VisitCompilationUnit(CompilationUnit node) {
        // Phase 1 — fold and cache every top-level const (emits no bytecode).
        foreach (AstNode item in node.TopLevel) {
            if (item is ConstDecl) Visit(item);
        }

        // Phase 2 — fn prologue: bind every top-level function before any top-level
        // statement executes, so forward calls resolve at runtime.
        foreach (AstNode item in node.TopLevel) {
            if (item is FnDecl) Visit(item);
        }

        // Phase 3 — top-level statements and value bindings in source order.
        foreach (AstNode item in node.TopLevel) {
            if (item is not ConstDecl and not FnDecl) Visit(item);
        }

        int returnLine = node.Range.End.Line;
        _chunk.WriteOpCode(OpCode.Return, returnLine);
        return null;
    }

    // -----------------------------------------------------------------------
    // Fallback — silently skip unrecognised nodes (deferred to Sprint 3+).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    protected override object? DefaultVisit(AstNode node) => null;

    // -----------------------------------------------------------------------
    // Error nodes — required abstract overrides (§29.2 contract).
    // Errors in the AST mean the type checker has already diagnosed the
    // problem; no bytecode is emitted.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitErrorExpr(ErrorExpr node) => null;

    /// <inheritdoc/>
    public override object? VisitErrorStmt(ErrorStmt node) => null;

    /// <inheritdoc/>
    public override object? VisitErrorDecl(ErrorDecl node) => null;

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Guards a bytecode operand that must fit in a single byte.
    /// Throws <see cref="GrobInternalException"/> on overflow; the current ISA
    /// uses 1-byte operands for all local/global indices (max 256).  Wide-operand
    /// variants are planned for a future sprint.
    /// </summary>
    private static byte ToByteOperand(int value, string context) {
        if ((uint)value > byte.MaxValue)
            throw new GrobInternalException(
                $"Bytecode operand overflow: {context} index {value} exceeds the " +
                $"1-byte limit of {byte.MaxValue}. Wide-operand opcodes are planned for a future sprint.");
        return (byte)value;
    }

    /// <summary>
    /// Adds <paramref name="value"/> to the constant pool and emits a
    /// <see cref="OpCode.Constant"/> (1-byte index) or
    /// <see cref="OpCode.ConstantLong"/> (2-byte big-endian index) instruction.
    /// </summary>
    private void EmitConstant(GrobValue value, int line) {
        int index = _chunk.AddConstant(value);
        if (index <= byte.MaxValue) {
            _chunk.WriteOpCode(OpCode.Constant, line);
            _chunk.WriteByte((byte)index, line);
        } else {
            _chunk.WriteOpCode(OpCode.ConstantLong, line);
            _chunk.WriteByte((byte)(index >> 8), line);
            _chunk.WriteByte((byte)(index & 0xFF), line);
        }
    }

    /// <summary>
    /// Returns the constant-pool index for the global name <paramref name="name"/>,
    /// creating a string constant in the pool the first time the name is seen.
    /// </summary>
    private int GetOrCreateGlobalNameIndex(string name) {
        if (_globalNameIndices.TryGetValue(name, out int existing)) return existing;
        int idx = _chunk.AddConstant(GrobValue.FromString(name));
        ToByteOperand(idx, $"global name '{name}'"); // validate before caching
        _globalNameIndices[name] = idx;
        return idx;
    }

    /// <summary>
    /// Looks up a local variable by name in the current scope stack,
    /// searching inner-to-outer.  Returns the slot index or <c>-1</c>
    /// if not found in any local scope.
    /// </summary>
    private int FindLocalSlot(string name) {
        foreach (List<LocalVar> scope in _localScopes) {
            for (int i = scope.Count - 1; i >= 0; i--) {
                if (scope[i].Name == name) return scope[i].Slot;
            }
        }
        return -1;
    }

    /// <summary>
    /// Marks the local at absolute <paramref name="slot"/> as captured by a nested
    /// lambda, so its scope-exit cleanup closes the upvalue (D-115). Called from a
    /// sub-compiler's <see cref="ResolveUpvalue"/> against this enclosing compiler.
    /// </summary>
    private void MarkLocalCaptured(int slot) {
        foreach (List<LocalVar> scope in _localScopes) {
            foreach (LocalVar local in scope) {
                if (local.Slot == slot) {
                    local.Captured = true;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Emits cleanup for <paramref name="locals"/> leaving scope. When none are
    /// captured (the common path) a single <see cref="OpCode.PopN"/> discards them.
    /// When at least one is captured, each local is cleaned top-of-stack first:
    /// <see cref="OpCode.CloseUpvalue"/> for a captured local (close the upvalue to
    /// the heap, then pop) and <see cref="OpCode.Pop"/> for an uncaptured one. This
    /// migrates captured variables off the stack before their slots can be reused
    /// (D-115), which a blind <c>PopN</c> would not do.
    /// </summary>
    private void EmitScopeCleanup(IReadOnlyCollection<LocalVar> locals, int line) {
        if (locals.Count == 0) return;

        if (!locals.Any(l => l.Captured)) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(locals.Count, "scope cleanup PopN"), line);
            return;
        }

        // CloseUpvalue closes the top open upvalue then pops one slot; Pop discards
        // one slot. Process highest slot first so each captured local is on top when
        // its CloseUpvalue runs.
        foreach (LocalVar local in locals.OrderByDescending(l => l.Slot)) {
            _chunk.WriteOpCode(local.Captured ? OpCode.CloseUpvalue : OpCode.Pop, line);
        }
    }

    /// <summary>
    /// Emits a load instruction for the variable <paramref name="name"/>:
    /// <see cref="OpCode.GetLocal"/> when the name resolves to a local slot in
    /// this function's scope, <see cref="OpCode.GetUpvalue"/> when it resolves
    /// to a captured upvalue (category 4, D-296), or
    /// <see cref="OpCode.GetGlobal"/> otherwise.
    /// </summary>
    private void EmitLoad(string name, int line) {
        int slot = FindLocalSlot(name);
        if (slot >= 0) {
            _chunk.WriteOpCode(OpCode.GetLocal, line);
            _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
            return;
        }
        int uv = ResolveUpvalue(name);
        if (uv >= 0) {
            _chunk.WriteOpCode(OpCode.GetUpvalue, line);
            _chunk.WriteByte(ToByteOperand(uv, "upvalue index"), line);
            return;
        }
        int nameIdx = GetOrCreateGlobalNameIndex(name);
        _chunk.WriteOpCode(OpCode.GetGlobal, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, "global name"), line);
    }

    /// <summary>
    /// Emits a store instruction for the variable <paramref name="name"/>:
    /// <see cref="OpCode.SetLocal"/> when the name resolves to a local slot in
    /// this function's scope, <see cref="OpCode.SetUpvalue"/> when it resolves
    /// to a captured upvalue (category 4, D-296), or
    /// <see cref="OpCode.SetGlobal"/> otherwise.
    /// The value to store must already be on the top of the stack.
    /// </summary>
    private void EmitStore(string name, int line) {
        int slot = FindLocalSlot(name);
        if (slot >= 0) {
            _chunk.WriteOpCode(OpCode.SetLocal, line);
            _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
            return;
        }
        int uv = ResolveUpvalue(name);
        if (uv >= 0) {
            _chunk.WriteOpCode(OpCode.SetUpvalue, line);
            _chunk.WriteByte(ToByteOperand(uv, "upvalue index"), line);
            return;
        }
        int nameIdx = GetOrCreateGlobalNameIndex(name);
        _chunk.WriteOpCode(OpCode.SetGlobal, line);
        _chunk.WriteByte(ToByteOperand(nameIdx, "global name"), line);
    }

    // -----------------------------------------------------------------------
    // Upvalue resolution helpers (Sprint 5 Increment D).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves <paramref name="name"/> as a category-4 capture (D-296) by walking
    /// the enclosing-compiler chain.  Returns the upvalue slot index in this
    /// compiler's <see cref="_upvalues"/> list, or −1 if the variable is not found
    /// in any enclosing function's local scope (i.e. it is a global or a
    /// categories-1/2/3 reference that falls through to <c>GetGlobal</c>).
    /// </summary>
    private int ResolveUpvalue(string name) {
        if (_enclosing is null) return -1;

        // Is the variable a direct local of the immediately enclosing function?
        int localSlot = _enclosing.FindLocalSlot(name);
        if (localSlot >= 0) {
            // Mark it captured so the enclosing scope closes the upvalue on exit.
            _enclosing.MarkLocalCaptured(localSlot);
            return AddUpvalue(isLocal: true, index: localSlot);
        }

        // Is it captured transitively by the enclosing closure?
        int enclosingUv = _enclosing.ResolveUpvalue(name);
        if (enclosingUv >= 0)
            return AddUpvalue(isLocal: false, index: enclosingUv);

        return -1;
    }

    /// <summary>
    /// Registers a new upvalue descriptor (isLocal, index) — or returns the
    /// existing slot if this descriptor was already recorded.  Deduplication
    /// ensures that two lambdas in the same enclosing function capturing the
    /// same variable share a single upvalue object at runtime.
    /// </summary>
    private int AddUpvalue(bool isLocal, int index) {
        for (int i = 0; i < _upvalues.Count; i++) {
            if (_upvalues[i].IsLocal == isLocal && _upvalues[i].Index == index)
                return i;
        }
        _upvalues.Add(new UpvalueDescriptor(isLocal, index));
        return _upvalues.Count - 1;
    }

    /// <summary>
    /// Evaluates a compile-time constant <paramref name="expr"/> to its
    /// <see cref="GrobValue"/>.  Called by <c>VisitConstDecl</c> to fold the
    /// RHS before any bytecode is emitted (D-289, D-293).
    /// </summary>
    /// <exception cref="GrobInternalException">
    /// Thrown when <paramref name="expr"/> is not a recognised compile-time
    /// constant form — indicating the type checker failed to reject a
    /// non-constant RHS.
    /// </exception>
    private GrobValue EvalConstantExpr(Expression expr) => expr switch {
        IntLiteralExpr e => GrobValue.FromInt(e.Value),
        FloatLiteralExpr e => GrobValue.FromFloat(e.Value),
        RawStringLiteralExpr e => GrobValue.FromString(e.Value),
        // Double-quoted strings without ${} interpolations are parsed as
        // InterpolatedStringExpr with all-text parts — fold them here.
        InterpolatedStringExpr istr when istr.Parts.All(p => p is StringTextPart)
                             => GrobValue.FromString(
                                    string.Concat(istr.Parts.OfType<StringTextPart>()
                                                         .Select(p => p.Text))),
        BoolLiteralExpr e => GrobValue.FromBool(e.Value),
        NilLiteralExpr => GrobValue.Nil,
        GroupingExpr g => EvalConstantExpr(g.Inner),
        IdentifierExpr id when id.Declaration is ConstDecl cd
                             => FetchCachedConst(cd, id.Name),
        // D-289: binary arithmetic, comparison and logical operators on
        // compile-time constant operands are allowed constant forms.
        BinaryExpr b => EvalBinaryConstant(b),
        // D-289: unary - and ! on compile-time constant operands.
        UnaryExpr u => EvalUnaryConstant(u),
        _ => ThrowNonConstantExpression(expr),
    };

    /// <summary>
    /// Folds a <see cref="BinaryExpr"/> whose operands are both compile-time
    /// constants. Handles the arithmetic, comparison and logical operators
    /// permitted by D-289.
    /// </summary>
    private GrobValue EvalBinaryConstant(BinaryExpr b) {
        GrobValue left = EvalConstantExpr(b.Left);
        GrobValue right = EvalConstantExpr(b.Right);

        return b.Operator switch {
            // Arithmetic
            BinaryOperator.Add when left.IsInt && right.IsInt
                => GrobValue.FromInt(checked(left.AsInt() + right.AsInt())),
            BinaryOperator.Add when left.IsFloat && right.IsFloat
                => GrobValue.FromFloat(left.AsFloat() + right.AsFloat()),
            BinaryOperator.Add when left.IsString && right.IsString
                => GrobValue.FromString(left.AsString() + right.AsString()),
            BinaryOperator.Subtract when left.IsInt && right.IsInt
                => GrobValue.FromInt(checked(left.AsInt() - right.AsInt())),
            BinaryOperator.Subtract when left.IsFloat && right.IsFloat
                => GrobValue.FromFloat(left.AsFloat() - right.AsFloat()),
            BinaryOperator.Multiply when left.IsInt && right.IsInt
                => GrobValue.FromInt(checked(left.AsInt() * right.AsInt())),
            BinaryOperator.Multiply when left.IsFloat && right.IsFloat
                => GrobValue.FromFloat(left.AsFloat() * right.AsFloat()),
            BinaryOperator.Divide when left.IsInt && right.IsInt
                => right.AsInt() == 0
                    ? throw new GrobInternalException("Division by zero in compile-time constant expression.")
                    : GrobValue.FromInt(checked(left.AsInt() / right.AsInt())),
            BinaryOperator.Divide when left.IsFloat && right.IsFloat
                => GrobValue.FromFloat(left.AsFloat() / right.AsFloat()),
            BinaryOperator.Modulo when left.IsInt && right.IsInt
                => right.AsInt() == 0
                    ? throw new GrobInternalException("Modulo by zero in compile-time constant expression.")
                    : GrobValue.FromInt(checked(left.AsInt() % right.AsInt())),
            BinaryOperator.Modulo when left.IsFloat && right.IsFloat
                => GrobValue.FromFloat(left.AsFloat() % right.AsFloat()),
            // Comparison — type-agnostic equality
            BinaryOperator.Equal => GrobValue.FromBool(left.Equals(right)),
            BinaryOperator.NotEqual => GrobValue.FromBool(!left.Equals(right)),
            BinaryOperator.Less when left.IsInt && right.IsInt
                => GrobValue.FromBool(left.AsInt() < right.AsInt()),
            BinaryOperator.Less when left.IsFloat && right.IsFloat
                => GrobValue.FromBool(left.AsFloat() < right.AsFloat()),
            BinaryOperator.Less when left.IsString && right.IsString
                => GrobValue.FromBool(string.CompareOrdinal(left.AsString(), right.AsString()) < 0),
            BinaryOperator.LessEqual when left.IsInt && right.IsInt
                => GrobValue.FromBool(left.AsInt() <= right.AsInt()),
            BinaryOperator.LessEqual when left.IsFloat && right.IsFloat
                => GrobValue.FromBool(left.AsFloat() <= right.AsFloat()),
            BinaryOperator.Greater when left.IsInt && right.IsInt
                => GrobValue.FromBool(left.AsInt() > right.AsInt()),
            BinaryOperator.Greater when left.IsFloat && right.IsFloat
                => GrobValue.FromBool(left.AsFloat() > right.AsFloat()),
            BinaryOperator.GreaterEqual when left.IsInt && right.IsInt
                => GrobValue.FromBool(left.AsInt() >= right.AsInt()),
            BinaryOperator.GreaterEqual when left.IsFloat && right.IsFloat
                => GrobValue.FromBool(left.AsFloat() >= right.AsFloat()),
            // Logical (no short-circuit needed for compile-time evaluation)
            BinaryOperator.And => GrobValue.FromBool(left.AsBool() && right.AsBool()),
            BinaryOperator.Or => GrobValue.FromBool(left.AsBool() || right.AsBool()),
            _ => ThrowNonConstantExpression(b),
        };
    }

    /// <summary>
    /// Folds a <see cref="UnaryExpr"/> whose operand is a compile-time constant.
    /// Handles unary <c>-</c> and <c>!</c> as permitted by D-289.
    /// </summary>
    private GrobValue EvalUnaryConstant(UnaryExpr u) {
        GrobValue operand = EvalConstantExpr(u.Operand);
        return u.Operator switch {
            UnaryOperator.Negate when operand.IsInt => GrobValue.FromInt(checked(-operand.AsInt())),
            UnaryOperator.Negate when operand.IsFloat => GrobValue.FromFloat(-operand.AsFloat()),
            UnaryOperator.Not => GrobValue.FromBool(!operand.AsBool()),
            _ => ThrowNonConstantExpression(u),
        };
    }

    /// <summary>
    /// Throws because a non-constant expression reached the constant folder; this
    /// indicates that the type checker failed to reject a non-constant RHS.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
        Justification = "Non-constant expressions are rejected by the type checker before emission.")]
    private static GrobValue ThrowNonConstantExpression(Expression expr) =>
        throw new GrobInternalException(
            $"Non-constant expression '{expr.GetType().Name}' in const declaration. "
          + "The type checker should have rejected this source.");

    // -----------------------------------------------------------------------
    // Forward-jump backpatch helpers (Sprint 3 Increment D — D-271).
    // Used by '?.' optional-chaining and reused by Sprint 4 (if/while/&&/||).
    //
    // Pattern:
    //   int site = EmitJump(OpCode.JumpIfTrue, line);   // writes opcode + 0xFFFF
    //   … emit skipped region …
    //   PatchJump(site);                                 // back-fills offset
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits <paramref name="opcode"/> followed by two placeholder bytes
    /// (<c>0xFF 0xFF</c>) and returns the chunk offset of the first placeholder
    /// byte. Call <see cref="PatchJump"/> once the jump target is known.
    /// </summary>
    /// <param name="opcode">A jump opcode — <see cref="OpCode.Jump"/>,
    /// <see cref="OpCode.JumpIfTrue"/> or <see cref="OpCode.JumpIfFalse"/>.</param>
    /// <param name="line">Source line attributed to the opcode byte.</param>
    /// <returns>The chunk offset of the first placeholder byte.</returns>
    internal int EmitJump(OpCode opcode, int line) {
        _chunk.WriteOpCode(opcode, line);
        int patchSite = _chunk.Count;
        _chunk.WriteByte(0xFF, line); // high byte placeholder
        _chunk.WriteByte(0xFF, line); // low byte placeholder
        return patchSite;
    }

    /// <summary>
    /// Fills in the two placeholder bytes written by <see cref="EmitJump"/>
    /// so that the jump skips forward to the current end of the chunk.
    /// </summary>
    /// <param name="patchSite">
    /// The value returned by the matching <see cref="EmitJump"/> call.
    /// </param>
    /// <exception cref="GrobInternalException">
    /// Thrown when the distance from <paramref name="patchSite"/> to the current
    /// end of the chunk overflows a 16-bit unsigned offset. This can only happen
    /// when a single expression emits more than 65 535 bytes, which is not
    /// reachable by any valid Grob expression in practice.
    /// </exception>
    internal void PatchJump(int patchSite) {
        // The offset counts bytes from the first instruction AFTER the two
        // placeholder bytes (i.e. from patchSite + 2) to the current end.
        int offset = _chunk.Count - (patchSite + 2);
        if ((uint)offset > ushort.MaxValue)
            throw new GrobInternalException(
                $"Jump offset {offset} overflows the 16-bit limit. The expression is too large.");
        _chunk.PatchByte(patchSite, (byte)(offset >> 8));
        _chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    /// <summary>
    /// Emits a <see cref="OpCode.Loop"/> instruction (backward jump) that sends
    /// the VM's instruction pointer back to <paramref name="loopStart"/>.
    /// </summary>
    /// <remarks>
    /// Unlike forward jumps, the offset is computed immediately and written in-line
    /// — no backpatching is needed because the loop-top position is always known
    /// before the body is compiled.
    /// <para>
    /// Offset formula: <c>(chunk.Count + 2) − loopStart</c> (computed after the
    /// opcode byte, before the two offset bytes).  The VM reads the two bytes
    /// advancing its instruction pointer to <c>loopEnd</c>, then subtracts the
    /// offset: <c>ip = loopEnd − offset = loopStart</c>.
    /// </para>
    /// </remarks>
    /// <param name="loopStart">
    /// Chunk offset of the first byte of the loop's condition — recorded before
    /// any condition or body code is emitted.
    /// </param>
    /// <param name="line">Source line attributed to the opcode byte.</param>
    internal void EmitLoop(int loopStart, int line) {
        _chunk.WriteOpCode(OpCode.Loop, line);
        // chunk.Count is now at the high-byte position; adding 2 gives loopEnd.
        int offset = (_chunk.Count + 2) - loopStart;
        if ((uint)offset > ushort.MaxValue)
            throw new GrobInternalException(
                $"Loop offset {offset} overflows the 16-bit limit. The loop body is too large.");
        _chunk.WriteByte((byte)(offset >> 8), line);
        _chunk.WriteByte((byte)(offset & 0xFF), line);
    }
}
