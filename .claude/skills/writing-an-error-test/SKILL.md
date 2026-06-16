---
name: 'Writing an Error Test'
description: 'How to add or update a gold-master error-example pair (_grob.txt + _expected.txt) and register the error code, without breaking the negative-test release gate.'
---

# Writing an Error Test

Grob's diagnostics are part of the product. The error-examples library is the negative-
test release gate: each case is a pair — a `*_grob.txt` source file that should fail,
and a `*_expected.txt` gold-master holding the exact stderr diagnostic. This skill
covers adding a case and, crucially, the discipline around regenerating expected output
when a message changes.

## When you reach for this

- A new compile-time or runtime error needs coverage.
- A diagnostic's wording, suggestion or format changed and the affected gold masters
  must be regenerated.
- A new error code is being introduced.

## Step 1 — The error code

Every diagnostic maps to a code in `docs/design/grob-error-codes.md` (`Exxxx`). Check
whether a suitable code exists.

- **Reuse** an existing code if the failure is an instance of it.
- **Register a new code** only if the failure is genuinely new. Place it in the correct
  category range (Type `E0xxx`, Name resolution `E1xxx`, Syntax `E2xxx`, Module
  `E3xxx`, Param/decorator `E4xxx`, Runtime `E5xxx`). Add the registry entry: code,
  title, category, status, description, source decision. Codes are **immutable once
  shipped** (ADR-0017) — choose carefully.

Never emit a diagnostic with no code.

## Step 2 — The source fixture (`*_grob.txt`)

Write the smallest `.grob` source that triggers exactly this error and nothing else. It
follows the Grob language rules — Windows paths in backticks, `:=` bindings, the v1
surface. A fixture that triggers three errors tests none of them cleanly. Name it for
the error: `nullable_interpolation_grob.txt`, not `test3_grob.txt`.

## Step 3 — Generate the expected output (`*_expected.txt`)

Run the source through the compiler/VM and capture stderr verbatim. The gold master is
byte-for-byte: file, line, column, the message, and the suggestion when there is one.
Do not hand-write it from memory — generate it from the actual implementation, then
read it to confirm it is the diagnostic you intend.

The expected output must meet the quality bar: clear statement of what went wrong, exact
location, and a suggested fix where the fix is obvious. No emoji. British English. If
the generated diagnostic is poor, the fix is to improve the diagnostic in the compiler,
then regenerate — not to bless poor output as the gold master.

## Step 4 — The regeneration discipline (the part that bites)

When a diagnostic's wording changes, **every** gold master that includes that wording is
now stale. Do not hand-edit individual `_expected.txt` files to make tests pass — that
hides which cases actually changed and erodes the gold master's value.

1. Grep the expected files for the affected message or code to find the full set.
2. Regenerate each from the current implementation.
3. Read the diff deliberately. Confirm every change is intended — a changed location or
   an altered suggestion you did not expect is a signal the implementation regressed,
   not that the gold master is wrong.
4. Commit the regenerated masters with the change that caused them, referencing the
   `D-###` if a decision drove the wording.

## Step 5 — Wire it into the gate

Make sure the new pair is picked up by the negative-test suite (the
`errors-examples-README.md` convention). The release gate requires the gold masters to
match; a drifting master is a release-gate fail, the same status as a failing positive
script.

## Checklist

- [ ] Error code reused or newly registered in the correct category range
- [ ] `*_grob.txt` triggers exactly one error, minimal, idiomatic, named for the error
- [ ] `*_expected.txt` generated from the implementation, not hand-written
- [ ] Diagnostic meets the quality bar (location, cause, fix, no emoji, British English)
- [ ] On a wording change: full set of affected masters found by grep and regenerated
- [ ] Diff reviewed deliberately — every change intended
- [ ] Pair wired into the negative-test gate
