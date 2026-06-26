# Grob — VM Architecture & Design Notes

> Captured from evening design session, February 2026.
> Deep dive into bytecode VM concepts, type checking, GC, and plugins.
> Informs Grob’s implementation after SharpBASIC and clox are complete.

---

## Bytecode VM — Core Concepts

### What a Bytecode VM Is

A bytecode VM replaces tree-walking execution with two distinct stages:

- **The compiler** — walks the AST and emits a flat sequence of simple instructions (bytecode)
- **The VM** — a tight loop that reads one instruction, executes it, reads the next

The compiler is the intelligent part. The VM is deliberately dumb — it just executes decisions already made at compile time.

### Comparison to SharpBASIC’s Evaluator

| SharpBASIC Evaluator         | Grob Bytecode VM            |
| ---------------------------- | --------------------------- |
| AST node                     | Opcode + operands           |
| `EvaluateExpression`         | Fetch-decode-execute loop   |
| Pattern match on node type   | Switch on opcode byte       |
| Recursive tree walk          | Linear instruction sequence |
| SymbolTable dictionary       | Stack slots + globals array |
| Call stack via C# call stack | Explicit call frames array  |

The evaluator is recursive — C#’s own call stack tracks where you are.
The VM is iterative — recursion was flattened by the compiler at compile time.

### The Stack Machine

Grob uses a **stack-based VM**. Values are pushed and popped from a value stack.

```
// 2 + 3 * 4 compiles to:
PUSH  2
PUSH  3
PUSH  4
MULTIPLY    ← operator precedence baked in at compile time
ADD
```

Operator precedence is resolved by the compiler. The VM executes blindly in sequence.

### The Fetch-Decode-Execute Loop

```csharp
while (true)
{
    var instruction = ReadByte();
    switch (instruction)
    {
        case OpCode.Constant:
            Push(ReadConstant());
            break;
        case OpCode.Add:
            var right = Pop();
            var left = Pop();
            Push(left + right);
            break;
        case OpCode.Return:
            return Pop();
    }
}
```

That loop is the entire VM. More instructions = more cases. Nothing more.

---

## The Instruction Set

### It’s Yours — Completely Custom

There is no standard bytecode format. Every language defines its own.
Python, the JVM, Lua, clox — all different, all custom, none interoperable.

An opcode is just a number. Conventionally a single byte — 256 possible instructions.
Most languages use far fewer. clox uses ~30. Python ~120. The JVM ~200.

```csharp
public enum OpCode : byte
{
    // Values
    Constant,       // push constant from pool
    Nil,            // push nil
    True,           // push true
    False,          // push false
    Pop,            // discard top of stack

    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    Negate,         // unary minus

    // Comparison
    Equal,
    NotEqual,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,

    // Logic
    Not,
    And,
    Or,

    // Variables
    GetLocal,
    SetLocal,
    GetGlobal,
    SetGlobal,
    DefineGlobal,

    // Control flow
    Jump,           // unconditional forward jump
    JumpIfFalse,    // conditional forward jump
    Loop,           // unconditional backward jump

    // Functions
    Call,
    Return,

    // I/O
    Print,
    Input,
}
```

The instruction set grows organically as the language needs it.
Don’t design all opcodes upfront — add them as features demand them.

### The Constant Pool

Literals don’t live inline in the bytecode stream — they live in a separate
constant pool array. The bytecode references them by index.

```
[CONSTANT] [0]    // push constants[0] — e.g. the integer 42
[CONSTANT] [1]    // push constants[1] — e.g. the string "hello"
[ADD]
```

The values stored in the constant pool — and pushed onto the operand stack
during execution — are `GrobValue` instances. The next section specifies the
`GrobValue` representation.

---

## GrobValue Representation

`GrobValue` is the runtime value type used everywhere the VM stores or
moves a Grob value: the operand stack, local slots, the globals table,
the constant pool, plugin call boundaries. Every Grob value at runtime
is a `GrobValue`.

This section locks the v1 representation. The full value-representation
decision (OQ-005 — tagged union versus NaN boxing) is closed: tagged
union is permanent (D-303). NaN boxing was rejected on managed-runtime
grounds — the .NET GC cannot trace references packed into a `ulong`,
pinning to keep packed addresses valid defeats the size win NaN boxing
was meant to deliver, and Grob's I/O-bound workload does not need the
cache-pressure win that justifies the technique in tight numeric loops.
Full rationale in `grob-decisions-log.md` D-303 and
`grob-open-questions.md` OQ-005.

### Target framework

Grob v1 targets **.NET 10 LTS**. `GrobValue` is implemented as a hand-rolled
tagged-union struct in plain C# 12 / .NET 10. The .NET 11 preview `union`
keyword is **not** used for v1, even though it became available in .NET 11
Preview 2.

The reasoning is straightforward. The .NET 11 `union` keyword's
compiler-generated form lowers to a struct whose only storage is
`public object? Value` — every value-type case is boxed on assignment.
For a stack-based VM that pushes thousands of integers per script
execution, that is the wrong cost profile. The `[Union]`-attributed
escape hatch lets you keep your own storage and gain compile-time
exhaustiveness checking on `switch`, but the storage work is identical
to what is described below; the attribute buys exhaustiveness only, at
the cost of a runtime dependency on a feature still in preview. .NET 10
is LTS (3 years of support); .NET 11 is STS (24 months). Targeting STS
as the primary platform forces a major-version migration during v1's
expected lifetime.

The shape chosen here is deliberately the right shape for `[Union]` to
slot in later. When .NET 11 is GA and battle-tested, adding `[Union]`
and `IUnion` to `GrobValue` is a one-commit change — see the migration
signpost at the end of this section.

