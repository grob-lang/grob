# Grob — Solution Architecture

> Confirmed April 2026. This document is the authoritative reference for
> Grob's solution structure, assembly responsibilities, and dependency graph.
> It supersedes any structural notes in the VM Architecture design notes.

-----

## Solution Structure

```
Grob.sln
├── src/
│   ├── Grob.Core/              ← Shared primitives — Chunk, OpCode, GrobType, value types
│   ├── Grob.Runtime/           ← Plugin contract — IGrobPlugin, GrobVM surface, FunctionSignature
│   ├── Grob.Compiler/          ← Lexer, Parser, AST, TypeChecker, Compiler
│   ├── Grob.Vm/                ← VM execution engine — fetch/decode/execute, value stack, call frames
│   ├── Grob.Stdlib/            ← Core stdlib as IGrobPlugin implementations
│   ├── Grob.Cli/               ← Entry point — grob.exe, REPL, CLI commands, composition root
│   └── Grob.Lsp/               ← Language server — LSP implementation (Phase 4)
├── plugins/
│   ├── Grob.Http/              ← First-party HTTP plugin — reference implementation
│   ├── Grob.Crypto/            ← First-party checksums/hashing plugin
│   └── Grob.Zip/               ← First-party archive plugin
├── tests/
│   ├── Grob.Core.Tests/
│   ├── Grob.Compiler.Tests/
│   ├── Grob.Vm.Tests/
│   ├── Grob.Stdlib.Tests/
│   └── Grob.Integration.Tests/
└── tooling/
    └── Grob.VsCode/             ← VS Code extension (TypeScript) — Phase 1 + Phase 3
        ├── package.json
        ├── syntaxes/
        │   └── grob.tmLanguage.json
        └── src/
            └── extension.ts
```

-----

## Assembly Responsibilities

### Grob.Core

Shared primitives consumed by both `Grob.Compiler` and `Grob.Vm`. Neither project
should reference the other — `Grob.Core` is the only coupling between them.

**Contents:**

- `Chunk` — instruction bytes plus constant pool; the compiler's output and the VM's input
- `OpCode` — the complete bytecode instruction set enum
- `GrobType` — the type system enum used by both the type checker and the VM
- Value representation types — `GrobValue` (tagged union or equivalent), primitive structs
- `ConstantPool` — constant storage accessed by both compiler (write) and VM (read)
- `SourceLocation` — line/column information for error reporting

**Key constraint:** `Grob.Core` has no dependencies on any other Grob assembly.
It is the foundation — anything that would create a circular dependency belongs elsewhere.

**Naming note:** The prefix is always `Grob` in full. `Gro` as an abbreviation is
not a convention in this codebase. `GrobType` not `GroType`. `GrobValue` not `GroValue`.

---

### Grob.Runtime

The public contract for the plugin ecosystem. Published as a NuGet package.
Plugin authors reference `Grob.Runtime` — nothing else.

**Contents:**

- `IGrobPlugin` — the plugin interface (`string Name`, `void Register(GrobVM vm)`)
- `GrobVM` — the registration surface exposed to plugins (not the full VM implementation)
- `FunctionSignature` — type signature for native function registration
- `Parameter` — parameter name + `GrobType` pairing
- `GrobError` hierarchy — `GrobError`, `IoError`, `NetworkError`, `JsonError`,
  `ProcessError`, `NilError`, `RuntimeError`
- `ExitSignal` — uncatchable internal signal for `exit()` unwinding (internal use)
- `AuthHeader` — opaque type for HTTP auth; lives here because `Grob.Http` references
  `Grob.Runtime` and `AuthHeader` is part of the type-safe plugin boundary

**Key constraint:** `Grob.Runtime` references `Grob.Core` (for `GrobType`, `GrobValue`).
It does not reference `Grob.Compiler` or `Grob.Vm` (the full implementation).

**NuGet versioning:** `Grob.Runtime` is versioned independently from the runtime.
Plugin authors declare which version they target. This is the compatibility contract.

---

### Grob.Compiler

Everything from source text to `Chunk`. Three passes in sequence.

**Contents:**

- `Lexer` — tokenises source text; reports all errors, never stops at first
- `Parser` — produces AST from token stream; reports all errors, never stops at first
- AST node types — one type per language construct
- `TypeChecker` — visitor over the AST; resolves types, infers where possible,
  validates method calls against the type registry, reports all errors
- `Compiler` — visitor over the type-annotated AST; emits bytecode into a `Chunk`
- `TypeRegistry` — compile-time knowledge of all built-in type methods and properties;
  populated at startup from `Grob.Stdlib` registrations
- `Optimiser` — optional pass between type checker and compiler; deferred to post-MVP

**Visitor pattern:** All three passes use the visitor pattern. Grob has multiple passes
over the same AST — visitor earns its place here. Implemented as `partial class` files
for physical separation of concerns within the same namespace.

**Key constraint:** `Grob.Compiler` references `Grob.Core` and `Grob.Runtime`.
It does not reference `Grob.Vm`. The compiler's job ends at `Chunk` production.

