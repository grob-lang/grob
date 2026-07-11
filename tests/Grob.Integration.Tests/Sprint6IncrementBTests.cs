using System.Text;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 6 Increment B end-to-end tests — named struct construction with
/// required-field validation, field defaults and the runtime struct value.
/// Each test drives the full pipeline: Lexer → Parser → TypeChecker →
/// Compiler → VM.
/// </summary>
public sealed class Sprint6IncrementBTests {
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
    // Basic construction — required fields only
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_RequiredFields_PrintsCorrectly() {
        string stdout = Run("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            print(c)
            print(c.host)
            print(c.port)
            """);

        // print() on the struct renders the full ValueDisplay form (D-336) —
        // named fields, mirroring the construction syntax.
        Assert.Equal($"Config {{ host: \"example.com\", port: 8080 }}{NL}example.com{NL}8080{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Field default — omitted uses default, supplied overrides
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_FieldDefault_OmittedUsesDefault() {
        string stdout = Run("""
            type Config {
            host: string
            port: int = 80
            }
            readonly c := Config { host: "localhost" }
            print(c.port)
            """);

        Assert.Equal($"80{NL}", stdout);
    }

    [Fact]
    public void StructConstruction_FieldDefault_SuppliedOverridesDefault() {
        string stdout = Run("""
            type Config {
            host: string
            port: int = 80
            }
            readonly c := Config { host: "localhost", port: 443 }
            print(c.port)
            """);

        Assert.Equal($"443{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Nested construction
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_Nested_ProducesCorrectResult() {
        string stdout = Run("""
            type Address {
            city: string
            }
            type Person {
            name: string
            address: Address
            }
            readonly p := Person { name: "Alice", address: Address { city: "London" } }
            print(p.name)
            print(p.address.city)
            """);

        Assert.Equal($"Alice{NL}London{NL}", stdout);
    }
}
