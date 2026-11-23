using Grob.Compiler.Binding;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// The public read of array <c>sort</c>'s defaulted parameters, which the drift check
/// (R-55) pins to the documents. It reads the binder's own view, so these tests hold
/// the view and the read together.
/// </summary>
public sealed class ArrayMethodParameterNamesTests {
    [Fact]
    public void SortDefaulted_BinderView_NamesDescendingOnly() {
        IReadOnlyList<string> names = ArrayMethodParameterNames.SortDefaulted();

        Assert.Equal(["descending"], names);
    }

    [Fact]
    public void SortDefaulted_BinderView_ExcludesTheRequiredKey() {
        var view = new ArraySortParameters();

        Assert.False(view.HasDefaultAt(0));
        Assert.DoesNotContain(view.NameAt(0), ArrayMethodParameterNames.SortDefaulted());
    }
}
