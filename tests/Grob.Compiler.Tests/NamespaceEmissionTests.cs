using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Sprint 8 Increment A — qualified-native emission (D-342). Hand-built AST, bypassing
/// the parser and type checker: the compiler's namespace-member emission branch
/// (<c>Compiler.Expressions.cs VisitMemberAccess</c>) reads the type checker's own
/// resolution — <c>node.Target.Declaration is NamespaceDecl</c> — rather than
/// re-deriving "is this a namespace" from the bare identifier name, so a shadowed local
/// with the same name as a namespace is not misidentified (PR #127 review). These
/// fixtures set <see cref="IdentifierExpr.Declaration"/> by hand via
/// <see cref="NamespaceIdent"/> to stand in for that checker annotation, so the
/// emission shape is still testable in isolation from the checker's dispatch-precedence
/// work. <c>math.sqrt(9.0)</c> compiles to the arg <c>Constant</c>, then
/// <c>GetGlobal</c> against the qualified name <c>"math.sqrt"</c>, then the existing
/// <c>Call</c> — not a second embedded function <c>Constant</c>, since
/// <c>Grob.Compiler</c> has no reference to <c>Grob.Stdlib</c> and so cannot know a
/// native's actual C# delegate at compile time (D-342). <c>math.pi</c> compiles to a bare
/// <c>GetGlobal</c>. No new opcode.
/// </summary>
public sealed class NamespaceEmissionTests {
    private static IdentifierExpr Ident(string name) => new(SourceRange.Unknown, name);

    /// <summary>An identifier annotated as a genuine namespace receiver, mirroring what
    /// <c>TryAnnotateNamespaceReceiver</c> sets during type-checking.</summary>
    private static IdentifierExpr NamespaceIdent(string name) =>
        new(SourceRange.Unknown, name) { Declaration = new NamespaceDecl(name) };

    private readonly record struct Instr(OpCode Op, int Arg);

