using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>Source edits are made against the original text, in offset order, and never overlap.</summary>
public sealed class SourceEditTests {
    [Fact]
    public void Apply_Replace_Insert_AndDelete_EditTheText() {
        Assert.Equal("a2c", SourceEdit.Apply("abc", [SourceEdit.Replace(1, 2, "2")]));
        Assert.Equal("abXc", SourceEdit.Apply("abc", [SourceEdit.Insert(2, "X")]));
        Assert.Equal("ac", SourceEdit.Apply("abc", [SourceEdit.Delete(1, 2)]));
    }

    [Fact]
    public void Apply_SeveralEdits_AreEachMadeAgainstTheOriginalText() {
        // Listed out of order: the later offset must not shift the earlier.
        string result = SourceEdit.Apply("0123456789", [SourceEdit.Replace(6, 8, "XYZ"), SourceEdit.Delete(1, 3)]);

        Assert.Equal("0345XYZ89", result);
    }

    [Fact]
    public void Apply_NoEdits_GivesTheSource() {
        Assert.Equal("abc", SourceEdit.Apply("abc", []));
    }

    [Fact]
    public void Apply_InsertionAtAnotherEditsStart_IsMadeBeforeIt() {
        Assert.Equal("XYc", SourceEdit.Apply("abc", [SourceEdit.Replace(0, 2, "Y"), SourceEdit.Insert(0, "X")]));
    }

    [Fact]
    public void Apply_OverlappingEdits_Throw() {
        Assert.Throws<ArgumentException>(() => SourceEdit.Apply("abcdef", [SourceEdit.Delete(1, 4), SourceEdit.Delete(3, 5)]));
    }

    [Fact]
    public void Apply_EditPastTheEnd_Throws() {
        Assert.Throws<ArgumentException>(() => SourceEdit.Apply("abc", [SourceEdit.Delete(2, 9)]));
    }

    [Fact]
    public void Apply_EditWithItsEndBeforeItsStart_Throws() {
        Assert.Throws<ArgumentException>(() => SourceEdit.Apply("abc", [new SourceEdit(2, 1, string.Empty)]));
    }
}
