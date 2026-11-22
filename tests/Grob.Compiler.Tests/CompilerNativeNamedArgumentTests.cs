using Grob.Core;
using Grob.Runtime;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 2 emission: a namespace-native or <c>input</c> call with named arguments
/// reaches the native as a fully bound positional list in parameter order, with each
/// skipped default emitted as its constant (D-358, D-446). An <c>int</c> argument bound
/// to a <c>float</c> parameter is widened (R-58, Chris's ruling of 22 November).
/// </summary>
public sealed class CompilerNativeNamedArgumentTests {
    private static string Render(string source, NativeCatalogue? catalogue = null) =>
        ChunkRendering.Render(ChunkRendering.CompileChecked(source, catalogue));

    // -----------------------------------------------------------------------
    // Skip and fill; either order; named equals positional.
    // -----------------------------------------------------------------------

    [Fact]
    public void RegexCompile_NamedTimeout_FillsTheSkippedFlagsDefault() =>
        ChunkRendering.AssertEmits("""r := regex.compile("a", timeout: 2)""", """
            GetGlobal [0] String "regex.compile"
            Constant [1] String "a"
            Constant [2] String ""
            Constant [3] Int 2
            Call 3
            DefineGlobal [4] String "r"
            Return
            """);

    [Fact]
    public void RegexCompile_NamedInEitherOrder_EmitsTheSameParameterOrder() {
        // Evaluation follows parameter order, as a script function's does (OQ-028).
        string forward = Render("""r := regex.compile("a", flags: "i", timeout: 2)""");
        string reverse = Render("""r := regex.compile("a", timeout: 2, flags: "i")""");
        string positional = Render("""r := regex.compile("a", "i", 2)""");

        Assert.Equal(positional, forward);
        Assert.Equal(positional, reverse);
    }

    [Fact]
    public void DateParse_NamedPattern_EmitsWhatThePositionalCallEmits() =>
        Assert.Equal(
            Render("""d := date.parse("05/04/2026", "dd/MM/yyyy")"""),
            Render("""d := date.parse("05/04/2026", pattern: "dd/MM/yyyy")"""));

    [Fact]
    public void Input_NamedPrompt_EmitsWhatThePositionalCallEmits() =>
        Assert.Equal(Render("""s := input("> ")"""), Render("""s := input(prompt: "> ")"""));

    // -----------------------------------------------------------------------
    // A plugin registered on the public surface, as a third party would.
    // -----------------------------------------------------------------------

    private sealed class PadPlugin : IGrobPlugin {
        public string Name => "acme";

        public void Register(IPluginRegistrar registrar) =>
            registrar.RegisterNative(
                new FunctionSignature("acme.pad",
                    [
                        new SignatureParameter("value", SignatureType.Of(GrobType.String)),
                        new SignatureParameter("width", SignatureType.Of(GrobType.Int), defaultValue: GrobValue.FromInt(8)),
                        new SignatureParameter("fill", SignatureType.Of(GrobType.String), defaultValue: GrobValue.FromString(" ")),
                    ],
                    SignatureType.Of(GrobType.String)),
                new NativeFunction("acme.pad", 3, (args, _) => args[0]));
    }

    [Fact]
    public void PluginNative_NamedSkipsDefault_FillsTheSkippedSlotWithItsConstant() {
        NativeCatalogue catalogue = new NativeCatalogueBuilder().Add(new PadPlugin()).Build();

        ChunkRendering.AssertEmits("""t := acme.pad("x", fill: "*")""", """
            GetGlobal [0] String "acme.pad"
            Constant [1] String "x"
            Constant [2] Int 8
            Constant [3] String "*"
            Call 3
            DefineGlobal [4] String "t"
            Return
            """, catalogue);
    }

    // -----------------------------------------------------------------------
    // R-58 native widening.
    // -----------------------------------------------------------------------

    [Fact]
    public void MathSqrt_IntArgument_IsWidenedToFloat() =>
        ChunkRendering.AssertEmits("x := math.sqrt(4)", """
            GetGlobal [0] String "math.sqrt"
            Constant [1] Int 4
            IntToFloat
            Call 1
            DefineGlobal [2] String "x"
            Return
            """);

    [Fact]
    public void MultiLineCall_Widening_RecordsTheArgumentsLine() {
        // The conversion belongs to the argument, as a binary operand's does, so a runtime
        // fault in it points at the argument's line, not the line the call opens on.
        Chunk chunk = ChunkRendering.CompileChecked("x := math.sqrt(\n    4)");

        (int offset, _, _) = Assert.Single(ChunkRendering.Instructions(chunk), i => i.Op == OpCode.IntToFloat);
        Assert.Equal(2, chunk.GetLine(offset));
    }

    [Fact]
    public void ScriptCall_IntArgumentToFloatParameter_IsWidenedToFloat() =>
        ChunkRendering.AssertEmits("""
            fn half(v: float): float {
                return v / 2.0
            }
            x := half(3)
            """, """
            Constant [0] Function fn half
            DefineGlobal [1] String "half"
            GetGlobal [1] String "half"
            Constant [2] Int 3
            IntToFloat
            Call 1
            DefineGlobal [3] String "x"
            Return
            """);
}
