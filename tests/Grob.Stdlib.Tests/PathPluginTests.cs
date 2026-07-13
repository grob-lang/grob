using Grob.Core;
using Grob.Vm;
using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment B: <see cref="PathPlugin"/> registers the full decompose/join/
/// normalise surface (D-342), Windows-native, pure — no capability injection, no throw
/// sites. End to end through a real <see cref="VirtualMachine"/>; chunks are hand
/// constructed — this project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class PathPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new PathPlugin().Register(vm);
        return vm;
    }

    private static Chunk BuildGetGlobalChunk(string name) {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromString(name));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)idx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static Chunk BuildCallChunk(string calleeName, params GrobValue[] args) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);
        foreach (GrobValue arg in args) {
            int argIdx = chunk.AddConstant(arg);
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)args.Length, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
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
    public void Separator_IsBackslashOnWindows() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("path.separator"));

        Assert.Equal(GrobValue.FromString("\\"), vm.Stack.Peek());
    }

    [Fact]
    public void Join_MultipleSegments_JoinsWithSeparator() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.join",
            GrobValue.FromString("C:\\Reports"), GrobValue.FromString("2026"),
            GrobValue.FromString("Q1"), GrobValue.FromString("summary.xlsx")));

        Assert.Equal(GrobValue.FromString("C:\\Reports\\2026\\Q1\\summary.xlsx"), vm.Stack.Peek());
    }

    [Fact]
    public void Join_OneSegment_ReturnsItUnchanged() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.join", GrobValue.FromString("C:\\Reports")));

        Assert.Equal(GrobValue.FromString("C:\\Reports"), vm.Stack.Peek());
    }

    [Fact]
    public void JoinAll_ArrayOfSegments_JoinsWithSeparator() {
        var vm = NewRegisteredVm();
        var parts = new GrobArray([
            GrobValue.FromString("C:\\Reports"), GrobValue.FromString("2026"), GrobValue.FromString("Q1"),
        ]);
        vm.Run(BuildCallChunk("path.joinAll", GrobValue.FromArray(parts)));

        Assert.Equal(GrobValue.FromString("C:\\Reports\\2026\\Q1"), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("C:\\Reports\\2026\\summary.xlsx", ".xlsx")]
    [InlineData("C:\\Reports\\2026\\SUMMARY.XLSX", ".xlsx")]
    [InlineData("C:\\Reports\\2026\\noext", "")]
    public void Extension_ReturnsLowercasedWithLeadingDot(string path, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.extension", GrobValue.FromString(path)));

        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    [Fact]
    public void Filename_ReturnsFinalSegmentWithExtension() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.filename", GrobValue.FromString("C:\\Reports\\2026\\summary.xlsx")));

        Assert.Equal(GrobValue.FromString("summary.xlsx"), vm.Stack.Peek());
    }

    [Fact]
    public void Stem_ReturnsFinalSegmentWithoutExtension() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.stem", GrobValue.FromString("C:\\Reports\\2026\\summary.xlsx")));

        Assert.Equal(GrobValue.FromString("summary"), vm.Stack.Peek());
    }

    [Fact]
    public void Directory_ReturnsParentDirectoryPortion() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.directory", GrobValue.FromString("C:\\Reports\\2026\\summary.xlsx")));

        Assert.Equal(GrobValue.FromString("C:\\Reports\\2026"), vm.Stack.Peek());
    }

    [Fact]
    public void ChangeExtension_ReplacesExtension() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.changeExtension",
            GrobValue.FromString("C:\\data\\report.xlsx"), GrobValue.FromString(".csv")));

        Assert.Equal(GrobValue.FromString("C:\\data\\report.csv"), vm.Stack.Peek());
    }

    [Fact]
    public void IsAbsolute_DriveRootedPath_ReturnsTrue() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isAbsolute", GrobValue.FromString("C:\\Reports")));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsAbsolute_BareSegment_ReturnsFalse() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isAbsolute", GrobValue.FromString("Reports\\2026")));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsRelative_BareSegment_ReturnsTrue() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isRelative", GrobValue.FromString("Reports\\2026")));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsRelative_DriveRootedPath_ReturnsFalse() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.isRelative", GrobValue.FromString("C:\\Reports")));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Resolve_RelativePath_AnchorsToCwd() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.resolve", GrobValue.FromString("reports")));

        string result = vm.Stack.Peek().AsString();
        Assert.True(System.IO.Path.IsPathRooted(result), $"expected rooted, got: {result}");
        Assert.EndsWith("reports", result);
    }

    [Fact]
    public void Normalise_AbsoluteMixedSeparatorsWithDotSegments_CollapsesToCanonicalForm() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.normalise",
            GrobValue.FromString("C:/Users\\chris/./documents/../downloads")));

        Assert.Equal(GrobValue.FromString("C:\\Users\\chris\\downloads"), vm.Stack.Peek());
    }

    [Fact]
    public void Normalise_RelativePath_DoesNotAnchorToCwd() {
        // The case Path.GetFullPath would get wrong: normalise must collapse dot
        // segments in a relative path without absolutising it (that's resolve's job).
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("path.normalise", GrobValue.FromString("a\\.\\b\\..\\c")));

        Assert.Equal(GrobValue.FromString("a\\c"), vm.Stack.Peek());
    }
}
