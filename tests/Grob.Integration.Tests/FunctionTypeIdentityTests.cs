using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// An array, a struct and a map in a function type's position, the element of an array of
/// functions and the value of a map of functions are compared by their element, field and
/// value types (R-98, D-463), as a compile-time diagnostic, where <c>fs[0](2.5)</c> through
/// a <c>(fn(float): float)[]</c> holding a <c>fn(int): int</c> once stopped with an internal
/// error, 'GrobValue kind mismatch: expected Int, actual Float'. A function read out of an
/// array carries its element's type and runs.
/// </summary>
public sealed class FunctionTypeIdentityTests {
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

    private static string FirstLine(string stderr) =>
        stderr.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[0];

    private static void AssertRefused(string source, string code, int line, int column) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stdout);
        Assert.Equal(ExitCodes.CompileError, exitCode);
        Assert.StartsWith($"error[{code}]", FirstLine(stderr), StringComparison.Ordinal);
        Assert.Contains($":{line}:{column}", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("E9001", stderr, StringComparison.Ordinal);
    }

    private static void AssertRuns(string source, string expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, stdout.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'));
    }

    // -----------------------------------------------------------------------
    // The row's reproducers, refused
    // -----------------------------------------------------------------------

    [Fact]
    public void Reproducer_IntArrayFunctionForFloatArrayFunction_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fn use(g: fn(int[]): int): void {
                h: fn(float[]): int := g
                print(h([2.5]))
            }
            use((xs) => xs[0])
            """, "E0001", 2, 28);

    [Fact]
    public void Reproducer_FunctionOfOneStructForAnother_IsRefusedAtCompileTime() =>
        AssertRefused("""
            type A { x: int }
            type B { y: string }
            fn use(g: fn(A): int): void {
                h: fn(B): int := g
                print(h(B { y: "s" }))
            }
            use((a) => a.x)
            """, "E0001", 4, 22);

    [Fact]
    public void Reproducer_ArrayLiteralOfMismatchedFunction_IsRefusedAtCompileTime() =>
        AssertRefused("""
            g: fn(int): int := (n) => n + 1
            fs: (fn(float): float)[] := [g]
            print(fs[0](2.5))
            """, "E0001", 2, 29);

    [Fact]
    public void Reproducer_ArrayOfIntFunctionsForFloatFunctions_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            hs: (fn(float): float)[] := fs
            print(hs[0](2.5))
            """, "E0001", 2, 29);

    [Fact]
    public void MapOfIntFunctionsForFloatFunctions_ByAnnotation_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fn use(m: map<string, fn(int): int>): void {
                k: map<string, fn(float): float> := m
            }
            print(1)
            """, "E0001", 2, 41);

    [Fact]
    public void CallThroughElementWithAWrongArgument_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            print(fs[0](2.5))
            """, "E0004", 2, 13);

    // -----------------------------------------------------------------------
    // Matched shapes run
    // -----------------------------------------------------------------------

    [Fact]
    public void ElementBoundToItsFunctionType_Runs() =>
        AssertRuns("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            h: fn(float): float := fs[0]
            print(h(2.5))
            """, "5.0");

    [Fact]
    public void CallThroughElement_Runs() =>
        AssertRuns("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            print(fs[0](2.5))
            """, "5.0");

    [Fact]
    public void CallThroughElementWithAnIntArgument_Widens() =>
        AssertRuns("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            print(fs[0](3))
            """, "6.0");

    /// <summary>
    /// The call's result is the element's <c>float</c>, so <c>/ 2</c> divides as floats; with
    /// no shape the result was unknown and the division took the <c>int</c> operand's opcode.
    /// </summary>
    [Fact]
    public void CallThroughElement_ResultDividesAsAFloat() =>
        AssertRuns("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            print(fs[0](3.0) / 4)
            """, "1.5");

    [Fact]
    public void ForInItemCalled_Runs() =>
        AssertRuns("""
            fs: (fn(float): float)[] := [(n) => n * 2.0, (n) => n + 0.5]
            for f in fs {
                print(f(3))
            }
            """, "6.0\n3.5");

    [Fact]
    public void FirstUnwrappedAndCalled_Runs() =>
        AssertRuns("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            h: (fn(float): float)? := fs.first()
            if (h != nil) {
                print(h(1.25))
            }
            """, "2.5");

    [Fact]
    public void FunctionReturningAnArrayOfFunctions_ElementCalled_Runs() =>
        AssertRuns("""
            fn mk(): (fn(int): int)[] {
                return [(n) => n + 1, (n) => n * 10]
            }
            print(mk()[1](4))
            """, "40");

    [Fact]
    public void StructReturnThroughAFunctionValue_FieldRead_Runs() =>
        AssertRuns("""
            type A { x: int }
            fn use(g: fn(): A): int {
                return g().x
            }
            print(use(() => A { x: 4 }))
            """, "4");

    [Fact]
    public void BlockLambdaEndingInIfElseReturningTheStruct_Runs() =>
        AssertRuns("""
            type A { x: int }
            fn pick(c: bool): fn(): A {
                return () => {
                    if (c) {
                        return A { x: 1 }
                    } else {
                        return A { x: 2 }
                    }
                }
            }
            print(pick(false)().x)
            """, "2");

    [Fact]
    public void StructFieldHoldingAnArrayOfFunctions_Runs() =>
        AssertRuns("""
            type T { fs: (fn(int): int)[] }
            hs: (fn(int): int)[] := [(n) => n + 1]
            t := T { fs: hs }
            print(t.fs.length)
            """, "1");
}
