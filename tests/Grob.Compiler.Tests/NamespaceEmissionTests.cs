using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Sprint 8 Increment A — qualified-native emission (D-342). Hand-built AST, bypassing
/// the parser and type checker entirely: the compiler's namespace-member emission branch
/// (<c>Compiler.Expressions.cs VisitMemberAccess</c>) reads only the AST's structural
/// shape (a bare-identifier target registered in <c>NamespaceRegistry</c>), never a
/// checker-set annotation, so it is testable in isolation from the checker's dispatch-
/// precedence work. <c>math.sqrt(9.0)</c> compiles to the arg <c>Constant</c>, then
/// <c>GetGlobal</c> against the qualified name <c>"math.sqrt"</c>, then the existing
/// <c>Call</c> — not a second embedded function <c>Constant</c>, since
/// <c>Grob.Compiler</c> has no reference to <c>Grob.Stdlib</c> and so cannot know a
/// native's actual C# delegate at compile time (D-342). <c>math.pi</c> compiles to a bare
/// <c>GetGlobal</c>. No new opcode.
/// </summary>
public sealed class NamespaceEmissionTests {
    private static IdentifierExpr Ident(string name) => new(SourceRange.Unknown, name);

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
        var target = new MemberAccessExpr(SourceRange.Unknown, Ident("math"), "pi");
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
        var callee = new MemberAccessExpr(SourceRange.Unknown, Ident("math"), "sqrt");
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

        var op = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var o = (OpCode)chunk.ReadByte(offset++);
            op.Add(o);
            offset += o switch {
                OpCode.GetGlobal or OpCode.GetProperty or OpCode.Constant => 1,
                _ => 0,
            };
        }
        Assert.Contains(OpCode.GetProperty, op);
    }
}
