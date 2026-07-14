using System.Globalization;
using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 8 Increment E integration tests: <c>formatAs</c> end to end through the full
/// pipeline (lex -> parse -> type-check -> compile -> VM, stdlib plugins auto-registered
/// at startup) — the function form and the chained-form rewrite producing identical
/// output, <c>list</c>/<c>csv</c>, and the <c>de-DE</c>-culture float-pinning stability
/// D-336 guarantees end to end.
/// </summary>
public sealed class Sprint8IncrementETests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        // Path.Join, not Path.Combine (CodeQL cs/path-combine-with-later-rooted-arg):
        // the second segment is a GUID-hex string, never rooted, so behaviour is
        // identical here, but Join has no rooted-segment pitfall to reason about at all.
        string path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static T UnderCulture<T>(string culture, Func<T> run) {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            return run();
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private const string ItemTypeAndData = """
        type Item {
            name: string
            price: float
        }
        items := [
            Item { name: "Widget", price: 9.5 },
            Item { name: "Gadget", price: 12.0 }
        ]
        """;

    [Fact]
    public void FunctionForm_Table_PrintsAlignedColumns() {
        (string stdout, string stderr, int exitCode) = RunSource($$"""
            {{ItemTypeAndData}}
            print(formatAs.table(items))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        // formatAs.table joins its own rows with a plain '\n' (portable, deterministic
        // for gold masters); only print()'s own trailing newline is platform-native.
        Assert.Equal($"name    price\nWidget    9.5\nGadget   12.0{Environment.NewLine}", stdout);
    }

    [Fact]
    public void ChainedForm_Table_ProducesIdenticalOutputToFunctionForm() {
        (string functionFormStdout, _, _) = RunSource($$"""
            {{ItemTypeAndData}}
            print(formatAs.table(items))
            """);
        (string chainedFormStdout, string stderr, int exitCode) = RunSource($$"""
            {{ItemTypeAndData}}
            print(items.formatAs.table())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(functionFormStdout, chainedFormStdout);
    }

    [Fact]
    public void ChainedForm_List_PrintsOneFieldPerLine() {
        // A directly-held struct value, not an indexed array element — array indexing
        // (arr[i]) has no compiler emission at all yet (a pre-existing gap unrelated to
        // formatAs; confirmed by a bare 'arr[0]' script crashing the VM identically with
        // or without this increment's changes) and is out of this increment's scope.
        (string stdout, string stderr, int exitCode) = RunSource("""
            type Item {
                name: string
                price: float
            }
            widget := Item { name: "Widget", price: 9.5 }
            print(widget.formatAs.list())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"name: Widget\nprice: 9.5{Environment.NewLine}", stdout);
    }

    [Fact]
    public void ChainedForm_Csv_PrintsHeaderAndCommaDelimitedRows() {
        (string stdout, string stderr, int exitCode) = RunSource($$"""
            {{ItemTypeAndData}}
            print(items.formatAs.csv())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"name,price\nWidget,9.5\nGadget,12.0{Environment.NewLine}", stdout);
    }

    [Fact]
    public void BareFormatAsAccess_CompileError_ExitsNonZeroWithE1004() {
        (string stdout, string stderr, int exitCode) = RunSource($$"""
            {{ItemTypeAndData}}
            print(items.formatAs)
            """);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E1004", stderr);
    }

    // -----------------------------------------------------------------------
    // Culture stability (D-336) — formatAs.csv's float cells stay pinned to
    // invariant-culture rendering ("9.5", never "9,5") regardless of the ambient
    // host culture.
    // -----------------------------------------------------------------------

    [Fact]
    public void Csv_UnderDeDECulture_StillRendersInvariantDecimalPoint() {
        (string stdout, string stderr, int exitCode) = UnderCulture("de-DE", () => RunSource($$"""
            {{ItemTypeAndData}}
            print(items.formatAs.csv())
            """));

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        // If float rendering leaked the ambient de-DE culture, "9.5"/"12.0" would render
        // "9,5"/"12,0" — an extra comma per row that would corrupt the CSV column count.
        // The exact match below is what actually proves the pin held.
        Assert.Equal($"name,price\nWidget,9.5\nGadget,12.0{Environment.NewLine}", stdout);
    }
}
