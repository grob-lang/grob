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
/// <b>D-416 closes Gap A.</b> D-415 isolated Gap A as "a generic-argument method
/// call reached via member access does not parse", with <c>a.mapAs&lt;Employee&gt;()</c>
/// as its repro, and listed scripts 04, 05, 07, 09 and 11 as affected. D-416's own
/// investigation gate found that framing narrower than the actual defect: the
/// determinant was never receiver shape but argument-list arity — an
/// <i>empty</i>-argument generic call (<c>x.mapAs&lt;T&gt;()</c>) hard-failed to
/// parse, while a <i>non-empty</i>-argument one (<c>x.mapAs&lt;T&gt;(a)</c>)
/// silently misparsed as a comparison feeding a call, with no parse diagnostic at
/// all — and free-function calls of both shapes were affected identically to
/// member-access ones. Confirmed against this corpus directly: scripts 04, 05 and
/// 09 each carried exactly one Gap-A diagnostic (the empty-argument shape) and now
/// parse with <b>zero</b> diagnostics — moved to <c>_cleanScripts</c> below. Script
/// 07 carried one Gap-A diagnostic plus two unrelated Gap-B ones; only the two
/// Gap-B diagnostics remain. <b>Script 11 was never actually Gap-A-affected</b> —
/// it contains no <c>mapAs</c>/generic call at all (its only near-collision use of
/// <c>&lt;</c>/<c>&gt;</c> is the unrelated, already-working <c>map&lt;string,
/// string&gt;{ }</c> literal on line 36, D-376); its single diagnostic, both
/// before and after D-416, is Gap B alone. D-415's "affects... 11" was carried
/// forward uncritically from a claim never re-verified against the actual
/// script — corrected here rather than repeated.
/// </para>
/// <para>
/// <b>Gap B remains open, reported and not fixed here (out of this increment's
/// scope):</b> named-struct construction and anonymous-struct literal fields
/// require comma separation even across lines — the <c>type</c>-declaration-body
/// convention of bare-newline-separated fields (D-406/§10) does not extend to
/// these two literal forms — isolated with a bare <c>Point { a: 1\nb: 2\n}</c>.
/// Affects scripts 03, 07 and 11. Script 08's <c>BranchInfo</c> construction is
/// the control that confirms Gap B precisely: it is multi-line but
/// comma-separated, and parses cleanly. Left for a future increment to scope —
/// most plausibly the corpus sweep D-415's own hand-off already queues.
/// </para>
/// <para>
/// The split below is deliberately not a weaker acceptance test: the eight clean
/// scripts assert zero diagnostics outright, and the three Gap-B-affected scripts
/// pin their <i>exact</i> diagnostic set (code, message, line, column) rather than
/// merely "some error" — a regression that introduced a new, unexpected
/// diagnostic (a param- or type-argument-shaped one, in particular) would still
/// fail this test.
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
        "04-github-repos-backup.grob",
        "05-csv-data-processing.grob",
        "06-azure-cli-wrapper.grob",
        "08-stale-git-branches.grob",
        "09-disk-space-monitor.grob",
        "10-download-and-verify.grob",
    ];

    private sealed record ExpectedDiagnostic(string Code, string Message, int Line, int Column);

    /// <summary>
    /// The scripts still affected by Gap B (see the type doc comment) now that
    /// D-416 has closed Gap A, each with its exact, currently-observed diagnostic
    /// set pinned so a regression — including a spurious param- or
    /// type-argument-related diagnostic — is caught, while the pre-existing
    /// unrelated gap is not silently mischaracterised as this increment's problem.
    /// </summary>
    private static readonly Dictionary<string, ExpectedDiagnostic[]> _knownGapScripts = new() {
        ["03-find-large-files-report.grob"] = [
            new("E2001", "expected '}' to close struct construction", 16, 9),
            new("E2001", "unexpected token '}' — expected expression", 18, 5),
        ],
        ["07-rest-api-data-pull.grob"] = [
            new("E2001", "expected '}' to close anonymous struct literal", 33, 13),
            new("E2001", "unexpected token '}' — expected expression", 36, 9),
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
