# Correctness batch — Increment 2: runtime error taxonomy (E5101 and E0004 misuse)

**Branch:** `fix/runtime-error-taxonomy`
**One concern:** two runtime conditions currently raise error codes documented as meaning
something else. Allocate correct codes and repoint the throw sites.

**This is the only finding in the correctness batch with a deadline.** ADR-0017 makes error
codes immutable once shipped. Both codes involved are still marked `pre-release` in
`grob-error-codes.md`. Fix the taxonomy while it is unfrozen, or v1 ships the ambiguity
permanently.

Runs against the fresh corpus zip carrying D-356 through D-381. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
or memory for D-### numbers or error codes. **The count is 119** — confirm.

---

## Authority and context

**Finding 1 — `E5101` used for allocation-ceiling breaches (D-366).** The registry documents
`E5101` as *"array index out of range"* (an `IndexError`, in the E51xx **bounds** sub-block
alongside `E5102 substring bounds out of range`). D-366's native-seam hardening used it for a
different condition entirely: a string operation whose **result would exceed an allocation
ceiling** — `repeat`, `padLeft`, `padRight` given a valid-but-enormous size. That is not an
index, not an array, and not a bounds violation. `--explain E5101` will tell someone who asked
for a fifty-million-character string that their array index was out of range.

**Finding 2 — `E0004` raised at runtime by the sort comparator (D-371).** `GrobValueComparer`
(`Grob.Vm/ArrayNatives.cs`) raises `E0004` when a sort key type does not implement `Comparable`
— but `E0004` is *"argument type mismatch"*, a **compile-time Type-category** code
(`E0001–E0999`). A compile-time code thrown at runtime is a category error, not merely an
imprecise label: `--explain E0004` describes a compile-time argument mismatch to someone whose
sort failed at runtime.

Both were correctly logged and deferred by the increments that found them.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Enumerate every throw site** for both conditions — the D-366 allocation guards
   (`repeat`/`padLeft`/`padRight`, and any sibling the D-366 audit guarded) and
   `GrobValueComparer`'s uncomparable-key throw. Report the exact list; D-366 audited a class,
   so there may be more sites than the three named here.
2. **Confirm the legitimate uses stay.** `E5101` must remain correct and in use for genuine
   array-index-out-of-range; `E0004` must remain correct and in use for genuine compile-time
   argument-type mismatches. This increment removes two **wrong** usages; it does not retire
   either code. Confirm both retain real call sites afterwards.
3. **Invoke `allocating-an-error-code` for both**, per the ladder — do not mint inline.
   Recommended placement, confirm or adjust:
   - **Allocation ceiling** → the next free code in the **E59xx general-runtime** sub-block,
     leaf `RuntimeError`. `E5901 call stack overflow` already lives there and is itself a
     resource limit, so a size-limit code sits naturally beside it. Message shape along the
     lines of *"result exceeds maximum size"*.
   - **Uncomparable sort key** → also E59xx, leaf `RuntimeError` — a runtime type failure with
     no better-fitting domain family (it is not arithmetic, bounds, nil, I/O, network, JSON,
     process, parse or env).

   Report the exact numbers taken from the **live registry tail** and confirm neither collides.
   The D-306 collision and the D-332→D-334 shift are the standing precedents for why this is
   read from the live file, never assumed.
4. **Registry entries.** `grob-error-codes.md` carries both a summary row and a per-code
   `### Exxxx — …` section. Both new codes need both, following the existing format including
   the `pre-release` stability marker. Report the format before writing.
5. **Check for asserting tests and fixtures.** Any gold master, `docs/errors/examples/` pair or
   integration test asserting `E5101` or `E0004` **for these two conditions** must be updated to
   the new codes. Tests asserting them for their legitimate conditions must be untouched.
   Enumerate both sets separately and report.
6. **Confirm the leaf is reachable and catchable.** Both conditions surface through
   `NativeFaultException` and the VM's catch (D-366's seam). Confirm the new codes' leaf makes
   them catchable from Grob source via `try`/`catch`, and that changing the leaf from
   `IndexError` to `RuntimeError` does not break an existing typed-catch fixture — a script
   catching `IndexError` to handle a too-large string would change behaviour. Report any such
   fixture; if one exists, that is a **user-visible behaviour change** to record, not to
   quietly absorb.

Report the throw-site enumeration, the ladder outcome and the two allocated codes, the registry
entries, the test-update lists, and the leaf-reachability confirmation. Then STOP.

---

## Scope boundaries — do NOT

- **Do not retire, renumber or repurpose `E5101` or `E0004`.** Both remain correct for their
  documented conditions. This increment removes two wrong usages.
- **Do not mint a code inline** — the `allocating-an-error-code` ladder governs both.
- **Do not write the long-form `docs/errors/Exxxx.md` documents** — those are a separate,
  scheduled pre-release session for the whole registry. Registry entries only here.
- **Do not fix the other batch findings** — `for...in` snapshotting (D-379's own increment),
  the `Synchronise()` double-diagnostic, or D-380's four new findings (the missing method-path
  nullable guard, permissive struct method calls, the missing `Error`-receiver cascade arm, and
  the `?.`-on-method-call runtime crash). Report anything new; fix none of them here.
- **No new opcode.**

---

## Tests — TDD, red first, same commit

- **Both conditions raise their new codes**, asserted as **catchable** `GrobError`s from Grob
  source via `try`/`catch` — not host exceptions: `"ab".repeat(<enormous>)`,
  `"x".padLeft(<enormous>)`, `"x".padRight(<enormous>)`, and sorting by a key that is an array,
  a map or a user struct.
- **The legitimate uses still raise the old codes** — a genuine out-of-range array index still
  raises `E5101`; a genuine compile-time argument-type mismatch still raises `E0004`. These are
  the regressions that a careless repoint breaks.
- **The registry is internally consistent**: both new codes appear in the summary table and as
  per-code sections, and the count is updated wherever it is stated.
- Every existing string, array, sort and native-seam test unchanged except the enumerated
  repoints.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-382**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: both taxonomy
  corrections and why each old code was wrong (a bounds code for a size limit; a compile-time
  Type code thrown at runtime); the two codes allocated, their numbers, leaves and the ladder
  outcome; that `E5101` and `E0004` are retained and still correct for their documented
  conditions; the full throw-site list repointed; any typed-catch behaviour change from the leaf
  change; the updated count (**119 → 121**, or as allocated); and that this closes the
  correctness batch's only ADR-0017-deadline item. Cite D-366, D-371, D-284 (the leaf
  hierarchy), ADR-0017 (code immutability), ADR-0014 (numbering).
- **Update `grob-error-codes.md`** — summary rows, per-code sections, and every stated total.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
