# Grob C# host code

These rules apply to the C# that *implements* Grob — the compiler, VM, runtime,
stdlib, CLI and plugins (`**/*.cs`). They do not apply to `.grob` language source.
This is ordinary modern C# on .NET 10; the rules below are the things specific to
Grob.

The decisions log (`docs/design/grob-decisions-log.md`) is the authority. Where a
rule here cites a `D-###`, that entry is the source of truth — read it when precision
matters.

## Language and target

- **C# 14 on .NET 10 LTS** (D-310). `<LangVersion>14</LangVersion>`,
  `<TargetFramework>net10.0</TargetFramework>`.
- `<Nullable>enable</Nullable>` everywhere. Nullable reference types are not optional.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` on every project.
- `<ImplicitUsings>enable</ImplicitUsings>`.

## Where code belongs (the DAG)

Before adding a type, decide which assembly owns it. The dependency graph is a DAG;
violating it is the most damaging mistake you can make in this codebase.

| Assembly        | Owns                                                                 | May reference            |
|-----------------|----------------------------------------------------------------------|--------------------------|
| `Grob.Core`     | `Chunk`, `OpCode`, `GrobType`, `GrobValue`, `ConstantPool`, `SourceLocation`, `Diagnostic`, AST node types, `GrobError` hierarchy, ambient-dependency interfaces | Nothing Grob-internal; no NuGet beyond the .NET 10 BCL |
| `Grob.Runtime`  | `IGrobPlugin`, `GrobVM` registration surface, `FunctionSignature`, `Parameter`, `ExitSignal`, `AuthHeader` | `Grob.Core`              |
| `Grob.Compiler` | `Lexer`, `Parser`, AST builder, `TypeChecker`, `Compiler`, `TypeRegistry`, `.grobc` writer | `Grob.Core`, `Grob.Runtime` |
| `Grob.Vm`       | `VirtualMachine`, `ValueStack`, `CallFrame`, `Globals`, `PluginLoader`, `Upvalue`, `.grobc` reader | `Grob.Core`, `Grob.Runtime` |
| `Grob.Stdlib`   | 13 core modules as `IGrobPlugin` implementations                     | `Grob.Core`, `Grob.Runtime` |
| `Grob.Cli`      | Composition root, CLI commands, REPL, error formatter, concrete ambient implementations | All `src/` assemblies    |
| `Grob.Lsp`      | LSP handlers (post-MVP)                                              | `Grob.Compiler`, `Grob.Core`, `Grob.Runtime` — never `Grob.Vm` |

**`Grob.Compiler` must never reference `Grob.Vm` and vice versa.** If you need a type
shared by both, it goes in `Grob.Core`. `Chunk` lives in `Grob.Core` for exactly this
reason — the compiler writes it, the VM reads it, neither depends on the other. The
`.grobc` binary format splits accordingly: writer in `Grob.Compiler`, reader in
`Grob.Vm`, format types in `Grob.Core`.

Hard rules, restated so they are unmissable:

1. **`Grob.Compiler` and `Grob.Vm` never reference each other.** The single most
   important constraint. If you find yourself wanting `using Grob.Vm;` in a
   `Grob.Compiler` file (or vice versa), stop — the type you want belongs in
   `Grob.Core`.
2. **`Grob.Core` references nothing.** No NuGet beyond the .NET 10 BCL, no other Grob
   project. It is the neutral shared foundation.
3. **`Grob.Lsp` does not reference `Grob.Vm`.** The language server is compile-time
   only; runtime concerns stay out.
4. **No upward references.** `Grob.Core` cannot reference `Grob.Compiler`;
   `Grob.Runtime` cannot reference `Grob.Stdlib`. Arrows point down only.

If a change requires a new project reference, that is a signal to stop and check: it
is almost always wrong, and where it is right it needs a decisions-log entry.

## The seven test projects

One test project per production project. Same DAG, same naming:

```
Grob.Core.Tests        <- references Grob.Core
Grob.Runtime.Tests     <- references Grob.Runtime
Grob.Compiler.Tests    <- references Grob.Compiler
Grob.Vm.Tests          <- references Grob.Vm
Grob.Stdlib.Tests      <- references Grob.Stdlib
Grob.Cli.Tests         <- references Grob.Cli
Grob.Lsp.Tests         <- references Grob.Lsp
```

A test project references exactly one production project plus test-support libraries.
If you find yourself wanting to reference two production projects from one test
project, the production design is probably wrong — surface it rather than papering
over it. Each production project declares
`[assembly: InternalsVisibleTo("Grob.<Project>.Tests")]` so internal types are
reachable from tests. The detailed test conventions live in `tests/CLAUDE.md`.

## Central package management

Shared dependencies — production and test — are version-managed in
`Directory.Packages.props` at the solution root. Individual `.csproj` files declare
`<PackageReference Include="..." />` **without** a version; the version is set
centrally. Any new `<PackageReference>` carrying an explicit `Version=` attribute is
a defect.

Test dependencies are `Microsoft.NET.Test.Sdk`, `xunit` and
`xunit.runner.visualstudio`, declared per test project, plus `coverlet.collector`,
which `Directory.Build.props` adds automatically to every `*.Tests` project. The
test suite uses plain xUnit assertions; `FluentAssertions` and `FsCheck` are
deliberately not used and are not in `Directory.Packages.props`.

## Namespaces follow folders

The namespace matches the project name and folder path exactly, for **every**
subfolder. `Grob.Core/Diagnostics/Diagnostic.cs` declares
`namespace Grob.Core.Diagnostics;`. A file at
`Grob.Compiler/Ast/Expressions/BinaryExpr.cs` declares
`namespace Grob.Compiler.Ast.Expressions;` — not `Grob.Compiler.Ast;`. When you
create or move a file, the namespace on line 1 must mirror the full folder path under
`src/` or `tests/`. If sibling files disagree, fix them; never copy a wrong namespace
from a neighbour. If parent-namespace callers would otherwise need a `using` for
every new subfolder, add a single `GlobalUsings.cs` at the project root rather than
weakening the per-file rule.

## What goes where

| Concern | Project |
| --- | --- |
| `SourceLocation`, `Diagnostic`, `GrobError` hierarchy, AST node types | `Grob.Core` |
| `GrobValue` and its variants; `GrobType` (runtime type-tag enum and metadata) | `Grob.Core` |
| Lexer, parser, type checker, emitter, `.grobc` writer | `Grob.Compiler` |
| Bytecode interpreter, call stack, value stack, `.grobc` reader | `Grob.Vm` |
| `IGrobPlugin` interface and plugin host | `Grob.Runtime` |
| `fs`, `strings`, `json`, `process`, the other core modules | `Grob.Stdlib` |
| `grob` command-line entry, argument parsing, REPL, error formatting | `Grob.Cli` |
| LSP protocol handling | `Grob.Lsp` |
| Ambient abstractions (`IClock`, `IFileSystem`, `IConsole`, `IProcessRunner`) | `Grob.Core` (interface) + `Grob.Cli` (concrete) |

Before writing the namespace on a new file, ask: which project owns this type; does
that project have access to everything the type needs; if not, can the missing thing
move to `Grob.Core`, or does the type belong elsewhere? If you cannot answer
confidently, the file is not ready to write — propose the design first.

## Naming and style

- The `Grob` prefix is always spelled in full on public runtime types: `GrobType`,
  `GrobValue`, `GrobError`, `GrobArray`, `GrobMap`, `GrobStruct`, `GrobFunction`.
  `Gro` is not a convention here. Internal types need no prefix — the namespace
  disambiguates.
- `PascalCase` for types, methods, properties, public fields, constants.
  `camelCase` for locals and parameters. `_camelCase` for private fields — no `m_`
  prefix, no Hungarian. Interface names start with `I`; generic type parameters
  start with `T`.
- In non-test, non-private code, identifiers have no underscores. The two exceptions
  are `_camelCase` private fields and `Subject_BehaviourClause` test method names
  (a single mid-identifier underscore).
- **Same-line braces (K&R).** `dotnet format` is the source of truth and enforces
  opening braces on the same line for types, methods and control-flow blocks. Do not
  raise review comments about brace placement — the formatter overrides any manual
  deviation.
- Spaces inside string interpolation braces: `$"{name}"` not `$"{ name }"`.
- Explicit access modifiers always. No reliance on default `private`.
- `var` only when the right-hand side makes the type obvious (`new Foo()`, literals,
  factory methods with the type in the name). Otherwise spell out the type.
- Expression-bodied members for one-liners that read naturally; block bodies for
  anything multi-statement.
- Pattern matching and switch expressions over chained `if/else if` when the shape
  fits. Exhaustive switch on enums.
- `is null` / `is not null` over `== null` / `!= null`. Always.
- `readonly` fields wherever the value is set once. `readonly struct` /
  `readonly record struct` for value types.
- No regions. No commented-out code. No `TODO` without an issue link.
- `partial class` is used throughout `Grob.Compiler` for physical separation by
  concern (expressions, statements, declarations, control flow) within one namespace.
  Add to the right partial file or create a new one named for its concern.
- British English in comments, XML docs and any string the user might see. No Oxford
  comma. Never "simply" / "just" / "obviously".

## File layout

- One public type per file. File name matches the type name exactly.
- `using` directives first, then a blank line, then the file-scoped namespace
  declaration, then the type. `using` directives sit outside the namespace, sorted
  `System.*` first, then other external packages, then `Grob.*`.
- Line endings are **LF** for all `.cs` files (`.gitattributes` enforces this). Do
  not convert to CRLF.

## Immutability

- Prefer immutable types. Records for data, classes for behaviour with state, structs
  for small value-like things measured to matter.
- `init` accessors instead of `set` for properties settable only during construction.
- Collections returned from public APIs are read-only types (`IReadOnlyList<T>`,
  `IReadOnlyDictionary<K, V>`). Never return `List<T>` from a public method.

## Errors and exceptions

- Throw exceptions for programmer errors and genuinely exceptional conditions only.
  Recoverable failures in the compiler pipeline use the diagnostic model (error
  nodes, `Diagnostic` records), not exceptions.
- Throw the most specific exception type that fits. `ArgumentNullException` and
  `ArgumentException` for invalid arguments at boundary methods.
- Never catch `Exception` except at the outermost CLI entry point. Never catch and
  swallow. Never catch and log-then-throw — log at the catch site only if you have
  context the caller cannot reconstruct.

## Async

- `async`/`await` always. No `.Result`, no `.Wait()`, no `Task.Run` to escape sync
  contexts.
- Suffix async method names with `Async` unless they implement an interface method
  that does not.
- `CancellationToken` as the last parameter on any async method that could take
  longer than a few milliseconds.
- `ConfigureAwait(false)` is not required — there is no synchronisation context to
  deadlock against in console apps and unit tests.

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

## Error-recovering parser (D-300) — a Sprint 1 invariant

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
enum, the compiler's opcode-selection logic and the VM's dispatch switch. The
`adding-an-opcode` skill walks the full procedure.

FOR loops are lowered to WHILE by the compiler — the VM never sees a FOR opcode.
Forward jumps use backpatching; backward jumps (loops) use known positions. `&&` and
`||` short-circuit via jumps, not dedicated opcodes.

**Emission must respect the closed enum and the runtime representation — the type
checker permitting an operation does not mean a direct opcode exists for the resolved
operand types** (D-315). The set is intentionally asymmetric: string `<`/`>` have
opcodes but `<=`/`>=` do not — lower the latter to the strict comparison plus `Not`
(`a <= b ≡ !(a > b)`) rather than falling through to a wrong-typed opcode that faults
the VM. When a value's static type is widened across branches or arms (the int/float
ternary, the switch expression) coerce each branch at runtime — a unified static type
alone leaves a narrower value on the stack and the VM faults on a kind mismatch. The
equality opcodes `Equal`/`NotEqual` are the language `==`/`!=`: they use `GrobValue`'s
IEEE 754 `operator==` (`NaN != NaN`), never the collection-friendly `Equals` where
`NaN.Equals(NaN)` is true.

## Diagnostics infrastructure

The `Diagnostics` namespace exists from Sprint 1: a `Diagnostic` record (severity,
message, source location, optional suggestion) and a `DiagnosticBag` that accumulates
without stopping. Every compile-time error maps to a registered code in
`docs/design/grob-error-codes.md` (`Exxxx`, immutable once shipped per ADR-0017).
When you add a diagnostic, use an existing code or register a new one in the right
category range. Do not emit a bare string with no code. Raise diagnostics via the
`ErrorCatalog` descriptors (D-308), not literal code strings.

## Doc comments

- Public types and members get XML doc comments. `<summary>` is mandatory; `<param>`,
  `<returns>`, `<exception>` where they add information beyond the signature.
- Internal types: doc comment when the purpose is not obvious from the name.
- Private members: comment only when the implementation has non-obvious reasoning.

## Design for testability

Production code is written expecting to be tested. The minimum line-coverage bar is
90% per project — see `tests/CLAUDE.md` for the bar and the rules around exclusions.
The conventions below are what makes that bar achievable without distortion.

- **No ambient dependencies in business logic.** No direct use of `DateTime.Now`,
  `DateTime.UtcNow`, `Environment.*`, `File.*`, `Directory.*`, `Console.*`, or
  `Process.Start` inside types that contain logic. Inject an abstraction (`IClock`,
  `IFileSystem`, `IConsole`, `IProcessRunner`) through the constructor. The single
  concrete implementation lives at the composition root — `Grob.Cli` — and is wired
  up there.
- **No static mutable state.** `static readonly` for genuinely constant lookup tables
  and singletons is fine. `static` fields that change at runtime are not — they make
  tests order-dependent and parallel-hostile.
- **Constructor injection over service location.** No `ServiceProvider.GetService<T>()`
  inside business code. Dependencies are declared on the constructor; the composition
  root supplies them.
- **`InternalsVisibleTo` with discipline.** Tests drive through the public surface
  where they can. Where the public surface would otherwise grow purely to enable
  testing, make the type `internal` and use `InternalsVisibleTo`. The bar: is this
  type part of the project's contract (→ public) or only its implementation
  (→ internal + `InternalsVisibleTo`)?
- **No `sealed` on internal types unless there is a reason.** They may need
  substituting in tests.
- **Avoid `static` methods on types with non-trivial branching.** A pure method
  (input → output, no I/O, no time, no randomness) may be `static`. One that touches
  anything ambient should be an instance method on a type that takes the ambient
  dependency through its constructor.

## `[ExcludeFromCodeCoverage]`

When code is genuinely unreachable by tests — defensive guards against states the
compiler should already have ruled out, platform-conditional code, generated code —
exclude it with `[ExcludeFromCodeCoverage]` and supply a substantive `Justification`:

```csharp
[ExcludeFromCodeCoverage(Justification =
    "Defensive guard against null after compiler enforces non-nullable. " +
    "Unreachable in normal execution; retained as belt-and-braces.")]
