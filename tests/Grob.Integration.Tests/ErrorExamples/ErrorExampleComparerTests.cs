using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The behavioural layer's comparison (D-428 Decision 2): a master's full ordered list of
/// <c>(severity, code, file, line, column)</c> tuples against the list the CLI writes. Any extra,
/// missing, reordered or differing diagnostic fails.
/// </summary>
public sealed class ErrorExampleComparerTests {
    private const string File = "p.grob";

    private static ExampleDiagnostic Error(string code, int line, int? column) => new("error", code, File, line, column);

    private static readonly ExampleDiagnostic First = Error("E0001", 2, 6);
    private static readonly ExampleDiagnostic Second = Error("E1001", 4, 3);

    private static string? Compare(IReadOnlyList<ExampleDiagnostic> expected, IReadOnlyList<ExampleDiagnostic> actual) =>
        ExampleDiagnostic.Compare(expected, actual, File);

    [Fact]
    public void Compare_IdenticalLists_Passes() {
        Assert.Null(Compare([First, Second], [First, Second]));
    }

    [Fact]
    public void Compare_BothEmpty_Passes() {
        Assert.Null(Compare([], []));
    }

    [Fact]
    public void Compare_ExtraActualDiagnostic_Fails() {
        Assert.Equal(
            "expected: error[E0001] 2:6\nactual:   error[E0001] 2:6; error[E1001] 4:3",
            Compare([First], [First, Second]));
    }

    [Fact]
    public void Compare_MissingActualDiagnostic_Fails() {
        Assert.Equal(
            "expected: error[E0001] 2:6; error[E1001] 4:3\nactual:   error[E0001] 2:6",
            Compare([First, Second], [First]));
    }

    [Fact]
    public void Compare_NoActualDiagnostics_Fails() {
        Assert.Equal("expected: error[E0001] 2:6\nactual:   none", Compare([First], []));
    }

    [Fact]
    public void Compare_ReorderedPair_Fails() {
        Assert.NotNull(Compare([First, Second], [Second, First]));
    }

    [Fact]
    public void Compare_DifferingCode_Fails() {
        Assert.NotNull(Compare([First], [Error("E0002", 2, 6)]));
    }

    [Fact]
    public void Compare_DifferingLine_Fails() {
        Assert.NotNull(Compare([First], [Error("E0001", 3, 6)]));
    }

    [Fact]
    public void Compare_DifferingColumn_Fails() {
        Assert.NotNull(Compare([First], [Error("E0001", 2, 7)]));
    }

    [Fact]
    public void Compare_DifferingSeverity_Fails() {
        Assert.NotNull(Compare([First], [First with { Severity = "warning" }]));
    }

    [Fact]
    public void Compare_DifferingFile_Fails() {
        Assert.Equal(
            "expected: error[E0001] 2:6\nactual:   error[E0001] q.grob:2:6",
            Compare([First], [First with { File = "q.grob" }]));
    }

    [Fact]
    public void Compare_ActualWithNoColumnAgainstMasterWithOne_Fails() {
        Assert.Equal(
            "expected: error[E0001] 2:6\nactual:   error[E0001] 2",
            Compare([First], [Error("E0001", 2, null)]));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("error[E0001] 2:6")]
    [InlineData("warning[E1101] 5")]
    [InlineData("error[E3201] grob.json:1:3")]
    [InlineData("error[E0001] 2:6; error[E1001] 4:3")]
    public void Encoding_RoundTrips(string encoded) {
        Assert.True(ExampleDiagnostic.TryDecodeList(encoded, File, out IReadOnlyList<ExampleDiagnostic>? list, out _));
        Assert.Equal(encoded, ExampleDiagnostic.EncodeList(list!, File));
    }

    // A bare line:column must not read as file "line" and line "column".
    [Fact]
    public void Decode_LineAndColumn_IsNotReadAsFileAndLine() {
        Assert.True(ExampleDiagnostic.TryDecodeList("error[E0002] 3:12; error[E3201] grob.json:1:3", File,
            out IReadOnlyList<ExampleDiagnostic>? list, out _));

        Assert.Equal(
            [new ExampleDiagnostic("error", "E0002", File, 3, 12), new ExampleDiagnostic("error", "E3201", "grob.json", 1, 3)],
            list);
    }

    [Theory]
    [InlineData("error[E01] 2:6", "error[E01] 2:6")]
    [InlineData("fatal[E0001] 2:6", "fatal[E0001] 2:6")]
    [InlineData("error[E0001]", "error[E0001]")]
    [InlineData("error[E0001] 2:6;error[E1001] 4:3", "error[E0001] 2:6;error[E1001] 4:3")]
    [InlineData("error[E0001] 2:6; ", "")]
    public void Decode_MalformedEntry_NamesIt(string encoded, string badEntry) {
        Assert.False(ExampleDiagnostic.TryDecodeList(encoded, File, out _, out string? bad));
        Assert.Equal(badEntry, bad);
    }
}

/// <summary>
/// The CLI's stderr read back into tuples, normalised as narrowly as D-428 Decision 2 allows: the
/// rendered path must be the path the harness passed, a range reduces to its start, and a location
/// with no column keeps none. Nothing else is normalised.
/// </summary>
public sealed class CliDiagnosticReaderTests {
    // Host-native, so Path.GetFileName finds 'p.grob' on every runner.
    private static readonly string Passed = Path.GetFullPath(Path.Join("corpus", "p", "p.grob"));

