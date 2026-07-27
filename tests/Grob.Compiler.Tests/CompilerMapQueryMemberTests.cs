using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 9 Increment C0b-2a (D-377) — the map non-mutating
/// query member surface. <c>length</c>/<c>isEmpty</c>/<c>keys</c>/<c>values</c> compile
/// through the existing generic <see cref="OpCode.GetProperty"/> path (no
/// compiler-emission change needed); <c>get(key)</c>/<c>contains(key)</c> compile through
/// the same generic <see cref="OpCode.GetProperty"/>-then-<see cref="OpCode.Call"/> shape
/// the array query members already prove (D-371).
/// </summary>
public sealed class CompilerMapQueryMemberTests {
    private static Chunk CompileSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    private readonly record struct Instr(int Offset, OpCode Op, int Arg);

    private static List<Instr> Decode(Chunk chunk) {
        var result = new List<Instr>();
        int offset = 0;
        while (offset < chunk.Count) {
            int here = offset;
            var op = (OpCode)chunk.ReadByte(offset++);
            int arg = 0;
            switch (op) {
                case OpCode.ConstantLong:
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    arg = (chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1);
                    offset += 2;
                    break;
                case OpCode.Constant:
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.PopN:
                case OpCode.IncrementInt:
                case OpCode.DecrementInt:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                case OpCode.NewArray:
                case OpCode.NewMap:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.GetUpvalue:
                case OpCode.SetUpvalue:
                case OpCode.NewAnonStruct:
                case OpCode.NewStruct:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break;
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // length / isEmpty / keys / values — properties, generic GetProperty emission.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly n := m.length\n", "length")]
    [InlineData("readonly e := m.isEmpty\n", "isEmpty")]
    [InlineData("readonly k := m.keys\n", "keys")]
    [InlineData("readonly v := m.values\n", "values")]
    public void Property_EmitsGetProperty_WithMemberName(string tail, string expectedName) {
        Chunk chunk = CompileSource("m := map<string, int>{\"a\": 1}\n" + tail);

        List<Instr> instrs = Decode(chunk);
        int idx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        Assert.True(idx >= 0, "no GetProperty instruction found");
        GrobValue nameConst = chunk.ReadConstant(instrs[idx].Arg);
        Assert.Equal(expectedName, nameConst.AsString());
    }

    [Fact]
    public void Length_UsesResolvedFieldType_ForTypedOpcodeSelection() {
        // m.length + 1 must emit AddInt, proving the compiler reads
        // MemberAccessExpr.ResolvedFieldType rather than falling back to Unknown.
        Chunk chunk = CompileSource("m := map<string, int>{\"a\": 1}\nreadonly n := m.length + 1\n");

        List<Instr> instrs = Decode(chunk);
        Assert.Contains(instrs, i => i.Op == OpCode.GetProperty);
        Assert.Contains(instrs, i => i.Op == OpCode.AddInt);
    }

    // -----------------------------------------------------------------------
    // get(key) / contains(key) — the generic GetProperty-then-Call shape; no
    // compiler-emission change needed.
    // -----------------------------------------------------------------------

    [Fact]
    public void Get_EmitsGetPropertyThenArgumentThenCallWithOneArgument() {
        Chunk chunk = CompileSource("m := map<string, int>{\"a\": 1}\nreadonly g := m.get(\"a\")\n");

        List<Instr> instrs = Decode(chunk);
        int propIdx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        Assert.True(propIdx >= 0, "no GetProperty instruction found");
        Assert.Equal("get", chunk.ReadConstant(instrs[propIdx].Arg).AsString());

        int callIdx = instrs.FindIndex(propIdx, i => i.Op == OpCode.Call);
        Assert.True(callIdx >= 0, "no Call instruction found after GetProperty");
        Assert.Equal(1, instrs[callIdx].Arg);
        Assert.True(propIdx < callIdx);
    }

    [Fact]
    public void Contains_EmitsGetPropertyThenArgumentThenCallWithOneArgument() {
        Chunk chunk = CompileSource("m := map<string, int>{\"a\": 1}\nreadonly b := m.contains(\"a\")\n");

        List<Instr> instrs = Decode(chunk);
        int propIdx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        Assert.True(propIdx >= 0, "no GetProperty instruction found");
        Assert.Equal("contains", chunk.ReadConstant(instrs[propIdx].Arg).AsString());

        int callIdx = instrs.FindIndex(propIdx, i => i.Op == OpCode.Call);
        Assert.True(callIdx >= 0, "no Call instruction found after GetProperty");
        Assert.Equal(1, instrs[callIdx].Arg);
        Assert.True(propIdx < callIdx);
    }
}
