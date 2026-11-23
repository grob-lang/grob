using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 4 emission: a named-type method call reaches the native as the bound
/// positional list in parameter order, each skipped or omitted default emitted as its
/// constant (D-358, D-446), on the plain path and on a nullable receiver's guarded path.
/// An <c>int</c> argument bound to a <c>float</c> parameter is widened (R-58). The type is
/// <c>box</c>, registered through the public surface as a third-party plugin would.
/// </summary>
public sealed class CompilerNamedTypeNamedArgumentTests {
    private static string Render(string source) =>
        ChunkRendering.Render(ChunkRendering.CompileChecked(source, MemberNamedArgumentFixtures.NamedType));

    private const string Prefix = "b := acmebox.make()\nv := ";

    [Fact]
    public void Fit_NamedSkipsDefault_FillsTheSkippedSlotWithItsConstant() =>
        ChunkRendering.AssertEmits(Prefix + """b.fit(5, right: "]")""", """
            GetGlobal [0] String "acmebox.make"
            Call 0
            DefineGlobal [1] String "b"
            GetGlobal [1] String "b"
            GetProperty [2] String "fit"
            Constant [3] Int 5
            Constant [4] String "<"
            Constant [5] String "]"
            Call 3
            DefineGlobal [6] String "v"
            Return
            """, MemberNamedArgumentFixtures.NamedType);

    [Fact]
    public void Fit_OmittedDefaults_FillsBothAfterTheWidth() =>
        ChunkRendering.AssertEmits(Prefix + "b.fit(5)", """
            GetGlobal [0] String "acmebox.make"
            Call 0
            DefineGlobal [1] String "b"
            GetGlobal [1] String "b"
            GetProperty [2] String "fit"
            Constant [3] Int 5
            Constant [4] String "<"
            Constant [5] String ">"
            Call 3
            DefineGlobal [6] String "v"
            Return
            """, MemberNamedArgumentFixtures.NamedType);

    [Fact]
    public void Fit_NamedInEitherOrder_EmitsTheSameParameterOrder() {
        // Evaluation follows parameter order, as a script function's does (OQ-028).
        string forward = Render(Prefix + """b.fit(5, left: "[", right: "]")""");
        string reverse = Render(Prefix + """b.fit(5, right: "]", left: "[")""");
        string positional = Render(Prefix + """b.fit(5, "[", "]")""");

        Assert.Equal(positional, forward);
        Assert.Equal(positional, reverse);
    }

    [Fact]
    public void Scale_IntArgument_IsWidenedToFloat() =>
        ChunkRendering.AssertEmits(Prefix + "b.scale(2)", """
            GetGlobal [0] String "acmebox.make"
            Call 0
            DefineGlobal [1] String "b"
            GetGlobal [1] String "b"
            GetProperty [2] String "scale"
            Constant [3] Int 2
            IntToFloat
            Call 1
            DefineGlobal [4] String "v"
            Return
            """, MemberNamedArgumentFixtures.NamedType);

    [Fact]
    public void NullableFit_NamedSkipsDefault_EmitsWhatThePositionalCallEmits() {
        const string Nullable = "nb: box? := acmebox.make()\nv := ";

        string named = ChunkRendering.Render(ChunkRendering.CompileChecked(
            Nullable + """nb?.fit(5, right: "]")""", MemberNamedArgumentFixtures.NamedType));
        string positional = ChunkRendering.Render(ChunkRendering.CompileChecked(
            Nullable + """nb?.fit(5, "<", "]")""", MemberNamedArgumentFixtures.NamedType));

        Assert.Equal(positional, named);
    }

    [Fact]
    public void Pick_LambdaSecond_EmitsTheFunctionOverloadsArguments() =>
        // The overload is chosen on positional arguments; the call emits as any other,
        // the string then the lambda, in parameter order.
        ChunkRendering.AssertEmits(Prefix + """b.pick("x", s => s)""", """
            GetGlobal [0] String "acmebox.make"
            Call 0
            DefineGlobal [1] String "b"
            GetGlobal [1] String "b"
            GetProperty [2] String "pick"
            Constant [3] String "x"
            Constant [4] Function fn <lambda>
            Call 2
            DefineGlobal [5] String "v"
            Return
            """, MemberNamedArgumentFixtures.NamedType);
}
