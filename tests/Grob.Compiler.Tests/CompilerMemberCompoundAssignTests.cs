using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Sprint 9 Increment A4b (D-360) — compound assignment (<c>obj.field op= v</c>) and
/// increment/decrement (<c>obj.field++</c>/<c>--</c>) on member (field) targets, closing
/// the sibling silent-drop gap D-359 confirmed but left unfixed. The receiver is
/// evaluated exactly once, stashed in a reserved temp local, then read-modify-write
/// composes the existing <see cref="OpCode.GetProperty"/>/<see cref="OpCode.SetProperty"/>
/// and the ordinary typed binary opcodes — no new opcode is emitted. The field name is a
/// static inline operand (unlike an index target's index expression), so only one temp
/// local is needed, not two.
/// </summary>
public sealed class CompilerMemberCompoundAssignTests {
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

    private static DiagnosticBag TypeCheckDiagnostics(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private readonly record struct Instr(int Offset, OpCode Op, int Arg);

    /// <summary>Decodes a chunk into a flat instruction list with offsets and operands.</summary>
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
                case OpCode.NewArray:
                case OpCode.NewStruct:
                case OpCode.PopN:
                case OpCode.Call:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break; // zero-operand opcode
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    private static List<OpCode> Opcodes(Chunk chunk) => Decode(chunk).Select(i => i.Op).ToList();

    // -----------------------------------------------------------------------
    // c.count += 5 — evaluate-once receiver, read-modify-write, cleanup.
    // Exact opcode sequence, operand values (constant pool, temp slot) and the
    // emitted source line, per the compiler-tests bytecode contract.
    // -----------------------------------------------------------------------

    private const string ConfigType = "type Config {\ncount: int\n}\n";

    [Fact]
    public void MemberCompoundAssign_EmitsEvaluateOnceReadModifyWriteThenCleanup() {
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count += 5\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal, // c := Config { count: 10 }
                OpCode.GetGlobal,                                       // Ra = c
                OpCode.GetLocal,                                        // SetProperty's eventual receiver operand
                OpCode.GetLocal, OpCode.GetProperty,                    // read current value: c.count
                OpCode.Constant, OpCode.AddInt,                         // + 5
                OpCode.SetProperty,                                     // write back
                OpCode.PopN,                                            // release Ra
                OpCode.Return,
            ],
            ops);
    }

    [Fact]
    public void MemberCompoundAssign_OperandsPointAtTheCorrectPropertyAndRhsConstants() {
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count += 5\n");
        List<Instr> instrs = Decode(chunk);

        List<Instr> getProperties = instrs.Where(i => i.Op == OpCode.GetProperty).ToList();
        Instr setProperty = instrs.Single(i => i.Op == OpCode.SetProperty);
        Assert.Equal("count", chunk.ReadConstant(getProperties.Single().Arg).AsString());
        Assert.Equal("count", chunk.ReadConstant(setProperty.Arg).AsString());
        Assert.Equal(getProperties.Single().Arg, setProperty.Arg); // the same pooled constant, reused

        Instr addInt = instrs.Single(i => i.Op == OpCode.AddInt);
        Instr rhsConstant = instrs[instrs.IndexOf(addInt) - 1];
        Assert.Equal(OpCode.Constant, rhsConstant.Op);
        Assert.Equal(5L, chunk.ReadConstant(rhsConstant.Arg).AsInt());
    }

    [Fact]
    public void MemberCompoundAssign_BothGetLocalsReferenceTheSameReceiverSlot() {
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count += 5\n");
        List<Instr> getLocals = Decode(chunk).Where(i => i.Op == OpCode.GetLocal).ToList();

        Assert.Equal(2, getLocals.Count);
        Assert.Equal(getLocals[0].Arg, getLocals[1].Arg); // receiver slot (Ra) reused
    }

    [Fact]
    public void MemberCompoundAssign_ReleasesTheOneTempLocalViaPopNOfOne() {
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count += 5\n");
        Instr popN = Decode(chunk).Single(i => i.Op == OpCode.PopN);
        Assert.Equal(1, popN.Arg);
    }

    [Fact]
    public void MemberCompoundAssign_SetPropertyIsEmittedOnTheStatementsSourceLine() {
        // Source has the type/struct declaration+construction on lines 1-4 and the
        // compound assignment on line 5.
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count += 5\n");
        Instr setProperty = Decode(chunk).Single(i => i.Op == OpCode.SetProperty);
        Assert.Equal(5, chunk.GetLine(setProperty.Offset));
    }

    [Theory]
    [InlineData("-=", OpCode.SubtractInt)]
    [InlineData("*=", OpCode.MultiplyInt)]
    [InlineData("/=", OpCode.DivideInt)]
    [InlineData("%=", OpCode.ModuloInt)]
    public void MemberCompoundAssign_EmitsExactOpcodesAndOperandsForEachOperator(string op, OpCode expectedBinaryOp) {
        Chunk chunk = CompileSource(ConfigType + $"c := Config {{ count: 10 }}\nc.count {op} 5\n");
        List<Instr> instrs = Decode(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal,
                OpCode.GetGlobal,
                OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetProperty,
                OpCode.Constant, expectedBinaryOp,
                OpCode.SetProperty,
                OpCode.PopN,
                OpCode.Return,
            ],
            instrs.Select(i => i.Op).ToList());

        // Operand contract per operator, not just the opcode sequence: the read
        // and write both name 'count' via the same pooled constant, and the RHS is
        // the int literal 5 loaded immediately before the binary op.
        Instr getProperty = instrs.Single(i => i.Op == OpCode.GetProperty);
        Instr setProperty = instrs.Single(i => i.Op == OpCode.SetProperty);
        Assert.Equal("count", chunk.ReadConstant(getProperty.Arg).AsString());
        Assert.Equal(getProperty.Arg, setProperty.Arg);
        Instr binaryOp = instrs.Single(i => i.Op == expectedBinaryOp);
        Instr rhsConstant = instrs[instrs.IndexOf(binaryOp) - 1];
        Assert.Equal(OpCode.Constant, rhsConstant.Op);
        Assert.Equal(5L, chunk.ReadConstant(rhsConstant.Arg).AsInt());
    }

    // -----------------------------------------------------------------------
    // c.count++ / c.count-- — lowers to c.count += 1 / -= 1 (int literal).
    // -----------------------------------------------------------------------

    [Fact]
    public void MemberIncrement_EmitsExactOpcodesWithLiteralOneAndAddInt() {
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count++\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal,
                OpCode.GetGlobal,
                OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetProperty,
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetProperty,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);

        Instr constantBeforeAdd = Decode(chunk).Last(i => i.Op == OpCode.Constant);
        Assert.Equal(1L, chunk.ReadConstant(constantBeforeAdd.Arg).AsInt());
    }

    [Fact]
    public void MemberDecrement_EmitsExactOpcodesWithLiteralOneAndSubtractInt() {
        Chunk chunk = CompileSource(ConfigType + "c := Config { count: 10 }\nc.count--\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal,
                OpCode.GetGlobal,
                OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetProperty,
                OpCode.Constant, OpCode.SubtractInt,
                OpCode.SetProperty,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);
    }

    // -----------------------------------------------------------------------
    // Float field — int RHS/literal widens to float (mirrors A4's index-target
    // and the identifier-target path). Covers all five compound operators.
    // -----------------------------------------------------------------------

    private const string FConfigType = "type FConfig {\namount: float\n}\n";

    [Theory]
    [InlineData("+=", OpCode.AddFloat)]
    [InlineData("-=", OpCode.SubtractFloat)]
    [InlineData("*=", OpCode.MultiplyFloat)]
    [InlineData("/=", OpCode.DivideFloat)]
    [InlineData("%=", OpCode.ModuloFloat)]
    public void FloatMemberCompoundAssign_EmitsExactOpcodesWithIntToFloatCoercion(string op, OpCode expectedFloatOp) {
        Chunk chunk = CompileSource(FConfigType + $"f := FConfig {{ amount: 1.0 }}\nf.amount {op} 1\n");
        List<Instr> instrs = Decode(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal, // f := FConfig { amount: 1.0 }
                OpCode.GetGlobal,
                OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetProperty,
                OpCode.Constant, OpCode.IntToFloat, // RHS literal 1 widened to float
                expectedFloatOp,
                OpCode.SetProperty,
                OpCode.PopN,
                OpCode.Return,
            ],
            instrs.Select(i => i.Op).ToList());

        // Operand contract per operator: read/write both name 'amount' via the same
        // pooled constant, and the RHS is the int literal 1 (widened by IntToFloat).
        Instr getProperty = instrs.Single(i => i.Op == OpCode.GetProperty);
        Instr setProperty = instrs.Single(i => i.Op == OpCode.SetProperty);
        Assert.Equal("amount", chunk.ReadConstant(getProperty.Arg).AsString());
        Assert.Equal(getProperty.Arg, setProperty.Arg);
        Instr intToFloat = instrs.Single(i => i.Op == OpCode.IntToFloat);
        Instr rhsConstant = instrs[instrs.IndexOf(intToFloat) - 1];
        Assert.Equal(OpCode.Constant, rhsConstant.Op);
        Assert.Equal(1L, chunk.ReadConstant(rhsConstant.Arg).AsInt());
    }

    // -----------------------------------------------------------------------
    // Regression lock: GetExprType's MemberAccessExpr arm has resolved
    // ResolvedFieldType since Sprint 6 Increment C (predates this increment) —
    // a *plain* binary op over a float field already selects float arithmetic,
    // not the Unknown-defaults-to-int fallback. This pins that behaviour so it
    // cannot silently regress.
    // -----------------------------------------------------------------------

    [Fact]
    public void PlainBinaryOp_FloatFieldOperand_SelectsFloatArithmeticNotInt() {
        Chunk chunk = CompileSource(FConfigType + "f := FConfig { amount: 1.0 }\nx := f.amount + 1\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Contains(OpCode.IntToFloat, ops);
        Assert.Contains(OpCode.AddFloat, ops);
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    // -----------------------------------------------------------------------
    // Chained target: a.b.c += v — inner receiver read once, then the
    // evaluate-once temp-local machine over the outer field. Asserted as an
    // exact opcode tail (mirroring CompilerIndexCompoundAssignTests's chained
    // precedent), since the preceding type/struct declarations are unrelated
    // to this increment's emission and would only obscure the shape under test.
    // -----------------------------------------------------------------------

    private const string ChainedTypes =
        "type Inner {\nc: int\n}\n" +
        "type Outer {\nb: Inner\n}\n";

    [Fact]
    public void ChainedMemberCompoundAssign_EmitsExactTailReadingInnerReceiverOnceThenSettingProperty() {
        Chunk chunk = CompileSource(
            ChainedTypes + "a := Outer { b: Inner { c: 1 } }\na.b.c += 9\n");
        List<Instr> instrs = Decode(chunk);
        List<OpCode> tail = instrs.Select(i => i.Op).SkipLast(1).TakeLast(9).ToList();

        Assert.Equal(
            [
                OpCode.GetGlobal, OpCode.GetProperty,       // a.b receiver read once (Ra)
                OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetProperty,        // current value: (a.b).c
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetProperty,
                OpCode.PopN,
            ],
            tail);

        Instr addInt = instrs.Single(i => i.Op == OpCode.AddInt);
        Instr constantNine = instrs[instrs.IndexOf(addInt) - 1];
        Assert.Equal(OpCode.Constant, constantNine.Op);
        Assert.Equal(9L, chunk.ReadConstant(constantNine.Arg).AsInt());
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.GetProperty));
        Assert.Equal(1, instrs.Count(i => i.Op == OpCode.SetProperty));
    }

    // -----------------------------------------------------------------------
    // Evaluate-once — the load-bearing case: a side-effecting receiver
    // expression is visited exactly once. Asserted as an exact opcode tail
    // for the same reason as the chained case above.
    // -----------------------------------------------------------------------

    [Fact]
    public void CallReceiverMemberCompoundAssign_EmitsExactTailWithTheCallOnce() {
        Chunk chunk = CompileSource(
            ConfigType +
            "c := Config { count: 10 }\n" +
            "fn getObj(): Config { return c }\n" +
            "getObj().count += 1\n");
        List<Instr> instrs = Decode(chunk);
        List<OpCode> tail = instrs.Select(i => i.Op).SkipLast(1).TakeLast(9).ToList();

        Assert.Equal(
            [
                OpCode.GetGlobal, OpCode.Call, // getObj() — receiver (Ra)
                OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetProperty,
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetProperty,
                OpCode.PopN,
            ],
            tail);

        Instr call = instrs.Single(i => i.Op == OpCode.Call);
        Assert.Equal(0, call.Arg); // nullary call
    }

    // -----------------------------------------------------------------------
    // Type-check diagnostics — no new error code: E0002 (binary/increment
    // mismatch) and E0204 (readonly root), exactly as the identifier-target
    // and index-target paths already raise them. Every case asserts the full
    // diagnostic contract (code, 1-based line and column).
    // -----------------------------------------------------------------------

    private const string SConfigType = "type SConfig {\nname: string\n}\n";

    [Fact]
    public void MemberIncrement_FloatField_EmitsE0002() {
        DiagnosticBag bag = TypeCheckDiagnostics(FConfigType + "f := FConfig { amount: 1.0 }\nf.amount++\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MemberIncrement_StringField_EmitsE0002() {
        DiagnosticBag bag = TypeCheckDiagnostics(SConfigType + "s := SConfig { name: \"a\" }\ns.name++\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MemberCompoundAssign_MismatchedFieldAndRhsTypes_EmitsE0002() {
        DiagnosticBag bag = TypeCheckDiagnostics(SConfigType + "s := SConfig { name: \"a\" }\ns.name *= 2\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MemberCompoundAssign_ReadonlyStruct_EmitsE0204() {
        DiagnosticBag bag = TypeCheckDiagnostics(ConfigType + "readonly c := Config { count: 10 }\nc.count += 1\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MemberIncrement_ReadonlyStruct_EmitsE0204() {
        DiagnosticBag bag = TypeCheckDiagnostics(ConfigType + "readonly c := Config { count: 10 }\nc.count++\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MemberCompoundAssign_ValidIntField_ProducesNoDiagnostics() {
        DiagnosticBag bag = TypeCheckDiagnostics(
            ConfigType + "c := Config { count: 10 }\nc.count += 1\nc.count++\nc.count--\n");
        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // E0206 — optional chaining ('?.') is rejected in a mutating member target,
    // exactly as the plain-assignment path (VisitAssignment) already rejects
    // 'c?.port = v'. A compound assignment or increment through '?.' must never
    // lower to GetProperty/SetProperty on a possibly-nil receiver. Full
    // diagnostic contract (code, 1-based line and column).
    // -----------------------------------------------------------------------

    [Fact]
    public void MemberCompoundAssign_OptionalChain_EmitsE0206() {
        DiagnosticBag bag = TypeCheckDiagnostics(ConfigType + "c := Config { count: 10 }\nc?.count += 5\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0206", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MemberIncrement_OptionalChain_EmitsE0206() {
        DiagnosticBag bag = TypeCheckDiagnostics(ConfigType + "c := Config { count: 10 }\nc?.count++\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0206", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Compiler defence-in-depth — an optional-chained mutating member target is
    // rejected by the type checker (E0206 above), so a well-formed compilation
    // never reaches the emitter with IsOptional=true. These tests compile despite
    // the TC error to exercise the 'if (memberTarget.IsOptional) return null'
    // guard that keeps SetProperty from being emitted even if one slips through,
    // mirroring FieldAssign_OptionalTarget_WithTcErrors_EmitsNoSetProperty.
    // -----------------------------------------------------------------------

    [Fact]
    public void MemberCompoundAssign_OptionalTarget_WithTcErrors_EmitsNoSetProperty() {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(ConfigType + "c := Config { count: 10 }\nc?.count += 5\n", bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0206", diag.Code);
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.DoesNotContain(Decode(chunk), i => i.Op == OpCode.SetProperty);
    }

    [Fact]
    public void MemberIncrement_OptionalTarget_WithTcErrors_EmitsNoSetProperty() {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(ConfigType + "c := Config { count: 10 }\nc?.count++\n", bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0206", diag.Code);
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.DoesNotContain(Decode(chunk), i => i.Op == OpCode.SetProperty);
    }
}