### The struct shape

`GrobValue` is a `readonly struct` with three private fields: a
discriminator enum, a 64-bit scalar slot and a single managed reference
slot.

```csharp
namespace Grob.Core;

public enum GrobValueKind : byte
{
    Nil      = 0,   // 0 is deliberate — default(GrobValue) is Nil
    Bool     = 1,
    Int      = 2,
    Float    = 3,
    String   = 4,
    Array    = 5,
    Map      = 6,
    Struct   = 7,   // user-defined and registered plugin types alike
    Function = 8,   // Grob lambdas and function references
}

public readonly struct GrobValue : IEquatable<GrobValue>
{
    // Representation locked May 2026 (D-303, OQ-005 closed).
    // Tagged union: discriminator + scalar slot + reference slot.
    // Do NOT access these fields outside Grob.Core. The public API
    // below is the encapsulation boundary.
    private readonly GrobValueKind _kind;
    private readonly long          _scalar;
    private readonly object?       _reference;

    private GrobValue(GrobValueKind kind, long scalar, object? reference)
    {
        _kind      = kind;
        _scalar    = scalar;
        _reference = reference;
    }

    // ... factories, accessors, equality, hashing — see below
}
```

**Storage layout, x64.** With natural alignment, the struct is **24 bytes**:
1 byte for the discriminator, 7 bytes of padding to align the `long`,
8 bytes for the scalar, 8 bytes for the managed reference. The reference
slot is the only field that participates in GC — primitives in the scalar
slot are never visible to the collector. Pushing a `GrobValue` onto the
operand stack is therefore a 24-byte struct copy with no allocation, and
populating an `int`, `bool` or `float` value never touches the heap.

**Encoding per kind:**

| Kind       | `_scalar`                               | `_reference`            |
| ---------- | --------------------------------------- | ----------------------- |
| `Nil`      | `0`                                     | `null`                  |
| `Bool`     | `0` for false, `1` for true             | `null`                  |
| `Int`      | the `long` value directly               | `null`                  |
| `Float`    | `BitConverter.DoubleToInt64Bits(value)` | `null`                  |
| `String`   | `0`                                     | the `string`            |
| `Array`    | `0`                                     | `GrobArray` instance    |
| `Map`      | `0`                                     | `GrobMap` instance      |
| `Struct`   | `0`                                     | `GrobStruct` instance   |
| `Function` | `0`                                     | `GrobFunction` instance |

Reading a `float` back: `BitConverter.Int64BitsToDouble(_scalar)` — bit-exact
round-trip including the full `NaN` payload, which matters for the IEEE 754
semantics specified in `grob-stdlib-reference.md`.

### The discriminator type set

Nine variants — small enough to fit in a single byte and to switch on
efficiently, rich enough that hot paths in the VM and `print()` decide
their behaviour from the kind alone with no further type-lookup work.

The set deliberately collapses several language-level types into the
`Struct` variant. `date`, `guid`, `File`, `ProcessResult`, `json.Node`,
`Regex`, `Match`, `csv.Table`, `CsvRow`, `Response`, `AuthHeader`,
`ZipEntry` and every user-declared `type` are all `Struct` at the
`GrobValue` level. Discrimination between them happens at the type-registry
level using the runtime type of the boxed reference — not at the
`GrobValue` discriminator, which would otherwise need to grow each time
a plugin registers a new type. This keeps `GrobValueKind` stable as the
ecosystem grows.

`guid` is reference-stored despite being a 128-bit primitive. Splitting
the scalar slot into two 64-bit halves to inline a `Guid` would double
the struct size; per-use boxing of `System.Guid` is an acceptable cost
because GUIDs are not pushed onto the stack at integer rates.

`Function` is a separate variant rather than a flavour of `Struct` because
function calls are a hot VM operation and the call site benefits from a
single-byte discriminator check rather than a runtime type check on the
reference.

Exception instances flow through the VM as `Struct` values — they are
declared in `Grob.Runtime` as ordinary types with named fields (per the
ten-leaf hierarchy in D-284) and require no special discriminator.

### The encapsulation boundary

The public surface of `GrobValue` is the only contract callers depend on.
The internal field layout is implementation detail. With OQ-005 closed
(D-303), the layout is stable; the encapsulation boundary remains the only
contract callers see, future-proofing the design against optimisation
work confined to `Grob.Core`.

```csharp
public readonly struct GrobValue : IEquatable<GrobValue>
{
    // ----- Singleton -----
    public static readonly GrobValue Nil;   // == default(GrobValue)

    // ----- Factories -----
    public static GrobValue FromBool    (bool value);
    public static GrobValue FromInt     (long value);
    public static GrobValue FromFloat   (double value);
    public static GrobValue FromString  (string value);
    public static GrobValue FromArray   (GrobArray value);
    public static GrobValue FromMap     (GrobMap value);
    public static GrobValue FromStruct  (GrobStruct value);
    public static GrobValue FromFunction(GrobFunction value);

    // ----- Inspection -----
    public GrobValueKind Kind { get; }
    public bool IsNil      { get; }
    public bool IsBool     { get; }
    public bool IsInt      { get; }
    public bool IsFloat    { get; }
    public bool IsString   { get; }
    public bool IsArray    { get; }
    public bool IsMap      { get; }
    public bool IsStruct   { get; }
    public bool IsFunction { get; }

    // ----- Strict accessors — throw GrobInternalException on kind mismatch -----
    public bool         AsBool();
    public long         AsInt();
    public double       AsFloat();
    public string       AsString();
    public GrobArray    AsArray();
    public GrobMap      AsMap();
    public GrobStruct   AsStruct();
    public GrobFunction AsFunction();

    // ----- Try-accessors — return false on kind mismatch, no exception -----
    public bool TryAsBool    (out bool         value);
    public bool TryAsInt     (out long         value);
    public bool TryAsFloat   (out double       value);
    public bool TryAsString  (out string?      value);
    public bool TryAsArray   (out GrobArray?   value);
    public bool TryAsMap     (out GrobMap?     value);
    public bool TryAsStruct  (out GrobStruct?  value);
    public bool TryAsFunction(out GrobFunction? value);

    // ----- Equality and hashing -----
    public bool          Equals(GrobValue other);
    public override bool Equals(object? obj);
    public override int  GetHashCode();
    public static bool   operator ==(GrobValue left, GrobValue right);
    public static bool   operator !=(GrobValue left, GrobValue right);
}
```

