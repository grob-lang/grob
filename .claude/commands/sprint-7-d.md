---
description: "Sprint 7 · Increment D — runtime-throw catchability and uncatchable `exit()`. Route the existing Sprint 3–6 runtime throw sites (ArithmeticError, NilError, IndexError, RuntimeError) through the first-class exception model so try/catch catches them. Clarify the D-039 two-mode contract to 'first UNHANDLED runtime error'. Verify exit()/ExitSignal is uncatchable past every catch and finally. No new opcode, no new error code."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 7 · Increment D — runtime-throw catchability and uncatchable `exit()`

Increments A–C built the exception machine for **user** `throw`s. This increment makes
the machine real across the surface that already fails: the runtime error sites live
from Sprints 3–6 — an int overflow, an int or float div/0 or mod/0, a math domain
violation, a nil dereference, an array or substring index out of range, a call-stack
overflow — currently halt the VM directly. This increment routes each through the
first-class exception object model (A), so `try`/`catch` (B) catches them and
`finally` (C) runs around them exactly as it does for a user `throw`. It also confirms
the D-039 two-mode contract now reads "first **unhandled** runtime error", and that
`exit()`/`ExitSignal` unwinds past **every** catch and **every** finally uncaught.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 7 — Error Handling** scope
   (the "unhandled exceptions propagate to VM top level" and the "`exit()` cannot be
   caught" bullets), and the **§10 error model** rules referencing the two-mode
   contract; §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§27** canonical throw-site table
   (the leaf each runtime failure maps to) and the uncatchable-exit subsection.
   Cross-read **§15** (integer overflow → `ArithmeticError`) and the arithmetic
   subsections, and the nil/index sections, for the exact failure conditions.
3. `docs/design/grob-error-codes.md` — confirm the runtime-leaf descriptors and their
   `throws_type` values; confirm the runtime codes (E5xxx) map to the right leaves. No
   new code is expected this increment — confirm.
4. `docs/design/grob-vm-architecture.md` — how the VM currently signals a runtime
   error (the D-039 first-runtime-error halt) and where the arithmetic/nil/index/
   stack-depth sites raise it. This increment converts those sites, so read them
   first.
5. Decisions: confirm **D-039** (the two-mode error model — the contract this
   increment clarifies), **D-284** (the leaf each site maps to), **D-110**
   (`exit()`/`ExitSignal` uncatchable), **D-322** (runtime diagnostics are
   `file:line`) and **D-308** (`ErrorCatalog`). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep to confirm §27's
> throw-site table, §15's overflow rule and D-039 read as this prompt assumes. If a
> section has moved or a decision been superseded, surface it rather than proceeding.
>
> **Runtime-throw routing rules — inline reference (authoritative source is §27's
> throw-site table, §15, D-039 and D-284; reproduced here so the implementation does
> not depend on a fetch landing well).**
>
> - **The sites to route, and their leaves.** Convert each existing VM-halt site into
>   a first-class `Throw` of the mapped leaf, constructed with a `message` and the
>   runtime-set `location`:
>   - **`ArithmeticError`** — integer overflow (§15), int div/0, int mod/0, float
>     div/0, float mod/0, and any math-domain violation already live.
>   - **`NilError`** — dereference of `nil` without `?.` or `??`.
>   - **`IndexError`** — array index out of range; substring/slice bounds out of range.
>   - **`RuntimeError`** — call-stack depth exceeded (stack overflow); the residual.
>   `map<K, V>` key-not-found is **not** routed — map lookup returns `V?`, not a throw
>   (§27). `ParseError`, `LookupError`, `IoError`, `NetworkError`, `JsonError` and
>   `ProcessError` sites are stdlib (Sprints 8–9) and are **not** in scope here — only
>   the sites that already exist from Sprints 3–6 are routed.
> - **Route, do not re-diagnose.** Each site already produces a correct runtime
>   diagnostic via its `ErrorCatalog` descriptor. Routing means raising the mapped
>   leaf **through the A/B/C machinery** — so the value is catchable and finally runs
>   around it — while preserving the **same** top-level diagnostic when the exception
>   is unhandled. The observable behaviour of an *unhandled* runtime error is
>   unchanged (same message, same `file:line`, same exit 1); what changes is that it
>   can now be **caught**. Do not invent new messages or codes.
> - **The D-039 two-mode clarification.** D-039 predates `try`/`catch`. Its "the VM
>   stops on the first runtime error" now means the first **unhandled** runtime error:
>   a caught runtime error is handled by its `catch` and the script resumes normally;
>   only an uncaught one halts. This is a spec clarification, not a design change — the
>   Sprint 6 §17.1-drift shape, surfaced not swept. If the fundamentals or requirements
>   prose states "first runtime error" without the "unhandled" qualifier in a way that
>   now misleads, correct it mechanically citing this clarification. Do not silently
>   rewrite; surface the correction. No new number beyond the clarification note.
> - **`exit()` is uncatchable, everywhere (D-110).** `exit(n)` throws the internal
>   `ExitSignal`, which is **not** a `GrobError` and matches no `catch` (not even a
>   catch-all, not even `catch (e: GrobError)`), unwinds past every protected region
>   and runs **no** finally (C built the exclusion; D confirms it across the surface),
>   is caught only at the VM top level, flushes buffers and terminates with the code.
>   Verify a routed runtime error inside a `try` **is** catchable while `exit()` in the
>   same position is **not** — the two must not be conflated.
> - **The two-mode boundary holds for the compile side.** D-039's compile-time half —
>   the checker collects **all** compile errors before execution — is untouched. This
>   increment is runtime only.
>
> **Sequencing note.** This is Increment D: A → B → C → **D (runtime-throw
> catchability + uncatchable exit)** → E (close). Do not pull the smoke script or the
> benchmark forward. This increment routes existing sites and confirms the exit
> contract; it builds no new language surface.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the site-by-site
   routing (arithmetic, nil, index, stack-depth), preserving each unhandled
   diagnostic, the D-039 clarification and any prose fix it needs, the `exit()`
   uncatchability verification and the tests — and wait for Chris's approval before
   editing. The plan is the gate. List every site you will touch; this is a
   wide-but-shallow increment and the touched-site list is the review surface.
2. On approval, run `/start-branch` and propose `feat/runtime-throws`. Wait for Chris
   to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first (each routed
   site gets a catch-it test that fails today because the site halts). Follow the
   `tdd-cycle` skill.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–6** as summarised in the Increment A prompt.
- **Sprint 7 A–C:** the exception hierarchy and `throw`; `try`/`catch` with the
  handler table and matching; `finally` with the D-332 compilation model and the
  `exit()`-skips-finally behaviour. The existing runtime error sites still halt the
  VM directly — this increment converts them. All green.

## Deliverable for this increment

Every existing runtime error site catchable through `try`/`catch`, the D-039 contract
clarified, and `exit()` confirmed uncatchable across the surface.

1. **Routed sites.** Arithmetic (overflow, int/float div/0 and mod/0, math domain),
   nil dereference, array/substring index, and stack-depth-exceeded each raise their
   mapped leaf through the A/B/C machinery — catchable, with `finally` running around
   them, and with the **same** unhandled top-level diagnostic as today.
2. **The D-039 clarification.** The two-mode contract reads "first **unhandled**
   runtime error"; any misleading prose corrected mechanically, surfaced not swept.
3. **`exit()` uncatchability.** Verified across catch, catch-all, `catch (e:
   GrobError)` and `finally` — `ExitSignal` matches nothing, runs no finally, exits
   with the code.
4. **No new surface.** No new opcode, no new error code, no parser/AST edit — this
   increment is runtime routing and a spec clarification only. Confirm the count is
   unchanged.

## Out of scope

The stdlib throw sites (`IoError`, `NetworkError`, `JsonError`, `ProcessError`,
`ParseError`, `LookupError`) — those modules do not exist until Sprints 8–9 and their
throw sites arrive with them. The smoke script and the benchmark (Increment E). Any
new language surface. Do not edit the `OpCode` enum or add an error code.

## Tests

Per §3.5, route each test to the project matching its kind.

- **VM tests (`Grob.Vm.Tests`):**
  - Each routed site is catchable: `try { x / 0 } catch (e: ArithmeticError) { … }`
    catches and the script resumes; likewise a nil dereference → `NilError`, an
    out-of-range index → `IndexError`, and a deliberate stack overflow →
    `RuntimeError`.
  - A caught runtime error does **not** halt the VM — the script continues and a later
    statement runs (the D-039 "unhandled" clarification made observable).
  - An **unhandled** routed runtime error produces the **same** top-level diagnostic
    (message, `file:line`, exit 1) as before routing — a gold-master comparison
    against the pre-routing output where one exists.
  - `finally` runs around a routed runtime error exactly as around a user `throw`.
  - `exit()` inside a `try`/`catch`/`finally` is caught by nothing and runs no
    finally; a routed runtime error in the same position is caught — the two are not
    conflated.
  - The catch-all and `catch (e: GrobError)` catch a routed runtime error (it is a
    `GrobError` subtype) but not `exit()`.
- **Integration / spec-consistency:** the consistency gate (D-316) is green; the
  error-code count is unchanged; the D-039 clarification, if it touched prose, is
  reflected and the gate agrees.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Every existing runtime error site is catchable through `try`/`catch` and has
  `finally` run around it; an unhandled one produces the unchanged top-level
  diagnostic and exit 1.
- A caught runtime error resumes the script — the D-039 "first **unhandled**" reading
  holds.
- `exit()` is uncatchable across catch, catch-all, `catch (e: GrobError)` and
  `finally`, and runs no finally.
- No new opcode, no new error code, no parser/AST edit; the DAG holds; the count is
  unchanged.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. This is wide-but-shallow routing over the A/B/C
machinery already in place plus a mechanical spec clarification — every leaf mapping
is settled by §27's throw-site table and D-284. There is no Opus carve-out. If routing
a site surfaces a genuine structural question about the unwind, stop and surface it
rather than reaching for Opus.

## Hand-off

Summarise: the sites routed and the leaf each maps to, how the unhandled diagnostic is
preserved, the D-039 clarification and any prose corrected, the `exit()` uncatchability
verification, and the test files added. Note for the next chat: Increment E is the
sprint close — the `errors.grob` smoke script over the Sprint 1–7 surface, the §
acceptance and the fifth VM-execution benchmark baseline against the two-axis gate
(D-313).
