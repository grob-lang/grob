using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// The eleven real-world scripts <c>grob-sample-scripts.md</c> uses to validate
/// Grob's API surface against PowerShell/bash equivalents (D-409). Extracted
/// verbatim from the document's fenced <c>```grob</c> blocks into
/// <c>tests/fixtures/validation-scripts/</c> — there is no existing project
/// convention for a cross-sprint corpus directory (the sprint-tagged
/// <c>tests/fixtures/sprint-N/</c> pattern is copy-to-output via a `Content`
/// item per directory in <c>Grob.Integration.Tests.csproj</c>, wired per
/// sprint and unsuited to a corpus that is not sprint-scoped), so this test
/// instead locates the repo root by walking up from <c>AppContext.BaseDirectory</c>
/// and reads the fixtures directly from source, mirroring
/// <c>ErrorCatalogAgreementTests.LocateRegistry</c>'s existing pattern for
/// reading a documentation/corpus file from the live tree rather than a
/// copied build artefact.
/// <para>
/// Before D-410 every one of these scripts failed to parse at its first
/// braceless <c>param</c> line (D-409's release-gate blocker). D-415 clears
/// that blocker: every script now gets past every <c>param</c>/decorator line
/// without incident.
/// </para>
/// <para>
/// <b>Finding, reported and not fixed here (out of this increment's scope,
/// per the standing "report, don't fix" discipline):</b> six of the eleven
/// still fail to parse to completion, for two pre-existing gaps that are
/// entirely unrelated to <c>param</c> — confirmed by isolating each failure
/// to a minimal repro with no <c>param</c> involved at all, and by every
/// pinned diagnostic below being ordinary <c>E2001</c> on an unrelated
/// construct, never <c>E4201</c>/<c>E2202</c>/<c>E4202</c>. <b>Gap A:</b> a
/// generic-argument method call reached via member access (<c>x.mapAs&lt;T&gt;()</c>)
/// does not parse — isolated with <c>a.mapAs&lt;Employee&gt;()</c> alone, no
/// receiver chain and no <c>param</c> in sight. Affects scripts 04, 05, 07, 09
/// and 11. <b>Gap B:</b> named-struct construction and anonymous-struct
/// literal fields require comma separation even across lines — the
/// <c>type</c>-declaration-body convention of bare-newline-separated fields
/// (D-406/§10) does not extend to these two literal forms — isolated with a
/// bare <c>Point { a: 1\nb: 2\n}</c>. Affects scripts 03, 07 and 11 (07 and 11
/// hit both gaps). Script 08's <c>BranchInfo</c> construction is the control
/// that confirms Gap B precisely: it is multi-line but comma-separated, and
/// parses cleanly. This is left for a future increment to scope — most
/// plausibly the corpus sweep the next chat's hand-off already queues, or the
/// D-405/D-406 style local-recovery survey extended to these two call sites.
/// </para>
/// <para>
/// The split below is deliberately not a weaker acceptance test: the five
/// clean scripts assert zero diagnostics outright, and the six gap-affected
/// scripts pin their <i>exact</i> diagnostic set (code, message, line,
/// column) rather than merely "some error" — a regression that introduced a
/// new, unexpected diagnostic (a param one, in particular) would still fail
/// this test.
/// </para>
/// </summary>
public sealed class ValidationScriptCorpusTests {
    private static readonly string _corpusDir = LocateCorpusDirectory();

    public static readonly string[] ExpectedScripts = [
        "01-bulk-file-rename.grob",
        "02-organise-photos-by-date.grob",
        "03-find-large-files-report.grob",
        "04-github-repos-backup.grob",
        "05-csv-data-processing.grob",
        "06-azure-cli-wrapper.grob",
        "07-rest-api-data-pull.grob",
        "08-stale-git-branches.grob",
        "09-disk-space-monitor.grob",
        "10-download-and-verify.grob",
        "11-azure-resource-provisioning.grob",
    ];

    private static readonly string[] _cleanScripts = [
        "01-bulk-file-rename.grob",
        "02-organise-photos-by-date.grob",
        "06-azure-cli-wrapper.grob",
        "08-stale-git-branches.grob",
        "10-download-and-verify.grob",
    ];

    private sealed record ExpectedDiagnostic(string Code, string Message, int Line, int Column);