The contract is:

1. **Construction** is always through a factory or the `Nil` singleton.
   Direct field manipulation is impossible — the fields are private. The
   compiler cannot assemble a `GrobValue` of a kind the public API does
   not expose, so the discriminator and the payload always agree.
2. **Inspection** is via `Kind` or the `Is*` predicates. Both compile to
   a single discriminator read.
3. **Strict accessors** are for code that statically knows the kind (the
   compiler emits the correct accessor based on type-checked operand
   types). A kind mismatch is a compiler bug, not a user bug, and throws
   `GrobInternalException` with the actual and expected kinds. **No user
   script can ever reach these throw sites in correctly compiled code.**
4. **Try-accessors** are for plugin authors and runtime helpers that
   defensively probe a value of unknown kind. They never throw.
5. **`default(GrobValue)` is `Nil`.** A zero-initialised struct is a valid
   `Nil` value. This matters for `GrobValue[]` allocation (e.g. the
   operand stack and the locals array) — the slots are usable on
   allocation with no per-slot initialisation pass.

The layout fields (`_kind`, `_scalar`, `_reference`) are explicitly **not**
part of the contract. Code outside `Grob.Core` must use the public API.
A test in `Grob.Core.Tests` asserts the public surface area to catch any
accidental field exposure.

### Equality and hashing

Equality at the runtime level is value equality for primitives and
strings; reference equality for arrays, maps and functions; and
delegated to the contained `GrobStruct` for struct values.

