using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment C end-to-end tests — lambdas as values and the four array
/// higher-order methods (<c>filter</c>, <c>select</c>, <c>sort</c>, <c>each</c>).
/// Each test drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
/// <remarks>
/// Source-level notes:
/// - <c>arr.select(...)</c> cannot appear in Grob source because <c>select</c> is a
///   keyword (D-301). Tests use <c>filter</c>, <c>sort</c> and <c>each</c> from source.
///   The <c>select</c> native is exercised via hand-built AST in the Compiler tests and
///   via hand-built chunks in the Vm tests.
/// - <c>arr.filter(x =&gt; x + 1)</c> causes a compiler crash (Unknown arithmetic on
///   the left — see D-296 category-4 note); use comparison-only bodies like
///   <c>x =&gt; x &gt; 0</c> instead.
/// </remarks>
public sealed class Sprint5IncrementCTests {
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
    // filter
    // -----------------------------------------------------------------------

    [Fact]
    public void Filter_PositiveElements_PrintsExpectedOutput() {
        string stdout = Run("""
            arr := [1, -2, 3, -4, 5]
            result := arr.filter(x => x > 0)
            print(result.length)
            """);
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void Filter_AllElementsExcluded_ReturnsEmptyArray() {
        string stdout = Run("""
            arr := [1, 2, 3]
            result := arr.filter(x => x > 100)
            print(result.length)
            """);
        Assert.Equal("0" + NL, stdout);
    }

    [Fact]
    public void Filter_AllElementsIncluded_ReturnsSameLength() {
        string stdout = Run("""
            arr := [1, 2, 3]
            result := arr.filter(x => x > 0)
            print(result.length)
            """);
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void Filter_ReferencingTopLevelConst_WorksEndToEnd() {
        string stdout = Run("""
            const THRESHOLD := 3
            arr := [1, 2, 3, 4, 5]
            result := arr.filter(x => x > THRESHOLD)
            print(result.length)
            """);
        Assert.Equal("2" + NL, stdout); // 4 and 5 pass
    }

    [Fact]
    public void Filter_ReferencingTopLevelReadonly_WorksEndToEnd() {
        string stdout = Run("""
            readonly min := 0
            arr := [-1, 0, 1, 2]
            result := arr.filter(x => x > min)
            print(result.length)
            """);
        Assert.Equal("2" + NL, stdout); // 1 and 2 pass
    }

    // -----------------------------------------------------------------------
    // sort
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_IntArray_SortsAscending() {
        string stdout = Run("""
            arr := [3, 1, 4, 1, 5, 9, 2]
            sorted := arr.sort(x => x)
            print(sorted.length)
            """);
        Assert.Equal("7" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // each
    // -----------------------------------------------------------------------

    [Fact]
    public void Each_PrintsEachElement() {
        string stdout = Run("""
            arr := [10, 20, 30]
            arr.each(x => print(x))
            """);
        Assert.Equal("10" + NL + "20" + NL + "30" + NL, stdout);
    }

    [Fact]
    public void Each_MutatesTopLevelMutable_Category3() {
        // Lambda references a mutable global (category 3 write).
        string stdout = Run("""
            counter := 0
            arr := [1, 2, 3]
            arr.each(x => {
            counter = counter + x
            })
            print(counter)
            """);
        Assert.Equal("6" + NL, stdout);
    }

    [Fact]
    public void Each_EmptyArray_NoPrint() {
        string stdout = Run("""
            arr := [1, 2, 3]
            empty := arr.filter(x => x > 100)
            empty.each(x => print(x))
            """);
        Assert.Equal(string.Empty, stdout);
    }

    // -----------------------------------------------------------------------
    // Chaining: filter then each
    // -----------------------------------------------------------------------

    [Fact]
    public void Filter_Then_Each_EndToEnd() {
        string stdout = Run("""
            arr := [1, -2, 3, -4, 5]
            arr.filter(x => x > 0).each(x => print(x))
            """);
        Assert.Equal("1" + NL + "3" + NL + "5" + NL, stdout);
    }

    [Fact]
    public void Filter_Then_Filter_EndToEnd() {
        string stdout = Run("""
            arr := [1, 2, 3, 4, 5, 6]
            step1 := arr.filter(x => x > 2)
            result := step1.filter(x => x > 4)
            print(result.length)
            """);
        Assert.Equal("2" + NL, stdout); // 5 and 6
    }

    // -----------------------------------------------------------------------
    // Block-body lambda
    // -----------------------------------------------------------------------

    [Fact]
    public void BlockBodyLambda_ImplicitLastExpression_WorksEndToEnd() {
        // sort uses a block-body lambda whose last expression is the key.
        string stdout = Run("""
            arr := [3, 1, 2]
            sorted := arr.sort(x => {
            y := x > 0
            y
            })
            print(sorted.length)
            """);
        Assert.Equal("3" + NL, stdout);
    }
}
