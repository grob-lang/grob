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
    private sealed record LocalVar(string Name, int Slot);
    private readonly Stack<List<LocalVar>> _localScopes = new();
    private int _nextSlot;   // next available stack slot for a new local

    // -----------------------------------------------------------------------
    // Const value table (Sprint 3C).
    // Maps each ConstDecl node to its compile-time-evaluated GrobValue.
    // Every reference to a const identifier is inlined as a direct Constant
    // load rather than going through the global/local slot machinery (D-293).
    // -----------------------------------------------------------------------
    private readonly Dictionary<ConstDecl, GrobValue> _constValues = [];

    private bool IsGlobalScope => _localScopes.Count == 0;

    private Compiler() { }

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
    public override object? VisitCompilationUnit(CompilationUnit node) {
        foreach (AstNode item in node.TopLevel) {
            Visit(item);
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
    /// Emits a load instruction for the variable <paramref name="name"/>:
    /// <see cref="OpCode.GetLocal"/> when the name resolves to a local slot,
    /// <see cref="OpCode.GetGlobal"/> otherwise.
    /// </summary>
    private void EmitLoad(string name, int line) {
        int slot = FindLocalSlot(name);
        if (slot >= 0) {
            _chunk.WriteOpCode(OpCode.GetLocal, line);
            _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
        } else {
            int nameIdx = GetOrCreateGlobalNameIndex(name);
            _chunk.WriteOpCode(OpCode.GetGlobal, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, "global name"), line);
        }
    }

    /// <summary>
    /// Emits a store instruction for the variable <paramref name="name"/>:
    /// <see cref="OpCode.SetLocal"/> when the name resolves to a local slot,
    /// <see cref="OpCode.SetGlobal"/> otherwise.
    /// The value to store must already be on the top of the stack.
    /// </summary>
    private void EmitStore(string name, int line) {
        int slot = FindLocalSlot(name);
        if (slot >= 0) {
            _chunk.WriteOpCode(OpCode.SetLocal, line);
            _chunk.WriteByte(ToByteOperand(slot, "local slot"), line);
        } else {
            int nameIdx = GetOrCreateGlobalNameIndex(name);
            _chunk.WriteOpCode(OpCode.SetGlobal, line);
            _chunk.WriteByte(ToByteOperand(nameIdx, "global name"), line);
        }
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
}
