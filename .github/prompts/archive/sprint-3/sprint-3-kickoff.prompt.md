---
name: "Sprint 3 Kickoff"
description: "Open Sprint 3 of the Grob compiler — variables, scope, const/readonly, nullable runtime, string interpolation and the REPL. Records the agreed A–F increment breakdown, the load-bearing ordering calls and the close-gate. Start with Increment A."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 Kickoff

Begin Sprint 3 — variables, scope and the REPL. Sprint 2 delivered the back
end (primitives, disassembler, VM dispatch loop, two-pass type checker,
compiler, benchmark skeleton — all green, QA and QA-fix landed). Sprint 3 turns
the expression-only pipeline into one that declares, scopes, reassigns and runs
real script files. The full scope and acceptance are in
`docs/design/grob-v1-requirements.md` §4 (Sprint 3 — Variables, Scope and
REPL). Read it word for word; it is the build contract.

> **This prompt is a record, not a gate.** Like the Sprint 2 kickoff, it does
> no setup work and unblocks nothing. The solution scaffold exists from
> Sprint 1, the REPL needs no new project (see below) and the increment
> breakdown was agreed in planning before this prompt was written. There is
> nothing to confirm before starting — **go straight to Increment A.**

## No new project — the REPL lives in `Grob.Cli`

The brief floated a possible `src/Grob.Repl/` project. The locked architecture
does not have one. `grob-solution-architecture.md` places both new commands
inside `Grob.Cli`: `RunCommand` (`grob run`) and `ReplCommand` (`grob repl`),
with `Grob.Cli` as the single composition root that references every other
`src/` assembly and that nothing references back. A separate REPL project would
be an architecture change requiring its own decision-log entry, against a
deliberately single-composition-point design. Sprint 3 adds no `src/` project.
That is why this kickoff is thin: there is no scaffold to own.

## The agreed increment breakdown

§4 describes Sprint 3 as a single section with one acceptance block. It is more
than Sprint 2's four increments — it carries mutable declarations, the scope
chain, `const`/`readonly`, the nullable runtime, string-interpolation
compilation and the REPL. The work is sliced into six, on the dependency seams:

- **A — Variables, assignment and scope.** `:=` declaration, `=` reassignment
  walking the scope chain, the globals table
  (`DefineGlobal`/`GetGlobal`/`SetGlobal`), locals with stack slots
  (`GetLocal`/`SetLocal`), block scoping with `PopN`, type annotations on
  declarations, `++`/`--` (`IncrementInt`/`DecrementInt`) and compound
  assignment (`+=` etc.) desugared by the compiler. The foundational layer
  every later increment builds on. Branch `feat/variables-and-scope`.
- **B — `grob run`.** A thin CLI wrapper — compile and execute a `.grob` file
  through the existing pipeline. Lands right after A so declaration-bearing
  scripts run end-to-end via the CLI, and the §4 `grob run` acceptance is
  regression-tested as each later increment grows the smoke script. Branch
  `feat/cli-run`.
- **C — `const` and `readonly`.** The shared `SingleAssignmentDeclaration` AST
  node with its `Kind` discriminator; the `const` compile-time-constant
  evaluator (§24 allowlist) with reference inlining and no runtime slot; the
  `readonly` runtime-once binding with deep-immutability checking; the
  cross-reference rules. Branch `feat/const-and-readonly`.
- **D — Nullable runtime.** `T?` type-system support, `??` (`NilCoalesce`),
  `?.` optional-chaining short-circuit, `IsNil`. Owns first-use of forward-jump
  backpatching (see the ordering calls). No flow-narrowing — that is Sprint 5.
  Branch `feat/nullable-runtime`.
- **E — String interpolation.** Compile the already-parsed interpolation parts
  to segment pushes and `BuildString`; enforce the nullable-interpolation
  compile error (D-279, E0102). Branch `feat/string-interpolation`.
- **F — REPL and sprint close.** `grob repl` (`G>` prompt, auto-printed
  expression results, multi-line input, history); the end-to-end
  `grob run` smoke script that closes the sprint; the first VM-execution
  benchmark baseline via the `benchmark.yml` workflow (D-309). Branch
  `feat/repl-and-sprint-3-close`.

Each increment has its own prompt (`sprint-3-{a..f}-*.prompt.md`). Run them in
order, each building and testing green before the next, a fresh chat per
increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **`grob run` early (B), not bundled at the end.** This is a focus call, not a
  bisection call. Sprint 2 already delivered `Grob.Integration.Tests` running
  source → stdout through the full pipeline with no CLI, so each Sprint 3
  increment has its end-to-end net regardless. `grob run` is pulled early
  because it is a near-trivial wrapper, the REPL (F) is the genuinely fiddly
  increment, and folding `grob run` into A or F bloats an already-heavy
  increment. Standing it up after A also keeps the §4 `grob run` acceptance
  continuously regression-tested.
