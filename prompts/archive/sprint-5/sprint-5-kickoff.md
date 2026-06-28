# Sprint 5 Kickoff — Functions and Closures

> **A record, not a gate.** Like the Sprint 4 kickoff, this prompt does no setup
> and unblocks nothing. It is not a slash command — it is the durable record of
> the agreed A–F breakdown, the load-bearing ordering calls, the error-code
> position and the close-gate, kept under `prompts/sprint-5/` with the QA brief.
> The increment commands live in `.claude/commands/sprint-5-{a..f}.md`, with
> archive copies under `prompts/archive/sprint-5/`. **Start by invoking
> `/sprint-5-a`.**

Begin Sprint 5 — functions and closures. Sprint 4 made the language branch and
loop. Sprint 5 makes it **call, return and close over** — `fn` declarations and
call frames, named and default arguments, lambdas, closures with upvalue capture,
the top-level initialisation state machine and flow-sensitive narrowing. The full
scope and acceptance are in `docs/design/grob-v1-requirements.md` **§6 (Sprint 5 —
Functions and Closures)**. Read it word for word; it is the build contract.

## Still on Claude Code (D-314)

Sprint 5 runs on the same Claude Code harness Sprint 4 established — durable rules
in `CLAUDE.md`, plan mode as the approval gate, increment prompts as slash
commands, the Husky.NET pre-push gate, CodeRabbit pre-PR, a PR per increment,
`main` protected, GPT-5.3 Codex as the external cold-read via Codex CLI against
the merged branch. Two deltas from Sprint 4:

- **The Opus 4.8 carve-out is a new subagent** — `.claude/agents/grob-closure-specialist.md`,
  reached only for **Increment D**'s upvalue-capture sub-problem (open vs closed
  upvalues, the `CloseUpvalue`-on-return discipline), only if that interaction
  gets fiddly. The Sprint 4 `grob-lowering-specialist` was scoped to `for...in`
  lowering and stays in history; it is not invoked this sprint.
- **Archive copies of the increment commands live under `prompts/archive/sprint-5/`**,
  not alongside the kickoff. (The Sprint 4 kickoff's "alongside them" framing is
  corrected: the archive is `prompts/archive/<sprint>/`; the kickoff and QA brief
  stay under `prompts/<sprint>/`.)

Full harness rationale in D-314.

## No new project, no new opcodes

Sprint 5 adds no `src/` project. It is type-checker, compiler-emission and
VM-opcode-arm work over already-parsed nodes, plus the first native-function
registrations. The function and closure opcodes — `Call`, `Return`, `Closure`,
`GetUpvalue`, `SetUpvalue`, `CloseUpvalue` — were all defined when the `OpCode`
enum was **closed** in Sprint 2 A. Sprint 5 **implements their compiler emission
and VM dispatch arms** for the first time; it does **not** grow the enum. This is
the same shape as Sprint 4 reusing the jump opcodes: the instruction exists, the
arm that emits and executes it is the increment's work. An implementer who reaches
to add an `OpCode` case stops and surfaces — the enum is complete (§3.3, ADR-0013).

The parser and AST are grammar-complete from Sprint 1: `fn` declarations, the
three lambda forms, call expressions and the named-argument call syntax all parse
already. No increment edits the parser, the AST or the `OpCode` enum.

## The agreed increment breakdown

§6 is one section with one acceptance block, carrying function declarations, call
frames, return values, named and default arguments, the three lambda forms,
closures with upvalue capture, the top-level initialisation state machine, native
registration and flow-sensitive narrowing. Sliced into six on the dependency
seams:

- **A — `fn` declarations, call frames, return.** `Call`/`Return` wired into
  dispatch, `CallFrame` + the fixed `CallFrame[256]` array, stack base, locals as
  slots over the call frame, recursion, the **E5901** call-stack-overflow guard,
  the `BytecodeFunction` representation. Positional-call arity and return-type
  checking. The structural increment — everything below reuses the call frame.
  Branch `feat/functions-and-call-frames`.
- **B — Named and default arguments.** Default parameter values, the D-113
  positional-then-named calling convention, the four named-argument call-site
  diagnostics. Type-checker and compiler argument-binding over A's call machinery.
  The new error codes land here. Branch `feat/named-arguments`.
