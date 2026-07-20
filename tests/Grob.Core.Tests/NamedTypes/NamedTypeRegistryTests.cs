using Grob.Core.NamedTypes;
using Xunit;

namespace Grob.Core.Tests.NamedTypes;

/// <summary>
/// Direct unit coverage of <see cref="NamedTypeRegistry"/>'s <c>guid</c>/<c>date</c>
/// entries (D-356) — exercises every property getter, every method binder and the
/// <c>toString()</c> renderer straight off the entry, independent of the compiler or VM
/// call sites that consult them. The behaviour asserted here must match
/// <c>Grob.Vm.GuidNatives</c>/<c>DateNatives</c>'s former per-type arms exactly.
/// </summary>
public sealed class NamedTypeRegistryTests {
    private static GrobStruct MakeGuid(string canonical) =>
        new("guid", [new KeyValuePair<string, GrobValue>("__value", GrobValue.FromString(canonical))]);

    private static GrobStruct MakeDate(string roundTrip) =>
        new("date", [new KeyValuePair<string, GrobValue>("__value", GrobValue.FromString(roundTrip))]);

    [Fact]
    public void Names_ContainsGuidAndDate() {
        Assert.Equal(["guid", "date"], NamedTypeRegistry.Names);
    }

    [Fact]
    public void TryGet_UnregisteredName_ReturnsFalse() {
        Assert.False(NamedTypeRegistry.TryGet("nope", out _));
    }

    [Theory]
    [InlineData("guid")]
    [InlineData("date")]
    public void TryGet_RegisteredName_ReturnsMatchingEntry(string name) {
        Assert.True(NamedTypeRegistry.TryGet(name, out NamedTypeEntry entry));
        Assert.Equal(name, entry.CanonicalName);
    }

    // -----------------------------------------------------------------------
    // guid
    // -----------------------------------------------------------------------

    [Fact]
    public void GuidVersion_ReadsUuidVersion() {
        GrobStruct g = MakeGuid("01234567-89ab-4def-8123-456789abcdef");
        Assert.Equal(4, NamedTypeRegistry.Guid.Properties["version"].Get(g).AsInt());
    }

    [Fact]
    public void GuidIsEmpty_TrueForAllZeros() {
        GrobStruct empty = MakeGuid("00000000-0000-0000-0000-000000000000");
        GrobStruct nonEmpty = MakeGuid("01234567-89ab-4def-8123-456789abcdef");
        Assert.True(NamedTypeRegistry.Guid.Properties["isEmpty"].Get(empty).AsBool());
        Assert.False(NamedTypeRegistry.Guid.Properties["isEmpty"].Get(nonEmpty).AsBool());
    }

    [Fact]
    public void GuidToString_ReturnsCanonicalLowercaseForm() {
        GrobStruct g = MakeGuid("01234567-89ab-4def-8123-456789abcdef");
        NativeFunction fn = NamedTypeRegistry.Guid.Methods["toString"].Bind(g);
        Assert.Equal("01234567-89ab-4def-8123-456789abcdef", fn.Implementation([], null!).AsString());
    }

    [Fact]
    public void GuidToUpperString_UppercasesCanonicalForm() {
        GrobStruct g = MakeGuid("01234567-89ab-4def-8123-456789abcdef");
        NativeFunction fn = NamedTypeRegistry.Guid.Methods["toUpperString"].Bind(g);
        Assert.Equal("01234567-89AB-4DEF-8123-456789ABCDEF", fn.Implementation([], null!).AsString());
    }

    [Fact]
    public void GuidToCompactString_StripsHyphens() {
        GrobStruct g = MakeGuid("01234567-89ab-4def-8123-456789abcdef");
        NativeFunction fn = NamedTypeRegistry.Guid.Methods["toCompactString"].Bind(g);
        Assert.Equal("0123456789ab4def8123456789abcdef", fn.Implementation([], null!).AsString());
    }

    [Fact]
    public void GuidToStringRenderer_MatchesCanonicalForm() {
        GrobValue g = GrobValue.FromStruct(MakeGuid("01234567-89ab-4def-8123-456789abcdef"));
        Assert.Equal("01234567-89ab-4def-8123-456789abcdef", NamedTypeRegistry.Guid.ToStringRenderer(g));
    }

    // -----------------------------------------------------------------------
    // date
    // -----------------------------------------------------------------------

