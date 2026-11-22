using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Byte-identity characterisation of call-site emission (native named arguments,
/// gate report §1.5). Each test renders the whole top-level chunk, so every byte of
/// it appears in the expected text, and pins what a complete all-positional call
/// (or a call that omits a trailing default) emitted before the shared binder
/// existed. D-446 requires those calls to stay byte-identical when named arguments
/// start binding on native paths, so these expectations must not change. Rendering is
/// <see cref="ChunkRendering"/>'s.
/// </summary>
public sealed class CompilerCallEmissionCharacterisationTests {
    private static void AssertEmits(string source, string expected) => ChunkRendering.AssertEmits(source, expected);

    // -----------------------------------------------------------------------
    // Script functions — complete positional, omitted default, named reorder.
    // -----------------------------------------------------------------------

    private const string ScriptFn = """
        fn f(a: int, b: int = 99, c: bool = true): int {
            return a + b
        }

        """;

    [Fact]
    public void ScriptCall_CompletePositional_EmitsPinnedBytes() =>
        AssertEmits(ScriptFn + "f(1, 2, false)", """
            Constant [0] Function fn f
            DefineGlobal [1] String "f"
            GetGlobal [1] String "f"
            Constant [2] Int 1
            Constant [3] Int 2
            False
            Call 3
            Pop
            Return
            """);

    [Fact]
    public void ScriptCall_OmittedDefaults_EmitsPinnedBytes() =>
        AssertEmits(ScriptFn + "f(1)", """
            Constant [0] Function fn f
            DefineGlobal [1] String "f"
            GetGlobal [1] String "f"
            Constant [2] Int 1
            Constant [3] Int 99
            True
            Call 3
            Pop
            Return
            """);

    [Fact]
    public void ScriptCall_NamedReorder_EmitsPinnedBytes() =>
        AssertEmits(ScriptFn + "f(1, c: false, b: 5)", """
            Constant [0] Function fn f
            DefineGlobal [1] String "f"
            GetGlobal [1] String "f"
            Constant [2] Int 1
            Constant [3] Int 5
            False
            Call 3
            Pop
            Return
            """);

    // -----------------------------------------------------------------------
    // Namespace natives and input.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceCall_MathSqrt_EmitsPinnedBytes() =>
        AssertEmits("x := math.sqrt(2.0)", """
            GetGlobal [0] String "math.sqrt"
            Constant [1] Float 2
            Call 1
            DefineGlobal [2] String "x"
            Return
            """);

    [Fact]
    public void NamespaceCall_DateParseOneArgument_EmitsPinnedBytes() =>
        AssertEmits("""d := date.parse("2026-04-05")""", """
            GetGlobal [0] String "date.parse"
            Constant [1] String "2026-04-05"
            Constant [2] String ""
            Call 2
            DefineGlobal [3] String "d"
            Return
            """);

    [Fact]
    public void NamespaceCall_DateParseTwoArguments_EmitsPinnedBytes() =>
        AssertEmits("""d := date.parse("05/04/2026", "dd/MM/yyyy")""", """
            GetGlobal [0] String "date.parse"
            Constant [1] String "05/04/2026"
            Constant [2] String "dd/MM/yyyy"
            Call 2
            DefineGlobal [3] String "d"
            Return
            """);

    [Fact]
    public void NamespaceCall_RegexCompileOneArgument_EmitsPinnedBytes() =>
        AssertEmits("""r := regex.compile("a+")""", """
            GetGlobal [0] String "regex.compile"
            Constant [1] String "a+"
            Constant [2] String ""
            Constant [3] Int 5
            Call 3
            DefineGlobal [4] String "r"
            Return
            """);

    [Fact]
    public void NamespaceCall_RegexCompileTwoArguments_EmitsPinnedBytes() =>
        AssertEmits("""r := regex.compile("a+", "i")""", """
            GetGlobal [0] String "regex.compile"
            Constant [1] String "a+"
            Constant [2] String "i"
            Constant [3] Int 5
            Call 3
            DefineGlobal [4] String "r"
            Return
            """);

    [Fact]
    public void NamespaceCall_RegexCompileThreeArguments_EmitsPinnedBytes() =>
        AssertEmits("""r := regex.compile("a+", "i", 2)""", """
            GetGlobal [0] String "regex.compile"
            Constant [1] String "a+"
            Constant [2] String "i"
            Constant [3] Int 2
            Call 3
            DefineGlobal [4] String "r"
            Return
            """);

    [Fact]
    public void VariadicNamespaceCall_PathJoinTwoArguments_EmitsPinnedBytes() =>
        AssertEmits("""p := path.join("a", "b")""", """
            GetGlobal [0] String "path.join"
            Constant [1] String "a"
            Constant [2] String "b"
            Call 2
            DefineGlobal [3] String "p"
            Return
            """);

    [Fact]
    public void VariadicNamespaceCall_PathJoinThreeArguments_EmitsPinnedBytes() =>
        AssertEmits("""p := path.join("a", "b", "c")""", """
            GetGlobal [0] String "path.join"
            Constant [1] String "a"
            Constant [2] String "b"
            Constant [3] String "c"
            Call 3
            DefineGlobal [4] String "p"
            Return
            """);

    [Fact]
    public void InputCall_NoArgument_EmitsPinnedBytes() =>
        AssertEmits("s := input()", """
            GetGlobal [0] String "input"
            Constant [1] String ""
            Call 1
            DefineGlobal [2] String "s"
            Return
            """);

    [Fact]
    public void InputCall_OneArgument_EmitsPinnedBytes() =>
        AssertEmits("""s := input("> ")""", """
            GetGlobal [0] String "input"
            Constant [1] String "> "
            Call 1
            DefineGlobal [2] String "s"
            Return
            """);

    // -----------------------------------------------------------------------
    // Primitive and named-type methods, including ?. receivers.
    // -----------------------------------------------------------------------

    [Fact]
    public void PrimitiveCall_PadLeftOmittedDefault_EmitsPinnedBytes() =>
        AssertEmits("""t := "x".padLeft(5)""", """
            GetGlobal [0] String "string.padLeft"
            Constant [1] String "x"
            Constant [2] Int 5
            Constant [3] String " "
            Call 3
            DefineGlobal [4] String "t"
            Return
            """);

    [Fact]
    public void NamedTypeCall_AddDays_EmitsPinnedBytes() =>
        AssertEmits("""
            d := date.parse("2026-04-05")
            e := d.addDays(1)
            """, """
            GetGlobal [0] String "date.parse"
            Constant [1] String "2026-04-05"
            Constant [2] String ""
            Call 2
            DefineGlobal [3] String "d"
            GetGlobal [3] String "d"
            GetProperty [4] String "addDays"
            Constant [5] Int 1
            Call 1
            DefineGlobal [6] String "e"
            Return
            """);

    [Fact]
    public void NamedTypeCall_RegexReplaceString_EmitsPinnedBytes() =>
        AssertEmits("""
            r := regex.compile("a")
            t := r.replace("banana", "o")
            """, """
            GetGlobal [0] String "regex.compile"
            Constant [1] String "a"
            Constant [2] String ""
            Constant [3] Int 5
            Call 3
            DefineGlobal [4] String "r"
            GetGlobal [4] String "r"
            GetProperty [5] String "replace"
            Constant [6] String "banana"
            Constant [7] String "o"
            Call 2
            DefineGlobal [8] String "t"
            Return
            """);

    [Fact]
    public void NamedTypeCall_RegexReplaceFunction_EmitsPinnedBytes() =>
        AssertEmits("""
            r := regex.compile("a")
            t := r.replace("banana", m => m.value)
            """, """
            GetGlobal [0] String "regex.compile"
            Constant [1] String "a"
            Constant [2] String ""
            Constant [3] Int 5
            Call 3
            DefineGlobal [4] String "r"
            GetGlobal [4] String "r"
            GetProperty [5] String "replace"
            Constant [6] String "banana"
            Constant [7] Function fn <lambda>
            Call 2
            DefineGlobal [8] String "t"
            Return
            """);

    [Fact]
    public void OptionalChainCall_NamedTypeAddDays_EmitsPinnedBytes() =>
        AssertEmits("""
            nd: date? := date.parse("2026-04-05")
            e := nd?.addDays(1)
            """, """
            GetGlobal [0] String "date.parse"
            Constant [1] String "2026-04-05"
            Constant [2] String ""
            Call 2
            DefineGlobal [3] String "nd"
            GetGlobal [3] String "nd"
            IsNil
            JumpIfTrue 6
            Pop
            GetProperty [4] String "addDays"
            Jump 1
            Pop
            IsNil
            JumpIfTrue 8
            Pop
            Constant [5] Int 1
            Call 1
            Jump 1
            Pop
            DefineGlobal [6] String "e"
            Return
            """);

    [Fact]
    public void OptionalChainCall_PrimitivePadLeft_EmitsPinnedBytes() =>
        AssertEmits("""
            ns: string? := "x"
            t := ns?.padLeft(5)
            """, """
            Constant [0] String "x"
            DefineGlobal [1] String "ns"
            GetGlobal [1] String "ns"
            IsNil
            JumpIfTrue 6
            Pop
            GetProperty [2] String "padLeft"
            Jump 1
            Pop
            IsNil
            JumpIfTrue 8
            Pop
            Constant [3] Int 5
            Call 1
            Jump 1
            Pop
            DefineGlobal [4] String "t"
            Return
            """);
}
