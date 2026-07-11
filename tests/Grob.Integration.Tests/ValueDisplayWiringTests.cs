using System.Text;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Pre-Sprint-8 interlude Increment C end-to-end tests (D-336): <c>print()</c> and
/// string interpolation routed through <c>ValueDisplay</c>. Covers the float-round-trip
/// invariant, which no sprint-close smoke gold master exercises.
/// </summary>
public sealed class ValueDisplayWiringTests {
    private static string Run(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
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

    [Fact]
    public void Print_Float_EmitsDecimalPoint() {
        string stdout = Run("print(1.0)");

        Assert.Equal($"1.0{NL}", stdout);
    }

    [Fact]
    public void Interpolation_Float_EmitsDecimalPoint() {
        string stdout = Run("""print("${1.0}")""");

        Assert.Equal($"1.0{NL}", stdout);
    }
}