| Same kind                | `Equals` semantics                                      |
| ------------------------ | ------------------------------------------------------- |
| `Nil` == `Nil`           | always true                                             |
| `Bool` == `Bool`         | scalar equality                                         |
| `Int` == `Int`           | scalar equality                                         |
| `Float` == `Float`       | IEEE 754: `NaN != NaN`, `+0.0 == -0.0` (C# default)     |
| `String` == `String`     | ordinal value equality (`string.Equals(a, b, Ordinal)`) |
| `Array` == `Array`       | reference equality                                      |
| `Map` == `Map`           | reference equality                                      |
| `Struct` == `Struct`     | delegate to `GrobStruct.Equals` (field-by-field)        |
| `Function` == `Function` | reference equality                                      |
| _different kinds_        | always false                                            |

`==` between Grob values of incompatible kinds is a compile error at the
language level (D-169). The runtime cross-kind rule above is defensive —
if a bug elsewhere produces such a comparison, the runtime returns
`false` rather than throwing. The compiler bears the responsibility for
preventing the situation in correctly typed code.

`GetHashCode` mirrors equality: each kind hashes its payload.
`HashCode.Combine(_kind, payload)` ensures that, for example, an `Int`
holding `42` and a `Float` holding `42.0` hash to different values —
because they are not equal under the rules above.

The hash policy matters now even though v1 maps key only on `string`
(D-141): user code can place `GrobValue` instances into `HashSet<GrobValue>`
or `Dictionary<GrobValue, ...>` for runtime helpers, and post-MVP
non-string map keys will reach this path directly.

For `Float` values, `NaN.Equals(NaN)` returns `true` at the C# `double`
level — a deliberate inconsistency with `==` semantics that exists so
collections can locate `NaN` keys. Grob inherits this behaviour from
`double.Equals`. The asymmetry is documented but not exposed at the
language level: Grob script authors never hit it because `==` on `NaN`
floats follows IEEE 754 (returns `false`), and that comparison is what
user code observes.

### Test strategy

`Grob.Core.Tests/GrobValueTests.cs` covers the representation:

- **Construction round-trip.** For every kind, `FromX(value).AsX() == value`.
  Includes `NaN`, `±Infinity`, `long.MinValue`, `long.MaxValue`, empty
  string, single-character string, multi-byte UTF-8 string.
- **Discrimination.** Each `IsX` predicate returns `true` only for its
  kind. `Kind` returns the expected enum value.
- **Default value.** `default(GrobValue)` is `Nil`; `default(GrobValue) == GrobValue.Nil`;
  `default(GrobValue).IsNil` is `true`.
- **Kind-mismatch accessors.** `FromInt(42).AsString()` throws
  `GrobInternalException` with a message naming both kinds.
  `FromInt(42).TryAsString(out _)` returns `false`.
- **Equality.** Same-kind value equality matches the table above.
  Different-kind equality is always `false`. `==` operator agrees with
  `Equals`.
- **Hashing.** Equal values produce equal hashes. `FromInt(42)` and
  `FromFloat(42.0)` produce different hashes.
- **Float edge cases.** `FromFloat(double.NaN) != FromFloat(double.NaN)`
  via `==`, but `FromFloat(double.NaN).Equals(FromFloat(double.NaN))` is
  true (matching `double.Equals`). `FromFloat(0.0) == FromFloat(-0.0)`.
- **Reference identity for collections.** Two `FromArray` values built
  from the same `GrobArray` instance are equal; two `FromArray` values
  built from distinct but element-wise identical instances are not.
- **Struct delegation.** `FromStruct(s1) == FromStruct(s2)` returns the
  result of `s1.Equals(s2)` (field-by-field per D-169).

A separate test asserts that `sizeof(GrobValue)` is 24 (x64) — a
canary that catches accidental field churn that would change the layout.

### Locked contract

With OQ-005 closed (D-303), every aspect of `GrobValue` is locked for v1:

- The public API surface above. Plugins, the compiler and the VM use
  this surface.
- `default(GrobValue)` is `Nil`.
- The nine-variant `GrobValueKind` enum.
- The equality and hashing rules.
- The internal field layout: `_kind` + `_scalar` + `_reference`. NaN
  boxing was explicitly rejected on managed-runtime grounds (D-303,
  OQ-005). Future optimisation work on `GrobValue` stays within the
  encapsulation boundary above — any layout change is a `Grob.Core`-local
  concern and does not propagate to `Grob.Compiler` or `Grob.Vm`.
- The .NET 10 LTS target.

**Migration signpost — .NET 11 native unions.** When .NET 11 is GA and the
`[Union]` attribute escape hatch leaves preview, adding it to `GrobValue`
is a one-commit change:

1. Apply `[Union]` to the struct.
2. Implement `IUnion` and tag each variant via `[Tag]` markers or the
   discriminated layout the attribute expects.
3. Storage does not change — the hand-rolled fields stay.
4. Gain compile-time exhaustiveness checking on every `switch` over
   `Kind` in the codebase.

The encapsulation boundary above is already the right shape for
`[Union]` to slot in. This is a future-additive path, not a redesign.

---

## The Bytecode File Format

The full `.grobc` binary format is specified in **`grob-grobc-format.md`**.
The summary below is a sketch for context; the spec doc is authoritative.

### Structure (sketch)

A compiled Grob file (`.grobc`) is a section-based binary file with a
fixed-size header followed by the constant pool, instruction stream,
optional source map and optional symbol table. The header carries
explicit offset and size fields for every section, so a loader can read
sections in any order and a future format version can append fields
without breaking older readers up to the offset they understand.

```
[header]              40 bytes — magic "GROB", format version, flags,
                                 six (offset, size) pairs
[constant pool]       variable — each entry prefixed with a kind byte
[instruction stream]  variable — flat opcode stream per ADR-0013
[source map]          variable, optional — PC → (line, column) table
[symbol table]        variable, optional — function and parameter names
```

See `grob-grobc-format.md` for the full byte-level layout, the
constant-pool wire format per kind, the source map and symbol table
encodings, the version-mismatch behaviour and the explicit
non-features for v1 (no signing, no compression, no encryption,
no multi-chunk packaging).

### In-Memory Execution

For Grob’s primary use case — `grob run script.grob` — compilation happens
in memory and bytecode is never written to disk unless explicitly requested.

Compile time for typical scripts: single digit milliseconds. Invisible to users.

The file format still matters for optional caching — if source hasn’t changed
since last run, load the cached `.grobc` instead of recompiling. The cache
lives in a `.grob/cache/` side directory next to the source file; mtime-driven
invalidation; see `grob-grobc-format.md` for the integration with `grob run`.

---

## Control Flow in Bytecode

### Jump Instructions

Control flow is implemented with jump instructions — no branches in the
instruction stream, just jumps forward or backward.

```
// IF x > 5 THEN PRINT "big" END IF

GET_LOCAL   0        // push x
CONSTANT    0        // push 5
GREATER
JUMP_IF_FALSE → end  // skip block if false
CONSTANT    1        // push "big"
PRINT
end:
```

### Backpatching — Forward Jumps

When emitting `JUMP_IF_FALSE` the compiler doesn’t yet know how far to jump.
Solution: emit a placeholder, compile the block, then patch the real distance.

```csharp
private int EmitJump(OpCode op)
{
    Emit(op);
    Emit(0xFF);  // placeholder high byte
    Emit(0xFF);  // placeholder low byte
    return _bytecode.Count - 2;  // return position to patch later
}

private void PatchJump(int offset)
{
    int distance = _bytecode.Count - offset - 2;
    _bytecode[offset]     = (byte)(distance >> 8);
    _bytecode[offset + 1] = (byte)(distance & 0xFF);
}
```

### IF / ELSE — Two Jumps

```
CONDITION
JUMP_IF_FALSE → else_start
[then block]
JUMP → end              ← unconditional, skips else
[else block]
[end]
```

Two backpatches. Same mechanism applied twice.

### Loops — Backward Jumps

```csharp
private void CompileWhile(WhileStatement stmt)
{
    int loopStart = _bytecode.Count;  // record BEFORE condition

    CompileExpression(stmt.Condition);
    int exitJump = EmitJump(OpCode.JumpIfFalse);  // needs patch

    CompileBlock(stmt.Body);
    EmitLoop(loopStart);   // backward jump — position already known

    PatchJump(exitJump);   // patch exit
}
```

Forward jumps need backpatching. Backward jumps don’t.

### FOR Loops — Lowering

FOR is desugared to WHILE by the compiler. The VM never sees FOR opcodes.

```grob
FOR i = 1 TO 10 STEP 1
```

is compiled as if it were:

```grob
i := 1
while i <= 10 {
    [body]
    i = i + 1
}
```

This technique — reducing a higher level construct to a simpler one before
emitting code — is called **lowering**.

---

## Call Frames and the Call Stack

### The Call Frame

When a function is called the VM pushes a call frame onto the frames array:

```csharp
struct CallFrame
{
    public GrobFunction Function;
    public int InstructionPointer;   // where we are in this function's bytecode
    public int StackBase;            // where this frame's locals start on stack
}
```

The VM maintains:

```csharp
CallFrame[] _frames = new CallFrame[256];
int _frameCount = 0;
```

### Calling a Function

```csharp
case OpCode.Call:
    int argCount = ReadByte();
    var function = Peek(argCount) as GrobFunction;
    _frames[_frameCount++] = new CallFrame
    {
        Function = function,
        InstructionPointer = 0,
        StackBase = _stackTop - argCount
    };
    break;
```

### Returning

```csharp
case OpCode.Return:
    var result = Pop();
    _frameCount--;
    _stackTop = frame.StackBase - 1;
    Push(result);
    break;
```

### Local Variables as Stack Slots

Local variables live directly on the value stack — no dictionary, no string
lookup. Just array indexing by slot number.

```
// fn add(a: int, b: int): int
// Stack after call with add(3, 4):
... | 3 | 4 | _ |
      ↑   ↑   ↑
    slot0 slot1 slot2 (result)
    (a)   (b)
```

Arguments pushed by the caller become the first locals automatically.

```csharp
case OpCode.GetLocal:
    int slot = ReadByte();
    Push(_stack[frame.StackBase + slot]);
    break;

case OpCode.SetLocal:
    int slot = ReadByte();
    _stack[frame.StackBase + slot] = Peek(0);
    break;
```

### Stack Overflow

```csharp
if (_frameCount == MaxFrames)
{
    RuntimeError("Stack overflow — maximum call depth exceeded");
    return;
}
```

### What a Stack Trace Actually Is

A stack trace is the frames array printed from top to bottom.
Each line is one CallFrame — the function name and instruction pointer.
The debugger’s locals pane is `_stack[frame.StackBase + slot]` for each slot.
Debug builds include a name table mapping slot numbers to variable names.
Release builds strip it — the bytecode is identical, only metadata differs.

### Developer Diagnostics — Disassembler and Execution Tracing

Two diagnostic tools sit alongside the VM. Both exist to make bytecode
visible during development. They are developer affordances, not language
or — with one deferred exception — user-facing features. Authority: D-306.

#### The disassembler

`Disassembler` lives in `Grob.Vm` and is **always compiled** — present in
Release builds as much as Debug. It walks a `Chunk` and prints it
human-readably:

```csharp
disassembleChunk(Chunk chunk)              // whole chunk, instruction by instruction
disassembleInstruction(Chunk chunk, int offset)  // one instruction, returns next offset
```

Each line shows the byte offset, the opcode name, any operands, and — for
`Constant` and friends — the constant-pool index alongside the resolved
value it points at. Source line numbers are printed from the chunk's line
array, with a marker when an instruction shares a line with the one before
it. The shape mirrors clox's `debug.c`: a `switch` over the same `OpCode`
enum the VM dispatches on, printing instead of executing.

This is the single most valuable tool for debugging the compiler. When the
VM produces a wrong answer there are three suspects — the type checker
annotated wrong, the compiler emitted wrong bytecode, or the VM executed
correct bytecode incorrectly. The disassembler bisects them: compile, dump,
read the bytecode by eye. If the bytecode is wrong the bug is in the
compiler; if it is right the bug is in the VM. It is built in Sprint 2
against hand-constructed chunks — before the compiler emits anything — so
that the first bytecode the compiler ever produces is readable immediately.

It is reached three ways: directly from tests (the Sprint 2 compiler tests
call `disassembleChunk` to assert emitted bytecode), from a scratch entry
point during development, and — from Sprint 12 — through the `grob dump
<file>` command, which compiles a script and prints its chunk without
executing. `grob dump` is a thin wrapper; the engine is the Sprint 2
deliverable.

#### Execution tracing

Execution tracing is the per-instruction firehose: the value stack and the
about-to-execute instruction, printed every iteration of the dispatch loop.
It is the fastest way to find a stack-discipline bug — a value left on the
stack, a pop that underflows — because you watch the stack depth evolve one
opcode at a time.

It is gated behind `#if DEBUG` in the `Grob.Vm` C# source, not behind a
runtime flag:

```csharp
while (true)
{
#if DEBUG
    TraceInstruction(chunk, ip);
#endif
    var instruction = ReadByte();
    switch (instruction)
    {
        // ...
    }
}
```

In a Debug build the call compiles in and fires every iteration. In a
Release build the C# compiler removes it entirely — it is not in the binary,
not even as a disabled branch. The reason is the benchmarks: the D-302 VM
micro-benchmarks run in Release and exist to catch regressions in the
dispatch loop, the hottest path in the runtime. A runtime `if (_trace)`
check would put a branch on every instruction in the measured Release
binary even when tracing is off, polluting exactly the numbers the
benchmarks protect. `#if DEBUG` makes the cost genuinely zero where it is
measured.

The consequence for the developer: tracing is reached by **compiling a
Debug build and running**, never by a CLI flag. This is distinct from the
disassembler, which is always present, and from `--verbose`, which surfaces
`log.debug()` output and is a user-facing feature. Tracing surfaces none of
those — it is raw VM internals for whoever is debugging the VM itself.

### Top-Level Initialisation and Global Slots

Top-level `readonly` and mutable bindings are compiled as globals, not as
locals. The VM holds a globals table keyed by slot index, sized by the type
checker at compile time.

Each top-level binding slot carries a three-state tag:

```csharp
enum SlotState : byte
{
    Uninitialised = 0,
    Initialising  = 1,
    Initialised   = 2,
}
```

The `DefineGlobal` opcode flips the slot's tag from `Uninitialised` to
`Initialising` before evaluating the right-hand side, and from
`Initialising` to `Initialised` once the RHS has produced a value and
been stored. `GetGlobal` during startup consults the tag; a read from a
slot that is not `Initialised` raises `RuntimeError` with the circular-
initialisation diagnostic (see Language Fundamentals §19.1).

After the top-level code's final instruction, the VM sets a single
`_startupComplete` flag. Subsequent `GetGlobal` dispatches skip the tag
check and read the slot directly. The cost of the check is therefore a
single branch per global read during startup and zero afterwards.

`const` bindings do not occupy a global slot. The type checker resolves
each `const` in pass 2 and the compiler inlines every reference as a
direct `Constant` opcode against the constant pool.

---

## Type Checking

### Where It Sits

```
Source → Lexer → Parser → AST → Type Checker → Compiler → Bytecode → VM
```

The type checker walks the AST before any bytecode is emitted.
If the program is not type-safe, compilation stops. The VM never sees
a type-unsafe program.

### What It Does

Annotates every expression node in the AST with a resolved type.
The compiler reads these annotations to emit the right opcodes.
Types are resolved once at compile time — never checked at runtime.

### The Type Environment

Same concept as SharpBASIC’s SymbolTable — maps names to types instead
of names to values. Requires the same parent chain for scope support:

```csharp
class TypeEnvironment(TypeEnvironment? parent = null)
{
    private readonly Dictionary<string, GrobType> _types = new();

    public GrobType? Get(string name) =>
        _types.TryGetValue(name, out var t) ? t : parent?.Get(name);

    public void Define(string name, GrobType type) => _types[name] = type;
}
```

### Type Inference

Literals have obvious types. For declarations:

```grob
x := 42       // right side is int → x is int, recorded in type environment
```

For binary expressions — look up both operand types, check compatibility,
annotate result type. Mismatch is a compile time error.

### Optional Type Narrowing (Flow-Sensitive Typing)

```grob
name: string? := nil
print(name)           // compile error — name might be nil

if name != nil {
    print(name)       // fine — compiler narrows type to string here
}
```

Inside `if x != nil` blocks the type checker adds `x` to a known-non-nil set.
The type narrows from `string?` to `string`. Removed again after the block.

### Function Call Type Checking

When the type checker encounters a call it:

1. Looks up the function signature in the type environment
2. Verifies each argument type matches the parameter type
3. Annotates the call expression with the function’s return type

```grob
fn add(a: int, b: int): int { ... }

add(1, "hello")          // compile error — arg 2 expected int got string
let x: string = add(1,2) // compile error — add returns int not string
```

### Type-Driven Opcode Selection

The compiler uses type annotations to emit specialised opcodes:

```
Both sides int    → ADD_INT
Both sides float  → ADD_FLOAT
Both sides string → CONCAT
Mixed int/float   → PROMOTE_TO_FLOAT then ADD_FLOAT
```

No runtime type checks needed — the type checker already verified correctness.

---

## Memory Management

### Strategy — lean on .NET GC (D-304)

Grob delegates heap memory management to the .NET garbage collector. No
custom mark-and-sweep collector is shipped in v1. OQ-006 is closed; full
rationale in `grob-decisions-log.md` D-304 and `grob-open-questions.md`
OQ-006.

The choice follows from the platform. Grob's VM runs on the CLR. CLR-
allocated objects cannot be hidden from the .NET GC; any custom collector
would compete with the runtime collector rather than replace it,
introducing a parallel object lifecycle, an allocation hook on every
heap-bound value construction, and a new class of latent bug
(use-after-free in marked-then-collected-incorrectly cases) for no
measured benefit. clox implements its own collector because C has no
choice. Grob is in C#; the choice is whether to add a redundant layer to
a runtime that already solves the problem. The answer is no.

### What lives where

**Value types** — live directly in `GrobValue`'s `_scalar` field, zero
GC pressure:

- `int`, `float`, `bool`, `nil` — fixed size, stored in the 64-bit scalar
  slot, never allocated on the heap.

**Heap types** — ordinary CLR objects, reclaimed by the .NET GC when no
live `GrobValue` references them:

- `string`, `GrobArray`, `GrobMap`, `GrobStruct`, `GrobFunction`,
  plugin-registered reference types — stored in `GrobValue`'s
  `_reference` field, allocated via `new`, freed by the runtime.

The `_reference` field is the single GC root the runtime sees per
`GrobValue` slot. Stack slots, locals, the globals table and the
constant pool participate in the normal GC root walk by virtue of being
arrays of `GrobValue` reachable from VM state. No finaliser is required
on `GrobValue` or any runtime-internal type.

### What does not exist in v1

- No mark phase, sweep phase, allocation-threshold trigger, or
  `CollectGarbage()` entry point in `Grob.Vm`.
- No custom heap data structure (no `_heapHead`/`_heapSize`/`Allocate()`
  plumbing). Each runtime reference type is allocated by `new` and
  managed by the CLR.
- No GC tuning surface in `grob.json` or the CLI. The runtime exposes no
  GC settings of its own; users may set CLR GC switches (server GC,
  concurrent GC) via standard .NET configuration if they choose, but this
  is not a Grob feature.

### Pressure profile

Grob's target workload is I/O-bound scripting — Azure CLI orchestration,
DevOps REST tooling, JSON/CSV transformation, file-system walks. The hot
path is stdlib calls, not allocation churn. Heap pressure is dominated
by short-lived string objects from `json.parse()`, `fs.readText()` and
string interpolation — exactly the pattern the .NET generational GC
handles well in Gen 0.

The design work that matters for GC pressure is at the value-
representation level: primitives stay in the scalar slot, the
`_reference` field is `null` for primitive `GrobValue` instances, and
heap allocation only happens when a value genuinely needs to live on the
heap. That work is already done — D-303 locks the shape.

### Revisit conditions

The benchmarking infrastructure (D-302) provides the empirical surface
to revisit this decision. `[MemoryDiagnoser]` on every benchmark records
allocations and Gen 0/1/2 collection counts in the baseline. If a real
script later shows GC pressure that the .NET collector handles badly,
the data to substantiate the case will exist.

If a revisit is forced, the migration path is additive rather than
destructive: introduce a managed-side weak-reference tracking table
inside `Grob.Vm`, register heap-bound `GrobValue` constructions through
it, and run a periodic walk over reachable VM state to identify which
entries are still live at the VM-semantic level. Even this path does
not replace the .NET GC; it sits above it, identifying retention
patterns that the platform's collector cannot see (e.g. closure-
captured arrays retained beyond their useful lifetime). That is a
Grob-aware memory-introspection feature — already noted as deferred
post-v1 in D-302 — not a competitor to the platform collector.

