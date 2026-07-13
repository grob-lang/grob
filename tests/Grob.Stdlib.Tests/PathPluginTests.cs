using System.IO;

using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment B: <see cref="PathPlugin"/> registers the full decompose/join/
/// normalise surface (D-342), pure — no capability injection, no throw sites. End to
/// end through a real <see cref="VirtualMachine"/>; chunks are hand constructed — this
/// project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
/// <remarks>
/// Per ADR-0007, <c>path</c>'s functions are "platform-aware at runtime" — Grob's own
/// CI matrix runs this project on both <c>windows-latest</c> and <c>ubuntu-latest</c>,
/// so every expectation here is computed via <see cref="Path"/> at test-run time
/// (<see cref="Path.Combine(string[])"/>, <see cref="Path.DirectorySeparatorChar"/>,
/// <see cref="Path.GetTempPath"/> for a value guaranteed rooted on any OS) rather than
/// a literal Windows path — a hardcoded <c>C:\...</c> expectation only ever holds on
/// the Windows leg (regression: PR #130 CI, ubuntu-latest).
/// </remarks>
public sealed class PathPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new PathPlugin().Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsPath() {
        Assert.Equal("path", new PathPlugin().Name);
    }

    [Fact]
    public void Register_AddsExactlyTheDocumentedPathMembers() {
        var vm = new VirtualMachine(new StringWriter());
        new PathPlugin().Register(vm);

        string[] expectedMembers = [
            "separator",
            "join", "joinAll", "extension", "filename", "stem", "directory",
            "resolve", "normalise", "isAbsolute", "isRelative", "changeExtension",
        ];
        foreach (string member in expectedMembers) {
            Assert.True(vm.Globals.ContainsKey($"path.{member}"), $"missing path.{member}");
        }
        Assert.Equal(expectedMembers.Length, vm.Globals.Count);
    }

    [Fact]
    public void Separator_MatchesPlatformDirectorySeparator() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("path.separator"));

        Assert.Equal(GrobValue.FromString(Path.DirectorySeparatorChar.ToString()), vm.Stack.Peek());
    }

    [Fact]
    public void Join_MultipleSegments_JoinsWithSeparator() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.join",
            GrobValue.FromString("Reports"), GrobValue.FromString("2026"),
            GrobValue.FromString("Q1"), GrobValue.FromString("summary.xlsx")));

        Assert.Equal(GrobValue.FromString(Path.Combine("Reports", "2026", "Q1", "summary.xlsx")), vm.Stack.Peek());
    }

    [Fact]
    public void Join_OneSegment_ReturnsItUnchanged() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.join", GrobValue.FromString("Reports")));

        Assert.Equal(GrobValue.FromString("Reports"), vm.Stack.Peek());
    }

    [Fact]
    public void JoinAll_ArrayOfSegments_JoinsWithSeparator() {
        var vm = NewRegisteredVm();
        var parts = new GrobArray([
            GrobValue.FromString("Reports"), GrobValue.FromString("2026"), GrobValue.FromString("Q1"),
        ]);
        vm.Run(BuildCallChunk("path.joinAll", GrobValue.FromArray(parts)));

        Assert.Equal(GrobValue.FromString(Path.Combine("Reports", "2026", "Q1")), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("Reports/2026/summary.xlsx", ".xlsx")]
    [InlineData("Reports/2026/SUMMARY.XLSX", ".xlsx")]
    [InlineData("Reports/2026/noext", "")]
    public void Extension_ReturnsLowercasedWithLeadingDot(string path, string expected) {
        // '/' is a valid alternate separator to .NET's Path class on every platform
        // (including Windows), so this input is portable without dynamic construction.
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.extension", GrobValue.FromString(path)));

        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    [Fact]
    public void Filename_ReturnsFinalSegmentWithExtension() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.filename", GrobValue.FromString(Path.Combine("Reports", "2026", "summary.xlsx"))));

        Assert.Equal(GrobValue.FromString("summary.xlsx"), vm.Stack.Peek());
    }

    [Fact]
    public void Stem_ReturnsFinalSegmentWithoutExtension() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.stem", GrobValue.FromString(Path.Combine("Reports", "2026", "summary.xlsx"))));

        Assert.Equal(GrobValue.FromString("summary"), vm.Stack.Peek());
    }

    [Fact]
    public void Directory_ReturnsParentDirectoryPortion() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.directory", GrobValue.FromString(Path.Combine("Reports", "2026", "summary.xlsx"))));

        Assert.Equal(GrobValue.FromString(Path.Combine("Reports", "2026")), vm.Stack.Peek());
    }

    [Fact]
    public void ChangeExtension_ReplacesExtension() {
        // '/' — see the note on Extension_ReturnsLowercasedWithLeadingDot.
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.changeExtension",
            GrobValue.FromString("data/report.xlsx"), GrobValue.FromString(".csv")));

        Assert.Equal(GrobValue.FromString("data/report.csv"), vm.Stack.Peek());
    }

    [Fact]
    public void IsAbsolute_PlatformRootedPath_ReturnsTrue() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isAbsolute", GrobValue.FromString(Path.GetTempPath())));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsAbsolute_BareSegment_ReturnsFalse() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isAbsolute", GrobValue.FromString(Path.Combine("Reports", "2026"))));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsRelative_BareSegment_ReturnsTrue() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isRelative", GrobValue.FromString(Path.Combine("Reports", "2026"))));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsRelative_PlatformRootedPath_ReturnsFalse() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isRelative", GrobValue.FromString(Path.GetTempPath())));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Resolve_RelativePath_AnchorsToCwd() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.resolve", GrobValue.FromString("reports")));

        string result = vm.Stack.Peek().AsString();
        Assert.True(Path.IsPathRooted(result), $"expected rooted, got: {result}");
        Assert.EndsWith("reports", result);
    }

    [Fact]
    public void Resolve_EmptyString_ResolvesToCurrentWorkingDirectory_DoesNotThrow() {
        // Regression: Path.GetFullPath("") throws ArgumentException — path.resolve("")
        // must not let that raw .NET exception escape (CodeRabbit review, PR #130).
        var vm = NewRegisteredVm();

        var ex = Record.Exception(() => vm.Run(BuildCallChunk("path.resolve", GrobValue.FromString(""))));

        Assert.Null(ex);
        string result = vm.Stack.Peek().AsString();
        Assert.True(Path.IsPathRooted(result), $"expected rooted, got: {result}");
        Assert.Equal(Path.GetFullPath("."), result);
    }

    [Fact]
    public void Normalise_AbsoluteMixedSeparatorsWithDotSegments_CollapsesToCanonicalForm() {
        var vm = NewRegisteredVm();
        string root = Path.GetPathRoot(Path.GetTempPath())!;
        string input = root + "Users\\chris/./documents/../downloads";
        string expected = Path.Combine(root, "Users", "chris", "downloads");

        vm.Run(BuildCallChunk("path.normalise", GrobValue.FromString(input)));

        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    [Fact]
    public void Normalise_RelativePath_DoesNotAnchorToCwd() {
        // The case Path.GetFullPath would get wrong: normalise must collapse dot
        // segments in a relative path without absolutising it (that's resolve's job).
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.normalise", GrobValue.FromString("a\\.\\b\\..\\c")));

        Assert.Equal(GrobValue.FromString(Path.Combine("a", "c")), vm.Stack.Peek());
    }
}
