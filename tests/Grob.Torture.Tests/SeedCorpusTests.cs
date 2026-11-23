using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The seed corpus is found by scanning, never registered, so a new fixture, example, sample
/// block or test literal is a seed the moment it exists (gate report §1.3). Ids hash the source
/// with FNV-1a, never <c>string.GetHashCode</c>, which is randomised per process (§4.5).
/// </summary>
public sealed class SeedCorpusTests : IDisposable {
    private readonly string _root = Directory.CreateTempSubdirectory("grob-torture-corpus-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string relative, string text) {
        string path = Path.Join(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void Fnv1a64_KnownVectors_Match() {
        Assert.Equal(0xcbf29ce484222325UL, SeedCorpus.Fnv1a64(""));
        Assert.Equal(0xaf63dc4c8601ec8cUL, SeedCorpus.Fnv1a64("a"));
        Assert.Equal(0x85944171f73967e8UL, SeedCorpus.Fnv1a64("foobar"));
    }

    [Fact]
    public void Load_EveryLocation_GivesSeedsOfItsKind() {
        Write("tests/fixtures/a/one.grob", "print(1)\n");
        Write("docs/errors/examples/e/e.grob", "x: int := \"a\"\n");
        Write("bench/Grob.Benchmarks/Fixtures/b.grob", "print(3)\n");
        Write("README.md", "# Title\n\n```grob\nprint(4)\n```\n");
        Write("docs/wiki/Page.md", "```grob\nprint(5)\n```\n");
        Write("tests/Some.Tests/T.cs", "var s = \"\"\"\n    print(6)\n    \"\"\";\n");

        IReadOnlyList<Seed> seeds = SeedCorpus.Load(_root);

        Assert.Equal(
            [
                (SeedKind.Fixture, "tests/fixtures/a/one.grob"),
                (SeedKind.ErrorExample, "docs/errors/examples/e/e.grob"),
                (SeedKind.Benchmark, "bench/Grob.Benchmarks/Fixtures/b.grob"),
                (SeedKind.PublicSample, "README.md"),
                (SeedKind.PublicSample, "docs/wiki/Page.md"),
                (SeedKind.RawLiteral, "tests/Some.Tests/T.cs"),
            ],
            seeds.Select(s => (s.Kind, s.Origin)));
        Assert.Equal("print(4)\n", seeds[3].Source);
    }

    [Fact]
    public void Load_BuildOutputAndAdrPages_AreExcluded() {
        Write("bench/Grob.Benchmarks/Fixtures/bin/Release/b.grob", "print(1)\n");
        Write("tests/fixtures/obj/x.grob", "print(2)\n");
        Write("tests/Some.Tests/bin/Debug/T.cs", "var s = \"\"\"\n    print(3)\n    \"\"\";\n");
        Write("docs/wiki/ADR/0001.md", "```grob\nprint(4)\n```\n");

        Assert.Empty(SeedCorpus.Load(_root));
    }

    [Fact]
    public void Load_SameSourceTwice_KeepsTheFirstInOrder() {
        Write("tests/fixtures/b.grob", "print(1)\n");
        Write("tests/fixtures/a.grob", "print(1)\r\n");

        Seed seed = Assert.Single(SeedCorpus.Load(_root));

        Assert.Equal("tests/fixtures/a.grob", seed.Origin);
        Assert.Equal("print(1)\n", seed.Source);
    }

    [Fact]
    public void Load_Id_IsOriginAndContentHash() {
        Write("tests/fixtures/a.grob", "print(1)\n");

        Seed seed = Assert.Single(SeedCorpus.Load(_root));

        Assert.Equal($"tests/fixtures/a.grob@{SeedCorpus.Fnv1a64("print(1)\n"):x16}", seed.Id);
    }

    [Fact]
    public void LocateRepositoryRoot_FromASubdirectory_FindsTheSolution() {
        Write("Grob.slnx", "<Solution />");
        Write("deep/er/file.txt", "x");

        Assert.Equal(_root, SeedCorpus.LocateRepositoryRoot(Path.Join(_root, "deep", "er")));
    }

    [Fact]
    public void LocateRepositoryRoot_NoSolutionAbove_Throws() {
        string isolated = Path.Join(Path.GetPathRoot(_root), "no-grob-solution-here-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => SeedCorpus.LocateRepositoryRoot(isolated));
    }
}
