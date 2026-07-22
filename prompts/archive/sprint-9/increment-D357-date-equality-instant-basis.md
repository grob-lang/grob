# Sprint 9 — Increment: `date` equality on the instant (D-357)

**Branch:** `fix/date-equality-instant-basis`
**One concern:** make `date`-vs-`date` equality instant-based so trichotomy holds, and
consolidate the duplicated round-trip format constant the fix depends on.

Runs against the fresh corpus zip carrying D-356 through D-366. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust this
prompt or memory for D-### numbers, error codes or opcode values.

---

## Authority and context

- **D-357** is the ratified design: `date`-vs-`date` equality **and** ordering are both
  instant-based, matching .NET `DateTimeOffset` operator semantics (`EqualsExact`, the
  offset-sensitive variant, is deliberately **not** exposed). Every other struct pairing keeps
  D-169's field-by-field equality unchanged — this amendment is `date`-scoped. `guid` was
  reviewed under D-357 and deliberately left field-by-field: its canonical string *is* its
  identity, so no "same value, different representation" case exists.
- **Ordering is already correct — do not change it.** `OpCode.LessDate`/`GreaterDate`
  (`VirtualMachine.cs` ~644 and ~683, D-354) call `DateNatives.ToDateTimeOffset` and compare
  `DateTimeOffset` values, normalising across offsets. This increment **locks** that behaviour
  with tests; it does not modify it.
- **Equality is the defect.** `OpCode.Equal` (`VirtualMachine.cs` ~611) pushes generic
  `a == b` on `GrobValue`, which for `Struct` values is D-169 field-by-field — for `date` that
  compares the `__value` round-trip **strings**, which differ across offsets for the same
  instant. Result: for `a := date.now()` and `b := a.toUtc()`, `a < b` is false, `a > b` is
  false **and `a == b` is false**. A value neither less than, greater than, nor equal to another
  of the same instant violates trichotomy — the precise surprise the "language that doesn't
  surprise you" identity forbids.
- **The format constant is triplicated.** `"yyyy-MM-ddTHH:mm:sszzz"` is defined independently in
  `Grob.Vm/DateNatives.cs` (~34), `Grob.Core/NamedTypes/NamedTypeRegistry.cs` (~30) and
  `Grob.Stdlib/DatePlugin.cs` (~37), each commented as needing to match the others; the
  `NamedTypeRegistry` comment calls `DateNatives`'s copy "former", implying a C0c removal that
  never happened. Because the equality fix works by **parsing `__value` with that format**,
  three drifting definitions is a live hazard on the mechanism this increment depends on —
  consolidation is in scope as a dependency, not a drive-by.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Confirm the `Equal` defect empirically** where practical (the D-366 precedent: `dotnet run`
   against the built CLI) — `a := date.now()`, `b := a.toUtc()`, then `a == b`, `a < b`, `a > b`.
   Report the observed results before changing anything.
2. **The date-vs-date type-checker gate.** D-354 established a nominal `date`-vs-`date` check
   (`GetStructTypeName(...) == "date"` on both operands) to emit `LessDate`/`GreaterDate`.
   Confirm where it lives and that the same gate can drive equality-opcode selection. Confirm
   what `date == <non-date>` and `date == <other struct>` currently do (expected: existing
   E0002 or a structural false — report which; do not change it in this increment beyond what
   the new arm requires).
3. **`<=` and `>=` on dates.** There appear to be `LessDate`/`GreaterDate` but no
   `LessEqualDate`/`GreaterEqualDate`. Determine how `d1 <= d2` compiles today — a negation of
   the strict opcode, an unsupported construct, or a fall-through to a wrong generic. **Report
   it; if it is wrong, name it as a sibling finding rather than fixing it here** unless the fix
   is a direct consequence of the equality arm (one concern per branch).
4. **The opcode decision.** Recommended: a new `EqualDate` opcode, compiler-emitted when the
   checker sees the nominal date-vs-date pair, mirroring D-354's `LessDate` precedent exactly.
   Rationale: `OpCode.Equal` is the hottest path in the language — adding a struct-type-name
   check inside its handler taxes **every** `==` in every program to fix one type. Confirm this
   is right, and if so **follow the `adding-an-opcode` procedure and ADR-0013 opcode-stability
   governance in full**, including any `.grobc` format-version implication (D-298). Report
   whether `!=` needs a sibling `NotEqualDate` or is better served by `EqualDate` + `Not` —
   choose the smaller enum growth that keeps the disassembler honest.
