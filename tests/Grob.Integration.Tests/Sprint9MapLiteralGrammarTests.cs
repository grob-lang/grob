using System.Text;

using Grob.Cli;
using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Map-literal construction (D-376) integration tests: <c>map&lt;K, V&gt;{ "key": value, … }</c>
/// end to end through the full pipeline (lex -&gt; parse -&gt; type-check -&gt; compile -&gt; VM).
/// Includes the release-gate assertion — Script 11's <c>tags</c> literal shape (four entries,
/// mixed literal/expression values) compiling and running end to end, producing the expected
/// map (<c>grob-sample-scripts.md</c>).
/// </summary>
public sealed class Sprint9MapLiteralGrammarTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
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

    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // 'map' remains an ordinary bindable identifier — the comparison still evaluates.
    // -----------------------------------------------------------------------

    [Fact]
    public void MapAsIdentifier_ComparisonEvaluatesCorrectly() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            map := 5
            result := map < 10
            print(result)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("true" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Release-gate assertion — Script 11's four-entry 'tags' literal shape.
    // -----------------------------------------------------------------------

    [Fact]
    public void ScriptElevenTagsShape_FourEntriesMixedLiteralAndExpression_ProducesExpectedMap() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            environment := "prod"
            count := 42
            tags := map<string, string>{
                "environment": environment,
                "deployedBy": "grob",
                "count": count.toString(),
                "region": "eastus",
            }
            for k, v in tags {
                print("${k}: ${v}")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal(
            "environment: prod" + NL +
            "deployedBy: grob" + NL +
            "count: 42" + NL +
            "region: eastus" + NL,
            stdout);
    }

    // -----------------------------------------------------------------------
    // Empty and single-line literal forms, end to end.
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyMapLiteral_RunsAndReportsZeroLength_ViaIndexerMiss() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{}
            v := m["missing"]
            print(v)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("nil" + NL, stdout);
    }

    [Fact]
    public void SingleLineMapLiteral_IndexerReadsInsertedValue() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            flags := map<string, bool>{ "verbose": true, "dryRun": false }
            print(flags["verbose"])
            print(flags["dryRun"])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("true" + NL + "false" + NL, stdout);
    }
}