    /// <summary>Decodes a chunk into a flat instruction list, resolving string-constant operands.</summary>
    private static List<Instr> Decode(Chunk chunk) {
        var result = new List<Instr>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            int arg = 0;
            switch (op) {
                case OpCode.Constant:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.DefineGlobal:
                case OpCode.Call:
                case OpCode.GetProperty:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                case OpCode.Return:
                case OpCode.Pop:
                case OpCode.Nil:
                    break;
                default:
                    throw new InvalidOperationException($"Decode: unhandled opcode {op} in this test's fixtures.");
            }
            result.Add(new Instr(op, arg));
        }
        return result;
    }

    [Fact]
    public void MathPi_CompilesToBareGetGlobalAgainstQualifiedName() {
        var target = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("math"), "pi");
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, target)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        // GetGlobal "math.pi", Pop (ExpressionStmt discards its value), Return.
        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal);
        Assert.Equal("math.pi", chunk.ReadConstant(getGlobal.Arg).AsString());
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.Constant);
    }

    [Fact]
    public void MathSqrt_Call_DisassemblesToArgConstant_GetGlobal_Call() {
        var callee = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("math"), "sqrt");
        var call = new CallExpr(SourceRange.Unknown, callee,
            [new CallArgument(SourceRange.Unknown, null, new FloatLiteralExpr(SourceRange.Unknown, 9.0))]);
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, call)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        // Order: GetGlobal "math.sqrt" (callee pushed first), Constant 9.0 (arg),
        // Call 1, Pop, Return — matches OpCode.Call's contract (callee below its
        // arguments), the same order a plain top-level function call already uses.
        Assert.Equal(OpCode.GetGlobal, instrs[0].Op);
        Assert.Equal("math.sqrt", chunk.ReadConstant(instrs[0].Arg).AsString());

        Assert.Equal(OpCode.Constant, instrs[1].Op);
        Assert.Equal(GrobValue.FromFloat(9.0), chunk.ReadConstant(instrs[1].Arg));

        Assert.Equal(OpCode.Call, instrs[2].Op);
        Assert.Equal(1, instrs[2].Arg);

        // Exactly one Constant in the whole chunk — the argument. No second
        // embedded-function Constant: GetGlobal is the qualified-native reference.
        Assert.Single(instrs, i => i.Op == OpCode.Constant);
    }

    [Fact]
    public void IntMin_Call_DisassemblesToTwoArgConstants_GetGlobal_CallTwo() {
        // Sprint 9 Increment A1b (D-370): int.min(a, b) is a plain 2-arity namespace-native
        // call — the exact same GetGlobal + args + Call shape math.sqrt already proves, just
        // with two arguments instead of one.
        var callee = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("int"), "min");
        var call = new CallExpr(SourceRange.Unknown, callee, [
            new CallArgument(SourceRange.Unknown, null, new IntLiteralExpr(SourceRange.Unknown, 3)),
            new CallArgument(SourceRange.Unknown, null, new IntLiteralExpr(SourceRange.Unknown, 5)),
        ]);
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, call)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        // Full chunk contract: callee GetGlobal, both arg Constants in order, Call 2, then
        // the ExpressionStmt Pop and the trailing Return — no extra or misplaced bytecode.
        Assert.Equal(6, instrs.Count);

        Assert.Equal(OpCode.GetGlobal, instrs[0].Op);
        Assert.Equal("int.min", chunk.ReadConstant(instrs[0].Arg).AsString());

        Assert.Equal(OpCode.Constant, instrs[1].Op);
        Assert.Equal(GrobValue.FromInt(3), chunk.ReadConstant(instrs[1].Arg));
        Assert.Equal(OpCode.Constant, instrs[2].Op);
        Assert.Equal(GrobValue.FromInt(5), chunk.ReadConstant(instrs[2].Arg));

        Assert.Equal(OpCode.Call, instrs[3].Op);
        Assert.Equal(2, instrs[3].Arg);

        Assert.Equal(OpCode.Pop, instrs[4].Op);
        Assert.Equal(OpCode.Return, instrs[5].Op);

        // Exactly two Constants — both supplied arguments, no embedded-function Constant.
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.Constant));
    }

    [Fact]
    public void FloatClamp_Call_DisassemblesToThreeArgConstants_GetGlobal_CallThree() {
        // The three-arity sibling — proves the shape generalises to clamp's arg count too.
        var callee = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("float"), "clamp");
        var call = new CallExpr(SourceRange.Unknown, callee, [
            new CallArgument(SourceRange.Unknown, null, new FloatLiteralExpr(SourceRange.Unknown, 1.5)),
            new CallArgument(SourceRange.Unknown, null, new FloatLiteralExpr(SourceRange.Unknown, 0.0)),
            new CallArgument(SourceRange.Unknown, null, new FloatLiteralExpr(SourceRange.Unknown, 1.0)),
        ]);
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, call)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        // Full chunk contract: callee GetGlobal, all three arg Constants in order, Call 3,
        // then the ExpressionStmt Pop and the trailing Return — no extra or misplaced bytecode.
        Assert.Equal(7, instrs.Count);

        Assert.Equal(OpCode.GetGlobal, instrs[0].Op);
        Assert.Equal("float.clamp", chunk.ReadConstant(instrs[0].Arg).AsString());

        Assert.Equal(OpCode.Constant, instrs[1].Op);
        Assert.Equal(GrobValue.FromFloat(1.5), chunk.ReadConstant(instrs[1].Arg));
        Assert.Equal(OpCode.Constant, instrs[2].Op);
        Assert.Equal(GrobValue.FromFloat(0.0), chunk.ReadConstant(instrs[2].Arg));
        Assert.Equal(OpCode.Constant, instrs[3].Op);
        Assert.Equal(GrobValue.FromFloat(1.0), chunk.ReadConstant(instrs[3].Arg));

        Assert.Equal(OpCode.Call, instrs[4].Op);
        Assert.Equal(3, instrs[4].Arg);

        Assert.Equal(OpCode.Pop, instrs[5].Op);
        Assert.Equal(OpCode.Return, instrs[6].Op);

        // No second embedded-function Constant — GetGlobal is the qualified-native reference,
        // exactly three Constants (the three arguments).
        Assert.Equal(3, instrs.Count(i => i.Op == OpCode.Constant));
    }

    [Fact]
    public void GuidNamespacesDns_CompilesToBareGetGlobalAgainstFullyQualifiedName() {
        // Sprint 8 Increment D — the two-level namespace chain guid.namespaces.dns
        // compiles to a single GetGlobal against the fully qualified flat key
        // "guid.namespaces.dns" (CodeRabbit review, PR #133 — this branch previously
        // had only downstream runtime evidence, no dedicated compiler emission test).
        var inner = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("guid"), "namespaces");
        var target = new MemberAccessExpr(SourceRange.Unknown, inner, "dns");
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, target)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal);
        Assert.Equal("guid.namespaces.dns", chunk.ReadConstant(getGlobal.Arg).AsString());
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.GetProperty);
    }

    [Fact]
    public void NonNamespaceMemberAccess_StillEmitsGetProperty_Unaffected() {
        // Regression: a receiver that is NOT a registered namespace name (here,
        // "someVar" — an ordinary, unregistered identifier standing in for a struct
        // value, since the type checker isn't run in this fixture) must still take
        // the pre-existing GetProperty path, not the new namespace GetGlobal path.
        // "someVar" itself still compiles to a GetGlobal load (EmitLoad's normal
        // fallback for a name not found in local scopes) — that GetGlobal is
        // incidental to loading the receiver, not the namespace-emission branch
        // under test, so this asserts GetProperty is present rather than asserting
        // GetGlobal's absence.
        var target = new MemberAccessExpr(SourceRange.Unknown, Ident("someVar"), "length");
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, target)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        Assert.Contains(instrs, i => i.Op == OpCode.GetProperty);
    }

    [Fact]
    public void DateParse_OneArgument_SynthesisesEmptyPatternDefault_CallArityTwo() {
        // D-358: date.parse's optional trailing pattern parameter. A 1-argument source
        // call under-supplies the native's full arity (2) — the compiler's default-fill
        // branch (Compiler.Expressions.cs VisitCall) must synthesise the declared ""
        // default as a second Constant, after the supplied argument and before Call,
        // so the runtime native (Grob.Stdlib.DatePlugin, arity 2) always receives both.
        var callee = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("date"), "parse");
        var call = new CallExpr(SourceRange.Unknown, callee,
            [new CallArgument(SourceRange.Unknown, null, new StringLiteralExpr(SourceRange.Unknown, "2026-04-05"))]);
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, call)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        // Full chunk contract: callee GetGlobal, the supplied arg Constant, the synthesised
        // "" default Constant, Call 2, then the ExpressionStmt Pop and the trailing Return —
        // no extra or misplaced bytecode.
        Assert.Equal(6, instrs.Count);

        Assert.Equal(OpCode.GetGlobal, instrs[0].Op);
        Assert.Equal("date.parse", chunk.ReadConstant(instrs[0].Arg).AsString());

        Assert.Equal(OpCode.Constant, instrs[1].Op);
        Assert.Equal(GrobValue.FromString("2026-04-05"), chunk.ReadConstant(instrs[1].Arg));

        Assert.Equal(OpCode.Constant, instrs[2].Op);
        Assert.Equal(GrobValue.FromString(""), chunk.ReadConstant(instrs[2].Arg));

        Assert.Equal(OpCode.Call, instrs[3].Op);
        Assert.Equal(2, instrs[3].Arg);

        Assert.Equal(OpCode.Pop, instrs[4].Op);
        Assert.Equal(OpCode.Return, instrs[5].Op);
    }

    [Fact]
    public void DateParse_TwoArguments_CompilesBothSupplied_NoSynthesisedConstant() {
        var callee = new MemberAccessExpr(SourceRange.Unknown, NamespaceIdent("date"), "parse");
        var call = new CallExpr(SourceRange.Unknown, callee, [
            new CallArgument(SourceRange.Unknown, null, new StringLiteralExpr(SourceRange.Unknown, "05/04/2026")),
            new CallArgument(SourceRange.Unknown, null, new StringLiteralExpr(SourceRange.Unknown, "dd/MM/yyyy")),
        ]);
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, call)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        // Full chunk contract: callee GetGlobal, both supplied arg Constants, Call 2, then
        // the ExpressionStmt Pop and the trailing Return — no third synthesised default and
        // no extra or misplaced bytecode.
        Assert.Equal(6, instrs.Count);

        Assert.Equal(OpCode.GetGlobal, instrs[0].Op);
        Assert.Equal("date.parse", chunk.ReadConstant(instrs[0].Arg).AsString());
        Assert.Equal(OpCode.Constant, instrs[1].Op);
        Assert.Equal(GrobValue.FromString("05/04/2026"), chunk.ReadConstant(instrs[1].Arg));
        Assert.Equal(OpCode.Constant, instrs[2].Op);
        Assert.Equal(GrobValue.FromString("dd/MM/yyyy"), chunk.ReadConstant(instrs[2].Arg));
        Assert.Equal(OpCode.Call, instrs[3].Op);
        Assert.Equal(2, instrs[3].Arg);
        Assert.Equal(OpCode.Pop, instrs[4].Op);
        Assert.Equal(OpCode.Return, instrs[5].Op);

        // Exactly two Constants — both supplied, no third synthesised default.
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.Constant));
    }

    [Fact]
    public void ShadowedNamespaceName_Declaration_IsNotNamespaceDecl_EmitsGetPropertyNotGetGlobal() {
        // Regression (PR #127 review): a local/parameter named "math" that the type
        // checker resolved to something other than the namespace (Declaration is not
        // NamespaceDecl — simulated here via the plain Ident helper, whose Declaration
        // is left null) must still emit GetProperty, not a GetGlobal against a
        // "math.pi"-shaped qualified name. Before the fix this branch matched on the
        // bare identifier name alone and would have emitted the wrong bytecode even
        // though the type checker had already resolved the receiver correctly.
        var target = new MemberAccessExpr(SourceRange.Unknown, Ident("math"), "pi");
        var unit = new CompilationUnit(SourceRange.Unknown,
            [new ExpressionStmt(SourceRange.Unknown, target)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);

        List<Instr> instrs = Decode(chunk);
        Assert.Contains(instrs, i => i.Op == OpCode.GetProperty);
        Assert.DoesNotContain(instrs, i =>
            i.Op == OpCode.GetGlobal && chunk.ReadConstant(i.Arg).AsString() == "math.pi");
    }
}