    [Fact]
    public void DateProperties_ReadEveryComponent() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");
        NamedTypeEntry entry = NamedTypeRegistry.Date;
        Assert.Equal(2026, entry.Properties["year"].Get(d).AsInt());
        Assert.Equal(3, entry.Properties["month"].Get(d).AsInt());
        Assert.Equal(15, entry.Properties["day"].Get(d).AsInt());
        Assert.Equal(9, entry.Properties["hour"].Get(d).AsInt());
        Assert.Equal(30, entry.Properties["minute"].Get(d).AsInt());
        Assert.Equal(45, entry.Properties["second"].Get(d).AsInt());
        Assert.Equal(74, entry.Properties["dayOfYear"].Get(d).AsInt());
        Assert.Equal("Sunday", entry.Properties["dayOfWeek"].Get(d).AsString());
        Assert.Equal(0, entry.Properties["utcOffset"].Get(d).AsInt());
    }

    [Fact]
    public void DateAddDays_ReturnsNewDateStruct() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["addDays"].Bind(d);
        GrobValue result = fn.Implementation([GrobValue.FromInt(1)], null!);
        Assert.True(result.TryAsStruct(out GrobStruct? resultStruct));
        Assert.Equal("date", resultStruct!.TypeName);
        Assert.Equal(16, NamedTypeRegistry.Date.Properties["day"].Get(resultStruct).AsInt());
    }

    [Fact]
    public void DateAddDays_IsRegisteredAsReturningNominalSelf() {
        Assert.True(NamedTypeRegistry.Date.Methods["addDays"].ReturnsNominalSelf);
        Assert.Equal(GrobType.Struct, NamedTypeRegistry.Date.Methods["addDays"].ReturnType);
    }

    [Fact]
    public void DateIsBefore_ComparesInstantOrder() {
        GrobStruct earlier = MakeDate("2026-03-15T09:30:45+00:00");
        GrobStruct later = MakeDate("2026-03-16T09:30:45+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["isBefore"].Bind(earlier);
        Assert.True(fn.Implementation([GrobValue.FromStruct(later)], null!).AsBool());
    }

    [Fact]
    public void DateIsBefore_ParameterIsNominalSelf() {
        NamedTypeParameter param = Assert.Single(NamedTypeRegistry.Date.Methods["isBefore"].Parameters);
        Assert.Equal(NamedTypeParameterKind.NominalSelf, param.Kind);
        Assert.Equal(GrobType.Struct, param.Type);
    }

    [Fact]
    public void DaysUntil_ComputesWholeDayInterval() {
        GrobStruct from = MakeDate("2026-03-15T00:00:00+00:00");
        GrobStruct to = MakeDate("2026-03-20T00:00:00+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["daysUntil"].Bind(from);
        Assert.Equal(5, fn.Implementation([GrobValue.FromStruct(to)], null!).AsInt());
    }

    [Fact]
    public void ToIso_RendersDateOnlyForm() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["toIso"].Bind(d);
        Assert.Equal("2026-03-15", fn.Implementation([], null!).AsString());
    }

    [Fact]
    public void ToIsoDateTime_UsesZSuffixForZeroOffset() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["toIsoDateTime"].Bind(d);
        Assert.Equal("2026-03-15T09:30:45Z", fn.Implementation([], null!).AsString());
    }

    [Fact]
    public void ToIsoDateTime_UsesOffsetSuffixForNonZeroOffset() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+02:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["toIsoDateTime"].Bind(d);
        Assert.Equal("2026-03-15T09:30:45+02:00", fn.Implementation([], null!).AsString());
    }

    [Fact]
    public void DateToStringRenderer_MatchesIsoDateTimeString() {
        GrobValue d = GrobValue.FromStruct(MakeDate("2026-03-15T09:30:45+00:00"));
        Assert.Equal("2026-03-15T09:30:45Z", NamedTypeRegistry.Date.ToStringRenderer(d));
    }

    [Fact]
    public void ToUnixSeconds_ConvertsFromEpoch() {
        GrobStruct d = MakeDate("1970-01-01T00:00:10+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["toUnixSeconds"].Bind(d);
        Assert.Equal(10, fn.Implementation([], null!).AsInt());
    }

    [Fact]
    public void ToDateOnly_ZeroesTimeComponents() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["toDateOnly"].Bind(d);
        GrobValue result = fn.Implementation([], null!);
        Assert.True(result.TryAsStruct(out GrobStruct? resultStruct));
        Assert.Equal(0, NamedTypeRegistry.Date.Properties["hour"].Get(resultStruct!).AsInt());
        Assert.Equal(15, NamedTypeRegistry.Date.Properties["day"].Get(resultStruct!).AsInt());
    }

    [Fact]
    public void Format_UsesSuppliedFormatString() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");
        NativeFunction fn = NamedTypeRegistry.Date.Methods["format"].Bind(d);
        Assert.Equal("2026", fn.Implementation([GrobValue.FromString("yyyy")], null!).AsString());
    }
}
