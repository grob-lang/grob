# Sprint 9 QA Brief — Core Standard Library (Part 2) (cold-read)

> **What this is.** The adversarial cold-read brief for Sprint 9, run by GPT-5.3 Codex
> via Codex CLI against the **merged** Sprint 9 branch after Increment H lands and before
> the sprint is declared closed. It is the external second pair of eyes on the A–H work —
> the same role the Sprint 5, 6, 7 and 8 QA briefs played. It is **not** a slash command
> and unblocks nothing; it is the durable QA contract, kept under `prompts/sprint-9/` with
> the kickoff.
>
> **Standing instruction to the cold-reader.** Read the merged branch directly. The
> design corpus under `docs/design/` is authority and **the decisions log wins on
> conflict**. Assume each seam below is wrong until the code proves it right — this brief
> is a list of where the standard library breaks after it looks done, not a list of things
> that are probably fine. Report findings against the acceptance and the risk classes; do
> not fix, do not open PRs.

## Why this brief is shaped the way it is

Sprint 9 completes the standard library with the six type-carrying modules, the two
remaining capability interfaces and a standing-debt fix. Unlike Sprint 8's thin native
registrations, the failure modes here cluster around **type-directed behaviour** —
`mapAs<T>` consumption, the indexer surface, the registered plugin types — and around the
host boundary the last two capabilities extend. Stripped to causes, the sprint's risk
falls into seven classes; the first two are where a shipped bug would be most expensive:

1. **`mapAs<T>` type-argument consumption (D-349).** The checker must resolve an explicit
   `<T>` at a call site (a named struct or `T[]`), type the call result as that type, and
   the runtime must coerce a `json.Node`/`csv.Table` into it — rejecting a wrong count
   (`E0401`), a constraint violation (`E0402`) and a runtime shape mismatch (the existing
   `JsonError` shape code) with the **right** codes. This is the sprint's headline risk,
   the analogue of Sprint 8's D-342 dispatch.
2. **The indexer surface (D-347).** Array index read must emit (the D-345 gap fixed), and
   `json.Node["key"]`/`row[name]`/`row[index]` must build on it — an out-of-bounds array
   index throwing `IndexError` via the existing `E5101` through the handler table, a
   `json.Node` missing-key returning nil not throwing.
3. **The native-throw seam is one mechanism.** Every Sprint 9 fault — `IoError` (fs/csv),
   `ParseError` (date/regex), `JsonError` (json/csv coercion), `ProcessError` (process
   timeout), `IndexError` (array bounds) — must unwind the **same** Sprint-7 handler
   table, run `finally` correctly and produce the same top-level diagnostic. No bespoke
   native-error path.
4. **The two capability interfaces (D-343/D-319).** `IFileSystem` and `IProcessRunner`
   land into the proven seam, closing D-319's six; no direct OS contact in `Grob.Stdlib`;
   the playground VFS and unsupported seams intact.
5. **The registered plugin types and `ValueDisplay`.** Six new `Struct`-discriminated
   types (`date`/`File`/`json.Node`/`csv.Table`/`CsvRow`/`Regex`/`Match`/`ProcessResult`)
   each with a registered `toString()` so D-336 renders them canonically, never their
   structural fields — the same precedence that keeps `AuthHeader` opaque.
6. **Closed-surface and error-code integrity.** No new opcode, no new `GrobValueKind`
   variant, no `RegexLiteral` `TokenKind`, the codes in lockstep, D-316 green, the count
   correct from 118.
7. **Calibration and close integrity.** No locally-produced baseline committed, the
   stability re-characterisation against the enlarged runnable set, no frozen defect, the
   `regex.compile()` release-gate obligation discharged, the Sprint 8 reconciliation
   landed.

## Per-increment adversarial probes

### Increment A — array-indexer emission + reconciliation

- **Emission (class 2). The core probe.** Confirm `arr[i]` array index read emits over
  existing opcodes — verified through the disassembler, not only the VM's answer — and
  that the pre-D-345 stack-underflow crash is gone. Grep the `OpCode` enum for a new case;
  a new opcode is a closed-surface breach and a blocker.
- **Bounds (class 2/3).** Confirm out-of-bounds throws `IndexError` via the **existing**
  `E5101` through the Sprint-7 handler table — caught by `catch (e: IndexError)` the
  script resumes; unhandled it produces the quality top-level diagnostic (`file:line`,
  D-322) and exit 1; a `finally` around it runs **exactly once**. Grep for a bespoke
  bounds-error path bypassing the handler table.
- **Index write.** Confirm the increment answered whether `arr[i] = v` emits — either
  working, or surfaced with the approved path taken. An unaddressed write crash is a
  should-fix.
