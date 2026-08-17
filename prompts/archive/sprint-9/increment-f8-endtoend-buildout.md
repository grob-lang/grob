# F8 — Build the `endToEnd` benchmark category

**Branch:** `bench/endtoend-buildout`
**One concern:** give the `endToEnd` category real content — frozen script fixtures, benchmark
methods, and a first captured baseline — closing the principal review's F8, open since Sprint 9B.

Runs against the fresh corpus zip carrying D-356 through D-408. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
for D-### numbers. **Error-code count is 121** — unchanged by this increment.

---

## Standing requirements

**Both apply, without needing to be asked.**

**1. Archive the prompt in the increment's own commit.** Copy this prompt verbatim to
`prompts/archive/sprint-9/<branch-name>.md` and commit it **with the increment**, not as a
follow-up. Archive it **as issued** — never retrofitted to match what was decided.

**2. Write the plan-mode report to a file, never into the chat.** Use a scratch path of your own
choosing outside the repository working tree. **Do not stage, commit or archive it** — it exists
so the report renders in the editor rather than scrolling past in the chat. Put the full report
there, at whatever length it needs. In the chat, give the file path, a line per gate item, and —
explicitly — **any STOP condition hit**, since a blocker should not need a file to be opened to
be noticed.

---

## Authority

**F8** (`grob-principal-review-sprint9b.md`): `grob-benchmarking-strategy.md` names end-to-end
script benchmarks the **primary** regression gate, yet the category has produced no fresh
benchmarks through nine sprints — D-341 and D-347 both recorded it untouched, correctly, because
the workload did not exist. Every canonical run since has ended with the same line:

> `endToEnd: no fresh benchmarks matched 'Grob.Benchmarks.EndToEnd' — nothing to compare.`

**D-386 deliberately kept it empty.** D-385 had proposed populating `endToEnd` with the existing
full-pipeline micro-script fixtures; D-386 rejected that — §4.3 defines the category as the
**validation-suite scripts**, so synthetic micro-scripts would have given it content contradicting
its own specification and made F8 read as partly resolved while the artefact it names still did
not exist. F8 was recorded **open**, to be built properly. **This increment is that.**

**The design is already specified — implement it, do not redesign it:**

- **§4.3** — source corpus is the validation-suite scripts, plus a synthetic large script
  (auto-generated, 1000+ lines) to surface throughput characteristics small scripts cannot.
- **§7.3** — benchmarks consume **frozen copies** under `Fixtures/EndToEnd/`, **not** live
  references to `tests/Grob.Integration.Tests/`. The rationale is explicit: validation scripts
  evolve for test-quality reasons, and a live reference would silently change the benchmark
  workload and invalidate the baseline without anyone noticing. A documented **refresh ritual**
  covers deliberate propagation.
- **§4.3's measured region** — `[GlobalSetup]` reads the script source from disk into a `string`;
  the benchmark measures from source string onward, deliberately **including** VM construction,
  since that is a real part of script startup.

---

## Two things the corpus cannot settle — resolve them in the gate

**1. How many scripts, and which ones actually run.** F8 estimated "roughly seven of the eleven"
would be runnable at Sprint 9 close — those not needing `Grob.Http`/`Grob.Crypto`. That estimate
predates the entire consolidation phase, which built string methods, numeric methods, the array
and map surfaces, map literals, default arguments and more. **Determine empirically which
validation scripts compile and run today**; the answer is very likely higher than seven.

**2. The script count is contested in the corpus.** §4.3 and §7.3 both say **thirteen**; F6
records that the correct figure is **eleven** and that a thirteen→eleven sweep is still pending
across several documents. **Do not resolve F6 here** — that is the corpus sweep's job. But do not
propagate a number you have not verified either: state the actual count of validation scripts
found, and note the discrepancy for the sweep.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Enumerate the validation-suite scripts** and, for each, determine empirically whether it
   compiles and runs today — build the CLI and try it, per the standing precedent. Report a table:
   script, runs yes/no, and if no, the blocking dependency (`Grob.Http`, `Grob.Crypto`, `fs`,
   `json`, `csv`, `regex`, `process`, or something else). **This table is the increment's scope.**
2. **Report the actual script count** and how it relates to §4.3/§7.3's "thirteen" and F6's
   "eleven". Note for the sweep; do not fix.
3. **Read `VmBenchmarks` and `AttributionBenchmarks`** as the structural precedent — `[GlobalSetup]`
   usage, the `[MemoryDiagnoser]` attribute, category attribution, and how D-387's restructure
   separated setup from the measured region. `endToEnd` must fit the same harness conventions.
