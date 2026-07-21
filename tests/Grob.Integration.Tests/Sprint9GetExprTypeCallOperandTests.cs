using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// End-to-end (lex, parse, type-check, compile, VM with stdlib plugins auto-registered)
/// value tests for the <c>GetExprType</c> arithmetic-operand completeness sweep (D-362,
/// closing D-360's <c>CallExpr</c> residue). Proves the three real sub-cases — a
/// native/stdlib call, a function-typed-variable call, and a nominal-type-method call —
/// not only pick the right typed opcode (locked at the bytecode level in
/// <c>Grob.Compiler.Tests.CompilerCallOperandTypingTests</c>) but produce the correct
/// runtime value, i.e. the int/float promotion actually happens rather than faulting or
/// silently truncating.
/// </summary>
public sealed class Sprint9GetExprTypeCallOperandTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void NativeCallAsOperand_MathSqrtPlusFloat_PromotesAndComputesCorrectly() {
        (string stdout, string stderr, int exitCode) = RunSource(
            "print(math.sqrt(4.0) + 1.0)\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("3.0" + Environment.NewLine, stdout);
    }

    [Fact]
    public void FunctionTypedVariableCallAsOperand_FloatReturn_PromotesAndComputesCorrectly() {
        (string stdout, string stderr, int exitCode) = RunSource(
            "f: fn(): float := () => 2.5\n" +
            "print(f() + 1.0)\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("3.5" + Environment.NewLine, stdout);
    }

    [Fact]
    public void NominalTypeMethodCallAsOperand_DaysUntilPlusInt_ComputesCorrectly() {
        (string stdout, string stderr, int exitCode) = RunSource(
            "a := date.of(2026, 1, 1)\n" +
            "b := date.of(2026, 1, 11)\n" +
            "print(a.daysUntil(b) + 1)\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("11" + Environment.NewLine, stdout);
    }
}
