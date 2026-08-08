using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

/// <summary>
/// Unit tests for <see cref="GrobTypeHelpers"/> — nullable variance helpers.
/// Covers the <see cref="GrobType.AnonStruct"/> / <see cref="GrobType.NullableAnonStruct"/>
/// arms added in Sprint 6 Increment D alongside the existing nullable kinds.
/// </summary>
public sealed class GrobTypeHelpersTests {
    // -----------------------------------------------------------------------
    // IsNullable
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(GrobType.NullableInt)]
    [InlineData(GrobType.NullableFloat)]
    [InlineData(GrobType.NullableString)]
    [InlineData(GrobType.NullableBool)]
    [InlineData(GrobType.NullableFunction)]
    [InlineData(GrobType.NullableArray)]
    [InlineData(GrobType.NullableStruct)]
    [InlineData(GrobType.NullableAnonStruct)]
    [InlineData(GrobType.NullableMap)]
    public void GrobTypeHelpers_IsNullable_NullableVariants_ReturnsTrue(GrobType type) =>
        Assert.True(GrobTypeHelpers.IsNullable(type));

    [Theory]
    [InlineData(GrobType.Int)]
    [InlineData(GrobType.Struct)]
    [InlineData(GrobType.AnonStruct)]
    [InlineData(GrobType.Unknown)]
    [InlineData(GrobType.Map)]
    public void GrobTypeHelpers_IsNullable_NonNullableVariants_ReturnsFalse(GrobType type) =>
        Assert.False(GrobTypeHelpers.IsNullable(type));

    // -----------------------------------------------------------------------
    // ToNullable
    // -----------------------------------------------------------------------

    [Fact]
    public void GrobTypeHelpers_ToNullable_AnonStruct_ReturnsNullableAnonStruct() =>
        Assert.Equal(GrobType.NullableAnonStruct, GrobTypeHelpers.ToNullable(GrobType.AnonStruct));

    [Fact]
    public void GrobTypeHelpers_ToNullable_NullableAnonStruct_IsIdempotent() =>
        Assert.Equal(GrobType.NullableAnonStruct, GrobTypeHelpers.ToNullable(GrobType.NullableAnonStruct));

    [Fact]
    public void GrobTypeHelpers_ToNullable_Struct_ReturnsNullableStruct() =>
        Assert.Equal(GrobType.NullableStruct, GrobTypeHelpers.ToNullable(GrobType.Struct));

    [Fact]
    public void GrobTypeHelpers_ToNullable_Map_ReturnsNullableMap() =>
        Assert.Equal(GrobType.NullableMap, GrobTypeHelpers.ToNullable(GrobType.Map));

    [Fact]
    public void GrobTypeHelpers_ToNullable_NullableMap_IsIdempotent() =>
        Assert.Equal(GrobType.NullableMap, GrobTypeHelpers.ToNullable(GrobType.NullableMap));

    // -----------------------------------------------------------------------
    // ElementType
    // -----------------------------------------------------------------------

    [Fact]
    public void GrobTypeHelpers_ElementType_NullableAnonStruct_ReturnsAnonStruct() =>
        Assert.Equal(GrobType.AnonStruct, GrobTypeHelpers.ElementType(GrobType.NullableAnonStruct));

    [Fact]
    public void GrobTypeHelpers_ElementType_AnonStruct_IsIdentity() =>
        Assert.Equal(GrobType.AnonStruct, GrobTypeHelpers.ElementType(GrobType.AnonStruct));

    [Fact]
    public void GrobTypeHelpers_ElementType_NullableMap_ReturnsMap() =>
        Assert.Equal(GrobType.Map, GrobTypeHelpers.ElementType(GrobType.NullableMap));

    [Fact]
    public void GrobTypeHelpers_ElementType_Map_IsIdentity() =>
        Assert.Equal(GrobType.Map, GrobTypeHelpers.ElementType(GrobType.Map));
}
