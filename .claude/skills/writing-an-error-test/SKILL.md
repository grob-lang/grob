---
name: 'Writing an Error Test'
description: 'How to add or update a gold-master error-example pair (<slug>/<slug>.grob + <slug>.expected.txt) and register the error code, without breaking the negative-test release gate.'
---

# Writing an Error Test

Grob's diagnostics are part of the product. The error-examples library is the negative-
test release gate: each case is a directory `docs/errors/examples/<slug>/` holding a
pair — `<slug>.grob`, a source that should fail, and `<slug>.expected.txt`, the
hand-written gold master stating the diagnostic it produces. The harness in
`Grob.Integration.Tests` checks every pair (D-428). This skill covers adding a case and,
crucially, the rule for when a master may change.

## When you reach for this

- A new compile-time or runtime error needs coverage.
- A diagnostic's wording, suggestion or format changed and the affected gold masters
  must be regenerated.
- A new error code is being introduced.

## Step 1 — The error code

Every diagnostic maps to a code in `docs/design/grob-error-codes.md` (`Exxxx`). This
skill owns the gold-master pair, not the code allocation — for the code itself, defer to
`allocating-an-error-code`, which is the single authoritative procedure.

- **Reuse** an existing code if the failure is an instance of it (the semantic-match
  test in that skill — member access versus construction site, not surface similarity).
- **Register a new code** only through the `allocating-an-error-code` ladder: confirm
  nothing fits, surface the fold-versus-new judgement, get the `D-###`, then register in
  the correct category range (Type `E0xxx`, Name resolution `E1xxx`, Syntax `E2xxx`,
  Module `E3xxx`, Param/decorator `E4xxx`, Runtime `E5xxx`) at the next free number from
  the live registry, in three-location lockstep with the `ErrorCatalog` descriptor. Codes
  are **immutable once shipped** (ADR-0017) — the judgement is the slow part, the
  mechanics are deterministic.

Never emit a diagnostic with no code.

## Step 2 — The source fixture (`<slug>.grob`)

Write the smallest `.grob` source that triggers exactly this error and nothing else. It
follows the Grob language rules — Windows paths in backticks, `:=` bindings, the v1
surface. A fixture that triggers three errors tests none of them cleanly. Its first line
is a single `//` comment naming the code and the intent. Name the directory and both
files for the failure, in kebab case: `nullable-interpolation/nullable-interpolation.grob`,
not `test3`.

## Step 3 — Write the expected output (`<slug>.expected.txt`)

Write the master by hand, as a specification against the registry and
`grob-v1-requirements.md` §10, in the format and voice `docs/errors/examples/README.md`
fixes: the title is `ErrorCatalog`'s, the `-->` line names the bare file with a line and
a column, each snippet line equals the source line it numbers and the first caret starts
at the `-->` column. Until the §10 renderer lands (R-25) nothing emits this format, so
there is nothing to regenerate from. Run `grob run <slug>.grob` from the pair's directory
to confirm the CLI reports the code and location the master states; if it does not, the
pair goes in `docs/errors/examples/quarantine.md` with what it produces today.

The expected output must meet the quality bar: clear statement of what went wrong, exact
location, and a suggested fix where the fix is obvious. No emoji; prose follows
`house-style/SKILL.md`. If
the generated diagnostic is poor, the fix is to improve the diagnostic in the compiler,
then regenerate — not to bless poor output as the gold master.

## Step 4 — The regeneration discipline (the part that bites)

A master's code or location may change on three grounds only (D-428 Decision 4): **a
decision changed the language under it** (cite the decision); **it contradicts its own
source by character count**; or **a decision resolving a register divergence finds the
master's side wrong**. Otherwise the implementation is what is wrong, and the pair is
quarantined rather than the master edited to make the harness pass.

1. Grep the `.expected.txt` files for the affected code or wording to find the full set.
2. Change each on one of the three grounds, or quarantine it.
3. Read the diff deliberately. A changed location or an altered suggestion you did not
   expect is a signal the implementation regressed, not that the gold master is wrong.
4. Commit the masters with the change that caused them, citing the `D-###`.

After R-25 a master may be regenerated from the implementation only in the commit that
deliberately changes the diagnostic it pins, with the diff reviewed. Bulk regeneration
never.

## Step 5 — The index and the gate

There is nothing to wire: the harness discovers pairs by directory, so a new pair is in
the gate the moment it exists. Add its code and slug to the index in
`docs/errors/examples/README.md`; the static layer fails on an unindexed pair. The
release gate is every pair passing and the quarantine manifest empty; a drifting master
is a release-gate fail, the same status as a failing positive script.

## Checklist

- [ ] Error code reused or newly registered in the correct category range
- [ ] `<slug>.grob` triggers exactly one error, minimal, idiomatic, named for the error
- [ ] `<slug>.expected.txt` hand-written to the registry, §10 and the README's voice
- [ ] Diagnostic meets the quality bar (location, cause, fix) and the `house-style` checklist
- [ ] On a change: full set of affected masters found by grep, each changed on one of
      D-428's three grounds or quarantined
- [ ] Diff reviewed deliberately — every change intended
- [ ] Pair indexed in the README; quarantined in `quarantine.md` if it does not yet match
