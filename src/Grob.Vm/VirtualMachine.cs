using System.Threading;
using Grob.Core;
using Grob.Runtime;

namespace Grob.Vm;

/// <summary>
/// The Grob stack-based bytecode VM. Owns the operand stack and the
/// fetch-decode-execute dispatch loop. Sprint 2 Increment B implements the
/// subset of <see cref="OpCode"/> needed to execute hand-constructed chunks
/// up to <c>print(2 + 3 * 4)</c>; subsequent increments extend the dispatch
/// table. Out-of-scope opcodes raise <see cref="GrobInternalException"/> until
/// their owning increment lands.
///
/// Sprint 5 Increment C adds:
/// - <see cref="NativeFunction"/> dispatch and the re-entrant call-back bridge
///   (<see cref="InvokeCallable"/>).
/// - Array higher-order method binding (<see cref="OpCode.GetProperty"/> arm).
/// - D-319 cooperative-cancellation step-budget seam (<see cref="Run"/>).
///
/// Authority: grob-vm-architecture.md (dispatch loop, value stack, developer
/// diagnostics) and grob-v1-requirements.md §3.3 (the OpCode set).
/// </summary>
public sealed class VirtualMachine : IPluginRegistrar {
    /// <summary>
    /// Maximum call depth (D-180). The frames array holds call frames only — the
    /// top-level script is not a frame — so a 257th nested call has no slot and
    /// raises E5901 rather than overflowing the host's CLR stack.
    /// </summary>
    private const int MaxFrames = 256;

    /// <summary>
    /// D-319: check the cancellation token every 256 dispatch iterations.
    /// The mask must be a power-of-two minus one so the bitwise AND is a
    /// single instruction.  256 iterations ≈ 1–2 µs at current throughput —
    /// fine-grained enough to feel instant to an interactive user, coarse
    /// enough that the test-and-branch cost is negligible in steady state.
    /// </summary>
    private const long BudgetMask = 0xFF;

    // GrobError leaf type names for TryRaiseRuntimeGrobError's routed-fault sites
    // (Sprint 7 Increment D) — named constants rather than repeated literals
    // (SonarCloud S1192; ArithmeticError alone appears at 5 call sites).
    private const string ArithmeticErrorLeaf = "ArithmeticError";
    private const string IndexErrorLeaf = "IndexError";
    private const string NilErrorLeaf = "NilError";
    private const string RuntimeErrorLeaf = "RuntimeError";

    private readonly ValueStack _stack = new();
    private readonly IStandardStreams _streams;
    private readonly Dictionary<string, GrobValue> _globals = new(StringComparer.Ordinal);

    // -----------------------------------------------------------------------
    // Top-level initialisation state machine (§19.1, D-294).
    //
    // _globalStates carries a SlotState tag per top-level binding name. It is
    // pre-scanned from the chunk at the start of each Run() so that a GetGlobal
    // of a binding the chunk defines later can be distinguished from an
    // undefined name (E1001) and reported as circular initialisation (E5902).
    // Native functions registered before Run() are not in this map and so are
    // never subject to the check.
    //
    // After the top-level script's terminal Return, _startupComplete is set and
    // the tag is no longer consulted — the cost is a single branch per global
    // read during startup and zero afterwards.
    // -----------------------------------------------------------------------
    private readonly Dictionary<string, SlotState> _globalStates = new(StringComparer.Ordinal);
    private bool _startupComplete;

    // Top-level bindings in DefineGlobal (emission) order, each with the source line
    // of its declaration. Built by the pre-scan and used only to compose the E5902
    // trace-through-function message (§19.1, D-321): the binding being initialised is
    // the first entry not yet Initialised, and a read binding's declaration line is
    // looked up here.
    private readonly List<(string Name, int Line)> _topLevelBindings = [];

    /// <summary>The call-stack frames (D-180). Active entries are <c>0.._frameCount-1</c>.</summary>
    private readonly CallFrame[] _frames = new CallFrame[MaxFrames];
    private int _frameCount;

    // -----------------------------------------------------------------------
    // Open-upvalue list (Sprint 5 Increment D — D-115).
    //
    // While a closure is active, its captured variables are "open": each
    // Upvalue points directly at a stack slot so that reads and writes flow
    // through to the live frame. When the enclosing function returns,
    // CloseUpvaluesFrom(stackBase) copies every open upvalue at a slot >=
    // stackBase to the heap and removes it from this list. Two closures from
    // the same enclosing call that capture the same variable share one Upvalue
    // object in this list (dedup is in CaptureUpvalue).
    // -----------------------------------------------------------------------
    private readonly List<Upvalue> _openUpvalues = [];

    // -----------------------------------------------------------------------
    // D-319: step counter — VM-instance lifetime, NOT reset per Run() call so
    // the budget is continuous across the re-entrant native↔VM call-back bridge.
    // -----------------------------------------------------------------------
    private long _steps;

    // -----------------------------------------------------------------------
    // Active dispatch state (instance fields so InvokeCallable can save/restore
    // them when bridging into a re-entrant lambda call from a native).
    // The outer Run() initialises these; RunDispatch reads/writes them in place.
    // -----------------------------------------------------------------------

    /// <summary>The chunk currently being dispatched.</summary>
    private Chunk _activeChunk = null!;

    /// <summary>Instruction pointer into <see cref="_activeChunk"/>.</summary>
    private int _ip;

    /// <summary>
    /// Stack index of slot 0 for the active call frame.  Locals are addressed
    /// as <c>_stack[_stackBase + slot]</c>.
    /// </summary>
    private int _stackBase;

    /// <summary>
    /// The <see cref="CancellationToken"/> for the current top-level
    /// <see cref="Run"/> invocation.  Stored as a field so
    /// <see cref="InvokeCallable"/> can thread it through re-entrant
    /// <see cref="RunDispatch"/> calls without adding parameters to every
    /// switch arm.
    /// </summary>
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Writer for the per-instruction trace hook. Only read inside
    /// <c>#if DEBUG</c>, so it appears unread to Release-mode static analysis;
    /// SonarCloud waives S4487 for this file in
    /// <c>.github/workflows/sonarcloud.yml</c>.
    /// </summary>
    private readonly TextWriter _trace;

    /// <summary>
    /// Renders values for <see cref="OpCode.Print"/> and <see cref="OpCode.BuildString"/>
    /// (D-336). No registry is wired in yet — real built-in/plugin <c>toString()</c>
    /// registration is a later increment's job (see <c>IValueToStringRegistry</c>).
    /// </summary>
    private readonly ValueDisplay _valueDisplay = new();

    /// <summary>
    /// Construct a VM whose <see cref="OpCode.Print"/> output goes to
    /// <paramref name="output"/>. <paramref name="trace"/> receives the
    /// <c>#if DEBUG</c> per-instruction trace (defaults to
    /// <see cref="TextWriter.Null"/>, which is also the only meaningful value
    /// in Release where the trace call is compiled out entirely).
    /// </summary>
    /// <remarks>
    /// Wraps <paramref name="output"/> in a minimal internal
    /// <see cref="IStandardStreams"/> (D-343) — kept alongside the
    /// <see cref="VirtualMachine(IStandardStreams, TextWriter?)"/> overload so the
    /// existing single-<see cref="TextWriter"/> call sites across the test suite and
    /// <c>Grob.Cli</c> need no change.
    /// </remarks>
    public VirtualMachine(TextWriter output, TextWriter? trace = null)
        : this(new SingleWriterStreams(output ?? throw new ArgumentNullException(nameof(output))), trace) { }

    /// <summary>
    /// Construct a VM whose <see cref="OpCode.Print"/> output and future
    /// stderr-routed diagnostics go through <paramref name="streams"/> (D-343, the
    /// capability-injection seam) — the overload <c>Grob.Cli</c>'s composition root
    /// uses, passing an OS-backed implementation wrapping <see cref="Console.Out"/>/
    /// <see cref="Console.Error"/>. <paramref name="trace"/> is as in the
    /// <see cref="VirtualMachine(TextWriter, TextWriter?)"/> overload.
    /// </summary>
    public VirtualMachine(IStandardStreams streams, TextWriter? trace = null) {
        ArgumentNullException.ThrowIfNull(streams);
        _streams = streams;
        _trace = trace ?? TextWriter.Null;
    }

    /// <summary>The operand stack, exposed for tests to inspect post-run state.</summary>
    public ValueStack Stack => _stack;

    /// <summary>The global variables table, exposed for tests to inspect post-run state.</summary>
    public IReadOnlyDictionary<string, GrobValue> Globals => _globals;

    /// <summary>
    /// Test-only visibility into the active call-frame depth (Sprint 7 Increment A).
    /// Lets <c>Grob.Vm.Tests</c> assert that an unhandled <see cref="OpCode.Throw"/>
    /// actually unwinds every frame — rather than merely raising the diagnostic with
    /// stale frame state left behind — mirroring the existing <see cref="Stack"/>
    /// test-visibility precedent.
    /// </summary>
    internal int FrameCount => _frameCount;

