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
    private readonly DiagnosticBag _diagnostics;

    private Compiler(DiagnosticBag diagnostics) {
        _diagnostics = diagnostics;
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
        var compiler = new Compiler(diagnostics);
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
}
