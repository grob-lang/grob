using BenchmarkDotNet.Attributes;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

namespace Grob.Benchmarks.Vm;

/// <summary>
/// VM-execution category benchmarks (Sprint 3 baseline, D-309; rebuilt in place by
/// D-385/D-386/D-387). Measures <c>vm.Run(chunk)</c> alone, against a <c>Chunk</c>
/// compiled once in <see cref="Setup"/> — restoring <c>grob-benchmarking-strategy.md</c>
/// §4.2's always-standing definition ("hand-constructed <c>Chunk</c> instances — the
/// compiler is not involved") from which the previous full-pipeline implementation had
/// drifted. The baseline JSON for this category is produced via the
/// <c>benchmark.yml</c> GitHub Actions workflow (D-309) on a <c>windows-latest</c>
/// runner; the committed <c>baseline/vm.json</c> must not be replaced with a
/// locally-produced file. Because this rebuild changes what the category measures,
/// <c>vm.json</c>/<c>vm.origin.json</c> as committed before D-387 describe a different
/// quantity and need a deliberate re-capture before they mean anything again (D-387).
/// </summary>
[MemoryDiagnoser]
public class VmBenchmarks {
    private Chunk _declAndArith = null!;
    private Chunk _interpolation = null!;
    private Chunk _controlFlow = null!;
    private Chunk _arrayForIn = null!;
    private Chunk _mapForIn = null!;
    private VirtualMachine _vm = null!;

    /// <summary>Compiles every fixture to a <c>Chunk</c> once, before any benchmark run.
    /// Compilation is not part of the measured region (D-385/D-386 Q1').</summary>
    [GlobalSetup]
    public void Setup() {
        // Path.Join is used here (not Path.Combine) — Path.Join never resets
        // the path on a rooted later argument, which avoids the CodeQL
        // cs/path-injection concern that Path.Combine carries.
        string fixturesDir = Path.Join(AppContext.BaseDirectory, "Fixtures", "Vm");
        _declAndArith = CompileFixture(Path.Join(fixturesDir, "decl-and-arith.grob"));
        _interpolation = CompileFixture(Path.Join(fixturesDir, "interpolation.grob"));
        _controlFlow = CompileFixture(Path.Join(fixturesDir, "control-flow.grob"));
        _arrayForIn = CompileFixture(Path.Join(fixturesDir, "array-for-in.grob"));
        _mapForIn = CompileFixture(Path.Join(fixturesDir, "map-for-in.grob"));
    }

    /// <summary>
    /// Constructs a fresh <see cref="VirtualMachine"/> before every iteration. Several
    /// fixtures (<c>array-for-in</c>, <c>map-for-in</c>) mutate global state via the same
    /// reused <c>Chunk</c>, so a VM shared across iterations would compound that state
    /// (array/map growth) run over run and invalidate the measurement — the mechanism
    /// D-385's own text assumes remains necessary. TextWriter.Null discards print()
    /// output — this category benchmarks VM execution, not I/O throughput.
    /// </summary>
    [IterationSetup]
    public void IterationSetup() => _vm = new VirtualMachine(TextWriter.Null);

    /// <summary>Execute a declarations-and-arithmetic script (warm path, minimal).</summary>
    [Benchmark(Baseline = true)]
    public void Run_DeclAndArith() => _vm.Run(_declAndArith);

    /// <summary>Execute a string-interpolation script (exercises BuildString opcode).</summary>
    [Benchmark]
    public void Run_Interpolation() => _vm.Run(_interpolation);

    /// <summary>
    /// Execute a Sprint 4 control-flow script: 100-iteration <c>while</c> loop with
    /// <c>select</c> dispatch (exercises JumpIfFalse, Loop, Jump and select equality
    /// chains together).
    /// </summary>
    [Benchmark]
    public void Run_ControlFlow() => _vm.Run(_controlFlow);

    /// <summary>
    /// Execute an array <c>for...in</c> over 1,000 elements (D-383). Measures the
    /// cost of the contents-snapshot copy added to the previously copy-free array
    /// <c>for...in</c> path — see D-313's benchmark obligation.
    /// </summary>
    [Benchmark]
    public void Run_ArrayForIn() => _vm.Run(_arrayForIn);

    /// <summary>
    /// Execute a map <c>for...in</c> over 1,000 entries (D-383). Measures the cost
    /// of the extra values-array snapshot added alongside the pre-existing keys
    /// snapshot — see D-313's benchmark obligation.
    /// </summary>
    [Benchmark]
    public void Run_MapForIn() => _vm.Run(_mapForIn);

    private static Chunk CompileFixture(string path) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(File.ReadAllText(path), bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return Grob.Compiler.Compiler.Compile(unit, bag);
    }
}
