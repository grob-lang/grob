using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 9 Increment B's date-vs-date comparison support
/// (D-354): the compiler's opcode-selection default for any non-Float/non-String
/// category is <c>Int</c> (<see cref="Compiler.ComparisonCategory"/>), so a
/// <c>Struct</c>-typed date comparison reaching the ordinary path unchanged would
/// silently emit <c>LessInt</c>/<c>GreaterInt</c> against two struct receivers — these
/// tests pin that <c>LessDate</c>/<c>GreaterDate</c> are selected instead, and that
/// <c>&lt;=</c>/<c>&gt;=</c> lower to the strict opcode plus <see cref="OpCode.Not"/>
/// (mirroring the pre-existing string lowering), never to the Int family.
/// </summary>
public sealed class CompilerDateComparisonTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            result.Add(op);
            switch (op) {
                case OpCode.Constant:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.DefineGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.GetProperty:
                case OpCode.Call:
                    offset += 1;
                    break;
                case OpCode.ConstantLong:
                    offset += 2;
                    break;
            }
            if (op == OpCode.Return) break;
        }
        return result;
    }

    private const string TwoDates = """
        a := date.now()
        b := date.now()
        """;

    [Fact]
    public void DateLess_EmitsLessDate() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a < b");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.LessDate, ops);
        Assert.DoesNotContain(OpCode.LessInt, ops);
    }

    [Fact]
    public void DateGreater_EmitsGreaterDate() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a > b");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.GreaterDate, ops);
        Assert.DoesNotContain(OpCode.GreaterInt, ops);
    }

    [Fact]
    public void DateLessEqual_LowersToGreaterDateThenNot() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a <= b");
        List<OpCode> ops = ReadOpcodes(chunk);
        int idx = ops.IndexOf(OpCode.GreaterDate);
        Assert.True(idx >= 0, "expected GreaterDate in the lowered sequence");
        Assert.Equal(OpCode.Not, ops[idx + 1]);
        Assert.DoesNotContain(OpCode.LessEqualInt, ops);
    }

    [Fact]
    public void DateGreaterEqual_LowersToLessDateThenNot() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a >= b");
        List<OpCode> ops = ReadOpcodes(chunk);
        int idx = ops.IndexOf(OpCode.LessDate);
        Assert.True(idx >= 0, "expected LessDate in the lowered sequence");
        Assert.Equal(OpCode.Not, ops[idx + 1]);
        Assert.DoesNotContain(OpCode.GreaterEqualInt, ops);
    }
}
