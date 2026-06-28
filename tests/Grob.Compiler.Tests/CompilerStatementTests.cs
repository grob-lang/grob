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

    // -----------------------------------------------------------------------
    // EvalBinaryConstant — float arithmetic arms
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_FloatSubtractFloat_FoldsToResult() {
        Chunk chunk = CompileSource("""
            const X := 3.0 - 1.5
            print(X)
            """);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsFloat && chunk.ReadConstant(i).AsFloat() == 1.5) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 1.5 in pool.");
    }

    [Fact]
    public void ConstDecl_FloatMultiplyFloat_FoldsToProduct() {
        Chunk chunk = CompileSource("""
            const X := 2.0 * 3.0
            print(X)
            """);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsFloat && chunk.ReadConstant(i).AsFloat() == 6.0) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 6.0 in pool.");
    }

    [Fact]
    public void ConstDecl_FloatDivideFloat_FoldsToQuotient() {
        Chunk chunk = CompileSource("""
            const X := 9.0 / 3.0
            print(X)
            """);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsFloat && chunk.ReadConstant(i).AsFloat() == 3.0) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 3.0 in pool.");
    }

    [Fact]
    public void ConstDecl_FloatModuloFloat_FoldsToRemainder() {
        Chunk chunk = CompileSource("""
            const X := 5.0 % 2.0
            print(X)
            """);
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsFloat && chunk.ReadConstant(i).AsFloat() == 1.0) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant 1.0 in pool.");
    }

    // -----------------------------------------------------------------------
    // EvalBinaryConstant — comparison arms (produce bool constants)
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_IntEqual_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 1 == 1
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_IntNotEqual_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 1 != 2
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_IntLess_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 1 < 2
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_FloatLess_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 1.0 < 2.0
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_StringLess_FoldsToBool() {
        // Less/string arm — string.CompareOrdinal
        Chunk chunk = CompileSource("""
            const X := "a" < "b"
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_IntLessEqual_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 1 <= 1
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_FloatLessEqual_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 1.0 <= 2.0
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_IntGreater_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 2 > 1
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_FloatGreater_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 2.0 > 1.0
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_IntGreaterEqual_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 2 >= 2
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    [Fact]
    public void ConstDecl_FloatGreaterEqual_FoldsToBool() {
        Chunk chunk = CompileSource("""
            const X := 2.0 >= 1.0
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    // -----------------------------------------------------------------------
    // EvalBinaryConstant — logical arms
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_And_FoldsToFalse() {
        // And arm — true && false = false
        Chunk chunk = CompileSource("""
            const X := true && false
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsBool && !chunk.ReadConstant(i).AsBool()) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant false in pool.");
    }

    [Fact]
    public void ConstDecl_Or_FoldsToTrue() {
        // Or arm — false || true = true
        Chunk chunk = CompileSource("""
            const X := false || true
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
    }

    // -----------------------------------------------------------------------
    // EvalBinaryConstant — division/modulo by zero throw paths
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_IntDivideByZero_ThrowsGrobInternalException() {
        // The zero-divisor guard in EvalBinaryConstant must throw before C#'s
        // DivideByZeroException can surface.
        Assert.Throws<GrobInternalException>(() => CompileSource("const X := 1 / 0"));
    }

    [Fact]
    public void ConstDecl_IntModuloByZero_ThrowsGrobInternalException() {
        Assert.Throws<GrobInternalException>(() => CompileSource("const X := 1 % 0"));
    }

    // -----------------------------------------------------------------------
    // EvalUnaryConstant — negate float arm
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstDecl_UnaryNegateFloat_FoldsToNegative() {
        // UnaryOperator.Negate when operand.IsFloat arm.
        Chunk chunk = CompileSource("""
            const X := -3.14
            print(X)
            """);
        Assert.DoesNotContain(OpCode.GetGlobal, ReadOpcodes(chunk));
        bool found = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            if (chunk.ReadConstant(i).IsFloat && chunk.ReadConstant(i).AsFloat() == -3.14) { found = true; break; }
        }
        Assert.True(found, "Expected folded constant -3.14 in pool.");
    }

    // -----------------------------------------------------------------------
    // Assignment — SetGlobal / SetLocal emission
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_Global_EmitsSetGlobal() {
        Chunk chunk = CompileSource("""
            x := 0
            x = 5
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.SetGlobal, ops);
    }

    [Fact]
    public void Assignment_Local_EmitsSetLocal() {
        // Assignment inside a function body targets a local slot.
        Chunk chunk = CompileSource("""
            fn f(): int {
            x := 0
            x = 5
            }
            """);

        // Extract the function's chunk from the constant pool.
        BytecodeFunction fn = ExtractSingleFunction(chunk);
        List<OpCode> bodyOps = ReadFunctionOpcodes(fn.Bytecode);
        Assert.Contains(OpCode.SetLocal, bodyOps);
        Assert.DoesNotContain(OpCode.SetGlobal, bodyOps);
    }

    // -----------------------------------------------------------------------
    // Compound assignment — EmitCompoundBinaryOpCode all arms
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("x := 5\nx += 3", OpCode.AddInt)]
    [InlineData("x := 1.0\nx += 2.0", OpCode.AddFloat)]
    [InlineData("x := \"a\"\nx += \"b\"", OpCode.Concat)]
    [InlineData("x := 10\nx -= 3", OpCode.SubtractInt)]
    [InlineData("x := 5\nx *= 2", OpCode.MultiplyInt)]
    [InlineData("x := 10\nx /= 2", OpCode.DivideInt)]
    [InlineData("x := 7\nx %= 3", OpCode.ModuloInt)]
    [InlineData("x := 5.0\nx -= 1.0", OpCode.SubtractFloat)]
    [InlineData("x := 2.0\nx *= 3.0", OpCode.MultiplyFloat)]
    [InlineData("x := 9.0\nx /= 3.0", OpCode.DivideFloat)]
    [InlineData("x := 5.0\nx %= 2.0", OpCode.ModuloFloat)]
    public void CompoundAssignment_EmitsExpectedOpcode(string source, OpCode expectedOp) {
        Chunk chunk = CompileSource(source);
        Assert.Contains(expectedOp, ReadOpcodes(chunk));
    }

    [Fact]
    public void CompoundAssignment_IntPlusFloatRhs_EmitsIntToFloatThenAddFloat() {
        // LHS int, RHS float — the LHS is coerced to float before addition.
        Chunk chunk = CompileSource("""
            x := 1.0
            x += 2
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.IntToFloat, ops);
        Assert.Contains(OpCode.AddFloat, ops);
    }

    // -----------------------------------------------------------------------
    // Increment / decrement — IncrementInt/DecrementInt (local) and
    // GetGlobal+Add/Sub+SetGlobal (global)
    // -----------------------------------------------------------------------

    [Fact]
    public void Increment_Global_EmitsGetGlobalAddIntSetGlobal() {
        Chunk chunk = CompileSource("""
            x := 0
            x++
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.GetGlobal, ops);
        Assert.Contains(OpCode.AddInt, ops);
        Assert.Contains(OpCode.SetGlobal, ops);
    }

    [Fact]
    public void Decrement_Global_EmitsGetGlobalSubtractIntSetGlobal() {
        Chunk chunk = CompileSource("""
            x := 10
            x--
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.GetGlobal, ops);
        Assert.Contains(OpCode.SubtractInt, ops);
        Assert.Contains(OpCode.SetGlobal, ops);
    }

    [Fact]
    public void Increment_Local_EmitsIncrementIntWithSlot() {
        // Inside a function body, x++ must use the IncrementInt slot opcode.
        Chunk chunk = CompileSource("""
            fn f(): int {
            x := 0
            x++
            }
            """);
        BytecodeFunction fn = ExtractSingleFunction(chunk);
        List<OpCode> bodyOps = ReadFunctionOpcodes(fn.Bytecode);
        Assert.Contains(OpCode.IncrementInt, bodyOps);
        Assert.DoesNotContain(OpCode.GetGlobal, bodyOps);
    }

    [Fact]
    public void Decrement_Local_EmitsDecrementIntWithSlot() {
        Chunk chunk = CompileSource("""
            fn f(): int {
            x := 5
            x--
            }
            """);
        BytecodeFunction fn = ExtractSingleFunction(chunk);
        List<OpCode> bodyOps = ReadFunctionOpcodes(fn.Bytecode);
        Assert.Contains(OpCode.DecrementInt, bodyOps);
    }

    [Fact]
    public void Increment_FloatTarget_RaisesE0002() {
        // float++ is a type error; the type checker must reject it with E0002.
        // Compilation must not be attempted on an error-typed programme.
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan("x := 1.0\nx++", bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Helpers for function-body extraction
    // -----------------------------------------------------------------------

    private static BytecodeFunction ExtractSingleFunction(Chunk scriptChunk) {
        BytecodeFunction? found = null;
        for (int i = 0; i < scriptChunk.ConstantCount; i++) {
            GrobValue v = scriptChunk.ReadConstant(i);
            if (!v.IsFunction || v.AsFunction() is not BytecodeFunction fn)
                continue;
            if (found is not null)
                throw new InvalidOperationException("Expected exactly one BytecodeFunction in constant pool.");
            found = fn;
        }
        return found ?? throw new InvalidOperationException("No BytecodeFunction found in constant pool.");
    }

    private static List<OpCode> ReadFunctionOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            result.Add(op);
            switch (op) {
                case OpCode.Constant:
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.PopN:
                case OpCode.IncrementInt:
                case OpCode.DecrementInt:
                case OpCode.Call:
                case OpCode.NewArray:
                case OpCode.BuildString:
                    offset += 1;
                    break;
                case OpCode.ConstantLong:
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    offset += 2;
                    break;
            }
        }
        return result;
    }
}
