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
/// <b>Gap B closed (D-417).</b> Named-struct construction and anonymous-struct
/// literal fields require comma separation even across lines — the
/// <c>type</c>-declaration-body convention of bare-newline-separated fields
/// (D-406/§10) does not extend to these two literal forms, per the formatter
/// specification's own §3.5 Category A/B rule, which the parser already
/// implemented correctly for struct/anon-struct/array/map literals. The
/// corpus scripts were the defect, not the parser, for those constructs:
/// scripts 03, 07 and 11 were written in Category A style for Category B
/// literal constructs. D-417 corrects eighteen of the nineteen missing/wrong
/// separators the investigation gate found across the six affected scripts
/// (03, 06, 07, 08, 09, 11) — more sites than the five diagnostics the parser
/// had previously surfaced, since the parser stops at the first failure per
/// construct and a missing *trailing* comma on a literal alone (grammatically
/// optional there) never surfaces a diagnostic at all.
/// </para>
/// <para>
/// <b>The nineteenth site is now fixed (D-421).</b> §3.5 lists call-argument
/// lists as Category B, requiring a trailing comma in multi-line form, and
/// script 07's <c>http.get(...)</c> call (lines 23–26) was missing one on its
/// last argument. Before D-421, adding it did not parse — call-argument
/// lists and function parameter lists rejected a trailing comma
/// unconditionally, unlike struct/array/map/anon-struct literals, which
/// accepted one — a genuine parser/spec mismatch against
/// <c>grob-language-fundamentals.md</c>'s own worked example
/// (<c>fn foo(a: int, b: int,): int { }</c> and <c>foo(1, 2,)</c>), not a
/// corpus defect. D-421 closes it: every comma-separated list now accepts an
/// optional trailing comma (six parser sites, D-421's own table), and script
/// 07's <c>http.get</c> call now carries the trailing comma its own category
/// requires. All eleven scripts still parse with zero diagnostics.
/// </para>
/// <para>
/// The assertion below is strictly stronger than what it replaces: the three
/// previously Gap-B-affected scripts (03, 07, 11) moved from an exact-pinned
/// diagnostic list to a zero-diagnostic assertion, folded into the same clean
/// list the other eight scripts already used. No coverage was lost — a
/// zero-diagnostic assertion catches every diagnostic the pinned list caught,
/// plus any diagnostic the pinned list did not anticipate.
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

    public static IEnumerable<object[]> CleanScriptCases =>
        _cleanScripts.Select(name => new object[] { name });

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
        // Every script is accounted for — now that Gap B is closed, all eleven
        // are clean scripts, so the two lists are the same set.
        Assert.Equal(
            ExpectedScripts.OrderBy(n => n, StringComparer.Ordinal),
            _cleanScripts.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CleanScriptCases))]
    public void CleanScript_ParsesToCompletionWithZeroDiagnostics(string fileName) {
        DiagnosticBag bag = ParseCorpusFile(fileName, out CompilationUnit unit);
        Assert.True(bag.Count == 0,
            $"{fileName}: parser diagnostics:\n{string.Join('\n', bag.Diagnostics)}");
        Assert.NotEmpty(unit.TopLevel);
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