- **`const` and `readonly` together (C), not split.** They share the
  `SingleAssignmentDeclaration` node and the reject-reassignment / reject-
  mutation diagnostic surface, and the cross-reference rules (`const` may not
  reference `readonly`; `readonly` may reference `const`) need both kinds
  present to test. Splitting leaves a dead `Kind` arm and an untestable
  cross-reference rule. They are different machines underneath — the
  compile-time-constant evaluator versus the deep-immutability checker — so C
  is the heaviest feature increment.
- **Nullable its own increment (D), before interpolation (E).** Nullable is
  substantial — type rules for `T?`, two opcodes and jump-based `?.`
  short-circuit — and E depends on it for the D-279 rule. D and E are kept
  separate rather than merged: the only coupling is the single D-279 rule,
  which a clean D-before-E ordering handles without welding two distinct
  features into one increment.
- **D owns first-use of forward-jump backpatching.** `?.` must short-circuit
  the chain (§21) and there is no `?.` opcode, so it compiles via `IsNil` +
  `JumpIfTrue`/`Jump` with forward-jump backpatching. The jump opcodes exist
  (the `OpCode` enum was closed in Sprint 2 A), but the backpatching *mechanism*
  is first needed in D — even though §4 frames backpatching as Sprint 4's. No
  contradiction: D builds the `emitJump`/`patchJump` helper, and Sprint 4's
  `if`/`while`/`&&`/`||` reuse it. This is the one genuinely structural piece in
  the nullable increment.

## Planning constraints recorded here (durable context for the increment prompts)

- **`??` is eager, per the spec as written.** The `NilCoalesce` opcode is
  defined as "pop two, push right if left is nil" (§3.3), and the operator
  table does not annotate `??` as short-circuiting (unlike `&&`/`||`). So
  `a ?? b` evaluates both operands. Increment D implements this and inlines the
  rule with no leeway. If short-circuiting `??` is later wanted, that is a
  `D-###` that reshapes D's compilation — not a silent implementer choice.
- **Two `const` error codes are assigned in this sprint.** E0205 (const RHS is
  not a compile-time constant) and E0206 (const references a runtime /
  `readonly` value) have §24 spec text but were unregistered. Increment C adds
  both to `grob-error-codes.md` and the `ErrorCatalog` (D-308) at these exact
  numbers — the next free in the `const`/`readonly` block. No implementer
  invents code numbers.
- **`++`/`--` are `int` only.** §[increment-and-decrement] makes `float++` and
  `float--` compile errors. The `OpCode` enum carries `IncrementFloat`/
  `DecrementFloat` for completeness; Sprint 3 does **not** wire them. Increment
  A wires `IncrementInt`/`DecrementInt` only. An implementer who believes
  `float++` should be legal stops and surfaces rather than wiring the float
  arms.

## The close-gate

§4's Sprint 3 acceptance does not name the calculator smoke test — the
calculator needs `while` loops and functions, which are Sprint 4 and Sprint 5.
The calculator is named in *Sprint 4's* acceptance. Sprint 3 closes (Increment
F) on `grob run hello.grob` over a script that exercises declarations, the scope
chain, `const`/`readonly`, nil safety and string interpolation — the §4
acceptance surface — the same way Sprint 2's D closed on `print(2 + 3 * 4)` →
`14`. The calculator end-to-end belongs to Sprint 4's close.

## Acceptance to hit (whole sprint)

The §4 acceptance, met across the six increments: variables declared, reassigned
and used; `const` rejecting non-compile-time right-hand sides, reassignment and
mutation; `readonly` rejecting reassignment and mutation while accepting any
runtime right-hand side; nil safety working; string interpolation working; the
REPL working; `grob run hello.grob` executing a script file. Sprint 3 is
closeable when Increment F's acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh zip uploaded at the start of
  each increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 3 code is C# 14 from the first line;
  `LangVersion 14` is canonical.
- **ErrorCatalog (D-308).** Every diagnostic references a central catalog
  descriptor; the `"Exxxx"` string for any code appears exactly once in the
  solution. New Sprint 3 codes go in `grob-error-codes.md` and the catalog at
  the numbers the increment prompts assign — never as a literal at the call
  site, never invented.
- **Parser and AST are stable.** Sprint 1's parser is grammar-complete; every
  Sprint 3 increment is type-checker, compiler-emission, VM-opcode-arm and CLI
  work over already-parsed nodes. No increment edits the parser or AST.
- **§4 stays as written.** Whether to rewrite §4 to reflect the A–F structure
  is a documentation-authority call deferred to Sprint 3 close.
- **Model:** per-increment guidance is in each prompt. Sonnet 4.6 (High) is the
  code-gen workhorse throughout; Opus is named only against three specific
  sub-problems (A's local-slot model, C's immutability representation, D's
  backpatching helper) and gated behind "only if this specific thing gets
  fiddly", never "this part is hard".
- Start with Increment A.
