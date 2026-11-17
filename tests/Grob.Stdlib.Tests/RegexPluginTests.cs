using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// The <c>regex</c> module's namespace functions as <see cref="RegexPlugin"/> registers
/// them (D-432, D-440): <c>regex.compile</c>, the run-time route to a <c>Regex</c>, and
/// the seven one-shot functions over a string pattern. Each native is called straight off
/// the catalogue; the <c>Regex</c> and <c>Match</c> members are
/// <see cref="RegexNamedTypeTests"/>' job, and the end-to-end pipeline is the integration
/// suite's.
/// </summary>
public sealed class RegexPluginTests {
    private static readonly NativeCatalogue Catalogue = TestCatalogue.Catalogue(new RegexPlugin());

    private static GrobValue Call(string member, params GrobValue[] args) {
        Assert.True(Catalogue.TryGetNative("regex", member, out NativeEntry? entry), $"regex.{member} is not registered");
        return entry.Implementation.Implementation(args, default);
    }

    private static GrobValue Str(string s) => GrobValue.FromString(s);

    private static GrobValue Int(long n) => GrobValue.FromInt(n);

    private static NativeFaultException Fault(string member, params GrobValue[] args) =>
        Assert.Throws<NativeFaultException>(() => Call(member, args));

    // -----------------------------------------------------------------------
    // regex.compile
    // -----------------------------------------------------------------------

    [Fact]
    public void Compile_BuildsARegexWithNormalisedFlagsAndTheTimeout() {
        GrobStruct regex = Call("compile", Str(@"\d+"), Str("mi"), Int(30)).AsStruct();

        Assert.Equal("Regex", regex.TypeName);
        Assert.Equal(@"\d+", regex.GetField(RegexLayout.PatternFieldName).AsString());
        Assert.Equal("im", regex.GetField(RegexLayout.FlagsFieldName).AsString());
        Assert.Equal(30, regex.GetField(RegexLayout.TimeoutFieldName).AsInt());
    }

    [Fact]
    public void Compile_InvalidPattern_ThrowsParseErrorWithoutThePattern() {
        NativeFaultException fault = Fault("compile", Str("secret("), Str(""), Int(5));

        Assert.Equal("ParseError", fault.LeafTypeName);
        Assert.Equal("E5702", fault.Code);
        Assert.Contains("regex.compile", fault.Message);
        Assert.Contains("offset 7", fault.Message);
        Assert.DoesNotContain("secret", fault.Message);
    }

