# Correctness batch — Increment 3: `for...in` contents snapshot (D-379, D-383)

**Branch:** `fix/forin-contents-snapshot`
**One concern:** make `for...in` iterate a snapshot of the collection's contents taken at loop
entry, for both arrays and maps. **Changes shipped behaviour on both paths**, and closes a
soundness hole.

Runs against the fresh corpus zip carrying D-356 through D-383. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
or memory for D-### numbers or error codes. **The count is 121** (D-382's E5905/E5906) — confirm.

---

## Authority and context

**D-383 is the operative decision; D-379 is the one it refines.** Read both in full before
planning — D-383 corrects a conflation in D-379 that changes the implementation materially.

Current behaviour, both confirmed by direct bytecode reading in earlier increments:

- **Arrays (D-373):** `EmitArrayForIn` re-reads the bound via a fresh `GetProperty("length")` on
  **every iteration**. An appending body iterates unboundedly; a removing body ends early.
- **Maps (D-378):** `EmitMapForIn` materialises a copy of the **keys** into a synthetic local,
  then reads each **value live** from the live map. A key removed mid-loop is still visited and
  `v` degrades to `nil`.

**The soundness hole this closes.** `for k, v in m` binds `v` as **non-nullable `V`** (D-374).
The map path's live value read puts `nil` into that binding when a key has been removed — a
direct contradiction of Grob's foundational guarantee that non-nullable types are never nil, and
exactly the class of unsoundness explicit nullability exists to prevent. This is the increment's
most important outcome, more than the consistency fix.

**What D-383 ratifies:** snapshot the **contents** at loop entry, both types. Arrays copy the
element sequence; maps copy key-**value** pairs, not keys alone. The rule becomes statable
without knowing the collection type — *you iterate exactly what was present when the loop
started*. Mutation stays permitted; it just has no effect on what this loop visits.

**The copy is shallow, deliberately** (D-372 reference semantics). Copied entries hold the same
`GrobArray`/`GrobMap`/`GrobStruct` references, so mutating an element's own contents **is**
visible inside the loop; only the sequence and entry set are frozen. Do not deep-copy.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Read `EmitArrayForIn` and `EmitMapForIn` in full** (`Compiler.ControlFlow.cs`). Report the
   exact current lowering of each: bound handling, synthetic locals, counter, element/value read,
   scope cleanup, and how `break`/`continue` interact.
2. **`EmitMapForIn`'s existing keys materialisation is the pattern to extend**, not replace. It
   already allocates a snapshot into a synthetic local. Report how, and how it can carry values
   as well — a parallel values array, an array of pairs, or a copied `GrobMap`. Recommend the
   option with the least new machinery, and state the reason.
3. **Array copy mechanism.** Report the cheapest existing way to snapshot a `GrobArray`'s
   elements — an existing native, a `GrobArray` constructor or copy method, or a new internal
   helper. **Do not add a new opcode**; if the gate concludes one is unavoidable, STOP and
   escalate via `adding-an-opcode`.
4. **Synthetic local lifetime.** Both paths need the snapshot to live for the loop's duration and
   be released after. Report how the existing synthetic locals are declared, scoped and cleaned
   up (`DeclareLocalSlot`/`EmitScopeCleanup`, the vehicle A4/A4b used), and confirm nesting works
   — a `for` inside a `for` over the same or different collections must not collide.
5. **Closures and `break`/`continue`.** Confirm a lambda capturing the loop variable still
   captures correctly, and that early exit releases the snapshot local. These are the paths most
   likely to break silently.
6. **THE BREAKING-CHANGE ENUMERATION — required before any edit.** Enumerate **every** test,
   fixture, gold master, error-example pair and validation script whose behaviour changes.
   Report the list. A test may be **updated to assert the new correct behaviour**; it may
   **never** be weakened or deleted to accommodate the change. If the fallout is wider than a
   handful of sites, STOP and report.
