using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Grob.Core;

using Xunit;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="ValueDisplay"/> (D-336): the two-position rendering service.
/// <c>Display</c> is the top-level, public-facing form; <c>Inspect</c> is the nested
/// form composites recurse into. Dispatch precedence is load-bearing — the registered
/// <c>toString()</c> lookup runs before the structural arm so a credential-bearing type
/// (D-159/D-297) renders through its guard, not field-by-field.
/// </summary>
public sealed class ValueDisplayTests {
    private static ValueDisplay New() => new();

    private static string UnderCulture(string culture, Func<string> render) {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            return render();
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static KeyValuePair<string, GrobValue> Pair(string key, GrobValue value) => new(key, value);

    // ----- Arm 1: nil -----

    [Fact]
    public void Display_Nil_RendersNil() =>
        Assert.Equal("nil", New().Display(GrobValue.Nil));

    [Fact]
    public void Inspect_Nil_RendersNil() =>
        Assert.Equal("nil", New().Inspect(GrobValue.Nil));

    // ----- Arm 2: registered toString() precedence and credential safety -----

    /// <summary>A test-only registry that redacts any struct carrying a "secret" field.</summary>
    private sealed class RedactingRegistry : IValueToStringRegistry {
        public bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) {
            if (value.Kind == GrobValueKind.Struct && value.AsStruct().TryGetField("secret", out _)) {
                rendered = "Credential(****)";
                return true;
            }
            rendered = null;
            return false;
        }
    }

    private static GrobValue MakeCredential(string secret) {
        var s = new GrobStruct("Credential");
        s.SetField("secret", GrobValue.FromString(secret));
        return GrobValue.FromStruct(s);
    }

    [Fact]
    public void Display_RegisteredToString_WinsOverStructuralArm() {
        var display = new ValueDisplay(new RedactingRegistry());
        string rendered = display.Display(MakeCredential("hunter2"));

        Assert.Equal("Credential(****)", rendered);
        Assert.DoesNotContain("hunter2", rendered);
        Assert.DoesNotContain("secret", rendered);
    }

    [Fact]
    public void Inspect_RegisteredToString_WinsOverStructuralArm() {
        var display = new ValueDisplay(new RedactingRegistry());
        string rendered = display.Inspect(MakeCredential("hunter2"));

        Assert.Equal("Credential(****)", rendered);
        Assert.DoesNotContain("hunter2", rendered);
    }

    [Fact]
    public void Display_CredentialNestedInComposite_IsRedactedViaRegistry() {
        var display = new ValueDisplay(new RedactingRegistry());
        var arr = new GrobArray([MakeCredential("hunter2")]);

        string rendered = display.Display(GrobValue.FromArray(arr));

        Assert.Equal("[Credential(****)]", rendered);
        Assert.DoesNotContain("hunter2", rendered);
    }

    [Fact]
    public void Display_NullRegistryDefault_RendersStructStructurally() {
        // With no registry the same struct renders field-by-field — proving the default
        // path is structural and the redaction above is the registry, not the fallback.
        string rendered = New().Display(MakeCredential("hunter2"));

        Assert.Equal("Credential { secret: \"hunter2\" }", rendered);
    }

