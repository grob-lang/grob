using System.Text;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 6 Increment C end-to-end tests — struct field access and assignment.
/// Covers: reading a field via dot notation, writing a field through a mutable
/// binding, nested field access and the D-325 closure-in-field escape regression.
/// Each test drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint6IncrementCTests {
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

    // -----------------------------------------------------------------------
    // Field read
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldRead_Simple_OutputsCorrectValue() {
        string stdout = Run("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            print(c.host)
            """);

        Assert.Contains("example.com", stdout);
    }

    [Fact]
    public void FieldRead_IntField_OutputsCorrectValue() {
        string stdout = Run("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "x", port: 9090 }
            print(c.port)
            """);

        Assert.Contains("9090", stdout);
    }

    // -----------------------------------------------------------------------
    // Field write (mutable binding)
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldWrite_MutableBinding_UpdatesAndPrintsNewValue() {
        string stdout = Run("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "example.com", port: 8080 }
            c.host = "localhost"
            print(c.host)
            """);

        Assert.Contains("localhost", stdout);
        Assert.DoesNotContain("example.com", stdout);
    }

    [Fact]
    public void FieldWrite_IntField_UpdatesAndPrintsNewValue() {
        string stdout = Run("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "x", port: 8080 }
            c.port = 443
            print(c.port)
            """);

        Assert.Contains("443", stdout);
        Assert.DoesNotContain("8080", stdout);
    }

    // -----------------------------------------------------------------------
    // Nested field read
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldRead_Nested_OutputsNestedFieldValue() {
        string stdout = Run("""
            type Address {
            city: string
            }
            type Person {
            name: string
            address: Address
            }
            readonly p := Person { name: "Alice", address: Address { city: "London" } }
            print(p.address.city)
            """);

        Assert.Contains("London", stdout);
    }

    // -----------------------------------------------------------------------
    // D-325 closure-in-field escape
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosureInField_Integration_CallsAfterReturn() {
        // A closure capturing a local is stored in a struct field and returned
        // from the enclosing function. The caller retrieves the struct, reads the
        // callback field, calls it, and receives the captured value.
        // This exercises the D-325 close-on-return path through a struct field.
        string stdout = Run("""
            type Box {
            callback: fn(): int
            }
            fn makeBox(): Box {
            n := 42
            f := () => n
            return Box { callback: f }
            }
            readonly box := makeBox()
            readonly result := box.callback()
            print(result)
            """);

        Assert.Contains("42", stdout);
    }
}
