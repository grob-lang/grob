using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-450: with one operand of unknown type, such as a lambda's untyped parameter, the
/// compiler chooses a binary operator's opcode from the operand whose type it knows,
/// whichever side it is on. It took the left operand's type, or <c>int</c> when the left
/// was the unknown one, so <c>"[" + s</c> threw in the compiler and <c>s + "]"</c>
/// compiled to <see cref="OpCode.AddInt"/> (R-61).
/// </summary>
public sealed class CompilerUnknownOperandOpcodeTests {
    private static readonly HashSet<OpCode> OneByteOperand = [
        OpCode.Constant, OpCode.Call, OpCode.GetGlobal, OpCode.SetGlobal, OpCode.DefineGlobal,
        OpCode.GetLocal, OpCode.SetLocal, OpCode.GetUpvalue, OpCode.SetUpvalue,
        OpCode.GetProperty, OpCode.SetProperty, OpCode.PopN, OpCode.NewArray, OpCode.BuildString,
        OpCode.NewStruct, OpCode.NewAnonStruct, OpCode.Import, OpCode.TryBegin,
        OpCode.IncrementInt, OpCode.DecrementInt, OpCode.IncrementFloat, OpCode.DecrementFloat,
    ];

    private static readonly HashSet<OpCode> TwoByteOperand = [
        OpCode.ConstantLong, OpCode.Jump, OpCode.JumpIfFalse, OpCode.JumpIfTrue, OpCode.Loop,
    ];

    private static Chunk CompileSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        Assert.False(bag.HasErrors, $"TypeChecker produced errors: {string.Join("; ", bag.Errors)}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors, $"Compiler produced errors: {string.Join("; ", bag.Errors)}");
        return chunk;
    }

    private static List<(OpCode Op, int Arg)> Decode(Chunk chunk) {
        var result = new List<(OpCode, int)>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            int arg = 0;
            if (op == OpCode.Closure) {
                arg = chunk.ReadByte(offset++);
                if (chunk.ReadConstant(arg).TryAsFunction(out GrobFunction? gf) && gf is BytecodeFunction fn) {
                    offset += fn.UpvalueCount * 2;
                }
            } else if (TwoByteOperand.Contains(op)) {
                arg = (chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1);
                offset += 2;
            } else if (OneByteOperand.Contains(op)) {
                arg = chunk.ReadByte(offset++);
            }
            result.Add((op, arg));
        }
        return result;
    }

    /// <summary>The opcodes of the single lambda the source compiles.</summary>
    private static List<OpCode> LambdaOpcodes(string source) {
        Chunk chunk = CompileSource(source);
        BytecodeFunction lambda = Assert.Single(Decode(chunk)
            .Where(i => i.Op == OpCode.Constant)
            .Select(i => chunk.ReadConstant(i.Arg))
            .Where(v => v.IsFunction)
            .Select(v => v.AsFunction())
            .OfType<BytecodeFunction>());
        return [.. Decode(lambda.Bytecode).Select(i => i.Op)];
    }

    [Theory]
    [InlineData("f := (s) => \"[\" + s\n")]
    [InlineData("f := (s) => s + \"]\"\n")]
    [InlineData("names := [\"a\"]\nr := names.select((n) => \"Hi \" + n)\n")]
    public void StringAndUnknownOperand_Add_EmitsConcat(string source) {
        List<OpCode> ops = LambdaOpcodes(source);

        Assert.Equal(1, ops.Count(o => o == OpCode.Concat));
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    [Fact]
    public void StringOnBothSidesOfUnknown_Add_EmitsTwoConcats() {
        List<OpCode> ops = LambdaOpcodes("f := (s) => \"[\" + s + \"]\"\n");

        Assert.Equal(2, ops.Count(o => o == OpCode.Concat));
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    [Theory]
    [InlineData("s < \"z\"", OpCode.LessString)]
    [InlineData("s > \"z\"", OpCode.GreaterString)]
    [InlineData("\"a\" < s", OpCode.LessString)]
    [InlineData("s < date.now()", OpCode.LessDate)]
    [InlineData("date.now() > s", OpCode.GreaterDate)]
    [InlineData("s > 2.5", OpCode.GreaterFloat)]
    [InlineData("s < 2", OpCode.LessInt)]
    public void KnownAndUnknownOperand_Ordering_EmitsTheKnownOperandsComparison(string body, OpCode expected) {
        List<OpCode> ops = LambdaOpcodes($"f := (s) => {body}\n");

        Assert.Contains(expected, ops);
        Assert.DoesNotContain(OpCode.Not, ops);
    }

    [Theory]
    [InlineData("s <= \"z\"", OpCode.GreaterString)]
    [InlineData("\"a\" >= s", OpCode.LessString)]
    [InlineData("s >= date.now()", OpCode.LessDate)]
    public void KnownAndUnknownOperand_NonStrictOrdering_LowersToStrictPlusNot(string body, OpCode strict) {
        List<OpCode> ops = LambdaOpcodes($"f := (s) => {body}\n");

        int index = ops.IndexOf(strict);
        Assert.True(index >= 0, $"Expected {strict} in {string.Join(", ", ops)}");
        Assert.Equal(OpCode.Not, ops[index + 1]);
    }

    [Theory]
    [InlineData("s + 1", OpCode.AddInt)]
    [InlineData("1 - s", OpCode.SubtractInt)]
    [InlineData("s * 1.5", OpCode.MultiplyFloat)]
    [InlineData("2.0 / s", OpCode.DivideFloat)]
    public void NumericAndUnknownOperand_Arithmetic_EmitsTheNumericOpcode(string body, OpCode expected) {
        List<OpCode> ops = LambdaOpcodes($"f := (s) => {body}\n");

        Assert.Contains(expected, ops);
        Assert.DoesNotContain(OpCode.IntToFloat, ops);
    }

    /// <summary>
    /// The inner <c>s + "a"</c> is a <c>string</c>, so the outer <c>+</c> has a known
    /// operand too and concatenates.
    /// </summary>
    [Fact]
    public void NestedUnknownAndString_Add_EmitsConcatAtEachLevel() {
        List<OpCode> ops = LambdaOpcodes("f := (s) => s + \"a\" + s\n");

        Assert.Equal(2, ops.Count(o => o == OpCode.Concat));
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    /// <summary>
    /// The inner <c>s - 1</c> is an <c>int</c>, so the outer <c>+ 2.5</c> widens it before
    /// adding floats.
    /// </summary>
    [Fact]
    public void NestedUnknownAndInt_ThenFloat_WidensTheInnerResult() {
        List<OpCode> ops = LambdaOpcodes("f := (n) => n - 1 + 2.5\n");

        int widen = ops.IndexOf(OpCode.IntToFloat);
        Assert.True(widen > ops.IndexOf(OpCode.SubtractInt), string.Join(", ", ops));
        Assert.Equal(OpCode.AddFloat, ops[widen + 2]);
    }

    /// <summary>
    /// Two operands of unknown type give nothing to choose from, so the opcode stays the
    /// <c>int</c> default (R-63).
    /// </summary>
    [Fact]
    public void TwoUnknownOperands_Add_KeepsTheIntDefault() {
        List<OpCode> ops = LambdaOpcodes("f := (a, b) => a + b\n");

        Assert.Contains(OpCode.AddInt, ops);
        Assert.DoesNotContain(OpCode.Concat, ops);
    }
}
