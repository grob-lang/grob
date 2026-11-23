using Grob.Torture.Findings;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A failing program is kept as its own record, grouped under its signature, and an existing record
/// is never overwritten (prompt §4): the strategy names a finding by signature alone, which would
/// let one program's record replace another's.
/// </summary>
public sealed class FindingStoreTests : IDisposable {
    private const string Signature = "Run|Exception|GrobInternalException: boom";

    private readonly string _root = Path.Join(Path.GetTempPath(), $"grob-findings-{Guid.NewGuid():N}");

    public void Dispose() {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static FindingRecord Record(string programId, string reproducer = "x := bad()\n", string signature = Signature) =>
        new(programId, signature, "boom", "Grob.Vm.VirtualMachine.Run", "a := 1\nx := bad()\n", reproducer, false);

    [Fact]
    public void Add_TwoProgramsWithOneSignature_LeaveTwoRecords() {
        var store = new FindingStore(_root);

        Assert.True(store.Add(Record("seed-a@1/replace-value/3")));
        Assert.True(store.Add(Record("seed-b@2/swap-operands/9")));

        IReadOnlyList<FindingRecord> records = store.Records(Signature);
        Assert.Equal(2, records.Count);
        Assert.Equal(["seed-a@1/replace-value/3", "seed-b@2/swap-operands/9"], records.Select(r => r.ProgramId).Order().ToArray());
        Assert.Single(Directory.GetDirectories(_root));
    }

    [Fact]
    public void Add_SecondRunOverTheSameProgram_LeavesTheFirstRecordUnchanged() {
        var store = new FindingStore(_root);
        Assert.True(store.Add(Record("seed-a@1/replace-value/3", reproducer: "first\n")));
        string path = store.PathFor(Signature, "seed-a@1/replace-value/3");
        byte[] before = File.ReadAllBytes(path);

        bool written = store.Add(Record("seed-a@1/replace-value/3", reproducer: "second\n"));

        Assert.False(written);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal("first\n", Assert.Single(store.Records(Signature)).Reproducer);
    }

    [Fact]
    public void Add_DifferentSignatures_GoUnderDifferentDirectories() {
        var store = new FindingStore(_root);

        store.Add(Record("p@1/x/0", signature: "Run|Exception|A"));
        store.Add(Record("p@1/x/0", signature: "Run|Exception|B"));

        Assert.Equal(2, Directory.GetDirectories(_root).Length);
        Assert.Single(store.Records("Run|Exception|A"));
        Assert.Single(store.Records("Run|Exception|B"));
    }

    [Fact]
    public void Add_ProgramIdWithPathCharacters_IsAValidFileName() {
        var store = new FindingStore(_root);

        Assert.True(store.Add(Record("tests/a/b.cs@00ff/route-value/12")));
        Assert.True(File.Exists(store.PathFor(Signature, "tests/a/b.cs@00ff/route-value/12")));
    }

    [Fact]
    public void Records_RoundTripEveryField() {
        var store = new FindingStore(_root);
        var written = new FindingRecord("p@1/x/0", Signature, "a message", "Type.Method", "original\n", "repro\n", true);
        store.Add(written);

        Assert.Equal(written, Assert.Single(store.Records(Signature)));
    }

    [Fact]
    public void Records_UnknownSignature_IsEmpty() {
        Assert.Empty(new FindingStore(_root).Records("Run|Exception|none"));
    }

    [Fact]
    public void Add_ARecordThatCannotBeWritten_ThrowsAndIsNotReportedAsExisting() {
        var store = new FindingStore(_root);
        string path = store.PathFor(Signature, "p@1/x/0");
        Directory.CreateDirectory(path);   // a directory where the file should go

        Assert.ThrowsAny<Exception>(() => store.Add(Record("p@1/x/0")));
    }

    [Fact]
    public void Triage_EachFinding_IsMinimisedAndStoredWithItsOriginal() {
        var store = new FindingStore(_root);
        var finding = new Finding(new Signature(PipelinePhase.Run, FindingClass.Exception, "boom"), "boom");
        IReadOnlyList<Finding> Run(string source) => source.Contains("bad()", StringComparison.Ordinal) ? [finding] : [];
        var triager = new Triager(Run, store);

        var outcomes = triager.Triage("p@1/x/0", "a := 1\nx := bad()\nb := 2\n", [finding]);

        (Finding _, Grob.Torture.Minimisation.MinimisationResult minimisation, bool stored) = Assert.Single(outcomes);
        Assert.True(stored);
        Assert.Equal("x := bad()\n", minimisation.Reproducer);
        FindingRecord record = Assert.Single(store.Records(finding.Signature.Key));
        Assert.Equal("a := 1\nx := bad()\nb := 2\n", record.Original);
        Assert.Equal("x := bad()\n", record.Reproducer);
    }

    [Fact]
    public void Triage_TwoFindingsInOneProgram_GiveTwoRecordsUnderTheirOwnSignatures() {
        var store = new FindingStore(_root);
        var first = new Finding(new Signature(PipelinePhase.Run, FindingClass.Exception, "one"), "one");
        var second = new Finding(new Signature(PipelinePhase.Check, FindingClass.Sentinel, "two"), "two");
        IReadOnlyList<Finding> Run(string source) => [.. new[] { source.Contains("A", StringComparison.Ordinal) ? first : null, source.Contains("B", StringComparison.Ordinal) ? second : null }.OfType<Finding>()];

        new Triager(Run, store).Triage("p@1/x/0", "A\nB\n", [first, second]);

        Assert.Equal("A\n", Assert.Single(store.Records(first.Signature.Key)).Reproducer);
        Assert.Equal("B\n", Assert.Single(store.Records(second.Signature.Key)).Reproducer);
    }
}
