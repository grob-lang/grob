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

### Release Gate — All Eleven Sample Scripts

The sample scripts document contains eleven real-world scripts that validate
the API surface. All eleven must compile and run correctly against the v1
implementation before public release. Any script that fails reveals a gap
in the implementation.

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
  VM ─────────── Fetch-decode-execute loop (stops on FIRST runtime error)
    |
    +── Value Stack (int/float/bool live here directly)
    +── Call Frames (CallFrame[256], fixed array)
    +── Globals (built-ins + plugin functions)
    +── Plugin Loader (IGrobPlugin assemblies at startup)
```

### C# Solution Structure

> **Authority:** `Grob___Solution_Architecture.md` (confirmed April 2026).
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
└── tests/
    ├── Grob.Core.Tests/
    ├── Grob.Compiler.Tests/    ← Highest priority — this is where bugs will live
    ├── Grob.Vm.Tests/
    ├── Grob.Stdlib.Tests/
    └── Grob.Integration.Tests/ ← End-to-end: source file → stdout/stderr/exit code
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
- **Lean on C#’s GC** — structs for value types (int, float, bool), classes
  for heap objects (string, array, function) only. No custom mark-sweep
  unless profiling proves it necessary.
- **Tagged union for values** (tentative, OQ-005) — `GrobValue` struct with
  `ValueKind` enum, `long Raw` for primitives, `object? Ref` for heap
  types.
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

See `Grob___Tooling___Strategy.md` for full rationale.

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
Keywords:       fn, if, else, while, for, in, return, const, type, param,
                import, as, try, catch, select, case, default, break,
                continue, true, false, nil, step, switch, print, exit
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
  `Grob___Solution_Architecture.md`. .NET 10, self-contained deployment
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
  `catch`, `param` block, `import`, `const`).
- AST node types — every node carries source location.
- `DiagnosticBag`, `Diagnostic`, error formatting to stderr.
- Line continuation: trailing-token heuristic (line ends with operator,
  comma, opening bracket, opening brace, `=>`) plus leading-dot chain
  suppression.
- Lexer and parser tests — comprehensive.

**Acceptance:** Lex and parse any valid Grob v1 source file into a correct
AST. Report clear errors with line/column for any invalid source.

### Sprint 2 — Type Checker and Compiler Core

**Delivers:** Static type checking, bytecode compilation, VM execution of
arithmetic expressions and `print()`.

**Scope:**

- `OpCode` enum — complete (see §3.3).
- `Value` struct — tagged union: `ValueKind` enum, `long Raw`, `object? Ref`.
- `Chunk` — bytecode array, constant pool, line number array.
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

**Acceptance:** `print(2 + 3 * 4)` compiles, runs and prints `14`. Type
errors (e.g. `"hello" + 42`) produce clear compile-time diagnostics with
line numbers.

### Sprint 3 — Variables, Scope and REPL

**Delivers:** Variable declaration/assignment, `const`, scope chain, REPL.

**Scope:**

- `:=` declaration — local scope, type inferred.
- `=` reassignment — walks scope chain.
- `const` — immutable binding, reassignment is a compile error.
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
and used in expressions. `const` prevents reassignment. Nil safety works.
String interpolation works. `grob run hello.grob` executes a script
file.

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
- `break` — exits innermost loop. Compile error outside a loop.
- `continue` — skips to next iteration. Compile error outside a loop.
- `break`/`continue` inside `select` do NOT apply to the `select` — they
  apply to an enclosing loop if one exists.
- Logical `&&` and `||` — short-circuit using `JumpIfFalse`/`JumpIfTrue`.
  Not dedicated opcodes.
- Ternary `condition ? a : b` — jump-based compilation.
- Switch expression: `value switch { pattern => result, _ => default }` —
  exhaustiveness enforced by type checker. All arms same type.

**Acceptance:** All control flow constructs work correctly. Array `for...in`
and map `for k, v in` iteration work correctly. Single-identifier `for k in`
on a map produces a clear compile error. The calculator smoke test script
runs. Nested loops with `break`/`continue` behave as specified.

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
- `BytecodeFunction` and `NativeFunction` as function representations.
  VM dispatches transparently.
- Native function registration mechanism: `RegisterNative(name, signature, implementation)`.
- Flow-sensitive type narrowing for optionals: inside `if (x != nil) { }`
  the type checker narrows `x` from `T?` to `T`.

**Acceptance:** Functions call and return correctly. Lambdas work in
`filter`, `map`, `sort`. Closures capture enclosing variables. Named
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
  `ProcessError`, `NilError`, `RuntimeError`.
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
`format`, `guid` modules as `IGrobPlugin` implementations.

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
  `RuntimeError` if absent), `set(key, value)`, `has(key) → bool`,
  `all() → map<string, string>`.
- **`log`** — `debug()`, `info()`, `warning()`, `error()`. All to stderr.
  `setLevel()`. `debug` suppressed by default, visible with `--verbose`.
- **`format`** — `format.table()`, `format.list()`, `format.csv()`.
  Column names derived from type field registry at compile time. Works
  on named structs and anonymous structs.
- **`guid`** — `guid.newV4()`, `guid.newV7()`, `guid.newV5()`.
  `guid.parse()`, `guid.tryParse()`, `guid.empty`. Well-known
  namespaces: `guid.namespaces.dns`, `guid.namespaces.url`,
  `guid.namespaces.oid`. `guid` type with `version`, `isEmpty`
  properties, `toString()`, `toUpperString()` methods. `==`, `!=`
  operators. Compile-time validation on `guid.parse()` with string
  literal argument. `guid` is a primitive type distinct from `string`.

**Acceptance:** Each module’s full API works. `math.sqrt(9.0)` returns
`3.0`. `env.require("MISSING")` throws `RuntimeError`. `log.error()`
writes to stderr. `format.table()` produces aligned column output.

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

**Delivers:** `grob fmt`, `grob check`, `grob new`, `grob version`,
`--help`, Windows Terminal profile, first-run acknowledgement,
`.grobc` caching, final polish.

**Scope:**

- `grob fmt <file>` — format source code. Never automatic, always opt-in.
  Same-line braces, consistent indentation, `snake_case` warnings.
- `grob check <file>` — run lexer, parser and type checker only. Report
  all diagnostics. Do not execute.
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
Terminal profile registers correctly. All eleven sample scripts compile and
run.

-----

## 5. Language Features — Complete v1 Surface

### Syntax

- Same-line opening braces `{`. No semicolons. Newline terminates
  statements.
- Line continuation: trailing-token heuristic plus leading-dot chain
  suppression.
- Comments: `//` single-line, `/* */` block, `///` recognised and
  discarded (semantics post-MVP).
- British English in all documentation. Oxford comma never.

### Variables and Bindings

- `:=` declares and assigns (first use). Type inferred.
- `=` reassigns — walks scope chain. Name must exist.
- `const` — immutable binding. Reassignment is a compile error.
- No uninitialised variables. Every declaration requires a value or
  explicit `?` nil.
- Explicit type annotation: `name: Type := value` — optional on locals,
  required when initialising to `nil` or `[]`.

### Operators

- **Arithmetic:** `+`, `-`, `*`, `/`, `%`. `int / int → int` (truncating).
  `int op float → float` (implicit promotion).
- **Comparison:** `==`, `!=`, `<`, `>`, `<=`, `>=`.
- **Logical:** `&&`, `||`, `!`. Short-circuit evaluation.
- **Assignment:** `:=`, `=`, `+=`, `-=`, `*=`, `/=`, `%=`.
- **Increment/decrement:** `++`, `--` — postfix, statement only (not
  expression).
- **String:** `+` for concatenation. `${...}` interpolation in
  double-quoted strings.
- **Nil:** `??` coalescing, `?.` optional chaining.
- **Range:** `..` inclusive (numeric `for` loops only).
- **Lambda:** `=>` arrow.

### Operator Precedence (highest to lowest)

1. Postfix: `()`, `[]`, `.`, `?.`
2. Unary: `-`, `!`
3. Multiplicative: `*`, `/`, `%`
4. Additive: `+`, `-`
5. Range: `..`
6. Comparison: `<`, `>`, `<=`, `>=`
7. Equality: `==`, `!=`
8. Logical AND: `&&`
9. Logical OR: `||`
10. Ternary: `? :`
11. Nil coalescing: `??`
12. Assignment: `=`, `+=`, `-=`, `*=`, `/=`, `%=`
13. Lambda: `=>`

### Literals

- **Int:** `42`, `0xFF`, `0b1010`, `1_000_000`.
- **Float:** `3.14`, `0.5`. Leading dot `.5` is NOT valid.
  Scientific notation `1.5e10` is NOT in v1.
- **String:** `"hello"` with escape sequences (`\n`, `\r`, `\t`, `\\`, `\"`, `\$`).
  Interpolation: `"Hello ${name}"`. Single backtick raw: `C:\path\file`
  (inline verbatim, no escapes, no newlines). Triple backtick raw: multiline
  verbatim block, content preserved exactly as written, no trimming.
- **Bool:** `true`, `false`.
- **Nil:** `nil`.
- **Array:** `[1, 2, 3]`. Empty array requires type: `items: int[] := []`.

### Control Flow

- `if (condition) { }` / `else if (condition) { }` / `else { }` —
  parentheses required on conditions.
- `while (condition) { }`
- `for item in collection { }` / `for i, item in collection { }` — arrays
- `for k, v in myMap { }` — maps (two-identifier form required)
- `for i in 0..10 { }` / `for i in 0..100 step 5 { }` — numeric ranges
- `select (value) { case X { } default { } }` — first match, no
  fall-through.
- `break`, `continue` — innermost loop only. Compile error outside loop.
- Ternary: `condition ? a : b`.
- Switch expression: `value switch { pattern => result, _ => default }`.

### Functions

- `fn name(p: Type, p: Type = default): ReturnType { }`.
- Return type explicit (required in v1).
- Named parameter calling: positional first, named after. Only defaulted
  parameters may be named.
- Lambdas: `x => expr`, `(a, b) => expr`, `x => { block }`.
- Closures capture enclosing scope via upvalues.

### Types

- `type Name { field: Type, field: Type = default }`.
- Named construction: `TypeName { field: value }`.
- Anonymous struct: `#{ field: value }`.
- Field access: `instance.field`. Assignment: `instance.field = value`.

### Script Parameters

- `param name: Type` / `param name: Type = default`.
- `@secure`, `@allowed(...)`, `@minLength(n)`, `@maxLength(n)`.

### Error Handling

- `try { } catch TypedError e { } catch e { }`.
- Bare `catch e` is catch-all — must be last.

### Built-in Functions

- `print(values...)` — variadic, stdout, newline appended, void.
- `input(prompt: string = "")` — reads one line from stdin, returns `string`.
- `exit(code: int = 0)` — exits script. Cannot be caught.

### Modules

- Core modules auto-available — no import.
- Plugins: `import Grob.Http` after `grob install`.

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

### Built-in Type Methods (v1 — from Type Registry)

> **Authority:** `Grob___Type___Registry.md`. The registry is the definitive
> list — this section summarises it. If the two conflict, the registry wins.

**string:** `length`, `isEmpty` (properties). `toInt()`, `toFloat()`,
`trim()`, `trimStart()`, `trimEnd()`, `upper()`, `lower()`, `split()`,
`contains()`, `startsWith()`, `endsWith()`, `replace()`, `indexOf()`,
`lastIndexOf()`, `substring()`, `padLeft()`, `padRight()`, `repeat()`,
`truncate()`, `left()`, `right()`, `toString()` (methods).

**int:** `toString()`, `toFloat()`, `abs()` (methods).
`int.min()`, `int.max()`, `int.clamp()` (static).

**float:** `toString()`, `toInt()`, `round()`, `round(decimals)`,
`floor()`, `ceil()`, `abs()` (methods).
`float.min()`, `float.max()`, `float.clamp()` (static).

**bool:** `toString()` (method).

**array (T[]):** `length`, `isEmpty` (properties). `first()`, `last()`,
`contains()`, `filter()`, `map()`, `each()`, `sort()`, `select()`
(read-only / functional — return new arrays). `append()`, `insert()`,
`remove()`, `clear()` (mutation — compile error on `const`-bound array).

**map<K, V>:** `length`, `isEmpty`, `keys`, `values` (properties).
`get()`, `set()`, `contains()`, `remove()`, `clear()` (methods).
`m[key]` indexer (read), `m[key] = value` indexer (write). v1: string
keys only. Construction: `map<string, string>{ "key": value }`.
`env.all()` and `Response.headers` return `map<string, string>`.

**guid:** `version`, `isEmpty` (properties). `toString()`,
`toUpperString()`, `toCompactString()` (methods). `==`, `!=` (operators).
Static: `guid.newV4()`, `guid.newV7()`, `guid.newV5()`, `guid.parse()`,
`guid.tryParse()`, `guid.empty`. Namespaces: `guid.namespaces.dns`,
`guid.namespaces.url`, `guid.namespaces.oid`. Primitive type distinct
from `string`.

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
|`format` |6 functions              |—                    |
|`guid`   |6 statics, 3 namespaces  |`guid`               |

Full API detail for each module is in `Grob___Stdlib___Reference.md`
and the confirmed decisions in `Grob___Decisions___Context_Log.md`.

-----

## 8. CLI — v1 Commands

|Command                          |Description                               |
|---------------------------------|------------------------------------------|
|`grob run <file>`                |Compile and execute a script              |
|`grob run <file> --params <file>`|Execute with parameter file               |
|`grob run <file> --verbose`      |Execute with debug output                 |
|`grob repl`                      |Interactive REPL (`G>` prompt)            |
|`grob check <file>`              |Lex, parse, type-check only — no execution|
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

- Plugins are C# class libraries implementing `IGrobPlugin`.
- `Grob.Runtime` NuGet package is the public SDK — versioned
  independently from the runtime.
- Plugins register native functions with type signatures. The type
  checker validates call sites statically.
- Standard library modules are plugins, auto-registered at startup.
- External plugins require `import` and prior `grob install`.

### First-Party Plugins (v1)

|Plugin       |Purpose                  |Status   |
|-------------|-------------------------|---------|
|`Grob.Http`  |HTTP client, auth helpers|v1 target|
|`Grob.Crypto`|Checksums and hashing — `sha256File()`, `md5File()`, `sha256()`, `md5()`, `verifySha256()`, `verifyMd5()`. All hex output lowercase. File functions stream internally. Verify functions use constant-time comparison.|v1 target|
|`Grob.Zip`   |Archive compress/expand  |v1 target|

### Security

Plugin loading is running arbitrary code. Documented prominently. No
sandbox claims. No sandbox attempted. The safe path is the obvious path.

-----

## 10. Error Handling

### Two-Mode Strategy

1. **Compile time** — type checker collects ALL errors. Never stops at
   first. Reports all diagnostics after the phase completes.
2. **Runtime** — VM stops on FIRST unhandled exception. Grob-quality
   diagnostic produced.

### Exception Hierarchy

```
GrobError (root)
  ├── IoError         — file system operations
  ├── NetworkError    — HTTP and network operations
  ├── JsonError       — JSON parse/serialise failures
  ├── ProcessError    — external process execution
  ├── NilError        — nil access on non-nullable path
  └── RuntimeError    — all other runtime failures
```

### Error Message Format

```
error[E001]: type mismatch
  --> deploy.grob:14:12
   |
14 |     count := "hello" + 42
   |              ^^^^^^^^^^^^
   |
   = expected: string + string, or int + int
   = got: string + int
   = help: use string interpolation: "${count}${42}"
```

### Error Message Rules

- Show variable names and types. Never show values.
- `--verbose` overrides the value suppression for debugging.
- Suggested fix when the fix is obvious.
- Never “simply” in any error message text.
- No emoji in any compiler or CLI output.
- Errors to stderr. Results to stdout.

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
|`Grob.Integration.Tests`|End-to-end through full pipeline                                                                               |Given `.grob` source file → assert stdout, stderr, exit code. The eleven sample scripts live here            |50+            |

### Test Discipline

- Every sprint includes tests for the features it delivers.
- Compiler tests are the highest priority — this is where bugs will
  live (per SharpBASIC retrospective).
- The VM loop can be trusted once verified on simple cases.
- Edge cases and failure paths are tested as thoroughly as the happy path.
- The eleven sample scripts are integration tests in `Grob.Integration.Tests`.

-----

## 13. Explicitly Out of Scope

These features are NOT in v1. They are confirmed as post-MVP in the
decisions log.

|Feature                |Notes                                                                                                                                    |
|-----------------------|-----------------------------------------------------------------------------------------------------------------------------------------|
|Compile to executable  |Transpile to C# via Roslyn — post-MVP                                                                                                    |
|VS Code extension      |TextMate grammar, LSP — post-MVP                                                                                                         |
|JIT compilation        |Explicitly out of scope, permanently                                                                                                     |
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

These ten scripts from the sample scripts document serve as the release
gate. All must compile and run correctly before v1 ships.

|# |Script                          |Exercises                                                                        |
|--|--------------------------------|---------------------------------------------------------------------------------|
|1 |Bulk file renamer               |`param`, `fs.list`, `for...in`, `File.rename`, `string.contains/replace`         |
|2 |Photo organiser                 |`fs.list`, `date` components, `fs.ensureDir`, `file.moveTo`, string interpolation|
|3 |Azure DevOps stale branch report|`import Grob.Http`, `json`, `date`, `filter`, `format.table`                     |
|4 |Bicep deployment wrapper        |`process.run`, `env.require`, `param`, `@secure`, `try/catch`                    |
|5 |CSV data cleaner                |`csv.read`, `filter`, `map`, `csv.write`, lambdas                                |
|6 |Log file parser                 |`fs.readLines`, `regex`, `mapAs`, `sort`, `format.table`                         |
|7 |Disk space monitor              |`process.run`, `json.parse`, `select/case`, `log`, `exit`                        |
|8 |Multi-repo Git status           |`fs.list`, `process.run`, `for...in`, `format.table`                             |
|9 |SharePoint list export          |`import Grob.Http`, `json`, `csv.write`, `while` pagination                      |
|10|Self-updating agent hook        |`json.stdin`, `select/case`, `process.run`, `json.stdout`                        |
|11|Azure resource provisioning helper|`guid.newV5`, `Grob.Crypto`, `map<K,V>` iteration, `Grob.Http`, `env.require`  |

-----

## 15. Definition of Done

Grob v1 is ready for public release when:

- [ ] All thirteen core stdlib modules pass their test suites
- [ ] All eleven validation scripts compile and run correctly
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

*This document was updated April 2026 — pre-implementation review: `env` module*
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
*Previous: OQ-007 resolved: `for...in` map iteration added to Sprint 4 scope and acceptance*
*criteria, Section 5 control flow summary updated. `input()` built-in added; array mutation*
*methods confirmed; `map<K, V>` added as first-class built-in type.*
*This document was created April 2026.*
*It draws from: Grob___Decisions___Context_Log.md (authority),*
*Grob___Solution_Architecture.md, Grob___Language_Fundamentals.md,*
*Grob___Stdlib___Reference.md, Grob___Type___Registry.md,*
*Grob___VM_Architecture___Design_Notes.md, Grob___Install___Strategy.md,*
*Grob___Personality___Identity.md,*
*Grob___Sample_Scripts___API_Surface_Validation.md, Grob_Plugins.md,*
*SharpBASIC___Retrospective.md, ADR-0007 (solution structure and naming),*
*and past design session conversations.*
*Update when the decisions log changes or sprint scope is adjusted.*