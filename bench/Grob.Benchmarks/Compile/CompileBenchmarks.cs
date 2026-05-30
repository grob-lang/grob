using BenchmarkDotNet.Attributes;
using Grob.Compiler;
using Grob.Core;

namespace Grob.Benchmarks.Compile;

/// <summary>
/// Compile-category benchmarks (Sprint 2 baseline, D-307 benchmarking strategy §3).
/// Measures the time from raw source string to a populated <see cref="Chunk"/>
/// for representative programs. These numbers seed the <c>baseline/compile.json</c>
/// reference file; future sprints must not regress beyond +5% without an
/// explicit decision record.
/// </summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser]
public class CompileBenchmarks {
    private string _twoExpressions = null!;
    private string _tenPrints = null!;

    /// <summary>Reads benchmark fixture files from disk once before any benchmark run.</summary>
    [GlobalSetup]
    public void Setup() {
        // Path.Join is used here (not Path.Combine) — Path.Join never resets
        // the path on a rooted later argument, which avoids the CodeQL
        // cs/path-injection concern that Path.Combine carries.
        string fixturesDir = Path.Join(AppContext.BaseDirectory, "Fixtures", "Compile");
        _twoExpressions = File.ReadAllText(Path.Join(fixturesDir, "two-expressions.grob"));
        _tenPrints = File.ReadAllText(Path.Join(fixturesDir, "ten-prints.grob"));
    }

    /// <summary>Compile a two-statement source file (warm path, minimal).</summary>
    [Benchmark(Baseline = true)]
    public Chunk Compile_TwoExpressions() => CompileSource(_twoExpressions);

    /// <summary>Compile a ten-print source file.</summary>
    [Benchmark]
    public Chunk Compile_TenPrints() => CompileSource(_tenPrints);

    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return Grob.Compiler.Compiler.Compile(unit, bag);
    }
}
