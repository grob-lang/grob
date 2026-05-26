using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// The Grob stack-based bytecode VM. Owns the operand stack and the
/// fetch-decode-execute dispatch loop. Sprint 2 Increment B implements the
/// subset of <see cref="OpCode"/> needed to execute hand-constructed chunks
/// up to <c>print(2 + 3 * 4)</c> — see <see cref="Run"/> for the supported
/// set; out-of-scope opcodes (control flow, calls, globals, structs, arrays,
/// closures, exceptions, increments, properties, build-string, etc.) raise
/// <see cref="GrobInternalException"/> until their owning increment lands.
///
/// Authority: grob-vm-architecture.md (dispatch loop, value stack, developer
/// diagnostics) and grob-v1-requirements.md §3.3 (the OpCode set).
/// </summary>
public sealed class VirtualMachine {
    private readonly ValueStack _stack = new();
    private readonly TextWriter _out;
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S4487:Unread \"private\" fields should be removed",
        Justification = "Read only inside #if DEBUG by the per-instruction trace hook; appears unread to Release-mode static analysis. Field is required so a single VM instance can be configured with a trace writer regardless of build configuration.")]
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

    /// <summary>
    /// Execute <paramref name="chunk"/> until <see cref="OpCode.Return"/>.
    /// Running off the end of the bytecode without a <see cref="OpCode.Return"/>
    /// is treated as a malformed chunk — it raises
    /// <see cref="GrobInternalException"/>, because the compiler always emits
    /// a terminating <c>Return</c> and hand-constructed test chunks must do
    /// the same.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Bytecode dispatch loop. Per D-302 each opcode is handled inline in a single switch to keep dispatch branch-free; extracting per-opcode handlers would add a call frame per instruction and is explicitly rejected.")]
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
                                throw new GrobArithmeticException("E5002", line, column, "integer division by zero");
                            // long.MinValue / -1 overflows: caught below as E5001.
                            _stack.Push(GrobValue.FromInt(checked(a / b)), line);
                            break;
                        }
                    case OpCode.ModuloInt: {
                            long b = _stack.Pop().AsInt();
                            long a = _stack.Pop().AsInt();
                            if (b == 0L)
                                throw new GrobArithmeticException("E5003", line, column, "integer modulo by zero");
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
#pragma warning disable S1244 // Exact-zero check is intentional per D-273: +0.0/-0.0 both caught, NaN propagates as NaN.
                            if (b == 0.0)
#pragma warning restore S1244
                                throw new GrobArithmeticException("E5004", line, column, "float division by zero");
                            _stack.Push(GrobValue.FromFloat(a / b), line);
                            break;
                        }
                    case OpCode.ModuloFloat: {
                            double b = _stack.Pop().AsFloat();
                            double a = _stack.Pop().AsFloat();
#pragma warning disable S1244 // Exact-zero check is intentional per D-273: +0.0/-0.0 both caught, NaN propagates as NaN.
                            if (b == 0.0)
#pragma warning restore S1244
                                throw new GrobArithmeticException("E5005", line, column, "float modulo by zero");
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

                    // --- Top-level return ends this chunk's execution ---
                    case OpCode.Return:
                        return;

                    default:
                        throw new GrobInternalException(
                            $"opcode {(OpCode)instruction} not implemented in Sprint 2 Increment B dispatch loop");
                }
            }
        } catch (OverflowException) {
            // Centralised handler for `checked(...)` arithmetic: any int op
            // that overflows surfaces as E5001 carrying the failing line.
            throw new GrobArithmeticException("E5001", line, column, "integer overflow");
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
