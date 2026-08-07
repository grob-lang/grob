# Increment: re-derive the moved ceilings and refresh the rolling baselines

**Branch:** `bench/reset-post-dispatch-baselines`
**One concern:** bring the allocation ceilings and rolling baselines back into line with what
the code now allocates, after D-393's three dispatch fixes (D-394, D-397, D-398) reduced several
fixtures by up to 73%. Closes the benchmark thread.

Runs against the fresh corpus zip carrying D-356 through D-398. Corpus-first discipline
throughout; read the live decisions log tail, do not trust this prompt for D-### numbers.
**Error-code count is 121** — unchanged by this increment.

---

## Authority and the canonical figures

The canonical run is **31046217136** (`windows-latest`, AMD EPYC 7763, gate **PASS**), dispatched
against `main` after D-398 merged. Read its committed `-report-full.json` artifacts rather than
this prompt's table; the numbers below are for orientation and must be re-read from source.

D-391 set the ceilings; **D-393's three fixes then moved the ground under some of them**:

| Ceiling | Canonical now | Headroom | Status |
|---|---:|---:|---|
| `compile` 20,100 B | 16,728 B | 1.20× | **correct — do not touch** |
| `vm`-scalar 4,700 B | 3,848 B | 1.22× | **correct — do not touch** |
| `attribution`-floor 55,400 B | 46,113 B | 1.20× | **correct — do not touch** |
| `Run_ArrayForIn` 638,000 B | 147,832 B | **4.3×** | stale |
| `Run_MapForIn` 1,240,000 B | 761,592 B | 1.63× | stale |
| `attribution` upper group | see below | varies | re-derive |

**Verify that classification before acting on it.** The three "correct" ceilings sit on fixtures
the dispatch fixes did not touch, which is why they still match D-391's 20% convention exactly.
If the live figures disagree with this table, the live figures win.

**D-391's headroom convention is unchanged and is not being renegotiated: 20% over the current
canonical measured value.** This increment applies that existing convention to new measurements.
It does not revisit the convention, the revision rule, or any category's gating.

---

## The rolling baselines matter more than the ceilings

`Run_ArrayForIn`'s rolling baseline still holds **531,616 B** while the fixture now allocates
**147,832 B**. A future regression back to 500,000 B would read as **−6%** and sail through the
10% allocation-percent gate. Every fixture that moved has the same hole, sized by how far it fell.

**This is not a ratchet violation, and the landing entry must say why.** D-313 forbids updating a
baseline to **absorb a known regression**. Recording a **measured improvement** as the new normal
is what a rolling baseline is for; leaving it stale is what actually weakens the gate. State the
distinction explicitly — this project has been careful about it throughout and the record should
stay that way.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Read run 31046217136's `-report-full.json` artifacts** and tabulate every benchmark's
   canonical allocation. Confirm or correct the orientation table above.
2. **Classify every ceiling**: unchanged-and-still-correct, or moved-and-stale. Report the list
   with headroom ratios. **Only re-derive the stale ones** — touching a correct ceiling is
   churn that obscures the diff.
3. **Re-check the `attribution` shape groups.** D-391 grouped by fixture shape. `attr-array-dispatch`
   fell 73% and `attr-native` 48%, so fixtures that were once an order of magnitude apart may now
   be neighbours — or the groups may no longer partition sensibly at all. Report whether the
   existing grouping still holds, and if not, propose a regrouping under the same 20% convention.
4. **Enumerate every rolling baseline that is now stale**, with the size of the gap. Report which
   files change and by how much.
5. **`compile`'s baselines stay untouched** — its measurement is unchanged, and it carries the
   unresolved cumulative-drift question (+71.6% vs origin, still `Unknown processor`, D-395's
   recorded gap). Confirm nothing in this increment touches them.
6. **The D-398 crossover gap — report and scope, do not fix.** D-398 recorded that break-even for
   the receiver cache is ~3–4 calls per receiver, and that **no fixture exercises the "many
   receivers, one call each" shape** — both existing fixtures are the cache's best case. Report
   what an `attr-many-receivers` fixture would look like and what it would cost to add.
   **Recommend whether it belongs in this increment or its own**; adding a fixture changes the
   `attribution` baseline set, which is an argument for doing it here, while a new fixture is
   also a new measurement rather than a re-derivation, which is an argument against.

Report the canonical table, the stale/correct classification, the regrouping proposal if any, the
baseline change list, and the crossover-fixture recommendation. Then STOP.

---

## Scope boundaries — do NOT

- **Do not change the 20% headroom convention**, the revision rule, or any gating configuration.
  D-391 and D-396 settled those.
- **Do not touch a ceiling that is still correct.** Re-derive only what moved.
- **Do not touch `compile`'s baselines** or attempt to resolve its cumulative drift.
- **Do not re-capture any `*.origin.json`.** Origin baselines are the cumulative reference point;
  re-capturing them is a separate decision D-395 explicitly left open.
- **Do not touch `src/`.** This is `bench/` config, baselines and docs.
- **Do not add the crossover fixture without approval** — report and recommend first.
- **No new opcode. No new error code** — count stays **121**.

---

## Verification

- `BenchCheck` runs green against run 31046217136's figures with the new ceilings and baselines.
- **Prove each re-derived ceiling still fires**: show a deliberately inflated value failing for
  each one. A ceiling nobody has seen trigger is indistinguishable from no ceiling, and D-391
  established this as the standard.
- **Prove the refreshed baselines close the hole**: a value at the *old* baseline (e.g.
  `Run_ArrayForIn` at 531,616 B) must now fail the allocation-percent gate, where before it
  would have read as an improvement. This is the point of the increment.
- Categories that gate still gate; categories that do not still do not (D-396's `AllocGating`).

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-399**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: the canonical run used;
  which ceilings were re-derived and which were left alone **because they were still correct**;
  every re-derived value with its measured basis and the unchanged 20% convention applied; the
  `attribution` regrouping if one was needed; the refreshed rolling baselines with before/after
  figures; **the explicit statement that recording a measured improvement is not the ratchet
  D-313 forbids**, with the reasoning; the proof that each ceiling still fires and that the old
  values now fail; the crossover-fixture recommendation and its disposition; and that this
  **closes the benchmark thread opened by D-385**. No opcode change, no new error code, count
  121. Cite D-391, D-393, D-394, D-395, D-396, D-397, D-398, D-313, D-309, and
  `docs/design/bench-allocation-attribution.md`.
- **Update `grob-benchmarking-strategy.md`** if it states any ceiling value or baseline figure
  inline, so the documented and enforced numbers remain the same statement.
- **Dispatch `benchmark.yml` on `windows-latest` after merge** and report the result. It should
  pass with the intended ~20% headroom rather than the current 4.3×.
- **Deliverable:** repo-pathed zip (bench config, baselines, updated design docs). Archive this
  prompt under `prompts/archive/sprint-9/`.
