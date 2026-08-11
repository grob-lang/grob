# Correctness batch — Increment: side-channel type-resolution symmetry

**Branch:** `fix/side-channel-resolution-symmetry`
**One concern:** close the remaining gaps where type information travels beside the main
`GrobType` tag and is lost — `?.` property access returning `Unknown`, and the two
expression-walking helpers still missing their `NilCoalesce` arm — and decide whether the class
can be enforced mechanically instead of found one instance at a time.

Runs against the fresh corpus zip carrying D-356 through D-402. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority — three findings from D-402, all reported and deliberately not fixed there

**Finding 1 — `?.` property access types as `Unknown`, and the call path now outperforms it.**
`VisitMemberAccess`'s `?.` arm returns `Unknown` **unconditionally** rather than the
nullable-widened field type. D-402 found this is an **undocumented deferral** predating that
branch — a test comment (`TypeCheckerFieldAccessTests.FieldAccess_OptionalChainOnNullableScalar_NoError`)
calls it "the F3 guard", and **no `D-###` records it**. D-402's own fix to the call path has
therefore created an inversion:

| Expression | Types as |
|---|---|
| `xs?.first()` | `T?` — correct, since D-402 |
| `xs?.length` | `Unknown` — the simpler, more common operation, now the degraded one |

This is also a permissive-`Unknown` source that **D-362's catalogue never enumerated**, because
nobody knew it existed.

**Findings 2 and 3 — two more helpers missing the `BinaryExpr`/`NilCoalesce` arm.**
`ExpressionDescriptor` (`TypeChecker.cs` — the function-descriptor retrieval; note the *data*
type lives in `FunctionTypeDescriptor.cs` but the *retrieval logic* does not) and
`GetStructTypeName` (`TypeChecker.Expressions.cs` — the named-type-identity side channel
`ResolveMemberAccessCall`'s own struct arm consults). So `(f ?? g)()` on a nullable function
value and `(g1 ?? g2).toString()` on a nullable `guid` both lose their descriptor or name through
`??` and fall back to `Unknown`.

---

## The pattern, which matters more than the three instances

**Four helpers have now been found with the identical gap, across three increments:**
`MapDescriptorOf` (D-401), `ArrayDescriptorOf` (D-402), and these two.

Four instances of one shape is no longer a set of bugs — it is an **unenforced invariant**:
*every helper that walks an expression to recover side-channel type information must handle every
expression form that can produce a value*, and nothing checks that it does. Each instance has been
found by accident, while chasing something else, and fixed as a one-off.

**Part of this increment's job is to ask whether the class can be mechanised.** Options to
evaluate — recommend, do not assume:

- A **shared walker** the side-channel helpers delegate to for the structural forms
  (`??`, parenthesised, conditional, switch expression, block-lambda result — whatever the survey
  in gate step 2 finds), each keeping only its own leaf logic.
- An **exhaustiveness test** asserting every side-channel helper handles the same node-kind set,
  so a new helper or a new expression form fails CI rather than silently degrading to `Unknown`.
- A documented **convention plus checklist** in `CLAUDE.md` or the fundamentals, if the code-level
  options prove disproportionate.

**If mechanising is disproportionate, say so and record why** — a reasoned "no" with the four
instances documented is a legitimate outcome, and better than a mechanism nobody maintains. But
the fifth instance turning up inside `json` or `csv` and being fixed as another one-off is the
outcome to avoid.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce all three empirically** (the standing precedent — build the CLI and run them):
   `xs?.length`'s resolved type; `(f ?? g)()` on a nullable function; `(g1 ?? g2).toString()` on a
   nullable `guid`. Report actual behaviour before designing.
2. **Survey the side-channel helpers.** Enumerate **every** helper that walks an expression to
   recover information not carried by the flat `GrobType` tag — the four known plus any others —
   and, for each, which expression node kinds it handles. **The gaps are the cells this table is
   missing**, and it is the evidence base for the mechanisation question. Report it in full.
3. **Finding 1's scope — this is the one that needs a decision, not just a fix.** Report why
   `VisitMemberAccess`'s `?.` arm returns `Unknown`: is it a genuine deferral (something about
   field access made widening hard), or an oversight? Read "the F3 guard" test and its comment,
   and report what F3 refers to. **If a real obstacle exists, report it rather than forcing the
   fix** — an undocumented deferral becoming a *documented* one is still progress.
