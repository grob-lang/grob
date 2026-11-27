using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Two function types are compared invariantly at every parameter and return position
/// (R-97, D-461): <c>fn(int): int</c> is refused for <c>fn(float): float</c>, and the reverse,
/// as a compile-time diagnostic, where it once type-checked and the call through the wider
/// type stopped with an internal error, 'GrobValue kind mismatch: expected Int, actual Float'.
/// </summary>
public sealed class FunctionTypeInvarianceTests {
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

    [Fact]
    public void Reproducer_IntFunctionBoundToFloatFunction_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fn use(g: fn(int): int): void {
                h: fn(float): float := g
                print(h(2.5))
            }
            use((n) => n + 1)
            """, "E0001", 2, 28);

    [Fact]
    public void BindingReassignedWithDeclaredReturnFunction_IsRefusedForAFloatReturnAtCompileTime() =>
        AssertRefused("""
            fn id(f: fn(): int): fn(): int {
                return f
            }
            k := () => 1
            k = id(() => 2)
            h: fn(): float := k
            print(h() / 2.0)
            """, "E0001", 6, 19);

    [Fact]
    public void FloatFunctionBoundToIntFunction_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fn use(g: fn(float): float): void {
                h: fn(int): int := g
                print(h(2))
            }
            use((n) => n * 2.0)
            """, "E0001", 2, 24);

    [Fact]
    public void IntFunctionPassedToFloatFunctionParameter_IsRefusedAtCompileTime() =>
        AssertRefused("""
            fn take(f: fn(float): float): float {
                return f(2.5)
            }
            g: fn(int): int := (n) => n + 1
            print(take(g))
            """, "E0004", 5, 12);

    [Fact]
    public void ExactFunctionType_StillRuns() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            fn use(g: fn(float): float): void {
                h: fn(float): float := g
                print(h(2.5))
            }
            use((n) => n * 2.0)
            """);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal("5.0", stdout.Trim());
    }

    [Fact]
    public void IntArgumentToFloatFunctionValue_StillWidens() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            h: fn(float): float := (n) => n * 2.0
            print(h(3))
            """);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal("6.0", stdout.Trim());
    }
}
