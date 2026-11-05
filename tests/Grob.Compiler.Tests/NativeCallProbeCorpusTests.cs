using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// The native-call probe corpus: D-426 Decision 5's second no-behaviour-change
/// condition for the <c>FunctionSignature</c> increment. Every in-scope native and
/// instance member (namespace natives and constants, <c>string</c>/<c>int</c>/
/// <c>float</c>/<c>bool</c> instance members, <c>guid</c>/<c>date</c> instance
/// members) is called correctly, with the wrong arity, with a wrong argument type and,
/// where a parameter is non-nullable, with a nullable value. The diagnostics were
/// captured against the untouched tree at <c>b202509</c> through the CLI's own
/// formatter, before any signature work began, and the <c>*.expected.txt</c> files are
/// the gold master: a difference after the migration is a defect in the migration, not
/// in the capture, so the expected files are never regenerated to match new output
/// (D-313's no-ratchet rule, applied by D-426).
/// <para>
/// Fixtures live in <c>tests/fixtures/native-probes/</c>, one pair per receiver. A
/// <c>*.clean.grob</c> file holds the calls that must type-check and compile with zero
/// diagnostics; its sibling <c>*.errors.grob</c> holds the arity, type, nullable and
/// return-type probes whose diagnostics are pinned by <c>*.errors.expected.txt</c>.
/// <c>named-arguments.grob</c> pins D-426 Decision 4: named-argument binding to natives
/// stays unsupported, so every named call keeps exactly the diagnostic it produces
/// today, including the named-type method calls that produce none. The corpus is read
/// from the live tree by walking up from <c>AppContext.BaseDirectory</c>, mirroring
/// <see cref="ValidationScriptCorpusTests"/>.
/// </para>
/// <para>
/// Diagnostics are rendered with <see cref="Diagnostic.ToString"/>, which is the same
/// <c>error[Exxxx]: message</c> / <c>  --> file:line:col</c> shape the CLI writes, and
/// the source is scanned under its bare file name so the ranges match the capture.
/// Line endings are normalised to <c>\n</c> on both sides before comparison, since the
/// repository stores text files as LF and a Windows checkout may not.
/// </para>
/// </summary>
public sealed class NativeCallProbeCorpusTests {
    private static readonly string _corpusDir = LocateCorpusDirectory();

    /// <summary>Every receiver with a clean/errors pair, by fixture stem.</summary>
    public static readonly string[] Receivers = [
        "math",
        "path",
        "strings",
        "env",
        "log",
        "guid-namespace",
        "date-namespace",
        "int-statics",
        "float-statics",
        "string-members",
        "int-members",
        "float-members",
        "bool-members",
        "guid-members",
        "date-members",
    ];

    /// <summary>The probe files whose diagnostics are pinned by an expected file.</summary>
    public static readonly string[] PinnedProbeFiles = [
        .. Receivers.Select(r => $"{r}.errors.grob"),
        "named-arguments.grob",
    ];

    public static IEnumerable<object[]> CleanCases =>
        Receivers.Select(r => new object[] { $"{r}.clean.grob" });

    public static IEnumerable<object[]> PinnedCases =>
        PinnedProbeFiles.Select(f => new object[] { f });

    /// <summary>
    /// The corpus is exactly the files this class names, each pinned probe with its
    /// expected file alongside: nothing silently added, dropped or renamed.
    /// </summary>
    [Fact]
    public void Corpus_ContainsExactlyTheNamedProbeFiles() {
        string[] onDisk = Directory.GetFiles(_corpusDir, "*.grob")
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray()!;
        string[] expected = [
            .. Receivers.Select(r => $"{r}.clean.grob"),
            .. PinnedProbeFiles,
        ];
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), onDisk);

        foreach (string probe in PinnedProbeFiles) {
            Assert.True(File.Exists(Path.Join(_corpusDir, ExpectedFileFor(probe))),
                $"{probe} has no expected file alongside it.");
        }
    }

    [Theory]
    [MemberData(nameof(CleanCases))]
    public void CleanProbe_TypeChecksAndCompilesWithZeroDiagnostics(string fileName) {
        DiagnosticBag bag = Check(fileName, out CompilationUnit unit);
        Assert.True(bag.Count == 0,
            $"{fileName}: expected no diagnostics, got:\n{Render(bag)}");

        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.NotNull(chunk);
        Assert.True(bag.Count == 0,
            $"{fileName}: compile diagnostics:\n{Render(bag)}");
    }

    [Theory]
    [MemberData(nameof(PinnedCases))]
    public void PinnedProbe_ProducesTheCapturedDiagnostics(string fileName) {
        DiagnosticBag bag = Check(fileName, out _);
        string expected = Normalise(File.ReadAllText(Path.Join(_corpusDir, ExpectedFileFor(fileName))));
        Assert.Equal(expected, Normalise(Render(bag)));
    }

    private static DiagnosticBag Check(string fileName, out CompilationUnit unit) {
        string source = File.ReadAllText(Path.Join(_corpusDir, fileName));
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag, fileName);
        unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return bag;
    }

    private static string ExpectedFileFor(string probeFile) =>
        probeFile[..^".grob".Length] + ".expected.txt";

    private static string Render(DiagnosticBag bag) =>
        string.Concat(bag.Diagnostics.Select(d => d.ToString() + "\n"));

    private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string LocateCorpusDirectory() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            string candidate = Path.Join(dir.FullName, "tests", "fixtures", "native-probes");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate tests/fixtures/native-probes by walking up from " + AppContext.BaseDirectory);
    }
}