**Error strategy:** Compiler and type checker collect ALL errors before execution.
A program with type errors never reaches the VM. The VM stops on the FIRST runtime error.

**LSP dependency:** `Grob.Lsp` is a consumer of `Grob.Compiler`. Every AST node
must carry a `SourceLocation` and every identifier node must carry a `Declaration`
back-reference set by the type checker. This is a day-one compiler construction
requirement — see `Grob___Tooling___Strategy.md`.

---

### Grob.Vm

The execution engine. Receives a `Chunk`; runs it.

**Contents:**

- `VirtualMachine` — the fetch/decode/execute loop
- `ValueStack` — the operand stack; structs for value types, no heap allocation
- `CallFrame` — one per active function call; stored in a fixed `CallFrame[256]` array
- `Globals` — built-in and plugin-registered native functions
- `PluginLoader` — loads `IGrobPlugin` assemblies; invokes `Register(GrobVM)`
- `GcStrategy` — C# GC is the primary strategy; this is a placeholder for any
  explicit GC cooperation if required (tentative: lean on C# entirely)
- `Upvalue` — closed-over variable support for lambdas and closures

**Key constraint:** `Grob.Vm` references `Grob.Core` and `Grob.Runtime`.
It does not reference `Grob.Compiler`. The VM has no knowledge of how a `Chunk`
was produced.

---

### Grob.Stdlib

All core modules implemented as `IGrobPlugin`. Registered automatically at VM startup
by `Grob.Cli` — not hardwired into the VM itself.

**One plugin class per module:**

| Class | Module | Notes |
|---|---|---|
| `FsPlugin` | `fs` | File system operations; registers `File` type |
| `StringsPlugin` | `strings` | `strings.join()` only; all other ops are `string` type methods |
| `JsonPlugin` | `json` | Read, parse, write, stdin, stdout |
| `CsvPlugin` | `csv` | Read, parse, write, stdin, stdout; registers `CsvTable`, `CsvRow` types |
| `EnvPlugin` | `env` | Environment variables and `env.require()` |
| `ProcessPlugin` | `process` | `run`, `runShell`, fail variants; registers `ProcessResult` type |
| `DatePlugin` | `date` | Registers `date` type and all date functions |
| `MathPlugin` | `math` | Constants and mathematical functions |
| `LogPlugin` | `log` | Levelled diagnostic output to stderr |
| `RegexPlugin` | `regex` | Registers `Regex` and `Match` types; module-level convenience functions |
| `PathPlugin` | `path` | Path string manipulation — no I/O |
| `FormatPlugin` | `format` | Human-readable output formatters |
| `GuidPlugin` | `guid` | GUID generation (v4, v5, v7), parsing; registers `guid` type |

**Key constraint:** `Grob.Stdlib` references `Grob.Core` and `Grob.Runtime` only.
It is independently testable — register a plugin into a VM instance and assert outputs.

---

### Grob.Cli

The composition root and entry point. Wires everything together.

**Contents:**

- `Program` — CLI entry point; parses command line args
- `RunCommand` — compiles source, registers stdlib plugins, executes via VM
- `ReplCommand` — interactive REPL with `G>` prompt
- `InstallCommand` — `grob install`, `grob restore`
- `FmtCommand` — `grob fmt`
- `SearchCommand` — `grob search`
- `InitCommand` — `grob init`
- `CheckCommand` — `grob check` (linting, credential pattern warnings)
- `WindowsTerminalProfile` — ships the Windows Terminal profile
- Error formatter — produces Grob-quality diagnostics from compiler and VM errors

**Key constraint:** `Grob.Cli` references all other `src/` assemblies. Nothing
else references `Grob.Cli`. It is the only composition point in the solution.

---

### Grob.Lsp

The language server. Runs as a standalone process started by the VS Code extension.
Consumes `Grob.Compiler` to provide editor intelligence — never executes scripts.

**Contents:**

- `Program` — entry point; wires stdin/stdout to `OmniSharp.Extensions.LanguageServer`
- `DiagnosticsHandler` — runs the type checker on file change; pushes errors to the editor
- `CompletionHandler` — resolves type at cursor position; queries `TypeRegistry` for members
- `HoverHandler` — returns resolved type of identifier at cursor
- `DefinitionHandler` — returns `Declaration.Location` for identifier at cursor

**Key constraint:** `Grob.Lsp` references `Grob.Compiler`, `Grob.Core`, `Grob.Runtime`.
It does not reference `Grob.Vm`. The LSP analyses code — it never runs it.

---

### plugins/ — First-Party Plugins

`Grob.Http`, `Grob.Crypto`, `Grob.Zip` live in `plugins/` and are built and tested
exactly as a third-party plugin would be. They reference `Grob.Runtime` only.
If they require special treatment, the plugin model is broken.

`Grob.Http` is the reference implementation for the plugin ecosystem — the most
complete example of what a well-authored plugin looks like.

-----

## Dependency Graph

The dependency graph is a DAG — no cycles.

```
Grob.Cli
  ├── Grob.Compiler
  │     ├── Grob.Core
  │     └── Grob.Runtime
  │           └── Grob.Core
  ├── Grob.Vm
  │     ├── Grob.Core
  │     └── Grob.Runtime
  └── Grob.Stdlib
        ├── Grob.Core
        └── Grob.Runtime

Grob.Lsp
  ├── Grob.Compiler
  │     ├── Grob.Core
  │     └── Grob.Runtime
  ├── Grob.Core
  └── Grob.Runtime

plugins/Grob.Http
  └── Grob.Runtime
        └── Grob.Core

plugins/Grob.Crypto
  └── Grob.Runtime

plugins/Grob.Zip
  └── Grob.Runtime
```

The critical rule: `Grob.Compiler` and `Grob.Vm` never reference each other.
`Grob.Core` is the only shared ground between them.

-----

## The Chunk Boundary

`Chunk` is the interface between compiler and VM. It lives in `Grob.Core` because:

- Both `Grob.Compiler` (produces chunks) and `Grob.Vm` (consumes chunks) need it
- Neither should reference the other
- `Grob.Runtime` focuses on the plugin contract, not internal execution primitives

`Chunk` is a data structure: instruction bytes plus a constant pool. It carries no
behaviour that belongs to either compiler or VM. It is the right resident for `Grob.Core`.

-----

## Testing Strategy

Each project is independently testable in isolation.

| Test project | What it tests | Approach |
|---|---|---|
| `Grob.Core.Tests` | Value representation, chunk construction, opcode encoding | Unit |
| `Grob.Compiler.Tests` | Given source → assert emitted bytecode. Primary bug surface | Unit |
| `Grob.Vm.Tests` | Given hand-constructed chunks → assert execution results | Unit |
| `Grob.Stdlib.Tests` | Register plugin into VM instance → assert function outputs | Unit |
| `Grob.Integration.Tests` | Full pipeline: source → compile → run → assert output | Integration |

Compiler output tests are the highest priority. Bugs live in the compiler.
The VM loop can be trusted once verified on simple cases.

-----

## Implementation Order

The solution structure maps directly onto the locked implementation order.
Steps 1–2 use hand-constructed chunks in tests — the compiler is not yet involved.
Steps 3 onwards name both assemblies where both are touched.

1. `Grob.Core` — `Chunk`, `OpCode`, `GrobType`, constant pool, `CONSTANT`/`RETURN`
2. `Grob.Vm` — value stack, arithmetic opcodes (chunks hand-constructed in tests — no compiler yet)
3. `Grob.Vm` + `Grob.Compiler` — global variables — `LOAD_GLOBAL`, `STORE_GLOBAL`
4. `Grob.Vm` + `Grob.Compiler` — control flow — conditional/unconditional jump, backpatching
5. `Grob.Vm` + `Grob.Compiler` — local variables, call frames
6. `Grob.Vm` + `Grob.Compiler` — functions — `CALL`/`RETURN`, `CallFrame` push/pop
7a. `Grob.Runtime` — plugin infrastructure — `IGrobPlugin`, `RegisterNative`,
    `FunctionSignature`, `Parameter`. This is the registration mechanism the stdlib
    depends on. It is not the user-facing plugin loader — that is step 9.
7b. `Grob.Stdlib` — all thirteen core modules as `IGrobPlugin` implementations,
    auto-registered at VM startup: `fs`, `strings`, `json`, `csv`, `env`, `process`,
    `date`, `math`, `log`, `regex`, `path`, `format`, `guid`
8. GC — tentative: lean on C# entirely; if a custom GC is required, implement here.
   In the lean-on-C# path (current decision) this step is a no-op.
9. `Grob.Vm` (`PluginLoader`) — third-party plugin loading — `Assembly.LoadFrom`,
   `PluginLoader`, `--dev-plugin` flag. `Grob.Http` is the first exercise of this path.
   Distinct from step 7a: that step defines the interface; this step opens it to external assemblies.
10. `Grob.Compiler` — import resolution, module system, namespace aliasing

-----

## Naming Conventions

- Assembly prefix: always `Grob` in full — never abbreviated to `Gro`
- Type prefix: `Grob` for public runtime types (`GrobType`, `GrobValue`, `GrobError`)
- Internal types: no prefix required — namespace provides disambiguation
- C# conventions: `PascalCase` for types and public members, `camelCase` for locals
- `partial class` used throughout `Grob.Compiler` for physical separation of concerns
- Structs for value types (no GC pressure). Classes for heap objects only.
- British English in all documentation and comments.

-----

*Confirmed April 2026. Updated April 2026 — `Grob.Lsp` and `tooling/Grob.VsCode` added.*
*Updated April 2026 — implementation order clarified: step 7 split into 7a (plugin infrastructure)*
*and 7b (stdlib modules); step 9 explicitly scoped to third-party plugin loading only;*
*compiler involvement from step 3 onwards made explicit; GC step 8 no-op note added.*
*`guid` confirmed as 13th core module in step 7b.*
*Supersedes structural notes in Grob___VM_Architecture___Design_Notes.md.*
*See `Grob___Tooling___Strategy.md` for LSP phased plan and SourceLocation requirements.*
*`Gro` as a type prefix abbreviation is explicitly not a Grob convention — always `Grob`.*
