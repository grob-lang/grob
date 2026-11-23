namespace Grob.Torture;

/// <summary>Where a seed came from (gate report §1.3), in the order the corpus loads them.</summary>
internal enum SeedKind {
    /// <summary>A <c>.grob</c> file under <c>tests/fixtures/</c>: native probes, validation scripts and sprint fixtures.</summary>
    Fixture,

    /// <summary>An error example's source under <c>docs/errors/examples/</c>.</summary>
    ErrorExample,

    /// <summary>A benchmark fixture under <c>bench/Grob.Benchmarks/Fixtures/</c>.</summary>
    Benchmark,

    /// <summary>A <c>grob</c> block in <c>README.md</c> or a wiki page, as the public samples gate extracts it.</summary>
    PublicSample,

    /// <summary>A multi-line C# raw string literal in a test project.</summary>
    RawLiteral,
}