    /// <summary>
    /// The scripts affected by Gap A and/or Gap B (see the type doc comment),
    /// each with its exact, currently-observed diagnostic set pinned so a
    /// regression — including a spurious param-related diagnostic — is
    /// caught, while the pre-existing unrelated gap is not silently
    /// mischaracterised as this increment's problem.
    /// </summary>
    private static readonly Dictionary<string, ExpectedDiagnostic[]> _knownGapScripts = new() {
        ["03-find-large-files-report.grob"] = [
            new("E2001", "expected '}' to close struct construction", 16, 9),
            new("E2001", "unexpected token '}' — expected expression", 18, 5),
        ],
        ["04-github-repos-backup.grob"] = [
            new("E2001", "unexpected token ')' — expected expression", 18, 66),
        ],
        ["05-csv-data-processing.grob"] = [
            new("E2001", "unexpected token ')' — expected expression", 14, 42),
            new("E2001", "unexpected token '}' — expected expression", 16, 106),
        ],
        ["07-rest-api-data-pull.grob"] = [
            new("E2001", "unexpected token ')' — expected expression", 26, 25),
            new("E2001", "expected '}' to close anonymous struct literal", 33, 13),
            new("E2001", "unexpected token '}' — expected expression", 36, 9),
        ],
        ["09-disk-space-monitor.grob"] = [
            new("E2001", "unexpected token ']' — expected expression", 25, 47),
        ],
        ["11-azure-resource-provisioning.grob"] = [
            new("E2001", "expected '}' to close anonymous struct literal", 55, 9),
        ],
    };

    public static IEnumerable<object[]> CleanScriptCases =>
        _cleanScripts.Select(name => new object[] { name });

    public static IEnumerable<object[]> KnownGapScriptCases =>
        _knownGapScripts.Keys.Select(name => new object[] { name });

    /// <summary>
    /// The release-gate blocker itself: the corpus is exactly eleven scripts,
    /// none silently dropped or renamed. A directory listing rather than a
    /// hardcoded count, so an accidental extra or missing file fails loudly.
    /// </summary>
    [Fact]
    public void Corpus_ContainsExactlyElevenScripts() {
        string[] onDisk = Directory.GetFiles(_corpusDir, "*.grob")
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray()!;
        Assert.Equal(ExpectedScripts.OrderBy(n => n, StringComparer.Ordinal), onDisk);
        // Every script is accounted for by exactly one of the two groups below.
        Assert.Equal(
            ExpectedScripts.OrderBy(n => n, StringComparer.Ordinal),
            _cleanScripts.Concat(_knownGapScripts.Keys).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CleanScriptCases))]
    public void CleanScript_ParsesToCompletionWithZeroDiagnostics(string fileName) {
        DiagnosticBag bag = ParseCorpusFile(fileName, out CompilationUnit unit);
        Assert.True(bag.Count == 0,
            $"{fileName}: parser diagnostics:\n{string.Join('\n', bag.Diagnostics)}");
        Assert.NotEmpty(unit.TopLevel);
    }

    [Theory]
    [MemberData(nameof(KnownGapScriptCases))]
    public void KnownGapScript_FailsOnlyOnThePreExistingUnrelatedGap(string fileName) {
        DiagnosticBag bag = ParseCorpusFile(fileName, out _);
        ExpectedDiagnostic[] expected = _knownGapScripts[fileName];

        Assert.Equal(expected.Length, bag.Diagnostics.Count);
        for (int i = 0; i < expected.Length; i++) {
            Diagnostic actual = bag.Diagnostics[i];
            Assert.Equal(expected[i].Code, actual.Code);
            Assert.Equal(expected[i].Message, actual.Message);
            Assert.Equal(expected[i].Line, actual.Range.Start.Line);
            Assert.Equal(expected[i].Column, actual.Range.Start.Column);
            // None of the pre-existing gap's diagnostics are param-related —
            // the D-409 blocker this increment clears would regress silently
            // if a future change reintroduced a param-shaped failure here.
            Assert.DoesNotContain("param", actual.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DiagnosticBag ParseCorpusFile(string fileName, out CompilationUnit unit) {
        string path = Path.Join(_corpusDir, fileName);
        string source = File.ReadAllText(path);

        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        Assert.True(bag.Count == 0,
            $"{fileName}: lexer diagnostics:\n{string.Join('\n', bag.Diagnostics)}");

        unit = Parser.Parse(tokens, bag);
        return bag;
    }

    private static string LocateCorpusDirectory() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            string candidate = Path.Join(dir.FullName, "tests", "fixtures", "validation-scripts");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate tests/fixtures/validation-scripts by walking up from " +
            $"{AppContext.BaseDirectory}. The corpus test needs the fixtures on disk.");
    }
}
