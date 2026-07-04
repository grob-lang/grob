using System.Text;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 6 Increment D end-to-end tests — anonymous struct literals.
/// Drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// Covers construction, field access, projection through .select(), and
/// nested anonymous structs.
/// </summary>
public sealed class Sprint6IncrementDTests {
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
    // Basic construction and field access
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_ConstructAndPrint_ContainsFieldValues() {
        string stdout = Run("""
            readonly p := #{ name: "Alice", salary: 50000 }
            print(p.name)
            print(p.salary)
            """);

        Assert.Contains("Alice", stdout);
        Assert.Contains("50000", stdout);
    }

    // -----------------------------------------------------------------------
    // Projection through .select()
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_SelectProjection_FieldsAccessible() {
        string stdout = Run("""
            readonly employees := [
              #{ name: "Alice", salary: 50000 },
              #{ name: "Bob",   salary: 60000 }
            ]
            readonly projected := employees.select(e => #{ name: e.name, salary: e.salary })
            for item in projected {
              print(item.name)
            }
            """);

        Assert.Contains("Alice", stdout);
        Assert.Contains("Bob", stdout);
    }

    // -----------------------------------------------------------------------
    // Nested anonymous structs
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_Nested_DeepFieldAccessible() {
        string stdout = Run("""
            readonly body := #{ properties: #{ mode: "Incremental" } }
            print(body.properties.mode)
            """);

        Assert.Contains("Incremental", stdout);
    }

    // -----------------------------------------------------------------------
    // Anonymous struct in a lambda (inline construction via .select)
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_InLambda_ConstructedAndReturned() {
        // fn declarations can't annotate anonymous-struct return types (D-327),
        // so this tests anonymous struct construction inside an inline lambda.
        string stdout = Run("""
            readonly labels := ["test"]
            readonly items := labels.select(s => #{ label: s, count: 0 })
            for item in items {
              print(item.label)
              print(item.count)
            }
            """);

        Assert.Contains("test", stdout);
        Assert.Contains("0", stdout);
    }
}
