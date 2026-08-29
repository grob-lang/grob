using System.Text.RegularExpressions;

using Xunit;
using Xunit.Sdk;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-417's drift guard. <c>docs/design/grob-sample-scripts.md</c> publishes the
/// same eleven validation scripts <see cref="ValidationScriptCorpusTests"/>
/// reads from <c>tests/fixtures/validation-scripts/</c> — the <c>.grob</c> files
/// on disk are the authority (confirmed at D-417's investigation gate: nothing
/// in the build or test pipeline reads the markdown as input), and the
/// markdown is a publication of them. Without this guard a fix applied to one
/// side and not the other — the exact failure mode this increment's own
/// investigation gate found no prior guard against — goes unnoticed until
/// someone reads the two side by side.
/// <para>
/// <b>Identification mechanism</b> (settled at the investigation gate, item 3):
/// each script sits under a top-level <c>## Script N — Title</c> heading, and
/// every one of the eleven sections contains exactly one <c>```grob</c> fence —
/// confirmed for all eleven before this guard was written. No document
/// restructuring or marker comment was needed; the existing heading structure
/// is reliable on its own. That uniqueness is <em>enforced, not assumed</em>:
/// extraction fails loudly on a duplicate <c>## Script N</c> heading or on a
/// second fence in the section, rather than silently taking whichever came
/// first, so a later edit cannot quietly turn this guard into a decoy check.
/// </para>
/// <para>
/// <b>Comparison policy: exact byte-for-byte match, no whitespace
/// normalisation.</b> The investigation gate found the two sides already
/// identical, character for character, with no historical divergence to
/// accommodate — so exact match is both the strongest guard available and
/// costs nothing today. Comparison-based (this test reads both and diffs)
/// rather than generation-based, per the same gate: no build step, works when
/// only the docs change, and <c>Assert.Equal</c> on two strings gives a
/// readable diff on failure.
/// </para>
/// </summary>
public sealed class ValidationScriptMarkdownSyncTests {
    private static readonly string _corpusDir = LocateDirectory("tests", "fixtures", "validation-scripts");
    private static readonly string _markdownPath = LocateFile("grob-sample-scripts.md");

    private static readonly Regex _sectionHeading = new(@"^## Script (\d+)", RegexOptions.Multiline);
    private static readonly Regex _grobFence = new(@"```grob\r?\n(.*?)```", RegexOptions.Singleline);

    public static IEnumerable<object[]> ScriptCases =>
        ValidationScriptCorpusTests.ExpectedScripts.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(ScriptCases))]
    public void MarkdownBlock_MatchesCorpusFileExactly(string fileName) {
        int scriptNumber = int.Parse(fileName[..2]);
        string markdownBlock = ExtractMarkdownBlock(scriptNumber);
        string corpusContent = File.ReadAllText(Path.Join(_corpusDir, fileName));

        Assert.Equal(corpusContent, markdownBlock);
    }

    /// <summary>
    /// The guard is only as good as its identification step: if it silently
    /// takes the <em>first</em> matching heading or the <em>first</em> fence, a
    /// later edit that adds a duplicate heading or a second Grob fence makes it
    /// compare a decoy and pass while the published sample drifts from its
    /// corpus file — the exact failure this class exists to catch. These five
    /// cases — duplicate heading, duplicate fence, missing heading, missing
    /// fence and successful extraction — pin the identification as
    /// unambiguous-or-fail. Raised by CodeRabbit on PR #205.
    /// </summary>
    [Fact]
    public void ExtractMarkdownBlock_RejectsDuplicateHeadingForTheSameScript() {
        string markdown = """
            ## Script 3 — The real one

            ```grob
            real
            ```

            ## Script 3 — A decoy added later

            ```grob
            decoy
            ```
            """;

        XunitException failure = Assert.ThrowsAny<XunitException>(
            () => ExtractMarkdownBlock(markdown, 3));
        Assert.Contains("exactly one", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMarkdownBlock_RejectsSecondGrobFenceInTheSameSection() {
        string markdown = """
            ## Script 3 — The real one

            ```grob
            real
            ```

            Prose, then a second Grob example that is not the sample.

            ```grob
            decoy
            ```

            ## Script 4 — Next
            """;

        XunitException failure = Assert.ThrowsAny<XunitException>(
            () => ExtractMarkdownBlock(markdown, 3));
        Assert.Contains("exactly one", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMarkdownBlock_ReportsMissingHeading() {
        string markdown = """
            ## Script 4 — Not the one asked for

            ```grob
            other
            ```
            """;

        XunitException failure = Assert.ThrowsAny<XunitException>(
            () => ExtractMarkdownBlock(markdown, 3));
        Assert.Contains("## Script 3", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMarkdownBlock_ReportsMissingFence() {
        string markdown = """
            ## Script 3 — Heading with no Grob fence

            ```powershell
            Get-ChildItem
            ```
            """;

        XunitException failure = Assert.ThrowsAny<XunitException>(
            () => ExtractMarkdownBlock(markdown, 3));
        Assert.Contains("grob fence", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMarkdownBlock_ReturnsTheSoleFenceOfTheNamedSection() {
        string markdown = """
            ## Script 3 — The real one

            ```grob
            real
            ```

            ## Script 4 — Next

            ```grob
            other
            ```
            """;

        Assert.Equal("real", ExtractMarkdownBlock(markdown, 3).Trim());
    }

    private static string ExtractMarkdownBlock(int scriptNumber) =>
        ExtractMarkdownBlock(File.ReadAllText(_markdownPath), scriptNumber);

    private static string ExtractMarkdownBlock(string text, int scriptNumber) {
        List<Match> headings = [.. _sectionHeading.Matches(text).Cast<Match>()];

        List<Match> matching = [.. headings.Where(m => int.Parse(m.Groups[1].Value) == scriptNumber)];
        Assert.True(
            matching.Count == 1,
            $"Expected exactly one '## Script {scriptNumber}' heading in {_markdownPath}, found {matching.Count}.");

        Match heading = matching[0];
        int start = heading.Index + heading.Length;
        Match? next = headings.FirstOrDefault(m => m.Index > heading.Index);
        int end = next?.Index ?? text.Length;
        string section = text[start..end];

        List<Match> fences = [.. _grobFence.Matches(section).Cast<Match>()];
        Assert.True(
            fences.Count == 1,
            $"Script {scriptNumber}: expected exactly one ```grob fence in its section, found {fences.Count}.");

        return fences[0].Groups[1].Value;
    }

    private static string LocateDirectory(params string[] relativeSegments) {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            string candidate = Path.Join([dir.FullName, .. relativeSegments]);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate {Path.Join(relativeSegments)} by walking up from {AppContext.BaseDirectory}.");
    }

    private static string LocateFile(string fileName) {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            string candidate = Path.Join(dir.FullName, "docs", "design", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate docs/design/{fileName} by walking up from {AppContext.BaseDirectory}.");
    }
}