    [Fact]
    public void Read_PathHoldingAColon_ReadsTheLocationAfterIt() {
        // A drive letter on Windows; a colon is legal in a directory name elsewhere. Either way the
        // lazy path must stop at the colon that starts the location, not an earlier one.
        string passed = Path.Join(Path.GetFullPath("corpus"), "a:b", "p.grob");

        CliDiagnostics read = CliDiagnosticReader.Read($"error[E0001]: bad\n  --> {passed}:2:6\n", passed);

        Assert.Empty(read.Problems);
        Assert.Equal([new ExampleDiagnostic("error", "E0001", "p.grob", 2, 6)], read.Diagnostics);
    }

    [Fact]
    public void CheckExitCode_ErrorReportedAndNonZero_Passes() {
        Assert.Null(CliDiagnosticReader.CheckExitCode(1, [new ExampleDiagnostic("error", "E0001", "p.grob", 2, 6)]));
    }

    [Fact]
    public void CheckExitCode_NothingReportedAndZero_Passes() {
        Assert.Null(CliDiagnosticReader.CheckExitCode(0, []));
    }

    [Fact]
    public void CheckExitCode_WarningOnlyAndZero_Passes() {
        Assert.Null(CliDiagnosticReader.CheckExitCode(0, [new ExampleDiagnostic("warning", "E1101", "p.grob", 3, 9)]));
    }

    [Fact]
    public void CheckExitCode_ErrorReportedButZero_Fails() {
        Assert.Equal("exit code 0, but the CLI reported an error",
            CliDiagnosticReader.CheckExitCode(0, [new ExampleDiagnostic("error", "E0001", "p.grob", 2, 6)]));
    }

    [Fact]
    public void CheckExitCode_NoErrorButNonZero_Fails() {
        Assert.Equal("exit code 1, but the CLI reported no error",
            CliDiagnosticReader.CheckExitCode(1, [new ExampleDiagnostic("warning", "E1101", "p.grob", 3, 9)]));
    }

    [Fact]
    public void Read_PointLocation_KeepsLineAndColumn() {
        CliDiagnostics read = CliDiagnosticReader.Read($"error[E0001]: bad\n  --> {Passed}:2:6\n", Passed);

        Assert.Empty(read.Problems);
        Assert.Equal([new ExampleDiagnostic("error", "E0001", "p.grob", 2, 6)], read.Diagnostics);
    }

    [Fact]
    public void Read_Range_ReducesToItsStart() {
        CliDiagnostics read = CliDiagnosticReader.Read($"error[E0001]: bad\n  --> {Passed}:2:6-3:14\n", Passed);

        Assert.Empty(read.Problems);
        Assert.Equal([new ExampleDiagnostic("error", "E0001", "p.grob", 2, 6)], read.Diagnostics);
    }

    [Fact]
    public void Read_NoColumn_AcquiresNone() {
        CliDiagnostics read = CliDiagnosticReader.Read($"error[E5002]: division by zero\n  --> {Passed}:5\n", Passed);

        Assert.Empty(read.Problems);
        Assert.Equal([new ExampleDiagnostic("error", "E5002", "p.grob", 5, null)], read.Diagnostics);
    }

    [Fact]
    public void Read_WarningAndCrLf_ReadsBothInOrder() {
        string stderr = $"warning[E1101]: shadowed\r\n  --> {Passed}:3:9\r\nerror[E0001]: bad\r\n  --> {Passed}:4:1\r\n";

        CliDiagnostics read = CliDiagnosticReader.Read(stderr, Passed);

        Assert.Empty(read.Problems);
        Assert.Equal(
            [new ExampleDiagnostic("warning", "E1101", "p.grob", 3, 9), new ExampleDiagnostic("error", "E0001", "p.grob", 4, 1)],
            read.Diagnostics);
    }

    [Fact]
    public void Read_EmptyOutput_IsNoDiagnostics() {
        CliDiagnostics read = CliDiagnosticReader.Read("", Passed);

        Assert.Empty(read.Problems);
        Assert.Empty(read.Diagnostics);
    }

    [Fact]
    public void Read_RenderedPathIsNotThePathPassed_Fails() {
        CliDiagnostics read = CliDiagnosticReader.Read("error[E0001]: bad\n  --> p.grob:2:6\n", Passed);

        string problem = Assert.Single(read.Problems);
        Assert.Equal($"rendered path 'p.grob' is not the path passed '{Passed}'", problem);
    }

    [Fact]
    public void Read_UnrecognisedLine_Fails() {
        CliDiagnostics read = CliDiagnosticReader.Read("error: internal error: boom\n", Passed);

        string problem = Assert.Single(read.Problems);
        Assert.Equal("unrecognised output line 'error: internal error: boom'", problem);
    }

    [Fact]
    public void Read_HeaderWithNoLocation_Fails() {
        CliDiagnostics read = CliDiagnosticReader.Read("error[E0001]: bad\n", Passed);

        string problem = Assert.Single(read.Problems);
        Assert.Equal("diagnostic error[E0001] has no location line", problem);
    }
}