7. **BENCHMARK OBLIGATION — D-383 requires this, it is not optional.** This adds an allocation to
   a previously copy-free array path. Plan how to measure it against D-313's two-axis gate (5%
   per sprint, 12% cumulative) using `tooling/Grob.BenchCheck` and `bench/Grob.Benchmarks/`, and
   report the plan. **If the measured cost breaches the gate, surface it and STOP** — do not
   update a baseline to absorb it (D-313's ratchet-trap rule) and do not weaken the semantics
   quietly. Bring the finding back instead.

Report the two lowerings, the snapshot mechanisms, the local-lifetime handling, the
closure/`break` confirmation, the breaking-change list and the benchmark plan. Then STOP.

---

## Scope boundaries — do NOT

- **Do not deep-copy.** Shallow, per D-372 and D-383's explicit statement.
- **Do not forbid or diagnose mutation during iteration.** D-379 rejected the C#/Python-dict
  throwing model for v1; mutation stays permitted and silent.
- **Do not change numeric-range `for...in`** (`for i in 0..10`) — no collection, no snapshot
  question. Confirm it is untouched.
- **Do not update a benchmark baseline to absorb a regression** (D-313).
- **Do not fix the other batch findings** — the `Synchronise()` double-diagnostic, the `?.`-on-
  method-call runtime crash, D-380's three diagnostic-quality gaps, or D-382's `E5102`-has-no-
  throw-site finding. Report anything new; fix none of them here.
- **No new opcode. No new error code** — count stays **121**.

---

## Tests — TDD, red first, same commit

- **Array append during iteration:** `for x in xs { xs.append(...) }` visits exactly the entry
  count and terminates. This currently iterates unboundedly — the test that proves the fix.
- **Array removal during iteration:** `xs := [1,2,3]; for x in xs { xs.remove(0) }` visits
  exactly three elements, each the value present at entry, and **does not fault**. This is the
  case a count-only snapshot would have crashed (D-383's motivating example).
- **THE SOUNDNESS TEST — load-bearing:** `for k, v in m { m.remove(k) }` visits every entry with
  `v` holding its **real value**, never `nil`. Include `m.clear()` in the body. This closes
  D-378's recorded `nil`-into-non-nullable-`V` hole and is the increment's most important
  assertion.
- **Map value updated mid-loop:** `for k, v in m { m.set(otherKey, 99) }` — a later iteration of
  `otherKey` sees the value present **at entry**, not 99. Behaviour change from the live read;
  assert the new semantics explicitly.
- **Shallow-copy semantics:** `rows := [[1],[2]]; for r in rows { r.append(9) }` — the mutation
  **is** visible in `rows` afterwards, proving the copy is shallow, while `rows.append([3])`
  inside the loop is **not** visited.
- **Nesting:** a `for` inside a `for` over the same array, and over different collections — no
  synthetic-local collision.
- **`break`/`continue`** exit cleanly with no stack residue; a lambda capturing the loop variable
  still captures correctly.
- **Numeric ranges unaffected:** `for i in 0..10` unchanged.
- **Benchmark results reported** in the PR, with the D-313 delta stated explicitly.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-384**; confirm, do not assume. Match
  the current index-row format (unpadded date cell). The entry records: D-383 implemented on both
  paths; the snapshot mechanisms chosen for arrays and for maps' key-value pairs, and why; the
  synthetic-local lifetime and nesting handling; that the soundness hole is closed — `v` can no
  longer be `nil` in a non-nullable binding; the shallow-copy semantics confirmed by test; the
  full breaking-change list with what each updated test now asserts; and **the measured benchmark
  delta against D-313's two-axis gate**, stated numerically. No new opcode, no new error code,
  count 121. Cite D-383, D-379, D-378, D-374, D-373, D-372, D-313.
- **Update `grob-language-fundamentals.md`** — the `for...in` section states the contents-snapshot
  guarantee and the shallow-copy consequence explicitly, citing D-383 and this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