---

## Plugins and Native Functions

### The Core Mechanism

Some function objects contain bytecode. Others contain native C# code.
The VM dispatches transparently — Grob scripts can’t tell the difference.

```csharp
abstract class GrobFunction
{
    public string Name { get; init; }
    public int Arity { get; init; }
}

class BytecodeFunction : GrobFunction
{
    public Chunk Bytecode { get; init; }
}

class NativeFunction : GrobFunction
{
    public Func<Value[], Value> Implementation { get; init; }
    public FunctionSignature Signature { get; init; }  // for type checker
}
```

### VM Dispatch

```csharp
case OpCode.Call:
    switch (function)
    {
        case BytecodeFunction bf:
            PushFrame(bf, argCount);        // execute bytecode
            break;
        case NativeFunction nf:
            var args = PopArgs(argCount);
            var result = nf.Implementation(args);
            Push(result);                   // call C# directly
            break;
    }
    break;
```

### Registering Native Functions

```csharp
vm.RegisterNative("print",
    signature: new FunctionSignature(
        parameters: [new Parameter("value", GrobType.Any)],
        returnType: GrobType.Nil
    ),
    implementation: args => {
        Console.WriteLine(args[0].ToString());
        return Value.Nil;
    }
);
```

### The Plugin Interface

```csharp
public interface IGrobPlugin
{
    string Name { get; }
    void Register(GrobVM vm);
}
```

