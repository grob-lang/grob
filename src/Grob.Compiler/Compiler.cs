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
            _chunk.WriteByte((byte)slot, line);
        } else {
            int nameIdx = GetOrCreateGlobalNameIndex(name);
            _chunk.WriteOpCode(OpCode.GetGlobal, line);
            _chunk.WriteByte((byte)nameIdx, line);
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
            _chunk.WriteByte((byte)slot, line);
        } else {
            int nameIdx = GetOrCreateGlobalNameIndex(name);
            _chunk.WriteOpCode(OpCode.SetGlobal, line);
            _chunk.WriteByte((byte)nameIdx, line);
        }
    }
}
