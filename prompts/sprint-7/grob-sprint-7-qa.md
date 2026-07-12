# Sprint 7 QA Brief — Error Handling (cold-read)

> **What this is.** The adversarial cold-read brief for Sprint 7, run by GPT-5.3 Codex
> via Codex CLI against the **merged** Sprint 7 branch after Increment E lands and
> before the sprint is declared closed. It is the external second pair of eyes on the
> A–E work — the same role the Sprint 5 and Sprint 6 QA briefs played. It is **not** a
> slash command and unblocks nothing; it is the durable QA contract, kept under
> `prompts/sprint-7/` with the kickoff.
>
> **Standing instruction to the cold-reader.** Read the merged branch directly. The
> design corpus under `docs/design/` is authority and **the decisions log wins on
> conflict**. Assume each seam below is wrong until the code proves it right — this
> brief is a list of where error handling breaks after it looks done, not a list of
> things that are probably fine. Report findings against the acceptance and the risk
> classes; do not fix, do not open PRs.

## Why this brief is shaped the way it is

Sprint 7 is the first sprint that unwinds the stack. Unlike a struct field or a
closure, an exception can leave any frame at any point, and `finally` must run on
every one of six exit paths and none of a seventh. The failure modes are not spread
evenly — they cluster in a small number of places where the stack, the handler table
and the emitted finally chains meet. Stripped to causes, the sprint's risk falls into
seven classes; the first two are where a shipped bug would be most expensive:

1. **Non-local control flow through `finally` (D-332).** The emitted finally chain on
   early `return`/`break`/`continue` through nested `try`/`finally` regions — run each
   pending finally exactly once, innermost-to-outermost, and get `break`/`continue`
   targeting an **outer** loop right. This is the sprint's headline risk, the analogue
   of Sprint 6's D-325 closure-in-field probe.
2. **Stack and frame unwinding on `throw`.** The value stack and call frames unwound
   to the handler frame with no underflow and no leaked operands, and upvalues closed
   by location on the exceptional path (D-325).
3. **Catch matching.** Polymorphic, source-order, first-match, catch-all-last, over
   the leaf-`<:`-`GrobError` subtype relationship.
4. **Exception construction reuses Sprint 6 struct machinery (D-043).** `throw Leaf {
   … }` is `NewStruct`; the leaves are real registered types with the D-274 fields and
   the runtime-set `location` rule.
5. **`exit()` uncatchability (D-110).** Uncatchable past every catch and every finally.
6. **Runtime-throw catchability and the D-039 clarification.** The existing runtime
   sites catchable, an unhandled one unchanged, a caught one resuming.
7. **Closed-surface and error-code integrity.** No `Leave`/`EndFinally`, the enum
   untouched, the two or three new codes in lockstep, D-316 green.

## Per-increment adversarial probes

### Increment A — the exception hierarchy, `throw`, the unhandled top-level path

- **Hierarchy registration (class 4).** Confirm `GrobError` and all ten leaves resolve
  by name and are registered as **built-in** nominal types (not user `type`
  declarations through the Sprint 6 pass-1/pass-2 path). Confirm the D-274 fields:
  `GrobError.message: string`, `GrobError.location: SourceLocation?`,
  `NetworkError.statusCode: int?`, and that no other leaf carries extra fields in v1.
- **Subtyping (class 3).** Confirm the assignability check is real and **polymorphic**,
  not exact-match special-cased: a leaf is assignable to `GrobError`, `GrobError` is
  **not** assignable to a leaf, and a leaf is **not** assignable to a sibling leaf.
  Grep for any `== GrobError`-style exact-type comparison standing in for a subtype
  check — that is the latent gap that breaks post-MVP user exceptions.
- **`throw` operand (class 4).** Confirm `throw IoError { message: "x" }` type-checks;
  `throw 42`, `throw "oops"` and `throw someInt` are the new throw-operand code (the
  provisional E0014 — confirm the real number) with a source location, not a crash.
- **Construction reuse (class 4).** Confirm `throw Leaf { … }` disassembles to the
  field values then `NewStruct` then `Throw` — verified through the disassembler, not
  only the VM's answer — and obeys E0103 (missing required field) and E0012 (unknown
  field). Confirm a user-supplied `location` is accepted at the checker but
  **overwritten** by the runtime (D-274); the diagnostic on an unhandled throw carries
  the throw-site location, not the user value.