private static void ThrowUnreachable() => throw new InvalidOperationException();
```

The justification must explain **why** the code cannot or should not be tested, not
what the code does. "Helper method" is not a justification. "Defensive branch
unreachable while the type checker enforces X" is. Code review rejects exclusions
without a substantive justification. Periodically review exclusions — one valid when
added may become invalid as surrounding code changes.

## Tests

Five xUnit projects mirror `src/`. Every change ships tests. **`Grob.Compiler.Tests`
is the highest-priority surface** — given source, assert emitted bytecode; that is
where bugs live (confirmed by the SharpBASIC retrospective). The full testing
discipline — coverage taxonomy, diagnostic-assertion contract, property-based
testing, gold-master error examples — lives in `tests/CLAUDE.md`.

## Performance

Correctness gates on `dotnet test`; performance gates separately via
`bench/Grob.Benchmarks` (D-302). Do not micro-optimise speculatively. The hot path is
I/O — REST, JSON, process spawning, file reads — not tight numeric loops. In the
genuinely tight paths (lexer scanner, VM dispatch) avoid LINQ, avoid `string.Format`,
prefer `Span<T>` and `ReadOnlySpan<char>` over substring, and use `StringBuilder` for
any assembly involving more than three concatenations or running in a loop. If you
believe a change affects performance, the benchmark harness is how you prove it, not
assertion. `[MemoryDiagnoser]` is on every benchmark; a 5% end-to-end regression
against the committed baseline is the gate.

## Static analysis (local Sonar gate)

`SonarAnalyzer.CSharp` runs as part of `dotnet build` for every non-test project
(wired in `Directory.Build.props`). It is the **same** csharpsquid analyzer
SonarCloud runs in CI, so the recurring new-code rules fail the build **locally,
before commit** — instead of surfacing one review round later. A `dotnet build`
failure of the form `error S3776: …` is an analyzer finding, not a compiler error;
`TreatWarningsAsErrors` escalates it.

- **The gate is scoped** (`.editorconfig`): all analyzer diagnostics default to
  `none`, and only the deterministic, false-positive-free repeat offenders are
  opted back in — **S3776** (cognitive complexity > 15), **S2219** (`is T` that is
  really a null check), **S2325/CA1822** (member can be static). Widen this list
  deliberately, never by accident.
- **S125 (commented-out code) is deliberately not gated.** It false-positives on the
  explanatory comments a compiler necessarily writes (those mentioning `if (x != nil)`,
  `T { }`, `(Unknown)`). Watch it by eye instead: keep comments from parsing as code.
- **Fix, don't suppress.** In compile-time code (type checker, compiler) an S3776 hit
  is a signal to extract guard-clause helpers — not to suppress. The only suppressions
  are the genuine hot paths (VM dispatch loop, type-cycle walker), carved out per file
  in `.editorconfig`. **That carve-out list must stay in lockstep with
  `sonar.issue.ignore.multicriteria` in `.github/workflows/sonarcloud.yml`** — change
  one, change the other.
- **A review-fix commit is itself scanned new code.** When fixing review findings,
  read every line you add as if the analyzers will scan it — they will. Watch for
  cascades: marking a method `static` can make its caller static-eligible too; fix the
  whole chain in one commit, not the leaf. Enumerate the actual Sonar issues with the
  unauthenticated API (this is a public project) rather than guessing:
  `curl -fsS "https://sonarcloud.io/api/issues/search?componentKeys=grob-lang_grob&pullRequest=<N>&resolved=false&ps=500"`.

## When to consult Microsoft Learn

For .NET 10 BCL APIs (`System.Text.Json`, `Span<T>` and `Memory<T>`, `System.IO`,
BenchmarkDotNet attributes, OmniSharp LSP types), prefer the Microsoft Learn MCP
server over training recall — it is the authoritative, current source for the
Microsoft-flavoured stack Grob's host code depends on. Ground API usage there rather
than guessing signatures.
