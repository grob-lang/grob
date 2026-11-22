using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 3 emission: a primitive-method call reaches the native as the receiver
/// then the checker's bound list in parameter order, each skipped or omitted default
/// emitted as its constant (D-358, D-446). The same list is emitted on the guarded path of
/// a nullable receiver.
/// </summary>
public sealed class CompilerPrimitiveNamedArgumentTests {
    private static string Render(string source) => ChunkRendering.Render(ChunkRendering.CompileChecked(source));

    [Fact]
    public void PadLeft_NamedChar_EmitsWhatThePositionalCallEmits() =>
        Assert.Equal(
            Render("s := \"abc\"\nv := s.padLeft(5, \"*\")"),
            Render("s := \"abc\"\nv := s.padLeft(5, char: \"*\")"));

    [Fact]
    public void PadLeft_OmittedChar_FillsTheDefaultAfterTheWidth() =>
        ChunkRendering.AssertEmits("""
            s := "abc"
            v := s.padLeft(5)
            """, """
            Constant [0] String "abc"
            DefineGlobal [1] String "s"
            GetGlobal [2] String "string.padLeft"
            GetGlobal [1] String "s"
            Constant [3] Int 5
            Constant [4] String " "
            Call 3
            DefineGlobal [5] String "v"
            Return
            """);

    [Fact]
    public void NullablePadLeft_NamedChar_EmitsWhatThePositionalCallEmits() {
        // Bytecode only: a non-nil nullable primitive '?.' call faults the VM (gate report
        // §1.4), so the guarded path is proven here and not at run time.
        const string Prefix = "ns: string? := \"x\"\nv := ";

        Assert.Equal(Render(Prefix + "ns?.padLeft(5, \"*\")"), Render(Prefix + "ns?.padLeft(5, char: \"*\")"));
    }

    [Fact]
    public void HandBuiltPrimitiveCall_SkippedDefault_IsEmittedAsItsConstantBetweenTheArguments() {
        // No first-party primitive has two defaulted parameters, and the registrar that
        // could add one is sealed off (D-426 Decision 3), so the skip is built by hand: a
        // call to "abc".frame(5, right: "]") against frame(width, left = "<", right = ">")
        // binds to (5, "<", "]"), the skipped slot carrying its constant.
        static SourceRange Range() => SourceRange.Unknown;
        var callee = new MemberAccessExpr(Range(), new StringLiteralExpr(Range(), "abc"), "frame");
        var call = new CallExpr(Range(), callee,
            [
                new CallArgument(Range(), null, new IntLiteralExpr(Range(), 5)),
                new CallArgument(Range(), "right", new StringLiteralExpr(Range(), "]")),
            ]) {
            ResolvedPrimitiveNativeName = "string.frame",
            ResolvedBinding = new BoundArgumentList(
                [
                    new BoundSlot(0, null),
                    new BoundSlot(BoundSlot.DefaultArgument, GrobValue.FromString("<")),
                    new BoundSlot(1, null),
                ]),
        };
        var unit = new CompilationUnit(Range(), [new ExpressionStmt(Range(), call)]);

        var bag = new DiagnosticBag();
        Chunk chunk = GrobCompiler.Compile(unit, bag);

        Assert.False(bag.HasErrors);
        Assert.Equal("""
            GetGlobal [0] String "string.frame"
            Constant [1] String "abc"
            Constant [2] Int 5
            Constant [3] String "<"
            Constant [4] String "]"
            Call 4
            Pop
            Return

            """.ReplaceLineEndings("\n"), ChunkRendering.Render(chunk));
    }
}
