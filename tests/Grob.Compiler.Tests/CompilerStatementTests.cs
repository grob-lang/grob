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

    // -----------------------------------------------------------------------
    // const — binary and unary constant folding (EvalBinaryConstant /
    // EvalUnaryConstant — D-289, added in QA pass fix)
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_IntAddInt_FoldsToSum() {
        // EvalBinaryConstant — Add int+int arm.
        Chunk chunk = CompileSource("""
            const X := 1 + 2
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 3L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 3 in pool.");
    }

    [Fact]
    public void ConstDecl_IntSubtractInt_FoldsToResult() {
        Chunk chunk = CompileSource("""
            const X := 10 - 4
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 6L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 6 in pool.");
    }

    [Fact]
    public void ConstDecl_IntMultiplyInt_FoldsToProduct() {
        Chunk chunk = CompileSource("""
            const X := 3 * 7
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 21L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 21 in pool.");
    }

    [Fact]
    public void ConstDecl_IntDivideInt_FoldsToQuotient() {
        Chunk chunk = CompileSource("""
            const X := 10 / 2
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 5L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 5 in pool.");
    }

    [Fact]
    public void ConstDecl_IntModuloInt_FoldsToRemainder() {
        Chunk chunk = CompileSource("""
            const X := 10 % 3
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 1L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 1 in pool.");
    }

    [Fact]
    public void ConstDecl_StringConcat_FoldsToJoinedString() {
        // Add string+string arm.
        Chunk chunk = CompileSource("""
            const X := "hello" + " world"
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsString && chunk.ReadConstant(i).AsString() == "hello world") { found = true; break; }
        }
        Assert.True(found, "Expected folded constant \"hello world\" in pool.");
    }

    [Fact]
    public void ConstDecl_UnaryNegateInt_FoldsToNegative() {
        // EvalUnaryConstant — Negate int arm.
        Chunk chunk = CompileSource("""
            const X := -5
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == -5L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant -5 in pool.");
    }

    [Fact]
    public void ConstDecl_UnaryNotBool_FoldsToInverse() {
        // EvalUnaryConstant — Not arm.
        Chunk chunk = CompileSource("""
            const X := !true
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsBool && !chunk.ReadConstant(i).AsBool()) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant false in pool.");
    }

    [Fact]
    public void ConstDecl_FloatAddFloat_FoldsToSum() {
        // EvalBinaryConstant — Add float+float arm.
        Chunk chunk = CompileSource("""
            const X := 1.5 + 2.5
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsFloat && chunk.ReadConstant(i).AsFloat() == 4.0) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 4.0 in pool.");
    }

    [Fact]
    public void ConstDecl_CompositeExpr_FoldsCorrectly() {
        // Compound expression: (3 + 4) * 2 = 14.
        Chunk chunk = CompileSource("""
            const X := (3 + 4) * 2
            print(X)
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 14L) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 14 in pool.");
    }

    // -----------------------------------------------------------------------
    // exit() — compiled as OpCode.Exit, not Pop (QA pass fix)
    // -----------------------------------------------------------------------

    [Fact]
    public void ExitCall_EmitsExitOpcode() {
        Chunk chunk = CompileSource("exit(0)");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.Exit, ops);
        Assert.DoesNotContain(OpCode.Pop, ops);
    }

    [Fact]
    public void ExitCallNoArg_EmitsExitOpcodeWithZeroConstant() {
        Chunk chunk = CompileSource("exit()");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.Exit, ops);
        // exit() with no argument emits constant 0 then Exit.
        bool hasZero = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsInt && chunk.ReadConstant(i).AsInt() == 0L) { hasZero = true; break; }
        }
        Assert.True(hasZero, "Expected constant 0 in pool for no-arg exit().");
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

    [Fact]
    public void ConstDecl_NilLiteral_EmitsNoGetGlobal() {
        // NilLiteralExpr arm of EvalConstantExpr.
        Chunk chunk = CompileSource("const N := nil");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.DefineGlobal, ops);
    }
}