- **Reconciliation (class 6/7).** Confirm the decisions log now reads count **118**, that
  a landing entry for Sprint 8 guid Increment D (E0601) exists, that D-345/D-346 were
  **not** edited in place (append-only), and D-316 reconciles at 118. No new error code in
  this increment.

### Increment B — `date`

- **Storage (class 5/6).** Confirm `date` is `Struct`-discriminated with a boxed
  `DateTimeOffset` and **no new `GrobValueKind` variant** — grep the enum.
- **Display (class 5).** Confirm `print(d)`/`"${d}"` render the canonical ISO-8601 string
  through `ValueDisplay`'s registered-`toString()` precedence (D-336 step 2), never
  structural fields.
- **Clock (class 4).** Confirm `date.now`/`date.today` read the injected `IClock`; grep
  for a direct `DateTime.Now`/`DateTimeOffset.Now` in `Grob.Stdlib`.
- **Throw (class 3).** Confirm `date.parse` on a bad string throws a catchable
  `ParseError` through the handler table, and that there is **no** compile-time date-
  literal validation (not in v1).

### Increment C — `fs` + `IFileSystem`

- **Capability (class 4). The core probe.** Confirm `IFileSystem` is declared in
  `Grob.Runtime`, consumed by `Grob.Stdlib`, the OS default constructed in `Grob.Cli`, the
  playground VFS substituted at injection. Grep for a direct `System.IO.File`/`Directory`/
  `Path` OS call in `Grob.Stdlib` — any direct call breaks the playground seam.
- **`File` type (class 5).** Confirm `File.modified`/`created` return the `date` type;
  `File` registers a `toString()` rendered by `ValueDisplay`, not structural fields;
  `overwrite` honoured on `copy`/`move`/`moveTo`/`copyTo`.
- **Throw (class 3).** Confirm an `fs` failure throws `IoError` through the handler table.

### Increment D — `json` + `mapAs<T>` + `json.Node` indexer

- **`mapAs<T>` (class 1). The core probe.** Confirm `json.read(p).mapAs<Config>()` types
  the result `Config` and `mapAs<Config[]>()` types it `Config[]`; a wrong type-argument
  count is `E0401`; a constraint violation is `E0402`; a runtime shape mismatch throws the
  existing `JsonError` shape code through the handler table — **not** a bespoke coercion
  path. Confirm no user-facing generic-declaration mechanism was built (D-080 consumption
  only).
- **Indexer (class 2).** Confirm `node["key"]` returns `json.Node?`, is total (nil for
  missing, never throws), and builds on A's array-indexer emission.
- **Accessors/display (class 5).** Confirm `asString`/`asInt`/… throw `JsonError` on a
  wrong-type node; `print(node)` renders raw JSON via the registered `toString()`.
- **Closed surface (class 6).** Grep the `OpCode` enum and `GrobValueKind` — no new case
  for `json.Node` or the `<T>` machinery.

### Increment E — `csv`

- **`mapAs<T>` reuse (class 1/6).** Confirm `csv.Table.mapAs<T>()` reuses the D-349
  machinery — **not** a second `mapAs` mechanism — types the result `T[]`, auto-converts
  int fields, and throws on a conversion/shape mismatch through the handler table.
- **Indexer (class 2).** Confirm `row[name]`/`row[index]` build on A's emission and the
  `get(name)`/`get(index)` method forms agree.
- **RFC 4180 (class 3).** Confirm quoted fields, embedded commas/newlines, `""` escape;
  `hasHeaders`/`delimiter` honoured; `csv.read` throws `IoError` through the handler table.

### Increment F — `process` + `IProcessRunner`

- **Capability (class 4). The core probe.** Confirm `IProcessRunner` is the **sixth and
  final** of D-319's six, landed into the proven seam; the playground injects
  `UnsupportedProcessRunner` raising an in-hierarchy host error with **no new code**. Grep
  for a direct `System.Diagnostics.Process` in `Grob.Stdlib`.
- **Semantics (class 3).** Confirm `timeout: 0` is infinite; timeout expiry throws
  `ProcessError` through the handler table; `runOrFail`/`runShellOrFail` throw on non-zero
  exit; `ProcessResult` carries no `timedOut` field.
- **Display (class 5).** Confirm `print(result)` renders via `ValueDisplay`.

### Increment G — `regex` + `regex.compile()`

- **No literal (class 6). The core probe.** Confirm the `/pattern/flags` literal is **not**
  implemented — no `RegexLiteral` `TokenKind`, no lexer `/` disambiguation. Grep the
  `TokenKind` enum; a `RegexLiteral` case is a scope breach (the literal is deferred).
- **`regex.compile()` (class 3).** Confirm `regex.compile(pattern, flags)` produces a
  reusable `Regex`; a bad pattern throws `ParseError` through the handler table; the
  `Regex`/`Match` surface, the `i`/`m` flags, named groups and `regex.escape` work; the
  module convenience functions match the compiled forms.
