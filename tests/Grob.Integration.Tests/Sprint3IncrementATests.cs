using System.Text;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 3 Increment A end-to-end tests: variables, assignment, compound
/// assignment, increment/decrement, and block scoping.
/// Each test drives the full pipeline Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint3IncrementATests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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

    private static DiagnosticBag TypeCheck(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // Global variable declaration and read
    // -----------------------------------------------------------------------

    [Fact]
    public void GlobalVar_DeclareAndPrint_OutputsValue() {
        string stdout = Run("""
            x := 42
            print(x)
            """);
        Assert.Equal($"42{NL}", stdout);
    }

    [Fact]
    public void GlobalVar_FloatDeclaration_PrintsFloat() {
        string stdout = Run("""
            pi := 3.14
            print(pi)
            """);
        Assert.Equal($"3.14{NL}", stdout);
    }

    [Fact]
    public void GlobalVar_StringDeclaration_PrintsString() {
        string stdout = Run("""
            greeting := "hello"
            print(greeting)
            """);
        Assert.Equal($"hello{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Assignment (=)
    // -----------------------------------------------------------------------

    [Fact]
    public void GlobalAssignment_ReassignsValue_PrintsNewValue() {
        string stdout = Run("""
            x := 5
            x = 10
            print(x)
            """);
        Assert.Equal($"10{NL}", stdout);
    }

    [Fact]
    public void GlobalAssignment_MultipleReassignments_PrintsFinalValue() {
        string stdout = Run("""
            n := 1
            n = 2
            n = 3
            print(n)
            """);
        Assert.Equal($"3{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Compound assignment
    // -----------------------------------------------------------------------

    [Fact]
    public void CompoundAssign_PlusEquals_IntGlobal() {
        string stdout = Run("""
            x := 10
            x += 5
            print(x)
            """);
        Assert.Equal($"15{NL}", stdout);
    }

    [Fact]
    public void CompoundAssign_MinusEquals_IntGlobal() {
        string stdout = Run("""
            x := 10
            x -= 3
            print(x)
            """);
        Assert.Equal($"7{NL}", stdout);
    }

    [Fact]
    public void CompoundAssign_StarEquals_IntGlobal() {
        string stdout = Run("""
            x := 6
            x *= 7
            print(x)
            """);
        Assert.Equal($"42{NL}", stdout);
    }

    [Fact]
    public void CompoundAssign_PlusEquals_StringGlobal() {
        string stdout = Run("""
            s := "hello"
            s += " world"
            print(s)
            """);
        Assert.Equal($"hello world{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Increment / decrement
    // -----------------------------------------------------------------------

    [Fact]
    public void Increment_GlobalInt_IncreasesByOne() {
        string stdout = Run("""
            i := 0
            i++
            print(i)
            """);
        Assert.Equal($"1{NL}", stdout);
    }

    [Fact]
    public void Decrement_GlobalInt_DecreasesByOne() {
        string stdout = Run("""
            i := 5
            i--
            print(i)
            """);
        Assert.Equal($"4{NL}", stdout);
    }

    [Fact]
    public void Increment_GlobalInt_MultipleIncrements() {
        string stdout = Run("""
            counter := 0
            counter++
            counter++
            counter++
            print(counter)
            """);
        Assert.Equal($"3{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Block scoping (locals)
    // -----------------------------------------------------------------------

    [Fact]
    public void BlockLocal_DeclareAndPrint_InsideBlock() {
        string stdout = Run("""
            {
              inner := 99
              print(inner)
            }
            """);
        Assert.Equal($"99{NL}", stdout);
    }

    [Fact]
    public void BlockLocal_StackCleanedOnExit() {
        // After the block, the stack should be clean; subsequent print must work.
        string stdout = Run("""
            x := 1
            {
              y := 2
              print(y)
            }
            print(x)
            """);
        Assert.Equal($"2{NL}1{NL}", stdout);
    }

    [Fact]
    public void BlockLocal_ShadowsGlobal_ThenGlobalRestored() {
        string stdout = Run("""
            x := 10
            {
              x := 20
              print(x)
            }
            print(x)
            """);
        Assert.Equal($"20{NL}10{NL}", stdout);
    }

    [Fact]
    public void BlockLocal_Assignment_UpdatesLocal() {
        string stdout = Run("""
            {
              n := 5
              n = 50
              print(n)
            }
            """);
        Assert.Equal($"50{NL}", stdout);
    }

    [Fact]
    public void BlockLocal_Increment_UpdatesLocal() {
        string stdout = Run("""
            {
              k := 0
              k++
              k++
              print(k)
            }
            """);
        Assert.Equal($"2{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Type errors are caught before VM (compile-time)
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeError_UndeclaredVariable_EmitsE1001() {
        DiagnosticBag bag = TypeCheck("print(notDeclared)");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal((1, 7), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void TypeError_SameScopeRedeclaration_EmitsE1102() {
        DiagnosticBag bag = TypeCheck("""
            x := 1
            x := 2
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1102", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void TypeError_IncrementFloat_EmitsE0002() {
        DiagnosticBag bag = TypeCheck("""
            f := 1.0
            f++
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }
}
