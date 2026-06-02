using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-assertion tests for statement and declaration compilation:
/// <c>readonly</c> and <c>const</c> bindings (Sprint 3 Increment C).
/// </summary>
public sealed class CompilerStatementTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    /// <summary>
    /// Reads every (opcode, operand-bytes) pair from the chunk until (and
    /// including) the first <see cref="OpCode.Return"/>.
    /// </summary>
    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            result.Add(op);
            // advance past operand bytes for opcodes we know about
            switch (op) {
                case OpCode.Constant:
                    offset += 1;
                    break;
                case OpCode.ConstantLong:
                    offset += 2;
                    break;
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.PopN:
                    offset += 1; // 1-byte operand
                    break;
            }
            if (op == OpCode.Return) break;
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // readonly — global scope
    // -----------------------------------------------------------------------

    [Fact]
    public void ReadonlyDecl_Global_EmitsDefineGlobal() {
        // `readonly x := 5` should emit: Constant [5], DefineGlobal [idx], Return
        Chunk chunk = CompileSource("readonly x := 5");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.DefineGlobal, OpCode.Return], ops);
    }

    [Fact]
    public void ReadonlyDecl_GlobalThenRead_EmitsGetGlobal() {
        // Reading a readonly global emits GetGlobal — same as a mutable global.
        Chunk chunk = CompileSource("""
            readonly x := 5
            print(x)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.DefineGlobal, ops);
        Assert.Contains(OpCode.GetGlobal, ops);
    }

    // -----------------------------------------------------------------------
    // const — global scope: no DefineGlobal, references are direct Constant loads
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_Global_EmitsNoDefineGlobal() {
        // `const MAX := 100` alone emits nothing except Return.
        // The const declaration itself must not produce a DefineGlobal.
        Chunk chunk = CompileSource("const MAX := 100");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.DefineGlobal, ops);
    }

    [Fact]
    public void ConstDecl_GlobalIntReference_EmitsConstantNotGetGlobal() {
        // Reading a const must emit Constant directly — no GetGlobal.
        Chunk chunk = CompileSource("""
            const MAX := 100
            print(MAX)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        // The constant pool should contain the inlined value.
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 100L) {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected constant value 100 in the constant pool.");
    }

    [Fact]
    public void ConstDecl_GlobalStringReference_EmitsConstantNotGetGlobal() {
        Chunk chunk = CompileSource("""
            const LABEL := "grob"
            print(LABEL)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
    }

    [Fact]
    public void ConstDecl_BoolTrue_InlinesConstantNotGetGlobal() {
        // BoolLiteralExpr arm of EvalConstantExpr.
        Chunk chunk = CompileSource("""
            const FLAG := true
            print(FLAG)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsBool && chunk.ReadConstant(i).AsBool()) {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected bool constant true in the constant pool.");
    }

    [Fact]
    public void ConstDecl_GroupedInt_InlinesValue() {
        // GroupingExpr arm of EvalConstantExpr — wrapping parens are transparent.
        Chunk chunk = CompileSource("""
            const X := (42)
            print(X)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 42L) {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected constant value 42 in the constant pool.");
    }

    [Fact]
    public void ConstDecl_ChainedConst_InlinesFromCache() {
        // IdentifierExpr -> ConstDecl arm: const B := A where A is already cached.
        Chunk chunk = CompileSource("""
            const A := 10
            const B := A
            print(B)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 10L) {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected constant value 10 in the constant pool.");
    }

    [Fact]
    public void ConstDecl_RawString_EmitsNoGetGlobal() {
        // RawStringLiteralExpr arm of EvalConstantExpr.
        Chunk chunk = CompileSource("""
            const S := `hello`
            print(S)
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
    }
}
