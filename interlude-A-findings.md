# Interlude Increment A — A1 Reconciliation Findings

> One-time reconciliation pass across the design corpus, the implementation and
> the wiki, run at the Sprint 4 -> 5 interlude. Each finding carries evidence, a
> classification and the action taken. The discipline is **stop and surface**:
> a stale count is a fix; a divergence between code behaviour and a spec rule is
> a finding that needs a decision, not a silent edit.
>
> Classifications: _count fix_ / _reference fix_ / _status fix_ /
> _spec correction (with D-###)_ / _code finding (raised, not fixed)_ /
> _new decision_ / _confirmation_.

---

## Summary

| # | Finding | Classification | Action |
| - | ------- | -------------- | ------ |
| F1 | Error-code total stated as 99; live count is 103 | count fix | Canonical total line + changelog entry added to `grob-error-codes.md` |
| F2 | Project Status milestone row stuck at "Sprint 1" | status fix | Row corrected in `grob-decisions-log.md` |
| F3 | `Exit` opcode exists in code, absent from spec §3.3 list | code finding (raised, not fixed) | Surfaced; §3.3 left untouched (closed surface); authorising decision D-110 noted |
| F4 | ADR cross-references across the design corpus | confirmation | All resolve; now locked by the A2 gate |
| F5 | `ErrorCatalog` <-> registry agreement (D-308) | confirmation | Green at 103 codes |
| F6 | Gold-master error-example pairs | confirmation (with caveat) | 46 pairs present, hand-authored, no automated harness |
| F7 | Decisions-log lockstep (index/entry/supersession) | confirmation | Bijection exact (232/232); all supersession targets resolve |

No registry rows were edited. No `ErrorCatalog` descriptor values were changed.
No language content of any spec document was modified. The only document edits
are the non-language count and status corrections in F1 and F2, each tied to a
finding here.

---

## F1 — Error-code total drift: stated 99, actual 103

**Classification:** count fix.

**Evidence.**

- `ErrorCatalog.All` contains 103 descriptors
  (`src/Grob.Core/ErrorCatalog.cs`): 25 Type, 7 NameResolution, 28 Syntax,
  7 Module, 6 ParamDecorator, 29 Runtime, 1 Internal.
- The registry summary index in `docs/design/grob-error-codes.md` lists the
  same 103 codes (E0001 .. E9001). The D-308 agreement test is green, so the
  two halves already agree with each other.
- The registry **footer narrative** stops at 99: the changelog records the run
  reaching 98 (Sprint 4C, E0501-E0504) and then 99 (Sprint 4E, E0505), and the
  most recent line (D-315 retitle) adds no codes. There is no canonical
  current-total line; a reader inferring the total from the last changelog
  number reads 99.
- The increment prompt itself cites "live: 99 across 7 categories". This is
  stale by the same four codes. Per `CLAUDE.md` and the prompt's own §6, the
  on-disk corpus outranks the prompt.

**Root cause — the four unaccounted codes.** Git shows the summary index held
94 codes at commit `e4130dd` (the May 2026 "corrected to 94" edit). Nine codes
were added between that commit and now:

```
E0205  non-constant expression in `const` right-hand side   (Sprint 3, const/readonly)
E0501  `for...in` subject is not iterable                    (Sprint 4C)  -- logged
E0502  single-identifier `for...in` over a `map`             (Sprint 4C)  -- logged
E0503  descending range without explicit negative `step`     (Sprint 4C)  -- logged
E0504  reassignment of `for...in` iterator variable          (Sprint 4C)  -- logged
E0505  non-exhaustive switch expression                      (Sprint 4E)  -- logged
E1102  variable already declared in this scope               (Sprint 3, scope)
E2211  `break` inside `select`                               (Sprint 4, D-315)
E2212  `break` / `continue` outside a loop                   (Sprint 4, D-315)
```

The footer changelog accounts for five of these (E0501-E0505). The other four
— **E0205, E1102, E2211, E2212** — accrued without a footer count update,
exactly the "stale count" class the May 2026 "86 -> 94" correction warned about.
94 + 9 = 103.

**Action.** Added one canonical current-total line to the registry footer and a
changelog entry recording the 99 -> 103 correction and naming the four codes.
Historical changelog lines are left intact as history. No codes were added,
removed or renumbered. The A2 count check parses the canonical total line and
asserts it equals both the summary-index count and `ErrorCatalog.All.Count`, so
this class cannot recur silently.

---

## F2 — Project Status milestone row stale at "Sprint 1"

**Classification:** status fix.

**Evidence.** `docs/design/grob-decisions-log.md` Project Status table, the
"Implementation started" row, read:

> `🔄 In progress — Sprint 1, cleared by D-305`

Sprints 1 through 4 are complete (lexer, parser, type checker, compiler, VM,
variables, control flow, `for...in`, `select`/`case`, switch expressions) and
the project is at the Sprint 4 -> 5 interlude. The row contradicts the actual
sprint position.

**Action.** Corrected the row to reflect Sprints 1-4 complete and the Sprint
4 -> 5 interlude in progress. No decision content changed.

---

## F3 — `Exit` opcode present in code, absent from spec §3.3 list

**Classification:** code finding (raised, not fixed).

**Evidence.** `Grob.Core.OpCode` declares `Exit`
("Terminate the script with the int exit code on top of the stack; pops the
code (D-110)"). The §3.3 opcode listing in `grob-v1-requirements.md` shows only
`Print` under the I/O section; `Exit` is not in the listed set. The authorising
decision **D-110** (`exit()` built-in; uncatchable `ExitSignal`) exists and is
the basis for the opcode.

**Why this is raised, not fixed.** §3.3 is the opcode specification — closed
surface for this increment ("opcode behaviour"). The divergence is one-way:
every opcode the spec lists exists in the enum (spec subset of code holds), so
the A2 completeness check (check 4.4, spec -> code) is and stays green. The gap
is that the spec listing is missing an opcode the code legitimately has.

**Recommended resolution (for a decision, outside this increment).** Add `Exit`
to the §3.3 listing as a spec-doc correction citing D-110, or record the listing
as deliberately illustrative-not-exhaustive. Either is a one-line change once
authorised; it is not folded into A1 because it touches closed-surface content.

---

## F4 — ADR cross-reference integrity

**Classification:** confirmation.

**Evidence.** The design corpus references ADR-0007, ADR-0008, ADR-0012,
ADR-0013, ADR-0014 and ADR-0017. Every one resolves to a published file under
`docs/wiki/ADR/` (0001-0017 all present). No dangling reference, no recurrence
of the historical `ADR-0007 -> 0012` / `ADR-0008 -> 0013` drift.

**Action.** None required. The A2 ADR-reference check now locks this class.

---

## F5 — `ErrorCatalog` <-> registry agreement (D-308)

**Classification:** confirmation.

**Evidence.** `Grob.Core.Tests.ErrorCatalogAgreementTests` runs green (5 tests
passed): every registered code has a descriptor, every descriptor is registered,
titles match, codes are unique and the runtime throws-leaf rule holds — at the
current 103 codes.

**Action.** None. A2 references this test as the catalog <-> registry guard
rather than duplicating it, so the consistency suite is the single index of
every mechanical agreement check in the build.

---

## F6 — Gold-master error-example pairs

**Classification:** confirmation (with caveat).

**Evidence.** `docs/errors/examples/` holds 46 example directories, each a
`slug/slug.grob` + `slug/slug.expected.txt` pair authored to the §10 rendering
format. No test under `tests/` or `tooling/` references `docs/errors/examples`
or `*.expected.txt`, so the pairs are hand-authored and not enforced by an
automated diagnostic-emission harness; the README's "test harness" description
is aspirational.

**Action.** None in this increment. A2 deliberately does not add gold-master
enforcement — that is a larger piece of work (a diagnostic-emission harness over
the CLI) and out of scope for a stateless consistency gate. Recorded here so the
gap is visible rather than implied-handled. Candidate for a later hardening
increment.

---

## F7 — Decisions-log lockstep and supersession integrity

**Classification:** confirmation.

**Evidence.**

- 232 numeric `D-###` summary-index rows and 232 `### D-### —` full entries,
  with an exact bijection and no duplicate index codes.
- All 30 distinct `Supersedes:` / `Superseded by:` targets resolve to a full
  entry.
- `D-PM-001` (the single post-MVP entry) is intentionally index-exempt; it sits
  outside the numeric decision series and is excluded by the `D-###`
  (digits-only) pattern the A2 lockstep check uses.

**Action.** None required. D-316 is the first free number (D-315 is the latest
entry). The A2 lockstep check now locks duplicate-code, bijection and
supersession-target integrity.

---

## Strand 3.2 — front-half code <-> spec reconciliation

The lex -> parse -> type-check -> control-flow surface is frozen for the
interlude. Mechanical reconciliation of the parts the gate can lock:

- **OpCode completeness:** every opcode named in §3.3 exists in
  `Grob.Core.OpCode`. The one divergence is F3 (`Exit` in code, not in the §3.3
  listing) — raised, not fixed.
- **TokenKind completeness:** every keyword, literal, structure and decorator
  token named in §3.4, and every operator/punctuation glyph, maps to a member
  of `Grob.Core.TokenKind`.

Both are now asserted continuously by the A2 completeness check, so this strand
is enforced going forward rather than audited once.
