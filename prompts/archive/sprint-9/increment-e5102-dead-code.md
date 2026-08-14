# Correctness batch — Increment: `E5102` has no throw site

**Branch:** `fix/e5102-dead-code`
**One concern:** `E5102` ("substring bounds out of range") is defined in the registry and in
`ErrorCatalog` but is thrown by nothing. Decide whether it earns its existence, then make the
code and the registry agree.

Runs against the fresh corpus zip carrying D-356 through D-406. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority, and the deadline

**D-382** found this while repointing two wrong-category codes, and deliberately left it:

> `E5102` ("substring bounds out of range") is defined in `ErrorCatalog` and the registry but has
> no throw site: `Substring`/`Left`/`Right` all reuse `E5101` instead. Out of scope here (not one
> of the two named findings); noted for a future correctness pass rather than folded into this one.

**Both codes are still marked `pre-release`** in `grob-error-codes.md` (rows 149–150), which is
the window this increment exists to use. **ADR-0017 makes error codes immutable once shipped** —
so `E5102` either gets its throw sites now, or v1 ships a permanently dead entry in the registry
that `--explain E5102` will describe for a condition nothing raises.

**This also corrects a claim I made.** The advertised-vs-built audit reported the error registry
as clean with "no dead codes". That check diffed documented codes against codes **referenced** in
`src/` — and `E5102` *is* referenced, as an `ErrorCatalog` definition. Referenced-as-defined is
not the same as actually-thrown, and the audit did not distinguish them. Treat that "clean"
result as unverified for this class.

---

## The decision this increment must make, not assume

There are two defensible outcomes, and the entry must argue for the one it takes:

- **Give `E5102` its throw sites.** `Substring`/`Left`/`Right` are string-bounds violations, not
  array-index violations. A distinct code lets `--explain E5102` describe the actual condition,
  and lets a script `catch` and distinguish them. This is presumably why the registry has two
  rows.
- **Retire `E5102` and let `E5101` cover both.** If the leaf is the same (`IndexError`) and no
  user could act differently on the two, a second code is registry weight without value. **This
  is legitimate** — a code that exists only because someone once wrote a row for it is not a
  reason to keep it.

**Weigh the diagnostic-quality argument honestly.** `E5101` reads "array index out of range". A
user who wrote `"hello".substring(2, 99)` and gets told their *array index* is out of range is
being handed a wrong description of their mistake — the same class of defect D-382 fixed for the
allocation-ceiling sites, where `E5101` was describing a size limit as an index violation.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce empirically first** (the standing precedent): run `"hello".substring(2, 99)`,
   `"hi".left(10)`, `"hi".right(10)`, and negative-argument variants through the CLI. Report the
   exact code, leaf, message and whether each is catchable from Grob source.
2. **Enumerate every string-bounds throw site** — `Substring`, `Left`, `Right`, and any other
   member with bounds semantics (`padLeft`/`padRight`/`truncate`/`repeat` have their own D-382
   `E5905` ceiling guard — confirm which faults are bounds and which are size limits, since the
   two were already conflated once).
3. **Confirm `E5102` genuinely has no throw site** across the whole tree, and report what it *is*
   referenced by — `ErrorCatalog` definition, registry row, long-form section, tests, anything.
   The audit's failure was conflating definition with use; do the check that distinguishes them.
4. **Read `E5101`'s and `E5102`'s registry rows and long-form sections**, and report whether the
   registry describes a distinction the code never implemented, or whether the two rows have
   always overlapped.
5. **The leaf question.** Both are presumably `IndexError`. Report whether a script could
   meaningfully `catch` them differently — if not, that is evidence for retirement; if the leaf
   differs or could, that is evidence for keeping both.
6. **Retirement mechanics, if that is the recommendation.** `E5102` is `pre-release`, so it has
   never shipped — report whether ADR-0017's immutability applies to a code in that state, and
   what removing a row does to the count, to `ErrorCatalog`, to any consistency test (D-316), and
   to the numbering (does `E5103` exist, leaving a hole?).
7. **Gold masters and examples.** Report every file in `docs/errors/examples/` and every test
   asserting `E5101` for a **string**-bounds condition — those change if `E5102` is adopted, and
   each must be classified as a legitimate behaviour change rather than a test-passing edit.

Report the reproductions, the throw-site enumeration, the definition-versus-use finding, the
leaf analysis, the recommendation **with its argument**, and the affected-test list. Then STOP.

---

## Scope boundaries — do NOT

- **Do not decide by symmetry.** "The registry has two rows so it must need two codes" is not an
  argument. Either outcome must be argued from what a user can observe and act on.
- **Do not change `E5101`'s meaning** or its array-index throw sites.
- **Do not touch D-382's `E5905`/`E5906`** — the allocation-ceiling and sort-comparator codes are
  settled and correct.
- **Do not mint a new code.** This resolves an existing one either way. If the gate finds a third
  condition needing its own code, STOP and escalate via `allocating-an-error-code`.
- **Do not fix D-380's three diagnostic-quality gaps** — the last remaining correctness-batch
  item, its own increment.
- **No new opcode.**

---

## Tests — TDD, red first

**If `E5102` is adopted:**

- Each string-bounds violation raises **`E5102`**, catchable from Grob source via `try`/`catch`,
  with a message describing a *string* bounds violation — asserted with the full diagnostic
  contract (code, message, position) per the project's convention.
- **Array index violations still raise `E5101`** — the regression a careless repoint breaks.
- Boundary cases per member: zero-length, exact-length, one-past-the-end, negative arguments.
- Every affected gold master regenerated and reviewed.

**If `E5102` is retired:**

- The registry, `ErrorCatalog` and the count agree, and the D-316 consistency gate passes.
- A test or mechanism proving **no dead code remains** — the check the advertised-vs-built audit
  should have done: every registered code is *thrown* somewhere, not merely defined. **If that is
  cheap to assert generally, it is worth more than this one fix**, since it closes the class
  rather than the instance.
- String-bounds violations still raise `E5101` and remain catchable.

**Either way:** every existing string, array and error-registry test green unless enumerated.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-407**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the empirical
  reproduction; **the decision and its argument** — adopted or retired, and why, in terms of what
  a user can observe and act on; the throw sites changed or the row removed; the resulting count;
  whether a general no-dead-code check was added and, if so, that it is mutation-verified (delete
  a throw site, confirm it fails); the gold masters regenerated; and that this closes D-382's
  reported finding. Note explicitly that the advertised-vs-built audit's "no dead codes" result
  was **definition-based, not use-based**, so that limitation is on record. Cite D-382 (the
  finding), ADR-0017 (immutability, and why `pre-release` is the window), ADR-0014 (numbering),
  D-284 (the leaf hierarchy), D-316 (the consistency gate).
- **Update `grob-error-codes.md`** — the row, the long-form section and every stated total.
- **Deliverable:** repo-pathed zip (source, tests, gold masters if any, updated design docs).
  Archive this prompt under `prompts/archive/sprint-9/`.
