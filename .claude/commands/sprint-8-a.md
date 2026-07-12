---
description: "Sprint 8 · Increment A — plugin & native-module infrastructure + module-namespace resolution. Stand up the `IGrobPlugin` contract in `Grob.Runtime`, the new `Grob.Stdlib` project + `Grob.Stdlib.Tests`, CLI auto-registration; the compile-time module-namespace model (D-339 — namespace name-category, member access folded to a qualified native, emitted as function-constant + Call, no new opcode); the native-throw seam (a native raising a catchable GrobError through the Sprint-7 handler table); the capability-injection seam (D-340, four interfaces + OS-backed defaults). Formalise print/exit as stdlib. Prove end-to-end with math.pi + math.sqrt. Opus carve-out on the member-access dispatch. B–F reuse."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 8 · Increment A — plugin & native-module infrastructure + module-namespace resolution

You are opening Sprint 8 of the Grob compiler — the core standard library. Sprint 7
made the language fail and recover. This increment stands up the **machine every
module runs on**: the `IGrobPlugin` contract, the `Grob.Stdlib` project, CLI
auto-registration, the compile-time **module-namespace resolution model** (how
`math.sqrt` gets from source to a native call), the **native-throw seam** (how a
native raises a catchable exception), and the **capability-injection seam** (how a
native reaches the host without `Grob.Stdlib` touching the OS). It is proved
end-to-end with a two-function `math` vertical — `math.pi` (a namespace constant) and
`math.sqrt` (a pure native that also throws on a domain error). It is the structural
foundation every later Sprint 8 increment reuses; get the dispatch, throw and
injection discipline right once, here.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 8 — Core Standard Library
   (Part 1)** scope (the `IGrobPlugin`/auto-registration bullets, `print`/`exit`, the
   `math` signatures), §3.1.1 (the day-one LSP properties), §3.3 (the closed `OpCode`
   enum — confirm `Import` exists and that **no core module needs it**; A emits only
   `Call` over a function constant), §3.5 (test routing), and the solution-architecture
   § (the `Grob.Stdlib` project, the DAG, the `Grob.Runtime` registration surface).
2. `docs/design/grob-stdlib-reference.md` — the **Core Modules — Auto-Available**
   table and the **`math`** section (constants, `sqrt`, the `ArithmeticError` domain
   throw sites). Read how modules are described as auto-available with no `import`.
3. `docs/design/grob-vm-architecture.md` — **Plugins and Native Functions**
   (`NativeFunction`, `RegisterNative`, the `IGrobPlugin` interface, `LoadPlugin`,
   type safety at the plugin boundary) and the `GrobValue` representation (D-303 —
   nine variants, `Struct` shared discriminator).