A plugin is a C# class library implementing `IGrobPlugin`. It registers
native functions when loaded:

```csharp
// Grob.Http.dll
public class HttpPlugin : IGrobPlugin
{
    public string Name => "Grob.Http";

    public void Register(GrobVM vm)
    {
        vm.RegisterNative("http.get",
            signature: new FunctionSignature(
                parameters: [new Parameter("url", GrobType.String)],
                returnType: GrobType.String
            ),
            implementation: args => {
                var url = args[0].AsString();
                var response = new HttpClient().GetStringAsync(url).Result;
                return new StringValue(response);
            }
        );
    }
}
```

### Loading Plugins

```csharp
grob run script.grob --dev-plugin Grob.Http.dll
```

```csharp
private void LoadPlugin(string path)
{
    var assembly = Assembly.LoadFrom(path);
    var pluginType = assembly.GetTypes()
        .First(t => typeof(IGrobPlugin).IsAssignableFrom(t));
    var plugin = (IGrobPlugin)Activator.CreateInstance(pluginType)!;
    plugin.Register(this);
}
```

### Type Safety at the Plugin Boundary

Plugins provide type signatures alongside implementations. The type checker
registers these and verifies call sites statically. A Grob script calling
a plugin function with wrong argument types gets a compile time error —
not a runtime crash.