- **C — Lambdas and natives.** The three lambda forms (`x => expr`,
  `x => { block }`, `(a, b) => expr`), the block-body implicit-last-expression
  with `return` early exit (D-276), the function-value-as-first-class-value path,
  the `NativeFunction` representation and `RegisterNative`, the native↔VM call-back
  bridge, and the array higher-order methods (`filter`, `select`, `sort`, `each`)
  as the lambda-consuming natives. Top-level reference resolution only (D-296
  categories 1–3 — `const` inline, `readonly`/mutable global read); **no upvalue
  capture yet**. Branch `feat/lambdas-and-natives`.
- **D — Closures.** Category-4 upvalue capture — `GetUpvalue`/`SetUpvalue`/
  `CloseUpvalue`/`Closure` emission and dispatch, open upvalues referencing live
  stack slots, closed upvalues copied to the heap on the enclosing function's
  return, the `Closure` runtime object (a `BytecodeFunction` plus its upvalue
  array), independent capture per enclosing-function call. The clox closures
  chapter — the heaviest increment, and the only one with an Opus carve-out
  (`grob-closure-specialist`). Branch `feat/closures`.
- **E — Initialisation state machine and narrowing.** The top-level
  initialisation state machine (§19.1, D-294) — the `SlotState`
  `Uninitialised`/`Initialising`/`Initialised` tag, the `DefineGlobal`/`GetGlobal`
  transitions, the `_startupComplete` short-circuit and the **E5902**
  circular-initialisation diagnostic — and flow-sensitive narrowing: inside
  `if (x != nil) { }` the type checker narrows `x` from `T?` to `T`, removed again
  after the block. Two type-system/runtime features, neither on the call-frame
  critical path. Branch `feat/init-and-narrowing`.
- **F — Sprint close.** The `functions.grob` smoke script, the §6 acceptance and
  the third VM-execution benchmark baseline against the two-axis gate (D-313).
  Branch `feat/functions-close`.

Run them in order, each building and testing green before the next, a fresh chat
per increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **Call frames first (A), because everything calls.** `Call`/`Return`, the frame
  array, the stack base and locals-on-slots-over-the-frame are the foundation B's
  argument binding, C's lambda values, D's upvalues and E's startup reads all sit
  on. Standing the frame up first nails the slot/stack-base discipline the rest
  reuse, and it confirms the Sprint 3 local-slot machinery extends cleanly to a
  per-frame base rather than a single global frame.
- **Arguments before lambdas (B before C).** The named/default calling convention
  is pure binding work over A's positional call — get it right on named functions
  first, where the parameter list is declared, before lambdas (which are anonymous
  functions over the same call surface) add the value-passing shape.
- **Lambdas before closures (C before D), split on the capture seam.** A lambda
  that references only top-level bindings (D-296 categories 1–3) needs **no**
  upvalue machinery — it reads `const` inline or the global via a global-read
  opcode. Only a lambda that references an **enclosing-function local** (category
  4) needs capture. C delivers lambdas, natives and the array higher-order methods
  working for categories 1–3; D bolts on category-4 capture. This is exactly the
  clox functions-then-closures order, and it keeps the upvalue machinery out of C.
- **The init machine and narrowing last before close (E), off the call path.** The
  §19.1 startup state machine and `if (x != nil)` narrowing are independent
  type-system/runtime features. Neither blocks the call frame, lambdas or closures,
  so they round off the sprint rather than sitting on the critical path. They are
  bundled into one increment because each is small and the pairing rationale is
  simply "the two features that are not call machinery" — not a shared mechanism.

## Planning constraints recorded here (durable context for the increment prompts)

- **The core function diagnostics already exist — Sprint 5 reuses them, it does
  not mint them.** `grob-error-codes.md` already carries **E0003** (wrong number
  of arguments), **E0004** (argument type mismatch), **E0005** (return type
  mismatch), **E5901** (call-stack overflow) and **E5902** (circular
  initialisation). Sprint 5 raises these through their existing `ErrorCatalog`
  descriptors (D-308). Unlike Sprint 4, most of this sprint registers no new code.
- **Four error codes are assigned this sprint, at the next-free numbers, never
  invented by an implementer.** The named-argument call-site diagnostics that
  D-113 specifies as compile errors but the registry does not yet itemise get
  **dedicated** codes (D-318), not a fold into E0003:
  - **E0008** — named argument before positional.
  - **E0009** — naming a required (defaultless) parameter.
  - **E0010** — duplicate named argument.
  - **E0011** — unknown parameter name.

  These occupy the next free slots in the **E00xx** Type block (E0006 and E0007
  are taken). Registered in **Increment B**, citing **D-318** as the source
  decision; raised through `ErrorCatalog` descriptors, no `"Exxxx"` literal at a
  call site. Each increment confirms the real next-free number against
  `grob-error-codes.md` before assigning. If a diagnostic needs a code not listed
  here and not already registered, the implementer **stops and surfaces** — it
  does not invent.
