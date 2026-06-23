using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment D end-to-end tests — closure (category-4 upvalue capture).
/// Each test drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
/// <remarks>
/// Both tests exercise a lambda that captures an enclosing-function local (category 4,
/// D-296). The first proves that mutations accumulated via <see cref="OpCode.SetUpvalue"/>
/// in a callback survive back to the enclosing function's stack slot. The second proves
/// that a parameter captured from the enclosing function is held in an upvalue and
/// filters correctly after the closure is created.
/// </remarks>
public sealed class Sprint5IncrementDTests {
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

    // -----------------------------------------------------------------------
    // Test 1: accumulation via open upvalue through each.
    //
    // The lambda passed to arr.each captures 'total' (a local of sumCapture)
    // as an upvalue. Each iteration writes total = total + x through
    // SetUpvalue, and the enclosing fn's slot is updated in-place. After
    // each finishes, return total reads the accumulated value.
    // -----------------------------------------------------------------------

    [Fact]
    public void SumCapture_AccumulatesThroughClosedUpvalue_ProducesExpectedSum() {
        string stdout = Run("""
            fn sumCapture(arr: array): int {
              total := 0
              arr.each(x => {
                total = total + x
              })
              return total
            }
            result := sumCapture([1, 2, 3])
            print(result)
            """);

        Assert.Equal("6" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Test 2: capturing a function parameter as an upvalue.
    //
    // filterAbove captures 'threshold' (a parameter of filterAbove, which is
    // therefore a category-4 local in that frame) from the lambda passed to
    // arr.filter. The lambda compares each element against the captured
    // threshold and filters accordingly.
    // -----------------------------------------------------------------------

    [Fact]
    public void FilterAbove_CapturingThresholdParameter_FiltersCorrectly() {
        string stdout = Run("""
            fn filterAbove(arr: array, threshold: int): array {
              return arr.filter(x => x > threshold)
            }
            filtered := filterAbove([1, 5, 3, 8, 2], 4)
            print(filtered.length)
            """);

        // Elements of [1, 5, 3, 8, 2] that are > 4 are 5 and 8 → 2 elements.
        Assert.Equal("2" + NL, stdout);
    }
}
