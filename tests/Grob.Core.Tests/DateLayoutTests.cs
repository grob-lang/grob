using Xunit;

namespace Grob.Core.Tests;

/// <summary>
/// <see cref="DateLayout"/> holds the one <c>date</c> layout constant <c>Grob.Vm</c>'s
/// comparison intrinsics and <c>Grob.Stdlib</c>'s <c>DatePlugin</c> both read (D-357,
/// D-367; relocated out of the deleted <c>NamedTypeRegistry</c> by D-426 change group 7).
/// The format is wire-visible: every stored <c>date</c> value carries a string in it, so
/// the literal is pinned here rather than derived.
/// </summary>
public sealed class DateLayoutTests {
    [Fact]
    public void TypeName_IsDate() => Assert.Equal("date", DateLayout.TypeName);

    [Fact]
    public void ValueFieldName_IsTheUnspellableHiddenField() => Assert.Equal("__value", DateLayout.ValueFieldName);

    [Fact]
    public void RoundTripFormat_IsTheOffsetPreservingIsoForm() =>
        Assert.Equal("yyyy-MM-ddTHH:mm:sszzz", DateLayout.RoundTripFormat);

    [Fact]
    public void RoundTripFormat_RoundTripsAnOffsetExactly() {
        var value = new DateTimeOffset(2026, 4, 5, 14, 30, 0, TimeSpan.FromHours(1));

        string stored = value.ToString(DateLayout.RoundTripFormat, System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset parsed = DateTimeOffset.ParseExact(
            stored, DateLayout.RoundTripFormat, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None);

        Assert.Equal("2026-04-05T14:30:00+01:00", stored);
        Assert.Equal(value, parsed);
        Assert.Equal(value.Offset, parsed.Offset);
    }
}
