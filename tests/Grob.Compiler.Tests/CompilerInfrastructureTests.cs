using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;
using static Grob.Compiler.Tests.AstTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Infrastructure tests for the Sprint 2 Compiler:
/// null guards, ConstantLong emission, error-node graceful skipping,
/// block statement emission, and the BuiltinDecl→DefaultVisit path.
/// </summary>
public sealed class CompilerInfrastructureTests {
    // -----------------------------------------------------------------------
    // Null guard — Compile() contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Compile_NullUnit_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => GrobCompiler.Compile(null!, new DiagnosticBag()));
    }

    [Fact]
    public void Compile_NullDiagnostics_ThrowsArgumentNullException() {
        var unit = new CompilationUnit(R, []);
        Assert.Throws<ArgumentNullException>(() => GrobCompiler.Compile(unit, null!));
    }

    // -----------------------------------------------------------------------
    // ConstantLong — constant pool index > 255
    // -----------------------------------------------------------------------

    [Fact]
    public void EmitConstant_IndexAbove255_WritesConstantLong() {
        // 257 distinct integer literals (0 .. 256) force constant index 256,
        // which exceeds byte.MaxValue and must use the ConstantLong encoding.
        string source = string.Join("\n", Enumerable.Range(0, 257).Select(i => i.ToString()));
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors);
        Chunk chunk = GrobCompiler.Compile(unit, bag);

        Assert.Equal(257, chunk.ConstantCount);
        bool hasConstantLong = false;
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            if (op == OpCode.ConstantLong) { hasConstantLong = true; offset += 2; } else if (op == OpCode.Constant) { offset += 1; } else if (op == OpCode.Return) break;
        }
        Assert.True(hasConstantLong, "Expected at least one ConstantLong for constant index > 255.");
    }

    // -----------------------------------------------------------------------
    // Error-node graceful skip — compiler must not throw on error AST nodes
    // -----------------------------------------------------------------------

    [Fact]
    public void VisitErrorExpr_InUnit_CompilesSilently() {
        // ExpressionStmt whose expression is an ErrorExpr — e.g. a parse failure
        // in an expression position. The compiler must skip it without throwing.
        var unit = new CompilationUnit(R, [new ExpressionStmt(R, new ErrorExpr(R, ErrDiag()))]);
        Chunk chunk = GrobCompiler.Compile(unit, new DiagnosticBag());
        Assert.NotNull(chunk);
    }

    [Fact]
    public void VisitErrorStmt_InUnit_CompilesSilently() {
        var unit = new CompilationUnit(R, [new ErrorStmt(R, ErrDiag())]);
        Chunk chunk = GrobCompiler.Compile(unit, new DiagnosticBag());
        Assert.NotNull(chunk);
    }

    [Fact]
    public void VisitErrorDecl_InUnit_CompilesSilently() {
        var unit = new CompilationUnit(R, [new ErrorDecl(R, ErrDiag())]);
        Chunk chunk = GrobCompiler.Compile(unit, new DiagnosticBag());
        Assert.NotNull(chunk);
    }

    // -----------------------------------------------------------------------
    // Block statement
    // -----------------------------------------------------------------------

    [Fact]
    public void VisitBlock_WithIntStatement_EmitsContainedInstruction() {
        // BlockStmt containing a single integer literal expression.
        // The compiler must recurse into the block and emit the inner constant.
        var inner = new ExpressionStmt(R, new IntLiteralExpr(R, 99L));
        var block = new BlockStmt(R, [inner]);
        var unit = new CompilationUnit(R, [block]);
        Chunk chunk = GrobCompiler.Compile(unit, new DiagnosticBag());
        Assert.Equal(OpCode.Constant, (OpCode)chunk.ReadByte(0));
        Assert.Equal(99L, chunk.ReadConstant(0).AsInt());
    }

    // -----------------------------------------------------------------------
    // BuiltinDecl → DefaultVisit path (covers AstVisitor.VisitBuiltinDecl
    // virtual body and ensures Compiler.DefaultVisit handles it silently)
    // -----------------------------------------------------------------------

    [Fact]
    public void VisitBuiltinDecl_InUnit_CompilesSilently() {
        // BuiltinDecl nodes are never produced by the parser; they live as the
        // DeclarationNode of pre-registered built-in symbols. If one were
        // somehow placed in TopLevel the compiler must skip it via DefaultVisit.
        var unit = new CompilationUnit(R, [new BuiltinDecl("print")]);
        Chunk chunk = GrobCompiler.Compile(unit, new DiagnosticBag());
        Assert.NotNull(chunk);
    }
}
