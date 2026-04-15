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

-----

## The Bytecode File Format

### Structure

A compiled Grob file (`.grobc`) is a structured binary file:

```
[magic number]        4 bytes — "GROB" in ASCII (0x47 0x52 0x4F 0x42)
[version]             1 byte  — Grob version that compiled this
[constant pool size]  2 bytes
[constants...]        variable — each prefixed with type byte
[bytecode size]       4 bytes
[bytecode...]         variable — raw instruction stream
[function count]      2 bytes
[functions...]        variable — each is its own mini chunk
```

### In-Memory Execution

For Grob’s primary use case — `grob run script.grob` — compilation happens
in memory and bytecode is never written to disk unless explicitly requested.

Compile time for typical scripts: single digit milliseconds. Invisible to users.

The file format still matters for optional caching — if source hasn’t changed
since last run, load the cached `.grobc` instead of recompiling.

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

Don’t design all of this upfront. Build it in layers.
Steps 1–2 use hand-constructed chunks in tests — the compiler is not yet involved.
Steps 3 onwards touch both VM and compiler.

1. Chunk + constant pool + a few opcodes (`CONSTANT`, `RETURN`)
2. Value stack + arithmetic opcodes (chunks hand-constructed in tests — no compiler yet)
3. Global variables — `LOAD_GLOBAL`, `STORE_GLOBAL` — VM + compiler both involved
4. Control flow — conditional/unconditional jump, backpatching — VM + compiler
5. Local variables + call frames — VM + compiler
6. Functions + `CALL`/`RETURN`, `CallFrame` push/pop — VM + compiler
7a. Plugin infrastructure — `IGrobPlugin`, `RegisterNative`, `FunctionSignature`,
    `Parameter` (`Grob.Runtime`). This is the registration mechanism the stdlib
    depends on. It is not the user-facing plugin loader — that is step 9.
7b. Core stdlib — all thirteen modules as `IGrobPlugin` implementations, auto-registered
    at VM startup (`Grob.Stdlib`): `fs`, `strings`, `json`, `csv`, `env`, `process`,
    `date`, `math`, `log`, `regex`, `path`, `format`, `guid`
8. GC — tentative: lean on C# entirely; if a custom GC is required, implement here.
   In the lean-on-C# path (current decision) this step is a no-op.
9. Third-party plugin loading — `Assembly.LoadFrom`, `PluginLoader`, `--dev-plugin` flag
   (`Grob.Vm`). `Grob.Http` is the first exercise of this path. Distinct from step 7a:
   that step defines the interface; this step opens it to external assemblies.
10. Import resolution + module system + namespace aliasing (`Grob.Compiler`)

Each layer is independently testable. Each one builds on the previous.

-----

## Key Decisions Deferred

|Decision            |Notes                                                                          |
|--------------------|-------------------------------------------------------------------------------|
|Value representation|Tagged union vs NaN boxing — decide after clox                                 |
|Error handling model|**Resolved — April 2026.** Exceptions with try/catch. See decisions log OQ-004.|
|GC strategy         |Lean on C# GC vs custom mark/sweep — lean on C# is likely right                |
|Concurrent GC       |Not needed for scripting use case — future consideration                       |
|JIT compilation     |Explicitly out of scope                                                        |

-----

*Nothing in this document is final until SharpBASIC retrospective is written*
*and clox is fully worked through. These are design intentions, not commitments.*
*Update with dated entries as decisions are confirmed during formal design phase.*
*Updated April 2026 — implementation order clarified: step 7 split into 7a (plugin*
*infrastructure) and 7b (stdlib modules); step 9 explicitly scoped to third-party plugin*
*loading only; compiler involvement from step 3 onwards made explicit; GC step 8 no-op*
*note added; `guid` confirmed as 13th core module in step 7b.*