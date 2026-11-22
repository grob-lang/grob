using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-450: a binary operator with one operand of unknown type, such as a lambda's untyped
/// parameter, still requires the known operand to suit the operator. <c>+</c> takes an
/// <c>int</c>, a <c>float</c> or a <c>string</c>; <c>-</c>, <c>*</c>, <c>/</c> and
/// <c>%</c> take an <c>int</c> or a <c>float</c>; <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>
/// and <c>&gt;=</c> take an <c>int</c>, a <c>float</c>, a <c>string</c> or a
/// <c>date</c>. Any other known operand is E0002 at that operand: it crashed the compiler
/// or compiled to code that always failed (R-61).
/// </summary>
public sealed class TypeCheckerUnknownOperandTests {
    private static DiagnosticBag TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return bag;
    }

    // `f := (s) => ` is twelve characters, so the body starts at column 13.
    [Theory]
    [InlineData("\"a\" - s", "-", "string", 13)]
    [InlineData("\"a\" * s", "*", "string", 13)]
    [InlineData("s / \"a\"", "/", "string", 17)]
    [InlineData("s % \"a\"", "%", "string", 17)]
    [InlineData("true + s", "+", "bool", 13)]
    [InlineData("s + true", "+", "bool", 17)]
    [InlineData("[1] + s", "+", "array", 13)]
    [InlineData("nil + s", "+", "nil", 13)]
    [InlineData("true < s", "<", "bool", 13)]
    [InlineData("s >= [1]", ">=", "array", 18)]
    public void KnownOperandUnsuitedToOperator_IsE0002AtThatOperand(
            string body, string op, string knownType, int column) {
        DiagnosticBag bag = TypeCheckSource($"f := (s) => {body}\n");

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0002", d.Code);
        Assert.Equal($"Operator '{op}' cannot be applied to type '{knownType}'.", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
    }

    [Fact]
    public void NullableKnownOperand_IsE0002() {
        DiagnosticBag bag = TypeCheckSource("""
            n: int? := nil
            f := (s) => n + s
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0002", d.Code);
        Assert.Equal("Operator '+' cannot be applied to type 'int?'.", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(13, d.Range.Start.Column);
    }

    [Fact]
    public void NonDateStructOrdered_IsE0002() {
        DiagnosticBag bag = TypeCheckSource("""
            type Box {
                size: int
            }
            bx := Box { size: 1 }
            f := (s) => s < bx
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0002", d.Code);
        Assert.Equal("Operator '<' cannot be applied to type 'struct'.", d.Message);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(17, d.Range.Start.Column);
    }

    [Theory]
    [InlineData("\"[\" + s")]
    [InlineData("s + \"]\"")]
    [InlineData("s - 1")]
    [InlineData("1.5 * s")]
    [InlineData("s % 2")]
    [InlineData("s < \"z\"")]
    [InlineData("\"a\" <= s")]
    [InlineData("s > 2.5")]
    [InlineData("s < date.now()")]
    [InlineData("date.now() > s")]
    [InlineData("s == true")]
    [InlineData("[1] != s")]
    [InlineData("s + s")]
    [InlineData("s < s")]
    public void KnownOperandSuitedToOperator_IsAccepted(string body) {
        DiagnosticBag bag = TypeCheckSource($"f := (s) => {body}\n");

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }
}