- **Reconciliation (class 7).** Confirm the requirements `regex` bullet's literal line was
  replaced by `regex.compile()` citing D-350, and that D-350 records the v1-gate
  release-script obligation.

### Increment H — close

- **Stability (class 7).** Confirm the re-characterisation ran against the enlarged
  Sprint-9-runnable set (not the six-script Sprint 8 set), recorded as a D-302/D-346
  addendum and a §6 correction; `stability.json` carries the locked numbers and a passing
  run.
- **Baseline (class 7).** Confirm the new VM-execution baseline was produced from
  `benchmark.yml` on `windows-latest` (D-309 — **not** locally committed) and passes the
  two-axis gate (D-313/D-333). Confirm no known defect was frozen.
- **Smoke and targets (class 7).** Confirm the close-gate smoke script meets its
  stdout/stderr/exit contract, the five prior smoke scripts still run, the file-organiser
  real-program target runs, and the `regex.compile()` release-gate obligation is
  discharged or recorded as a v1-gate item.

## Cross-cutting probes

- **The unwind is one mechanism (class 3).** Confirm user `throw`, the Sprint-7/8 routed
  errors and every Sprint 9 native throw (`IoError`/`ParseError`/`JsonError`/
  `ProcessError`/`IndexError`) unwind through the **same** handler table — grep for a
  second bespoke unwind bypassing the handler table or the `finallyOffset`.
- **`mapAs<T>` is one mechanism (class 1).** Confirm `csv` reuses `json`'s `mapAs<T>` — no
  duplicated type-argument-resolution or coercion machinery.
- **The host boundary holds (class 4).** Grep `Grob.Stdlib` for direct `System.IO`,
  `System.Diagnostics.Process`, `DateTime.Now`/`DateTimeOffset.Now`, `Console.` and
  `System.Environment`/`System.Random` — all host contact must route through the
  `Grob.Runtime` capability interfaces. A direct call breaks the playground seam (D-319).
- **Six interfaces complete (class 4).** Confirm D-319's six capability interfaces are all
  declared and consumed after Sprint 9 (`IStandardStreams`/`IEnvironment`/`IClock`/
  `IRandomSource` from Sprint 8, `IFileSystem`/`IProcessRunner` from Sprint 9).
- **OpCode / GrobValueKind / TokenKind untouched (class 6).** Confirm the `OpCode` enum is
  unchanged (ADR-0013), `GrobValueKind` is the same nine variants (D-303 — no case for any
  new type), and no `RegexLiteral` `TokenKind` was added (the literal is deferred).
- **Error-code integrity (class 6).** Confirm the final count reconciles from **118**
  through the sprint's few additions; catalog↔registry agreement holds; no `"Exxxx"`
  literal where an `ErrorCatalog` descriptor should be; `E0401`/`E0402`/`E5101` and the
  `JsonError` shape code are **reused**, not duplicated.
- **§3.1.1 invariant.** Spot-check that the new nodes — the `mapAs<T>` type-argument node,
  the indexer nodes, the plugin-type member nodes — carry non-null `ResolvedType` and
  `Declaration`.
- **DAG.** Confirm `Grob.Compiler` and `Grob.Vm` still do not reference each other,
  `Grob.Core` remains the only shared ground, the capability interfaces live in
  `Grob.Runtime`, and `Grob.Stdlib` references only `Grob.Core` + `Grob.Runtime` — no new
  cross-edge.
- **Coverage.** Confirm the affected projects, including `Grob.Stdlib`, hold at or above
  the 90% line+branch floor (D-328, ADR-0018) and that nothing was excluded to make the
  number — the `mapAs<T>` coercion paths, the indexer emission and the `process` failure
  paths especially.

## Report format

For each finding: the increment, the risk class (1–7) or "new", the file and line, the
observed behaviour, the expected behaviour with its corpus citation (decisions log wins)
and severity (blocker / should-fix / note). Lead with any **`mapAs<T>` defect** (class 1 —
a wrong result type, a wrong code, a duplicated mechanism, a bespoke coercion path) or any
**indexer/native-throw defect** (class 2/3 — array read not emitting, a missing-key
throw, a native error bypassing the handler table, a skipped `finally`) — those are the
classes most likely to ship and most expensive if they do. Flag any **direct OS call** in
`Grob.Stdlib` (class 4), any **structural/credential leak** through a registered type
(class 5), any **`GrobValueKind`/`OpCode`/`RegexLiteral` growth** (class 6) and any
**locally-committed baseline or frozen defect** (class 7) as blockers. Close with an
overall verdict: closeable, or the blocker list that must clear first.