4. **The measured region.** §4.3 specifies source string → full run, **including** VM
   construction. Confirm that is achievable without the `InvocationCount=1` job constraint
   `VmBenchmarks` needed (D-395: `[IterationSetup]` forced it, removing amortisation and making
   timings incomparable to other categories). Report whether end-to-end benchmarks need per-
   invocation isolation — a script that mutates global state may — and if so, **that the same
   timing-comparability caveat applies**.
5. **The synthetic large script.** §4.3 requires one, auto-generated, 1000+ lines. Report how to
   generate it deterministically — a committed generated file, or generated at `[GlobalSetup]`.
   **Deterministic output matters**: a script that differs between runs makes the baseline
   meaningless. Recommend, with the reason.
6. **Allocation ceilings.** D-391/D-399 set per-benchmark and per-shape ceilings from measured
   values with a **20% headroom convention**. New benchmarks arrive with no ceiling. Report
   whether they should get one in this increment (derived from this run's figures) or whether
   `endToEnd` starts unset like a new category — noting D-399 established that a first
   measurement is a *baseline establishment*, not a threshold relaxation.
7. **Gating.** §9.1's matrix and D-385 Q6 set `endToEnd` non-gating with a **stated flip
   condition**: it becomes the gate when it carries the validation-suite corpus, and
   `compile`/`vm` drop to informational. Report whether this increment satisfies that condition.
   **Recommend, but do not flip anything** — that is a policy change and belongs in its own
   decision if it is due.

Report the runnable-script table, the count discrepancy, the harness fit, the measured-region
verdict, the synthetic-script approach, the ceiling recommendation, and the gating assessment.
Then STOP.

---

## Scope boundaries — do NOT

- **Do not modify any validation script** to make it runnable. If a script does not run, it is
  out of scope and its blocking dependency is reported. The frozen copies are copies.
- **Do not resolve F6's thirteen→eleven discrepancy** — report it for the corpus sweep.
- **Do not flip any `gating` flag or `AllocGating` value.** Recommend only.
- **Do not touch `vm`, `compile` or `attribution`** — their fixtures, baselines and ceilings are
  settled by D-399 and must not move.
- **Do not touch `src/`.** This is `bench/`, fixtures and docs.
- **No new opcode. No new error code** — count stays **121**.

---

## Verification

- Every added benchmark builds and runs, and `BenchCheck` recognises the category — the
  `no fresh benchmarks matched` line disappears.
- **Each frozen fixture is byte-identical to its validation-suite source at the moment of
  copying**, and that is asserted or documented, so §7.3's decoupling is real rather than
  aspirational.
- The synthetic large script generates deterministically — **generate twice, compare**.
- `BenchCheck` handles the new benchmarks' first appearance correctly (`new`/`establishing`, no
  comparison, no spurious failure) — the path D-391 confirmed works and D-399 exercised.
- Existing categories' results are unchanged.

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-409**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: **the runnable-script
  table** — which scripts are in the workload and which are excluded with their blocking
  dependency, as the durable record of what the category actually covers; the count found and the
  F6 discrepancy noted for the sweep; the frozen-copy mechanism and the refresh ritual per §7.3;
  the measured region and whether the D-395 timing-comparability caveat applies; the synthetic
  script's generation approach and its determinism proof; the first captured figures; the ceiling
  decision; and the **gating recommendation with F8's flip condition assessed but not actioned**.
  State plainly whether **F8 is now closed** or remains partly open — if the workload is a subset
  of the validation suite because some scripts await `fs`/`json`/`csv`, say so, and say what would
  close it. No new opcode, no new error code, count 121. Cite F8 (`grob-principal-review-sprint9b.md`),
  D-386 (which kept the category empty deliberately), D-385 (Q6's matrix and flip condition),
  D-387, D-391, D-395, D-399, D-309, and `grob-benchmarking-strategy.md` §4.3, §7.3 and §9.1.
- **Update `grob-benchmarking-strategy.md`** if §4.3 or §7.3 states anything this increment
  contradicts — but **not** the thirteen/eleven count, which is the sweep's.
- **Dispatch `benchmark.yml` on `windows-latest` after merge** and report the result, so the first
  `endToEnd` figures are canonical rather than local.
- **Deliverable:** repo-pathed zip (bench fixtures, benchmark code, baselines, updated design
  docs), including the archived prompt per the standing requirements above. The plan file is
  scratch — not included.
