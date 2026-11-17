using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// A regex literal compiles to one <see cref="OpCode.Constant"/> whose pool entry is a
/// <c>Regex</c> struct laid out by <see cref="RegexLayout"/>, with its flags normalised and
/// the default timeout (D-432 Decision 7, D-440). No opcode is added: the literal site's
/// constant is one struct instance, which is what lets the plugin compile it once per site
/// per run. Closes R-30, where the compiler emitted nothing and the VM underflowed.
/// </summary>
public sealed class CompilerRegexTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    private static List<GrobStruct> RegexConstants(Chunk chunk) =>
        [.. Enumerable.Range(0, chunk.ConstantCount)
            .Select(chunk.ReadConstant)
            .Where(v => v.TryAsStruct(out GrobStruct? s) && s!.TypeName == RegexLayout.RegexTypeName)
            .Select(v => v.AsStruct())];

    [Fact]
    public void Literal_EmitsAConstantRegexStruct() {
        Chunk chunk = CompileSource("""x := /\d+/""");

        Assert.Equal(OpCode.Constant, (OpCode)chunk.ReadByte(0));
        GrobStruct regex = chunk.ReadConstant(chunk.ReadByte(1)).AsStruct();
        Assert.Equal("Regex", regex.TypeName);
        Assert.Equal(@"\d+", regex.GetField(RegexLayout.PatternFieldName).AsString());
        Assert.Equal("", regex.GetField(RegexLayout.FlagsFieldName).AsString());
        Assert.Equal(5, regex.GetField(RegexLayout.TimeoutFieldName).AsInt());
    }

    [Fact]
    public void Literal_FlagsAreNormalised() {
        Chunk chunk = CompileSource("x := /a/mi");

        GrobStruct regex = Assert.Single(RegexConstants(chunk));
        Assert.Equal("im", regex.GetField(RegexLayout.FlagsFieldName).AsString());
    }

    [Fact]
    public void Literal_InALoop_IsOneConstant() {
        Chunk chunk = CompileSource("""
            n := 0
            for i in 0..10 {
                if (/a/.isMatch("a")) {
                    n++
                }
            }
            """);

        Assert.Single(RegexConstants(chunk));
    }

    [Fact]
    public void TwoLiteralSites_AreTwoConstants() {
        Chunk chunk = CompileSource("""
            a := /x/
            b := /x/
            """);

        List<GrobStruct> regexes = RegexConstants(chunk);
        Assert.Equal(2, regexes.Count);
        Assert.NotSame(regexes[0], regexes[1]);
    }
}
