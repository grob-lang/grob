# VM Architecture — Overview

Grob uses a stack-based bytecode VM written in C# .NET. The compiler walks the
AST and emits a flat instruction stream. The VM is a dumb fetch-decode-execute
loop — all intelligence is in the compiler.

## Pipeline

```
Source → Lexer → Parser → AST → Type Checker → Optimiser → Compiler → Bytecode → VM
```

The type checker walks the AST before any bytecode is emitted. If the program is
not type-safe, compilation stops. The VM never sees a type-unsafe program.

## Architecture

```
VM — fetch/decode/execute loop
├── Value Stack      — ints/floats/bools live here directly (no GC)
├── Call Frames      — one per active function call (max 256)
├── Heap             — strings/arrays/functions, tracked by GC
├── Globals          — built-ins + plugin functions
├── GC               — mark and sweep, or lean on C#
└── Plugin Loader    — loads IGrobPlugin assemblies at startup
```

## Execution Model

The primary use case is `grob run script.grob` — compilation happens in memory
and bytecode is never written to disk unless explicitly requested. Compile time
for typical scripts: single digit milliseconds.

The `.grobc` binary format exists for optional caching. If source has not changed
since last run, the cached bytecode can be loaded instead of recompiling.

## Key Design Decisions

**Types resolved at compile time** — zero runtime type checking overhead. The
type checker annotates every expression node with a resolved type. The compiler
reads these annotations to emit the right opcodes.

**Local variables are stack slots** — array indexing, not dictionary lookup.
Arguments pushed by the caller become the first locals automatically.

**Call frames are a fixed array** — `CallFrame[256]`, no heap allocation per
function call.

**FOR lowered to WHILE** — the compiler desugars range loops. The VM never sees
FOR opcodes.

**Visitor pattern for the AST** — three passes (type checker, optimiser,
compiler) walk the same AST. The visitor earns its place here.

**Partial classes for the compiler** — physical separation of concerns with the
same namespace and architecture.

## Implementation Order

1. `Grob.Core` — `Chunk`, `OpCode`, `GrobType`, constant pool, `CONSTANT`/`RETURN`
2. `Grob.Vm` — value stack, arithmetic opcodes (chunks hand-constructed in tests)
3. `Grob.Vm` + `Grob.Compiler` — global variables
4. `Grob.Vm` + `Grob.Compiler` — control flow, conditional/unconditional jump, backpatching
5. `Grob.Vm` + `Grob.Compiler` — local variables, call frames
6. `Grob.Vm` + `Grob.Compiler` — functions, `CALL`/`RETURN`, `CallFrame` push/pop
   7a. `Grob.Runtime` — plugin infrastructure: `IGrobPlugin`, `IPluginRegistrar`, `FunctionSignature`, `NativeCatalogue` — one registration pass the type checker and the VM both read (D-426)
   7b. `Grob.Stdlib` — all thirteen core modules as `IGrobPlugin` implementations
7. GC — current decision: lean on C# entirely; this step is a no-op on that path
8. `Grob.Vm` (`PluginLoader`) — third-party plugin loading: `Assembly.LoadFrom`, `--dev-plugin`
9. `Grob.Compiler` — import resolution, module system, namespace aliasing

Each layer is independently testable. Each builds on the previous. Steps 1–2 use
hand-constructed chunks in tests — the compiler is not yet involved.

## Performance

For scripting use cases (file operations, automation, sysadmin tasks) this
architecture is comfortably fast. JIT compilation is explicitly out of scope.
C# as the implementation language means the .NET JIT compiles the VM loop to
efficient native code.

See also: [Instruction Set](Instruction-Set.md),
[Value Representation](Value-Representation.md),
[Call Frames](Call-Frames.md),
[GC Strategy](GC-Strategy.md)
