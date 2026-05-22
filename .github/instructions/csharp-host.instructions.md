---
name: 'Grob C# Host Code'
description: 'Architecture, conventions and invariants for the C# that implements Grob — compiler, VM, stdlib, CLI.'
applyTo: '**/*.cs'
---

# Grob C# host code

These rules apply to the C# that *implements* Grob — the compiler, VM, runtime,
stdlib, CLI and plugins. They do not apply to `.grob` language source. This is
ordinary modern C# on .NET 10; the rules below are the things specific to Grob.

The decisions log (`docs/design/grob-decisions-log.md`) is the authority. Where a
rule here cites a `D-###`, that entry is the source of truth — read it when precision
matters.

## Where code belongs (the DAG)

Before adding a type, decide which assembly owns it. The dependency graph is a DAG;
violating it is the most damaging mistake you can make in this codebase.

| Assembly        | Owns                                                                 | May reference            |
|-----------------|----------------------------------------------------------------------|--------------------------|
| `Grob.Core`     | `Chunk`, `OpCode`, `GrobType`, `GrobValue`, `ConstantPool`, `SourceLocation` | Nothing Grob-internal    |
| `Grob.Runtime`  | `IGrobPlugin`, `GrobVM` registration surface, `FunctionSignature`, `Parameter`, `GrobError` hierarchy, `ExitSignal`, `AuthHeader` | `Grob.Core`              |
| `Grob.Compiler` | `Lexer`, `Parser`, AST nodes, `TypeChecker`, `Compiler`, `TypeRegistry` | `Grob.Core`, `Grob.Runtime` |
| `Grob.Vm`       | `VirtualMachine`, `ValueStack`, `CallFrame`, `Globals`, `PluginLoader`, `Upvalue` | `Grob.Core`, `Grob.Runtime` |
| `Grob.Stdlib`   | 13 core modules as `IGrobPlugin` implementations                     | `Grob.Core`, `Grob.Runtime` |
| `Grob.Cli`      | Composition root, CLI commands, REPL, error formatter                | All `src/` assemblies    |
| `Grob.Lsp`      | LSP handlers (post-MVP)                                              | `Grob.Compiler`, `Grob.Core`, `Grob.Runtime` — never `Grob.Vm` |

**`Grob.Compiler` must never reference `Grob.Vm` and vice versa.** If you need a type
shared by both, it goes in `Grob.Core`. `Chunk` lives in `Grob.Core` for exactly this
reason — the compiler writes it, the VM reads it, neither depends on the other.

If a change requires a new project reference, that is a signal to stop and check: it
is almost always wrong, and where it is right it needs a decisions-log entry.

## Naming and style

- The `Grob` prefix is always spelled in full on public runtime types: `GrobType`,
  `GrobValue`, `GrobError`, `GrobArray`, `GrobMap`, `GrobStruct`, `GrobFunction`.
  `Gro` is not a convention here. Internal types need no prefix — the namespace
  disambiguates.
- C# conventions: `PascalCase` for types and public members, `camelCase` for locals
  and parameters, `_camelCase` for private fields.
- `partial class` is used throughout `Grob.Compiler` for physical separation by
  concern (expressions, statements, declarations, control flow) within one namespace.
  When you add to the compiler, add to the right partial file or create a new one
  named for its concern.
- British English in comments, XML docs and any string the user might see.
- No Oxford comma. Never "simply" / "just" / "obviously" in comments or messages.

## Value representation — `GrobValue` (D-303, OQ-005 closed)

`GrobValue` is a hand-rolled `readonly struct` tagged union — this is locked, not
provisional. The shape: a `GrobValueKind` discriminator, a `long _scalar` slot for
primitives (`int`, `float` reinterpreted, `bool`, `nil`), and an `object? _reference`
slot for heap types. Nine discriminator variants.

- Construct values through the factory surface; do not poke fields directly from
  outside the struct. The encapsulation boundary is deliberate (D-297).
- **NaN boxing is rejected and must not be proposed.** The .NET GC is a moving
  collector that never scans a `long` for references; packing a managed pointer into
  one breaks GC tracing silently. The full rationale is in D-303 — do not relitigate
  it.
- Primitives never reach the heap and generate zero GC pressure. Heap types are
  ordinary CLR objects.

## Memory — lean on the .NET GC (D-304, OQ-006 closed)

There is no custom garbage collector and there will not be one in v1. Heap objects
(`string`, `GrobArray`, `GrobMap`, `GrobStruct`, `GrobFunction`, plugin reference
types) are allocated with `new` and reclaimed by the CLR. Do not write a mark phase,
a sweep phase, an allocation hook, a `CollectGarbage()` entry point, or a custom heap
structure. Do not add a finaliser to any runtime-internal type. "Custom garbage
collector" is permanently out of scope (`grob-v1-requirements.md` §13).

## The compiler pipeline

```
source → Lexer → Parser → TypeChecker → (Optimiser, deferred) → Compiler → Chunk
```

