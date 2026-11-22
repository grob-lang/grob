using System.Globalization;
using System.Text;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Runtime;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Compiles checked source and renders a whole top-level chunk as text, one instruction
/// per line with its operands and any constant it names, so a test can pin every byte
/// of a call site (native named arguments, gate report §1.5).
/// </summary>
/// <remarks>
/// The renderer reads the operands of the opcodes call-site programs use. An opcode it
/// does not know is rendered on its own and its operand bytes are rendered as the
/// instructions that follow, so the rendering still covers every byte: a change in any
/// byte changes the text.
/// </remarks>
internal static class ChunkRendering {
    /// <summary>Lexes, parses, checks and compiles <paramref name="source"/>, asserting no diagnostic.</summary>
    public static Chunk CompileChecked(string source, NativeCatalogue? catalogue = null) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, catalogue ?? SharedCatalogue.Stdlib).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    /// <summary>Renders every instruction of <paramref name="chunk"/>, one per line.</summary>
    public static string Render(Chunk chunk) =>
        string.Concat(Instructions(chunk).Select(i => i.Text + "\n"));

    /// <summary>
    /// Every instruction of <paramref name="chunk"/>: its offset, opcode and rendered text,
    /// so a test can read the line table at an instruction's offset.
    /// </summary>
    public static IReadOnlyList<(int Offset, OpCode Op, string Text)> Instructions(Chunk chunk) {
        var result = new List<(int, OpCode, string)>();
        int offset = 0;
        while (offset < chunk.Count) {
            int start = offset;
            var op = (OpCode)chunk.ReadByte(offset++);
            var text = new StringBuilder(op.ToString());
            switch (op) {
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    text.Append(' ').Append((chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1));
                    offset += 2;
                    break;
                case OpCode.ConstantLong:
                    text.Append(' ').Append(Constant(chunk, (chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1)));
                    offset += 2;
                    break;
                case OpCode.Constant:
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetProperty:
                    text.Append(' ').Append(Constant(chunk, chunk.ReadByte(offset++)));
                    break;
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.PopN:
                case OpCode.NewArray:
                case OpCode.BuildString:
                case OpCode.Call:
                    text.Append(' ').Append(chunk.ReadByte(offset++));
                    break;
                case OpCode.Closure:
                    offset = RenderClosure(chunk, offset, text);
                    break;
                default:
                    break;
            }
            result.Add((start, op, text.ToString()));
        }
        return result;
    }

    /// <summary>Asserts that <paramref name="source"/> compiles to exactly <paramref name="expected"/>.</summary>
    public static void AssertEmits(string source, string expected, NativeCatalogue? catalogue = null) =>
        Assert.Equal(expected.ReplaceLineEndings("\n").TrimEnd('\n') + "\n", Render(CompileChecked(source, catalogue)));

    private static int RenderClosure(Chunk chunk, int offset, StringBuilder text) {
        int fnIndex = chunk.ReadByte(offset++);
        text.Append(' ').Append(Constant(chunk, fnIndex));
        if (chunk.ReadConstant(fnIndex).TryAsFunction(out GrobFunction? gf) && gf is BytecodeFunction closure) {
            for (int u = 0; u < closure.UpvalueCount; u++) {
                text.Append(' ').Append(chunk.ReadByte(offset)).Append(':').Append(chunk.ReadByte(offset + 1));
                offset += 2;
            }
        }
        return offset;
    }

    private static string Constant(Chunk chunk, int index) {
        GrobValue value = chunk.ReadConstant(index);
        string shown = value.Kind switch {
            GrobValueKind.String => $"\"{value.AsString()}\"",
            GrobValueKind.Function when value.TryAsFunction(out GrobFunction? fn) && fn is not null =>
                fn.Name.Length == 0 ? "fn <lambda>" : $"fn {fn.Name}",
            _ => Convert.ToString(value.ToString(), CultureInfo.InvariantCulture) ?? "?",
        };
        return $"[{index}] {value.Kind} {shown}";
    }
}
