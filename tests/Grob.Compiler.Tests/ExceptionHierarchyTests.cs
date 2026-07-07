using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Unit tests for <see cref="ExceptionHierarchy"/> — the Sprint 7 Increment A
/// leaf-to-root subtype walk (D-284). Pure logic, no parser/type-checker
/// involvement.
/// </summary>
public sealed class ExceptionHierarchyTests {
    [Theory]
    [InlineData("IoError")]
    [InlineData("NetworkError")]
    [InlineData("JsonError")]
    [InlineData("ProcessError")]
    [InlineData("NilError")]
    [InlineData("ArithmeticError")]
    [InlineData("IndexError")]
    [InlineData("ParseError")]
    [InlineData("LookupError")]
    [InlineData("RuntimeError")]
    public void IsSubtypeOf_LeafToRoot_IsTrue(string leaf) {
        Assert.True(ExceptionHierarchy.IsSubtypeOf(leaf, ExceptionHierarchy.Root));
    }

    [Fact]
    public void IsSubtypeOf_RootToLeaf_IsFalse() {
        Assert.False(ExceptionHierarchy.IsSubtypeOf(ExceptionHierarchy.Root, "IoError"));
    }

    [Fact]
    public void IsSubtypeOf_SiblingLeaves_IsFalse() {
        Assert.False(ExceptionHierarchy.IsSubtypeOf("IoError", "NetworkError"));
    }

    [Fact]
    public void IsSubtypeOf_ReflexiveOnExactMatch() {
        Assert.True(ExceptionHierarchy.IsSubtypeOf("IoError", "IoError"));
        Assert.True(ExceptionHierarchy.IsSubtypeOf(ExceptionHierarchy.Root, ExceptionHierarchy.Root));
    }

    [Fact]
    public void IsSubtypeOf_UnrecognisedName_IsFalse() {
        // No flat/exact-match special case: an arbitrary struct name walks zero
        // steps and is rejected, without any bespoke "not a hierarchy member" branch.
        Assert.False(ExceptionHierarchy.IsSubtypeOf("Config", ExceptionHierarchy.Root));
    }

    [Fact]
    public void AllNames_ContainsRootAndAllTenLeaves() {
        Assert.Equal(11, ExceptionHierarchy.AllNames.Count);
        Assert.Contains(ExceptionHierarchy.Root, ExceptionHierarchy.AllNames);
        foreach (string leaf in new[] {
            "IoError", "NetworkError", "JsonError", "ProcessError", "NilError",
            "ArithmeticError", "IndexError", "ParseError", "LookupError", "RuntimeError",
        }) {
            Assert.Contains(leaf, ExceptionHierarchy.AllNames);
        }
    }

    [Fact]
    public void FieldsFor_Root_HasMessageAndLocation() {
        var fields = ExceptionHierarchy.FieldsFor(ExceptionHierarchy.Root);
        Assert.Equal(2, fields.Count);
        Assert.Contains(fields, f => f.Name == "message" && f.Kind == GrobType.String && f.IsRequired);
        Assert.Contains(fields, f => f.Name == "location" && f.Kind == GrobType.Unknown && !f.IsRequired);
    }

    [Fact]
    public void FieldsFor_NetworkError_AddsStatusCode() {
        var fields = ExceptionHierarchy.FieldsFor("NetworkError");
        Assert.Equal(3, fields.Count);
        Assert.Contains(fields, f => f.Name == "statusCode" && f.Kind == GrobType.NullableInt && !f.IsRequired);
    }

    [Fact]
    public void FieldsFor_OtherLeaf_HasOnlyMessageAndLocation() {
        var fields = ExceptionHierarchy.FieldsFor("IoError");
        Assert.Equal(2, fields.Count);
    }

    [Fact]
    public void TypeFieldsFor_MatchesFieldsForCount() {
        foreach (string name in ExceptionHierarchy.AllNames) {
            Assert.Equal(ExceptionHierarchy.FieldsFor(name).Count, ExceptionHierarchy.TypeFieldsFor(name).Count);
        }
    }
}