- **Lexer** tokenises; reports all errors, never stops at first. `TokenKind` is the
  complete enum from `grob-v1-requirements.md` §3.4 — defined in full, not grown.
- **Parser** is error-recovering and stateless (D-300). See the dedicated rule below.
- **TypeChecker** is the first AST visitor pass. It infers types on `:=`, validates
  annotations, applies the arithmetic and comparison type rules, sets `ResolvedType`
  and `Declaration` on every identifier node, and collects all errors. It is a
  two-pass checker (D-166): pass 1 registers all top-level declarations, pass 2
  validates bodies — this is what allows forward references to types and functions
  declared later in the same file.
- **Compiler** is the second visitor pass. It emits typed opcodes using the type
  checker's annotations — `AddInt` / `AddFloat` / `Concat`, never a generic `Add`
  with a runtime type switch. A program containing any `Error`-typed node fails type
  checking and never reaches the emitter.

All three passes use the visitor pattern. Every visitor must handle the three error
node kinds (`ErrorExpr`, `ErrorStmt`, `ErrorDecl`).

## Error-recovering parser (D-300) — this is a Sprint 1 invariant

Build recovery in from the first parse method; retrofitting it means touching every
one of them.

- **Synchronisation set:** a statement-boundary newline outside any open bracket; the
  closing `}` of an enclosing block; the start keyword of a top-level declaration
  (`fn`, `type`, `param`, `import`, `const`, `readonly`). The lexer tracks bracket
  nesting depth so a newline inside parentheses is not a recovery anchor. EOF
  terminates recovery unconditionally.
- **On failure:** emit a diagnostic, build the appropriate error node carrying its
  source range and the diagnostic message, advance to the next anchor, resume.
- **Cascade suppression:** error nodes are typed `Error`, assignable to and from every
  type; operations on `Error` produce no further diagnostics. An `ErrorDecl` registers
  a synthetic symbol-table entry so references to a broken declaration do not produce
  "undefined identifier" cascades. One diagnostic per root cause.
- **Stateless:** no state across files or invocations beyond the token stream and the
  AST being built. Same input, same diagnostics, regardless of history.
- Spec text and worked example: `grob-language-fundamentals.md` §29.

## The OpCode set (`grob-v1-requirements.md` §3.3)

`OpCode : byte` is complete from Sprint 2 — written out once, never grown additively.
Adding an opcode is a decision-logged act (governed by ADR-0013 for stability), not
casual editing. When you touch the opcode set you touch three things together: the
enum, the compiler's opcode-selection logic, and the VM's dispatch switch. The skill
`adding-an-opcode` in `.github/skills/` walks the full procedure.

FOR loops are lowered to WHILE by the compiler — the VM never sees a FOR opcode.
Forward jumps use backpatching; backward jumps (loops) use known positions. `&&` and
`||` short-circuit via jumps, not dedicated opcodes.

## Diagnostics infrastructure

The `Diagnostics` namespace exists from Sprint 1: a `Diagnostic` record (severity,
message, source location, optional suggestion) and a `DiagnosticBag` that accumulates
without stopping. Every compile-time error maps to a registered code in
`docs/design/grob-error-codes.md` (`Exxxx`, immutable once shipped per ADR-0017).
When you add a diagnostic, use an existing code or register a new one in the right
category range (§Category Map). Do not emit a bare string with no code.

## Tests

- Five xUnit projects mirror `src/`. Every change ships tests.
- **`Grob.Compiler.Tests` is the highest-priority surface** — given source, assert
  emitted bytecode. This is where bugs live (confirmed by the SharpBASIC
  retrospective).
- `Grob.Vm.Tests` constructs `Chunk` instances by hand and asserts execution results
  — no compiler involved.
- `Grob.Integration.Tests` runs `.grob` source end-to-end and asserts stdout, stderr
  and exit code.
- Assert that every identifier node carries non-null `ResolvedType` and `Declaration`
  after type checking — this keeps the LSP-enabling invariant from regressing.
- Parser tests must include recovery cases: a malformed expression mid-function still
  yields a complete AST for the rest of the file; multi-error files yield one
  diagnostic per root cause with no cascade.

## Performance

Correctness gates on `dotnet test`; performance gates separately via
`bench/Grob.Benchmarks` (D-302). Do not micro-optimise speculatively. The hot path is
I/O — REST, JSON, process spawning, file reads — not tight numeric loops. If you
believe a change affects performance, the benchmark harness is how you prove it, not
assertion. `[MemoryDiagnoser]` is on every benchmark; a 5% end-to-end regression
against the committed baseline is the gate.

## When to consult Microsoft Learn

For .NET 10 BCL APIs (`System.Text.Json`, `Span<T>`, `System.IO`, BenchmarkDotNet
attributes, OmniSharp LSP types), prefer the Microsoft Learn MCP server over training
recall — it is the authoritative, current source for the Microsoft-flavoured stack
that Grob's host code depends on. Ground API usage there rather than guessing
signatures.
