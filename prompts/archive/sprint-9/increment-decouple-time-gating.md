# Increment: decouple time-gating from allocation-gating (D-395 Q3)

**Branch:** `bench/decouple-time-gating`
**One concern:** implement D-395's ratified outcome — no category gates on time, while
`compile` keeps gating on allocation. **This is what turns `main` green**; D-395 ratified the
decision but made no `policy.json` or `BenchCheck` change.

Runs against the fresh corpus zip carrying D-356 through D-395. Corpus-first discipline
throughout; read the live decisions log tail, do not trust this prompt for D-### numbers.
**Error-code count is 121** — unchanged by this increment.

---

## Authority

**D-395** is the operative decision — read it in full, particularly Q1 (time informational
everywhere) and Q3 (what this increment must do). Its confirmed mechanism: `BenchCheck`'s
noise floor is computed from **within-run** iteration variance
(`BdnStatistics.StandardDeviation / Mean`, `RelativePercent` ~line 421), which is structurally
blind to **between-run** variance. On run `30720384069` that produced
`max(5.0%, 3σ × 2.02%) = 6.06%` against a 12.0% between-run move — a false breach on code the
change never touched, with a **0.0%** allocation delta.

**The trap D-395 identified, and the reason this is a schema change rather than a flag flip.**
`policy.json`'s `gating` field currently couples **both** axes — D-333's text: the
allocation-percent axis "gates on the same categories time gates today". Setting
`compile.Gating = false` to silence its time axis would **also silence its 10%
allocation-percent check**, which the evidence says should keep gating, because allocation is
demonstrably deterministic (0.0% across every benchmark in both runs).

**The precedent to mirror:** D-391's absolute allocation ceiling already gates *regardless* of
`gating`. The allocation-percent axis should do the same — gate unconditionally wherever a
`PolicyCategory` configures one — leaving `gating` (or a renamed successor) governing **only**
the time axes.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **`ClassifyTime`** (~line 346) and **`RelativePercent`** (~line 421) — confirm D-395's
   reading of the noise-floor computation before changing anything around it.
2. **Every consumer of `PolicyCategory.Gating`** — enumerate them all. D-395 names the time and
   allocation-percent axes; confirm nothing else keys off the same flag (report and stop if it
   does).
3. **How the D-391 ceiling gates regardless of `gating`** — this is the pattern the
   allocation-percent axis should adopt. Report the mechanism so it is mirrored, not reinvented.
4. **Whether `gating` should be renamed.** D-395 says "`gating` (or a renamed successor)". Once
   it governs only time, `gating` is a misleading name for a field that no longer gates
   anything — every category's time axis becomes informational, so the flag has no true value
   left in practice. Report options: rename to something accurate, or remove it entirely and
   have `ClassifyTime` always return informational. **Recommend, with the reasoning** — a
   vestigial always-false flag is its own future confusion.
5. **The overall outcome to verify:** after this change, does *any* category gate on time? D-395
   says none should. Confirm the code makes that true rather than merely making `compile`'s case
   pass.
6. **Confirm the re-run passes.** Using run `30720384069`'s committed figures, walk the gate
   logic and report what each row would classify as under the new schema. The single breach row
   must become informational; every allocation row must keep its current classification.

Report the consumer enumeration, the decoupling design, the rename recommendation, and the
walked-through re-run result. Then STOP.

---

## Scope boundaries — do NOT

- **Do not change any threshold value.** Not the 5% per-sprint, not the 12% cumulative, not the
  10% allocation-percent, not any D-391 ceiling. This increment changes **which axes gate**, not
  what any number is. D-313's ratchet rule is untouched, and the entry must say so.
- **Do not update any baseline.** Nothing here changes what is measured.
- **Do not re-capture `compile.origin.json`.** D-395 addressed the cumulative axis by making it
  informational; re-capture is a separate question.
- **Do not remove the time axes.** They stay computed and reported, as information. Removing
  them would discard the only signal that would show a real slowdown, which D-395 explicitly
  accepted as a cost to be borne visibly rather than hidden.
- **Do not add cross-run history or a variance filter.** D-395 rejected option B on sample-size
  grounds and D-333 already deferred consecutive-breach filtering; both need history the tool
  does not retain.
- **Do not touch `src/`** — this is `tooling/`, `bench/` config and docs only.
- **No new opcode. No new error code** — count stays **121**.

---

## Tests

- **`BenchCheck` unit tests for the decoupled schema**, and they must be **load-bearing** — the
  D-391 review found an override test that stayed green when the override was deleted. Verify by
  mutation: a test asserting "allocation still gates for `compile`" must **fail** if the
  allocation-percent axis is wired to `gating`; a test asserting "no category gates on time" must
  **fail** if any time axis is left gating.
- **The `30720384069` scenario as a regression test**: that row's inputs classify as
  informational, not a breach.
- **The converse, equally important**: a genuine allocation-percent breach on `compile` still
  fails. A change that silences time must not silence allocation — that is the whole point.
- Every existing `BenchCheck` and `CliRender` test passes or is updated for the new shape, with
  the change visible in the diff.
- If `gating` is renamed or removed, every test and fixture referencing it is updated
  consistently.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-396**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: D-395 Q3 implemented; the
  decoupling mechanism and how it mirrors D-391's unconditional ceiling; the `gating` field's
  fate (renamed, removed or kept) and why; the confirmation that **no category gates on time**
  after this change; the walked-through and then actual re-run result for `main`; the
  mutation-verified tests; and explicitly that **no threshold value changed** — this alters which
  axes gate, not what any number is, so D-313's ratchet rule is untouched. Cite D-395, D-391,
  D-333, D-313, D-385 (Q6's matrix, amended by citation).
- **Update `grob-benchmarking-strategy.md`** — §9/§9.1's gating matrix and prose, so the
  documented gate and the enforced gate are the same statement.
- **Dispatch `benchmark.yml` on `windows-latest` after merge** and report the result. If it fails
  again, it can only be on allocation — which, given its demonstrated determinism, is a real
  signal to investigate, not noise to dismiss.
- **Deliverable:** repo-pathed zip (tooling, policy, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
