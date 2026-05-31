using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-assertion tests for the Sprint 2 Increment D compiler.
/// Each test verifies that a source snippet produces the exact sequence of
/// opcodes and constant-pool entries that the VM's dispatch loop expects.
/// </summary>
/// <remarks>
/// Chunks are compiled from source via the full front end
/// (Lexer → Parser → TypeChecker → Compiler) so that the tests exercise the
/// real pipeline rather than hand-built ASTs.
/// </remarks>
public sealed class CompilerExpressionTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Compiles <paramref name="source"/> and returns the resulting chunk.</summary>
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors, $"TypeChecker produced errors: {string.Join("; ", bag.Errors)}");
        return GrobCompiler.Compile(unit, bag);
    }

    /// <summary>
    /// Reads the opcode sequence from <paramref name="chunk"/> up to and
    /// including the first <see cref="OpCode.Return"/> and returns it.
    /// </summary>
    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            result.Add(op);
            offset++;
            switch (op) {
                case OpCode.Constant:
                    offset += 1; // 1-byte index
                    break;
                case OpCode.ConstantLong:
                    offset += 2; // 2-byte big-endian index
                    break;
                default:
                    break;
            }
            if (op == OpCode.Return) break;
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Integer literals
    // -----------------------------------------------------------------------

    [Fact]
    public void IntLiteral_EmitsConstantThenReturn() {
        Chunk chunk = CompileSource("42");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Return], ops);
        Assert.Equal(1, chunk.ConstantCount);
        Assert.Equal(42L, chunk.ReadConstant(0).AsInt());
    }

    [Fact]
    public void FloatLiteral_EmitsConstantThenReturn() {
        Chunk chunk = CompileSource("3.14");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Return], ops);
        Assert.Equal(3.14, chunk.ReadConstant(0).AsFloat());
    }

    [Fact]
    public void StringLiteral_EmitsConstantThenReturn() {
        Chunk chunk = CompileSource("\"hello\"");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Return], ops);
        Assert.Equal("hello", chunk.ReadConstant(0).AsString());
    }

    // -----------------------------------------------------------------------
    // Integer arithmetic
    // -----------------------------------------------------------------------

    [Fact]
    public void BinaryAddInt_EmitsConstantsAndAddInt() {
        Chunk chunk = CompileSource("2 + 3");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.AddInt, OpCode.Return], ops);
        Assert.Equal(2L, chunk.ReadConstant(0).AsInt());
        Assert.Equal(3L, chunk.ReadConstant(1).AsInt());
    }

    [Fact]
    public void BinarySubtractInt_EmitsSubtractInt() {
        Chunk chunk = CompileSource("10 - 4");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.SubtractInt, OpCode.Return], ops);
    }

    [Fact]
    public void BinaryMultiplyInt_EmitsMultiplyInt() {
        Chunk chunk = CompileSource("6 * 7");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.MultiplyInt, OpCode.Return], ops);
    }

    [Fact]
    public void BinaryDivideInt_EmitsDivideInt() {
        Chunk chunk = CompileSource("10 / 2");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.DivideInt, OpCode.Return], ops);
    }

    [Fact]
    public void BinaryModuloInt_EmitsModuloInt() {
        Chunk chunk = CompileSource("7 % 3");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.ModuloInt, OpCode.Return], ops);
    }

    // -----------------------------------------------------------------------
    // Operator precedence
    // -----------------------------------------------------------------------

    [Fact]
    public void OperatorPrecedence_TwoPlusThreeTimesFour_PostfixIsMultiplyFirst() {
        // 2 + 3 * 4 → push 2, push 3, push 4, MultiplyInt, AddInt
        Chunk chunk = CompileSource("2 + 3 * 4");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.MultiplyInt, OpCode.AddInt, OpCode.Return],
            ops);
        Assert.Equal(2L, chunk.ReadConstant(0).AsInt());
        Assert.Equal(3L, chunk.ReadConstant(1).AsInt());
        Assert.Equal(4L, chunk.ReadConstant(2).AsInt());
    }

    // -----------------------------------------------------------------------
    // Float arithmetic
    // -----------------------------------------------------------------------

    [Fact]
    public void BinaryAddFloat_EmitsAddFloat() {
        Chunk chunk = CompileSource("2.0 + 3.0");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.AddFloat, OpCode.Return], ops);
    }

    [Fact]
    public void BinarySubtractFloat_EmitsSubtractFloat() {
        Chunk chunk = CompileSource("5.5 - 1.5");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.SubtractFloat, OpCode.Return], ops);
    }

    // -----------------------------------------------------------------------
    // Mixed-type promotion (int + float → IntToFloat + float op)
    // -----------------------------------------------------------------------

    [Fact]
    public void MixedArithmetic_IntPlusFloat_EmitsIntToFloatOnLeft() {
        // 2 + 3.0 → Constant(2), IntToFloat, Constant(3.0), AddFloat
        Chunk chunk = CompileSource("2 + 3.0");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.AddFloat, OpCode.Return],
            ops);
    }

    [Fact]
    public void MixedArithmetic_FloatPlusInt_EmitsIntToFloatOnRight() {
        // 2.0 + 3 → Constant(2.0), Constant(3), IntToFloat, AddFloat
        Chunk chunk = CompileSource("2.0 + 3");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.AddFloat, OpCode.Return],
            ops);
    }

    // -----------------------------------------------------------------------
    // Unary negation
    // -----------------------------------------------------------------------

    [Fact]
    public void UnaryNegateInt_EmitsConstantAndNegateInt() {
        Chunk chunk = CompileSource("-5");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.NegateInt, OpCode.Return], ops);
        Assert.Equal(5L, chunk.ReadConstant(0).AsInt());
    }

    [Fact]
    public void UnaryNegateFloat_EmitsConstantAndNegateFloat() {
        Chunk chunk = CompileSource("-1.5");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.NegateFloat, OpCode.Return], ops);
    }

    // -----------------------------------------------------------------------
    // String concatenation
    // -----------------------------------------------------------------------

    [Fact]
    public void StringConcat_EmitsConstantsAndConcat() {
        Chunk chunk = CompileSource("\"hello\" + \" world\"");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.Concat, OpCode.Return], ops);
        Assert.Equal("hello", chunk.ReadConstant(0).AsString());
        Assert.Equal(" world", chunk.ReadConstant(1).AsString());
    }

    // -----------------------------------------------------------------------
    // print() built-in — emits arg then Print opcode
    // -----------------------------------------------------------------------

    [Fact]
    public void PrintCall_EmitsArgThenPrintOpcode() {
        Chunk chunk = CompileSource("print(42)");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.Constant, OpCode.Print, OpCode.Return], ops);
        Assert.Equal(42L, chunk.ReadConstant(0).AsInt());
    }

    [Fact]
    public void PrintCall_WithBinaryArg_EmitsArgExprThenPrint() {
        // print(2 + 3 * 4)
        Chunk chunk = CompileSource("print(2 + 3 * 4)");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.MultiplyInt, OpCode.AddInt, OpCode.Print, OpCode.Return],
            ops);
    }

    // -----------------------------------------------------------------------
    // Line-number metadata
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // Bool and nil literals
    // -----------------------------------------------------------------------

    [Fact]
    public void BoolLiteral_True_EmitsTrueOpcode() {
        Chunk chunk = CompileSource("true");
        Assert.Equal([OpCode.True, OpCode.Return], ReadOpcodes(chunk));
    }

    [Fact]
    public void BoolLiteral_False_EmitsFalseOpcode() {
        Chunk chunk = CompileSource("false");
        Assert.Equal([OpCode.False, OpCode.Return], ReadOpcodes(chunk));
    }

    [Fact]
    public void NilLiteral_EmitsNilOpcode() {
        Chunk chunk = CompileSource("nil");
        Assert.Equal([OpCode.Nil, OpCode.Return], ReadOpcodes(chunk));
    }

    // -----------------------------------------------------------------------
    // Raw string literal
    // -----------------------------------------------------------------------

    [Fact]
    public void RawStringLiteral_EmitsConstantWithValue() {
        Chunk chunk = CompileSource("`hello`");
        Assert.Equal([OpCode.Constant, OpCode.Return], ReadOpcodes(chunk));
        Assert.Equal("hello", chunk.ReadConstant(0).AsString());
    }

    // -----------------------------------------------------------------------
    // Grouping
    // -----------------------------------------------------------------------

    [Fact]
    public void Grouping_EmitsSameOpcodeAsInner() {
        Chunk chunk = CompileSource("(42)");
        Assert.Equal([OpCode.Constant, OpCode.Return], ReadOpcodes(chunk));
        Assert.Equal(42L, chunk.ReadConstant(0).AsInt());
    }

    // -----------------------------------------------------------------------
    // Remaining float arithmetic (Multiply, Divide, Modulo)
    // -----------------------------------------------------------------------

    [Fact]
    public void BinaryMultiplyFloat_EmitsMultiplyFloat() {
        Chunk chunk = CompileSource("2.0 * 3.0");
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.MultiplyFloat, OpCode.Return], ReadOpcodes(chunk));
    }

    [Fact]
    public void BinaryDivideFloat_EmitsDivideFloat() {
        Chunk chunk = CompileSource("4.0 / 2.0");
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.DivideFloat, OpCode.Return], ReadOpcodes(chunk));
    }

    [Fact]
    public void BinaryModuloFloat_EmitsModuloFloat() {
        Chunk chunk = CompileSource("5.0 % 2.0");
        Assert.Equal([OpCode.Constant, OpCode.Constant, OpCode.ModuloFloat, OpCode.Return], ReadOpcodes(chunk));
    }

    // -----------------------------------------------------------------------
    // Line-number metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void LineNumbers_MatchSourceLine() {
        // Source has two print statements: line 1 and line 2.
        string source = "print(1)\nprint(2)";
        Chunk chunk = CompileSource(source);

        // Find the offsets of the two Print opcodes.
        var printOffsets = new List<int>();
        int offset = 0;
        while (offset < chunk.Count) {
            byte b = chunk.ReadByte(offset);
            if ((OpCode)b == OpCode.Print) {
                printOffsets.Add(offset);
            }
            offset++;
            if ((OpCode)b == OpCode.Constant) offset++;   // skip index byte
            if ((OpCode)b == OpCode.ConstantLong) offset += 2;
        }

        Assert.Equal(2, printOffsets.Count);
        Assert.Equal(1, chunk.GetLine(printOffsets[0]));
        Assert.Equal(2, chunk.GetLine(printOffsets[1]));
    }
}