    /// <summary>
    /// A test-only registry standing in for D-159's <c>AuthHeader.toString()</c> —
    /// <c>Grob.Http</c> has no compiled source yet, so this is the closest honest proxy
    /// for "the runtime type registered a toString()" without inventing plugin code.
    /// </summary>
    private sealed class AuthHeaderRegistry : IValueToStringRegistry {
        public bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) {
            if (value.Kind == GrobValueKind.Struct && value.AsStruct().TypeName == "AuthHeader") {
                rendered = "[AuthHeader]";
                return true;
            }
            rendered = null;
            return false;
        }
    }

    [Fact]
    public void Display_AuthHeaderWithRegisteredToString_NeverExposesToken() {
        var s = new GrobStruct("AuthHeader");
        s.SetField("token", GrobValue.FromString("bearer-secret-token"));
        var display = new ValueDisplay(new AuthHeaderRegistry());

        string rendered = display.Display(GrobValue.FromStruct(s));

        Assert.Equal("[AuthHeader]", rendered);
        Assert.DoesNotContain("bearer-secret-token", rendered);
    }

    // ----- Arm 3: bool / int -----

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Display_Bool_RendersKeyword(bool value, string expected) =>
        Assert.Equal(expected, New().Display(GrobValue.FromBool(value)));

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(42L, "42")]
    [InlineData(-7L, "-7")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    public void Display_Int_RendersDigits(long value, string expected) =>
        Assert.Equal(expected, New().Display(GrobValue.FromInt(value)));

    // ----- Arm 3: float -----

    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(-2.0, "-2.0")]
    [InlineData(1.5, "1.5")]
    [InlineData(100.0, "100.0")]
    public void Display_Float_AlwaysCarriesDecimalPoint(double value, string expected) =>
        Assert.Equal(expected, New().Display(GrobValue.FromFloat(value)));

    [Fact]
    public void Display_Float_RoundTripsShortestRepresentation() =>
        Assert.Equal("0.30000000000000004", New().Display(GrobValue.FromFloat(0.1 + 0.2)));

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public void Display_Float_NonFinite_UsesPinnedSpelling(double value, string expected) =>
        Assert.Equal(expected, New().Display(GrobValue.FromFloat(value)));

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("")] // invariant
    public void Display_Float_IsCultureInvariant(string culture) =>
        Assert.Equal("1.5", UnderCulture(culture, () => New().Display(GrobValue.FromFloat(1.5))));

    // ----- Arm 4: string, position-dependent -----

    [Fact]
    public void Display_String_TopLevel_IsUnquoted() =>
        Assert.Equal("hello", New().Display(GrobValue.FromString("hello")));

    [Fact]
    public void Inspect_String_Nested_IsQuoted() =>
        Assert.Equal("\"hello\"", New().Inspect(GrobValue.FromString("hello")));

    [Theory]
    [InlineData("a\"b", "\"a\\\"b\"")]     // embedded double quote
    [InlineData("a\\b", "\"a\\\\b\"")]     // embedded backslash
    [InlineData("a\nb", "\"a\\nb\"")]      // embedded newline
    public void Inspect_String_EscapesSpecialCharacters(string raw, string expected) =>
        Assert.Equal(expected, New().Inspect(GrobValue.FromString(raw)));

    [Fact]
    public void Display_StringWithDigits_IsDistinctFromInt_WhenNested() {
        // "8080" the string vs 8080 the int — the nested (Inspect) quoting is what
        // distinguishes them inside a composite.
        var arr = new GrobArray([GrobValue.FromString("8080"), GrobValue.FromInt(8080)]);
        Assert.Equal("[\"8080\", 8080]", New().Display(GrobValue.FromArray(arr)));
    }

    // ----- Arm 5: function signature -----

    private static GrobValue Fn(string name, IReadOnlyList<GrobType> paramTypes, GrobType returnType) =>
        GrobValue.FromFunction(new BytecodeFunction(name, paramTypes.Count, new Chunk(),
            parameterTypes: paramTypes, returnType: returnType));

    [Fact]
    public void Display_Function_NoParams_RendersSignature() =>
        Assert.Equal("fn(): int", New().Display(Fn("f", [], GrobType.Int)));

    [Fact]
    public void Display_Function_OneParam_RendersSignature() =>
        Assert.Equal("fn(int): int", New().Display(Fn("f", [GrobType.Int], GrobType.Int)));

    [Fact]
    public void Display_Function_TwoParams_RendersSignature() =>
        Assert.Equal("fn(int, string): bool",
            New().Display(Fn("f", [GrobType.Int, GrobType.String], GrobType.Bool)));

    [Theory]
    [InlineData(GrobType.Float, "float")]
    [InlineData(GrobType.Nil, "nil")]
    [InlineData(GrobType.NullableInt, "int?")]
    [InlineData(GrobType.NullableFloat, "float?")]
    [InlineData(GrobType.NullableString, "string?")]
    [InlineData(GrobType.NullableBool, "bool?")]
    [InlineData(GrobType.Array, "array")]
    [InlineData(GrobType.NullableArray, "array?")]
    [InlineData(GrobType.Map, "map")]
    [InlineData(GrobType.Function, "fn")]
    [InlineData(GrobType.NullableFunction, "fn?")]
    [InlineData(GrobType.Struct, "struct")]
    [InlineData(GrobType.NullableStruct, "struct?")]
    [InlineData(GrobType.AnonStruct, "struct")]
    [InlineData(GrobType.NullableAnonStruct, "struct?")]
    [InlineData(GrobType.Unknown, "unknown")]   // lambda params/return are Unknown in v1 (untyped)
    public void Display_Function_SpellsEveryParameterAndReturnTypeKind(GrobType type, string expectedSpelling) =>
        Assert.Equal($"fn({expectedSpelling}): {expectedSpelling}", New().Display(Fn("f", [type], type)));

    // ----- Arm 6: composites -----

    private static GrobValue Config() {
        var s = new GrobStruct("Config", new[] {
            Pair("host", GrobValue.FromString("example.com")),
            Pair("port", GrobValue.FromInt(8080)),
        });
        return GrobValue.FromStruct(s);
    }

    [Fact]
    public void Display_NamedStruct_RendersTypeNameAndFields() =>
        Assert.Equal("Config { host: \"example.com\", port: 8080 }", New().Display(Config()));

    [Fact]
    public void Display_AnonymousStruct_RendersHashBrace() {
        var s = new GrobStruct("sig", new[] {
            Pair("host", GrobValue.FromString("example.com")),
            Pair("port", GrobValue.FromInt(8080)),
        }, isAnonymous: true);

        Assert.Equal("#{ host: \"example.com\", port: 8080 }", New().Display(GrobValue.FromStruct(s)));
    }

    [Fact]
    public void Display_Array_RendersBracketedElements() {
        var arr = new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2), GrobValue.FromInt(3)]);
        Assert.Equal("[1, 2, 3]", New().Display(GrobValue.FromArray(arr)));
    }

    [Fact]
    public void Display_Map_RendersQuotedKeysAndInspectedValues() {
        var map = new GrobMap();
        map.Set("a", GrobValue.FromInt(1));
        map.Set("b", GrobValue.FromInt(2));
        Assert.Equal("{ \"a\": 1, \"b\": 2 }", New().Display(GrobValue.FromMap(map)));
    }

    [Fact]
    public void Display_NestedComposite_RecursesViaInspect() {
        var inner = new GrobStruct("Config", new[] {
            Pair("host", GrobValue.FromString("example.com")),
            Pair("port", GrobValue.FromInt(8080)),
        });
        var arr = new GrobArray([GrobValue.FromStruct(inner)]);
        Assert.Equal("[Config { host: \"example.com\", port: 8080 }]",
            New().Display(GrobValue.FromArray(arr)));
    }

    [Fact]
    public void Display_EmptyArray_RendersEmptyBrackets() =>
        Assert.Equal("[]", New().Display(GrobValue.FromArray(new GrobArray())));

    [Fact]
    public void Display_EmptyMap_RendersSpacedBraces() =>
        Assert.Equal("{ }", New().Display(GrobValue.FromMap(new GrobMap())));

    [Fact]
    public void Display_EmptyNamedStruct_RendersNameAndSpacedBraces() =>
        Assert.Equal("Empty { }", New().Display(GrobValue.FromStruct(new GrobStruct("Empty"))));

    [Fact]
    public void Display_EmptyAnonymousStruct_RendersHashSpacedBraces() =>
        Assert.Equal("#{ }", New().Display(GrobValue.FromStruct(new GrobStruct("<anon>", null, isAnonymous: true))));

    // ----- Cycle detection -----

    [Fact]
    public void Display_ReferenceCycle_RendersCycleMarkerAndTerminates() {
        var a = new GrobStruct("Node");
        var b = new GrobStruct("Node");
        a.SetField("value", GrobValue.FromInt(1));
        a.SetField("next", GrobValue.FromStruct(b));
        b.SetField("value", GrobValue.FromInt(2));
        b.SetField("next", GrobValue.FromStruct(a));   // cycle back to a

        string rendered = New().Display(GrobValue.FromStruct(a));

        Assert.Equal("Node { value: 1, next: Node { value: 2, next: <cycle> } }", rendered);
    }

    [Fact]
    public void Display_SelfCycle_RendersCycleMarker() {
        var a = new GrobStruct("Node");
        a.SetField("next", GrobValue.FromStruct(a));

        Assert.Equal("Node { next: <cycle> }", New().Display(GrobValue.FromStruct(a)));
    }

    [Fact]
    public void Display_ArraySelfCycle_RendersCycleMarker() {
        var arr = new GrobArray([GrobValue.FromInt(1)]);
        GrobValue self = GrobValue.FromArray(arr);
        arr.Add(self);   // arr[1] is the array itself

        Assert.Equal("[1, <cycle>]", New().Display(self));
    }

    [Fact]
    public void Display_MapSelfCycle_RendersCycleMarker() {
        var map = new GrobMap();
        map.Set("a", GrobValue.FromInt(1));
        GrobValue self = GrobValue.FromMap(map);
        map.Set("self", self);

        Assert.Equal("{ \"a\": 1, \"self\": <cycle> }", New().Display(self));
    }

    [Fact]
    public void Display_SharedNonCyclicReference_RendersFullyTwice() {
        // A DAG (same struct referenced twice, no cycle) is not a cycle — both render.
        var shared = new GrobStruct("Point", new[] {
            Pair("x", GrobValue.FromInt(1)),
        });
        var arr = new GrobArray([GrobValue.FromStruct(shared), GrobValue.FromStruct(shared)]);

        Assert.Equal("[Point { x: 1 }, Point { x: 1 }]", New().Display(GrobValue.FromArray(arr)));
    }

    // ----- Depth cap -----

    [Fact]
    public void Display_BeyondMaxDepth_RendersEllipsis() {
        // Build a chain of arrays deeper than the backstop.
        GrobValue current = GrobValue.FromInt(0);
        for (int i = 0; i <= ValueDisplay.MaxDepth; i++)
            current = GrobValue.FromArray(new GrobArray([current]));

        string rendered = New().Display(current);

        Assert.Contains("...", rendered);
    }

    [Fact]
    public void Display_StructBeyondMaxDepth_RendersEllipsis() {
        GrobValue current = GrobValue.FromInt(0);
        for (int i = 0; i <= ValueDisplay.MaxDepth; i++) {
            var wrap = new GrobStruct("Wrap");
            wrap.SetField("inner", current);
            current = GrobValue.FromStruct(wrap);
        }

        Assert.Contains("...", New().Display(current));
    }

    [Fact]
    public void Display_MapBeyondMaxDepth_RendersEllipsis() {
        GrobValue current = GrobValue.FromInt(0);
        for (int i = 0; i <= ValueDisplay.MaxDepth; i++) {
            var wrap = new GrobMap();
            wrap.Set("inner", current);
            current = GrobValue.FromMap(wrap);
        }

        Assert.Contains("...", New().Display(current));
    }

    // ----- Properties as Theory rows (D-336 decision 4): determinism, termination -----

    public static IEnumerable<object[]> RepresentativeValues() {
        yield return [GrobValue.Nil];
        yield return [GrobValue.FromBool(true)];
        yield return [GrobValue.FromInt(-123)];
        yield return [GrobValue.FromFloat(0.1 + 0.2)];
        yield return [GrobValue.FromString("a\"b\n")];
        yield return [Config()];
        yield return [GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1), Config()]))];
    }

    [Theory]
    [MemberData(nameof(RepresentativeValues))]
    public void Display_IsDeterministic_AcrossRepeatedCalls(GrobValue value) {
        var display = New();
        Assert.Equal(display.Display(value), display.Display(value));
    }

    [Theory]
    [MemberData(nameof(RepresentativeValues))]
    public void DisplayAndInspect_Terminate_AndProduceBalancedBrackets(GrobValue value) {
        string rendered = New().Display(value);
        int opens = rendered.Count(c => c is '[' or '{');
        int closes = rendered.Count(c => c is ']' or '}');
        Assert.Equal(opens, closes);
    }
}
