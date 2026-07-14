# Grob v1 — Requirements Specification

> **Purpose:** This document defines exactly what Grob v1 includes. It is the
> build guide. Every feature listed here ships with the public release. Every
> feature not listed here does not.
>
> **Authority:** This document draws from the decisions log (the authority on
> all design decisions), the language fundamentals spec, the stdlib reference,
> the type registry, the VM architecture notes, the install strategy and the
> personality doc. Where those documents describe post-MVP features, this
> document excludes them. Where they describe v1 features, this document
> includes them and specifies acceptance criteria.
>
> **Methodology:** The build plan follows agile methodology. Each sprint
> produces a working, testable, valuable increment. Opcodes, enums and
> infrastructure are built out completely — never in an additive, drip-feed
> manner. Error handling and line tracking are present from day one.
>
> **Last updated:** April 2026

-----

## Table of Contents

1. [Success Criteria](#1-success-criteria)
2. [Architecture Overview](#2-architecture-overview)
3. [Cross-Cutting Concerns — Day One](#3-cross-cutting-concerns--day-one)
4. [Build Plan — Sprint Breakdown](#4-build-plan--sprint-breakdown)
5. [Language Features — Complete v1 Surface](#5-language-features--complete-v1-surface)
6. [Type System](#6-type-system)
7. [Standard Library — v1 Modules](#7-standard-library--v1-modules)
8. [CLI — v1 Commands](#8-cli--v1-commands)
9. [Plugin System](#9-plugin-system)
10. [Error Handling](#10-error-handling)
11. [Security](#11-security)
12. [Testing Strategy](#12-testing-strategy)
13. [Explicitly Out of Scope](#13-explicitly-out-of-scope)
14. [Validation Scripts](#14-validation-scripts)
15. [Definition of Done](#15-definition-of-done)

-----

## 1. Success Criteria

### Smoke Test — Calculator

A console-based non-scientific calculator written in Grob. This exercises
variables, arithmetic, control flow, functions, user input, string
conversion, `while` loops and `print()`. It was the original MVP criterion
from February 2026 and it remains the first milestone.

### Real-Program Target — File Organiser

The SharpBASIC retrospective identified that real programs reveal language
gaps that toy programs hide. The Sunken Crown was the most valuable design
tool in that project. Grob needs an equivalent.

The real-program target for v1 is: **a file organiser script that moves
files from a download folder into year/month subfolders based on file
date.** This is Script 2 from the sample scripts document — one of the
most common personal automation tasks on GitHub. It exercises:

- `param` block with typed, defaultable script parameters
- `fs.list()` and `File` type properties
- `date` type — component access (`year`, `month`)
- String interpolation
- `for...in` loop
- `if` conditional
- `fs.ensureDir()` and `file.moveTo()`
- `print()` for user feedback

### Release Gate — All Thirteen Sample Scripts

The sample scripts document contains thirteen real-world scripts that
validate the API surface. All thirteen must compile and run correctly
against the v1 implementation before public release. Any script that
fails reveals a gap in the implementation.

### Non-Functional Requirements

- **Startup time:** `grob run hello.grob` (a single `print("hello")`)
  completes in under 500ms cold, under 200ms warm. Scripting languages
  must feel instant.
- **Error quality:** Every error message includes the source file, line
  number, column number, what went wrong, and a suggested fix when the
  fix is obvious. No stack traces exposed to end users unless `--verbose`
  is set.
- **Windows-native:** Grob runs on Windows 11 without WSL. All paths use
  Windows conventions. All examples use Windows paths. The primary target
  user is a Windows developer or sysadmin.
- **Zero external dependencies at runtime:** `grob.exe` is a
  self-contained .NET deployment. No separate .NET install required by the
  end user.

-----

## 2. Architecture Overview

```
Grob Script (.grob)
    |
    v
  Lexer ──────── Token stream (with line/column tracking)
    |
    v
  Parser ─────── AST (with source location on every node)
    |
    v
  Type Checker ── Typed AST (collects ALL errors, never stops at first)
    |
    v
  Optimiser ──── Optimised AST (constant folding, dead code — minimal in v1)
    |
    v
  Compiler ───── Bytecode chunk (flat instruction stream + constant pool)
    |
    v
  VM ─────────── Fetch-decode-execute loop (stops on first UNHANDLED runtime error)
    |
    +── Value Stack (int/float/bool live here directly)
    +── Call Frames (CallFrame[256], fixed array)
    +── Globals (built-ins + plugin functions)
    +── Plugin Loader (IGrobPlugin assemblies at startup)
```

### C# Solution Structure

> **Authority:** `grob-solution-architecture.md` (confirmed April 2026).
> This section summarises the confirmed solution architecture. The full
> document covers assembly responsibilities, dependency constraints,
> naming conventions and the Chunk boundary rationale in detail.

```
Grob.sln
├── src/
│   ├── Grob.Core/              ← Shared primitives — Chunk, OpCode, GrobType, GrobValue, SourceLocation
│   ├── Grob.Runtime/           ← Plugin contract — IGrobPlugin, GrobVM registration surface, FunctionSignature, GrobError hierarchy
│   ├── Grob.Compiler/          ← Lexer, Parser, AST, TypeChecker, Compiler (partial classes by concern)
│   ├── Grob.Vm/                ← VM execution engine — fetch/decode/execute, ValueStack, CallFrame, PluginLoader, Upvalue
│   ├── Grob.Stdlib/            ← Core stdlib — one IGrobPlugin per module (FsPlugin, JsonPlugin, DatePlugin etc.)
│   └── Grob.Cli/               ← Entry point — grob.exe, REPL, CLI commands, composition root
├── plugins/
│   ├── Grob.Http/              ← First-party HTTP plugin — reference implementation
│   ├── Grob.Crypto/            ← First-party checksums/hashing plugin
│   └── Grob.Zip/               ← First-party archive plugin
├── tests/
│   ├── Grob.Core.Tests/
│   ├── Grob.Compiler.Tests/    ← Highest priority — this is where bugs will live
│   ├── Grob.Vm.Tests/
│   ├── Grob.Stdlib.Tests/
│   └── Grob.Integration.Tests/ ← End-to-end: source file → stdout/stderr/exit code
└── bench/
    └── Grob.Benchmarks/        ← BenchmarkDotNet harness — D-302 — per-sprint regression gate
```

### Dependency Graph (DAG — No Cycles)

```
Grob.Cli
  ├── Grob.Compiler ──→ Grob.Core + Grob.Runtime
  ├── Grob.Vm ─────────→ Grob.Core + Grob.Runtime
  └── Grob.Stdlib ─────→ Grob.Core + Grob.Runtime

plugins/Grob.Http ────→ Grob.Runtime → Grob.Core
```

**Critical rule:** `Grob.Compiler` and `Grob.Vm` never reference each
other. `Grob.Core` is the only shared ground between them. `Chunk` is
the boundary — the compiler produces it, the VM consumes it. Neither
knows about the other.

### Assembly Responsibilities (Summary)

|Assembly       |Responsibility                                                                          |Key Constraint                                                                |
|---------------|----------------------------------------------------------------------------------------|------------------------------------------------------------------------------|
|`Grob.Core`    |`Chunk`, `OpCode`, `GrobType`, `GrobValue`, `SourceLocation`, `ConstantPool`            |No dependencies on any other Grob assembly                                    |
|`Grob.Runtime` |`IGrobPlugin`, `FunctionSignature`, `GrobVM` registration surface, `GrobError` hierarchy|Published as NuGet package — plugin authors reference this only               |
|`Grob.Compiler`|Lexer → Parser → AST → TypeChecker → Compiler                                           |References Core + Runtime. Does NOT reference Vm. Job ends at Chunk production|
|`Grob.Vm`      |Fetch/decode/execute loop, ValueStack, CallFrame[256], Globals, PluginLoader            |References Core + Runtime. Does NOT reference Compiler                        |
|`Grob.Stdlib`  |13 core modules as `IGrobPlugin` implementations                                        |References Core + Runtime only. Auto-registered by Cli at startup             |
|`Grob.Cli`     |Composition root, CLI commands, REPL, error formatting                                  |References all `src/` assemblies. Nothing references Cli                      |

The v1 build comprises these six `src/` assemblies. `Grob.Lsp` is a
solution member (D-134) but is **not a v1 build target** — the LSP is
post-MVP (§13), so the assembly is absent from the v1 dependency graph
and from this table by design, not omission. When it is built, D-134
places it depending on `Grob.Compiler`, `Grob.Core` and `Grob.Runtime`,
never on `Grob.Vm`. The day-one `SourceLocation` and `Declaration`
back-reference requirements (§3.1.1) exist precisely so that `Grob.Lsp`
can be added later without retrofitting the compiler.

### Naming Convention

The prefix for all Grob runtime types is `Grob` in full. `Gro` as an
abbreviation is not a convention in this codebase. `GrobType` not
`GroType`. `GrobValue` not `GroValue`. Every Grob type name reads
unambiguously.

### Key Architectural Decisions

- **Visitor pattern** for the AST — three passes (type checker, optimiser,
  compiler) walk the same tree. Visitor earns its place here.
- **Partial classes** for the compiler — same namespace, physical separation
  by concern (expressions, statements, declarations, control flow).
- **Stack-based VM** — confirmed, not register-based. The .NET JIT compiles
  the VM loop to efficient native code.
- **Lean on .NET GC** (D-304) — primitives (`int`, `float`, `bool`, `nil`)
  stored directly in `GrobValue`'s scalar slot generate zero GC pressure;
  heap types (`string`, `GrobArray`, `GrobMap`, `GrobStruct`,
  `GrobFunction`, plugin-registered reference types) are ordinary CLR
  objects, reclaimed by the runtime. No custom mark-and-sweep in v1;
  benchmarking infrastructure (D-302) provides the empirical surface to
  revisit.
- **Tagged union for values** (D-303) — `GrobValue` is a hand-rolled
  `readonly struct` with `GrobValueKind` discriminator, `long _scalar` for
  primitives, `object? _reference` for heap types. NaN boxing rejected on
  managed-runtime grounds (full rationale in D-303 and OQ-005).
- **FOR loops lowered to WHILE** by the compiler — the VM never sees FOR
  opcodes.
- **Backpatching** for forward jumps. Backward jumps (loops) use known
  positions.
- **Chunk in Grob.Core** — not in Grob.Runtime. Plugin authors never need
  `Chunk`; keeping it out of the public NuGet surface is the right call.

-----

## 3. Cross-Cutting Concerns — Day One

These are not features to add later. They are infrastructure that exists
from the first line of code. Every sprint builds on them.

### 3.1 Source Location Tracking

Every token carries its source file path, line number and column number.
Every AST node carries the source location of the token that produced it.
Every bytecode instruction carries a line number (stored in a parallel
line array in the chunk, not inline in the instruction stream).

This means:

- The lexer records `(file, line, column)` on every `Token`.
- The parser copies source location onto every AST node.
- The compiler emits line information alongside every instruction.
- The VM can report the exact source line of any runtime error.

### 3.1.1 LSP-Enabling Properties — Day One

These properties exist on AST nodes from the first line of compiler code.
They cost one field per node and one assignment per identifier resolution.
They are not used by the v1 runtime — they exist so the LSP (post-MVP)
does not require a full type checker audit to retrofit.

**`ResolvedType` on identifier nodes:** The type checker sets
`GrobType ResolvedType` on every identifier node during its pass. This
is the data the LSP’s hover handler returns and the completions handler
uses to query the type registry.

**`Declaration` back-reference on identifier nodes:** The type checker
sets `AstNode? Declaration` on every identifier node, pointing to the
AST node where that name was declared. This is the data the LSP’s
go-to-definition handler returns.

**`DeclaredAt` on symbol table entries:** Every `Symbol` in the symbol
table carries `SourceLocation DeclaredAt`. This is set when the symbol
is registered and never changes.

```csharp
class IdentifierNode : AstNode
{
    public string Name { get; init; }
    public GrobType ResolvedType { get; set; }    // set by type checker
    public AstNode? Declaration { get; set; }      // set by type checker
}

class Symbol
{
    public string Name { get; init; }
    public GrobType Type { get; init; }
    public SourceLocation DeclaredAt { get; init; }
}
```

**Verification:** `Grob.Compiler.Tests` should assert that every
identifier node in a type-checked AST carries a non-null `ResolvedType`
and a non-null `Declaration`. This makes the constraint testable and
prevents regression.

See `grob-tooling-strategy.md` for full rationale.

### 3.2 Diagnostic Infrastructure

The `Diagnostics` namespace exists from Sprint 1. It provides:

- `Diagnostic` record: severity (error, warning, info), message, source
  location, optional suggestion.
- `DiagnosticBag` collection: accumulates all diagnostics from a phase.
  The type checker adds to this bag without stopping. The bag is reported
  after the phase completes.
- Formatted output: errors to stderr, colour-coded when the terminal
  supports it. Denim blue accent, warm amber for warnings (per personality
  doc).
- Error messages never show variable values — only names and types. The
  `--verbose` flag overrides this for debugging (per security decision).

### 3.3 OpCode Enum — Complete from Sprint 2

The `OpCode` enum is defined completely when first introduced. It is not
grown incrementally. The full v1 opcode set is:

```csharp
public enum OpCode : byte
{
    // --- Values ---
    Constant,           // push constant from pool (1-2 byte index)
    ConstantLong,       // push constant from pool (2-byte index, >256 constants)
    Nil,                // push nil
    True,               // push true
    False,              // push false
    Pop,                // discard top of stack
    PopN,               // discard N values from stack (1-byte operand)

    // --- Arithmetic (typed) ---
    AddInt,             // int + int → int
    AddFloat,           // float + float → float
    SubtractInt,
    SubtractFloat,
    MultiplyInt,
    MultiplyFloat,
    DivideInt,          // truncating: 7 / 2 → 3
    DivideFloat,
    ModuloInt,          // int % int → int
    ModuloFloat,        // float % float → float
    NegateInt,          // unary minus (int)
    NegateFloat,        // unary minus (float)
    Concat,             // string + string → string

    // --- Type promotion ---
    IntToFloat,         // promote int to float (mixed arithmetic)

    // --- Comparison ---
    Equal,
    NotEqual,
    LessInt,
    LessFloat,
    LessString,
    GreaterInt,
    GreaterFloat,
    GreaterString,
    LessEqualInt,
    LessEqualFloat,
    GreaterEqualInt,
    GreaterEqualFloat,

    // --- Logic ---
    Not,                // logical not
    // && and || use jump-based short-circuit, not dedicated opcodes

    // --- Variables ---
    GetLocal,           // push local from stack slot (1-byte slot index)
    SetLocal,           // store top of stack in slot (1-byte slot index)
    GetGlobal,          // push global by name index
    SetGlobal,          // store in global by name index
    DefineGlobal,       // create global binding

    // --- Upvalues (closures) ---
    GetUpvalue,         // push captured variable
    SetUpvalue,         // store in captured variable
    CloseUpvalue,       // move upvalue from stack to heap
    Closure,            // create closure object

    // --- Properties and fields ---
    GetProperty,        // get named property (1-byte name index)
    SetProperty,        // set named property (1-byte name index)

    // --- Array operations ---
    NewArray,           // create array from N stack values (1-byte count)
    GetIndex,           // array[index] — pop index, pop array, push element
    SetIndex,           // array[index] = value

    // --- Control flow ---
    Jump,               // unconditional forward jump (2-byte offset)
    JumpIfFalse,        // conditional forward jump (2-byte offset)
    JumpIfTrue,         // conditional forward jump for || short-circuit
    Loop,               // unconditional backward jump (2-byte offset)

    // --- Functions ---
    Call,               // call function (1-byte arg count)
    Return,             // return from function

    // --- Structs ---
    NewStruct,          // create struct instance (operand: type index)
    NewAnonStruct,      // create anonymous struct (operand: field count)

    // --- I/O ---
    Print,              // print top of stack to stdout with newline

    // --- Increment/decrement ---
    IncrementInt,       // ++ on int local (1-byte slot)
    DecrementInt,       // -- on int local (1-byte slot)
    IncrementFloat,     // ++ on float local
    DecrementFloat,     // -- on float local

    // --- Nil handling ---
    NilCoalesce,        // ?? — pop two, push right if left is nil
    IsNil,              // push bool: is top of stack nil?

    // --- String interpolation ---
    BuildString,        // concatenate N string fragments (1-byte count)

    // --- Exception handling ---
    TryBegin,           // mark start of try block (operand: handler table index)
    TryEnd,             // mark end of try block
    Throw,              // throw exception (top of stack)

    // --- Module ---
    Import,             // load plugin module (operand: name index)
}
```

**Rationale for typed opcodes:** The compiler uses type information from
the type checker to emit specialised opcodes (`AddInt` vs `AddFloat` vs
`Concat`). No runtime type checks — the type checker already verified
correctness. This is a confirmed design decision from the VM architecture
sessions. The full set is defined here so that the `OpCode` enum, the
compiler’s opcode selection logic and the VM’s dispatch switch are all
built once, correctly, and never revisited for additive expansion.

### 3.4 Token Kind Enum — Complete from Sprint 1

The `TokenKind` enum is defined completely when the lexer is first written.
The full v1 set includes all keywords, operators, punctuation and literal
types that the language supports. It is not grown incrementally.

```
Keywords:       fn, if, else, while, for, in, return, const, readonly, type,
                param, import, as, try, catch, finally, throw, case, default,
                break, continue, true, false, nil, step, switch
Built-ins:      print, exit, input (identifiers resolved at type-check time
                against registered natives — not keywords)
Reserved IDs:   formatAs, select (lex as identifiers and stay legal as member
                names after `.`; prohibited as binding names — E1103. D-282,
                D-320. `select` is the select-statement head only at statement
                position; `arr.select(fn)` is a method call.)
Operators:      + - * / % = := == != < > <= >= ! && || ? : ?? ?.
                += -= *= /= %= ++ -- .. =>
Punctuation:    ( ) { } [ ] , . #{ ///
Literals:       IntLiteral, FloatLiteral,
                StringStart, StringPart, StringEnd,
                InterpStart, InterpEnd,
                RawStringLiteral, RawStringBlockLiteral,
                RegexLiteral, Identifier
Structure:      Newline, EOF, Error
Decorators:     @ (followed by identifier: secure, allowed, minLength, maxLength)
```

### 3.5 Test Infrastructure

Five xUnit test projects from Sprint 1 (matching the solution
architecture): `Grob.Core.Tests`, `Grob.Compiler.Tests`, `Grob.Vm.Tests`,
`Grob.Stdlib.Tests`, `Grob.Integration.Tests`. Four categories of test:

1. **Lexer tests** (in `Grob.Compiler.Tests`) — given source string,
   assert token stream.
2. **Parser tests** (in `Grob.Compiler.Tests`) — given token stream,
   assert AST shape.
3. **Compiler tests** (in `Grob.Compiler.Tests`) — given source, assert
   bytecode output. This is where bugs will live (per SharpBASIC
   retrospective).
4. **Integration tests** (in `Grob.Integration.Tests`) — given `.grob`
   source file, assert stdout/stderr output and exit code. End-to-end
   through the full pipeline.

Test infrastructure is not optional and is not added retroactively. Every
sprint includes tests for the features it delivers.

-----

## 4. Build Plan — Sprint Breakdown

Each sprint produces a working increment. “Working” means: the existing
test suite passes, the REPL (from Sprint 3 onwards) can exercise the
new features interactively, and the increment can run meaningful scripts.

### Sprint 1 — Foundation

**Delivers:** Lexer, parser, diagnostic infrastructure, project skeleton.

**Scope:**

- C# solution with six `src/` projects (`Grob.Core`, `Grob.Runtime`,
  `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Cli`), three
  `plugins/` projects, and five `tests/` projects — as specified in
  `grob-solution-architecture.md`. .NET 10, self-contained deployment
  target. Dependency graph enforced: Compiler and Vm never reference
  each other.
- `TokenKind` enum — complete (see §3.4).
- `Token` record with `Kind`, `Lexeme`, `Line`, `Column`, `File`.
- `Lexer` — full implementation. All keywords, all operators, all literal
  forms (int with hex/binary/underscores, float, double-quoted strings
  with `${...}` interpolation detection, raw backtick strings, regex
  literals), all comment forms (`//`, `/* */`, `///` recognised and
  discarded).
- `Parser` — produces AST for: expressions (arithmetic, comparison,
  logical, unary, grouping, literals, identifiers, string interpolation,
  ternary, array literals, index expressions, member access, call
  expressions, lambda expressions), statements (variable declaration `:=`,
  variable assignment `=`, compound assignment, `++`/`--`, `print()`,
  `exit()`, `if`/`else if`/`else`, `while`, `for...in` collection,
  `for...in` numeric range, `select`/`case`/`default`, `break`,
  `continue`, `fn` declarations, `return`, `type` declarations, `try`/
  `catch`, `param` block, `import`, `const`, `readonly`).
- **Error-recovering parser per D-300 — day-one requirement.** The
  parser must recover from syntax errors using the synchronisation set
  specified in `grob-language-fundamentals.md` §29.1 (statement-boundary
  newlines outside any open bracket, closing `}` of an enclosing block,
  top-level declaration keywords). On a parse failure, the parser
  emits a diagnostic, builds a placeholder error node, advances to the
  next anchor, and resumes. A single malformed construct never aborts
  the parse. The parser is stateless across files and invocations.
- AST node types — every node carries source location. Includes the
  three error-node kinds (`ErrorExpr`, `ErrorStmt`, `ErrorDecl`)
  produced by the error-recovering parser, each carrying its source
  range and the diagnostic message that produced it. Every visitor in
  `Grob.Compiler` handles these node kinds — see `grob-language-
  fundamentals.md` §29.2.
- `DiagnosticBag`, `Diagnostic`, error formatting to stderr. No per-file
  diagnostic cap (per §29.4).
- Line continuation: trailing-token heuristic (line ends with operator,
  comma, opening bracket, opening brace, `=>`) plus leading-dot chain
  suppression.
- Lexer and parser tests — comprehensive. Parser tests must include
  error-recovery cases: malformed expression mid-function still allows
  the rest of the file to parse, malformed declaration still allows
  subsequent declarations to parse, multi-error files produce one
  diagnostic per root cause without cascade.

**Acceptance:** Lex and parse any valid Grob v1 source file into a correct
AST. Report clear errors with line/column for any invalid source. **Error
recovery works:** a file with a malformed expression in one function
still produces a complete AST for the surrounding well-formed code, with
an `ErrorExpr` placeholder at the failure site. A file with multiple
unrelated errors produces multiple diagnostics, one per root cause, with
no downstream cascade.

### Sprint 2 — Type Checker and Compiler Core

**Delivers:** Static type checking, bytecode compilation, VM execution of
arithmetic expressions and `print()`.

**Scope:**

- `OpCode` enum — complete (see §3.3).
- `GrobValue` struct (D-303) — hand-rolled `readonly struct` tagged union:
  `GrobValueKind` discriminator, `long _scalar` for primitives, `object?
  _reference` for heap types.
- `Chunk` — bytecode array, constant pool, line number array.
- **Bytecode disassembler (D-306)** — `Disassembler` in `Grob.Vm`,
  always compiled (not Debug-gated). `disassembleChunk(Chunk)` and
  `disassembleInstruction(Chunk, offset)` walk a chunk and print each
  opcode, its operands, constant-pool indices with their resolved values,
  and source line numbers, human-readably. Built against hand-constructed
  chunks before the compiler emits its first bytecode, so compiler output
  is readable by eye from the first emission — the layer-boundary
  bisection tool (is the bytecode wrong, or is the VM executing correct
  bytecode wrongly?). Authority: `grob-vm-architecture.md` "Developer
  Diagnostics". The `grob dump` CLI wrapper is deferred to Sprint 12.
- **Execution tracing (D-306)** — a `TraceInstruction(chunk, ip)` call at
  the top of the VM dispatch loop, guarded by `#if DEBUG` in the
  `Grob.Vm` C# source. Prints the value stack and the about-to-execute
  instruction every iteration. Absent from Release builds entirely — no
  runtime flag, no dispatch-loop branch — so the Release dispatch loop the
  D-302 benchmarks measure stays branch-free. Reached by compiling a Debug
  build, never by a CLI flag.
- Type checker (first AST visitor pass):
  - Type inference on `:=` declarations.
  - Explicit type annotation validation.
  - Arithmetic type rules: `int op int → int`, `float op float → float`,
    `int op float → float` (implicit promotion), `string + string → string`.
    `int / int → int` (truncating). `int + string` is a compile error.
  - Comparison type rules.
  - Set `ResolvedType` on every identifier node (§3.1.1).
  - Set `Declaration` back-reference on every identifier node (§3.1.1).
  - Collect ALL errors — never stop at first.
- Compiler (second AST visitor pass):
  - Emit typed arithmetic opcodes based on type checker annotations.
  - Constant pool management.
  - Line number tracking per instruction.
  - `print()` as a built-in.
  - `exit()` as a built-in.
- VM:
  - Fetch-decode-execute loop — full `OpCode` switch.
  - Value stack.
  - Arithmetic execution.
  - `Print` opcode.
  - `Return` opcode.
  - Runtime error with source line.
- Compiler tests — given source, assert bytecode.
- Integration tests — `print(2 + 3)` produces `5`.
- `bench/Grob.Benchmarks/` skeleton — BenchmarkDotNet console project
  created as sibling to `src/`, `tests/`, `plugins/`. Initial compile-time
  benchmark category with at least one benchmark (lexer throughput on
  a representative source). `Fixtures/` directory established. First
  baseline JSON committed to `bench/Grob.Benchmarks/baseline/compile.json`.
  Authority: D-302, `grob-benchmarking-strategy.md`.

**Acceptance:** `print(2 + 3 * 4)` compiles, runs and prints `14`. Type
errors (e.g. `"hello" + 42`) produce clear compile-time diagnostics with
line numbers. `bench/Grob.Benchmarks` builds, the seed compile-time
benchmark runs end-to-end via `dotnet run -c Release --project bench/Grob.Benchmarks`,
and the first baseline JSON is committed. `disassembleChunk` produces a
correct, readable listing for a hand-constructed chunk and for the chunk
the compiler emits for `print(2 + 3 * 4)`; a Debug build emits per-
instruction trace output, a Release build does not.

### Sprint 3 — Variables, Scope and REPL

**Delivers:** Variable declaration/assignment, `const`, `readonly`,
scope chain, REPL.

**Scope:**

- `:=` declaration — local scope, type inferred.
- `=` reassignment — walks scope chain.
- `const` — compile-time constant binding. Right-hand side must be a
  compile-time constant expression per §24 of
  `grob-language-fundamentals.md`. Type checker pass 2 evaluates the
  RHS; compiler inlines every reference as a constant pool load.
  Reassignment, mutation and referencing a `readonly` on the RHS are
  compile errors.
- `readonly` — runtime-once binding, any expression legal on the
  right-hand side. Evaluated at the point of declaration. Same
  `DefineGlobal` / `DefineLocal` opcodes as mutable bindings; the
  immutability is a compile-time check. Reassignment and mutation
  are compile errors.
- Shared AST node: `SingleAssignmentDeclaration` with a `Kind`
  discriminator (`Const` | `Readonly`). Mutable `:=` continues to
  use its existing declaration node.
- Global variables: `DefineGlobal`, `GetGlobal`, `SetGlobal`.
- Local variables: `GetLocal`, `SetLocal` with stack slot indexing.
- Block scoping — `{ }` creates a new scope. `PopN` cleans up on exit.
- Type annotations: `name: Type := value` — validated by type checker.
- Nil and nullable types: `?` suffix, `??` nil coalescing, `?.` optional
  chaining, `IsNil` opcode, `NilCoalesce` opcode.
- String interpolation compilation: parser produces `InterpolatedString`
  AST node, compiler emits segment pushes and `BuildString`.
- `++`/`--` postfix statements: `IncrementInt`, `DecrementInt`.
- Compound assignment: `+=`, `-=`, `*=`, `/=`, `%=` — desugared by the
  compiler to load-operate-store sequences.
- REPL: `grob repl` command. `G>` prompt. Expression results printed
  automatically. Multi-line input for blocks. History (readline or
  equivalent).
- `grob run <file>` command — compile and execute a `.grob` file.

**Acceptance:** The REPL works. Variables can be declared, reassigned,
and used in expressions. `const` bindings reject non-compile-time
right-hand sides and reject reassignment and mutation. `readonly`
bindings reject reassignment and mutation but accept any runtime
expression on the right-hand side. Nil safety works. String
interpolation works. `grob run hello.grob` executes a script file.

### Sprint 4 — Control Flow

**Delivers:** `if`/`else`, `while`, `for...in`, `select`/`case`, `break`,
`continue`.

**Scope:**

- `if (condition) { }` — `JumpIfFalse` with backpatching.
- `else if (condition) { }` — chained jumps.
- `else { }` — unconditional `Jump` past else block.
- `while (condition) { }` — `Loop` backward jump, `JumpIfFalse` exit.
- `for item in collection { }` — lowered to while by compiler. Iterator
  variable immutable within body (compile error on reassignment).
- `for i, item in collection { }` — index form for arrays, `i` is
  zero-based int.
- `for k, v in myMap { }` — map iteration. Two-identifier form required.
  Single-identifier form on a `map<K, V>` is a compile error with a
  suggestion to use `for k in myMap.keys` instead. Iterates insertion
  order. Lowered to while over an internal keys array.
- Any other type in `for...in` subject position is a compile error.
- `for i in 0..10 { }` — numeric range, inclusive both bounds.
- `for i in 0..100 step 5 { }` — step form.
- `for i in 10..0 step -1 { }` — descending. Descending without explicit
  negative step is a compile error.
- `select (value) { case X { } case Y, Z { } default { } }` — first
  match, no fall-through. `default` optional.
- `break` — exits innermost loop. Compile error (E2212) outside a loop.
- `continue` — skips to next iteration. Compile error (E2212) outside a loop.
- `break`/`continue` inside `select` are asymmetric (D-315). `break` inside a
  `select` arm is a compile error (E2211) at any nesting — `select` has no
  fall-through, so `break` has no meaning there and the loop-exit reading is a
  footgun. `continue` inside a `select` arm passes through to the nearest
  enclosing loop. `break`/`continue` with no enclosing loop is E2212. The
  compiler tracks `select` nesting; `select` is not loop-control-transparent.
- Logical `&&` and `||` — short-circuit using `JumpIfFalse`/`JumpIfTrue`.
  Not dedicated opcodes.
- Ternary `condition ? a : b` — jump-based compilation.
- Switch expression: `value switch { pattern => result, _ => default }` —
  exhaustiveness enforced by type checker. All arms same type.

**Acceptance:** All control flow constructs work correctly. Array `for...in`
and map `for k, v in` iteration work correctly. Single-identifier `for k in`
on a map produces a clear compile error. The calculator smoke test script
runs. Nested loops with `break`/`continue` behave as specified. `break` inside
a `select` is E2211 (with or without an enclosing loop); `continue` inside a
`select` continues the nearest enclosing loop; `break`/`continue` with no
enclosing loop is E2212.

### Sprint 5 — Functions and Closures

**Delivers:** `fn` declarations, call frames, return values, lambdas,
closures.

**Scope:**

- `fn name(param: Type, param: Type): ReturnType { }` — function
  declaration. Return type explicit (required in v1).
- `Call` opcode — push call frame, dispatch.
- `Return` opcode — pop call frame, push return value.
- `CallFrame` struct: function reference, instruction pointer, stack base.
- `CallFrame[256]` fixed array — no heap allocation per call.
- Recursion — works naturally via call frames.
- Named parameters at call sites: positional first, named after. Only
  parameters with defaults may be named. Compile errors for: named before
  positional, naming a required param, duplicate names, unknown names.
- Lambda expressions: `x => expr`, `x => { block }`, `(a, b) => expr`.
- Closures: upvalue capture. `GetUpvalue`, `SetUpvalue`, `CloseUpvalue`,
  `Closure` opcodes. Open upvalues reference stack slots; closed upvalues
  copy to heap on enclosing function return.
- Closure variable resolution (§12.1 of `grob-language-fundamentals.md`):
  the compiler classifies every identifier reference in a lambda body
  into one of four categories — top-level `const` (inlined), top-level
  `readonly` (global read), top-level mutable (global read/write), or
  enclosing-function local (upvalue). "Capture" applies only to the
  upvalue category.
- Top-level initialisation state machine (§19.1): each top-level binding
  slot carries a `SlotState` tag (`Uninitialised` / `Initialising` /
  `Initialised`). `DefineGlobal` transitions the tag; `GetGlobal`
  consults it during startup and raises `RuntimeError` on reads to a
  non-initialised slot. After the top-level code completes, a
  `_startupComplete` flag short-circuits the check. Circular-
  initialisation diagnostic per §19.1.
- `BytecodeFunction` and `NativeFunction` as function representations.
  VM dispatches transparently.
- Native function registration mechanism: `RegisterNative(name, signature, implementation)`.
- Flow-sensitive type narrowing for optionals: inside `if (x != nil) { }`
  the type checker narrows `x` from `T?` to `T`.

**Acceptance:** Functions call and return correctly. Lambdas work in
`filter`, `select`, `sort`. Closures capture enclosing variables. Named
parameters work. The type checker catches arity mismatches and type
mismatches on arguments.

### Sprint 6 — User-Defined Types

**Delivers:** `type` keyword, struct construction, field access, anonymous
structs.

**Scope:**

- `type Name { field: Type, field: Type = default }` — type declaration.
- `TypeName { field: value, field: value }` — named construction.
  Compiler validates all required fields are provided. Missing fields
  with defaults use the default.
- Field default expressions evaluate at construction time in the
  construction-site scope (§10). Any expression legal in a general
  expression position is admitted. The compiler emits the default
  expression's bytecode into each construction site that omits the
  field; defaults for supplied fields do not evaluate. A default may
  reference identifiers in scope at the construction site but cannot
  reference other fields of the type being constructed.
- Type cycle detection: required non-nullable fields cannot form a
  cycle. Standard DFS with three visit states reports a cycle
  diagnostic per §17.1.
- Nested construction: `Issue { user: IssueUser { login: "chris" } }`.
- Field access: `instance.field` — `GetProperty` opcode.
- Field assignment: `instance.field = value` — `SetProperty` opcode.
- Nested field access: `issue.user.login` — type checker resolves each
  step.
- Anonymous struct literals: `#{ field: value }` — `NewAnonStruct` opcode.
  Type checker creates internal structural type. Field access is type-safe.
- Bare `{ }` is always a block. `#{ }` is always an anonymous struct.
  `TypeName { }` is always named construction. No parser ambiguity.
- Type declarations registered in the type checker’s type registry.
  Accessing undefined fields is a compile error.

**Acceptance:** Types can be declared, constructed, accessed. Anonymous
structs work in lambdas and `select()` projections. The type checker
catches undefined field access at compile time.

### Sprint 7 — Error Handling

**Delivers:** `try`/`catch`, exception hierarchy, `throw`.

**Scope:**

- `try { } catch e { }` — basic form.
- `try { } catch IoError e { } catch e { }` — typed catches. Bare
  `catch e` is the catch-all and must appear last. Catch after catch-all
  is a compile error.
- Exception hierarchy (registered as types in `Grob.Runtime`):
  `GrobError` (root), `IoError`, `NetworkError`, `JsonError`,
  `ProcessError`, `NilError`, `ArithmeticError`, `IndexError`,
  `ParseError`, `LookupError`, `RuntimeError`. Flat hierarchy — all
  ten typed leaves are direct children of `GrobError`. See
  `grob-language-fundamentals.md` §27 for throw-site assignments.
- `Throw` opcode — pushes exception, unwinds to nearest handler.
- `TryBegin`/`TryEnd` opcodes — mark protected regions in bytecode.
  Handler table maps regions to catch handlers.
- `exit()` throws uncatchable `ExitSignal` — cannot be caught by
  `try`/`catch`. VM catches at top level, flushes buffers, terminates.
- Unhandled exceptions propagate to VM top level — Grob-quality
  diagnostic produced (file, line, error type, message, suggestion).
  Script halts with exit code 1.

**Acceptance:** `try`/`catch` works. Typed catches match correctly.
Catch-all catches everything. Unhandled exceptions produce quality
diagnostics. `exit()` cannot be caught.

### Sprint 8 — Core Standard Library (Part 1)

**Delivers:** `print`, `exit`, `math`, `strings`, `path`, `env`, `log`,
`formatAs`, `guid` modules as `IGrobPlugin` implementations.

**Scope:**

All modules implemented as `IGrobPlugin` classes, auto-registered at VM
startup. Type signatures registered with the type checker for compile-time
validation.

- **`print()`** — variadic, stdout, newline appended, void return.
  Already built-in from Sprint 2; now formalised as part of the stdlib.
- **`exit()`** — already built-in from Sprint 2.
- **`input()`** — `input(prompt: string = ""): string`. Writes prompt to
  stdout (no trailing newline). Reads one line from stdin. Returns string
  with newline stripped. Throws `IoError` if stdin is closed before a line
  is read. No namespace — always available, same category as `print()`.
- **`math`** — `pi`, `e`, `tau` constants. `sqrt()`, `pow()`, `log()`,
  `log10()`, `sin()`, `cos()`, `tan()`, `asin()`, `acos()`, `atan()`,
  `atan2()`, `toRadians()`, `toDegrees()`, `random()`, `randomInt()`,
  `randomSeed()`.
- **`strings`** — `strings.join(parts, separator)`. All other string
  operations are instance methods on the `string` type (already in the
  type registry).
- **`path`** — `join()`, `joinAll()`, `extension()`, `filename()`,
  `stem()`, `directory()`, `resolve()`, `normalise()`, `isAbsolute()`,
  `isRelative()`, `changeExtension()`, `separator` constant.
- **`env`** — `get(key) → string?`, `require(key) → string` (throws
  `LookupError` if absent), `set(key, value)`, `has(key) → bool`,
  `all() → map<string, string>`.
- **`log`** — `debug()`, `info()`, `warning()`, `error()`. All to stderr.
  `setLevel()`. `debug` suppressed by default, visible with `--verbose`.
- **`formatAs`** — `formatAs.table()`, `formatAs.list()`, `formatAs.csv()`.
  Column names derived from type field registry at compile time. Works
  on named structs and anonymous structs.
- **`guid`** — `guid.newV4()`, `guid.newV7()`, `guid.newV5()`.
  `guid.parse()`, `guid.tryParse()`, `guid.empty`. Well-known
  namespaces: `guid.namespaces.dns`, `guid.namespaces.url`,
  `guid.namespaces.oid`. `guid` type with `version`, `isEmpty`
  properties, `toString()`, `toUpperString()`, `toCompactString()`
  methods. `==`, `!=` operators. Compile-time validation on
  `guid.parse()` with string literal argument. `guid` is a
  primitive type distinct from `string`.

**Acceptance:** Each module’s full API works. `math.sqrt(9.0)` returns
`3.0`. `env.require("MISSING")` throws `LookupError`. `log.error()`
writes to stderr. `formatAs.table()` produces aligned column output.
Stability test calibration ritual complete — single-iteration pass
against the Sprint 8 build characterises iteration time, steady-state
heap and iteration-to-iteration variance. Final iteration count, warmup
window and tolerance committed to `bench/Grob.Benchmarks/baseline/stability.json`,
calibration outcome recorded as an addendum to D-302. Stability test
itself implemented and producing a first passing run. Authority: D-302,
`grob-benchmarking-strategy.md` §6 and §11.

### Sprint 9 — Core Standard Library (Part 2)

**Delivers:** `fs`, `date`, `json`, `csv`, `regex`, `process` modules.

**Scope:**

- **`fs`** — full API as specified. `File` type registered with type
  checker. `list()`, `exists()`, `isFile()`, `isDirectory()`,
  `ensureDir()`, `createDir()`, `delete()`, `deleteRecursive()`,
  `readText()`, `readLines()`, `writeText()`, `appendText()`,
  `copy(src, dest, overwrite: bool = false)`,
  `move(src, dest, overwrite: bool = false)`. `File` properties: `name`,
  `path`, `directory`, `extension`, `size`, `modified`, `created`,
  `isDirectory`. `File` methods: `rename()`,
  `moveTo(destDir, overwrite: bool = false)`,
  `copyTo(destDir, overwrite: bool = false)`, `delete()`.
- **`date`** — full API as specified. `now()`, `today()`, `of()`,
  `ofTime()`, `parse()`. Properties: `year`, `month`, `day`, `hour`,
  `minute`, `second`, `dayOfWeek`, `dayOfYear`, `utcOffset`. Methods:
  `addDays()`, `minusDays()`, `addMonths()`, `addHours()`,
  `addMinutes()`, `isBefore()`, `isAfter()`, `toIso()`,
  `toIsoDateTime()`, `format()`, `toUnixSeconds()`, `toUnixMillis()`,
  `toUtc()`, `toLocal()`, `toZone()`, `daysUntil()`, `daysSince()`.
  Static: `fromUnixSeconds()`, `fromUnixMillis()`.
- **`json`** — `read()`, `write(compact: bool = false)`, `parse()`,
  `encode(compact: bool = false)`, `stdin()`, `stdout(compact: bool = false)`.
  `json.Node` type with indexer access `node["key"]`. `asString()`,
  `asInt()`, `asFloat()`, `asBool()`, `asArray()`. `mapAs<T>()` for
  typed deserialization (constrained generic — type checker handles).
  Pretty-printed output by default; `compact: true` for single-line.
- **`csv`** — `read()`, `write()`, `parse()`, `stdin()`, `stdout()`.
  `csv.Table` type: `headers`, `rowCount`, `rows`. `CsvRow`: `get(name)`,
  `get(index)`, indexer syntax. `mapAs<T>()`. RFC 4180 compliance.
  `hasHeaders`, `delimiter` named parameters.
- **`regex`** — regex literal `/pattern/flags` (flags: `i`, `m`). `Regex`
  type: `match()`, `matchAll()`, `isMatch()`, `replace()`,
  `replaceAll()`, `split()`, `pattern`, `flags`. `Match` type: `value`,
  `index`, `length`, `groups`, `group(name)`. Module convenience
  functions for one-shot use. .NET regex engine underneath.
- **`process`** — `run(cmd, args[], timeout: int = 0)`,
  `runShell(cmd, timeout: int = 0)`, `runOrFail()`,
  `runShellOrFail()`. `ProcessResult`: `stdout`, `stderr`, `exitCode`.
  `timeout: int = 0` on all four functions — `0` means infinite.
  Throws `ProcessError` on timeout expiry.

**Acceptance:** The file organiser real-program target runs correctly.
JSON and CSV round-trip works. Regex matching and replacement works.
Process execution captures stdout/stderr.

### Sprint 10 — Script Parameters and Decorators

**Delivers:** `param` block, `.grobparams` files, `@secure`, `@allowed`,
`@minLength`, `@maxLength` decorators, CLI parameter passing.

**Scope:**

- `param` block at top of script: `param name: Type`,
  `param name: Type = default`.
- Type checker validates param types at compile time.
- Required params (no default) must be provided — compile error if
  missing.
- Command line passing: `grob run script.grob --name value`.
- Param file: `grob run script.grob --params file.grobparams`.
- `.grobparams` format: `key = value`, `//` comments.
- Command line overrides param file values.
- Decorators: `@secure` (not echoed, not logged, not in error messages),
  `@allowed("dev", "staging", "prod")` (compile-time validation),
  `@minLength(n)`, `@maxLength(n)`.
- `@secure` params warned if present in `.grobparams` in plain text.

**Acceptance:** Scripts accept typed parameters from CLI and param files.
Decorators validate correctly. `@secure` values are never exposed in
output.

### Sprint 11 — Plugin System and Imports

**Delivers:** `import` statement, plugin loading, `grob install`,
`grob search`, `grob.json` manifest.

**Scope:**

- `import Grob.Http` — type checker loads plugin signatures at compile
  time. Default alias: last segment lowercased (`http.*`).
- `import Grob.Http as client` — explicit alias.
- `IGrobPlugin` interface: `Name`, `Register(GrobVM vm)`.
- `Grob.Runtime` NuGet package: `IGrobPlugin`, `FunctionSignature`,
  `Parameter`, `GrobType` — the public SDK contract.
- Plugin loading: `Assembly.LoadFrom()`, find `IGrobPlugin`, instantiate,
  register.
- `grob install <package>` — install from NuGet (tagged `grob-plugin`).
  Three-tier scope: user-global (default, `%USERPROFILE%\.grob\packages\`),
  system (`--system`, `%ProgramFiles%\Grob\packages\`), project-local
  (`--local`, `.grob\packages\`).
- `grob search <query>` — search NuGet for `grob-plugin` tagged packages.
- `grob.json` manifest: `name`, `version`, `dependencies` with semver.
  `grob restore` installs all dependencies — idempotent, CI-safe.
- `grob.json` discovery: walk up from script file location, not CWD.
- Resolution order: local → user → system.
- Plugin not found → compile error with helpful message:
  `Grob.Http is not installed. Run: grob install Grob.Http`.
- `--dev-plugin path/to/local.dll` flag for plugin development.

**Acceptance:** `import Grob.Http` works end-to-end. Plugin functions are
type-checked at compile time. `grob install` downloads from NuGet.
`grob.json` restore works.

### Sprint 12 — CLI, Formatting, Polish

**Delivers:** `grob fmt`, `grob check`, `grob dump`, `grob new`,
`grob version`, `--help`, Windows Terminal profile, first-run
acknowledgement, `.grobc` caching, final polish.

**Scope:**

- `grob fmt <file>` — format source code. Never automatic, always opt-in.
  Same-line braces, consistent indentation, `snake_case` warnings.
- `grob check <file>` — run lexer, parser and type checker only. Report
  all diagnostics. Do not execute.
- `grob dump <file>` — compile and disassemble. Runs the full front end
  plus the compiler, then prints the resulting `Chunk` via the Sprint 2
  `Disassembler` (D-306). Does not execute. The CLI front door to the
  disassembler engine built in Sprint 2; this is the wrapper only.
- `grob new <name>` — scaffold a new script or project.
- `grob version` / `grob --version` — version string.
- `grob --help` — full command listing.
- `.grobc` bytecode caching — if source unchanged, load cached bytecode.
  Magic number `GROB` (0x47 0x52 0x4F 0x42). Version byte. Invalidated
  on source modification.
- First-run detection: `✦ First script. Nice work.` — once only, never
  repeated. Stored in `%USERPROFILE%\.grob\` config.
- Windows Terminal profile: name `Grob`, icon, denim blue colour scheme,
  `grob repl` startup command.
- Quiet on success, clear on failure. Results to stdout, errors to stderr.
- Exit codes: 0 success, 1 runtime error, 2 compile error.
- `--verbose` flag: shows `log.debug()` output and variable values in
  error messages.

**Acceptance:** All CLI commands work. `grob fmt` produces consistent
formatting. `grob check` reports errors without executing. The Windows
Terminal profile registers correctly. All thirteen sample scripts compile
and run.

-----

## 5. Language Features — Complete v1 Surface

> **Authority:** `grob-language-fundamentals.md`. That document is the
> normative grammar and syntax reference. This section is a pointer
> summary, not a specification. Decisions are in the decisions log —
> cross-references below.

The v1 surface covers: C-style syntax (D-008, D-009, D-010), `:=` /
`=` / `const` / `readonly` bindings (D-011, D-012, D-013, D-038,
D-288, D-291), full arithmetic,
comparison, logical, assignment, `++` / `--`, string `+` and
interpolation, nil `??` / `?.`, range `..`, and lambda `=>` operators,
a thirteen-level precedence table, int / float / string / bool / nil /
array literals with hex / binary / underscore int forms and three
string literal forms (D-127, D-128, D-129, D-130, D-161), `if` /
`else if` / `else` / `while` / `for...in` / numeric range `for` /
`select` / `break` / `continue` / ternary / switch expression control
flow, `fn` functions with explicit return types and named parameter
calling (D-016, D-087, D-113), `type` declarations with named and
anonymous (`#{ }`) construction (D-043, D-114), `param` blocks with
`@secure` / `@allowed` / `@minLength` / `@maxLength` decorators
(D-098, D-101, D-102), `try` / `catch` with typed and catch-all
handlers (D-082, D-083), and `print` / `input` / `exit` built-ins
(D-110, D-139).

Module loading: thirteen core modules are auto-available with no
import; plugins require `import X` after `grob install` (D-026,
D-027, D-032).

Nullable types, flow-sensitive narrowing, equality semantics, nil
chain propagation, script structure ordering, shadowing, forward
references and the no-script-level-return rule are all in the
Language Fundamentals document (D-014, D-015, D-166 through D-171).

-----

## 6. Type System

### Built-in Types

|Type       |Description                         |Default|
|-----------|------------------------------------|-------|
|`int`      |64-bit signed integer               |`0`    |
|`float`    |64-bit IEEE 754                     |`0.0`  |
|`bool`     |Boolean                             |`false`|
|`string`   |Immutable UTF-16 string             |`""`   |
|`nil`      |Absence of value                    |—      |
|`T[]`      |Typed array                         |—      |
|`map<K, V>`|Key-value map (v1: string keys only)|—      |
|`guid`     |Universally unique identifier       |—      |
|`T?`       |Nullable variant of any type        |`nil`  |

### Type Inference

`:=` infers type from the right-hand side. `x := 42` → `int`.
`name := "Chris"` → `string`. `items := [1, 2, 3]` → `int[]`.

### Nullable Types

- `string?` means the value can be `nil`.
- Non-nullable types are guaranteed non-nil by the type checker.
- `??` coalescing: `x ?? "default"`.
- `?.` optional chaining: `user?.name`.
- Flow-sensitive narrowing: inside `if (x != nil) { }`, `x` is narrowed
  from `T?` to `T`.

### Method-Call Syntax

All types support method-call syntax. `"42".toInt()`, `42.toString()`,
`3.14.round()`. This is compiler sugar — rewritten to native function
calls at compile time. Primitives are never boxed.

### Conversion Rule

Conversions are methods on the source type: `"42".toInt()`. Never
`int.parse("42")`. Static utilities live on the type namespace:
`int.min(a, b)`. No overlap between the two.

### Built-in Type Methods (v1)

> **Authority:** `grob-type-registry.md`. The registry is the definitive
> per-type method and property table and the compiler's implementation
> reference. Undefined method / property calls are a compile error
> (D-079). See D-149 for `guid`; D-118 and D-155 for the `Grob.Http`
> response types; D-140 for array mutation rules; D-141 for `map<K, V>`
> semantics.

-----

## 7. Standard Library — v1 Modules

All thirteen core modules ship with v1. All are auto-available — no import
required. All are implemented as `IGrobPlugin` classes.

|Module   |Functions                |Types Registered     |
|---------|-------------------------|---------------------|
|`fs`     |15 module functions      |`File`               |
|`strings`|1 (`join`)               |—                    |
|`json`   |5 module functions       |`json.Node`          |
|`csv`    |5 module functions       |`csv.Table`, `CsvRow`|
|`env`    |5 module functions       |—                    |
|`process`|4 module functions       |`ProcessResult`      |
|`date`   |6 constructors/statics   |`date`               |
|`math`   |13 functions, 3 constants|—                    |
|`log`    |5 functions              |—                    |
|`regex`  |7 convenience functions  |`Regex`, `Match`     |
|`path`   |11 functions, 1 constant |—                    |
|`formatAs`|3 functions              |—                    |
|`guid`   |6 statics, 3 namespaces  |`guid`               |

Full API detail for each module is in `grob-stdlib-reference.md`
and the confirmed decisions in `grob-decisions-log.md`.

-----

## 8. CLI — v1 Commands

|Command                          |Description                               |
|---------------------------------|------------------------------------------|
|`grob run <file>`                |Compile and execute a script              |
|`grob run <file> --params <file>`|Execute with parameter file               |
|`grob run <file> --verbose`      |Execute with debug output                 |
|`grob repl`                      |Interactive REPL (`G>` prompt)            |
|`grob check <file>`              |Lex, parse, type-check only — no execution|
|`grob dump <file>`               |Compile and disassemble — print bytecode, no execution|
|`grob fmt <file>`                |Format source code (never automatic)      |
|`grob new <name>`                |Scaffold new script or project            |
|`grob install <package>`         |Install plugin from NuGet                 |
|`grob install <package> --system`|Install system-wide (elevation)           |
|`grob install <package> --local` |Install project-local                     |
|`grob restore`                   |Install all `grob.json` dependencies      |
|`grob search <query>`            |Search NuGet for `grob-plugin` packages   |
|`grob version`                   |Print version                             |
|`grob --help`                    |Print command listing                     |

### Exit Codes

|Code|Meaning                            |
|----|-----------------------------------|
|0   |Success                            |
|1   |Runtime error (unhandled exception)|
|2   |Compile error (type/syntax error)  |

-----

## 9. Plugin System

### Architecture

- Plugins are C# class libraries implementing `IGrobPlugin` (D-018, D-051).
- `Grob.Runtime` NuGet package is the public SDK, versioned independently
  from the runtime.
- Plugins register native functions with type signatures. The type
  checker validates call sites statically (D-019, D-081).
- Standard library modules are plugins, auto-registered at startup.
- External plugins require `import` and prior `grob install` (D-026, D-032).

### First-Party Plugins (v1)

|Plugin       |Purpose                               |Decisions         |
|-------------|--------------------------------------|------------------|
|`Grob.Http`  |HTTP client, auth helpers             |D-118, D-155, D-158, D-159|
|`Grob.Crypto`|Checksums and hashing (file + string) |D-097, D-148      |
|`Grob.Zip`   |Archive compress / expand             |D-097, D-152      |

Full API shapes in `grob-stdlib-reference.md` and `grob-plugins.md`.

### Security

Plugin loading is running arbitrary code. Documented prominently. No
sandbox claims. No sandbox attempted. The safe path is the obvious path.
See D-072, D-073, D-078.

-----

## 10. Error Handling

### Two-Mode Strategy

Compile time: the type checker collects all errors and reports every
diagnostic after the phase completes, never stops at the first. Runtime:
the VM stops on the first unhandled exception and produces a
Grob-quality diagnostic. See D-039.

### Exception Hierarchy

`GrobError` is the root with ten v1 leaves: `IoError` (file system),
`NetworkError` (HTTP, DNS, connection), `JsonError` (JSON parse and
type coercion), `ProcessError` (subprocess timeout and failure),
`NilError` (nil dereference), `ArithmeticError` (integer overflow, int
div/mod by zero, float div/mod by zero, math domain violations),
`IndexError` (array and substring bounds), `ParseError` (`guid.parse`
and other explicit parse operations), `LookupError` (`env.require` on
missing keys), and `RuntimeError` (stack overflow and residual
VM-level resource failures). See `grob-language-fundamentals.md` §27
for the full throw-site assignments. User-defined exception types are
post-MVP (D-085).

### Error Message Format

```
error[E0001]: type mismatch
  --> deploy.grob:14:12
   |
14 |     count := "hello" + 42
   |              ^^^^^^^^^^^^
   |
   = expected: string + string, or int + int
   = got: string + int
   = help: use string interpolation: "${count}${42}"
```

Every error has a unique `Exxxx` code. The thousands digit encodes the
category (E0xxx type, E1xxx name resolution, E2xxx syntax, E3xxx module,
E4xxx param, E5xxx runtime, E9xxx internal). The full registry of allocated
codes lives in `grob-error-codes.md`. The numbering scheme and allocation
rules are specified in ADR-0014; the stability guarantees (codes immutable
once shipped, retired codes never reused) are specified in ADR-0017.
Long-form documentation for each code (cause, example, fix) is read by
`grob --explain Exxxx` from `docs/errors/Exxxx.md`.

### Error Message Rules

- Show variable names and types. Never show values (D-077).
- `--verbose` overrides the value suppression for debugging.
- Suggested fix when the fix is obvious (D-059).
- Never "simply" in any error message text (D-064).
- No emoji in any compiler or CLI output (D-064).
- Errors to stderr. Results to stdout (D-063).

-----

## 11. Security

- `process.run(cmd, args[])` is the safe form — no shell interpolation.
- `process.runShell(cmd)` makes shell involvement explicit in the name.
- Error messages never expose variable values.
- `@secure` params: not echoed, not logged, not in error messages.
- `env.require()` is the canonical pattern for credentials.
- Plugin loading documented as running arbitrary code. No sandbox claims.
- `grob install` is always a deliberate step. Plugins never auto-download
  at runtime.

-----

## 12. Testing Strategy

### Test Projects (from Solution Architecture)

|Test Project            |Covers                                                                                                         |Approach                                                                                                  |Quantity Target|
|------------------------|---------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------|---------------|
|`Grob.Core.Tests`       |Value representation, chunk construction, opcode encoding                                                      |Unit tests on shared primitives                                                                           |50+            |
|`Grob.Compiler.Tests`   |Lexer (token stream), parser (AST shape), type checker (error detection, inference), compiler (bytecode output)|Given source → assert tokens/AST/diagnostics/bytecode. **Highest priority — this is where bugs will live**|500+           |
|`Grob.Vm.Tests`         |Fetch/decode/execute, stack behaviour, call frames, closures                                                   |Construct chunks by hand → assert execution results                                                       |100+           |
|`Grob.Stdlib.Tests`     |All 13 core module APIs                                                                                        |Register plugin into VM instance → assert outputs                                                         |200+           |
|`Grob.Integration.Tests`|End-to-end through full pipeline                                                                               |Given `.grob` source file → assert stdout, stderr, exit code. The thirteen sample scripts live here          |50+            |

### Test Discipline

- Every sprint includes tests for the features it delivers.
- Compiler tests are the highest priority — this is where bugs will
  live (per SharpBASIC retrospective).
- The VM loop can be trusted once verified on simple cases.
- Edge cases and failure paths are tested as thoroughly as the happy path.
- The thirteen sample scripts are integration tests in `Grob.Integration.Tests`.

### Sprint-Close Smoke Scripts

> **Authority:** D-337. Distinct from the thirteen release-gate validation scripts
> of `grob-sample-scripts.md`. The validation suite is a **v1 gate**; the smoke
> family is a **per-sprint gate**. Both live in `Grob.Integration.Tests`.

Each sprint close adds one end-to-end smoke script, gold-mastered against its exact
stdout, stderr and exit code. The family is **cumulative** — every prior script must
still pass at every subsequent close.

|Script          |Added at |Exercises                                                   |Exit|
|----------------|---------|------------------------------------------------------------|----|
|`hello.grob`    |Sprint 3 |Lexer → parser → compiler → VM, `print()`                    |0   |
|`calculator.grob`|Sprint 4|Arithmetic, control flow, checked overflow                   |0   |
|`functions.grob`|Sprint 5 |Functions, closures, upvalue capture                          |0   |
|`types.grob`    |Sprint 6 |Struct types, construction, field access                      |0   |
|`errors.grob`   |Sprint 7 |`throw`, typed `catch`, source-order first-match, catch-all, `finally` on normal and exceptional paths, nested finally with early `return` (D-334), runtime `ArithmeticError` catchability, `exit()` uncatchability|42  |

**The contract is stdout, stderr and exit code.** Not exit 0. `errors.grob` exits
**42** by design: `exit(42)` is the final statement inside a `try`/`catch`/`finally`,
and neither the catch nor the finally runs. Any harness assuming a uniform exit 0
across the family is incorrect as of Sprint 7.

**Solution membership is gated.** `Grob.Integration.Tests` was silently dropped from
`Grob.slnx` between Sprint 5 and Sprint 7, during which none of these scripts ran under
`dotnet test` or CI (D-335). `Grob.Consistency.Tests` now asserts that every
`**/*.Tests.csproj` under `tests/` is referenced by `Grob.slnx`; drift fails the build.

### Benchmarking — Per-Sprint Regression Gate

> **Authority:** `grob-benchmarking-strategy.md` (D-302). This section
> summarises; the full spec covers harness, three categories, memory
> diagnostics, stability test, baseline storage and policy in detail.

Grob ships with a benchmarking harness in `bench/Grob.Benchmarks`
(sibling to `src/`, `tests/` and `plugins/`). BenchmarkDotNet drives
three categories — compile-time, VM execution and end-to-end script —
and `[MemoryDiagnoser]` on every benchmark puts allocations and GC
counts into the committed baseline alongside timing.

|Category                  |Project subdirectory                      |Question answered                                              |
|--------------------------|------------------------------------------|---------------------------------------------------------------|
|Compile-time              |`bench/Grob.Benchmarks/Compile/`          |How fast is the compiler? (startup cost for a script language) |
|VM execution              |`bench/Grob.Benchmarks/Vm/`               |How fast is the dispatch loop? (hand-constructed chunks)       |
|End-to-end script         |`bench/Grob.Benchmarks/EndToEnd/`         |Do the thirteen validation scripts run fast enough?            |
|Stability (separate run)  |`bench/Grob.Benchmarks/Stability/`        |Are there managed-side leaks invisible in single-run timing?   |

**Cadence:**

- Full benchmark run at the close of every sprint.
- Stability test runs at a longer cadence — once per release, or on
  demand. 10,000-iteration runs of all thirteen scripts are too slow
  for the per-sprint close.

**Regression gate:** 5% regression on end-to-end script benchmarks
against the committed baseline is the gate. Compile-time and VM
execution numbers are informational, used to localise regressions
surfaced end-to-end.

**Memory diagnostics — scope:**

- Managed-side retention and GC pressure are covered by `[MemoryDiagnoser]`
  in every benchmark, with stability test as the long-run leak catch.
- Grob-aware memory introspection (closure retention root tracing,
  reachable `GrobArray` counts) is explicitly deferred post-v1. The v1
  architecture preserves the option; the implementation does not.

**No CLI surface:** `grob bench` is not a v1 command. Benchmarks are
implementation infrastructure, not a user-facing feature. Entry point
is `dotnet run -c Release --project bench/Grob.Benchmarks`.

-----

## 13. Explicitly Out of Scope

These features are NOT in v1. They are confirmed as post-MVP in the
decisions log.

|Feature                |Notes                                                                                                                                    |
|-----------------------|-----------------------------------------------------------------------------------------------------------------------------------------|
|Compile to executable  |Transpile to C# via Roslyn — post-MVP                                                                                                    |
|VS Code extension      |TextMate grammar, LSP — post-MVP                                                                                                         |
|JIT compilation        |Explicitly out of scope, permanently                                                                                                     |
|Custom garbage collector|Lean on .NET GC (D-304); no custom mark-and-sweep in v1. Benchmarking (D-302) provides the surface to revisit if a real workload shows GC pressure the .NET collector handles badly|
|Concurrent GC          |Not needed for scripting                                                                                                                 |
|Content mutability     |Mutable binding vs mutable value distinction — `append`/`insert`/`remove`/`clear` ship in v1; full semantic distinction deferred post-MVP|
|AI tutor               |Guided learning companion — post-MVP                                                                                                     |
|User-defined exceptions|Custom typed throws — post-MVP                                                                                                           |
|Range/span indexing    |`[..n]`, `[^n..]`, `[start..end]` — post-MVP                                                                                             |
|User-facing generics   |Declare generic fns/types — post-MVP                                                                                                     |
|`do...while` loop      |Expressible as `while` — post-MVP                                                                                                        |
|Labelled break         |Restructure into function for v1 — post-MVP                                                                                              |
|Return type inference  |v1 requires explicit return types — post-MVP                                                                                             |
|Doc comment semantics  |`///` recognised and discarded in v1 — post-MVP                                                                                          |
|Scientific notation    |`1.5e10` float literals — post-MVP                                                                                                       |
|Tuples                |Additive grammar extension — structs serve the same purpose with named fields in v1                                                      |
|Out parameters        |Not planned — nullable return pattern (`toInt() → int?`) covers try-parse use case                                                       |
|Sparky plushie         |Post-release                                                                                                                             |
|Sparky commissioned art|Execute when project is public                                                                                                           |

-----

## 14. Validation Scripts

These thirteen scripts from the sample scripts document serve as the
release gate. All must compile and run correctly before v1 ships.

|# |Script                            |Exercises                                                                              |
|--|----------------------------------|---------------------------------------------------------------------------------------|
|1 |Bulk file renamer                 |`param`, `fs.list`, `for...in`, `File.rename`, `string.contains/replace`               |
|2 |Photo organiser                   |`fs.list`, `date` components, `fs.ensureDir`, `file.moveTo`, string interpolation      |
|3 |Find large files and report       |`fs.list`, `filter`, `select`, `sort`, struct projection, `formatAs.table`             |
|4 |GitHub repos backup               |`@secure`, `import Grob.Http`, `json` mapAs, `process.runOrFail`, `path.join`          |
|5 |CSV data cleaner                  |`csv.read`, `mapAs`, `filter`, `select`, `sort`, lambdas, `formatAs.table`             |
|6 |Bicep deployment wrapper          |`process.run`, `process.runOrFail`, `try/catch`, `log`, `exit`                         |
|7 |REST API data pull and JSON report|`import Grob.Http`, `json`, `date`, `filter`, `select`, `formatAs.table`               |
|8 |Stale Git branches report         |`process.runOrFail`, closure capture, `select`, `filter`, `formatAs.table`             |
|9 |Disk space monitor                |`process.run`, `json.parse`, `select/case`, `log`, `exit`                              |
|10|Download and verify a file        |`import Grob.Http`, `import Grob.Crypto`, `http.download`, `crypto.verifySha256`       |
|11|Azure resource provisioning helper|`guid.newV5`, `Grob.Crypto`, `map<K,V>` iteration, `Grob.Http`, `env.require`          |
|12|Log file filter by severity/time  |`fs.readText`, regex literal, `Regex.matchAll`, `select`, `filter`, `formatAs.table`   |
|13|Release promotion validated inputs|`@allowed`, `@minLength`, `@maxLength`, `@secure`, `Grob.Http`, `json.encode`          |

-----

## 15. Definition of Done

Grob v1 is ready for public release when:

- [ ] All thirteen core stdlib modules pass their test suites
- [ ] All thirteen validation scripts compile and run correctly
- [ ] The calculator smoke test works
- [ ] The file organiser real-program target works end-to-end
- [ ] `grob run`, `grob repl`, `grob check`, `grob fmt` all work
- [ ] `grob install` / `grob restore` / `grob search` work with NuGet
- [ ] At least one first-party plugin (`Grob.Http`) is published and
  installable
- [ ] Error messages meet the quality bar (file, line, column, suggestion)
- [ ] `winget install Grob.Grob` installs and configures PATH
- [ ] Windows Terminal profile registers correctly
- [ ] Self-contained deployment — no separate .NET install required
- [ ] README, CONTRIBUTING, PLUGINS docs are complete
- [ ] MIT licence file present
- [ ] `grob --version` reports the release version

-----

## 16. v1 Scope-Cut List

The scope-cut list exists as risk insurance. If implementation reveals
unexpected complexity in a sprint and the scope needs to shrink to
ship, the candidates below are the first features to move to v1.1.

Activation is at the project owner's discretion. There is no automatic
gate, no calendar trigger, and no sprint-slip threshold. The list is
consulted only when needed and shelved when not.

### Candidates (in activation order)

**1. Validation decorators** — `@allowed(...)`, `@minLength(n)`,
`@maxLength(n)` on param blocks.

- `@secure` is **not** on this list. It remains in v1 and is exercised
  in Scripts 4, 7, 11 and 13.
- v1 fallback: manual validation in the script body, typically three
  to five lines per validated param (`if (!["dev", "staging", "prod"].contains(env)) { log.error(...); exit(1) }`).
- Savings on activation: one sprint item in Sprint 10 (Script
  Parameters and Decorators). The decorator parsing infrastructure
  remains for `@secure`, so the activation saves implementation time
  on the three validation decorators only, not the decorator system as
  a whole.
- v1.1 re-add is pure grammar addition. No scripts break.
- Scripts affected on activation: Script 13 uses `@allowed`,
  `@minLength` and `@maxLength` directly. Activating this cut requires
  rewriting Script 13 with manual in-body validation (the same
  five-lines-per-param pattern the PowerShell original exhibits) before
  v1 ships. Scripts 1–12 are unaffected.

**2. Regex literal grammar** — `/pattern/flags` with context-sensitive
`/` disambiguation.

- v1 fallback: `regex.compile(pattern, flags)` function form. The
  `regex` module exists; only the grammar form is cut.
- Savings on activation: context-sensitive lexing is the single most
  architecturally novel piece of lexer work in Sprint 1. Cutting it
  simplifies the lexer materially.
- v1.1 re-add is pure grammar addition. No scripts break.
- Scripts affected on activation: Script 12 (Log file filter by
  severity and time) uses regex literals at `readonly LINE := /.../m`
  and `readonly LEVELS := /.../`. Activating this cut requires
  rewriting Script 12 against the `regex.compile(pattern, flags)`
  function form before v1 ships. The script body — `matchAll`,
  `isMatch`, capture-group access — is unchanged on activation.
  Scripts 1–11 and Script 13 are unaffected.

### Defer-gracefully constraints

Both candidates meet the "defer gracefully" constraint: v1 scripts
using the fallback forms continue to work in v1.1 unchanged; v1.1 adds
are purely additive; and no v1 feature becomes useless because a
candidate is cut.

### What is explicitly **not** on the list

Candidates considered and rejected during Session C Part 2:

- **`formatAs.table()` compiler rewrite** — considered and retired during
  Session C Part 1. The rewrite was simplified via the `formatAs`
  reserved-identifier mechanism (D-282) and stays in v1.
- **Switch expression exhaustiveness enforcement** — rejected.
  Non-exhaustive switch expressions are an identity compromise; if
  switch expressions need deferral, the whole feature defers, not the
  checking.
- **Closures with upvalue capture** — rejected. Closures without
  capture are second-class; most functional-style pipelines in the
  sample scripts rely on capture. If Sprint 5 slips, the answer is to
  slip the sprint, not to cut the feature.

-----

*This document was updated July 2026 — Sprint 8 Increment D reconciliation:*
*the §4 Sprint 8 `guid` bullet gains `toCompactString()`, aligning it with*
*`grob-stdlib-reference.md` and `grob-type-registry.md` (both already listed*
*it) and with this document's own §6/§7 "`guid` type summary updated with*
*`toCompactString()`" note below — the summary tables had the method, the*
*Sprint 8 scope bullet had drifted behind them. No behavioural change;*
*documentation-completeness only, surfaced per the increment's own*
*instruction rather than swept quietly.*
*Previous: May 2026 — §2 Assembly Responsibilities gains*
*a clarifying note that the v1 build is six `src/` assemblies and that*
*`Grob.Lsp` is a solution member (D-134) deliberately absent from the v1*
*dependency graph and table because the LSP is post-MVP (§13), not an*
*omission. The note records D-134's dependency direction (Compiler, Core,*
*Runtime; never Vm) and ties the absence back to the day-one*
*`SourceLocation` / `Declaration` requirements (§3.1.1) that make the*
*later addition retrofit-free. No decision changed.*

*This document was updated May 2026 — OQ-005 and OQ-006 closure:*
*§2 architecture bullets rewritten — "Lean on C#'s GC … unless profiling*
*proves it necessary" replaced with the locked "Lean on .NET GC" form*
*(D-304) naming the scalar-slot/heap-object split and the D-302 revisit*
*surface; "Tagged union for values (tentative, OQ-005)" replaced with the*
*locked form (D-303) naming the field shape and the NaN-boxing rejection.*
*§13 Explicitly Out of Scope gains a "Custom garbage collector" row*
*(D-304) beside the existing Concurrent GC and JIT exclusions — a*
*permanent architectural exclusion, not a §16 defer-under-pressure*
*candidate.*
*This document was updated May 2026 — Session 4 side-finding remediation:*
*Sprint 1 scope expanded to make the error-recovering parser an explicit*
*day-one deliverable per D-300 — synchronisation set, error node kinds*
*(`ErrorExpr`, `ErrorStmt`, `ErrorDecl`), no diagnostic cap, statelessness*
*all named in scope. Sprint 1 acceptance criteria gain an explicit error-*
*recovery clause: a file with one malformed expression must still produce*
*a complete AST for surrounding well-formed code; multi-error files must*
*produce one diagnostic per root cause without cascade. AST node types*
*bullet now lists the three error-node kinds explicitly (S-3.3). §7 module*
*table function count for `formatAs` corrected from 6 to 3 (`table`,*
*`list`, `csv` per D-282 — `format.number()` and `format.date()` moved to*
*instance methods on numeric/date types) (S-1.1). §10 ADR citation split:*
*"numbering scheme, allocation rules" cite ADR-0014 (numbering scheme is*
*ADR-0014's actual subject); "stability guarantees" cite ADR-0017 (S-2.3).*
*Previous: May 2026 — Session 1 mechanical sweep:*
*§14 Validation Scripts table expanded from eleven to thirteen rows*
*(Scripts 12 — Log file filter by severity and time, and 13 — Release*
*promotion with validated inputs — added to match the canonical*
*sample-scripts file). §14 heading "These ten scripts" corrected to*
*"These thirteen scripts". §1 Release Gate, §4 acceptance line,*
*§12 testing strategy, §15 Definition of Done, §16 scope-cut*
*paragraphs aligned to the thirteen-script gate. `format` module*
*references in §7 stdlib bullets, §7 module table, §13 retired-*
*features paragraph and Sprint 8 scope/acceptance renamed to*
*`formatAs` per D-282. §14 row-3/5/6/7/8 exercise lists updated for*
*`.select()` per D-280 and `formatAs.table` per D-282. §14 script*
*titles realigned to the canonical sample-scripts script names.*
*§16 cut-impact paragraphs corrected to reflect Script 12 (regex*
*literals) and Script 13 (validation decorators) as the scripts now*
*affected on activation.*
*Previous: April 2026 — Session C Part 2 pre-implementation*
*review: Sprint 7 exception hierarchy expanded from six leaves to ten*
*(`ArithmeticError`, `IndexError`, `ParseError`, `LookupError` added as*
*direct children of `GrobError`); §10 Exception Hierarchy summary updated*
*to reflect ten leaves with throw-site domains. §16 v1 Scope-Cut List*
*added (D-286) — validation decorators and regex literal grammar as cut*
*candidates, no activation gate.*
*Previous: Session B Part 1 pre-implementation review:*
*`TokenKind` keyword list edited (`print` and `exit` removed, `throw` and `finally`*
*added); new "Built-ins" category documents `print`/`exit`/`input` as identifiers*
*resolved at type-check time (D-270). Keyword count unchanged: –2 removed, +2 added.*
*Precedence table in Language Fundamentals §7 reduced from 13 to 12 levels (D-271,*
*D-272) — the "13-level table" reference below is historically correct but superseded.*
*Previous: `env` module*
*corrected to 5 functions; `format` module corrected to 6 functions;*
*`Grob.Stdlib.Tests` corrected to 13 core modules; `guid` type summary*
*updated with `toCompactString()`. Escape sequences aligned with Language*
*Fundamentals (`\r` added). Operator precedence aligned with Language Fundamentals*
*(13-level table). Scientific notation confirmed as post-MVP.*
*Previous: OQ-011 resolved (`Grob.Crypto` API shape expanded);*
*OQ-012 resolved (`process.run()` timeout parameter added); `guid` core module added to*
*built-in types, Sprint 8 scope, and stdlib table; `fs.copy`/`fs.move` overwrite parameter*
*added to Sprint 9; Script 11 (Azure Resource Provisioning Helper) added to validation suite;*
*module count updated to thirteen core modules.*
*April 2026 (Session B Interlude) — TokenKind list gains `readonly`; Sprint 3*
*scope expanded to cover `const` (compile-time) and `readonly` (runtime-once)*
*per D-288/D-289/D-291/D-293.*
*Previous: OQ-007 resolved: `for...in` map iteration added to Sprint 4 scope and acceptance*
*criteria, Section 5 control flow summary updated. `input()` built-in added; array mutation*
*methods confirmed; `map<K, V>` added as first-class built-in type.*
*This document was created April 2026.*
*It draws from: grob-decisions-log.md (authority),*
*grob-solution-architecture.md, grob-language-fundamentals.md,*
*grob-stdlib-reference.md, grob-type-registry.md,*
*grob-vm-architecture.md, grob-install-strategy.md,*
*grob-personality-identity.md,*
*grob-sample-scripts.md, grob-plugins.md,*
*sharpbasic-retrospective.md, ADR-0012 (solution structure and naming),*
*and past design session conversations.*
*Update when the decisions log changes or sprint scope is adjusted.*
