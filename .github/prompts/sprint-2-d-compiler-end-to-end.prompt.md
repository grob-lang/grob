---
name: "Sprint 2 · Increment D — Compiler, End-to-End and Benchmark Skeleton"
description: "Build the second AST visitor pass in Grob.Compiler — emit typed opcodes from the type checker's ResolvedType annotations, manage the constant pool, track per-instruction lines. Close Sprint 2 with print(2 + 3 * 4) → 14 end-to-end and the BenchmarkDotNet skeleton (D-302)."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 2 · Increment D — Compiler, End-to-End and Benchmark Skeleton

You are closing Sprint 2 of the Grob compiler. Increments A–C delivered the
back-end primitives and disassembler (A), the VM dispatch loop proven against
hand-constructed chunks (B), and the type checker that annotates the AST (C).
This increment builds the **compiler** — the second AST visitor pass — which
emits bytecode from the type-checked tree, and then **closes the loop** end
to end: source text in, `14` out. It also stands up the **benchmark
skeleton** (D-302), Sprint 2's other close-of-sprint deliverable.

This is the increment where the two halves built separately meet. The
compiler emits a `Chunk`; the Increment A disassembler makes that chunk
readable by eye; the Increment B VM executes it. Because B was proven against
hand-built chunks before this compiler existed, any end-to-end discrepancy
now has a single likely suspect — the compiler's emission — and the
disassembler localises it immediately. That is the payoff of the
B-before-D ordering.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 2 scope — the compiler
   bullet list, the integration-test bullet, the `bench/Grob.Benchmarks`
   bullet, and the **acceptance** block: `print(2 + 3 * 4)` → `14`), §3.3
   (the typed-opcode rationale), and §3.5 (test infrastructure — which test
   project each kind of test lives in).
2. `docs/design/grob-vm-architecture.md` — the `Chunk` and constant-pool
   sections (the compiler writes into the same `Chunk` write surface
   Increment A established).
3. `docs/design/grob-benchmarking-strategy.md` — §7 and the skeleton /
   baseline requirements, for the benchmark deliverable.
4. `docs/design/grob-solution-architecture.md` — `Grob.Compiler`
   responsibilities, the visitor pattern, partial-class compiler structure,
   and the DAG (`Grob.Compiler` produces `Chunk` and references `Grob.Core`;
   it never references `Grob.Vm`).
5. Decisions **D-302** (benchmarking), **D-303** (GrobValue), **D-307**
   (lowercase built-in type names) in `docs/design/grob-decisions-log.md`.

> **Verify before relying on cited decisions.** This prompt cites D-302,
> D-303 and D-307. Grep the decisions log to confirm each says what this
> prompt assumes (D-302 = benchmarking strategy / skeleton; D-303 = the
> `GrobValue` shape the chunk and VM use; D-307 = lowercase built-in scalar
> type names) before building on them. If any has been superseded or
> renumbered, surface it rather than proceeding.
>
> **Typed-opcode selection — inline reference (authoritative source is §3.3;
> reproduced here so emission does not depend on a fetch landing well).** The
> compiler does not re-derive types — it reads the `ResolvedType` Increment C
> set and maps it to the specialised opcode:
>
> - resolved `int` arithmetic → `AddInt` (and the rest of the int arithmetic
>   family)
> - resolved `float` arithmetic → `AddFloat` (and the float family); a
>   mixed `int op float` resolves to `float` at type-check time, so the
>   promotion is already baked into the annotation — emit the float opcode,
>   do not emit a runtime promotion check
> - resolved `string + string` → `Concat`
>
> No runtime type checks are emitted: the type checker already proved
> correctness. Emitting a typed opcode off an annotation is the whole point
> of the design — the compiler is where type information *becomes* opcode
> choice.
>
> **Sequencing note.** This is Increment D — the close of the agreed Sprint 2
> breakdown: A → B → C → **D (compiler + end-to-end + benchmark skeleton)**.
> After this increment, Sprint 2's §4 acceptance criteria are met in full and
> the sprint is closeable. Sprint 3 (declarations, REPL, `grob run`) is the
> next sprint, not part of this increment.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name
   `feat/compiler-and-end-to-end`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 (Increments A–E):** complete front end — lexer, AST,
  error-recovering parser, recovery suite. All tests green.
- **Sprint 2 Increment A:** `OpCode`, `GrobValue` (D-303), `Chunk` with its
  write surface (`writeByte`, `addConstant`, line tracking) in `Grob.Core`;
  `Disassembler` in `Grob.Vm`.
- **Sprint 2 Increment B:** VM dispatch loop in `Grob.Vm` — `ValueStack`,
  arithmetic execution, `Constant`/`Print`/`Return`, checked arithmetic,
  `#if DEBUG` `TraceInstruction`. Executes hand-constructed chunks.
- **Sprint 2 Increment C:** type checker in `Grob.Compiler` — two-pass
  (D-166), `:=` inference, arithmetic/comparison type rules, `ResolvedType`
  and `Declaration` set on every identifier node (§3.1.1), collect-all-errors,
  `Error`-type cascade suppression. Annotates the AST; emits nothing.

## Deliverable for this increment

The compiler as the second AST visitor pass in `Grob.Compiler`, the
end-to-end integration test, and the benchmark skeleton.

1. **The compiler — second visitor pass.** Walks the type-checked AST and
   emits bytecode into a `Chunk` via the Increment A write surface. Partial
   classes by concern (expressions here; statements/declarations/control-flow
   in their own files as later sprints add them), per the solution
   architecture.
2. **Typed-opcode selection from `ResolvedType` (§3.3).** Read the
   `ResolvedType` annotations Increment C set and emit the specialised
   opcode — `AddInt` vs `AddFloat` vs `Concat`, and the rest of the typed
   arithmetic family. No runtime type checks are emitted; the type checker
   already proved correctness. This is the whole point of the typed-opcode
   design — the compiler is where the type information becomes opcode choice.
