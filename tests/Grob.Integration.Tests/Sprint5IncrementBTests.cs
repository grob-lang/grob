using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment B end-to-end tests — default parameter values and the
/// named-argument calling convention (D-113). Each test drives the full pipeline:
/// Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint5IncrementBTests {
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
    public void NamedAndDefaultArguments_RunEndToEnd_ProduceExpectedStdout() {
        string stdout = Run("""
            fn greet(name: string, greeting: string = "Hello"): string {
                return greeting + ", " + name + "!"
            }
            fn flag(name: string, enabled: bool = false): string {
                return name + ": " + (enabled ? "on" : "off")
            }
            print(greet("World"))
            print(greet("Bob", "Hi"))
            print(greet("Alice", greeting: "Hey"))
            print(flag("wifi"))
            print(flag("vpn", enabled: true))
            """);

        string expected =
            $"Hello, World!{NL}" +   // default omitted
            $"Hi, Bob!{NL}" +        // default overridden positionally
            $"Hey, Alice!{NL}" +     // default overridden by name
            $"wifi: off{NL}" +       // bool default omitted
            $"vpn: on{NL}";          // named boolean argument
        Assert.Equal(expected, stdout);
    }
}
