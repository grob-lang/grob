using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment (function types) end-to-end tests — D-326: <c>fn(T…): R</c>
/// as a first-class type reference. Each test drives the full pipeline:
/// Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint5IncrementFunctionTypeTests {
    private static string Run(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        Assert.False(bag.HasErrors,
            $"Pipeline produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var output = new StringWriter(new StringBuilder());
        var vm = new VirtualMachine(output);
        vm.Run(chunk);
        return output.ToString();
    }

    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // Test 1: canonical makeCounter — fn returning fn(): int.
    //
    // makeCounter() returns a closure over a captured local (count).
    // Each invocation of the returned closure increments count and returns it.
    // The explicit return type annotation fn(): int is the D-326 feature under test.
    // -----------------------------------------------------------------------

    [Fact]
    public void MakeCounter_CompilesAndRuns() {
        string stdout = Run("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            c := makeCounter()
            print(c())
            print(c())
            """);

        Assert.Equal($"1{NL}2{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Test 2: two independent counters — per-call capture independence.
    //
    // Each makeCounter() activation produces a distinct closure with its own
    // 'count' upvalue. Calls to a and b should not interfere with each other.
    // -----------------------------------------------------------------------

    [Fact]
    public void MakeCounter_TwoIndependentCounters() {
        string stdout = Run("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            a := makeCounter()
            b := makeCounter()
            print(a())
            print(b())
            print(a())
            """);

        Assert.Equal($"1{NL}1{NL}2{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Calls through a function-typed parameter — the higher-order shape. The call
    // used to be checked and compiled against the enclosing function.
    // -----------------------------------------------------------------------

    [Fact]
    public void CallThroughFunctionTypedParameter_RunsTheArgument() {
        string stdout = Run("""
            fn applyTo(f: fn(int): int, x: int): int {
                return f(x)
            }
            fn combine(f: fn(int, int): int, a: int, b: int): int {
                return f(a, b)
            }
            print(applyTo(n => n * 2, 21))
            print(combine((p, q) => p - q, 10, 3))
            """);

        Assert.Equal($"42{NL}7{NL}", stdout);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_WithDefaultedSibling_DoesNotEvaluateTheSiblingDefault() {
        // 'h(n)' used to be compiled as a call to 'apply', so the body evaluated apply's
        // default for h, 'identity()', as a second argument on every call. The VM accepts
        // the surplus argument silently; the print in 'identity' is what makes it visible.
        string stdout = Run("""
            fn identity(): fn(int): int {
                print("default made")
                return v => v
            }
            fn apply(n: int, h: fn(int): int = identity()): int {
                return h(n)
            }
            print(apply(21, v => v * 2))
            print(apply(21))
            """);

        Assert.Equal($"42{NL}default made{NL}21{NL}", stdout);
    }
}