    [Fact]
    public void Compile_UnknownFlag_ThrowsParseError() {
        NativeFaultException fault = Fault("compile", Str("a"), Str("g"), Int(5));

        Assert.Equal("ParseError", fault.LeafTypeName);
        Assert.Equal("E5702", fault.Code);
        Assert.Contains("'i' and 'm'", fault.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(RegexLayout.MaxTimeoutSeconds + 1)]
    public void Compile_OutOfRangeTimeout_ThrowsParseError(long timeout) {
        NativeFaultException fault = Fault("compile", Str("a"), Str(""), Int(timeout));

        Assert.Equal("ParseError", fault.LeafTypeName);
        Assert.Equal("E5702", fault.Code);
        Assert.Contains("timeout", fault.Message);
    }

    [Fact]
    public void Compile_TimeoutExpiry_ThrowsRuntimeErrorNamingTheLimit() {
        // A catastrophic-backtracking pattern under a one-second limit (D-432 Decision 8).
        GrobValue regex = Call("compile", Str("^(a+)+$"), Str(""), Int(1));
        NamedTypeSurface surface = Assert.IsType<NamedTypeSurface>(
            Catalogue.TryGetNamedType("Regex", out NamedTypeSurface? s) ? s : null);
        NativeFunction isMatch = surface.Methods["isMatch"].Bind(regex.AsStruct());

        NativeFaultException fault = Assert.Throws<NativeFaultException>(
            () => isMatch.Implementation([Str(new string('a', 40) + "!")], default));

        Assert.Equal("RuntimeError", fault.LeafTypeName);
        Assert.Equal("E5903", fault.Code);
        Assert.Contains("Regex.isMatch", fault.Message);
        Assert.Contains("1-second", fault.Message);
        Assert.DoesNotContain("(a+)", fault.Message);
    }

    // -----------------------------------------------------------------------
    // The one-shot functions
    // -----------------------------------------------------------------------

    [Fact]
    public void IsMatch_ReportsWhetherThePatternMatches() {
        Assert.True(Call("isMatch", Str(@"^\d+$"), Str("123")).AsBool());
        Assert.False(Call("isMatch", Str(@"^\d+$"), Str("12a")).AsBool());
    }

    [Fact]
    public void Match_ReturnsTheFirstMatch() {
        GrobStruct match = Call("match", Str(@"\d+"), Str("ab 12 34")).AsStruct();

        Assert.Equal("Match", match.TypeName);
        Assert.Equal("12", match.GetField(RegexLayout.ValueFieldName).AsString());
        Assert.Equal(3, match.GetField(RegexLayout.IndexFieldName).AsInt());
    }

    [Fact]
    public void Match_NoMatch_ReturnsNil() => Assert.True(Call("match", Str(@"\d+"), Str("none")).IsNil);

    [Fact]
    public void MatchAll_ReturnsEveryNonOverlappingMatch() {
        GrobArray matches = Call("matchAll", Str(@"\d+"), Str("1 22 333")).AsArray();

        Assert.Equal(["1", "22", "333"],
            matches.Elements.Select(m => m.AsStruct().GetField(RegexLayout.ValueFieldName).AsString()));
    }

    [Fact]
    public void MatchAll_NoMatch_ReturnsAnEmptyArray() =>
        Assert.Equal(0, Call("matchAll", Str(@"\d+"), Str("none")).AsArray().Count);

    [Fact]
    public void Replace_ReplacesTheFirstMatchOnly() =>
        Assert.Equal("[1] 2 3", Call("replace", Str(@"(\d)"), Str("1 2 3"), Str("[$1]")).AsString());

    [Fact]
    public void ReplaceAll_ReplacesEveryMatch() =>
        Assert.Equal("[1] [2] [3]", Call("replaceAll", Str(@"(?<d>\d)"), Str("1 2 3"), Str("[${d}]")).AsString());

    [Fact]
    public void Split_KeepsCapturedSeparators() {
        GrobArray parts = Call("split", Str(@"(,)"), Str("a,b")).AsArray();

        Assert.Equal(["a", ",", "b"], parts.Elements.Select(p => p.AsString()));
    }

    [Fact]
    public void Escape_EscapesEveryMetacharacter() {
        string escaped = Call("escape", Str("a.b*c")).AsString();

        Assert.Equal(@"a\.b\*c", escaped);
        Assert.True(Call("isMatch", Str("^" + escaped + "$"), Str("a.b*c")).AsBool());
    }

    [Theory]
    [InlineData("isMatch")]
    [InlineData("match")]
    [InlineData("matchAll")]
    [InlineData("split")]
    public void OneShot_InvalidPattern_ThrowsParseError(string member) {
        NativeFaultException fault = Fault(member, Str("(x"), Str("input"));

        Assert.Equal("ParseError", fault.LeafTypeName);
        Assert.Equal("E5702", fault.Code);
        Assert.Contains($"regex.{member}", fault.Message);
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("replaceAll")]
    public void OneShotReplace_InvalidPattern_ThrowsParseError(string member) {
        NativeFaultException fault = Fault(member, Str("(x"), Str("input"), Str("y"));

        Assert.Equal("ParseError", fault.LeafTypeName);
        Assert.Equal("E5702", fault.Code);
    }

    [Fact]
    public void OneShot_IsCaseSensitiveAndCultureInvariant() =>
        Assert.False(Call("isMatch", Str("ABC"), Str("abc")).AsBool());
}
