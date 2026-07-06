# Sprint 7 Kickoff — Error Handling

> **A record, not a gate.** Like the Sprint 5 and Sprint 6 kickoffs, this prompt
> does no setup and unblocks nothing. It is not a slash command — it is the durable
> record of the agreed A–E breakdown, the load-bearing ordering calls, the
> error-code positions, the one new structural decision (the `finally` compilation
> model, D-332) and the close-gate, kept under `prompts/sprint-7/` with the QA brief.
> The increment commands live in `.claude/commands/sprint-7-{a..e}.md`, with archive
> copies under `prompts/archive/sprint-7/`. **Start by invoking `/sprint-7-a`.**

Begin Sprint 7 — error handling. Sprint 6 made the language declare, construct and
access structured data. Sprint 7 makes it **fail and recover**: the `throw` statement,
the built-in exception hierarchy registered as types, `try`/`catch` with typed and
catch-all handlers, `finally`, and the top-level diagnostic for an unhandled
exception. The full scope and acceptance are in `docs/design/grob-v1-requirements.md`
**§ (Sprint 7 — Error Handling)**. Read it word for word; it is the build contract.
The live construct spec is `docs/design/grob-language-fundamentals.md` **§27**
(exception handling — the hierarchy, `try`/`catch`, `throw`, `finally`, uncatchable
exit).

## Still on Claude Code (D-314)

Sprint 7 runs on the same Claude Code harness — durable rules in `CLAUDE.md`, plan
mode as the approval gate, increment prompts as slash commands, the Husky.NET
pre-push gate, CodeRabbit pre-PR, a PR per increment, `main` protected, GPT-5.3
Codex as the external cold-read via Codex CLI against the merged branch. Archive
copies of the increment commands live under `prompts/archive/sprint-7/`; the kickoff
and QA brief stay under `prompts/sprint-7/`. Full harness rationale in D-314.

**One Opus carve-out this sprint — Increment C.** Sprint 6 had none: its struct
machinery was settled by §9/§10/§17.1 and D-325 had already made upvalue closing
route-agnostic. Sprint 7 is different. The `finally` compilation model (D-332) is a
genuinely new load-bearing decision made at this kickoff, not a verification over a
settled mechanism — the closed `OpCode` enum carries no `Leave`/`EndFinally`, so the
non-exceptional `finally` paths are compiled as emitted finally chains that must run
across nested `try`/`finally` regions exactly once, in order. That is the Sprint 7
analogue of Sprint 5 D's open/closed-upvalue sub-problem, and it gets the same
treatment: Sonnet 4.6 (High) drives the increment, and the finally-emission-chain
sub-problem escalates to an Opus 4.8 subagent (the Sprint 5 D `grob-closure-specialist`
mechanism), config added under `.claude/agents/` as part of C. A–B, D and E are
Sonnet throughout. If any other increment turns out to carry a load-bearing
structural decision, stop and surface it rather than reaching for Opus on a task
that merely feels hard.

## No new project, no new opcodes

Sprint 7 adds no `src/` project. It is type-checker, compiler-emission and
VM-opcode-arm work over already-parsed nodes, plus the `Grob.Runtime` exception-type
registration. The exception opcodes — `TryBegin`, `TryEnd`, `Throw` — were all
defined when the `OpCode` enum was **closed** in Sprint 2 A (§3.3). Sprint 7
**implements their compiler emission and VM dispatch arms** for the first time; it
does **not** grow the enum. This is the same shape as Sprint 6 implementing the
struct opcodes and Sprint 5 reusing `Call`/`Return`/`Closure`: the instruction
exists, the arm that emits and executes it is the increment's work. **No
`Leave`/`EndFinally` opcode is added** — D-332 puts the non-exceptional `finally`
paths in the compiler, not a new instruction, precisely so the enum stays closed. An
implementer who reaches to add an `OpCode` case stops and surfaces — the enum is
complete (§3.3, ADR-0013). Growing it would be the `adding-an-opcode` procedure
(D-331) and a wire-format version consideration, not an incidental edit.

