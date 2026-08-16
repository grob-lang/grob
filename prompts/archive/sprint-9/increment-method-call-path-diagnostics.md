# Correctness batch — Increment: the method-call path's remaining diagnostic gaps

**Branch:** `fix/method-call-path-diagnostics`
**One concern:** bring `ResolveMemberAccessCall`'s diagnostic behaviour into line with
`VisitMemberAccess`'s. **The last item in the correctness batch.**

Runs against the fresh corpus zip carrying D-356 through D-407. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — confirm.

---

## Authority — three findings from D-380, recorded eight increments ago

D-380 recorded these under its "identified but not fixed" list, verbatim:

1. **No generic nullable guard on the call path** — a non-optional `.` on a nullable
   array/primitive/ordinary-struct receiver did not raise `E0101`, unlike the property path,
   though a *registered named type's* nullable receiver already did via a narrower pre-existing
   special case.
2. **Ordinary user `type` struct method calls are permissive regardless of name** — v1 gives
   structs no method surface at all, yet the property path's `ResolveStructFieldAccess` already
   raises `E1002` for an unrecognised field. `AnonStruct`/`NullableAnonStruct` are the same.
3. **No `Error`-receiver cascade-suppression arm on the call path** (unlike
   `VisitMemberAccess:1294`), so a call on an already-errored receiver resolves `Unknown` rather
   than `Error`.

**All three are the same structural gap**: `ResolveMemberAccessCall` lacks arms its property-path
counterpart has. Treat them as one concern with three symptoms unless the gate proves otherwise.

**Finding 1 is probably already closed — verify, do not assume.** D-402 added a *generic*
nullable-receiver guard to `ResolveMemberAccessCall` raising `E0101` for array, map, string, int,
float, bool and named-struct receivers. That is finding 1's subject, arriving from a different
direction. **Establish empirically what remains** — possibly nothing, possibly only the ordinary
`Struct`/`AnonStruct` receivers D-402's list does not obviously name.