- **Unhandled path (class 2).** Confirm an unhandled `throw`, including from a nested
  call frame, unwinds every frame, closes open upvalues by location (D-325) with no
  value-stack underflow, and produces the quality diagnostic (`file:line`, D-322;
  error type; message; suggestion) with exit code 1.
- **Scope discipline.** Confirm A emitted **only** `Throw` — no `TryBegin`/`TryEnd`,
  no handler table, no catch, no finally. The enum is unchanged. The count moved 112 →
  113 for the one new code, in three-location lockstep.

### Increment B — `try` / `catch`

- **Matching (class 3). The core probe.** Confirm typed catches match in **source
  order**, first-match wins, and the catch-all matches anything not matched above.
  Confirm `catch (e: GrobError)` behaves as a catch-all. Confirm a throw unmatched in
  the nearest region unwinds to the next enclosing region, and to the top level if
  nothing matches. Probe the flat-hierarchy trap: matching must be built
  polymorphically even though v1's flat hierarchy makes it observably exact — grep for
  an exact-match shortcut.
- **Unwind (class 2). The second-highest probe.** Confirm a `throw` from a nested call
  frame inside a `try` unwinds the frames to the handler frame, binds the exception
  into the catch slot, and leaves the value stack as exactly the handler frame's stack
  plus the bound exception — **no underflow, no leaked operands** from the abandoned
  try body. Confirm upvalues close by location on the torn-down frames (D-325).
- **Compile errors (class 7).** Confirm **E2204** (`try` with neither catch nor
  finally), **E2205** (`catch` after catch-all, D-083), the catch-type code
  (provisional E0015, `catch (e: int)`) and the duplicate-catch code (provisional
  E2213, two `catch (e: IoError)`). Confirm `catch (e)` (parens, no type) is a clean
  syntax diagnostic through recovery (D-300), not a crash. Confirm the catch binding
  is **immutable** — reassignment is a compile error.
