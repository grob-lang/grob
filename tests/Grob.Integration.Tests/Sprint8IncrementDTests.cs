using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 8 Increment D integration tests: the <c>guid</c> primitive type and module end
/// to end through the full pipeline (lex -> parse -> type-check -> compile -> VM, stdlib
/// plugins auto-registered at startup) — generation, parsing, instance members, value
/// equality, <c>ValueDisplay</c> rendering, and the two new/exercised diagnostics
/// (E0601 compile-time literal validation, E5701 runtime <c>ParseError</c>).
/// </summary>
public sealed class Sprint8IncrementDTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        // Path.Combine(Path.GetTempPath(), ...) rather than
        // Path.ChangeExtension(Path.GetTempFileName(), ".grob") — GetTempFileName()
        // creates (and leaves) a real 0-byte file at the original path; ChangeExtension
        // only rewrites the string, so that file leaks on every run (CodeRabbit review,
        // PR #133).
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

    // -----------------------------------------------------------------------
    // Generation and instance members.
    // -----------------------------------------------------------------------

    [Fact]
    public void NewV4_VersionIsFour() {
        (string stdout, string stderr, int exitCode) = RunSource("print(guid.newV4().version)\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("4" + Environment.NewLine, stdout);
    }

    [Fact]
    public void NewV7_VersionIsSeven() {
        (string stdout, string stderr, int exitCode) = RunSource("print(guid.newV7().version)\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("7" + Environment.NewLine, stdout);
    }

    [Fact]
    public void NewV5_DeterministicForSameInputs() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := guid.newV5(guid.namespaces.url, "example.com")
            b := guid.newV5(guid.namespaces.url, "example.com")
            print(a == b)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("true" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Empty_IsEmptyIsTrue_NewV4IsEmptyIsFalse() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            print(guid.empty.isEmpty)
            print(guid.newV4().isEmpty)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"true{Environment.NewLine}false{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Display — print() and string interpolation render the canonical string.
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_ParsedGuid_RendersCanonicalString() {
        (string stdout, string stderr, int exitCode) = RunSource(
            """print(guid.parse("550e8400-e29b-41d4-a716-446655440000"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("550e8400-e29b-41d4-a716-446655440000" + Environment.NewLine, stdout);
    }

    [Fact]
    public void StringInterpolation_Guid_CallsToStringImplicitly() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            id := guid.parse("550e8400-e29b-41d4-a716-446655440000")
            print("Resource ID: ${id}")
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("Resource ID: 550e8400-e29b-41d4-a716-446655440000" + Environment.NewLine, stdout);
    }

    [Fact]
    public void ToUpperString_And_ToCompactString_RenderExpectedForms() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            id := guid.parse("550e8400-e29b-41d4-a716-446655440000")
            print(id.toUpperString())
            print(id.toCompactString())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            $"550E8400-E29B-41D4-A716-446655440000{Environment.NewLine}" +
            $"550e8400e29b41d4a716446655440000{Environment.NewLine}",
            stdout);
    }

    // -----------------------------------------------------------------------
    // Namespaces.
    // -----------------------------------------------------------------------

    [Fact]
    public void Namespaces_Dns_IsRfc4122Value() {
        (string stdout, string stderr, int exitCode) = RunSource("print(guid.namespaces.dns)\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("6ba7b810-9dad-11d1-80b4-00c04fd430c8" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Compile-time distinctness — guid == string is a type mismatch, not a runtime fault.
    // -----------------------------------------------------------------------

    [Fact]
    public void GuidComparedToString_IsCompileErrorE0002() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            id := guid.newV4()
            print(id == "not-a-guid")
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0002", stderr);
    }

    // -----------------------------------------------------------------------
    // Compile-time literal validation (D-149) — E0601.
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseMalformedLiteral_IsCompileErrorE0601() {
        (string stdout, string stderr, int exitCode) = RunSource(
            """readonly id := guid.parse("not-a-guid")""" + "\n");

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0601", stderr);
    }

    // -----------------------------------------------------------------------
    // Runtime parse failure — E5701 (ParseError), catchable.
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseMalformedVariable_Unhandled_ExitsOneWithTopLevelDiagnostic() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            s := "not-a-guid"
            guid.parse(s)
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5701", stderr);
    }

    [Fact]
    public void ParseMalformedVariable_CaughtAsParseError() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            s := "not-a-guid"
            try {
                guid.parse(s)
            } catch (e: ParseError) {
                print("caught: ${e.message}")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.StartsWith("caught:", stdout);
    }

    [Fact]
    public void TryParse_MalformedVariable_ReturnsNil() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            s := "not-a-guid"
            id := guid.tryParse(s)
            print(id == nil)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("true" + Environment.NewLine, stdout);
    }
}
