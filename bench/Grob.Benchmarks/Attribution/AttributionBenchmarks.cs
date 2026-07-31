using BenchmarkDotNet.Attributes;
using Grob.Compiler;
using Grob.Core;
using Grob.Stdlib;
using Grob.Vm;

namespace Grob.Benchmarks.Attribution;

/// <summary>
/// Allocation-attribution differential fixtures (D-385/D-386 Q5'), a dedicated
/// non-gating category (<c>policy.json</c>: <c>"gating": false</c>). These are
/// instruments, not guards — they measure the pipeline floor, loop machinery and
/// per-native-call overhead by subtracting successive fixtures
/// (<c>docs/design/bench-allocation-attribution.md</c>), not features anyone should
/// gate on. A subtraction is only as clean as the pair it is taken over: each
/// benchmark below names the fixture it is meant to be differenced against, and
/// pairs spanning two different native-dispatch paths give a combined cost, not an
/// isolated one (phase 1 §4). They stay whole-script by design: perfect
/// isolation of a fragment inside its own measured region is unattainable, so each
/// fixture measures the full lex/parse/type-check/compile/run pipeline and the
/// differential technique does the isolating instead.
/// </summary>
[MemoryDiagnoser]
public class AttributionBenchmarks {
    private string _attrEmpty = null!;
    private string _attrRange = null!;
    private string _attrNative = null!;
    private string _attrArrayDispatch = null!;
    private string _attrBuild = null!;
    private string _attrMapBuild = null!;
    private string _attrSnapshotEmpty = null!;

    /// <summary>Reads benchmark fixture files from disk once before any benchmark run.</summary>
    [GlobalSetup]
    public void Setup() {
        // Path.Join is used here (not Path.Combine) — Path.Join never resets
        // the path on a rooted later argument, which avoids the CodeQL
        // cs/path-injection concern that Path.Combine carries.
        string fixturesDir = Path.Join(AppContext.BaseDirectory, "Fixtures", "Attribution");
        _attrEmpty = File.ReadAllText(Path.Join(fixturesDir, "attr-empty.grob"));
        _attrRange = File.ReadAllText(Path.Join(fixturesDir, "attr-range.grob"));
        _attrNative = File.ReadAllText(Path.Join(fixturesDir, "attr-native.grob"));
        _attrArrayDispatch = File.ReadAllText(Path.Join(fixturesDir, "attr-array-dispatch.grob"));
        _attrBuild = File.ReadAllText(Path.Join(fixturesDir, "attr-build.grob"));
        _attrMapBuild = File.ReadAllText(Path.Join(fixturesDir, "attr-map-build.grob"));
        _attrSnapshotEmpty = File.ReadAllText(Path.Join(fixturesDir, "attr-snapshot-empty.grob"));
    }

    /// <summary>
    /// Pipeline + VM setup floor. No loop, no native calls and no statements at all —
    /// as the fixture every other one is differenced against, it deliberately carries
    /// no terminal operation those fixtures lack.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Run_AttrEmpty() => RunSource(_attrEmpty);

    /// <summary>
    /// 1,000-iteration numeric range <c>for...in</c> with an empty body — isolates
    /// range-loop machinery from native-call cost.
    /// </summary>
    [Benchmark]
    public void Run_AttrRange() => RunSource(_attrRange);

    /// <summary>
    /// 1,000 Stdlib-plugin native calls with no collection growth — isolates
    /// per-native-call dispatch overhead on that path (the args array and
    /// <c>VmInvoker</c> closure built on every native call in
    /// <c>VirtualMachine.cs</c>). Difference against <c>Run_AttrRange</c>. This
    /// covers the registered-once, <c>GetGlobal</c>-per-call path only — not the
    /// costlier array/map instance-method path (phase 1 §4).
    /// </summary>
    [Benchmark]
    public void Run_AttrNative() => RunSource(_attrNative);

    /// <summary>
    /// 1,000 <c>xs.contains(i)</c> calls on an array that never grows — the
    /// growth-free control for <c>Run_AttrBuild</c>, and the dedicated fixture phase 1
    /// §4 recorded as missing. <c>contains</c> and <c>append</c> are bound by the same
    /// <c>OpCode.GetProperty</c> path through <c>ArrayNatives.GetMethod</c>, both as
    /// arity-1 natives capturing the receiver, so both pay the identical
    /// <c>NativeFunction</c>/delegate/display-class triple per call.
    /// </summary>
    [Benchmark]
    public void Run_AttrArrayDispatch() => RunSource(_attrArrayDispatch);

    /// <summary>
    /// 1,000 <c>append</c> calls that also grow a <c>GrobArray</c>. Difference against
    /// <c>Run_AttrArrayDispatch</c> to isolate array growth — the two share a dispatch
    /// path, so only growth remains. Difference against <c>Run_AttrNative</c> instead
    /// and the result is the combined array-member-dispatch tax plus growth, because
    /// that pair spans two structurally different native paths (phase 1 §4).
    /// </summary>
    [Benchmark]
    public void Run_AttrBuild() => RunSource(_attrBuild);

    /// <summary>
    /// Map construction only, no second loop — splits <c>Run_MapForIn</c>'s 1,000
    /// <c>"k${i}"</c> interpolations from its second (values) snapshot array
    /// (D-385/D-386 Q5').
    /// </summary>
    [Benchmark]
    public void Run_AttrMapBuild() => RunSource(_attrMapBuild);

    /// <summary>
    /// The same 1,000-append build loop as <c>attr-build.grob</c>, plus an
    /// empty-body <c>for...in</c> over the built array — isolates the pure
    /// contents-snapshot copy (D-383) from any iteration-body cost, testing D-386's
    /// doubling hypothesis for the unexplained ~55 KB found in phase 1 §5.
    /// </summary>
    [Benchmark]
    public void Run_AttrSnapshotEmpty() => RunSource(_attrSnapshotEmpty);

    private static void RunSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Chunk chunk = Grob.Compiler.Compiler.Compile(unit, bag);
        // TextWriter.Null discards print() output — we benchmark VM execution,
        // not I/O throughput.
        var vm = new VirtualMachine(TextWriter.Null);
        // Registered uniformly, on every fixture, so its one-time registration cost
        // cancels out of every pairwise subtraction the attribution note derives
        // (D-385 Q4, ratifying phase 1's stopgap as the permanent approach).
        // StringMethodsPlugin is pure (no capability injection) and is the only
        // stdlib plugin any attr-*.grob fixture calls into ("x".upper() in
        // attr-native.grob). Array/map instance-method dispatch (xs.append/
        // xs.contains/m.set — ArrayNatives/MapNatives) is a structurally distinct,
        // more expensive native-call path (a fresh NativeFunction/closure/
        // display-class triple built on every GetProperty dispatch, phase 1 §4)
        // reachable without any
        // plugin registration at all — Grob.Cli.RunCommand is the composition root
        // that registers the full stdlib set for real script runs; this harness
        // deliberately stays minimal.
        new StringMethodsPlugin().Register(vm);
        vm.Run(chunk);
    }
}