3. **Constant-pool management.** Intern constants into the chunk's pool via
   `addConstant`; emit `Constant` with the pool index. De-duplicate if the
   write surface supports it; otherwise note it for a later pass — do not
   over-engineer.
4. **Line-number tracking per instruction.** Every emitted instruction
   records its source line into the chunk's parallel line array (the same
   array the disassembler prints and the VM reads for runtime-error lines).
   `SourceLocation` is on every AST node from Sprint 1 — thread the line
   through.
5. **`print()` and `exit()` as built-ins.** Resolved at type-check time
   against registered natives (they are not keywords — D-270). `print()`
   compiles to the `Print` opcode path Increment B implemented. Implement
   `exit()` to the degree Sprint 2 needs it; defer anything beyond the §4
   scope.
6. **End-to-end integration test.** `print(2 + 3 * 4)` through the full
   pipeline — lex, parse, type-check, compile, execute — asserting stdout is
   `14`. This lives in `Grob.Integration.Tests` per §3.5. A type-error case
   (`"hello" + 42`) asserts a clear compile-time diagnostic with a line
   number and **no execution**.
7. **Benchmark skeleton (D-302).** `bench/Grob.Benchmarks/` as a
   BenchmarkDotNet console project, sibling to `src/`, `tests/`, `plugins/`.
   An initial compile-time benchmark category with at least one benchmark
   (lexer throughput on a representative source per §4). A `Fixtures/`
   directory. The first baseline JSON committed to
   `bench/Grob.Benchmarks/baseline/compile.json`. Runs end-to-end via
   `dotnet run -c Release --project bench/Grob.Benchmarks`. No `grob bench`
   CLI surface — benchmarks are infrastructure, not a shipped feature.

## Out of scope

No declarations / globals table / `DefineGlobal`/`GetGlobal` (Sprint 3, with
the REPL and `grob run`). No control flow (Sprint 4). No functions, call
frames or closures (Sprint 5). No user types (Sprint 6). No constant-pool
de-duplication heroics — basic interning is enough for Sprint 2. The
stability test and its calibration are **Sprint 8**, not now — this increment
ships only the skeleton plus the first compile-time baseline. Do not touch
`Grob.Vm` (it is complete for Sprint 2's surface from Increment B) except
through its public surface in the integration test's composition.

## Tests

Per §3.5, tests land in the project that matches their kind:

- **Compiler tests (`Grob.Compiler.Tests`)** — given source, assert the
  emitted bytecode. This is where the bugs live (per the SharpBASIC
  retrospective), so be thorough: `2 + 3 * 4` emits the correct typed-opcode
  sequence with correct precedence; `2.0 + 3` emits `AddFloat` with the
  promotion; `"a" + "b"` emits `Concat`; constants land in the pool at the
  expected indices; the line array matches the source lines.
- **Integration tests (`Grob.Integration.Tests`)** — `print(2 + 3 * 4)`
  produces stdout `14` end-to-end; `"hello" + 42` produces a compile-time
  diagnostic with a line number and does not execute.
- **Benchmark smoke** — the seed compile-time benchmark runs end-to-end via
  the documented `dotnet run -c Release` invocation and writes the baseline
  JSON.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The compiler lives in `Grob.Compiler`, produces a `Chunk`, and the DAG
  holds (`Grob.Compiler` references `Grob.Core`, not `Grob.Vm`).
- `print(2 + 3 * 4)` compiles, runs and prints `14`.
- Type errors (e.g. `"hello" + 42`) produce clear compile-time diagnostics
  with line numbers and do not execute.
- The emitted bytecode disassembles (Increment A) to a correct, readable
  listing — verify the compiler's output through the disassembler, not only
  through the VM's answer.
- `bench/Grob.Benchmarks` builds, the seed benchmark runs via
  `dotnet run -c Release --project bench/Grob.Benchmarks`, and the first
  baseline JSON is committed to `baseline/compile.json`.
- Sprint 2's §4 acceptance criteria are met in full.

## Model

Sonnet 4.6 (High effort) throughout — the emission arms, the benchmark
scaffolding, and the typed-opcode selection. The mixed-type promotion chains
are *not* an Opus case despite looking like one: they are settled by the
`ResolvedType` annotations Increment C already proved, so the compiler is
reading a resolved type and choosing an opcode — transcription, not
judgement. The one defensible Opus moment is the visitor / partial-class
compiler skeleton, because every subsequent sprint extends it and the shape
is worth getting right once. Even there, paste the partial-class compiler
section from `grob-solution-architecture.md` into the prompt first; with the
intended structure in front of it, Sonnet should lay it down correctly. The
trigger for Opus is "load-bearing structural decision later sprints build
on," never "this part is hard."

## Hand-off

Summarise: the compiler's visitor / partial-class structure, the
`ResolvedType`-to-typed-opcode selection logic, the constant-pool and
line-tracking approach, the `print()`/`exit()` built-in wiring, the
end-to-end result (`14`) and how it was verified through both the
disassembler and the VM, the benchmark skeleton layout and the first baseline
numbers, and the test files added across `Grob.Compiler.Tests`,
`Grob.Integration.Tests` and `bench/`. Confirm Sprint 2's §4 acceptance is
met in full so the sprint is closeable. Note for the next chat: Sprint 3
delivers declarations (`:=`, `=`, `const`, `readonly`, the globals table and
`DefineGlobal`/`GetGlobal`), the REPL (`G>`) and `grob run <file>` —
re-confirm the Sprint 3 increment breakdown with the maintainer before
assuming it.
