# Grob — VM Architecture & Design Notes

> Captured from evening design session, February 2026.
> Deep dive into bytecode VM concepts, type checking, GC, and plugins.
> Informs Grob’s implementation after SharpBASIC and clox are complete.

-----

## Bytecode VM — Core Concepts

### What a Bytecode VM Is

A bytecode VM replaces tree-walking execution with two distinct stages:

- **The compiler** — walks the AST and emits a flat sequence of simple instructions (bytecode)
- **The VM** — a tight loop that reads one instruction, executes it, reads the next

The compiler is the intelligent part. The VM is deliberately dumb — it just executes decisions already made at compile time.

### Comparison to SharpBASIC’s Evaluator

|SharpBASIC Evaluator        |Grob Bytecode VM           |
|----------------------------|---------------------------|
|AST node                    |Opcode + operands          |
|`EvaluateExpression`        |Fetch-decode-execute loop  |
|Pattern match on node type  |Switch on opcode byte      |
|Recursive tree walk         |Linear instruction sequence|
|SymbolTable dictionary      |Stack slots + globals array|
|Call stack via C# call stack|Explicit call frames array |

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

-----

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

-----

## GrobValue Provisional Representation

`GrobValue` is the runtime value type used everywhere the VM stores or
moves a Grob value: the operand stack, local slots, the globals table,
the constant pool, plugin call boundaries. Every Grob value at runtime
is a `GrobValue`.

This section locks the v1 representation. The full value-representation
decision (OQ-005 — tagged union versus NaN boxing) is deferred until clox
is complete; this section is the **provisional** answer that lets `Grob.Core`
ship before that decision lands. The encapsulation rules below ensure that
when OQ-005 resolves, the change is confined to `Grob.Core/GrobValue.cs`.

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
    // Provisional representation — pending OQ-005.
    // Do NOT access these fields outside Grob.Core. The public API
    // below is the encapsulation boundary; OQ-005 may rework the
    // internal layout, but the boundary is stable.
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

|Kind     |`_scalar`                              |`_reference`               |
|---------|---------------------------------------|---------------------------|
|`Nil`    |`0`                                    |`null`                     |
|`Bool`   |`0` for false, `1` for true            |`null`                     |
|`Int`    |the `long` value directly              |`null`                     |
|`Float`  |`BitConverter.DoubleToInt64Bits(value)`|`null`                     |
|`String` |`0`                                    |the `string`               |
|`Array`  |`0`                                    |`GrobArray` instance       |
|`Map`    |`0`                                    |`GrobMap` instance         |
|`Struct` |`0`                                    |`GrobStruct` instance      |
|`Function`|`0`                                   |`GrobFunction` instance    |

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
The internal field layout is implementation detail and may change when
OQ-005 resolves.

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