**The grammar is verified, not assumed.** D-331 recorded that the "grammar-complete
from Sprint 1" premise was false twice — Sprint 4E and 6B both needed new
productions. Sprint 7 does not repeat that mistake. D-274 and D-275 record the AST
additions the design mandates — `ThrowStatement(expression)`, `CatchClause(binding,
type, body)`, `FinallyClause(body)` on `TryStatement`, and the `Throw`/`Finally`
`TokenKind`s. Increment A's **first** act is to confirm the `try`/`catch`/`finally`/
`throw` productions actually parse against the merged tree. If a production is
missing or malformed, that is a finding — extend the grammar through the
`extending-the-grammar` skill (D-331), surfaced not swept, before building on it. Do
not assume the parser is done because the design says it should be.

## The agreed increment breakdown

§ (Sprint 7) is one section with one acceptance block, carrying the exception
hierarchy, `throw`, `try`/`catch`, typed and catch-all matching, `finally`, the
uncatchable `exit()` path and the unhandled-exception top-level diagnostic. Sliced
into five on the dependency seams:

- **A — The exception hierarchy, `throw` and the unhandled top-level path.**
  Register `GrobError` and its ten leaves as built-in nominal types in
  `Grob.Runtime` with their D-274 fields (`message: string`, runtime-set
  `location: SourceLocation?`, and `NetworkError.statusCode: int?`), including the
  **first user-observable subtype relationship** (each leaf `<: GrobError`); resolve
  a `throw <expr>` whose operand must be a `GrobError` subtype (the new
  throw-operand code); emit `Throw`; and give the VM a `Throw` arm that, with **no**
  handler present, unwinds the whole stack to the VM top level and produces the
  Grob-quality diagnostic (file, line, error type, message, suggestion) and exit
  code 1. Exception construction reuses Sprint 6's `NewStruct` path — `throw IoError
  { message: "…" }` **is** named construction (§10, D-043). The structural increment:
  B's matching, C's finally and D's runtime routing all resolve against the hierarchy
  registered here. Branch `feat/exception-hierarchy`.
- **B — `try` / `catch`.** `TryBegin`/`TryEnd` emission, the handler table mapping
  each protected region to its ordered catch handlers, polymorphic source-order
  first-match, the catch-all-last rule, the immutable catch binding in scope with
  its declared type (or `GrobError` for the catch-all); the catch compile errors —
  **E2204** (`try` without `catch` or `finally`), **E2205** (`catch` after
  catch-all), the new **catch-type-not-`GrobError`** code and the new
  **duplicate-typed-catch** code; the VM's unwind-to-nearest-matching-handler arm.
  No `finally` yet. Branch `feat/try-catch`.
- **C — `finally`. The load-bearing increment.** The handler-table `finallyOffset`
  for the exceptional path (D-275) and the **emitted finally chain** for every
  non-exceptional path — normal try completion, normal catch completion, and early
  `return`/`break`/`continue` leaving one or more enclosing `try`/`finally` regions,
  each pending finally emitted innermost-to-outermost exactly once (D-332); `throw`
  inside `finally` replaces the in-flight exception; **E2206** (`finally` not last)
  and **E2207** (`return`/`break`/`continue` inside `finally`) enforced. The Opus
  subagent carve-out sits on the finally-chain sub-problem. Branch `feat/finally`.
- **D — Runtime-throw catchability and uncatchable `exit()`.** Route the existing
  runtime throw sites live from Sprints 3–6 — `ArithmeticError` (overflow, int/float
  div/0 and mod/0, math domain), `NilError` (nil dereference), `IndexError` (array
  and substring bounds) and `RuntimeError` (call-stack depth exceeded) — through the
  first-class exception object model built in A, so `try`/`catch` catches them where
  before they halted the VM directly. Confirm `exit()`/`ExitSignal` (D-110) unwinds
  past **every** catch and **every** finally uncaught, and that no finally runs on
  the exit path. The D-039 two-mode contract is clarified here — the VM halts on the
  first **unhandled** runtime error, and a caught one resumes normally. Branch
  `feat/runtime-throws`.
- **E — Sprint close.** The `errors.grob` smoke script, the § acceptance and the
  fifth VM-execution benchmark baseline against the two-axis gate (D-313). Branch
  `feat/errors-close`.

Run them in order, each building and testing green before the next, a fresh chat per
increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **The hierarchy first (A), because everything resolves against it.** A `throw`
  needs a `GrobError` subtype to construct and check; a `catch (e: T)` needs `T`
  registered and its subtype relationship to `GrobError` known; the runtime routing
  in D needs the leaf types to instantiate. Standing the hierarchy up first — and
  proving `throw` end-to-end against the unhandled top-level path before any handler
  exists — nails the exception-construction and subtype-assignability discipline the
  rest reuse. A also carries the first real **nominal subtyping** the type system
  exercises (leaf `<: GrobError`); get it right once, here.
- **`throw` before `catch` (A before B).** You catch a thrown value, so the `Throw`
  path and the exception object must exist before there is anything to unwind to a
  handler. A's `Throw` arm unwinds to the **top level** (no handler); B adds the
  handler table and the unwind-to-**nearest-handler** arm on top. The two paths
  split cleanly on that seam — A is unwind-to-top, B is unwind-to-handler.
- **`catch` before `finally` (B before C).** `finally` composes with catch — a
  caught-then-normal exit falls through the finally, a caught-then-throw exit runs
  the finally on the way out. Build the catch machine and its handler table first,
  then add the `finallyOffset` and the emitted chains on top of a working unwind.
- **`finally` before the runtime routing (C before D).** `finally` is orthogonal to
  which exceptions exist; build and test it against user `throw`s while the machinery
  is fresh, then D widens the set of things that can throw. Doing the hard structural
  work (C) before the wide-but-mechanical routing (D) keeps the risk early — the
  Sprint 6 nominal-before-structural rhythm.
- **Close last (E), off the feature path.** The smoke script and the benchmark
  baseline round off the sprint once A–D are green.

## The one new structural decision — the `finally` compilation model (D-332)

This is the single load-bearing call of the sprint and it is settled here so the
increment prompts can cite it. Record it as the next-free `D-###` (provisionally
**D-332**, confirmed against the live decisions-log tail when Increment C lands —
the same verify-before-allocate discipline the error codes follow) extending D-275.