4. Decisions: confirm **D-282** and **D-320** (`formatAs`/`select` reserved
   identifiers, the "compiler-namespace" phrasing, E1103), **D-319** (the capability
   interfaces in `Grob.Runtime`), **D-303** (the settled `GrobValue` tagged union),
   **D-308** (`ErrorCatalog`), and the Sprint-7 exception decisions **D-274**/**D-334**
   (the hierarchy and the unwinding path a throwing native re-uses). Grep the log for
   the **next-free D-number** (this prompt provisionally names the module-resolution
   model **D-339** and the capability-seam scope **D-340**; confirm against the live
   tail — Sprint 7's provisional D-332 landed as D-334).

> **Verify before relying on cited decisions and sections.** Grep
> `grob-stdlib-reference.md` and `grob-vm-architecture.md` to confirm the module and
> plugin sections read as this prompt assumes, and the decisions log for D-282, D-320,
> D-319 and D-303. If a section has moved or a decision been superseded, surface it
> rather than proceeding.
>
> **Grammar-first gate (D-331).** Before any type-checker or emission work, confirm
> `math.sqrt(9.0)` and `math.pi` parse to member-access nodes against the merged tree
> (D-320 made member access on a reserved/namespace head grammatical when it landed
> `.select(...)`/`.formatAs` in Sprint 5). If the member-access production for a
> bare-identifier head like `math` is missing or malformed, that is a finding — extend
> it through the `extending-the-grammar` skill, surfaced not swept, before continuing.
>
> **Module-namespace resolution — inline reference (authoritative source is the
> Sprint 8 kickoff D-339 section, D-282, D-320 and the solution-architecture §;
> reproduced here so the implementation does not depend on a fetch landing well).**
>
> - **Core modules are compile-time namespaces, not runtime values.** `math` (and, in
>   later increments, `path`, `env`, `log`, `guid`, `formatAs`) is registered as a
>   distinct **namespace name-category** in the global scope — neither a value binding
>   nor a type binding — resolvable by the checker before any script is checked. A
>   namespace name in value position (`x := math`, `print(math)`) is a compile error at
>   the new namespace-as-value code.
> - **Member access on a namespace folds to a qualified native or a namespace constant.**
>   At a member-access node `x.y`, the checker's dispatch precedence is fixed and total:
>   **namespace** receiver → `y` is a qualified native (`"math.sqrt"`) or a namespace
>   constant (`math.pi`), resolved from the registry; **value of a built-in type** → an
>   instance method (D-070/D-071, existing); **struct value** → a field access /
>   `GetProperty` (existing). (The reserved-`formatAs` arm is Increment E.) An unknown
>   member on a namespace (`math.nope`) is a compile error at the new
>   unknown-namespace-member code — **not** the value-position undefined-member code
>   (E1002), because a namespace is not a value.
> - **No runtime module object, no new opcode.** A resolved qualified native is emitted
>   as a function constant (the `NativeFunction` registered under `"math.sqrt"`) followed
>   by the **existing** `Call`; a namespace constant (`math.pi`) is emitted as a `Constant`.
>   There is no module value on the stack, no `GetProperty` against a namespace, no
>   `Import` for a core module. If you reach to add an opcode or a `GetProperty`-against-
>   namespace arm, stop — the enum is closed (§3.3, ADR-0013).
> - **Natives may throw catchable `GrobError`s — the native-throw seam.** A native
>   implementation that raises a `GrobError` (here, `math.sqrt(x < 0)` →
>   `ArithmeticError`) enters the **same** unwinding path Sprint 7 built (D-334) — the
>   handler table, the `finallyOffset`, the top-level diagnostic — through one
>   mechanism. Do **not** build a bespoke native-error path that bypasses the handler
>   table. `math.sqrt(-1.0)` unhandled produces the same quality top-level diagnostic
>   (`file:line`, D-322) and exit 1 as a user `throw`; caught by a `try`/`catch` it is
>   an ordinary catchable `ArithmeticError`.
> - **The capability-injection seam (D-340, refining D-319).** `Grob.Stdlib` never
>   touches the OS. Host contact goes through interfaces in `Grob.Runtime` — this
>   increment lands the injection **mechanism** and the interfaces A itself needs:
>   `IStandardStreams` (for the `print`/`exit` formalisation) plus the seam by which
>   `Grob.Cli` constructs OS-backed default implementations and passes them to the VM
>   registration surface. `IEnvironment`, `IClock` and `IRandomSource` are declared
>   here as part of the seam (their consumers are B/C/D); `IFileSystem` and
>   `IProcessRunner` are **not** landed — they arrive in Sprint 9 with `fs`/`process`.
>   The DAG holds: interfaces in `Grob.Runtime`, consumed by `Grob.Vm`/`Grob.Stdlib`,
>   injected by `Grob.Cli`; no new cross-edge; `Grob.Compiler` ↔ `Grob.Vm` still do not
>   reference each other.
> - **`print`/`exit` are formalised, not rebuilt.** They are built-in from Sprint 2.
>   This increment registers them through the `IGrobPlugin` surface so they sit in the
>   same registration path as every other native, routing `print`'s output through
>   `IStandardStreams` — the observable behaviour is unchanged (D-336 `ValueDisplay`
>   still renders the argument; `exit` still raises `ExitSignal`, D-110/D-274). Do not
>   change what `print` or `exit` do.
> - **Prove it with two functions, not the whole of `math`.** A registers `math.pi`
>   (constant) and `math.sqrt` (pure native, throwing on domain error) as the minimal
>   vertical that exercises the namespace name-category, the qualified-native emission,
>   the `Constant` path for a namespace constant and the native-throw seam. The rest of
>   `math`, and `path`/`strings`, are **Increment B**. Do not pull them forward.
>
> **Sequencing note.** This is Increment A of the agreed Sprint 8 breakdown:
> **A (infrastructure + module resolution)** → B (`math`/`path`/`strings`) → C
> (`env`/`log`/`input`) → D (`guid`) → E (`formatAs`) → F (close). Do not pull module
> breadth, the `guid` primitive type, `formatAs` or the calibration forward — those are
> their own increments. This increment is the infrastructure, the resolution model, the
> throw and injection seams, the `print`/`exit` formalisation and the `math.pi`/
> `math.sqrt` proving vertical only.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the grammar-first
   verification, the `IGrobPlugin` contract + `Grob.Stdlib`/`Grob.Stdlib.Tests`
   projects + `Grob.slnx` membership, the module-namespace name-category and the
   member-access dispatch (D-339, incl. the two new error arms), the qualified-native
   emission, the native-throw seam, the capability-injection seam (D-340), the
   `print`/`exit` formalisation, the `math.pi`/`math.sqrt` vertical and the tests — and
   wait for Chris's approval before editing. The plan is the gate. **Flag the
   member-access dispatch precedence as the Opus sub-problem** and propose the
   `.claude/agents/` subagent config for it.
2. On approval, run `/start-branch` and propose `feat/stdlib-infrastructure`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first. Follow the
   `tdd-cycle` skill.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–2:** the front end (lexer, grammar-complete AST, the error-recovering
  parser) and the back end (closed `OpCode` enum, `GrobValue` (D-303), `Chunk`,
  disassembler, VM dispatch loop, the multi-pass type checker, compiler,
  BenchmarkDotNet skeleton). C# 14 / .NET 10 (D-310), `ErrorCatalog` (D-308), the
  consistency gate (D-316). `print` and `exit` are built-in.
- **Sprint 3–4:** variables, scope chain, `const`/`readonly`, nullable runtime, string
  interpolation, the REPL, control flow, arithmetic error sites as VM halts.
- **Sprint 5:** functions and closures — call frames, named/default arguments,
  lambdas, **natives (`NativeFunction`, `RegisterNative`)**, closures with upvalue
  capture, the three-pass checker (D-323), location-based upvalue closing (D-325),
  function types (D-326), array type-refs (D-327). The `.select(...)`/`.formatAs`
  member-access grammar and E1103 (D-320).
- **Sprint 6:** user-defined `type`s and the registry, named construction, field
  access/assignment, anonymous structs.
- **Sprint 7:** the ten-leaf `GrobError` hierarchy in `Grob.Runtime`, `throw`,
  `try`/`catch`/`finally`, the handler table and `finallyOffset` (D-334), runtime-throw
  catchability (D), uncatchable `exit()`. The unwinding path a throwing native reuses.
- **Pre-Sprint-8 interlude:** `ValueDisplay` (D-336), the `Grob.slnx` membership check
  (D-335), the smoke-script family documented (D-337). Error-code base **116**. All
  green.

## Deliverable for this increment

The plugin/native-module infrastructure, the module-namespace resolution model, the
throw and injection seams, `print`/`exit` formalised, and a two-function `math`
vertical. **No module breadth, no `guid`, no `formatAs`, no calibration.**

1. **`IGrobPlugin` + `Grob.Stdlib`.** The `IGrobPlugin` contract in `Grob.Runtime`
   (`Register(vm)`); the `Grob.Stdlib` project referencing `Grob.Core` + `Grob.Runtime`
   only; `Grob.Cli` discovering and auto-registering the stdlib plugins at VM startup;
   `Grob.Stdlib.Tests` created and added to `Grob.slnx` (D-335 check green).
2. **Module-namespace resolution (D-339).** The namespace name-category; the
   member-access dispatch precedence (namespace → qualified native / namespace
   constant; value → instance method / field, existing); the two new error arms
   (namespace-as-value; unknown-namespace-member). Set `ResolvedType` and `Declaration`
   on every identifier/member node introduced; the §3.1.1 invariant holds.
3. **Qualified-native emission.** `math.sqrt(9.0)` emits the arg then the
   `"math.sqrt"` native as a function constant then `Call`; `math.pi` emits a
   `Constant`. Verified through the disassembler, not only the VM's answer. No new
   opcode.
4. **The native-throw seam.** `math.sqrt(-1.0)` raises a `GrobError` that unwinds the
   Sprint-7 handler table — catchable in `try`/`catch`, and unhandled it produces the
   quality top-level diagnostic (`file:line`, D-322) with exit 1. One mechanism, no
   bespoke native-error path.
5. **The capability-injection seam (D-340).** The injection mechanism + `IStandardStreams`
   (consumed now by `print`), with `IEnvironment`/`IClock`/`IRandomSource` declared for
   B/C/D; OS-backed defaults constructed by `Grob.Cli`. `IFileSystem`/`IProcessRunner`
   not landed. DAG clean.
6. **`print`/`exit` formalised.** Registered through the plugin surface, `print`
   routing through `IStandardStreams`; behaviour unchanged (D-336 rendering, D-110
   `ExitSignal`).
7. **New error codes, registered in lockstep.** The D-339 error arm(s) registered in
   `grob-error-codes.md` and the catalog at the next-free number(s), three-location
   lockstep, count reconciled, D-316 gate green. No `"Exxxx"` literal at a call site.
8. **The two decisions logged.** D-339 (module-namespace resolution) and D-340
   (capability-seam scope, refining D-319) recorded at their real next-free numbers,
   three-location lockstep, D-339 extending D-282/D-320.

No new opcodes, no parser/AST edits beyond a surfaced-and-approved grammar finding,
no `guid`, no `formatAs`, no module breadth.

## Out of scope

The rest of `math`, and `path`/`strings.join` (Increment B). `env`/`log`/`input`
(Increment C). The `guid` primitive type and module (Increment D). `formatAs`
(Increment E). The stability calibration, the benchmark baseline and `stdlib.grob`
(Increment F). `IFileSystem`/`IProcessRunner` (Sprint 9). Do not edit the `OpCode`
enum. If you believe you need an opcode, stop and surface — A emits only `Call` and
`Constant`, which already exist.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - `math` resolves as a namespace; `math.pi` resolves to a float namespace constant;
    `math.sqrt` resolves to a native with signature `(float): float`.
  - `x := math` and `print(math)` are the namespace-as-value code with a source
    location; `math.nope()` is the unknown-namespace-member code — **not** E1002.
  - The dispatch does not regress existing arms: a struct field access and a built-in
    instance method (`"x".upper()` or equivalent) still resolve as before.
  - §3.1.1 invariant: every identifier/member node introduced carries a non-null
    `ResolvedType` and a non-null `Declaration` (sentinels by reference, `Assert.Same`).
- **Compiler / disassembler tests (`Grob.Compiler.Tests`):**
  - `math.sqrt(9.0)` disassembles to the arg `Constant`, the `"math.sqrt"` native
    `Constant`, then `Call` — verified through the disassembler. `math.pi`
    disassembles to a single `Constant`. No new opcode present.
- **Stdlib tests (`Grob.Stdlib.Tests`):**
  - `math.sqrt(9.0)` returns `3.0`; `math.pi` is `3.141592653589793`.
  - The `IGrobPlugin` for the `math` vertical registers exactly `math.pi` and
    `math.sqrt` under their qualified names.
- **VM tests (`Grob.Vm.Tests`):**
  - `math.sqrt(-1.0)` unhandled unwinds to the top level and produces the quality
    diagnostic (file, line, error type, message) and exit 1.
  - `math.sqrt(-1.0)` inside a `try`/`catch (e: ArithmeticError)` is caught and the
    script resumes — the native-throw seam runs through the handler table.
  - A `finally` around `math.sqrt(-1.0)` runs exactly once (the D-334 partition holds
    for a native throw as for a user throw).
  - Layer-invariant `[Theory]` rows: pathological but parseable module member accesses
    type-check to a result or a diagnostic, never throw a host exception.
- **Integration / spec-consistency:** the consistency gate (D-316) green;
  catalog↔registry agreement holds; the D-335 membership check passes with
  `Grob.Stdlib.Tests` present; the error-code count reconciled.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root; `Grob.Stdlib` and
  `Grob.Stdlib.Tests` exist and are in `Grob.slnx`.
- `math` resolves as a namespace; `math.pi`/`math.sqrt` resolve and run; namespace-as-
  value and unknown-namespace-member are compile errors at their codes.
- `math.sqrt` emits a qualified-native `Call`; `math.pi` a `Constant`; no new opcode.
- `math.sqrt(-1.0)` throws a catchable `ArithmeticError` through the Sprint-7 handler
  table and, unhandled, produces the quality top-level diagnostic and exit 1.
- The capability-injection seam is in place with `IStandardStreams` consumed by
  `print`; `print`/`exit` behaviour is unchanged.
- The §3.1.1 invariant holds; the DAG holds; no new opcode; no parser/AST edits beyond
  a surfaced grammar finding.
- D-339 and D-340 logged in lockstep; the new error code(s) registered in lockstep and
  the count reconciled; D-316 green.
- Coverage at or above 90% line+branch on the affected projects, including
  `Grob.Stdlib`.

## Model

Sonnet 4.6 (High) drives the increment. The **module-namespace member-access dispatch
precedence** (D-339 — namespace vs value vs type vs reserved at a `x.y` node) is the
Opus carve-out: it is the one genuinely new structural call, the analogue of Sprint 5
D's open/closed-upvalue sub-problem and Sprint 7 C's finally chain. Escalate it to an
Opus 4.8 subagent (the `grob-closure-specialist` mechanism), config added under
`.claude/agents/`. The plugin registration, the qualified-native emission, the
capability seam and the `print`/`exit` formalisation are Sonnet — registration and
straight-line emission over settled machinery.

## Hand-off

Summarise: how `IGrobPlugin`/`Grob.Stdlib` are wired and auto-registered; the
namespace name-category and the member-access dispatch precedence, with the two new
error codes; the qualified-native emission shape and the namespace-constant path; the
native-throw seam and how it reuses the Sprint-7 handler table; the capability-injection
seam and which interfaces landed; the `print`/`exit` formalisation; D-339 and D-340
and their lockstep entries; the test files added. Note for the next chat: Increment B
is `math` (complete), `path` and `strings.join` — the pure/near-pure breadth over the
A foundation, `math.random*` on the A-declared `IRandomSource`, the `math` domain-error
throws through the native-throw seam.
