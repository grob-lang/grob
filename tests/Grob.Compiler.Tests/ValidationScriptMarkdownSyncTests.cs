using System.Text.RegularExpressions;

using Xunit;

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
/// is reliable on its own.
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

    private static string ExtractMarkdownBlock(int scriptNumber) {
        string text = File.ReadAllText(_markdownPath);
        List<Match> headings = [.. _sectionHeading.Matches(text).Cast<Match>()];

        Match? heading = headings.FirstOrDefault(m => int.Parse(m.Groups[1].Value) == scriptNumber);
        Assert.True(heading is not null, $"No '## Script {scriptNumber}' heading found in {_markdownPath}.");

        int start = heading!.Index + heading.Length;
        Match? next = headings.FirstOrDefault(m => m.Index > heading.Index);
        int end = next?.Index ?? text.Length;
        string section = text[start..end];

        Match fence = _grobFence.Match(section);
        Assert.True(fence.Success, $"Script {scriptNumber}: no ```grob fence found in its section.");
        return fence.Groups[1].Value;
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
