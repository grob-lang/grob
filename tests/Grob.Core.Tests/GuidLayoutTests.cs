using Xunit;

namespace Grob.Core.Tests;

/// <summary>
/// <see cref="GuidLayout"/> holds the <c>guid</c> layout constants <c>Grob.Vm</c> and
/// <c>Grob.Stdlib</c>'s <c>GuidPlugin</c> both read (R-19, folded into D-426's change
/// group 8). Every stored <c>guid</c> value carries this type name and field, so the
/// literals are pinned here rather than derived.
/// </summary>
public sealed class GuidLayoutTests {
    [Fact]
    public void TypeName_IsGuid() => Assert.Equal("guid", GuidLayout.TypeName);

    [Fact]
    public void ValueFieldName_IsTheUnspellableHiddenField() => Assert.Equal("__value", GuidLayout.ValueFieldName);
}
