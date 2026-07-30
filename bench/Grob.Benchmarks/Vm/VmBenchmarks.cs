using BenchmarkDotNet.Attributes;
using Grob.Compiler;
using Grob.Core;
using Grob.Stdlib;
using Grob.Vm;

namespace Grob.Benchmarks.Vm;

/// <summary>
/// VM-execution category benchmarks (Sprint 3 baseline, D-309).
/// Measures the full pipeline — lex, parse, type-check, compile, VM execute —
/// for representative programmes.  The baseline JSON for this category is
/// produced via the <c>benchmark.yml</c> GitHub Actions workflow (D-309) on a
/// <c>windows-latest</c> runner; the committed <c>baseline/vm.json</c> must
/// not be replaced with a locally-produced file.
/// </summary>
[MemoryDiagnoser]
public class VmBenchmarks {
    private string _declAndArith = null!;
    private string _interpolation = null!;
    private string _controlFlow = null!;
    private string _arrayForIn = null!;
    private string _mapForIn = null!;
    private string _attrEmpty = null!;
    private string _attrRange = null!;
    private string _attrNative = null!;
    private string _attrBuild = null!;

    /// <summary>Reads benchmark fixture files from disk once before any benchmark run.</summary>
    [GlobalSetup]
    public void Setup() {
        // Path.Join is used here (not Path.Combine) — Path.Join never resets
        // the path on a rooted later argument, which avoids the CodeQL
        // cs/path-injection concern that Path.Combine carries.
        string fixturesDir = Path.Join(AppContext.BaseDirectory, "Fixtures", "Vm");
        _declAndArith = File.ReadAllText(Path.Join(fixturesDir, "decl-and-arith.grob"));
        _interpolation = File.ReadAllText(Path.Join(fixturesDir, "interpolation.grob"));
        _controlFlow = File.ReadAllText(Path.Join(fixturesDir, "control-flow.grob"));
        _arrayForIn = File.ReadAllText(Path.Join(fixturesDir, "array-for-in.grob"));
        _mapForIn = File.ReadAllText(Path.Join(fixturesDir, "map-for-in.grob"));
        _attrEmpty = File.ReadAllText(Path.Join(fixturesDir, "attr-empty.grob"));
        _attrRange = File.ReadAllText(Path.Join(fixturesDir, "attr-range.grob"));
        _attrNative = File.ReadAllText(Path.Join(fixturesDir, "attr-native.grob"));
        _attrBuild = File.ReadAllText(Path.Join(fixturesDir, "attr-build.grob"));
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

    /// <summary>
    /// Execute an array <c>for...in</c> over 1,000 elements (D-383). Measures the
    /// cost of the contents-snapshot copy added to the previously copy-free array
    /// <c>for...in</c> path — see D-313's benchmark obligation.
    /// </summary>
    [Benchmark]
    public void Run_ArrayForIn() => RunSource(_arrayForIn);

    /// <summary>
    /// Execute a map <c>for...in</c> over 1,000 entries (D-383). Measures the cost
    /// of the extra values-array snapshot added alongside the pre-existing keys
    /// snapshot — see D-313's benchmark obligation.
    /// </summary>
    [Benchmark]
    public void Run_MapForIn() => RunSource(_mapForIn);

    /// <summary>
    /// Phase 1 allocation-attribution fixture (throwaway,
    /// <c>prompts/archive/sprint-9/phase1-allocation-attribution.md</c>): pipeline +
    /// VM setup floor, no loop, no native calls.
    /// </summary>
    [Benchmark]
    public void Run_AttrEmpty() => RunSource(_attrEmpty);

    /// <summary>
    /// Phase 1 allocation-attribution fixture (throwaway): 1,000-iteration numeric
    /// range <c>for...in</c> with an empty body — isolates range-loop machinery from
    /// native-call cost.
    /// </summary>
    [Benchmark]
    public void Run_AttrRange() => RunSource(_attrRange);

    /// <summary>
    /// Phase 1 allocation-attribution fixture (throwaway): 1,000 native calls with no
    /// collection growth — isolates per-native-call dispatch overhead (the args array
    /// and <c>VmInvoker</c> closure built on every native call).
    /// </summary>
    [Benchmark]
    public void Run_AttrNative() => RunSource(_attrNative);

    /// <summary>
    /// Phase 1 allocation-attribution fixture (throwaway): 1,000 native calls that
    /// also grow a <c>GrobArray</c> via <c>append</c> — isolates array-growth cost
    /// from bare native-call dispatch cost.
    /// </summary>
    [Benchmark]
    public void Run_AttrBuild() => RunSource(_attrBuild);

    private static void RunSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Chunk chunk = Grob.Compiler.Compiler.Compile(unit, bag);
        // TextWriter.Null discards print() output — we benchmark VM execution,
        // not I/O throughput.
        var vm = new VirtualMachine(TextWriter.Null);
        // Phase 1 allocation-attribution (throwaway,
        // prompts/archive/sprint-9/phase1-allocation-attribution.md): registered
        // uniformly, on every fixture including the pre-existing five, so its one-time
        // cost cancels out of every pairwise subtraction the attribution note derives.
        // StringMethodsPlugin is pure (no capability injection) and is the only stdlib
        // plugin any attr-*.grob fixture calls into ("x".upper() in attr-native.grob) —
        // Grob.Cli.RunCommand is the composition root that registers the full stdlib
        // set for real script runs; this harness deliberately stays minimal.
        new StringMethodsPlugin().Register(vm);
        vm.Run(chunk);
    }
}