|Same kind                |`Equals` semantics                                   |
|-------------------------|-----------------------------------------------------|
|`Nil` == `Nil`           |always true                                          |
|`Bool` == `Bool`          |scalar equality                                      |
|`Int` == `Int`            |scalar equality                                      |
|`Float` == `Float`        |IEEE 754: `NaN != NaN`, `+0.0 == -0.0` (C# default) |
|`String` == `String`      |ordinal value equality (`string.Equals(a, b, Ordinal)`)|
|`Array` == `Array`        |reference equality                                   |
|`Map` == `Map`            |reference equality                                   |
|`Struct` == `Struct`      |delegate to `GrobStruct.Equals` (field-by-field)     |
|`Function` == `Function`  |reference equality                                   |
|*different kinds*        |always false                                         |

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

`Grob.Core.Tests/GrobValueTests.cs` covers the provisional representation:

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

### Provisional contract — what this section locks, what it defers

**Locked for v1:**

- The public API surface above. Plugins, the compiler and the VM use
  this surface.
- `default(GrobValue)` is `Nil`.
- The nine-variant `GrobValueKind` enum.
- The equality and hashing rules.
- The .NET 10 LTS target.

**Deferred to OQ-005 — confined to `Grob.Core`:**

- The internal field layout. NaN boxing would replace the
  `_scalar`/`_reference` pair with a single `ulong _bits` field and a
  payload-aware decoder. The factories and accessors keep their
  signatures; their bodies change.
- Whether `GrobValueKind` continues to be stored explicitly or becomes
  derived from the bit pattern.

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
The two migrations (OQ-005 representation change; `[Union]` annotation)
are independent — either can land first.

-----

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
[instruction stream]  variable — flat opcode stream per ADR-0008
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

-----

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

-----

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

-----

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

-----

## Garbage Collection

### What Needs Collecting

**Value types** — live directly on the stack, no GC needed:

- `int`, `float`, `bool` — fixed size, stack allocated, gone when frame pops

**Heap types** — variable size, can be referenced from multiple places:

- `string`, `array`, `function` — live on the heap, need GC tracking

### Mark and Sweep

The algorithm Grob will use. Two phases run periodically:

**Mark phase** — starting from all roots, follow every reference and mark
each reachable object:

```csharp
private void MarkRoots()
{
    for (int i = 0; i < _stackTop; i++)
        MarkValue(_stack[i]);

    foreach (var global in _globals.Values)
        MarkValue(global);

    for (int i = 0; i < _frameCount; i++)
        MarkObject(_frames[i].Function);
}
```

**Sweep phase** — walk every heap object. Unmarked = unreachable = free it.
Marked = clear the mark, keep for next cycle.

### Triggered Collection

Run GC when heap grows past a threshold, then double the threshold:

```csharp
private GrobObject Allocate(GrobObject obj)
{
    _heapSize += obj.Size;
    if (_heapSize > _heapThreshold)
    {
        CollectGarbage();
        _heapThreshold = _heapSize * 2;
    }
    obj.Next = _heapHead;
    _heapHead = obj;
    return obj;
}
```

### GC Pauses

Mark and sweep stops the program during collection. For scripting use cases
(file operations, automation) pauses are invisible. Document the limitation
honestly. Not a problem for Grob’s target use case.

### Grob’s GC Strategy — Lean on C#

Since Grob’s VM is written in C#, C#’s GC handles heap memory automatically.
The design work is in **value representation** — minimising heap allocations
in the hot path so GC pressure is low.

Use **structs** for value types (int, float, bool) — stack allocated, zero GC
pressure. Use **classes** only for heap objects (string, array, function).

```csharp
// Good — int lives on stack, no allocation
struct Value
{
    public ValueKind Kind;
    public long Raw;      // int/float/bool stored directly
    public object? Ref;   // string/array/function pointer — only when needed
}
```

Nystrom covers NaN boxing in clox — an alternative representation that packs
type information into unused bits of a 64-bit float. Worth understanding even
if Grob uses a tagged union instead.

-----

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

-----

## Complete Runtime Architecture

```
Grob Script
    ↓
Lexer → Parser → Type Checker → Compiler
    ↓
Bytecode Chunk
    ↓
VM — fetch/decode/execute loop
    ├── Value Stack      — ints/floats/bools live here directly (no GC)
    ├── Call Frames      — one per active function call (max 256)
    ├── Heap             — strings/arrays/functions, tracked by GC
    ├── Globals          — built-ins + plugin functions
    ├── GC               — mark and sweep, triggered by allocation threshold
    └── Plugin Loader    — loads IGrobPlugin assemblies at startup
```

-----

## Performance Notes

- Types resolved at compile time — zero runtime type checking overhead
- Local variables are stack slots — array indexing not dictionary lookup
- Call frames are a fixed array — no heap allocation per function call
- VM loop is flat — no recursion, no tree traversal
- Native functions call C# directly — no interpretation overhead
- GC pressure minimised by using structs for value types

For scripting use cases (file operations, automation, sysadmin tasks)
this architecture is comfortably fast. JIT compilation is explicitly
out of scope — a well-written bytecode VM is sufficient.

C# as the implementation language is the right call. The .NET JIT will
compile Grob’s VM loop to efficient native code. Don’t fight the platform.

-----

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

-----

## Key Decisions Deferred

|Decision            |Notes                                                                          |
|--------------------|-------------------------------------------------------------------------------|
|Value representation|**Provisional — April 2026.** Tagged union struct, 24 bytes on x64. See "GrobValue provisional representation" above. Full decision (OQ-005) deferred until clox is complete.|
|Error handling model|**Resolved — April 2026.** Exceptions with try/catch. See decisions log OQ-004.|
|GC strategy         |Lean on C# GC vs custom mark/sweep — lean on C# is likely right                |
|Concurrent GC       |Not needed for scripting use case — future consideration                       |
|JIT compilation     |Explicitly out of scope                                                        |

-----

*Nothing in this document is final until SharpBASIC retrospective is written*
*and clox is fully worked through. These are design intentions, not commitments.*
*Update with dated entries as decisions are confirmed during formal design phase.*
*Updated April 2026 — GrobValue provisional representation locked (OQ-009 resolved):*
*hand-rolled tagged-union struct under .NET 10 LTS, nine-variant kind enum, 24 bytes on x64,*
*encapsulation boundary specified, .NET 11 [Union] migration signposted; bytecode file*
*format now points to grob-grobc-format.md as authoritative (OQ-010 resolved).*
*Previous: implementation order clarified: step 7 split into 7a (plugin*
*infrastructure) and 7b (stdlib modules); step 9 explicitly scoped to third-party plugin*
*loading only; compiler involvement from step 3 onwards made explicit; GC step 8 no-op*
*note added; `guid` confirmed as 13th core module in step 7b.*