- **Permissiveness.** Confirm `catch (e: IoError)` on a try that cannot throw `IoError`
  type-checks (the C# model, no can-throw analysis).
- **Code integrity (class 7).** Confirm the two new codes registered in lockstep at
  their real next-free numbers, no `"Exxxx"` literal at a call site, D-316 green, and
  the duplicate-catch fold-vs-new call recorded (dedicated or folded into E2205).

### Increment C — `finally` (the load-bearing increment)

- **The finally chain (class 1). This is the highest-value probe in the sprint.**
  Confirm `finally` runs **exactly once** on each of the six paths — normal
  completion, uncaught exception (then propagation continues), catch-then-normal,
  catch-then-throw (then the new exception propagates), early `return`, early
  `break`/`continue`. Assert once-ness via a counter the finally increments, not by
  eyeballing output. Confirm a `return` through *N* nested `try`/`finally` regions runs
  all *N* finallys **inner-to-outer**, each once — verified through the disassembler
  (the bodies emitted at the transfer site) **and** the VM. Confirm a `break`/
  `continue` whose target loop sits outside one or more intervening `try`/`finally`
  regions runs each intervening finally exactly once then transfers. Any miss here —
  a finally run twice, zero times, or out of order — is the headline finding.
- **The partition (class 1).** Confirm the exceptional path is **VM-run**
  (`finallyOffset`) and the non-exceptional paths are **compiler-emitted** (D-332).
  Confirm **no `Leave`/`EndFinally` opcode** was added — grep the enum; if one appears
  it is a closed-surface breach and a blocker regardless of correctness.
- **Throw-in-finally (class 1).** Confirm a `throw` inside a `finally` replaces the
  in-flight exception (the outer catch sees the finally's exception, the original is
  lost — no chaining), on both the VM-run and the compiler-emitted-then-diverted path.
- **`exit()` exclusion (class 5).** Confirm `exit()` inside a `try`/`finally` runs
  **no** finally and terminates with the code.
- **Compile errors (class 7).** Confirm **E2206** (`finally` not last) and **E2207**
  (`return`/`break`/`continue` inside `finally`). Confirm the D-276 carve-out: a
  `return` inside a **block-body lambda** inside a `finally` is **not** E2207.
- **D-325 on the finally path (class 2).** Confirm a closure captured before a `throw`
  and reached after the finally runs sees its captured value with no underflow.
- **Decision log.** Confirm the D-332 entry is present, at a real next-free number, in
  three-location lockstep, extending D-275; no new error code this increment.

### Increment D — runtime-throw catchability and uncatchable `exit()`

- **Catchability (class 6).** Confirm each routed site is catchable: int div/0 →
  `ArithmeticError`, nil dereference → `NilError`, out-of-range index → `IndexError`,
  stack overflow → `RuntimeError`. Confirm `finally` runs around a routed runtime error
  as around a user `throw`. Confirm `map<K, V>` key-not-found is **not** routed (it
  returns `V?`).
- **The D-039 clarification (class 6).** Confirm a caught runtime error **resumes** the
  script (a later statement runs) — the "first **unhandled**" reading. Confirm an
  **unhandled** routed runtime error produces the **same** top-level diagnostic
  (message, `file:line`, exit 1) as before routing — gold-master comparison where one
  exists. Confirm any prose that stated "first runtime error" without the "unhandled"
  qualifier was corrected mechanically, surfaced not swept.
- **`exit()` vs runtime error (class 5).** Confirm a routed runtime error inside a
  `try` is caught by the catch-all and by `catch (e: GrobError)`, while `exit()` in the
  same position is caught by **neither** and runs no finally. The two must not be
  conflated.
- **Scope discipline (class 7).** Confirm D added **no** opcode, **no** error code and
  **no** parser/AST edit — routing over the A/B/C machinery only. Count unchanged.

### Increment E — close

- Confirm `grob run errors.grob` meets the § acceptance and that `errors.grob`
  exercises every bullet (throw, source-order typed catch, catch-all, finally on the
  normal and exceptional paths, a nested-finally early-return chain, a caught
  `ArithmeticError`, an uncatchable `exit()`) using **no** stdlib modules. Confirm
  `hello.grob`, `calculator.grob`, `functions.grob` and `types.grob` still run.
- Confirm the **fifth** VM-execution benchmark baseline was produced from the workflow
  (D-309) and passes the two-axis gate (D-313).

### Note — `Sprint6IncrementBTests` state

The four `Sprint6IncrementBTests` assert the exact rendered struct form
(`Config { host: "example.com", port: 8080 }`) and pass — the pre-Sprint-8 interlude's
Increment C (D-336) has landed. No caveat is needed here beyond this pointer to D-336.

## Cross-cutting probes

- **The unwind is one mechanism, not several (class 1/2).** Confirm user `throw`,
  routed runtime errors and catch-rethrows all unwind through the **same** code path —
  grep for a second bespoke unwind for runtime errors that bypasses the handler table
  or the `finallyOffset`. A parallel path is the latent gap that lets `finally` be
  skipped for one class of failure.
- **OpCode enum untouched.** Confirm `TryBegin`, `TryEnd`, `Throw` are the **same**
  enum members closed in Sprint 2 — no case added, **no `Leave`/`EndFinally`**, the
  enum unchanged (ADR-0013). Confirm no increment edited the parser or the AST beyond
  what D-274/D-275 already mandated (verified, not grown — D-331).
- **Error-code integrity.** Confirm the final count reconciles from 112 through the two
  or three additions (throw-operand, catch-type, and the duplicate-catch code if taken
  dedicated); catalog↔registry agreement holds; no `"Exxxx"` literal anywhere an
  `ErrorCatalog` descriptor should be. Confirm E2204–E2207 are wired to live
  diagnostics for the first time and each fires.
- **§3.1.1 invariant.** Spot-check that identifier and member nodes across the new
  surface — the throw operand, the catch binding — carry non-null `ResolvedType` and
  `Declaration` (the LSP day-one properties). Confirm the immutable catch binding
  reads as a readonly local.
- **DAG.** Confirm `Grob.Compiler` and `Grob.Vm` still do not reference each other,
  `Grob.Core` remains the only shared ground, and the exception hierarchy lives in
  `Grob.Runtime` (D-084/D-284) without pulling a new cross-edge.
- **Coverage.** Confirm the affected projects hold at or above the 90% line+branch
  floor (D-328, ADR-0018) and that nothing was excluded to make the number — the
  unwind and finally-chain paths especially, which are easy to leave partially covered.

## Report format

For each finding: the increment, the risk class (1–7) or "new", the file and line, the
observed behaviour, the expected behaviour with its corpus citation (decisions log
wins) and severity (blocker / should-fix / note). Lead with any **finally-chain
defect** (class 1 — a finally run twice, zero times or out of order, or a
`Leave`/`EndFinally` opcode added) or any **unwind stack defect** (class 2 — underflow,
leaked operands, an upvalue not closed) — those are the two classes most likely to
ship and most expensive if they do. Close with an overall verdict: closeable, or the
blocker list that must clear first.