D-275 already fixed the **exceptional** path: the handler table carries a
`finallyOffset` per entry and the VM runs the finally before propagating. What was
open — and is decided now — is the **non-exceptional** paths. The closed `OpCode`
enum (§3.3) has `TryBegin`/`TryEnd`/`Throw` but **no `Leave`/`EndFinally`**, so the
VM cannot intercept a `Return` or a `Jump` to run a pending finally. The decision:

- **No `Leave`/`EndFinally` opcode is added.** The non-exceptional finally paths are
  compiled, not VM-intercepted.
- **The compiler tracks the stack of enclosing `try`/`finally` regions.** At every
  control-transfer site that leaves one or more of them — normal fall-through
  completion of a try or catch body, and early `return`/`break`/`continue` from
  inside a try or catch body — it emits each pending finally block's body inline, in
  **innermost-to-outermost** order, before the transfer. Normal completion emits the
  finally once at fall-through. A non-local exit through *N* nested `try`/`finally`
  regions emits *N* finally bodies, each exactly once.
- **The E2207 control-flow-in-`finally` ban makes the chain analyzable.** A finally
  body never itself transfers control (`return`/`break`/`continue` are compile
  errors inside it; only `throw` is permitted), so every emitted chain is
  straight-line and terminating. The one finally-body exit that is not straight-line
  is a `throw`, which re-enters the unwinding path and replaces the in-flight
  exception (D-275).
- **`exit()` is not a chained transfer.** `exit()`/`ExitSignal` (D-110, D-274)
  unwinds past every finally without running any — it is a VM-level stack teardown,
  not a control transfer the compiler chains.

Rationale: duplication is the javac-post-`jsr` model. A dedicated opcode would grow a
wire-format surface (ADR-0013) for a mechanism the compiler handles cleanly, and
`finally`'s own control-flow ban makes the duplication bounded and analyzable. The
partition is exact — **compiler-emitted** finally on every straight-line
fall-through and early-exit path; **VM-run** finally (`finallyOffset`) on every
exceptional propagation path (uncaught from a try, thrown from a catch).

