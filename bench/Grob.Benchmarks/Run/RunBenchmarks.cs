using BenchmarkDotNet.Attributes;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

namespace Grob.Benchmarks.Run;

/// <summary>
/// VM-execution category benchmarks (Sprint 3 baseline, D-309).
/// Measures the full pipeline — lex, parse, type-check, compile, VM execute —
/// for representative programmes.  The baseline JSON for this category is
/// produced via the <c>benchmark.yml</c> GitHub Actions workflow (D-309) on a
/// <c>windows-latest</c> runner; the committed <c>baseline/run.json</c> must
/// not be replaced with a locally-produced file.
/// </summary>
[MemoryDiagnoser]
public class RunBenchmarks {
    private string _declAndArith = null!;
    private string _interpolation = null!;
    private string _controlFlow = null!;

    /// <summary>Reads benchmark fixture files from disk once before any benchmark run.</summary>
    [GlobalSetup]
    public void Setup() {
        // Path.Join is used here (not Path.Combine) — Path.Join never resets
        // the path on a rooted later argument, which avoids the CodeQL
        // cs/path-injection concern that Path.Combine carries.
        string fixturesDir = Path.Join(AppContext.BaseDirectory, "Fixtures", "Run");
        _declAndArith = File.ReadAllText(Path.Join(fixturesDir, "decl-and-arith.grob"));
        _interpolation = File.ReadAllText(Path.Join(fixturesDir, "interpolation.grob"));
        _controlFlow = File.ReadAllText(Path.Join(fixturesDir, "control-flow.grob"));
    }

    /// <summary>Execute a declarations-and-arithmetic script (warm path, minimal).</summary>
    [Benchmark(Baseline = true)]
    public void Run_DeclAndArith() => RunSource(_declAndArith);

    /// <summary>Execute a string-interpolation script (exercises BuildString opcode).</summary>
    [Benchmark]
    public void Run_Interpolation() => RunSource(_interpolation);

    /// <summary>
    /// Execute a Sprint 4 control-flow script: 100-iteration <c>while</c> loop with
    /// <c>select</c> dispatch (exercises JumpIfFalse, Loop, Jump and select equality
    /// chains together).
    /// </summary>
    [Benchmark]
    public void Run_ControlFlow() => RunSource(_controlFlow);

    private static void RunSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Chunk chunk = Grob.Compiler.Compiler.Compile(unit, bag);
        // TextWriter.Null discards print() output — we benchmark VM execution,
        // not I/O throughput.
        var vm = new VirtualMachine(TextWriter.Null);
        vm.Run(chunk);
    }
}