This is essential for Grob’s identity as a statically typed language.
The type safety guarantee should not break at the native boundary.

### The Standard Library Is Just Plugins

`fs`, `strings`, `process` — all `IGrobPlugin` implementations registered
automatically at VM startup. This means:

- Standard library is independently testable
- Core VM can ship without the standard library
- Standard library can be updated without touching the VM
- Users can replace standard library functions with their own

### The Module System Connection

```grob
import Grob.Http
```

The import system is a managed plugin loader with namespace handling.
This is why the module system is a late-phase feature — it builds on
plugin architecture which builds on native function registration which
builds on VM function dispatch. Each layer depends on the one below.

---

## Complete Runtime Architecture

```
Grob Script
    ↓
Lexer → Parser → Type Checker → Compiler
    ↓
Bytecode Chunk
    ↓
VM — fetch/decode/execute loop
    ├── Value Stack      — ints/floats/bools live here directly (no heap allocation)
    ├── Call Frames      — one per active function call (max 256)
    ├── Heap             — strings/arrays/functions, managed by .NET GC
    ├── Globals          — built-ins + plugin functions
    └── Plugin Loader    — loads IGrobPlugin assemblies at startup
```

---

## Performance Notes

- Types resolved at compile time — zero runtime type checking overhead
- Local variables are stack slots — array indexing not dictionary lookup
- Call frames are a fixed array — no heap allocation per function call
- VM loop is flat — no recursion, no tree traversal
- Native functions call C# directly — no interpretation overhead
- GC pressure minimised by storing primitives directly in `GrobValue`'s
  scalar slot — `int`, `float`, `bool`, `nil` never allocate on the heap

For scripting use cases (file operations, automation, sysadmin tasks)
this architecture is comfortably fast. JIT compilation is explicitly
out of scope — a well-written bytecode VM is sufficient.

C# as the implementation language is the right call. The .NET JIT will
compile Grob’s VM loop to efficient native code. Don’t fight the platform.

---

## Implementation Order