    /// <summary>
    /// Register a native C# function under <paramref name="name"/> in the global
    /// variables table so that Grob scripts can call it by that name.
    /// Overwrites any previous binding with the same name (last write wins).
    /// </summary>
    public void RegisterNative(string name, NativeFunction fn) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fn);
        _globals[name] = GrobValue.FromFunction(fn);
    }

    /// <summary>
    /// Register a constant value under <paramref name="name"/> in the global variables
    /// table (D-343) — the runtime counterpart of a namespace constant such as
    /// <c>math.pi</c>, which has no callable behaviour to dispatch through
    /// <see cref="RegisterNative"/>. Overwrites any previous binding with the same name
    /// (last write wins).
    /// </summary>
    public void RegisterConstant(string name, GrobValue value) {
        ArgumentNullException.ThrowIfNull(name);
        _globals[name] = value;
    }

    /// <summary>
    /// Execute <paramref name="chunk"/> until <see cref="OpCode.Return"/>.
    /// Running off the end of the bytecode without a <see cref="OpCode.Return"/>
    /// is treated as a malformed chunk — it raises
    /// <see cref="GrobInternalException"/>, because the compiler always emits
    /// a terminating <c>Return</c> and hand-constructed test chunks must do
    /// the same.
    /// </summary>
    /// <param name="chunk">The top-level bytecode chunk to execute.</param>
    /// <param name="cancellationToken">
    /// D-319: the cooperative-cancellation token.  Pass
    /// <see cref="CancellationToken.None"/> (the default) for unlimited
    /// execution — production callers wire their own policy.  When the token
    /// is signalled, the dispatch loop raises
    /// <see cref="OperationCanceledException"/> on the next budget-check
    /// boundary; this exception is outside the <see cref="GrobRuntimeException"/>
    /// hierarchy so a Grob <c>catch</c> block cannot swallow it.
    /// </param>
    // Bytecode dispatch loop. Per D-302 each opcode is handled inline in a
    // single switch to keep dispatch branch-free; extracting per-opcode
    // handlers would add a call frame per instruction and is explicitly
    // rejected. SonarCloud suppresses S3776 (cognitive complexity) for this
    // file in .github/workflows/sonarcloud.yml.
    public void Run(Chunk chunk, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(chunk);

        // Defensive: a prior Run that terminated by exception, exit() or
        // cancellation may have left values on the operand stack, call frames or
        // open upvalues (a frame that never reached Return). Start every invocation
        // clean so the VM behaves the same on the Nth chunk as on the first — and
        // so CaptureUpvalue cannot deduplicate against a stale cell from a prior run.
        _stack.Reset();
        _frameCount = 0;
        _openUpvalues.Clear();

        // Top-level initialisation state machine (§19.1, D-294): tag every
        // top-level binding the chunk defines as Uninitialised, and re-arm the
        // startup check. The states are rebuilt per Run() so a fresh chunk gets
        // its own circular-initialisation detection.
        _globalStates.Clear();
        _topLevelBindings.Clear();
        _startupComplete = false;
        ScanTopLevelGlobals(chunk);

        // Initialise active dispatch state (instance fields shared with InvokeCallable).
        _activeChunk = chunk;
        _ip = 0;
        _stackBase = 0;
        _cancellationToken = cancellationToken;

        // _steps is intentionally NOT reset — the step budget is VM-instance-lifetime
        // so it spans re-entrant InvokeCallable calls (D-319).

        RunDispatch(floorFrameCount: 0, isReentrant: false);
    }

    // -----------------------------------------------------------------------
    // Core dispatch loop
    // -----------------------------------------------------------------------

    /// <summary>
    /// Inner dispatch loop.  Executes bytecode from the current
    /// <see cref="_activeChunk"/> / <see cref="_ip"/> / <see cref="_stackBase"/>
    /// until the dispatch's entry body returns.
    /// </summary>
    /// <param name="floorFrameCount">
    /// In re-entrant mode, the <see cref="_frameCount"/> value the callee was invoked
    /// at; a <c>Return</c> that pops back to this depth ends the invocation.  Ignored
    /// in top-level mode (where the script's terminal <c>Return</c> at frame 0 is the
    /// exit signal).
    /// </param>
    /// <param name="isReentrant">
    /// <see langword="false"/> for the top-level <see cref="Run"/> dispatch: the script
    /// body runs at frame 0 and is not a call frame, so its terminal <c>Return</c>
    /// (executed with <see cref="_frameCount"/> already 0, nothing to pop) ends the
    /// dispatch — and a top-level function call that returns to frame 0 must
    /// <em>continue</em> the script.  <see langword="true"/> for
    /// <see cref="InvokeCallable"/>: the callee is a real frame above
    /// <paramref name="floorFrameCount"/>, so the dispatch ends when that frame's
    /// <c>Return</c> pops <see cref="_frameCount"/> back down to the floor.
    /// </param>
    /// <param name="boundedFinally">
    /// <see langword="true"/> when this dispatch is running a single <c>finally</c> body on
    /// the exceptional unwind path (Sprint 7 Increment C, D-275). In that mode the run ends
    /// when it reaches its own region's closing <see cref="OpCode.TryEnd"/> — tracked by
    /// <c>finallyDepth</c> counting the TryBegin/TryEnd of any nested try inside the finally,
    /// so only the unbalanced closing TryEnd stops it — rather than at a <c>Return</c>. A
    /// throw that escapes the body raises <see cref="FinallyEscapeException"/> via
    /// <see cref="PropagateThrow"/>.
    /// </param>
    /// <param name="finallyBoundaryStart">Start offset of the bounded finally body (the
    /// floor below which regions are ineligible at the finally's own frame), or −1.</param>
    /// <param name="finallyBoundaryFloor">Frame count the bounded finally runs at, or −1.</param>
    private void RunDispatch(
            int floorFrameCount, bool isReentrant,
            bool boundedFinally = false, int finallyBoundaryStart = -1, int finallyBoundaryFloor = -1) {
        int line = 0;
        int column = 0;
        int finallyDepth = 0;

        while (true) {
            // D-319: cooperative cancellation check every BudgetMask+1 steps.
            if ((++_steps & BudgetMask) == 0)
                _cancellationToken.ThrowIfCancellationRequested();

            if (_ip >= _activeChunk.Count)
                throw new GrobInternalException(
                    "execution ran past end of chunk without Return");

            line = _activeChunk.GetLine(_ip);
            column = _activeChunk.GetColumn(_ip);

#if DEBUG
            TraceInstruction(_activeChunk, _ip);
#endif

            byte instruction = _activeChunk.ReadByte(_ip);
            _ip++;

            try {
                switch ((OpCode)instruction) {
                    // --- Constants and singletons ---
                    case OpCode.Constant: {
                            byte index = _activeChunk.ReadByte(_ip++);
                            _stack.Push(_activeChunk.ReadConstant(index), line);
                            break;
                        }
                    case OpCode.ConstantLong: {
                            int index = (_activeChunk.ReadByte(_ip) << 8) | _activeChunk.ReadByte(_ip + 1);
                            _ip += 2;
                            _stack.Push(_activeChunk.ReadConstant(index), line);
                            break;
                        }
                    case OpCode.Nil: _stack.Push(GrobValue.Nil, line); break;
                    case OpCode.True: _stack.Push(GrobValue.FromBool(true), line); break;
                    case OpCode.False: _stack.Push(GrobValue.FromBool(false), line); break;

                    case OpCode.Pop: _stack.Pop(); break;
                    case OpCode.PopN: {
                            byte count = _activeChunk.ReadByte(_ip++);
                            for (int i = 0; i < count; i++) _stack.Pop();
                            break;
                        }

                    // --- Globals ---
                    case OpCode.DefineGlobal: {
                            byte nameIdx = _activeChunk.ReadByte(_ip++);
                            string name = _activeChunk.ReadConstant(nameIdx).AsString();
                            // Transition Uninitialised -> Initialising before the value is
                            // stored, then -> Initialised once it is. The right-hand side has
                            // already been evaluated onto the stack, so Initialising is a
                            // transient within this opcode; both non-Initialised states map to
                            // the same circular-initialisation diagnostic on a read.
                            if (_globalStates.ContainsKey(name))
                                _globalStates[name] = SlotState.Initialising;
                            _globals[name] = _stack.Pop();
                            if (_globalStates.ContainsKey(name))
                                _globalStates[name] = SlotState.Initialised;
                            break;
                        }
                    case OpCode.GetGlobal: {
                            byte nameIdx = _activeChunk.ReadByte(_ip++);
                            string name = _activeChunk.ReadConstant(nameIdx).AsString();
                            // Startup check (§19.1, D-294): a read of a tagged slot that is not
                            // yet Initialised is a circular initialisation — E5902. Skipped
                            // entirely once _startupComplete is set, and never applied to
                            // names that carry no tag (e.g. registered natives).
                            if (!_startupComplete
                                && _globalStates.TryGetValue(name, out SlotState state)
                                && state != SlotState.Initialised)
                                throw new GrobRuntimeException(ErrorCatalog.E5902.Code, line, column,
                                    ComposeCircularInitMessage(name));
                            if (!_globals.TryGetValue(name, out GrobValue val))
                                throw new GrobRuntimeException(ErrorCatalog.E1001.Code, line, column,
                                    $"Undefined global '{name}'.");
                            _stack.Push(val, line);
                            break;
                        }
                    case OpCode.SetGlobal: {
                            byte nameIdx = _activeChunk.ReadByte(_ip++);
                            string name = _activeChunk.ReadConstant(nameIdx).AsString();
                            if (!_globals.ContainsKey(name))
                                throw new GrobRuntimeException(ErrorCatalog.E1001.Code, line, column,
                                    $"Undefined global '{name}'.");
                            _globals[name] = _stack.Pop();
                            break;
                        }

                    // --- Locals (slots are relative to the active frame's stack base) ---
                    case OpCode.GetLocal: {
                            byte slot = _activeChunk.ReadByte(_ip++);
                            _stack.Push(_stack.GetSlot(_stackBase + slot), line);
                            break;
                        }
                    case OpCode.SetLocal: {
                            byte slot = _activeChunk.ReadByte(_ip++);
                            _stack.SetSlot(_stackBase + slot, _stack.Pop());
                            break;
                        }

                    // --- Increment / decrement (int locals only; float arms are compile errors) ---
                    case OpCode.IncrementInt: {
                            byte slot = _activeChunk.ReadByte(_ip++);
                            long cur = _stack.GetSlot(_stackBase + slot).AsInt();
                            _stack.SetSlot(_stackBase + slot, GrobValue.FromInt(checked(cur + 1L)));
                            break;
                        }
                    case OpCode.DecrementInt: {
                            byte slot = _activeChunk.ReadByte(_ip++);
                            long cur = _stack.GetSlot(_stackBase + slot).AsInt();
                            _stack.SetSlot(_stackBase + slot, GrobValue.FromInt(checked(cur - 1L)));
                            break;
                        }

                    // --- Integer arithmetic (checked; OverflowException → E5001) ---
                    case OpCode.AddInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromInt(checked(a + b)), line);
                            break;
                        }
                    case OpCode.SubtractInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromInt(checked(a - b)), line);
                            break;
                        }
                    case OpCode.MultiplyInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromInt(checked(a * b)), line);
                            break;
                        }
                    case OpCode.DivideInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            if (b == 0L) {
                                const string message = "integer division by zero";
                                if (!TryRaiseRuntimeGrobError(ArithmeticErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobArithmeticException(ErrorCatalog.E5002.Code, line, column, message);
                                break;
                            }
                            // long.MinValue / -1 overflows: caught below as E5001.
                            _stack.Push(GrobValue.FromInt(checked(a / b)), line);
                            break;
                        }
                    case OpCode.ModuloInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            if (b == 0L) {
                                const string message = "integer modulo by zero";
                                if (!TryRaiseRuntimeGrobError(ArithmeticErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobArithmeticException(ErrorCatalog.E5003.Code, line, column, message);
                                break;
                            }
                            _stack.Push(GrobValue.FromInt(checked(a % b)), line);
                            break;
                        }
                    case OpCode.NegateInt: {
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromInt(checked(-a)), line);
                            break;
                        }

                    // --- Float arithmetic (D-273: x / 0.0 and x % 0.0 throw) ---
                    case OpCode.AddFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromFloat(a + b), line);
                            break;
                        }
                    case OpCode.SubtractFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromFloat(a - b), line);
                            break;
                        }
                    case OpCode.MultiplyFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromFloat(a * b), line);
                            break;
                        }
                    case OpCode.DivideFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            // Exact-zero check is intentional per D-273: +0.0/-0.0 both caught,
                            // NaN propagates as NaN. SonarCloud suppresses S1244 for this file
                            // in .github/workflows/sonarcloud.yml.
                            if (b == 0.0) {
                                const string message = "float division by zero";
                                if (!TryRaiseRuntimeGrobError(ArithmeticErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobArithmeticException(ErrorCatalog.E5004.Code, line, column, message);
                                break;
                            }
                            _stack.Push(GrobValue.FromFloat(a / b), line);
                            break;
                        }
                    case OpCode.ModuloFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            // See S1244 note above on DivideFloat — intentional exact-zero check (D-273).
                            if (b == 0.0) {
                                const string message = "float modulo by zero";
                                if (!TryRaiseRuntimeGrobError(ArithmeticErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobArithmeticException(ErrorCatalog.E5005.Code, line, column, message);
                                break;
                            }
                            _stack.Push(GrobValue.FromFloat(a % b), line);
                            break;
                        }
                    case OpCode.NegateFloat: {
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromFloat(-a), line);
                            break;
                        }

                    // --- Strings ---
                    case OpCode.Concat: {
                            string b = _stack.Pop().AsString();
                            string a = _stack.Pop().AsString();
                            _stack.Push(GrobValue.FromString(string.Concat(a, b)), line);
                            break;
                        }
                    case OpCode.BuildString: {
                            // Sprint 3E: concatenate N string fragments from the stack.
                            // The 1-byte operand is the fragment count. Fragments are popped
                            // LIFO so we reverse-fill the parts array to restore source order.
                            // Each fragment is rendered via ValueDisplay.Display — the D-336
                            // display rule for interpolation slots (supersedes D-279's
                            // ToString()-based wording).
                            byte count = _activeChunk.ReadByte(_ip++);
                            var parts = new string[count];
                            for (int i = count - 1; i >= 0; i--) {
                                parts[i] = _valueDisplay.Display(_stack.Pop());
                            }
                            _stack.Push(GrobValue.FromString(string.Concat(parts)), line);
                            break;
                        }

                    // --- Promotion ---
                    case OpCode.IntToFloat: {
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromFloat(a), line);
                            break;
                        }

                    // --- I/O ---
                    case OpCode.Print:
                        _streams.Out.WriteLine(_valueDisplay.Display(_stack.Pop()));
                        break;

                    case OpCode.Exit: {
                            // D-110: exit(n) terminates the script with the given code.
                            // The value on the stack is the int exit code.
                            long code = _stack.Pop().AsInt();
                            throw new GrobExitException(checked((int)code));
                        }

                    // --- Boolean and comparison opcodes (Sprint 4 Increment A) ---

                    case OpCode.Not: {
                            bool v = _stack.Pop().AsBool();
                            _stack.Push(GrobValue.FromBool(!v), line);
                            break;
                        }
                    case OpCode.Equal: {
                            GrobValue b = _stack.Pop();
                            GrobValue a = _stack.Pop();
                            // Language-level '==' uses GrobValue's IEEE 754 operator (NaN != NaN,
                            // +0.0 == -0.0), NOT the collection-friendly Equals where NaN.Equals(NaN)
                            // is true. See GrobValue.operator== and D-315.
                            _stack.Push(GrobValue.FromBool(a == b), line);
                            break;
                        }
                    case OpCode.NotEqual: {
                            GrobValue b = _stack.Pop();
                            GrobValue a = _stack.Pop();
                            _stack.Push(GrobValue.FromBool(a != b), line);
                            break;
                        }
                    case OpCode.LessInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromBool(a < b), line);
                            break;
                        }
                    case OpCode.LessFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromBool(a < b), line);
                            break;
                        }
                    case OpCode.LessString: {
                            string b = _stack.Pop().AsString();
                            string a = _stack.Pop().AsString();
                            _stack.Push(GrobValue.FromBool(string.CompareOrdinal(a, b) < 0), line);
                            break;
                        }
                    case OpCode.LessEqualInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromBool(a <= b), line);
                            break;
                        }
                    case OpCode.LessEqualFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromBool(a <= b), line);
                            break;
                        }
                    case OpCode.GreaterInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromBool(a > b), line);
                            break;
                        }
                    case OpCode.GreaterFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromBool(a > b), line);
                            break;
                        }
                    case OpCode.GreaterString: {
                            string b = _stack.Pop().AsString();
                            string a = _stack.Pop().AsString();
                            _stack.Push(GrobValue.FromBool(string.CompareOrdinal(a, b) > 0), line);
                            break;
                        }
                    case OpCode.GreaterEqualInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            _stack.Push(GrobValue.FromBool(a >= b), line);
                            break;
                        }
                    case OpCode.GreaterEqualFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            _stack.Push(GrobValue.FromBool(a >= b), line);
                            break;
                        }

                    // --- Nil handling (Sprint 3 Increment D) ---

                    case OpCode.IsNil: {
                            // Peeks the top of the stack without popping; pushes a bool.
                            GrobValue top = _stack.Peek();
                            _stack.Push(GrobValue.FromBool(top.IsNil), line);
                            break;
                        }
                    case OpCode.NilCoalesce: {
                            // Eager: both operands already on the stack. Pop right then left;
                            // push left when left is non-nil, right otherwise.
                            GrobValue right = _stack.Pop();
                            GrobValue left = _stack.Pop();
                            _stack.Push(left.IsNil ? right : left, line);
                            break;
                        }

                    // --- Control flow (Sprint 3 Increment D — forward-jump backpatch) ---

                    case OpCode.Jump: {
                            // Unconditional forward jump. The 2-byte big-endian offset counts
                            // bytes from the instruction immediately after the two operand bytes.
                            int hi = _activeChunk.ReadByte(_ip++);
                            int lo = _activeChunk.ReadByte(_ip++);
                            _ip += (hi << 8) | lo;
                            break;
                        }
                    case OpCode.JumpIfFalse: {
                            // Conditional forward jump; pops the bool condition.
                            int hi = _activeChunk.ReadByte(_ip++);
                            int lo = _activeChunk.ReadByte(_ip++);
                            GrobValue cond = _stack.Pop();
                            if (!cond.AsBool())
                                _ip += (hi << 8) | lo;
                            break;
                        }
                    case OpCode.JumpIfTrue: {
                            // Conditional forward jump for OR short-circuit; peeks (does not pop)
                            // so the condition value remains on the stack as the result.
                            int hi = _activeChunk.ReadByte(_ip++);
                            int lo = _activeChunk.ReadByte(_ip++);
                            if (_stack.Peek().AsBool())
                                _ip += (hi << 8) | lo;
                            break;
                        }
                    case OpCode.Loop: {
                            // Unconditional backward jump (Sprint 4 Increment B).
                            // The 2-byte big-endian offset is subtracted from the
                            // instruction pointer after the two operand bytes are read,
                            // landing exactly at the loop-top (condition start).
                            int hi = _activeChunk.ReadByte(_ip++);
                            int lo = _activeChunk.ReadByte(_ip++);
                            _ip -= (hi << 8) | lo;
                            break;
                        }

                    // --- Arrays and maps (Sprint 4 Increment C — for...in iteration surface) ---

                    case OpCode.NewArray: {
                            // 1-byte element count. Pop that many values (LIFO) and
                            // reverse-fill so the array preserves source order.
                            byte count = _activeChunk.ReadByte(_ip++);
                            var elements = new GrobValue[count];
                            for (int i = count - 1; i >= 0; i--) elements[i] = _stack.Pop();
                            _stack.Push(GrobValue.FromArray(new GrobArray(elements)), line);
                            break;
                        }
                    case OpCode.GetIndex: {
                            // array[int] → element (E5101 out of range); map[string] → value
                            // or nil on a miss (map lookup is V?, not a throw).
                            GrobValue index = _stack.Pop();
                            GrobValue receiver = _stack.Pop();
                            if (receiver.TryAsArray(out GrobArray? array)) {
                                long i = index.AsInt();
                                if (i < 0 || i >= array!.Count) {
                                    string message =
                                        $"array index {i} is out of range for an array of length {array!.Count}";
                                    if (!TryRaiseRuntimeGrobError(IndexErrorLeaf, message, line,
                                            boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                        throw new GrobRuntimeException(ErrorCatalog.E5101.Code, line, column, message);
                                    break;
                                }
                                _stack.Push(array[(int)i], line);
                            } else if (receiver.TryAsMap(out GrobMap? map)) {
                                _stack.Push(
                                    map!.TryGetValue(index.AsString(), out GrobValue value) ? value : GrobValue.Nil,
                                    line);
                            } else if (receiver.IsNil) {
                                const string message = "nil dereference: cannot index nil value";
                                if (!TryRaiseRuntimeGrobError(NilErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobRuntimeException(ErrorCatalog.E5201.Code, line, column, message);
                                break;
                            } else {
                                throw new GrobInternalException(
                                    $"GetIndex on receiver of kind {receiver.Kind} is not supported.");
                            }
                            break;
                        }

                    // --- Properties (array.length, map.keys, and Sprint 5C array methods) ---

                    case OpCode.GetProperty: {
                            byte nameIdx = _activeChunk.ReadByte(_ip++);
                            string propertyName = _activeChunk.ReadConstant(nameIdx).AsString();
                            GrobValue receiver = _stack.Pop();
                            // Nil receiver raises E5201 (nil dereference at runtime).
                            if (receiver.IsNil) {
                                const string message = "nil dereference: cannot access member on nil value";
                                if (!TryRaiseRuntimeGrobError(NilErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobRuntimeException(ErrorCatalog.E5201.Code, line, column, message);
                                break;
                            }
                            if (receiver.TryAsArray(out GrobArray? array)) {
                                if (propertyName == "length") {
                                    _stack.Push(GrobValue.FromInt(array!.Count), line);
                                    break;
                                }
                                // Sprint 5C: array higher-order method binding.
                                // Capture the token at property-access time so the bound native
                                // carries the live token through to InvokeCallable.
                                CancellationToken ct = _cancellationToken;
                                var finallyContext = new FinallyContext(
                                    boundedFinally, finallyBoundaryFloor, finallyBoundaryStart);
                                NativeFunction? method = ArrayNatives.GetMethod(
                                    propertyName, array!,
                                    (callable, args) => InvokeCallable(callable, args, line, column, ct, finallyContext));
                                if (method is not null) {
                                    _stack.Push(GrobValue.FromFunction(method), line);
                                    break;
                                }
                            }
                            if (receiver.TryAsMap(out GrobMap? map) && propertyName == "keys") {
                                // No LINQ on the dispatch path: build the keys array with a
                                // manual indexed loop over the live ordered-key view.
                                IReadOnlyList<string> keyView = map!.InsertionOrderKeys;
                                var elements = new GrobValue[keyView.Count];
                                for (int i = 0; i < keyView.Count; i++)
                                    elements[i] = GrobValue.FromString(keyView[i]);
                                _stack.Push(GrobValue.FromArray(new GrobArray(elements)), line);
                                break;
                            }
                            if (receiver.TryAsStruct(out GrobStruct? grobStruct)) {
                                if (grobStruct!.TryGetField(propertyName, out GrobValue fieldValue)) {
                                    _stack.Push(fieldValue, line);
                                    break;
                                }
                                throw new GrobInternalException(
                                    $"GetProperty: struct '{grobStruct.TypeName}' has no field '{propertyName}'. " +
                                    "Type checker should have rejected this before emission.");
                            }
                            throw new GrobInternalException(
                                $"opcode {OpCode.GetProperty} '{propertyName}' on receiver of kind " +
                                $"{receiver.Kind} not yet implemented.");
                        }

                    case OpCode.SetProperty: {
                            byte setNameIdx = _activeChunk.ReadByte(_ip++);
                            string setPropertyName = _activeChunk.ReadConstant(setNameIdx).AsString();
                            GrobValue setValue = _stack.Pop();
                            GrobValue setReceiver = _stack.Pop();
                            if (!setReceiver.TryAsStruct(out GrobStruct? setStruct))
                                throw new GrobInternalException(
                                    $"SetProperty on non-struct receiver of kind {setReceiver.Kind}");
                            if (!setStruct!.TryGetField(setPropertyName, out _))
                                throw new GrobInternalException(
                                    $"SetProperty: struct '{setStruct.TypeName}' has no field '{setPropertyName}'. " +
                                    "Type checker should have rejected this before emission.");
                            setStruct.SetField(setPropertyName, setValue);
                            break;
                        }

                    // --- Calls and returns (Sprint 5 Increment A + C) ---

                    case OpCode.Call: {
                            // The callee was pushed before its arguments, so it sits
                            // argCount slots below the top. Its arguments become the
                            // callee's first locals over a new frame base.
                            int argCount = _activeChunk.ReadByte(_ip++);
                            if (_frameCount == MaxFrames) {
                                const string message = "Stack overflow — maximum call depth (256) exceeded";
                                if (!TryRaiseRuntimeGrobError(RuntimeErrorLeaf, message, line,
                                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                    throw new GrobRuntimeException(ErrorCatalog.E5901.Code, line, column, message);
                                break;
                            }

                            GrobValue calleeValue = _stack.Peek(argCount);
                            if (!calleeValue.TryAsFunction(out GrobFunction? callee))
                                throw new GrobInternalException(
                                    $"Call target is not a function (kind: {calleeValue.Kind}). " +
                                    "The type checker should have rejected this source.");

                            // Sprint 5C: dispatch NativeFunction transparently.
                            if (callee is NativeFunction native) {
                                // Collect args from the stack in call order (bottom to top).
                                var callArgs = new GrobValue[argCount];
                                for (int i = argCount - 1; i >= 0; i--) callArgs[i] = _stack.Pop();
                                _stack.Pop(); // pop the callee value itself

                                // Build the VmInvoker that threads back through this VM instance.
                                CancellationToken ct = _cancellationToken;
                                var finallyContext = new FinallyContext(
                                    boundedFinally, finallyBoundaryFloor, finallyBoundaryStart);
                                VmInvoker invoker = (callable, args) =>
                                    InvokeCallable(callable, args, line, column, ct, finallyContext);

                                // The native-throw seam (D-342): a native signals a domain
                                // error by throwing NativeFaultException rather than
                                // returning a value. Routed through the SAME
                                // TryRaiseRuntimeGrobError handler-table walk every
                                // VM-internal fault site already uses (D-334) — no bespoke
                                // native-error path.
                                GrobValue nativeResult;
                                try {
                                    nativeResult = native.Implementation(callArgs, invoker);
                                } catch (NativeFaultException fault) {
                                    if (!TryRaiseRuntimeGrobError(fault.LeafTypeName, fault.Message, line,
                                            boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                                        throw new GrobRuntimeException(fault.Code, line, column, fault.Message);
                                    break;
                                }
                                _stack.Push(nativeResult, line);
                                break;
                            }

                            // Closure — save caller context with ActiveClosure set.
                            if (callee is Closure closure) {
                                _frames[_frameCount++] = new CallFrame {
                                    ReturnChunk = _activeChunk,
                                    ReturnInstructionPointer = _ip,
                                    ReturnStackBase = _stackBase,
                                    ActiveClosure = closure,
                                    Callee = closure,
                                };
                                _stackBase = _stack.Count - argCount;
                                _activeChunk = closure.Function.Bytecode;
                                _ip = 0;
                                break;
                            }

                            // Plain bytecode function — save caller context (no ActiveClosure).
                            if (callee is not BytecodeFunction fn)
                                throw new GrobInternalException(
                                    $"Call target is an unknown GrobFunction subtype: {callee!.GetType().Name}.");

                            // Save the caller's resume context, then switch the
                            // dispatch loop into the callee's chunk.
                            _frames[_frameCount++] = new CallFrame {
                                ReturnChunk = _activeChunk,
                                ReturnInstructionPointer = _ip,
                                ReturnStackBase = _stackBase,
                                Callee = fn,
                            };
                            _stackBase = _stack.Count - argCount;
                            _activeChunk = fn.Bytecode;
                            _ip = 0;
                            break;
                        }

                    case OpCode.Return: {
                            // Top-level mode: the script body runs at frame 0 and is not a
                            // call frame.  Its terminal Return executes with _frameCount
                            // already 0 (nothing to pop) and ends the dispatch.  A function
                            // call that returns to frame 0 pops a frame (handled below) and
                            // the script continues — so this guard is top-level only.
                            if (!isReentrant && !boundedFinally && _frameCount == 0) {
                                // Top-level code has finished; every top-level binding is now
                                // Initialised. Subsequent global reads skip the tag check
                                // (§19.1, D-294). A bounded finally run at frame 0 never ends
                                // here — it ends at its own closing TryEnd — so it is excluded.
                                _startupComplete = true;
                                return;
                            }

                            GrobValue result = _stack.Pop();
                            // Close any open upvalues that point into this frame's slots
                            // before the slots are discarded. This copies their values
                            // from the stack to the heap (open → closed transition).
                            CloseUpvaluesFrom(_stackBase);
#if DEBUG
                            // D-325 post-return invariant: after the sweep, no open upvalue
                            // may reference a slot at or above the returning frame's base.
                            // A violation here means CloseUpvaluesFrom missed an upvalue —
                            // converting that category of bug from a late underflow into an
                            // immediate, located failure.
                            {
                                int frameBase = _stackBase;
                                foreach (Upvalue uv in _openUpvalues)
                                    if (uv.SlotIndex >= frameBase)
                                        throw new GrobInternalException(
                                            $"Post-return upvalue invariant violated (D-325): " +
                                            $"open upvalue at slot {uv.SlotIndex} is at or above " +
                                            $"the returning frame base {frameBase}.");
                            }
#endif
                            // Discard the callee value, its arguments and its locals in
                            // one step (everything from the callee value upward).
                            _stack.TrimToCount(_stackBase - 1);
                            _stack.Push(result, line);

                            // Restore the caller's execution context.
                            CallFrame frame = _frames[--_frameCount];
                            _activeChunk = frame.ReturnChunk;
                            _ip = frame.ReturnInstructionPointer;
                            _stackBase = frame.ReturnStackBase;

                            // Re-entrant mode: when the callee frame pops back to the floor,
                            // the InvokeCallable callee has returned — the result is on the
                            // stack and caller state is restored, so end this dispatch.
                            if (isReentrant && _frameCount == floorFrameCount)
                                return;
                            break;
                        }

                    // --- Upvalue / Closure opcodes (Sprint 5 Increment D) ---

                    case OpCode.Closure: {
                            // Variable-length encoding:
                            //   Closure <fnPoolIdx:1> (<isLocal:1> <index:1>) × fn.UpvalueCount
                            byte fnIdx = _activeChunk.ReadByte(_ip++);
                            var fn = (BytecodeFunction)_activeChunk.ReadConstant(fnIdx).AsFunction();
                            var upvalues = new Upvalue[fn.UpvalueCount];
                            for (int i = 0; i < fn.UpvalueCount; i++) {
                                byte isLocal = _activeChunk.ReadByte(_ip++);
                                byte uvIdx = _activeChunk.ReadByte(_ip++);
                                if (isLocal != 0) {
                                    // Capture a local of the current frame (absolute slot = _stackBase + uvIdx).
                                    upvalues[i] = CaptureUpvalue(_stackBase + uvIdx);
                                } else {
                                    // Transitive capture: copy the enclosing closure's upvalue at uvIdx.
                                    Closure? enclosing = _frameCount > 0
                                        ? _frames[_frameCount - 1].ActiveClosure
                                        : null;
                                    if (enclosing is null)
                                        throw new GrobInternalException(
                                            "Closure opcode with isLocal=0 executed outside a closure frame — " +
                                            "transitive capture requires an enclosing closure context.");
                                    upvalues[i] = enclosing.Upvalues[uvIdx];
                                }
                            }
                            _stack.Push(GrobValue.FromFunction(new Closure(fn, upvalues)), line);
                            break;
                        }

                    case OpCode.GetUpvalue: {
                            byte slot = _activeChunk.ReadByte(_ip++);
                            _stack.Push(GetActiveUpvalues()[slot].Read(), line);
                            break;
                        }

                    case OpCode.SetUpvalue: {
                            byte slot = _activeChunk.ReadByte(_ip++);
                            GetActiveUpvalues()[slot].Write(_stack.Pop());
                            break;
                        }

                    case OpCode.CloseUpvalue:
                        // Close the upvalue tracking the top-of-stack slot, then pop.
                        // Used when a captured local exits its block scope before the
                        // enclosing function returns.
                        CloseUpvaluesFrom(_stack.Count - 1);
                        _stack.Pop();
                        break;

                    // --- Named struct construction (§10, Sprint 6B) ---

                    case OpCode.NewStruct: {
                            byte typeIndex = _activeChunk.ReadByte(_ip++);
                            StructTypeDescriptor descriptor = _activeChunk.GetStructType(typeIndex);
                            int fieldCount = descriptor.FieldNames.Count;
                            var fieldPairs = new KeyValuePair<string, GrobValue>[fieldCount];
                            for (int i = fieldCount - 1; i >= 0; i--)
                                fieldPairs[i] = new KeyValuePair<string, GrobValue>(descriptor.FieldNames[i], _stack.Pop());
                            _stack.Push(GrobValue.FromStruct(new GrobStruct(descriptor.TypeName, fieldPairs)), line);
                            break;
                        }

                    // --- Anonymous struct construction (§10, Sprint 6D) ---
                    // Stack layout before this opcode (bottom→top):
                    //   name₁, val₁, name₂, val₂, …, nameₙ, valₙ
                    // Operand: field count (byte). Pop pairs in LIFO order, then push
                    // a GrobStruct whose TypeName is the sorted field-signature string
                    // (matching the compile-time structural type identity).

                    case OpCode.NewAnonStruct: {
                            byte fieldCount = _activeChunk.ReadByte(_ip++);
                            var fieldPairs = new KeyValuePair<string, GrobValue>[fieldCount];
                            for (int i = fieldCount - 1; i >= 0; i--) {
                                GrobValue value = _stack.Pop();
                                string name = _stack.Pop().AsString();
                                fieldPairs[i] = new KeyValuePair<string, GrobValue>(name, value);
                            }
                            // The sorted signature string is the structural type
                            // identity (Sprint 6D) — two anonymous structs with the same
                            // fields in any order share it and so compare equal. Field
                            // order for display is carried separately by GrobStruct.Fields,
                            // which preserves the source order of fieldPairs (D-336).
                            string typeName = fieldCount == 0
                                ? "<anon>"
                                : string.Join(",",
                                    fieldPairs
                                        .OrderBy(p => p.Key, StringComparer.Ordinal)
                                        .Select(p => $"{p.Key}:{p.Value.Kind}"));
                            _stack.Push(GrobValue.FromStruct(new GrobStruct(typeName, fieldPairs, isAnonymous: true)), line);
                            break;
                        }

                    // --- try / catch / throw (Sprint 7 Increment B; A: throw + top-level) ---

                    // Structural markers only — the handler table (built by the
                    // compiler, read by the Throw arm below via (chunk, ip)) carries
                    // all the matching logic. Neither opcode does anything at runtime
                    // beyond stepping past its own bytes.
                    case OpCode.TryBegin:
                        // Track nested-try depth so a bounded finally run does not stop at a
                        // nested try's TryEnd (only at its own region's closing TryEnd).
                        if (boundedFinally) finallyDepth++;
                        _ip++; // 1-byte operand: handler-table index, unused here.
                        break;

                    case OpCode.TryEnd:
                        if (boundedFinally) {
                            // finallyDepth 0 means this is the bounded finally body's own
                            // closing TryEnd — the body is done, hand control back to the walk.
                            if (finallyDepth == 0) return;
                            finallyDepth--;
                        }
                        break;

                    case OpCode.Throw: {
                            GrobValue exceptionValue = _stack.Pop();
                            if (!exceptionValue.TryAsStruct(out GrobStruct? exceptionStruct))
                                throw new GrobInternalException(
                                    $"Throw operand is not a struct (kind {exceptionValue.Kind}). " +
                                    "The type checker should have rejected a non-GrobError throw operand before emission.");

                            // D-322-style line-only stamp — the VM has no source-file
                            // identifier (Chunk debug info is line-keyed only; the file name
                            // lives only at the CLI layer). "<unknown>" fills the file slot
                            // exactly as SourceLocation.Unknown already does elsewhere in the
                            // compiler. This is the one and only place 'location' is ever set;
                            // a catch handler reads it back already correct.
                            exceptionStruct!.SetField("location", GrobValue.FromString($"<unknown>:{line}"));

                            // Walk protected regions from the innermost enclosing frame
                            // outward (D-274), running the finally of every finally-bearing
                            // region passed over without a match, innermost first (D-275).
                            // When this throw itself occurs inside a bounded finally body,
                            // the boundary confines the walk so an uncaught throw escapes to
                            // the enclosing driver instead of propagating past the finally.
                            GrobStruct thrown = exceptionStruct;
                            bool handled = boundedFinally
                                ? PropagateThrow(ref exceptionValue, ref thrown, line, finallyBoundaryFloor, finallyBoundaryStart)
                                : PropagateThrow(ref exceptionValue, ref thrown, line, -1, -1);
                            if (handled) break;

                            string messageText = thrown.TryGetField("message", out GrobValue msgValue) && msgValue.IsString
                                ? msgValue.AsString()
                                : "<no message>";
                            throw new GrobRuntimeException(ErrorCatalog.E5904.Code, line, column,
                                $"{thrown.TypeName}: {messageText}");
                        }

                    default:
                        throw new GrobInternalException(
                            $"opcode {(OpCode)instruction} not yet implemented (Sprint 3+)");
                }
            } catch (OverflowException) {
                // Any checked(...) int op that overflows surfaces as E5001 carrying
                // the failing line — routed (Sprint 7 Increment D) through the same
                // handler-table walk a user throw uses; falls back to the pre-routing
                // diagnostic unchanged when nothing catches it.
                const string message = "integer overflow";
                if (!TryRaiseRuntimeGrobError(ArithmeticErrorLeaf, message, line,
                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                    throw new GrobArithmeticException(ErrorCatalog.E5001.Code, line, column, message);
            } catch (GrobRuntimeException ex) when (ex.Code == ErrorCatalog.E5903.Code) {
                // ValueStack.Push raises E5903 from dozens of call sites across the
                // switch — intercepted here rather than at each one. The original
                // exception already carries the exact code/line/message the unhandled
                // diagnostic needs, so the fallback rethrows it as-is (no reconstruction,
                // unlike OverflowException which carries no Grob-specific data).
                if (!TryRaiseRuntimeGrobError(RuntimeErrorLeaf, ex.Message, ex.Line,
                        boundedFinally, finallyBoundaryFloor, finallyBoundaryStart))
                    throw;
            } catch (RoutedThrowHandledException) {
                // Raised by InvokeCallable (Sprint 7 Increment D) when its own
                // frame-depth check is caught: PropagateThrow already moved
                // _ip/_stackBase/the value stack to the matching handler, so there is
                // nothing further to do here beyond unwinding back to this switch.
            }
        }
        // Note: OperationCanceledException from ThrowIfCancellationRequested() is NOT
        // caught here — it propagates to the caller of Run() as required by D-319.
    }

    // -----------------------------------------------------------------------
    // Re-entrant call-back bridge (D-319 load-bearing sub-problem)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The enclosing <see cref="RunDispatch"/> invocation's bounded-finally state
    /// (Sprint 7 Increment D review fix), bundled so <see cref="InvokeCallable"/> can
    /// thread it through to <see cref="TryRaiseRuntimeGrobError"/> without exceeding
    /// SonarCloud's S107 parameter-count bar. The three fields are always passed
    /// together — they are <see cref="RunDispatch"/>'s own three parameters of the
    /// same names, read at the call site and carried unchanged.
    /// </summary>
    private readonly record struct FinallyContext(bool Bounded, int BoundaryFloor, int BoundaryStart);

    /// <summary>
    /// Invoke a Grob callable (typically a lambda argument received by a
    /// <see cref="NativeFunction"/>) and return its result.  This is the
    /// re-entrant bridge: it saves the current dispatch state to a call frame,
    /// switches <see cref="RunDispatch"/> into the callee's chunk, and returns
    /// to the native only after the callee has returned.
    ///
    /// The step counter (<see cref="_steps"/>) is NOT reset, so the D-319
    /// cancellation budget is continuous across the bridge — a runaway lambda
    /// invoked by a native is caught by the same token as a runaway top-level
    /// loop.
    /// </summary>
    private GrobValue InvokeCallable(
            GrobValue callable, GrobValue[] args,
            int line, int column, CancellationToken ct,
            FinallyContext finallyContext) {
        if (!callable.TryAsFunction(out GrobFunction? fn))
            throw new GrobInternalException(
                $"InvokeCallable: value of kind {callable.Kind} is not callable. " +
                "The type checker should have rejected the call site.");

        // Nested native: call the C# delegate directly without entering bytecode dispatch.
        if (fn is NativeFunction nativeFn) {
            VmInvoker nestedInvoker = (c, a) => InvokeCallable(c, a, line, column, ct, finallyContext);
            return nativeFn.Implementation(args, nestedInvoker);
        }

        // Resolve Closure to its underlying BytecodeFunction for dispatch.
        Closure? activeClosure = null;
        BytecodeFunction bf;
        if (fn is Closure c) {
            activeClosure = c;
            bf = c.Function;
        } else if (fn is BytecodeFunction plain) {
            bf = plain;
        } else {
            throw new GrobInternalException(
                $"InvokeCallable: unknown GrobFunction subtype {fn!.GetType().Name}.");
        }

        if (_frameCount == MaxFrames) {
            const string message = "Stack overflow — maximum call depth (256) exceeded";
            if (!TryRaiseRuntimeGrobError(RuntimeErrorLeaf, message, line,
                    finallyContext.Bounded, finallyContext.BoundaryFloor, finallyContext.BoundaryStart))
                throw new GrobRuntimeException(ErrorCatalog.E5901.Code, line, column, message);

            // Handled: PropagateThrow has already moved _ip/_stackBase/the value stack
            // to the matching handler — but that handler lives in the enclosing
            // RunDispatch's own switch, several real C# frames below this one (through
            // the native's own loop, e.g. array.each, and this method). Returning a
            // GrobValue here would let that native's loop keep running past the point
            // the try/catch already aborted it at — wrong for anything but a
            // single-element receiver. Unwind those C# frames with the same signal
            // shape FinallyEscapeException already uses, caught once at the switch.
            throw new RoutedThrowHandledException();
        }

        // Remember the frame depth before pushing; RunDispatch stops when the callee
        // frame pops back to this floor.
        int floorFrameCount = _frameCount;

        // Push the callee and its arguments so Return can clean them up normally.
        _stack.Push(callable, line);
        foreach (GrobValue arg in args)
            _stack.Push(arg, line);

        // Save current dispatch state to the frame array; include ActiveClosure when calling
        // a Closure so GetUpvalue/SetUpvalue can reach the upvalue array. Carry the callee
        // so an E5902 raised through this re-entrant native bridge still names the function
        // (§19.1, D-321), exactly as the direct Call arms do.
        _frames[_frameCount++] = new CallFrame {
            ReturnChunk = _activeChunk,
            ReturnInstructionPointer = _ip,
            ReturnStackBase = _stackBase,
            ActiveClosure = activeClosure,
            Callee = fn,
        };
        _stackBase = _stack.Count - args.Length;
        _activeChunk = bf.Bytecode;
        _ip = 0;

        // The previous _cancellationToken is already set (same token for the whole Run()).
        // Update in case InvokeCallable is called with a different ct (defensive).
        _cancellationToken = ct;

        // Run the callee to completion.  On the callee's Return back to the floor,
        // RunDispatch restores _activeChunk/_ip/_stackBase from the frame and returns.
        RunDispatch(floorFrameCount, isReentrant: true);

        // The Return arm already pushed the result and restored caller state.
        return _stack.Pop();
    }

    /// <summary>
    /// Composes the circular-initialisation diagnostic (E5902, §19.1, D-321),
    /// tracing through the function: the top-level binding whose initialiser is
    /// running, the function that performed the read and the binding
    /// <paramref name="readName"/> read before its declaration had executed — each
    /// with its source line. Called only on the rare E5902 path, so it favours
    /// clarity over speed.
    /// </summary>
    private string ComposeCircularInitMessage(string readName) {
        int readLine = DeclarationLineOf(readName);

        // The binding being initialised is the earliest top-level binding not yet
        // Initialised: hoisted functions and already-run bindings are Initialised,
        // and a read-before-declaration can only target a binding declared later, so
        // the first non-Initialised entry in emission order is the current initialiser.
        (string Name, int Line)? initialising = null;
        foreach ((string Name, int Line) binding in _topLevelBindings) {
            if (!_globalStates.TryGetValue(binding.Name, out SlotState s) || s != SlotState.Initialised) {
                initialising = binding;
                break;
            }
        }

        // The function that performed the read is the call target of the active frame.
        // A read directly in a top-level initialiser (no enclosing call) has none.
        string? functionName = _frameCount > 0 ? _frames[_frameCount - 1].Callee?.Name : null;

        if (initialising is not (string initName, int initLine))
            // Defensive: the read binding is itself the only candidate.
            (initName, initLine) = (readName, readLine);

        string readClause = $"top-level binding '{readName}' (line {readLine})";
        if (functionName is not { Length: > 0 })
            return $"While initialising top-level binding '{initName}' (line {initLine}), "
                 + $"{readClause} was read before its declaration had executed.";

        // A top-level function is itself a hoisted binding, so its declaration line is
        // recorded in the pre-scan; include it when known to match the §19.1 template.
        int fnLine = DeclarationLineOf(functionName);
        string fnClause = fnLine > 0 ? $"function '{functionName}' (line {fnLine})" : $"function '{functionName}'";
        return $"While initialising top-level binding '{initName}' (line {initLine}), "
             + $"{fnClause} read {readClause} before its declaration had executed.";
    }

    /// <summary>
    /// Returns the source line of <paramref name="name"/>'s top-level declaration as
    /// recorded by the pre-scan, or 0 when the name carries no recorded binding.
    /// </summary>
    private int DeclarationLineOf(string name) {
        foreach ((string Name, int Line) binding in _topLevelBindings) {
            if (binding.Name == name) return binding.Line;
        }
        return 0;
    }

    // -----------------------------------------------------------------------
    // Top-level initialisation pre-scan (Sprint 5 Increment E, §19.1 / D-294).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks the top-level chunk and tags every binding it defines (each
    /// <see cref="OpCode.DefineGlobal"/>) as <see cref="SlotState.Uninitialised"/>.
    /// This lets <see cref="OpCode.GetGlobal"/> tell a read of a binding declared
    /// later in the same source (circular initialisation, E5902) from a read of a
    /// name that is never defined (E1001). Only the flat top-level instruction
    /// stream is scanned — function bodies live in the constant pool and define no
    /// top-level bindings of their own. The scan never throws; it is a best-effort
    /// pre-pass over already-validated bytecode.
    /// </summary>
    private void ScanTopLevelGlobals(Chunk chunk) {
        int ip = 0;
        while (ip < chunk.Count) {
            byte raw = chunk.ReadByte(ip);
            if (!Enum.IsDefined(typeof(OpCode), raw)) {
                ip++;
                continue;
            }

            switch ((OpCode)raw) {
                case OpCode.DefineGlobal:
                    if (ip + 1 < chunk.Count) {
                        byte nameIdx = chunk.ReadByte(ip + 1);
                        if (nameIdx < chunk.ConstantCount) {
                            string name = chunk.ReadConstant(nameIdx).AsString();
                            _globalStates[name] = SlotState.Uninitialised;
                            // Record the binding in emission order with its source line,
                            // for the E5902 trace (§19.1, D-321).
                            _topLevelBindings.Add((name, chunk.GetLine(ip)));
                        }
                    }
                    ip += 2;
                    break;

                case OpCode.Closure: {
                        // Variable-length: pool-index byte + (isLocal, index) pair per upvalue.
                        int advance = 2;
                        if (ip + 1 < chunk.Count) {
                            byte fnIdx = chunk.ReadByte(ip + 1);
                            if (fnIdx < chunk.ConstantCount
                                && chunk.ReadConstant(fnIdx).TryAsFunction(out GrobFunction? gf)
                                && gf is BytecodeFunction fn)
                                advance += fn.UpvalueCount * 2;
                        }
                        ip += advance;
                        break;
                    }

                // Two-byte operand (constant-long index or jump offset).
                case OpCode.ConstantLong:
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    ip += 3;
                    break;

                // One-byte operand.
                case OpCode.Constant:
                case OpCode.PopN:
                case OpCode.NewArray:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.NewStruct:
                case OpCode.NewAnonStruct:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetUpvalue:
                case OpCode.SetUpvalue:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                case OpCode.Import:
                case OpCode.TryBegin:
                case OpCode.IncrementInt:
                case OpCode.DecrementInt:
                case OpCode.IncrementFloat:
                case OpCode.DecrementFloat:
                    ip += 2;
                    break;

                // No-operand opcodes.
                default:
                    ip += 1;
                    break;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Upvalue helpers (Sprint 5 Increment D).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns an open <see cref="Upvalue"/> that tracks absolute stack slot
    /// <paramref name="absoluteSlot"/>. If one already exists in the open-upvalue
    /// list for that slot (because another closure in the same enclosing frame
    /// already captured the same variable), the existing object is reused —
    /// deduplication is what lets two closures share mutations through one cell.
    /// </summary>
    private Upvalue CaptureUpvalue(int absoluteSlot) {
        foreach (Upvalue uv in _openUpvalues)
            if (uv.SlotIndex == absoluteSlot) return uv;
        var fresh = new Upvalue(_stack, absoluteSlot);
        _openUpvalues.Add(fresh);
        return fresh;
    }

    /// <summary>
    /// Closes every open upvalue whose stack slot is ≥ <paramref name="fromSlot"/>
    /// by copying the value from the stack to the heap cell and removing the entry
    /// from the open-upvalue list. Called in the <see cref="OpCode.Return"/> path
    /// before the frame's slots are discarded, and by <see cref="OpCode.CloseUpvalue"/>
    /// when a captured local exits its block scope mid-function.
    /// </summary>
    private void CloseUpvaluesFrom(int fromSlot) {
        for (int i = _openUpvalues.Count - 1; i >= 0; i--) {
            if (_openUpvalues[i].SlotIndex >= fromSlot) {
                _openUpvalues[i].Close();
                _openUpvalues.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Routes a VM-detected runtime fault (Sprint 7 Increment D) through the same
    /// handler-table walk <see cref="OpCode.Throw"/> uses, instead of halting the VM
    /// directly. Builds the <paramref name="leafTypeName"/> <c>GrobError</c> leaf as a
    /// <see cref="GrobStruct"/> — the same shape a user-authored <c>throw</c> constructs
    /// via <c>NewStruct</c> — stamps <c>location</c> the same way <see cref="OpCode.Throw"/>
    /// does, and drives it through <see cref="PropagateThrow"/>. Returns <see langword="true"/>
    /// when a matching handler was found (the VM has already jumped to it, so the caller
    /// should resume dispatch normally rather than throw); <see langword="false"/> when
    /// unhandled, so the caller raises the pre-existing top-level diagnostic unchanged —
    /// routing does not re-diagnose, so no <c>E5904</c> here, unlike an unhandled user throw.
    /// </summary>
    /// <param name="leafTypeName">The <c>GrobError</c> leaf to construct (e.g. <c>"ArithmeticError"</c>).</param>
    /// <param name="message">The fault message, stored on the constructed struct's <c>message</c> field.</param>
    /// <param name="line">The failing instruction's source line, stamped into <c>location</c>.</param>
    /// <param name="boundedFinally">
    /// <see langword="true"/> when the calling <see cref="RunDispatch"/> invocation is
    /// itself running a <c>finally</c> body on the exceptional-unwind path — the fault
    /// site's own three <see cref="RunDispatch"/> parameters, passed straight through so
    /// this call mirrors <see cref="OpCode.Throw"/>'s identical branch exactly (D-275). A
    /// fault occurring inside a finally body must confine its search to that body and, if
    /// unhandled there, raise <see cref="FinallyEscapeException"/> to replace the in-flight
    /// exception — never search past the finally's boundary directly, which would let this
    /// (still-executing, bounded) dispatch loop run arbitrary outer code with real side
    /// effects before its own state is discarded on return.
    /// </param>
    /// <param name="finallyBoundaryFloor">Passed straight through to <see cref="PropagateThrow"/> when
    /// <paramref name="boundedFinally"/> is <see langword="true"/>; ignored otherwise.</param>
    /// <param name="finallyBoundaryStart">Passed straight through to <see cref="PropagateThrow"/> when
    /// <paramref name="boundedFinally"/> is <see langword="true"/>; ignored otherwise.</param>
    private bool TryRaiseRuntimeGrobError(
            string leafTypeName, string message, int line,
            bool boundedFinally, int finallyBoundaryFloor, int finallyBoundaryStart) {
        var errStruct = new GrobStruct(leafTypeName,
            [new KeyValuePair<string, GrobValue>("message", GrobValue.FromString(message))]);
        errStruct.SetField("location", GrobValue.FromString($"<unknown>:{line}"));
        GrobValue exceptionValue = GrobValue.FromStruct(errStruct);
        GrobStruct thrown = errStruct;
        return boundedFinally
            ? PropagateThrow(ref exceptionValue, ref thrown, line, finallyBoundaryFloor, finallyBoundaryStart)
            : PropagateThrow(ref exceptionValue, ref thrown, line, -1, -1);
    }

    /// <summary>
    /// Drives the outward exceptional unwind for a thrown exception (Sprint 7 Increment C,
    /// D-275, extending the Increment B nearest-handler walk). Walks protected regions from
    /// the throw point outward, innermost first; for every finally-bearing region passed over
    /// without a matching handler — including one whose own catch body threw — it runs that
    /// region's finally exactly once, before continuing outward. A finally that itself throws
    /// replaces the in-flight <paramref name="exceptionValue"/>/<paramref name="exceptionStruct"/>
    /// (D-275) and the walk resumes from the next-outer region.
    /// </summary>
    /// <remarks>
    /// The construct-containment upper bound is a region's <see cref="TryRegion.FinallyOffset"/>
    /// when it has a finally — so the try body and every catch body count as "within the
    /// construct" for running the finally — else its catch-matching <see cref="TryRegion.EndOffset"/>.
    /// Handler matching still uses the inclusive <c>ip &lt;= EndOffset</c> bound (a throw from a
    /// catch body, <c>ip &gt; EndOffset</c>, cannot be re-caught by its own region but still runs
    /// its finally). The throw <c>ip</c> is a post-fetch position (as in Increment B).
    /// <para>
    /// Returns <see langword="true"/> when a matching handler is found (<see cref="_ip"/> and the
    /// value stack are set to enter it) and <see langword="false"/> when every frame is exhausted
    /// (the caller raises E5904). When <paramref name="boundaryFloor"/> ≥ 0 the walk is confined
    /// to a bounded finally body: at that frame only regions inside the body
    /// (start ≥ <paramref name="boundaryStart"/>) are eligible, and an uncaught throw raises
    /// <see cref="FinallyEscapeException"/> so the enclosing driver replaces its in-flight exception.
    /// </para>
    /// </remarks>
    private bool PropagateThrow(
            ref GrobValue exceptionValue, ref GrobStruct exceptionStruct, int line,
            int boundaryFloor, int boundaryStart) {
        int ip = _ip;
        while (true) {
            // At the bounded-finally boundary frame, ignore regions below the finally body.
            int lowerStart = boundaryFloor >= 0 && _frameCount == boundaryFloor ? boundaryStart : -1;

            int cursorStart = int.MaxValue;
            while (true) {
                TryRegion? region = InnermostRegionBelow(_activeChunk, ip, cursorStart, lowerStart);
                if (region is null) break;
                cursorStart = region.StartOffset;

                // A handler matches only when the throw is in the try body (ip <= EndOffset);
                // a throw from a catch body cannot be re-caught by the same region (D-274).
                if (ip <= region.EndOffset) {
                    foreach (CatchHandler h in region.Handlers) {
                        if (h.IsCatchAll || h.MatchTypeNames.Contains(exceptionStruct.TypeName)) {
                            int bindingSlot = _stackBase + h.BindingSlot;
                            CloseUpvaluesFrom(bindingSlot);
                            _stack.TrimToCount(bindingSlot);
                            _stack.Push(exceptionValue, line);
                            _ip = h.HandlerOffset;
                            return true;
                        }
                    }
                }

                // No handler here — run this region's finally before moving outward. If it
                // throws, the in-flight exception is replaced and the walk simply continues
                // from the next-outer region (cursorStart has already advanced past it).
                if (region.FinallyOffset >= 0)
                    RunFinallyExceptional(region, ref exceptionValue, ref exceptionStruct);
            }

            // No handler in this chunk. A bounded finally body's uncaught throw escapes to
            // its enclosing driver rather than propagating past the finally.
            if (boundaryFloor >= 0 && _frameCount == boundaryFloor)
                throw new FinallyEscapeException(exceptionValue, exceptionStruct);

            if (_frameCount == 0) return false;

            // Unwind one frame (closing its upvalues, D-325) and continue in the caller.
            CloseUpvaluesFrom(_stackBase);
            CallFrame frame = _frames[--_frameCount];
            _activeChunk = frame.ReturnChunk;
            _ip = frame.ReturnInstructionPointer;
            _stackBase = frame.ReturnStackBase;
            ip = _ip;
        }
    }

    /// <summary>
    /// Runs <paramref name="region"/>'s finally body on the exceptional unwind path, in the
    /// current frame (unchanged <see cref="_stackBase"/>, so the finally reads the enclosing
    /// function's locals directly — matching the compiler's inline copies). The body runs via
    /// a bounded <see cref="RunDispatch"/> that stops at the region's own closing
    /// <see cref="OpCode.TryEnd"/>. When the finally throws and the throw escapes its own body,
    /// the in-flight exception is replaced with the new one via the <see langword="ref"/>
    /// parameters (D-275) — the caller does not need to know whether that happened, only that
    /// <paramref name="exceptionValue"/>/<paramref name="exceptionStruct"/> are current on
    /// return, so this method reports nothing further. The escaping walk has already unwound
    /// back to this frame by the time the escape is caught.
    /// </summary>
    private void RunFinallyExceptional(
            TryRegion region, ref GrobValue exceptionValue, ref GrobStruct exceptionStruct) {
        int savedIp = _ip;
        Chunk savedChunk = _activeChunk;
        int floor = _frameCount;
        _ip = region.FinallyOffset;
        try {
            RunDispatch(floorFrameCount: floor, isReentrant: false,
                boundedFinally: true, finallyBoundaryStart: region.FinallyOffset, finallyBoundaryFloor: floor);
        } catch (FinallyEscapeException escape) {
            exceptionValue = escape.Value;
            exceptionStruct = escape.ExceptionStruct;
        } finally {
            _ip = savedIp;
            _activeChunk = savedChunk;
        }
    }

    /// <summary>
    /// Returns the innermost protected region of <paramref name="chunk"/> that contains
    /// <paramref name="ip"/> and whose start offset is strictly below
    /// <paramref name="cursorStart"/> (and, when <paramref name="lowerStart"/> ≥ 0, at or
    /// above it — the bounded-finally floor), or null when none remain. Containment upper
    /// bound is the finally offset when the region has a finally, else the catch-matching end
    /// offset. Ordered by descending start offset so the caller walks innermost to outermost.
    /// </summary>
    private static TryRegion? InnermostRegionBelow(Chunk chunk, int ip, int cursorStart, int lowerStart) {
        TryRegion? best = null;
        for (int i = 0; i < chunk.TryRegionCount; i++) {
            TryRegion r = chunk.GetTryRegion(i);
            int upper = r.FinallyOffset >= 0 ? r.FinallyOffset : r.EndOffset;
            if (r.StartOffset > ip || ip > upper) continue;              // not within the construct
            if (r.StartOffset >= cursorStart) continue;                  // not strictly inner to cursor
            if (lowerStart >= 0 && r.StartOffset < lowerStart) continue; // below the finally boundary
            if (best is null || r.StartOffset > best.StartOffset) best = r;
        }
        return best;
    }

    /// <summary>
    /// Internal control-flow signal raised when a finally body running on the exceptional
    /// unwind path throws an exception it does not catch itself. Carries the replacement
    /// exception back to the enclosing <see cref="PropagateThrow"/> driver (D-275). Never
    /// leaves <see cref="RunFinallyExceptional"/> — deliberately <see langword="private"/>,
    /// the same pattern as <c>Parser.ParseFailedException</c>: a same-class-only control-flow
    /// signal, not a reportable error, so it stays out of the public API surface.
    /// </summary>
    private sealed class FinallyEscapeException : Exception {
        public GrobValue Value { get; }
        public GrobStruct ExceptionStruct { get; }

        public FinallyEscapeException(GrobValue value, GrobStruct exceptionStruct) {
            Value = value;
            ExceptionStruct = exceptionStruct;
        }
    }

    /// <summary>
    /// Internal control-flow signal raised by <see cref="InvokeCallable"/> (Sprint 7
    /// Increment D) when its own frame-depth check (<see cref="ErrorCatalog.E5901"/>)
    /// is caught by a Grob <c>try</c>/<c>catch</c>. <see cref="PropagateThrow"/> has
    /// already moved <c>_ip</c>/<c>_stackBase</c>/the value stack to the matching
    /// handler by the time this is thrown; it carries no payload, only unwinds the
    /// real C# frames between the throw site (inside a native's own call-back loop,
    /// e.g. <c>array.each</c>) and the switch statement of the <c>RunDispatch</c> that
    /// will resume from the handler. Never leaves that switch's own catch — the same
    /// same-class-only pattern as <see cref="FinallyEscapeException"/>.
    /// </summary>
    private sealed class RoutedThrowHandledException : Exception;

    /// <summary>
    /// Returns the upvalue array of the closure currently executing in the topmost
    /// call frame. Throws <see cref="GrobInternalException"/> when called with no
    /// active closure — GetUpvalue/SetUpvalue are only emitted inside lambda bodies
    /// that compile with at least one capture, so reaching here without a closure
    /// indicates a compiler bug.
    /// </summary>
    private Upvalue[] GetActiveUpvalues() {
        Closure? active = _frameCount > 0 ? _frames[_frameCount - 1].ActiveClosure : null;
        return active?.Upvalues
            ?? throw new GrobInternalException(
                "GetUpvalue/SetUpvalue executed outside a closure frame — " +
                "these opcodes are only emitted inside capturing lambda bodies.");
    }

#if DEBUG
    /// <summary>
    /// D-306 per-instruction trace: renders the value stack and the
    /// about-to-execute instruction every iteration of the dispatch loop.
    /// Compiled into Debug builds only — entirely absent in Release so that
    /// D-302 benchmarks measure a branch-free dispatch path.
    /// </summary>
    private void TraceInstruction(Chunk chunk, int ip) {
        _trace.Write("          ");
        var span = _stack.AsSpan();
        for (int i = 0; i < span.Length; i++) {
            _trace.Write("[ ");
            _trace.Write(span[i].ToString());
            _trace.Write(" ]");
        }
        _trace.WriteLine();
        Disassembler.DisassembleInstruction(chunk, ip, _trace);
    }
#endif
}
