# VM Architecture Overview

Grob uses a stack-based bytecode VM written in C# .NET.

## Pipeline

```
Grob Script
    ↓
Lexer → Parser → Type Checker → Optimiser → Compiler
    ↓
Bytecode Chunk
    ↓
VM — fetch/decode/execute loop
    ├── Value Stack      — ints/floats/bools live here (no GC)
    ├── Call Frames      — one per active function call (max 256)
    ├── Globals          — built-ins + plugin functions
    └── Plugin Loader    — loads IGrobPlugin assemblies
```

The compiler is the intelligent part. The VM is deliberately dumb — it executes
decisions already made at compile time.

## Solution Structure

Six projects in a strict DAG dependency graph:

- `Grob.Core` — shared types (`GrobValue`, `GrobType`, `SourceLocation`)
- `Grob.Runtime` — `IGrobPlugin`, `FunctionSignature`, exception hierarchy
- `Grob.Compiler` — lexer, parser, type checker, optimiser, code generator
- `Grob.Vm` — fetch-decode-execute loop, value stack, call frames
- `Grob.Stdlib` — thirteen core modules as `IGrobPlugin` implementations
- `Grob.Cli` — `grob` command-line entry point

`Grob.Compiler` and `Grob.Vm` never reference each other.

See also: [Instruction Set](Instruction-Set.md),
[Value Representation](Value-Representation.md)