5. **`daysUntil`/`daysSince` current basis.** D-357 pins them to whole 86,400-second periods
   between the two instants (`DateTimeOffset` subtraction → `TimeSpan.Days`), **not** calendar
   days in either operand's local offset. Report the current implementation and whether it
   already matches; Scripts 7 and 8 depend on this answer.
6. **The format-constant consolidation.** Confirm `Grob.Core` is referenced by both `Grob.Vm`
   and `Grob.Stdlib` (the solution architecture's only shared ground, no `Compiler`↔`Vm` edge),
   so one internal-but-shared definition in `Grob.Core` can serve all three call sites. Report
   the single home and the three sites to update.

Report the empirical confirmation, the opcode decision and its procedure compliance, the
type-checker gate reuse, the `daysUntil`/`daysSince` finding, the consolidation plan, any sibling
finding, and the test list. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

1. **Consolidate the round-trip format** to a single definition in `Grob.Core`; `Grob.Vm`'s
   `DateNatives` and `Grob.Stdlib`'s `DatePlugin` consume it. No behaviour change — the string is
   identical in all three today; this removes the drift hazard.
2. **Add `EqualDate`** (per the `adding-an-opcode` procedure), emitted by the compiler when the
   checker resolves a nominal date-vs-date equality. Its handler parses both `__value` strings via
   the shared helper and compares the resulting `DateTimeOffset` instants — `DateTimeOffset`'s own
   `==` already normalises across offsets, the same property `LessDate` relies on. `!=` per the
   gate's recommendation.
3. **Leave `Equal`'s generic path untouched** for every non-date pairing — D-169 unchanged, D-315's
   IEEE 754 semantics for floats unchanged, `guid` unchanged.
4. **Pin `daysUntil`/`daysSince`** to instant-difference whole days if they do not already match.

---

## Scope boundaries — do NOT

- **Do not change `LessDate`/`GreaterDate`** — already instant-based; lock with tests only.
- **Do not alter `guid` equality** — D-357 reviewed and deliberately kept it field-by-field.
- **Do not alter D-169 for any other struct** — user `type`s and other nominal types keep
  field-by-field.
- **Do not expose an `EqualsExact`/offset-sensitive comparison** — deliberately withheld by D-357.
- **Do not fix `<=`/`>=` on dates** if the gate finds them broken — report as a sibling finding
  and schedule separately.
- **Do not grow the `OpCode` enum without the `adding-an-opcode` procedure and ADR-0013
  compliance.** If that procedure's outcome is that no new opcode is acceptable, STOP and
  escalate rather than falling back to a runtime check in the generic `Equal` handler.
- **No new error code.** Count stays 118.

---

## Tests — TDD, red first, same commit

- **The trichotomy lock (the load-bearing test):** for `a := date.now()` and `b := a.toUtc()` —
  `a == b` is **true**, `a < b` is false, `a > b` is false, `a != b` is false. Repeat with
  `toZone()` at a third offset.
- Genuinely different instants still compare correctly: `<`, `>`, `==` false, `!=` true.
- Same instant constructed by different routes (parse of an offset string vs `toUtc`) is equal.
- Existing `date` ordering gold masters unchanged (the `LessDate`/`GreaterDate` lock).
- `guid` equality unchanged — an explicit test asserting `guid` still compares field-by-field, so
  the date carve-out cannot silently generalise.
- Other struct equality unchanged — a user `type` with equal fields still equal, D-169 intact.
- Float `==` unchanged — `NaN != NaN`, `+0.0 == -0.0` (D-315) still hold, proving the generic
  `Equal` path was not disturbed.
- `daysUntil`/`daysSince` across an offset boundary return the instant-based answer.
- Disassembler renders the new opcode(s); `.grobc` round-trip unaffected or versioned per ADR-0013.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog), D-###
  from the **live registry tail** — next free is **D-367**; confirm, do not assume. The entry
  records: D-357 implemented — `date` equality now instant-based, trichotomy restored; the
  empirical before/after confirmation; the new opcode(s), their values and `adding-an-opcode`/
  ADR-0013 compliance (and any `.grobc` version implication); ordering confirmed already correct
  and locked, not changed; the three-way round-trip format constant consolidated into `Grob.Core`
  (naming the C0c removal that had been missed); `daysUntil`/`daysSince` basis pinned; `guid` and
  all other structs explicitly unchanged under D-169; and any sibling finding (`<=`/`>=` on dates).
  No new error code, count 118. Cite D-357, D-354, D-169, D-315, D-361, D-284.
- **Update `grob-stdlib-reference.md`** — the `date` comparison section states the instant-basis
  explicitly for both equality and ordering, and pins `daysUntil`/`daysSince`, citing this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt under
  `prompts/archive/sprint-9/`.
