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

    // Renders every emitted instruction as "OpCode:operand@line", so an assertion pins
    // the exact opcode sequence, the exact operand bytes and the line-number array in
    // one comparison, per tests/CLAUDE.md's full-Chunk-contract rule (CodeRabbit review,
    // PR #165 — the earlier FindIndex-based assertions only located instructions).
    private static string[] Describe(Chunk chunk) =>
        Decode(chunk).Select(i => $"{i.Op}:{i.Arg}@{chunk.GetLine(i.Offset)}").ToArray();

    // Renders the constant pool as "Kind:value", so an assertion pins both the entry
    // order the operands above index into and each entry's runtime kind.
    private static string[] DescribePool(Chunk chunk) =>
        Enumerable.Range(0, chunk.ConstantCount)
            .Select(i => $"{chunk.ReadConstant(i).Kind}:{chunk.ReadConstant(i)}")
            .ToArray();

    // Every case below compiles this prologue on line 1, so its four instructions and
    // its first three pool entries ("a", 1, "m") are shared by all of them.
    private const string MapPrologue = "m := map<string, int>{\"a\": 1}\n";

    // -----------------------------------------------------------------------
    // length / isEmpty / keys / values — properties, generic GetProperty emission.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("length")]
    [InlineData("isEmpty")]
    [InlineData("keys")]
    [InlineData("values")]
    public void Property_EmitsGetProperty_WithMemberName(string memberName) {
        Chunk chunk = CompileSource(MapPrologue + $"readonly r := m.{memberName}\n");

        Assert.Equal([
            "Constant:0@1", "Constant:1@1", "NewMap:1@1", "DefineGlobal:2@1",
            "GetGlobal:2@2", "GetProperty:3@2", "DefineGlobal:4@2",
            "Return:0@3",
        ], Describe(chunk));
        Assert.Equal([
            "String:a", "Int:1", "String:m", $"String:{memberName}", "String:r",
        ], DescribePool(chunk));
    }

    [Fact]
    public void Length_UsesResolvedFieldType_ForTypedOpcodeSelection() {
        // m.length + 1 must emit AddInt (not the untyped Add), proving the compiler reads
        // MemberAccessExpr.ResolvedFieldType rather than falling back to Unknown.
        Chunk chunk = CompileSource(MapPrologue + "readonly n := m.length + 1\n");

        Assert.Equal([
            "Constant:0@1", "Constant:1@1", "NewMap:1@1", "DefineGlobal:2@1",
            "GetGlobal:2@2", "GetProperty:3@2", "Constant:4@2", "AddInt:0@2",
            "DefineGlobal:5@2", "Return:0@3",
        ], Describe(chunk));
        Assert.Equal([
            "String:a", "Int:1", "String:m", "String:length", "Int:1", "String:n",
        ], DescribePool(chunk));
    }

    // -----------------------------------------------------------------------
    // get(key) / contains(key) — the generic GetProperty-then-Call shape; no
    // compiler-emission change needed.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("get", "g")]
    [InlineData("contains", "b")]
    public void Method_EmitsGetPropertyThenArgumentThenCallWithOneArgument(
            string memberName, string bindingName) {
        Chunk chunk = CompileSource(MapPrologue + $"readonly {bindingName} := m.{memberName}(\"a\")\n");

        Assert.Equal([
            "Constant:0@1", "Constant:1@1", "NewMap:1@1", "DefineGlobal:2@1",
            "GetGlobal:2@2", "GetProperty:3@2", "Constant:4@2", "Call:1@2",
            "DefineGlobal:5@2", "Return:0@3",
        ], Describe(chunk));
        Assert.Equal([
            "String:a", "Int:1", "String:m", $"String:{memberName}", "String:a",
            $"String:{bindingName}",
        ], DescribePool(chunk));
    }
}
