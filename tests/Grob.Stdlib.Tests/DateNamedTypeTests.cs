using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// The <c>date</c> named type as <see cref="DatePlugin"/> registers it (D-426 Decision 3,
/// change group 7): the nine properties and eighteen methods, their signatures (parameter
/// names from the gate report, nominal <c>date</c> parameters and returns) and the
/// renderer, read straight off the catalogue the plugin builds. The behaviour asserted
/// here is what <c>NamedTypeRegistry</c>'s <c>date</c> entry used to carry, re-homed with
/// the code (gate report F6); the end-to-end dispatch through a VM is
/// <see cref="DatePluginTests"/>' job.
/// </summary>
public sealed class DateNamedTypeTests {
    private static readonly NativeCatalogue Catalogue = new NativeCatalogueBuilder()
        .Add(new DatePlugin(new TestClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))))
        .Build();

    private static readonly SignatureType.Concrete DateType = SignatureType.Named(GrobType.Struct, "date");

    private static NamedTypeSurface Date =>
        Catalogue.TryGetNamedType("date", out NamedTypeSurface? surface)
            ? surface
            : throw new InvalidOperationException("DatePlugin did not register the date named type.");

    private static GrobStruct MakeDate(string roundTrip) =>
        new("date", [new KeyValuePair<string, GrobValue>("__value", GrobValue.FromString(roundTrip))]);

    private static GrobValue Get(string property, GrobStruct receiver) => Date.Properties[property].Get(receiver);

    private static GrobValue Call(string method, GrobStruct receiver, params GrobValue[] args) =>
        Date.Methods[method].Bind(receiver).Implementation(args, default);

    // ----- Registration shape -----

    [Fact]
    public void Register_DeclaresTheDateNamedType() {
        Assert.Equal(["date"], Catalogue.NamedTypeNames);
        Assert.Equal("date", Date.Owner);
        Assert.Equal("date", Date.Definition.Name);
    }

    [Fact]
    public void Register_DeclaresNinePropertiesAndEighteenMethods() {
        Assert.Equal(9, Date.Properties.Count);
        Assert.Equal(18, Date.Methods.Count);
    }

    [Theory]
    [InlineData("year", GrobType.Int)]
    [InlineData("month", GrobType.Int)]
    [InlineData("day", GrobType.Int)]
    [InlineData("hour", GrobType.Int)]
    [InlineData("minute", GrobType.Int)]
    [InlineData("second", GrobType.Int)]
    [InlineData("dayOfYear", GrobType.Int)]
    [InlineData("dayOfWeek", GrobType.String)]
    [InlineData("utcOffset", GrobType.Int)]
    public void Property_HasItsDeclaredType(string name, GrobType expected) {
        PropertySignature signature = Date.Properties[name].Signature;

        Assert.Equal(name, signature.Name);
        Assert.Equal(SignatureType.Of(expected), signature.Type);
    }

    [Theory]
    [InlineData("addDays", "days")]
    [InlineData("addMonths", "months")]
    [InlineData("addHours", "hours")]
    [InlineData("addMinutes", "minutes")]
    [InlineData("format", "pattern")]
    [InlineData("toZone", "zone")]
    public void Method_WithOnePrimitiveParameter_UsesTheGateReportName(string method, string parameterName) {
        SignatureParameter parameter = Assert.Single(Date.Methods[method].Signature.Parameters);

        Assert.Equal(parameterName, parameter.Name);
        Assert.False(parameter.IsVariadic);
        Assert.Null(parameter.Default);
    }

    [Theory]
    [InlineData("addDays", GrobType.Int)]
    [InlineData("addMonths", GrobType.Int)]
    [InlineData("addHours", GrobType.Int)]
    [InlineData("addMinutes", GrobType.Int)]
    [InlineData("format", GrobType.String)]
    [InlineData("toZone", GrobType.String)]
    public void Method_WithOnePrimitiveParameter_DeclaresItsType(string method, GrobType expected) =>
        Assert.Equal(SignatureType.Of(expected), Assert.Single(Date.Methods[method].Signature.Parameters).Type);

    [Theory]
    [InlineData("isBefore")]
    [InlineData("isAfter")]
    [InlineData("daysUntil")]
    [InlineData("daysSince")]
    public void Method_TakingAnotherDate_DeclaresTheNominalDateParameterNamedOther(string method) {
        SignatureParameter parameter = Assert.Single(Date.Methods[method].Signature.Parameters);

        Assert.Equal("other", parameter.Name);
        Assert.Equal(DateType, parameter.Type);
    }

    [Theory]
    [InlineData("addDays")]
    [InlineData("addMonths")]
    [InlineData("addHours")]
    [InlineData("addMinutes")]
    [InlineData("toUtc")]
    [InlineData("toLocal")]
    [InlineData("toZone")]
    [InlineData("toDateOnly")]
    [InlineData("toTimeOnly")]
    public void Method_ReturningADate_DeclaresTheNominalDateReturn(string method) =>
        Assert.Equal(DateType, Date.Methods[method].Signature.ReturnType);

    [Theory]
    [InlineData("isBefore", GrobType.Bool)]
    [InlineData("isAfter", GrobType.Bool)]
    [InlineData("toIso", GrobType.String)]
    [InlineData("toIsoDateTime", GrobType.String)]
    [InlineData("format", GrobType.String)]
    [InlineData("toUnixSeconds", GrobType.Int)]
    [InlineData("toUnixMillis", GrobType.Int)]
    [InlineData("daysUntil", GrobType.Int)]
    [InlineData("daysSince", GrobType.Int)]
    public void Method_ReturningAPrimitive_DeclaresIt(string method, GrobType expected) =>
        Assert.Equal(SignatureType.Of(expected), Date.Methods[method].Signature.ReturnType);

    [Theory]
    [InlineData("toIso")]
    [InlineData("toIsoDateTime")]
    [InlineData("toUnixSeconds")]
    [InlineData("toUnixMillis")]
    [InlineData("toUtc")]
    [InlineData("toLocal")]
    [InlineData("toDateOnly")]
    [InlineData("toTimeOnly")]
    public void Method_WithNoParameters_DeclaresNone(string method) =>
        Assert.Empty(Date.Methods[method].Signature.Parameters);

    [Fact]
    public void Methods_AreAllNonGenericAndBareNamed() {
        Assert.All(Date.Methods.Values, m => {
            Assert.False(m.Signature.IsGeneric);
            Assert.False(m.Signature.IsVariadic);
            Assert.DoesNotContain('.', m.Signature.Name);
        });
    }

    // ----- Behaviour, as the former registry tests asserted it -----

    [Fact]
    public void Properties_ReadEveryComponent() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");

        Assert.Equal(2026, Get("year", d).AsInt());
        Assert.Equal(3, Get("month", d).AsInt());
        Assert.Equal(15, Get("day", d).AsInt());
        Assert.Equal(9, Get("hour", d).AsInt());
        Assert.Equal(30, Get("minute", d).AsInt());
        Assert.Equal(45, Get("second", d).AsInt());
        Assert.Equal(74, Get("dayOfYear", d).AsInt());
        Assert.Equal("Sunday", Get("dayOfWeek", d).AsString());
        Assert.Equal(0, Get("utcOffset", d).AsInt());
    }

    [Fact]
    public void UtcOffset_ReportsMinutesOfANonZeroOffset() =>
        Assert.Equal(90, Get("utcOffset", MakeDate("2026-03-15T09:30:45+01:30")).AsInt());

    [Fact]
    public void AddDays_ReturnsANewDateStruct() {
        GrobValue result = Call("addDays", MakeDate("2026-03-15T09:30:45+00:00"), GrobValue.FromInt(1));

        Assert.True(result.TryAsStruct(out GrobStruct? resultStruct));
        Assert.Equal("date", resultStruct!.TypeName);
        Assert.Equal(16, Get("day", resultStruct).AsInt());
    }

    [Fact]
    public void AddMonths_AddHours_AddMinutes_ShiftTheirComponent() {
        GrobStruct d = MakeDate("2026-03-15T09:30:45+00:00");

        Assert.Equal(4, Get("month", Call("addMonths", d, GrobValue.FromInt(1)).AsStruct()).AsInt());
        Assert.Equal(11, Get("hour", Call("addHours", d, GrobValue.FromInt(2)).AsStruct()).AsInt());
        Assert.Equal(45, Get("minute", Call("addMinutes", d, GrobValue.FromInt(15)).AsStruct()).AsInt());
    }

    [Fact]
    public void AddMonths_OverflowingInt32_ThrowsRatherThanWrapping() =>
        Assert.Throws<OverflowException>(() =>
            Call("addMonths", MakeDate("2026-03-15T09:30:45+00:00"), GrobValue.FromInt(1L + int.MaxValue)));

    [Fact]
    public void IsBefore_IsAfter_CompareInstantOrder() {
        GrobStruct earlier = MakeDate("2026-03-15T09:30:45+00:00");
        GrobStruct later = MakeDate("2026-03-16T09:30:45+00:00");

        Assert.True(Call("isBefore", earlier, GrobValue.FromStruct(later)).AsBool());
        Assert.False(Call("isBefore", later, GrobValue.FromStruct(earlier)).AsBool());
        Assert.True(Call("isAfter", later, GrobValue.FromStruct(earlier)).AsBool());
        Assert.False(Call("isAfter", earlier, GrobValue.FromStruct(later)).AsBool());
    }

    [Fact]
    public void DaysUntil_DaysSince_ComputeWholeDayIntervals() {
        GrobStruct from = MakeDate("2026-03-15T00:00:00+00:00");
        GrobStruct to = MakeDate("2026-03-20T00:00:00+00:00");

        Assert.Equal(5, Call("daysUntil", from, GrobValue.FromStruct(to)).AsInt());
        Assert.Equal(5, Call("daysSince", to, GrobValue.FromStruct(from)).AsInt());
    }

    [Fact]
    public void ToIso_RendersTheDateOnlyForm() =>
        Assert.Equal("2026-03-15", Call("toIso", MakeDate("2026-03-15T09:30:45+00:00")).AsString());

    [Fact]
    public void ToIsoDateTime_UsesZForAZeroOffset() =>
        Assert.Equal("2026-03-15T09:30:45Z", Call("toIsoDateTime", MakeDate("2026-03-15T09:30:45+00:00")).AsString());

    [Fact]
    public void ToIsoDateTime_UsesTheOffsetSuffixOtherwise() =>
        Assert.Equal("2026-03-15T09:30:45+02:00", Call("toIsoDateTime", MakeDate("2026-03-15T09:30:45+02:00")).AsString());

    [Fact]
    public void Render_MatchesToIsoDateTime() {
        GrobValue d = GrobValue.FromStruct(MakeDate("2026-03-15T09:30:45+00:00"));

        Assert.Equal("2026-03-15T09:30:45Z", Date.Definition.Render(d));
        Assert.Equal("2026-03-15T09:30:45Z", Catalogue.RenderValue(d));
    }

    [Fact]
    public void ToUnixSeconds_ToUnixMillis_CountFromTheEpoch() {
        GrobStruct d = MakeDate("1970-01-01T00:00:10+00:00");

        Assert.Equal(10, Call("toUnixSeconds", d).AsInt());
        Assert.Equal(10_000, Call("toUnixMillis", d).AsInt());
    }

    [Fact]
    public void ToDateOnly_ZeroesTheTimeComponents() {
        GrobStruct result = Call("toDateOnly", MakeDate("2026-03-15T09:30:45+00:00")).AsStruct();

        Assert.Equal(0, Get("hour", result).AsInt());
        Assert.Equal(0, Get("minute", result).AsInt());
        Assert.Equal(15, Get("day", result).AsInt());
    }

    [Fact]
    public void ToTimeOnly_MovesTheTimeOntoTheEpochDay() {
        GrobStruct result = Call("toTimeOnly", MakeDate("2026-03-15T09:30:45+02:00")).AsStruct();

        Assert.Equal(1970, Get("year", result).AsInt());
        Assert.Equal(1, Get("day", result).AsInt());
        Assert.Equal(9, Get("hour", result).AsInt());
        Assert.Equal(120, Get("utcOffset", result).AsInt());
    }

    [Fact]
    public void ToUtc_RebasesToAZeroOffset() {
        GrobStruct result = Call("toUtc", MakeDate("2026-03-15T09:30:45+02:00")).AsStruct();

        Assert.Equal(7, Get("hour", result).AsInt());
        Assert.Equal(0, Get("utcOffset", result).AsInt());
    }

    [Fact]
    public void ToZone_ConvertsToTheNamedZone() {
        GrobStruct result = Call("toZone", MakeDate("2026-07-15T12:00:00+00:00"), GrobValue.FromString("UTC")).AsStruct();

        Assert.Equal(12, Get("hour", result).AsInt());
        Assert.Equal(0, Get("utcOffset", result).AsInt());
    }

    [Fact]
    public void Format_UsesTheSuppliedPattern() =>
        Assert.Equal("2026", Call("format", MakeDate("2026-03-15T09:30:45+00:00"), GrobValue.FromString("yyyy")).AsString());
}
