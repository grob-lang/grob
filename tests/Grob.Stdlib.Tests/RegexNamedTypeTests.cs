using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// The <c>Regex</c> and <c>Match</c> named types as <see cref="RegexPlugin"/> registers
/// them (D-432, D-440): their properties, methods and renderers, read straight off the
/// catalogue, and the per-value compiled-pattern cache that gives a regex literal its
/// "compiled once per site" contract.
/// </summary>
public sealed class RegexNamedTypeTests {
    private static readonly NativeCatalogue Catalogue = TestCatalogue.Catalogue(new RegexPlugin());

    private static NamedTypeSurface Surface(string name) =>
        Catalogue.TryGetNamedType(name, out NamedTypeSurface? surface)
            ? surface
            : throw new InvalidOperationException($"RegexPlugin did not register the {name} named type.");

    private static NamedTypeSurface Regex => Surface("Regex");

    private static NamedTypeSurface Match => Surface("Match");

    private static GrobValue Str(string s) => GrobValue.FromString(s);

    private static GrobValue Call(NamedTypeSurface type, string method, GrobStruct receiver, params GrobValue[] args) =>
        type.Methods[method].Bind(receiver).Implementation(args, default);

    private static GrobStruct Levels() => RegexLayout.NewRegex(@"\b(?<level>ERROR|WARN)\b", "i", 5);

    private static GrobStruct FirstMatch(GrobStruct regex, string input) =>
        Call(Regex, "match", regex, Str(input)).AsStruct();

    // -----------------------------------------------------------------------
    // Registration
    // -----------------------------------------------------------------------

    [Fact]
    public void Register_DeclaresTheRegexNamespaceAndBothNamedTypes() {
        Assert.Equal(["regex"], Catalogue.NamespaceNames);
        Assert.Equal(["Regex", "Match"], Catalogue.NamedTypeNames);
        Assert.Equal("regex", Regex.Owner);
        Assert.Equal("regex", Match.Owner);
    }

