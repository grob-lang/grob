using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker and bytecode-shape tests for Sprint 4 Increment D — the
/// <c>select</c>/<c>case</c> statement and the D-315 control-flow correction.
/// </summary>
/// <remarks>
/// <para><b>Emission.</b> <c>select</c> evaluates the subject once into a synthetic
/// local, then compiles an equality ladder: per pattern, <c>GetLocal</c> the subject,
/// emit the pattern, <see cref="OpCode.Equal"/>, then <see cref="OpCode.JumpIfFalse"/>
/// to the next test. A matched block ends with an unconditional <see cref="OpCode.Jump"/>
/// to the end. A multi-value <c>case</c> ORs several <see cref="OpCode.Equal"/> tests to
/// one block. The synthetic subject local is discarded with a trailing
/// <see cref="OpCode.PopN"/>.</para>
/// <para><b>Control flow (D-315).</b> <c>select</c> is not loop-control-transparent:
/// <c>break</c> inside a <c>select</c> arm is E2211 at any nesting; <c>continue</c>
/// passes through to the nearest enclosing loop; <c>break</c>/<c>continue</c> with no
/// enclosing loop is E2212.</para>
/// </remarks>
public sealed class CompilerSelectTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    private static DiagnosticBag TypeCheckSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static CompilationUnit ParseAndCheck(string source, out DiagnosticBag bag) {
        bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return unit;
    }

    /// <summary>
    /// Reads the opcode sequence together with each opcode's byte offset, so that
    /// jump targets can be decoded. Stops at (and includes) the first
    /// <see cref="OpCode.Return"/>.
    /// </summary>
    private static List<(OpCode Op, int Offset)> ReadOpcodesWithOffsets(Chunk chunk) {
        var result = new List<(OpCode, int)>();
        int offset = 0;
        while (offset < chunk.Count) {
            int opOffset = offset;
            var op = (OpCode)chunk.ReadByte(offset++);
            result.Add((op, opOffset));
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
            if (op == OpCode.Return) break;
        }
        return result;
    }

    private static List<OpCode> ReadOpcodes(Chunk chunk) =>
        ReadOpcodesWithOffsets(chunk).Select(e => e.Op).ToList();

    /// <summary>
    /// Decodes the absolute target byte offset of a forward jump whose opcode byte
    /// sits at <paramref name="jumpOpOffset"/>. Mirrors <c>PatchJump</c>: the offset
    /// counts from the byte after the two operand bytes.
    /// </summary>
    private static int ForwardJumpTarget(Chunk chunk, int jumpOpOffset) {
        int hi = chunk.ReadByte(jumpOpOffset + 1);
        int lo = chunk.ReadByte(jumpOpOffset + 2);
        int delta = (hi << 8) | lo;
        return jumpOpOffset + 3 + delta;
    }

    // -----------------------------------------------------------------------
    // Compiler — equality ladder shape
    // -----------------------------------------------------------------------

    /// <summary>
    /// A single-value <c>case</c> compiles to: evaluate the subject into the
    /// synthetic local, then <c>GetLocal</c> / pattern / <see cref="OpCode.Equal"/> /
    /// <see cref="OpCode.JumpIfFalse"/>, the (empty) body, an unconditional
    /// <see cref="OpCode.Jump"/> to the end, then <see cref="OpCode.PopN"/> of the
    /// subject.
    /// </summary>
    [Fact]
    public void Select_SingleCase_EmitsEqualJumpIfFalseLadder() {
        Chunk chunk = CompileSource("select (1) { case 1 { } }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [
                OpCode.Constant,    // subject 1 -> $subject local
                OpCode.GetLocal,    // load subject
                OpCode.Constant,    // pattern 1
                OpCode.Equal,
                OpCode.JumpIfFalse, // skip body when not equal
                OpCode.Jump,        // end of body -> exit select
                OpCode.PopN,        // discard $subject
                OpCode.Return,
            ],
            ops);
    }

    /// <summary>
    /// A multi-value <c>case 1, 2</c> ORs two <see cref="OpCode.Equal"/> tests to a
    /// single body block. The first test's matched path is an unconditional
    /// <see cref="OpCode.Jump"/> that lands on the shared body; the second test falls
    /// through to that same body on a match.
    /// </summary>
    [Fact]
    public void Select_MultiValueCase_OrsTwoEqualTestsToOneBlock() {
        // A non-empty body (bare 'nil' -> Nil, Pop) makes the body block a distinct
        // jump target, so the OR-jump can be proven to land on it.
        Chunk chunk = CompileSource("select (1) { case 1, 2 { nil } }");
        List<(OpCode Op, int Offset)> ops = ReadOpcodesWithOffsets(chunk);

        Assert.Equal(
            [
                OpCode.Constant,    // subject
                OpCode.GetLocal,    // test 1: load subject
                OpCode.Constant,    // pattern 1
                OpCode.Equal,
                OpCode.JumpIfFalse, // not 1 -> try pattern 2
                OpCode.Jump,        // matched 1 -> OR-jump to shared body
                OpCode.GetLocal,    // test 2: load subject
                OpCode.Constant,    // pattern 2
                OpCode.Equal,
                OpCode.JumpIfFalse, // not 2 -> exit select
                OpCode.Nil,         // body
                OpCode.Pop,
                OpCode.Jump,        // end of body -> exit select
                OpCode.PopN,        // discard $subject
                OpCode.Return,
            ],
            ops.Select(e => e.Op));

        // Exactly two Equal tests.
        Assert.Equal(2, ops.Count(e => e.Op == OpCode.Equal));

        // The OR-jump (the first Jump) must target the shared body block — the Nil
        // opcode — not the select exit.
        (OpCode Op, int Offset) orJump = ops.First(e => e.Op == OpCode.Jump);
        int bodyOffset = ops.First(e => e.Op == OpCode.Nil).Offset;
        Assert.Equal(bodyOffset, ForwardJumpTarget(chunk, orJump.Offset));
    }

    /// <summary>
    /// First-match, no fall-through: each <c>case</c> body ends with an unconditional
    /// <see cref="OpCode.Jump"/> to the select exit, so a matched earlier case cannot
    /// fall into a later one. Two single-value cases emit two body-terminating jumps
    /// that both target the trailing <see cref="OpCode.PopN"/>.
    /// </summary>
    [Fact]
    public void Select_TwoCases_EachBodyJumpsPastTheRest() {
        Chunk chunk = CompileSource("select (1) { case 1 { nil } case 2 { nil } }");
        List<(OpCode Op, int Offset)> ops = ReadOpcodesWithOffsets(chunk);

        Assert.Equal(2, ops.Count(e => e.Op == OpCode.Equal));

        int popnOffset = ops.First(e => e.Op == OpCode.PopN).Offset;
        List<(OpCode Op, int Offset)> jumps = ops.Where(e => e.Op == OpCode.Jump).ToList();
        Assert.Equal(2, jumps.Count);
        foreach ((OpCode _, int jumpOffset) in jumps)
            Assert.Equal(popnOffset, ForwardJumpTarget(chunk, jumpOffset));
    }

    /// <summary>
    /// A <c>select</c> with no matching case and no <c>default</c> is not an error
    /// (D-301) and compiles to a no-op tail: the subject is evaluated and discarded,
    /// the single case test falls straight through to the exit.
    /// </summary>
    [Fact]
    public void Select_NoMatchNoDefault_CompilesToNoOpTail() {
        Chunk chunk = CompileSource("select (1) { case 2 { } }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [
                OpCode.Constant,    // subject
                OpCode.GetLocal,
                OpCode.Constant,    // pattern 2
                OpCode.Equal,
                OpCode.JumpIfFalse, // no match -> exit
                OpCode.Jump,        // (empty) body terminator
                OpCode.PopN,        // discard $subject
                OpCode.Return,
            ],
            ops);
    }

    /// <summary>
    /// A <c>default</c> block compiles as the fall-through tail after the last case
    /// test, before the trailing <see cref="OpCode.PopN"/>.
    /// </summary>
    [Fact]
    public void Select_WithDefault_EmitsDefaultTailBeforePop() {
        Chunk chunk = CompileSource("select (1) { case 2 { } default { nil } }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [
                OpCode.Constant,    // subject
                OpCode.GetLocal,
                OpCode.Constant,    // pattern 2
                OpCode.Equal,
                OpCode.JumpIfFalse, // no match -> default tail
                OpCode.Jump,        // matched-case body terminator -> exit
                OpCode.Nil,         // default body
                OpCode.Pop,
                OpCode.PopN,        // discard $subject
                OpCode.Return,
            ],
            ops);
    }

    /// <summary>
    /// A <c>select</c> pushes no loop context, so the equality ladder contains no
    /// backward <see cref="OpCode.Loop"/> — it is a forward-jump construct only.
    /// </summary>
    [Fact]
    public void Select_EmitsNoBackwardLoop() {
        Chunk chunk = CompileSource("select (1) { case 1 { } case 2 { } default { } }");
        Assert.DoesNotContain(OpCode.Loop, ReadOpcodes(chunk));
    }

    // -----------------------------------------------------------------------
    // Type-checker — case-value compatibility
    // -----------------------------------------------------------------------

    /// <summary>
    /// A case value whose type is not comparable to the subject type is a compile
    /// error (E0001 type mismatch) located at the offending pattern.
    /// </summary>
    [Fact]
    public void Select_IncompatibleCaseType_ProducesE0001() {
        DiagnosticBag bag = TypeCheckSource("select (1) { case \"x\" { } }");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E0001");
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(19, diag.Range.Start.Column);
    }

    /// <summary>
    /// Matching case-value types produce no diagnostic, including a multi-value case
    /// and a <c>default</c>.
    /// </summary>
    [Fact]
    public void Select_CompatibleCaseTypes_NoError() {
        DiagnosticBag bag = TypeCheckSource("select (1) { case 1, 2 { } default { } }");
        Assert.False(bag.HasErrors);
    }

    /// <summary>
    /// The §3.1.1 invariant: every identifier node carries a non-null
    /// <see cref="IdentifierExpr.ResolvedType"/> and <see cref="IdentifierExpr.Declaration"/>
    /// after type-check — here the subject identifier and an identifier case value.
    /// </summary>
    [Fact]
    public void Select_IdentifierSubjectAndPattern_AreResolved() {
        CompilationUnit unit = ParseAndCheck(
            "x := 1\ny := 2\nselect (x) { case y { } }\n", out DiagnosticBag bag);
        Assert.False(bag.HasErrors);

        SelectStmt select = unit.TopLevel.OfType<SelectStmt>().Single();
        // The parenthesised subject is wrapped in a GroupingExpr; the identifier
        // node carrying the invariant is its inner expression.
        var grouping = Assert.IsType<GroupingExpr>(select.Subject);
        var subject = Assert.IsType<IdentifierExpr>(grouping.Inner);
        Assert.Equal(GrobType.Int, subject.ResolvedType);
        Assert.NotNull(subject.Declaration);

        var pattern = Assert.IsType<IdentifierExpr>(select.Cases[0].Patterns[0]);
        Assert.Equal(GrobType.Int, pattern.ResolvedType);
        Assert.NotNull(pattern.Declaration);
    }

    // -----------------------------------------------------------------------
    // Type-checker — break / continue inside select (D-315)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>break</c> inside a <c>select</c> that sits inside a <c>while</c> is E2211 —
    /// it is not retargeted at the enclosing loop (D-315).
    /// </summary>
    [Fact]
    public void Break_InSelectInsideWhile_ProducesE2211() {
        DiagnosticBag bag = TypeCheckSource("while (true) { select (1) { case 1 { break } } }");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E2211");
        Assert.DoesNotContain(bag.Errors, e => e.Code == "E2212");
    }

    /// <summary>
    /// <c>break</c> inside a <c>select</c> with no enclosing loop is E2211 — the same
    /// code, independent of loop presence (D-315).
    /// </summary>
    [Fact]
    public void Break_InSelectNoEnclosingLoop_ProducesE2211() {
        DiagnosticBag bag = TypeCheckSource("select (1) { case 1 { break } }");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E2211");
    }

    /// <summary>
    /// <c>break</c> inside a <c>select</c> nested two loops deep is still E2211 — the
    /// nearest control frame is the <c>select</c>, so the break reaches neither loop.
    /// </summary>
    [Fact]
    public void Break_InSelectInsideNestedWhiles_ProducesE2211() {
        DiagnosticBag bag = TypeCheckSource(
            "while (true) { while (true) { select (1) { case 1 { break } } } }");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E2211");
    }

    /// <summary>
    /// <c>continue</c> inside a <c>select</c> inside a <c>while</c> is legal — it
    /// passes through to the nearest enclosing loop (D-315).
    /// </summary>
    [Fact]
    public void Continue_InSelectInsideWhile_IsLegal() {
        DiagnosticBag bag = TypeCheckSource("while (true) { select (1) { case 1 { continue } } }");
        Assert.False(bag.HasErrors);
    }

    /// <summary>
    /// <c>continue</c> inside a <c>select</c> with no enclosing loop is E2212 — no
    /// loop for it to resolve to (D-315).
    /// </summary>
    [Fact]
    public void Continue_InSelectNoEnclosingLoop_ProducesE2212() {
        DiagnosticBag bag = TypeCheckSource("select (1) { case 1 { continue } }");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E2212");
    }

    /// <summary>
    /// <c>continue</c> inside a <c>select</c> inside a <c>while</c> compiles to a
    /// backward <see cref="OpCode.Loop"/> targeting the enclosing loop — the
    /// <c>select</c> frame is skipped.
    /// </summary>
    [Fact]
    public void Continue_InSelectInsideWhile_EmitsBackwardLoop() {
        Chunk chunk = CompileSource("while (true) { select (1) { case 1 { continue } } }");
        Assert.Contains(OpCode.Loop, ReadOpcodes(chunk));
    }
}