**Finding 3 matters more than its size suggests.** D-300's cascade suppression — one mistake, one
diagnostic — is the mechanism D-404 and D-406 both relied on, and D-406 has just spent an
increment restoring one-mistake-one-diagnostic across six parser constructs. A missing `Error`
arm on the call path means the same failure persists in the type checker: an errored receiver
produces `Unknown`, which is permissive, so the mistake propagates rather than being absorbed.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Reproduce all three empirically first** (the standing precedent — build the CLI and run
   them), and report exact codes, messages and positions:
   - a non-optional `.` method call on a nullable **ordinary struct** and a nullable
     **anon-struct** receiver (the cases D-402's guard may not cover);
   - `someStruct.anyMethodName()` on a user `type` value, and the anon-struct equivalent;
   - a method call on a receiver that already produced an error — e.g. an undefined identifier,
     or an expression whose own diagnostic fired — and report **how many diagnostics** result.
2. **Diff `ResolveMemberAccessCall` against `VisitMemberAccess` arm by arm.** Report every arm the
   property path has that the call path lacks, and vice versa. That table is the increment's scope
   and the durable record — the same shape as D-403's side-channel helper survey, and for the same
   reason: this is the third time a gap between these two paths has been found one instance at a
   time.
3. **Finding 1's true remainder.** State precisely which receiver kinds D-402's guard already
   covers and which, if any, still lack it. **If nothing remains, say so** — a finding closed by
   an earlier increment is a good outcome and belongs in the record.
4. **Finding 2's diagnostic.** `E1002` is what `ResolveStructFieldAccess` raises for an
   unrecognised field. Confirm it is right for "structs have no methods in v1", and that its
   message reads correctly for a method call rather than a field. **This rejects programs that
   currently compile** — see step 6.
5. **Finding 3's arm.** Read `VisitMemberAccess:1294`'s `Error` arm and mirror it. Confirm
   returning `Error` rather than `Unknown` suppresses the cascade via `Error`'s universal
   assignability (D-300), and that it does **not** suppress a genuinely separate later mistake —
   the distinction D-405/D-406 spent two increments getting right on the parser side.
6. **THE BREAKING-CHANGE ENUMERATION — required before any edit.** Findings 1 and 2 both reject
   programs that compile today. Enumerate every test, fixture, gold master and validation script
   affected, and report the list. Updated to assert new correct behaviour, **never weakened**. If
   the fallout is wider than a handful, STOP and report.
7. **D-362's permissive-`Unknown` catalogue** — report its current membership and what this
   increment changes it to. Finding 3 removes one source; finding 2 may remove another.

Report the three reproductions, the arm-by-arm diff table, finding 1's remainder, the `E1002`
suitability check, the cascade analysis, the breaking-change list, and the catalogue update.
Then STOP.

---

## Standing requirements

**Both apply to every increment, without needing to be asked.**

**1. Archive the prompt in the increment's own commit.** Copy this prompt verbatim to
`prompts/archive/sprint-9/<branch-name>.md` and commit it **with the increment**, not as a
follow-up. Archive it **as issued** — never retrofitted to match what was decided. The gap
between what was asked and what landed is the record's value.

**2. Write the plan-mode report to a file, never into the chat.** Use a scratch path of your own
choosing outside the repository working tree. **Do not stage, commit or archive it** — it exists
so the report renders in the editor rather than scrolling past in the chat. Put the full report
there: every reproduction, enumeration, table and finding, at whatever length it needs. In the
chat, give the file path, a line per gate item, and — explicitly — **any STOP condition hit**,
since a blocker should not need a file to be opened to be noticed.

---


## Scope boundaries — do NOT

- **Do not give structs a method surface.** v1 has none by design (D-043, D-080); this makes the
  *diagnostic* correct, not the language larger.
- **Do not change `VisitMemberAccess`** — it is the reference implementation here.
- **Do not change `?.` behaviour** (D-400, D-402, D-403) or the merge guards (D-404).
- **Do not suppress diagnostics broadly.** Finding 3's failure mode is swallowing a genuine second
  error; D-300's design is one-mistake-one-diagnostic, **not** fewer diagnostics.
- **Do not weaken or delete a test** to absorb the breaking change.
- **No new error code** — `E0101` for nullable receivers, `E1002` for unrecognised members. Count
  stays **121**. If the gate finds a genuinely new condition, STOP and escalate via
  `allocating-an-error-code`. **No new opcode.**

---

## Tests — TDD, red first

- **Finding 1 (if anything remains):** a non-optional `.` method call on each still-uncovered
  nullable receiver kind raises `E0101`; `?.` on the same still resolves and short-circuits
  correctly (D-400/D-402 green).
- **Finding 2:** `someStruct.anything()` on a user `type` and on an anon-struct raises `E1002`,
  with a message that reads correctly for a method call. Field **access** on the same structs
  still works — the regression a careless change breaks.
- **Finding 3 — load-bearing:** a method call on an errored receiver produces **exactly one**
  diagnostic, and **two genuinely distinct mistakes still produce two**. This is D-405's proof
  shape, and the test that distinguishes suppression from swallowing.
- **The arm-diff table asserted**, so the next divergence between the two paths fails a test
  rather than being found by accident a fourth time. **Mutation-verify it** — remove one arm,
  confirm it fails for the right reason, restore — per D-403/D-404/D-406's standard.
- Every existing member-access, struct, nullable and error-recovery test green unless enumerated,
  each updated assertion visible in the diff.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-408**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the three reproductions;
  **finding 1's remainder — including "already closed by D-402" if that is the answer**; the
  **arm-by-arm diff table** as the durable record of the two paths' divergence, and whether it is
  now pinned; finding 2's `E1002` adoption and that structs gain no method surface; finding 3's
  `Error` arm and the test proving it suppresses a cascade without swallowing a distinct mistake;
  the breaking-change list with what each updated test now asserts; D-362's catalogue updated; and
  that this **closes the correctness batch**. No new opcode, no new error code, count 121. Cite
  D-380 (the three findings), D-402, D-403, D-404, D-405, D-406, D-300, D-362, D-043/D-080 (no
  struct methods in v1).
- **Deliverable:** repo-pathed zip (source, tests, updated design docs), including the archived
  prompt per the standing requirements above. The plan file is scratch — not included.