- **The array higher-order methods are in scope (Increment C), as the
  lambda-consuming natives.** `filter`, `select`, `sort` and `each` on `T[]`
  (the type registry surface) are the demonstration vehicle the §6 acceptance
  names. They are **not** stdlib modules — those are Sprint 8. They are built-in
  type methods implemented as natives that call back into the VM with each
  element. Their lambda argument may capture; in C the capture is categories 1–3
  only (top-level references), and D's upvalues make a body-local-capturing lambda
  argument work through the same call.
- **The §6 acceptance line "lambdas work in filter, map, sort" names a method that
  no longer exists.** `map` was removed by **D-280**; the canonical transformation
  primitive is `select`. The §6 acceptance is corrected to "filter, **select**,
  sort" as a mechanical spec fix in Increment C (the registry and
  `writing-grob-source` already say `select`; only the §6 prose lags). This is a
  drift correction, not a design change — surfaced and sanctioned, not silently
  swept.
- **The close-gate script is `functions.grob`, built from the Sprint 1–5 surface
  only, with no stdlib modules.** Like Sprint 3's `hello.grob` and Sprint 4's
  `calculator.grob`, it is a planning-defined smoke test, **not** one of the
  thirteen release-gate scripts. It exercises a recursive function, a function with
  named and default parameters, a `makeCounter` closure (upvalue capture), an array
  pipeline (`filter`/`select`/`sort` with a capturing lambda) and an `if (x != nil)`
  narrowing — the whole Sprint 5 surface end-to-end. It uses no `fs`, `json`,
  `process` or any other module, none of which exist until Sprint 8.

## The close-gate

Sprint 5 closes (Increment F) on `grob run functions.grob` over a script that
exercises declarations, recursion, named and default arguments, lambdas, a
closure, the array higher-order methods and narrowing — the §6 acceptance surface
— the way Sprint 4 closed on `grob run calculator.grob`. Sprint 5 is closeable
when Increment F's acceptance is green and the third VM-execution benchmark
baseline passes the two-axis gate (D-313).

## Acceptance to hit (whole sprint)

The §6 acceptance, met across the six increments: functions call and return
correctly; recursion works; named and default arguments work, with the four
call-site error cases caught; lambdas work in `filter`, `select` and `sort`;
closures capture enclosing-function locals, with independent capture per call; the
top-level initialisation state machine catches circular initialisation; flow-
sensitive narrowing narrows `T?` to `T` inside `if (x != nil)`; the type checker
catches arity and argument-type mismatches; `grob run functions.grob` runs. Sprint
5 is closeable when Increment F's acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh corpus zip uploaded at the start
  of each increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 5 code is C# 14 from the first line.
- **`ErrorCatalog` (D-308).** Every diagnostic references a central catalog
  descriptor; the `"Exxxx"` string for any code appears exactly once. The four new
  Sprint 5 codes go in `grob-error-codes.md` and the catalog at the numbers above
  in Increment B — never as a literal at the call site, never invented. The
  consistency gate (D-316) asserts catalog↔registry agreement on every commit.
- **Parser, AST and the `OpCode` enum are stable.** Every Sprint 5 increment is
  type-checker, compiler-emission, VM-opcode-arm, native-registration and (F)
  fixture work over already-parsed nodes. No increment edits the parser, the AST
  or the closed enum.
- **Tests are plain xUnit.** Routed per §3.5 to the project matching their kind.
  `[Theory]` rows cover the layer invariant for pipeline work; `FsCheck` and
  `FluentAssertions` are not in `Directory.Packages.props` and are not used —
  follow the `tdd-cycle` skill.
- **§6 stays as written**, save the mechanical `map` → `select` correction in C.
  Whether to rewrite §6 to the A–F structure is a documentation-authority call
  deferred to Sprint 5 close, as it was for §5.
- **Model (D-314).** Sonnet 4.6 (High) is the code-gen workhorse throughout; the
  Opus 4.8 `grob-closure-specialist` subagent is named only against Increment D's
  upvalue-capture sub-problem and gated behind "only if this specific open/closed-
  upvalue interaction gets fiddly", never "this part is hard".
- Start with `/sprint-5-a`.
