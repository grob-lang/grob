using System.Threading;
using Grob.Core;

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
public sealed class VirtualMachine {
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

    private readonly ValueStack _stack = new();
    private readonly TextWriter _out;
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
    /// Construct a VM whose <see cref="OpCode.Print"/> output goes to
    /// <paramref name="output"/>. <paramref name="trace"/> receives the
    /// <c>#if DEBUG</c> per-instruction trace (defaults to
    /// <see cref="TextWriter.Null"/>, which is also the only meaningful value
    /// in Release where the trace call is compiled out entirely).
    /// </summary>
    public VirtualMachine(TextWriter output, TextWriter? trace = null) {
        ArgumentNullException.ThrowIfNull(output);
        _out = output;
        _trace = trace ?? TextWriter.Null;
    }

    /// <summary>The operand stack, exposed for tests to inspect post-run state.</summary>
    public ValueStack Stack => _stack;

    /// <summary>The global variables table, exposed for tests to inspect post-run state.</summary>
    public IReadOnlyDictionary<string, GrobValue> Globals => _globals;

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
    private void RunDispatch(int floorFrameCount, bool isReentrant) {
        int line = 0;
        int column = 0;

        try {
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
                            if (b == 0L)
                                throw new GrobArithmeticException(ErrorCatalog.E5002.Code, line, column, "integer division by zero");
                            // long.MinValue / -1 overflows: caught below as E5001.
                            _stack.Push(GrobValue.FromInt(checked(a / b)), line);
                            break;
                        }
                    case OpCode.ModuloInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            if (b == 0L)
                                throw new GrobArithmeticException(ErrorCatalog.E5003.Code, line, column, "integer modulo by zero");
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
                            if (b == 0.0)
                                throw new GrobArithmeticException(ErrorCatalog.E5004.Code, line, column, "float division by zero");
                            _stack.Push(GrobValue.FromFloat(a / b), line);
                            break;
                        }
                    case OpCode.ModuloFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
                            // See S1244 note above on DivideFloat — intentional exact-zero check (D-273).
                            if (b == 0.0)
                                throw new GrobArithmeticException(ErrorCatalog.E5005.Code, line, column, "float modulo by zero");
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
                            // Each fragment is converted to string via ToString() — this is the
                            // display/toString rule for interpolation slots (D-279).
                            byte count = _activeChunk.ReadByte(_ip++);
                            var parts = new string[count];
                            for (int i = count - 1; i >= 0; i--) {
                                parts[i] = _stack.Pop().ToString();
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
                        _out.WriteLine(_stack.Pop().ToString());
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
                                if (i < 0 || i >= array!.Count)
                                    throw new GrobRuntimeException(ErrorCatalog.E5101.Code, line, column,
                                        $"array index {i} is out of range for an array of length {array!.Count}");
                                _stack.Push(array[(int)i], line);
                            } else if (receiver.TryAsMap(out GrobMap? map)) {
                                _stack.Push(
                                    map!.TryGetValue(index.AsString(), out GrobValue value) ? value : GrobValue.Nil,
                                    line);
                            } else if (receiver.IsNil) {
                                throw new GrobRuntimeException(ErrorCatalog.E5201.Code, line, column,
                                    "nil dereference: cannot index nil value");
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
                            if (receiver.IsNil)
                                throw new GrobRuntimeException(ErrorCatalog.E5201.Code, line, column,
                                    "nil dereference: cannot access member on nil value");
                            if (receiver.TryAsArray(out GrobArray? array)) {
                                if (propertyName == "length") {
                                    _stack.Push(GrobValue.FromInt(array!.Count), line);
                                    break;
                                }
                                // Sprint 5C: array higher-order method binding.
                                // Capture the token at property-access time so the bound native
                                // carries the live token through to InvokeCallable.
                                CancellationToken ct = _cancellationToken;
                                NativeFunction? method = ArrayNatives.GetMethod(
                                    propertyName, array!,
                                    (callable, args) => InvokeCallable(callable, args, line, column, ct));
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
                            // Struct field resolution is deferred to Sprint 5.
                            throw new GrobInternalException(
                                $"opcode {OpCode.GetProperty} '{propertyName}' on receiver of kind " +
                                $"{receiver.Kind} not yet implemented (Sprint 5).");
                        }

                    // --- Calls and returns (Sprint 5 Increment A + C) ---

                    case OpCode.Call: {
                            // The callee was pushed before its arguments, so it sits
                            // argCount slots below the top. Its arguments become the
                            // callee's first locals over a new frame base.
                            int argCount = _activeChunk.ReadByte(_ip++);
                            if (_frameCount == MaxFrames)
                                throw new GrobRuntimeException(ErrorCatalog.E5901.Code, line, column,
                                    "Stack overflow — maximum call depth (256) exceeded");

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
                                VmInvoker invoker = (callable, args) =>
                                    InvokeCallable(callable, args, line, column, ct);

                                GrobValue nativeResult = native.Implementation(callArgs, invoker);
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
                            if (!isReentrant && _frameCount == 0) {
                                // Top-level code has finished; every top-level binding is now
                                // Initialised. Subsequent global reads skip the tag check
                                // (§19.1, D-294).
                                _startupComplete = true;
                                return;
                            }

                            GrobValue result = _stack.Pop();
                            // Close any open upvalues that point into this frame's slots
                            // before the slots are discarded. This copies their values
                            // from the stack to the heap (open → closed transition).
                            CloseUpvaluesFrom(_stackBase);
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

                    default:
                        throw new GrobInternalException(
                            $"opcode {(OpCode)instruction} not yet implemented (Sprint 3+)");
                }
            }
        } catch (OverflowException) {
            // Centralised handler for `checked(...)` arithmetic: any int op
            // that overflows surfaces as E5001 carrying the failing line.
            throw new GrobArithmeticException(ErrorCatalog.E5001.Code, line, column, "integer overflow");
        }
        // Note: OperationCanceledException from ThrowIfCancellationRequested() is NOT
        // caught here — it propagates to the caller of Run() as required by D-319.
    }

    // -----------------------------------------------------------------------
    // Re-entrant call-back bridge (D-319 load-bearing sub-problem)
    // -----------------------------------------------------------------------

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
            int line, int column, CancellationToken ct) {
        if (!callable.TryAsFunction(out GrobFunction? fn))
            throw new GrobInternalException(
                $"InvokeCallable: value of kind {callable.Kind} is not callable. " +
                "The type checker should have rejected the call site.");

        // Nested native: call the C# delegate directly without entering bytecode dispatch.
        if (fn is NativeFunction nativeFn) {
            VmInvoker nestedInvoker = (c, a) => InvokeCallable(c, a, line, column, ct);
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

        if (_frameCount == MaxFrames)
            throw new GrobRuntimeException(ErrorCatalog.E5901.Code, line, column,
                "Stack overflow — maximum call depth (256) exceeded");

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
