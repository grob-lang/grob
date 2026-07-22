# Sprint 9 — Increment: native-seam size/count hardening (`repeat` and its siblings)

**Branch:** `fix/native-seam-size-guards`
**One concern:** ensure a hostile or accidental size/count/width argument to a stdlib native
raises a proper `GrobError` through the native seam rather than escaping as a host .NET
exception. Fix `repeat` (the known instance) and sweep the class.

Runs against the fresh corpus zip carrying D-356 through D-365. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
or memory for D-### numbers or error codes.

---

## Authority and context

- **The known instance.** The D-365 PR review flagged that the pre-existing `string.repeat`
  native (shipped in D-363) carries the same integer-overflow-past-the-seam bug class that was
  fixed for `padLeft`/`padRight` during D-365. It was correctly left alone as out-of-scope for
  that branch. This increment closes it.
- **The class, not the instance.** `"ab".repeat(<huge>)` overflows the result-length computation
  or exhausts allocation, surfacing an `OverflowException`/`OutOfMemoryException` — a **host**
  exception, not a `GrobError`. That breaches the "fails well" contract: the user sees a .NET
  stack trace instead of a Grob diagnostic, and `grob-adversarial-testing-strategy.md`'s Pillar 1
  fuzzer will generate exactly this input. Any native taking a count, width, size or length and
  allocating or multiplying from it is a candidate for the same failure.
- **The seam already exists.** `NativeFaultException(leaf, code, message)` is the established
  mechanism — caught in `VirtualMachine.cs` (~line 973) and already used by `EnvPlugin`,
  `GuidPlugin`, `MathPlugin`, `DatePlugin` and `IoPlugin`. `DatePlugin` already wraps its
  component arithmetic in `checked(...)` for this exact reason. This increment applies an
  established pattern to the natives that missed it; it invents nothing.
- **The ten `GrobError` leaves** (D-284, `ExceptionHierarchy.cs`): `IoError`, `NetworkError`,
  `JsonError`, `ProcessError`, `NilError`, `ArithmeticError`, `IndexError`, `ParseError`,
  `LookupError`, `RuntimeError`.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Read the `padLeft`/`padRight` fix from D-365** — the reference implementation. Report exactly
   what it guards (overflow in the width computation, an allocation ceiling, an explicit clamp, or
   a combination) and what it does on breach (clamp silently, or fault). **The sweep must apply the
   same treatment**, so this is the shape to mirror rather than a fresh design.
2. **Enumerate the candidate natives** across `Grob.Stdlib` — every native taking a count, width,
   size, length or repetition argument, or otherwise allocating proportionally to a user-supplied
   integer. Start from `string.repeat`, `string.padLeft`/`padRight` (already fixed — confirm),
   `string.substring`/`left`/`right` (already throw `IndexError` per D-363 — confirm they are
   genuinely safe, including for `int.MaxValue` and negative inputs), then widen to array, `formatAs`
   and any other plugin natives fitting the shape. Report the full candidate list and which are
   already safe.
3. **For each unsafe candidate, determine the failure mode concretely** — does it overflow the
   length computation (`count * length`), or allocate successfully-but-enormously, or both? These
   want different guards: overflow is a `checked(...)` arithmetic breach; a valid-but-enormous
   allocation needs an explicit ceiling.
4. **Choose the fault leaf and code.** For an overflow in a size computation, `ArithmeticError`
   matches Grob's own checked-arithmetic convention (an `int` overflow throws `ArithmeticError`
   language-wide) — confirm the existing code the VM's checked arithmetic raises and **reuse it**.
   If a valid-but-too-large allocation genuinely has no fitting existing code, **STOP and escalate
   via the `allocating-an-error-code` ladder** rather than minting inline. Report the proposed
   leaf/code per candidate before writing anything.
5. **Confirm the guard sits on the correct side of the seam** — inside the native, raising
   `NativeFaultException`, so the VM's existing catch converts it to a catchable `GrobError`. A
   guard that lets the host exception escape to the VM's generic handler is not a fix.

Report the reference fix's shape, the candidate list with safe/unsafe status, the per-candidate
failure mode, the proposed leaf/code reuse, and the test list. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

Mirror the D-365 `padLeft`/`padRight` treatment onto every unsafe candidate found:

- Wrap the size computation in `checked(...)` so an overflow is caught rather than wrapping
  silently to a nonsense length.
- Convert the caught host condition into a `NativeFaultException` with the reused leaf and code, so
  it surfaces as a catchable `GrobError` with a Grob diagnostic.
- Preserve every existing correct behaviour — negative and zero counts keep whatever semantics
  D-365 and D-363 pinned (`repeat(0)` → empty string, per `grob-type-registry.md`; confirm and do
  not change it).

---

## Scope boundaries — do NOT

- **Do not redesign the seam.** `NativeFaultException` + VM catch is the mechanism; use it.
- **Do not change any documented success-path semantics.** This increment changes what happens on
  *hostile* input only. Every existing passing test stays passing.
- **Do not mint a new error code inline.** Reuse; escalate through the ladder if genuinely needed.
- **Do not widen into non-size argument validation** (nil handling, type coercion, encoding) — a
  different concern.
- **Do not touch `tests/CLAUDE.md`** — its naming-convention prose correction is queued for the
  pending corpus doc sweep, not this branch.
- **No new opcode.**

---

## Tests — TDD, red first, same commit

- `"ab".repeat(<value that overflows the length computation>)` raises a catchable `GrobError` of
  the chosen leaf — **not** a host exception, and **not** a process crash. Assert it is catchable
  from Grob source via `try`/`catch`.
- The same for every other candidate the sweep fixes.
- Boundary cases per candidate: `0`, `1`, negative, `int.MaxValue`.
- Existing success-path behaviour unchanged: `"ab".repeat(3)` → `"ababab"`, `repeat(0)` → `""`,
  and the full pre-existing `string` member suite green.
- A test asserting the already-safe natives (`substring`/`left`/`right`) remain safe at
  `int.MaxValue` and negative inputs — locking the audit's findings so a future change cannot
  silently regress them.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog), D-###
  from the **live registry tail** — next free is **D-366**; confirm, do not assume. The entry
  records: the native-seam size/count guard class closed; the full candidate list audited with
  safe/unsafe status (so the audit's negative results are recorded, not just the fixes); the guard
  shape mirrored from D-365's `padLeft`/`padRight` treatment; the leaf and code reused per
  candidate; that hostile size input now raises a catchable `GrobError` rather than a host
  exception, satisfying the "fails well" contract ahead of Pillar 1 fuzzing; and any candidate
  deliberately left unguarded with its reason. No new opcode; error codes reused (state the count
  — 118 unless the ladder was invoked and approved). Cite D-363, D-365, D-284 (the leaf
  hierarchy), D-353 (the adversarial contract this satisfies).
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt under
  `prompts/archive/sprint-9/`.
