using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// The Grob stack-based bytecode VM. Owns the operand stack and the
/// fetch-decode-execute dispatch loop. Sprint 2 Increment B implements the
/// subset of <see cref="OpCode"/> needed to execute hand-constructed chunks
/// up to <c>print(2 + 3 * 4)</c> — see <see cref="Run"/> for the supported
/// set; out-of-scope opcodes (control flow, calls, structs, arrays,
/// closures, exceptions, properties, build-string, etc.) raise
/// <see cref="GrobInternalException"/> until their owning increment lands.
///
/// Authority: grob-vm-architecture.md (dispatch loop, value stack, developer
/// diagnostics) and grob-v1-requirements.md §3.3 (the OpCode set).
/// </summary>
public sealed class VirtualMachine {
    private readonly ValueStack _stack = new();
    private readonly TextWriter _out;
    private readonly Dictionary<string, GrobValue> _globals = new(StringComparer.Ordinal);

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
    /// Execute <paramref name="chunk"/> until <see cref="OpCode.Return"/>.
    /// Running off the end of the bytecode without a <see cref="OpCode.Return"/>
    /// is treated as a malformed chunk — it raises
    /// <see cref="GrobInternalException"/>, because the compiler always emits
    /// a terminating <c>Return</c> and hand-constructed test chunks must do
    /// the same.
    /// </summary>
    // Bytecode dispatch loop. Per D-302 each opcode is handled inline in a
    // single switch to keep dispatch branch-free; extracting per-opcode
    // handlers would add a call frame per instruction and is explicitly
    // rejected. SonarCloud suppresses S3776 (cognitive complexity) for this
    // file in .github/workflows/sonarcloud.yml.
    public void Run(Chunk chunk) {
        ArgumentNullException.ThrowIfNull(chunk);

        // Defensive: a prior Run that terminated by exception may have left
        // values on the operand stack. Start every invocation clean so the
        // VM behaves the same on the Nth chunk as on the first.
        _stack.Reset();

        int ip = 0;
        int line = 0;
        int column = 0;

        try {
            while (true) {
                if (ip >= chunk.Count)
                    throw new GrobInternalException(
                        "execution ran past end of chunk without Return");

                line = chunk.GetLine(ip);
                column = chunk.GetColumn(ip);

#if DEBUG
                TraceInstruction(chunk, ip);
#endif

                byte instruction = chunk.ReadByte(ip);
                ip++;

                switch ((OpCode)instruction) {
                    // --- Constants and singletons ---
                    case OpCode.Constant: {
                            byte index = chunk.ReadByte(ip++);
                            _stack.Push(chunk.ReadConstant(index), line);
                            break;
                        }
                    case OpCode.ConstantLong: {
                            int index = (chunk.ReadByte(ip) << 8) | chunk.ReadByte(ip + 1);
                            ip += 2;
                            _stack.Push(chunk.ReadConstant(index), line);
                            break;
                        }
                    case OpCode.Nil: _stack.Push(GrobValue.Nil, line); break;
                    case OpCode.True: _stack.Push(GrobValue.FromBool(true), line); break;
                    case OpCode.False: _stack.Push(GrobValue.FromBool(false), line); break;

                    case OpCode.Pop: _stack.Pop(); break;
                    case OpCode.PopN: {
                            byte count = chunk.ReadByte(ip++);
                            for (int i = 0; i < count; i++) _stack.Pop();
                            break;
                        }

                    // --- Globals ---
                    case OpCode.DefineGlobal: {
                            byte nameIdx = chunk.ReadByte(ip++);
                            string name = chunk.ReadConstant(nameIdx).AsString();
                            _globals[name] = _stack.Pop();
                            break;
                        }
                    case OpCode.GetGlobal: {
                            byte nameIdx = chunk.ReadByte(ip++);
                            string name = chunk.ReadConstant(nameIdx).AsString();
                            if (!_globals.TryGetValue(name, out GrobValue val))
                                throw new GrobRuntimeException(ErrorCatalog.E1001.Code, line, column,
                                    $"Undefined global '{name}'.");
                            _stack.Push(val, line);
                            break;
                        }
                    case OpCode.SetGlobal: {
                            byte nameIdx = chunk.ReadByte(ip++);
                            string name = chunk.ReadConstant(nameIdx).AsString();
                            if (!_globals.ContainsKey(name))
                                throw new GrobRuntimeException(ErrorCatalog.E1001.Code, line, column,
                                    $"Undefined global '{name}'.");
                            _globals[name] = _stack.Pop();
                            break;
                        }

                    // --- Locals ---
                    case OpCode.GetLocal: {
                            byte slot = chunk.ReadByte(ip++);
                            _stack.Push(_stack.GetSlot(slot), line);
                            break;
                        }
                    case OpCode.SetLocal: {
                            byte slot = chunk.ReadByte(ip++);
                            _stack.SetSlot(slot, _stack.Pop());
                            break;
                        }

                    // --- Increment / decrement (int locals only; float arms are compile errors) ---
                    case OpCode.IncrementInt: {
                            byte slot = chunk.ReadByte(ip++);
                            long cur = _stack.GetSlot(slot).AsInt();
                            _stack.SetSlot(slot, GrobValue.FromInt(checked(cur + 1L)));
                            break;
                        }
                    case OpCode.DecrementInt: {
                            byte slot = chunk.ReadByte(ip++);
                            long cur = _stack.GetSlot(slot).AsInt();
                            _stack.SetSlot(slot, GrobValue.FromInt(checked(cur - 1L)));
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
                            byte count = chunk.ReadByte(ip++);
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
                            int hi = chunk.ReadByte(ip++);
                            int lo = chunk.ReadByte(ip++);
                            ip += (hi << 8) | lo;
                            break;
                        }
                    case OpCode.JumpIfFalse: {
                            // Conditional forward jump; pops the bool condition.
                            int hi = chunk.ReadByte(ip++);
                            int lo = chunk.ReadByte(ip++);
                            GrobValue cond = _stack.Pop();
                            if (!cond.AsBool())
                                ip += (hi << 8) | lo;
                            break;
                        }
                    case OpCode.JumpIfTrue: {
                            // Conditional forward jump for OR short-circuit; peeks (does not pop)
                            // so the condition value remains on the stack as the result.
                            int hi = chunk.ReadByte(ip++);
                            int lo = chunk.ReadByte(ip++);
                            if (_stack.Peek().AsBool())
                                ip += (hi << 8) | lo;
                            break;
                        }
                    case OpCode.Loop: {
                            // Unconditional backward jump (Sprint 4 Increment B).
                            // The 2-byte big-endian offset is subtracted from the
                            // instruction pointer after the two operand bytes are read,
                            // landing exactly at the loop-top (condition start).
                            int hi = chunk.ReadByte(ip++);
                            int lo = chunk.ReadByte(ip++);
                            ip -= (hi << 8) | lo;
                            break;
                        }

                    // --- Arrays and maps (Sprint 4 Increment C — for...in iteration surface) ---

                    case OpCode.NewArray: {
                            // 1-byte element count. Pop that many values (LIFO) and
                            // reverse-fill so the array preserves source order.
                            byte count = chunk.ReadByte(ip++);
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

                    // --- Properties (struct fields Sprint 5; array.length and map.keys here) ---

                    case OpCode.GetProperty: {
                            byte nameIdx = chunk.ReadByte(ip++);
                            string propertyName = chunk.ReadConstant(nameIdx).AsString();
                            GrobValue receiver = _stack.Pop();
                            // Nil receiver raises E5201 (nil dereference at runtime).
                            if (receiver.IsNil)
                                throw new GrobRuntimeException(ErrorCatalog.E5201.Code, line, column,
                                    "nil dereference: cannot access member on nil value");
                            if (receiver.TryAsArray(out GrobArray? array) && propertyName == "length") {
                                _stack.Push(GrobValue.FromInt(array!.Count), line);
                                break;
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

                    // --- Top-level return ends this chunk's execution ---
                    case OpCode.Return:
                        return;

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