## Planning constraints recorded here (durable context for the increment prompts)

- **Most exception diagnostics already exist — Sprint 7 wires them, it does not mint
  most of them.** The exception surface was specced with its structural diagnostics.
  `grob-error-codes.md` already carries **E2204** (`try` without `catch` or
  `finally`), **E2205** (`catch` after catch-all), **E2206** (`finally` not last in
  `try`) and **E2207** (`return`/`break`/`continue` inside `finally`), each raised
  through its existing `ErrorCatalog` descriptor (D-308). The ten runtime leaves
  each have their descriptor with a `throws_type`. Confirm every code against the
  registry before use; if a diagnostic needs a code not listed here and not already
  registered, follow `allocating-an-error-code` (D-331) — do not invent a literal.
- **Two or three error codes are assigned this sprint, at the next-free numbers.**
  Two are certain, one is a fold-vs-new call, and all three are confirmed next-free
  against the **live** registry at their increment (the base in the current snapshot
  is 112, with E0012 and E0013 already present from Sprint 6):
  - **throw-operand-not-an-exception-type** (`throw 42`, `throw "oops"`) — a
    dedicated Type-block code (provisionally **E0014**), not a fold into E0001, the
    D-318/D-330 house style. Registered in **Increment A** through its `ErrorCatalog`
    descriptor.
  - **catch-type-not-`GrobError`-or-subtype** (`catch (e: int)`) — a dedicated
    Type-block code (provisionally **E0015**). Registered in **Increment B**.
  - **duplicate typed catch for the same type** — a Syntax-block code (provisionally
    **E2213**), sibling of E2205. Whether this is a dedicated code or folds into
    E2205's "unreachable catch" framing is an `allocating-an-error-code` (D-331)
    fold-vs-new call to make in **Increment B**. Lean: dedicated — the diagnostic is
    distinct and actionable ("catch for `IoError` already declared" vs "catch after
    catch-all").
  - **`catch (e)` (parens, no type)** is a syntax error (D-274). It most likely
    reuses the generic parser-recovery diagnostic (D-300) rather than a dedicated
    code — confirm in B; add a code only if recovery produces a misleading message.
  Each new code is registered in three-location lockstep (summary row, full entry,
  footer changelog), the count reconciled, and the D-316 consistency gate asserts
  catalog↔registry agreement on the commit.
- **The D-039 two-mode contract is clarified, not changed (D).** D-039 predates
  `try`/`catch`; its "the VM stops on the first runtime error" now reads "the first
  **unhandled** runtime error" — a caught runtime error resumes normally, which is
  the whole point of the feature. This is a spec clarification, the Sprint 6 §17.1
  `E—cycle`-drift shape — surfaced not swept. Increment D confirms the fundamentals
  and requirements prose reads correctly against `try`/`catch`; if any passage
  states "first runtime error" without the "unhandled" qualifier in a way that now
  misleads, correct it mechanically citing this clarification. No design change, no
  new number beyond the note.
- **Exception construction is Sprint 6 construction (A).** `throw IoError { message:
  "…" }` goes through the `NewStruct` path and obeys named-construction rules —
  required-field validation (E0103) and unknown-field (E0012) apply to exception
  types as to any struct. The `location` field is **runtime-set and any user-supplied
  value is ignored** (D-274); the checker permits it in the initialiser but the
  runtime overwrites it. Do not build a second construction path for exceptions.
- **The subtype relationship is real, even though the hierarchy is flat (A).** In v1
  the hierarchy is a single root with ten flat leaves and is closed (no user
  exceptions, D-085), so polymorphic catch matching has the same observable
  behaviour as exact matching in every legal program. The assignability check is
  specified and built polymorphically anyway — leaf assignable to `GrobError`,
  `GrobError` not assignable to a leaf — so user-defined exceptions slot in post-MVP
  without a grammar or matching-engine change (D-274). Build the real check; do not
  special-case the flat shape.
- **The unwind must respect location-based upvalue closing (C/D, D-325).** An
  exception unwinding a frame closes that frame's open upvalues exactly as a normal
  return does — D-325 made closing location-based and route-agnostic, and the
  exceptional exit is one more route. A closure captured in an outer scope and
  reached after an inner frame is torn down by a `throw` must still see its captured
  value with no value-stack underflow. This is verification over the settled D-325
  mechanism on the exception path, and it is a mandatory C acceptance line.
- **The close-gate script is `errors.grob`, built from the Sprint 1–7 surface only,
  with no stdlib modules.** Like Sprint 4's `calculator.grob`, Sprint 5's
  `functions.grob` and Sprint 6's `types.grob`, it is a planning-defined smoke test,
  **not** one of the thirteen release-gate scripts. It exercises a user `throw` of a
  constructed exception, a typed catch, the catch-all, a `finally` that runs on both
  a normal and an exceptional path, a nested `try`/`finally` with an early `return`
  that runs both finallys in order, a caught runtime error (an int div/0 producing a
  catchable `ArithmeticError`) and an `exit()` inside a `try`/`finally` that proves
  neither the catch nor the finally runs. It uses no `fs`, `json`, `process`, `env`
  or any other module — none exist until Sprint 8.

## The close-gate

Sprint 7 closes (Increment E) on `grob run errors.grob` over a script that exercises
`throw`, typed and catch-all matching, `finally` on the normal and exceptional paths,
a nested-finally early-exit chain, a caught runtime error and the uncatchable
`exit()` — the § acceptance surface — the way Sprint 6 closed on `grob run
types.grob`. Sprint 7 is closeable when Increment E's acceptance is green and the
fifth VM-execution benchmark baseline passes the two-axis gate (D-313).

## Acceptance to hit (whole sprint)

The § acceptance, met across the five increments: `try`/`catch` works; typed catches
match polymorphically in source order and the catch-all catches everything not
matched above; a catch after the catch-all, a duplicate typed catch, a `try` with
neither catch nor finally, a `finally` not last, control flow inside a `finally`, a
non-`GrobError` throw operand and a non-`GrobError` catch type are all compile
errors; `throw` constructs and raises a `GrobError` subtype; `finally` runs on every
normal and exceptional exit and never on `exit()`; runtime errors are catchable;
unhandled exceptions produce a Grob-quality diagnostic and exit 1; `exit()` cannot be
caught; `grob run errors.grob` runs. Sprint 7 is closeable when Increment E's
acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh corpus zip uploaded at the start of
  each increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 7 code is C# 14 from the first line.
- **`ErrorCatalog` (D-308).** Every diagnostic references a central catalog
  descriptor; the `"Exxxx"` string for any code appears exactly once. The two or
  three new Sprint 7 codes go in `grob-error-codes.md` and the catalog at their
  next-free numbers in their increments — never as a literal at the call site, never
  invented. The consistency gate (D-316) asserts catalog↔registry agreement on every
  commit.
- **Parser, AST and the `OpCode` enum are stable — but verified.** Every Sprint 7
  increment is type-checker, compiler-emission, VM-opcode-arm, runtime-registration
  and (E) fixture work over already-parsed nodes. No increment edits the `OpCode`
  enum. The AST additions D-274/D-275 mandate are confirmed to parse in A (the
  D-331 verify-first discipline); if one is absent it is extended through
  `extending-the-grammar`, surfaced not swept.
- **Tests are plain xUnit.** Routed per §3.5 to the project matching their kind.
  `[Theory]` rows cover the layer invariant for pipeline work; `FsCheck` and
  `FluentAssertions` are not in `Directory.Packages.props` and are not used — follow
  the `tdd-cycle` skill.
- **§ (Sprint 7) stays as written**, save the mechanical D-039 two-mode clarification
  in D. Whether to rewrite the section to the A–E structure is a documentation-
  authority call deferred to Sprint 7 close, as it was for §5, §6 and §7.
- **Model (D-314).** Sonnet 4.6 (High) is the code-gen workhorse throughout. The one
  Opus carve-out is the finally-emission-chain sub-problem in Increment C, run
  through an Opus 4.8 subagent (the Sprint 5 D `grob-closure-specialist` mechanism).
- Start with `/sprint-7-a`.
