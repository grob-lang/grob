using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment A end-to-end tests — positional function declarations, calls
/// and returns, including recursion. Each test drives the full pipeline:
/// Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint5IncrementATests {
    private static string Run(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"Pipeline produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        var output = new StringWriter(new StringBuilder());
        var vm = new VirtualMachine(output);
        vm.Run(chunk);
        return output.ToString();
    }

    private static string NL => Environment.NewLine;

    [Fact]
    public void DeclareAndCall_SimpleFunction_PrintsReturnValue() {
        string stdout = Run("""
            fn add(a: int, b: int): int {
                return a + b
            }
            print(add(3, 4))
            """);
        Assert.Equal($"7{NL}", stdout);
    }

    [Fact]
    public void FunctionCall_ResultUsedInExpression() {
        string stdout = Run("""
            fn square(n: int): int {
                return n * n
            }
            print(square(5) + 1)
            """);
        Assert.Equal($"26{NL}", stdout);
    }

    [Fact]
    public void ForwardReference_CallBeforeDeclaration_Runs() {
        string stdout = Run("""
            fn outer(): int {
                return inner() + 1
            }
            fn inner(): int {
                return 10
            }
            print(outer())
            """);
        Assert.Equal($"11{NL}", stdout);
    }

    [Fact]
    public void RecursiveFactorial_ComputesCorrectly() {
        string stdout = Run("""
            fn factorial(n: int): int {
                if (n <= 1) {
                    return 1
                }
                return n * factorial(n - 1)
            }
            print(factorial(5))
            """);
        Assert.Equal($"120{NL}", stdout);
    }

    [Fact]
    public void RecursiveFibonacci_ComputesCorrectly() {
        string stdout = Run("""
            fn fib(n: int): int {
                if (n < 2) {
                    return n
                }
                return fib(n - 1) + fib(n - 2)
            }
            print(fib(10))
            """);
        Assert.Equal($"55{NL}", stdout);
    }

    [Fact]
    public void Function_WithLocalVariables_Runs() {
        string stdout = Run("""
            fn compute(n: int): int {
                doubled := n * 2
                tripled := n * 3
                return doubled + tripled
            }
            print(compute(4))
            """);
        Assert.Equal($"20{NL}", stdout);
    }

    [Fact]
    public void MultipleCalls_ShareGlobalFunction() {
        string stdout = Run("""
            fn inc(n: int): int {
                return n + 1
            }
            print(inc(1))
            print(inc(inc(1)))
            """);
        Assert.Equal($"2{NL}3{NL}", stdout);
    }
}
