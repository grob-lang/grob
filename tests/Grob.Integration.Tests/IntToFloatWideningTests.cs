using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// An <c>int</c> the checker accepts where a <c>float</c> is expected runs as a <c>float</c> in
/// each position that takes one and whose target type the compiler can see (R-58, D-460). Each
/// case runs a program whose value is then divided by an <c>int</c>: a value left as an <c>int</c>
/// faults the VM, 'expected Float, actual Int', or divides as an integer and prints a different
/// number, so a fraction in the output is the proof that the value was widened.
/// </summary>
public sealed class IntToFloatWideningTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr, new StringReader(string.Empty), verbose: false).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static string[] Lines(string stdout) =>
        stdout.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    private static void AssertRuns(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    // -----------------------------------------------------------------------
    // A binding
    // -----------------------------------------------------------------------

    [Fact]
    public void GlobalBinding_IntForFloat_RunsAsFloat() =>
        AssertRuns("""
            x: float := 1
            print(x / 2)
            """, "0.5");

    [Fact]
    public void LocalBinding_IntForFloat_RunsAsFloat() =>
        AssertRuns("""
            fn f(): void {
                y: float := 3
                print(y / 2)
            }
            f()
            """, "1.5");

    [Fact]
    public void ReadonlyBinding_IntForFloat_RunsAsFloat() =>
        AssertRuns("""
            readonly r: float := 1
            print(r / 2)
            """, "0.5");

    [Fact]
    public void ConstBinding_IntForFloat_RunsAsFloat() =>
        AssertRuns("""
            const c: float := 3
            print(c / 2)
            """, "1.5");

    [Fact]
    public void Binding_IntTernaryForFloat_RunsAsFloat() =>
        AssertRuns("""
            c := true
            x: float := c ? 1 : 2
            print(x / 2)
            """, "0.5");

    // -----------------------------------------------------------------------
    // An assignment
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_IntToGlobalFloat_RunsAsFloat() =>
        AssertRuns("""
            x := 1.5
            x = 1
            print(x / 2)
            x += 2
            print(x / 2)
            """, "0.5", "1.5");

    [Fact]
    public void Assignment_IntToLocalFloat_RunsAsFloat() =>
        AssertRuns("""
            fn f(): void {
                y := 1.5
                y = 3
                print(y / 2)
                y -= 1
                print(y / 2)
            }
            f()
            """, "1.5", "1.0");

    [Fact]
    public void Assignment_IntToCapturedFloat_RunsAsFloat() =>
        AssertRuns("""
            fn make(): fn(): float {
                total := 1.5
                set := () => {
                    total = 3
                }
                set()
                return () => total
            }
            get := make()
            print(get() / 2)
            """, "1.5");

    [Fact]
    public void Assignment_IntToStructFloatField_RunsAsFloat() =>
        AssertRuns("""
            type S {
                f: float
            }
            s := S { f: 1.5 }
            s.f = 3
            print(s.f / 2)
            """, "1.5");

    [Fact]
    public void Assignment_IntToFloatArrayElement_RunsAsFloat() =>
        AssertRuns("""
            xs: float[] := [1.5, 2.5]
            xs[1] = 3
            print(xs[1] / 2)
            """, "1.5");

    [Fact]
    public void Assignment_IntToNestedFloatArrayElement_RunsAsFloat() =>
        AssertRuns("""
            rows: float[][] := [[1, 2.5], [3, 4.5]]
            rows[1][0] = 5
            print(rows[0][0] / 2)
            print(rows[1][0] / 2)
            """, "0.5", "2.5");

    [Fact]
    public void Assignment_IntToFloatParameter_RunsAsFloat() =>
        AssertRuns("""
            fn halve(v: float): float {
                v = 7
                return v / 2
            }
            print(halve(1.5))
            """, "3.5");

    // -----------------------------------------------------------------------
    // A return
    // -----------------------------------------------------------------------

    [Fact]
    public void Return_IntFromFloatFunction_RunsAsFloat() =>
        AssertRuns("""
            fn f(): float { return 1 }
            print(f() / 2)
            """, "0.5");

    [Fact]
    public void Return_IntVariableFromFloatFunction_RunsAsFloat() =>
        AssertRuns("""
            fn f(n: int): float { return n }
            print(f(3) / 2)
            """, "1.5");

    [Fact]
    public void Return_IntFromFloatFunctionInsideTryFinally_RunsAsFloat() =>
        AssertRuns("""
            fn f(): float {
                try {
                    return 1
                } finally {
                    print("done")
                }
            }
            print(f() / 2)
            """, "done", "0.5");

    // -----------------------------------------------------------------------
    // A struct field
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_IntForFloatField_RunsAsFloat() =>
        AssertRuns("""
            type S {
                f: float
            }
            s := S { f: 1 }
            print(s.f / 2)
            """, "0.5");

    [Fact]
    public void StructConstruction_IntFieldDefaultForFloatField_RunsAsFloat() =>
        AssertRuns("""
            type S {
                f: float = 1
            }
            s := S {}
            print(s.f / 2)
            """, "0.5");

    // -----------------------------------------------------------------------
    // A parameter default
    // -----------------------------------------------------------------------

    [Fact]
    public void ParameterDefault_IntForFloatParameter_RunsAsFloat() =>
        AssertRuns("""
            fn g(x: float = 1): float { return x / 2 }
            print(g())
            print(g(3))
            """, "0.5", "1.5");

    [Fact]
    public void ParameterDefault_FilledAroundANamedArgument_RunsAsFloat() =>
        AssertRuns("""
            fn g(a: float = 1, b: int = 2): float { return a / 2 + b }
            print(g(b: 5))
            """, "5.5");

    // -----------------------------------------------------------------------
    // An argument to a function value
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionValueCall_ThroughAParameter_RunsAsFloat() =>
        AssertRuns("""
            fn apply(h: fn(float): float): float { return h(3) }
            print(apply(a => a / 2.0))
            """, "1.5");

    [Fact]
    public void FunctionValueCall_ThroughATypedBinding_RunsAsFloat() =>
        AssertRuns("""
            k: fn(float): float := a => a / 2.0
            print(k(5))
            """, "2.5");

    [Fact]
    public void FunctionValueCall_ThroughAStructField_RunsAsFloat() =>
        AssertRuns("""
            type H {
                g: fn(float): float
            }
            h := H { g: a => a / 2.0 }
            print(h.g(1))
            """, "0.5");

    [Fact]
    public void FunctionValueCall_ThroughAnOptionalChainedField_RunsAsFloat() =>
        AssertRuns("""
            type H {
                g: (fn(float): float)?
            }
            h := H { g: a => a / 2.0 }
            print(h?.g(3))
            none := H { g: nil }
            print(none?.g(3))
            """, "1.5", "nil");

    [Fact]
    public void FunctionValueCall_ThroughACallResult_RunsAsFloat() =>
        AssertRuns("""
            fn mk(): fn(float): float {
                return a => a / 2.0
            }
            print(mk()(7))
            """, "3.5");

    // -----------------------------------------------------------------------
    // An array literal, a map literal and the methods of both
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayLiteral_IntBesideAFloat_RunsAsFloat() =>
        AssertRuns("""
            xs: float[] := [1, 2.5]
            print(xs[0] / 2)
            zs := [2.5, 1]
            print(zs[1] / 2)
            """, "0.5", "0.5");

    [Fact]
    public void MapLiteral_IntValueForFloatMap_RunsAsFloat() =>
        AssertRuns("""
            m := map<string, float>{ "a": 1 }
            print((m.get("a") ?? 0.0) / 2)
            """, "0.5");

    [Fact]
    public void ArrayMethods_IntForFloatElement_RunAsFloat() =>
        AssertRuns("""
            fs: float[] := [1.5]
            fs.append(3)
            fs.insert(0, 1)
            print(fs[0] / 2)
            print(fs[2] / 2)
            print(fs.contains(1))
            print(fs.contains(2))
            """, "0.5", "1.5", "true", "false");

    [Fact]
    public void MapSet_IntForFloatValue_RunsAsFloat() =>
        AssertRuns("""
            m := map<string, float>{ "a": 1.5 }
            m.set("b", 3)
            print((m.get("b") ?? 0.0) / 2)
            """, "1.5");

    // -----------------------------------------------------------------------
    // A switch's value pattern
    // -----------------------------------------------------------------------

    [Fact]
    public void SwitchValuePattern_IntPatternOnAFloatSubject_Matches() =>
        AssertRuns("""
            x := 2.0
            r := x switch {
                1 => "one",
                2 => "two",
                _ => "other"
            }
            print(r)
            """, "two");

    [Fact]
    public void SwitchValuePattern_IntPatternOnANullableFloatSubject_Matches() =>
        AssertRuns("""
            x: float? := 2.0
            r := x switch {
                1 => "one",
                2 => "two",
                nil => "none",
                _ => "other"
            }
            print(r)
            """, "two");

    // -----------------------------------------------------------------------
    // Nothing that already worked changes
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatPositions_GivenFloats_RunUnchanged() =>
        AssertRuns("""
            x: float := 1.5
            fn f(): float { return 2.5 }
            type S {
                v: float = 0.5
            }
            k: fn(float): float := a => a / 2.0
            print(x / 2)
            print(f() / 2)
            print(S {}.v / 2)
            print(k(3.0))
            """, "0.75", "1.25", "0.25", "1.5");
}