4. **The widening mechanism** D-402 used for the call path (`ToNullable`) — confirm it applies to
   field access, and that a `?.` field read on a nullable struct yields the field's nullable form.
5. **THE BREAKING-CHANGE ENUMERATION — required before any edit.** Typing `xs?.length` as `int?`
   where it was `Unknown` **rejects programs that currently compile**, since `Unknown` is
   permissive and `int?` is not. Enumerate every test, fixture, gold master and validation script
   affected, and report the list. Updated to assert new correct behaviour, **never weakened**. If
   the fallout is wider than a handful, STOP and report.
6. **D-362's permissive-`Unknown` catalogue** — report its current count and membership, and what
   this increment changes it to. Finding 1 is a source it never listed.
7. **Confirm no runtime change is needed.** All three are type-checker resolution; `?.`'s emission
   (D-400) is correct and untouched.

Report the empirical reproduction, the helper survey table, finding 1's verdict, the widening
confirmation, the breaking-change list, the catalogue update, and the mechanisation
recommendation. Then STOP.

---

## Scope boundaries — do NOT

- **Do not fix `ResolveNilCoalesce`'s missing `ArrayElementAssignable` guard** (D-402's finding 1
  — `int[]? ?? string[]` compiling when it should not). That is a **validation** gap, not a
  resolution one, and D-402 records it as larger than first framed. Its own increment.
- **Do not change `?.`'s runtime emission** (D-400) or `ResolveMemberAccessCall`'s guard (D-402) —
  both correct.
- **Do not build the mechanisation if the gate recommends against it** — record the reasoning.
- **Do not weaken or delete a test** to absorb the breaking change.
- **Do not fix the remaining correctness-batch findings** — `Synchronise()`'s double diagnostic
  (D-376), `E5102`'s missing throw site (D-382), D-380's diagnostic-quality gaps.
- **No new error code** — existing nullable diagnostics apply. Count stays **121**. **No new
  opcode.**

---

## Tests — TDD, red first

- **Finding 1 — load-bearing:** `xs?.length` types as `int?`; a `?.` field read on a nullable
  struct types as the field's nullable form; both are consumable via `??` and a nil guard, and
  raise the existing nullable diagnostic when used unguarded. **Assert the property and call
  paths agree** — `xs?.length` and `xs?.first()` should no longer disagree about what `?.`
  produces, and a test naming that symmetry directly is what stops it re-diverging.
- **Findings 2 and 3:** `(f ?? g)()` resolves the function's return type, not `Unknown`;
  `(g1 ?? g2).toString()` resolves through the named-type path. Nested `??` works, since the arm
  recurses on both operands.
- **The symmetry made visible:** one test file or region asserting all four helpers resolve
  through `??`, so the next missing arm fails a test rather than degrading silently.
- **If an exhaustiveness mechanism is built**, it must be **mutation-verified**: remove one arm
  from one helper and confirm the mechanism fails. The D-391 review found a test that stayed green
  when the thing it guarded was deleted — do not repeat that.
- **`?.` runtime behaviour unchanged** (D-400's tests stay green); non-nullable receivers
  unaffected.
- Each updated test carries its new assertion, visible in the diff.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-403**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: **finding 1's verdict** —
  fixed, or documented as a deferral with the obstacle named, closing the fact that it had no
  `D-###` at all; the property/call asymmetry D-402 created, now resolved or explained; the two
  helper arms added; **the full side-channel helper survey table**, as the durable record of the
  class; **the mechanisation decision** — built, or reasoned against, with the four instances
  documented either way; the breaking-change list with what each updated test now asserts;
  D-362's permissive-`Unknown` catalogue updated; and that `ResolveNilCoalesce`'s validation gap
  remains open as its own item. No new opcode, no new error code, count 121. Cite D-402, D-401,
  D-400, D-362, D-374, and the fundamentals' nullable rules.
- **Update `grob-language-fundamentals.md`** if its `?.` section is silent on the resolved type of
  an optional-chained property access. If the spec was already correct and only the
  implementation lagged, say so and change nothing.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