> **Authority:** `grob-solution-architecture.md`. That document maps the
> build order to the assemblies it touches and is the authoritative
> reference for sequencing. Don’t design all of this upfront — build it
> in layers. Steps 1–2 use hand-constructed chunks in tests; the
> compiler is involved from step 3 onwards. Step 7 is split into 7a
> (plugin infrastructure — the `IGrobPlugin` interface) and 7b (the
> core stdlib modules as `IGrobPlugin` implementations). Step 9 is
> scoped to third-party plugin loading only.

Each layer is independently testable. Each one builds on the previous.

---

## Upvalue Lifecycle (D-325)

Upvalues are the mechanism by which a closure reads and writes locals of an enclosing
function after that function's call frame has been discarded. Each upvalue cell passes
through two states.

### Open and closed states

An **open** upvalue holds a reference to the `ValueStack` object and a slot index. Reads
and writes go directly through to the live stack slot. This is efficient while the
enclosing frame is still executing: no heap allocation is needed.

A **closed** upvalue has had its stack slot copied to a heap field (`_closedValue`) inside
the `Upvalue` object itself. The `ValueStack` reference is nulled and the slot index set
to `-1`. Reads and writes go to `_closedValue`. The closure now owns the captured value
independently of any stack.

The transition — open → closed — is called **closing** the upvalue.

### The open-upvalue list

`VirtualMachine` holds `_openUpvalues`, a `List<Upvalue>`. Every open upvalue for the
current execution is registered in this list. The list is cleared at the start of each
top-level `Run()` so open upvalues cannot leak across re-entrant or sequential script
runs.

### Capture at `OP_CLOSURE`

When the VM executes `OP_CLOSURE`, it iterates the upvalue descriptor bytes emitted by
the compiler. For each descriptor that marks a **local** capture (a variable in the
immediately enclosing function), the VM calls `CaptureUpvalue(absoluteSlot)`:

1. Walk `_openUpvalues`. If an existing open upvalue already points at `absoluteSlot`,
   reuse it. Two closures created in the same enclosing frame therefore share one
   `Upvalue` cell for the same slot, so a write through one is immediately visible via
   the other.
2. Otherwise, allocate a fresh `Upvalue(_stack, absoluteSlot)` and append it to
   `_openUpvalues`.

For a **transitive** capture (a variable in a grandparent or deeper enclosing scope),
the VM copies the upvalue reference from the enclosing closure's own upvalue array — no
new cell is created.

### Frame-exit close sweep

When `OP_RETURN` executes for a non-top-level frame, the VM calls
`CloseUpvaluesFrom(_stackBase)` before discarding the frame's stack slots. The sweep
iterates `_openUpvalues` in reverse and closes every upvalue whose slot index is at or
above `_stackBase`. Closed upvalues are removed from the list.

Because the sweep is driven by the **stack location** (the slot index relative to the
frame base) rather than by inspecting the return value, every open upvalue in the frame
is closed regardless of which heap object holds a reference to the closure — whether the
closure is the direct return value, an element of an array, a map value or a struct
field. This is the D-325 fix; value-based closing missed closures that escaped
indirectly through containers.

The same sweep runs at `OP_CLOSE_UPVALUE`, which the compiler emits at block-scope
exits inside a function body where a local captured by an inner closure goes out of
scope before the function returns.

### Post-return invariant (D-325)

In `DEBUG` builds the VM asserts after each `CloseUpvaluesFrom` sweep that no open
upvalue in the list has a slot index at or above the frame base that just returned. A
violation means the sweep missed an upvalue; the assertion converts that class of bug
from a later value-stack underflow into an immediate, located `GrobInternalException`.

---

## Key Decisions — Resolved Reference

| Decision             | Notes                                                                                                                                                                                            |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Value representation | **Resolved — May 2026 (D-303).** Tagged union struct, 24 bytes on x64. See "GrobValue Representation" above. NaN boxing rejected — managed-runtime mismatch, full rationale in D-303 and OQ-005. |
| Error handling model | **Resolved — April 2026.** Exceptions with try/catch. See decisions log OQ-004.                                                                                                                  |
| GC strategy          | **Resolved — May 2026 (D-304).** Lean on .NET GC; no custom mark-and-sweep in v1. Full rationale in D-304 and OQ-006.                                                                            |
| Concurrent GC        | Not needed for scripting use case — future consideration                                                                                                                                         |
| JIT compilation      | Explicitly out of scope                                                                                                                                                                          |

---

_Updated May 2026 — OQ-005 and OQ-006 closed (D-303, D-304). "GrobValue_
_Provisional Representation" section renamed to "GrobValue Representation";_
_provisional framing removed throughout; "Deferred to OQ-005" subsection_
_replaced by definitive "Locked contract." The speculative "Garbage_
_Collection" section (Mark and Sweep algorithm sketch, allocation threshold_
_pseudocode, custom GC pauses note) replaced by "Memory Management" — lean_
_on .NET GC, what lives where, what does not exist in v1, pressure profile,_
_revisit conditions. Runtime architecture diagram no longer lists a custom_
_GC component. Deferred-decisions table converted to a resolved-decisions_
_reference._
_Previous: April 2026 — GrobValue provisional representation locked (OQ-009 resolved):_
_hand-rolled tagged-union struct under .NET 10 LTS, nine-variant kind enum, 24 bytes on x64,_
_encapsulation boundary specified, .NET 11 [Union] migration signposted; bytecode file_
_format now points to grob-grobc-format.md as authoritative (OQ-010 resolved)._
_Previous: implementation order clarified: step 7 split into 7a (plugin_
_infrastructure) and 7b (stdlib modules); step 9 explicitly scoped to third-party plugin_
_loading only; compiler involvement from step 3 onwards made explicit; GC step 8 no-op_
_note added; `guid` confirmed as 13th core module in step 7b._