    [Fact]
    public void Regex_DeclaresThreePropertiesAndSixMethods() {
        Assert.Equal(["flags", "pattern", "timeout"], Regex.Properties.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["isMatch", "match", "matchAll", "replace", "replaceAll", "split"],
            Regex.Methods.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Match_DeclaresFourPropertiesAndOneMethod() {
        Assert.Equal(["groups", "index", "length", "value"], Match.Properties.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["group"], Match.Methods.Keys);
    }

    // -----------------------------------------------------------------------
    // Regex properties and methods
    // -----------------------------------------------------------------------

    [Fact]
    public void RegexProperties_ReadTheHiddenFields() {
        GrobStruct regex = Levels();

        Assert.Equal(@"\b(?<level>ERROR|WARN)\b", Regex.Properties["pattern"].Get(regex).AsString());
        Assert.Equal("i", Regex.Properties["flags"].Get(regex).AsString());
        Assert.Equal(5, Regex.Properties["timeout"].Get(regex).AsInt());
    }

    [Fact]
    public void IsMatch_HonoursTheIgnoreCaseFlag() {
        Assert.True(Call(Regex, "isMatch", Levels(), Str("an error here")).AsBool());
        Assert.False(Call(Regex, "isMatch", RegexLayout.NewRegex("ERROR", "", 5), Str("an error here")).AsBool());
    }

    [Fact]
    public void Multiline_AnchorsAtLineBoundaries() {
        GrobArray lines = Call(Regex, "matchAll", RegexLayout.NewRegex("^\\w+$", "m", 5), Str("one\ntwo")).AsArray();

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void Match_NoMatch_ReturnsNil() =>
        Assert.True(Call(Regex, "match", Levels(), Str("all fine")).IsNil);

    [Fact]
    public void MatchAll_ReturnsMatchValues() {
        GrobArray matches = Call(Regex, "matchAll", Levels(), Str("WARN then error")).AsArray();

        Assert.Equal(["WARN", "error"],
            matches.Elements.Select(m => Match.Properties["value"].Get(m.AsStruct()).AsString()));
    }

    [Fact]
    public void Replace_ReplacesTheFirstMatchWithDotNetSyntax() =>
        Assert.Equal("[WARN] and ERROR",
            Call(Regex, "replace", Levels(), Str("WARN and ERROR"), Str("[${level}]")).AsString());

    [Fact]
    public void ReplaceAll_ReplacesEveryMatchAndHonoursDollarEscape() =>
        Assert.Equal("[WARN]$ and [ERROR]$",
            Call(Regex, "replaceAll", Levels(), Str("WARN and ERROR"), Str("[$1]$$")).AsString());

    [Fact]
    public void Split_SplitsOnThePattern() {
        GrobArray parts = Call(Regex, "split", RegexLayout.NewRegex(@"\s*,\s*", "", 5), Str("a , b,c")).AsArray();

        Assert.Equal(["a", "b", "c"], parts.Elements.Select(p => p.AsString()));
    }

    [Fact]
    public void Methods_ShareOneCompiledRegexPerValue() {
        RegexCache cache = new();
        GrobStruct regex = Levels();

        Assert.Same(cache.Get(regex), cache.Get(regex));
        Assert.NotSame(cache.Get(regex), cache.Get(Levels()));
    }

    [Fact]
    public void Cache_UsesTheValuesTimeout() {
        RegexCache cache = new();

        Assert.Equal(TimeSpan.FromSeconds(7), cache.Get(RegexLayout.NewRegex("a", "", 7)).MatchTimeout);
        Assert.Equal(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout,
            cache.Get(RegexLayout.NewRegex("a", "", 0)).MatchTimeout);
    }

    // -----------------------------------------------------------------------
    // Match properties and group(name)
    // -----------------------------------------------------------------------

    [Fact]
    public void MatchProperties_DescribeTheMatch() {
        GrobStruct match = FirstMatch(Levels(), "at WARN now");

        Assert.Equal("WARN", Match.Properties["value"].Get(match).AsString());
        Assert.Equal(3, Match.Properties["index"].Get(match).AsInt());
        Assert.Equal(4, Match.Properties["length"].Get(match).AsInt());
        Assert.Equal(["WARN", "WARN"],
            Match.Properties["groups"].Get(match).AsArray().Elements.Select(g => g.AsString()));
    }

    [Fact]
    public void Groups_UnmatchedNumberedGroupIsEmpty() {
        GrobStruct match = FirstMatch(RegexLayout.NewRegex("(a)|(b)", "", 5), "b");

        Assert.Equal(["b", "", "b"],
            Match.Properties["groups"].Get(match).AsArray().Elements.Select(g => g.AsString()));
    }

    [Fact]
    public void Groups_ExplicitlyNumberedGroup_IsListedInGroupNumberOrder() {
        // .NET's group enumerator drops an explicitly numbered group such as (?<2>a);
        // groups lists every defined group by number, without gaps.
        GrobStruct match = FirstMatch(RegexLayout.NewRegex("(?<2>a)", "", 5), "a");

        Assert.Equal(["a", "a"],
            Match.Properties["groups"].Get(match).AsArray().Elements.Select(g => g.AsString()));
    }

    [Fact]
    public void Groups_ReturnsACopyEachTime() {
        GrobStruct match = FirstMatch(Levels(), "WARN");
        GrobArray first = Match.Properties["groups"].Get(match).AsArray();

        first.Clear();

        Assert.Equal(2, Match.Properties["groups"].Get(match).AsArray().Count);
    }

    [Fact]
    public void Group_NamedGroupThatMatched_ReturnsItsText() =>
        Assert.Equal("WARN", Call(Match, "group", FirstMatch(Levels(), "WARN"), Str("level")).AsString());

    [Fact]
    public void Group_AbsentName_ReturnsNil() =>
        Assert.True(Call(Match, "group", FirstMatch(Levels(), "WARN"), Str("nope")).IsNil);

    [Fact]
    public void Group_NamedGroupThatDidNotTakePart_ReturnsNil() {
        GrobStruct match = FirstMatch(RegexLayout.NewRegex("(?<x>a)|(?<y>b)", "", 5), "b");

        Assert.True(Call(Match, "group", match, Str("x")).IsNil);
        Assert.Equal("b", Call(Match, "group", match, Str("y")).AsString());
    }

    // -----------------------------------------------------------------------
    // Display
    // -----------------------------------------------------------------------

    [Fact]
    public void Regex_RendersAsALiteral() =>
        Assert.Equal(@"/\b(?<level>ERROR|WARN)\b/i", Catalogue.RenderValue(GrobValue.FromStruct(Levels())));

    [Fact]
    public void Match_RendersAsItsValue() =>
        Assert.Equal("WARN", Catalogue.RenderValue(GrobValue.FromStruct(FirstMatch(Levels(), "x WARN"))));
}
