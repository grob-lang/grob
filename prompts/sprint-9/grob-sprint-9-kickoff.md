# Sprint 9 Kickoff — Core Standard Library (Part 2)

> **A record, not a gate.** Like the Sprint 5, 6, 7 and 8 kickoffs, this prompt does no
> setup and unblocks nothing. It is not a slash command — it is the durable record of
> the agreed A–H breakdown, the load-bearing ordering calls, the error-code budget, the
> structural decisions settled here (the array-indexer emission fix, the `mapAs<T>`
> typed-deserialisation model, the regex-literal scope-cut resolution and the
> capability-seam completion), and the close-gate, kept under `prompts/sprint-9/` with
> the QA brief. The increment commands live in `.claude/commands/sprint-9-{a..h}.md`,
> with archive copies under `prompts/archive/sprint-9/`. **Start by invoking
> `/sprint-9-a`.**

Begin Sprint 9 — the core standard library, part two. Sprint 8 stood up the
module machine — the plugin/native-module infrastructure, the compile-time
module-namespace model (D-342), the native-throw seam and the capability-injection
seam (D-343) — and delivered nine light modules on top of it. Sprint 9 completes the
standard library with the **six heavier, type-carrying modules** — `date`, `fs`,
`json`, `csv`, `process`, `regex` — lands the **two remaining capability interfaces**
(`IFileSystem`, `IProcessRunner`), closing out D-319's six, fixes the standing
array-indexer emission gap that D-345 surfaced, and settles the regex-literal scope-cut
in favour of deferral. The full scope and acceptance are in
`docs/design/grob-v1-requirements.md` **§ (Sprint 9 — Core Standard Library (Part 2))**.
Read it word for word; it is the build contract. The module shapes are in
`docs/design/grob-stdlib-reference.md` (`fs`, `date`, `json`, `csv`, `regex`,
`process` sections). The stability ritual is
`docs/design/grob-benchmarking-strategy.md` **§6** and **§11** under D-302/D-346.

## Why this sprint is shaped the way it is

Sprint 8 modules were pure functions and constants — fail-fast, low integration risk.
Sprint 9 modules carry the **registered types** — `date`, `File`, `json.Node`,
`csv.Table`/`CsvRow`, `Regex`/`Match`, `ProcessResult` (D-283 risk-front-loading, D-303
`Struct` discriminator) — so any latent issue in the type-registry plumbing surfaces
here, where the compiler's type-registry support has had Sprint 8 to settle. Two of
these modules touch the host through the seam's **last two interfaces**, and two of
them (`json`, `csv`) exercise the language's first real **explicit type-argument
consumption** (`mapAs<T>()`). None of that is a native registration; each is more than
`math.sqrt`. The sprint's risk therefore clusters in three places: the `mapAs<T>`
typed-deserialisation dispatch, the indexer surface the JSON/CSV types depend on, and
the host boundary the two new capabilities extend.

## Still on Claude Code (D-314)

Sprint 9 runs on the same Claude Code harness — durable rules in `CLAUDE.md`, plan mode
as the approval gate, increment prompts as slash commands, the Husky.NET pre-push gate,
CodeRabbit pre-PR, a PR per increment, `main` protected, GPT-5.3 Codex as the external
cold-read via Codex CLI against the merged branch. Archive copies of the increment
commands live under `prompts/archive/sprint-9/`; the kickoff and QA brief stay under
`prompts/sprint-9/`. Full harness rationale in D-314.

**One Opus carve-out this sprint — Increment D.** Sprint 9's single genuinely new
structural call is `mapAs<T>()` — the checker resolving an explicit type argument at a
call site and producing a value of that type from a `json.Node`, and the runtime
coercion that realises it. It is the first real use of the constrained-generic
consumption machinery (D-080, users consume generic functions and cannot declare them)
against a plugin type, and it is release-gate-critical — four validation scripts call
`.mapAs<…>()`. Getting the `<T>`-consumption dispatch right, once, is the load-bearing
call — the direct analogue of Sprint 8's D-342 module-namespace dispatch and Sprint 7's
D-334 finally-chain. Sonnet 4.6 (High) drives Increment D, and the `mapAs<T>`
type-argument-resolution sub-problem escalates to an Opus 4.8 subagent (the
`grob-closure-specialist` mechanism, config under `.claude/agents/`). A–C and E–H are
Sonnet throughout. If any other increment turns out to carry a load-bearing structural
decision, stop and surface it rather than reaching for Opus on a task that merely feels
hard.

## No new project, no new opcodes, two interfaces to finish the seam

Sprint 9 adds **no** `src/` project — `Grob.Stdlib` and `Grob.Stdlib.Tests` exist from
Sprint 8. The six modules are `IGrobPlugin` implementations in the existing project,
which references `Grob.Core` + `Grob.Runtime` only (§ solution architecture; DAG
strict). The new surface is six registered plugin types, the two remaining capability
interfaces, and the one missing compiler emission.

Sprint 9 adds **no opcode**. Module member access resolves at compile time to a
qualified native and emits the existing `Call` (D-342); the new plugin types are
`Struct`-discriminated with **no new `GrobValueKind` variant** (D-303 — the nine
variants are fixed, and `File`/`date`/`json.Node`/`csv.Table`/`CsvRow`/`Regex`/`Match`/
`ProcessResult` all share the `Struct` discriminator, reference-stored). An implementer
who reaches to add an `OpCode` case or a `GrobValueKind` variant stops and surfaces —
both surfaces are closed (§3.3, D-303, ADR-0013). Growing `OpCode` would be the
`adding-an-opcode` procedure (D-331) and a wire-format consideration, not an incidental
edit.

**The two remaining capability interfaces land into a proven seam.** D-343 established
the injection mechanism and landed `IStandardStreams`, declaring `IEnvironment`/
`IClock`/`IRandomSource` for their Sprint 8 consumers. Sprint 9 declares and consumes
the final two — `IFileSystem` (Increment C, `fs`) and `IProcessRunner` (Increment F,
`process`) — wiring two more implementations into the seam rather than retrofitting it.
That closes D-319's six. The DAG holds: interfaces in `Grob.Runtime`, consumed by
`Grob.Stdlib`, OS-backed defaults injected by `Grob.Cli`; no new cross-edge, and the
`Grob.Compiler` ↔ `Grob.Vm` non-reference invariant is untouched.

## The array-indexer emission fix (Increment A)

D-345 surfaced, and deliberately left out of scope, a standing correctness gap:
`Compiler.Expressions.cs` has no `VisitIndex` override, so `arr[i]` on an **array** has
no emission at all and crashes the VM with a stack underflow. `arr[n]` has been
grammatical since D-112 and type-checks, but the compiler never emitted for it. This is
release-blocking on its own, and it is a **hard prerequisite** for this sprint:
`json.Node["key"]` and `csv`'s `row[name]`/`row[index]` are indexer access on plugin
types, and the array-indexer emission underneath them does not exist. Map indexers
already emit (D-112); array indexers do not.

Increment A adds the `VisitIndex` emission for array index read, wires out-of-bounds to
the **existing** `E5101` (array index out of range → `IndexError`, already registered)
through the native-throw/handler-table path (D-334/D-342), and confirms the
`IndexError` leaf (D-284) is reached. **No new error code** — E5101 and E5102 (substring
bounds) are already in the registry; the fix wires them, it does not mint them. This is
the one increment that clears standing debt before any module work.

**The reconciliation rides Increment A.** Sprint 8's guid Increment D landed **E0601**
(invalid guid string literal), taking the true error-code count 117 → **118**, recorded
correctly in `grob-error-codes.md`'s three internal locations under source decision
D-149 — but with **no decisions-log landing entry**, so D-345 and D-346 both still state
"count unchanged at 117". The decisions log — the authority — is behind the registry by
one, and the guid landing it never captured. Increment A lands a short **append-only**
reconciliation entry recording the Increment D landing and correcting the running count
to 118 (D-345/D-346 stay as written — they were correct for the codes they touched, and
the log is append-only, so the correction is a new entry, not an in-place edit). The
D-316 gate is green today because it checks catalog↔registry agreement, not the log's
narrative count; the reconciliation restores the authority to the true state and is the
kind of drift a planning pass exists to catch.

## The regex-literal scope-cut, resolved: deferred to post-MVP

D-186/D-283 list the regex literal grammar (`/pattern/flags`) as a v1 scope-cut
candidate, activation at Chris's discretion. The requirements Sprint 9 § lists the
literal as in-scope. That contradiction is resolved here: **regex literals are deferred
to post-MVP; Sprint 9 ships `regex` with the module convenience functions plus a new
`regex.compile()` constructor for reusable `Regex` values.**

Rationale, against the language-quality bar rather than build convenience:

- Context-sensitive `/` disambiguation (regex literal versus division) is the single
  highest-risk lexer change in the language, and both reference audiences — C#
  (`new Regex`) and Go (`regexp.Compile`) — compile from strings. A C#/Go developer
  reads Grob without needing `/…/`.
- Backtick raw strings are already the canonical Grob idiom for regex patterns (D-285),
  so `regex.compile(\`\d+\`, "i")` reads cleanly with no escaping cost.
- **Zero** of the eleven release-gate scripts use a regex literal (D-283's own
  observation, re-confirmed against `grob-sample-scripts.md`). Activating literals buys
  the highest-risk grammar change for a syntax the release gate never exercises.
- Deferral is additive: the literal grammar can land in v1.1 (D-186) with `E2007`/
  `E2008` — the unterminated-regex-literal codes are already reserved in the Syntax
  block, so the grammar-error surface is pre-provisioned either way.

**The load-bearing consequence Increment G must carry.** A `Regex` value is currently
*only* creatable by a literal (D-089/D-095). Deferring literals without a replacement
constructor would leave the entire `Regex`/`Match` type surface dead — only the
compile-per-call module functions would work. Increment G therefore **adds**
`regex.compile(pattern: string, flags: string = ""): Regex` as the Regex constructor —
exactly the form D-283 names as "covering everything regex literals would" — and D-186
requires a `regex.compile()`-exercising release-gate script before v1, recorded as a v1
release-gate step in Increment H. The requirements Sprint 9 `regex` bullet is reconciled
in Increment G: the `/pattern/flags` literal line is replaced by `regex.compile()`,
citing the deferral decision.

## The agreed increment breakdown

§ (Sprint 9) is one section with one acceptance block, carrying the six modules, the
two capability interfaces, the indexer fix and the stability re-characterisation.
Sliced into eight on the dependency seams, commits kept small and single-concern:

- **A — Array-indexer emission fix + reconciliation.** *The prerequisite increment.*
  `VisitIndex` emission for `arr[i]` read; out-of-bounds → existing `E5101`/`IndexError`
  through the handler-table path; the `IndexError` leaf confirmed reached. The
  append-only decisions-log reconciliation (Sprint 8 guid Increment D landing; count
  117 → 118). No module code, no new error code. Branch `fix/array-indexer-emission`.
- **B — `date`.** The foundation `fs` consumes. The `date` plugin type
  (`Struct`-discriminated, D-303), its registered `toString()` so `ValueDisplay`
  renders it canonically not structurally (D-336), the full `now`/`today`/`of`/`parse`
  surface on `IClock` (declared in D-343, first heavy consumer here), the property and
  method API, `date.parse` throwing `ParseError` through the native-throw seam. Proves
  the value-semantics-plugin-type pattern C–G reuse. Branch `feat/stdlib-date`.
- **C — `fs` + `IFileSystem`.** The `File` plugin type with `date`-typed properties
  (`modified`/`created` — hence date-first), the full decompose/read/write/copy/move
  API, `IoError` through the native-throw seam, the fifth capability interface landed
  with its OS-backed default and its playground VFS seam (D-319 — in-memory
  path→bytes). Branch `feat/stdlib-fs`.
- **D — `json` + `mapAs<T>` + `json.Node` indexer.** *The load-bearing increment.* The
  `json.Node` plugin type, the `node["key"]: json.Node?` indexer over A's emission,
  `asString`/`asInt`/… throwing `JsonError`, and the **`mapAs<T>()` typed
  deserialisation** — the checker's explicit-type-argument resolution (compile-time
  arg-count/constraint via existing `E0401`/`E0402`) and the runtime coercion (shape
  mismatch via the existing `json.Node`/`mapAs<T>` shape-mismatch code). The `<T>`
  consumption dispatch is the Opus carve-out. Branch `feat/stdlib-json`.
- **E — `csv`.** The `csv.Table`/`CsvRow` plugin types, `mapAs<T>()` reuse over the D
  foundation (CSV int-field auto-conversion at the boundary), the `row[name]`/
  `row[index]` indexer, RFC 4180 baseline (quoted fields, embedded commas/newlines, `""`
  escape). Branch `feat/stdlib-csv`.
- **F — `process` + `IProcessRunner`.** The `ProcessResult` plugin type, the four
  `run`/`runShell`/`runOrFail`/`runShellOrFail` forms with `timeout: int = 0`,
  `ProcessError` on timeout expiry through the native-throw seam, the sixth and final
  capability interface landed — closing D-319's six — with its playground unsupported
  seam (D-319 — in-hierarchy host error, no new code) and the command-execution security
  note. Branch `feat/stdlib-process`.
- **G — `regex` (module form) + `regex.compile()`.** The `Regex`/`Match` plugin types,
  the module convenience functions (compile-per-call), and the new
  `regex.compile(pattern, flags): Regex` constructor that replaces the deferred literal.
  The requirements `regex` bullet reconciled to drop the literal and cite the deferral.
  .NET regex engine underneath. Branch `feat/stdlib-regex`.
- **H — Sprint close.** The stability re-characterisation against the **now-larger
  Sprint-9-runnable script set** (Sprint 9 unblocks most of the eleven validation
  scripts, so the full-suite run deferred by D-346 partly activates here), the next
  VM-execution benchmark baseline against the two-axis gate (D-313/D-333), the
  gold-mastered close-gate smoke script (the D-337 family) and the file-organiser
  real-program target from the § acceptance. Branch `feat/stdlib-close`.

Run them in order, each building and testing green before the next, a fresh chat per
increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **The indexer fix first (A), because the JSON/CSV types depend on it.**
  `json.Node["key"]` and `row[name]`/`row[index]` are indexer access, and the
  array-indexer emission underneath them does not exist (D-345). Fixing it first — and
  clearing the Sprint 8 reconciliation in the same standing-debt pass — means the module
  increments build on a working indexer rather than discovering the gap mid-flight.
- **`date` before `fs` (B before C), because `fs` consumes `date`.** `File.modified` and
  `File.created` return `date` values (D-283 — the `fs → date` link is satisfied because
  `date` ships in the same sprint). `date` is also the lightest of the type-carrying
  modules, so it proves the plugin-type-with-registered-`toString()` pattern before the
  heavier `File`/`json.Node`/… reuse it.
- **`json` before `csv` (D before E), because `csv` reuses `mapAs<T>`.** The
  type-argument-consumption machinery is stood up once in D against `json.Node`, then E
  reuses it over `csv.Table` — the Sprint 8 nominal-before-structural rhythm.
- **The host-and-scope-heavy modules late (F, G).** `process` lands the last capability
  and carries the command-execution security surface; `regex` carries the
  `regex.compile()` addition and the literal-deferral reconciliation. Both sit after the
  seam and the type-registry pattern are proven.
- **Close last (H), off the feature path.** The stability re-characterisation, the
  benchmark baseline and the smoke script round off the sprint once A–G are green.

## Structural decisions to log this sprint

Confirm every `D-###` against the **live** decisions-log tail at increment time — the
same verify-before-allocate discipline the error codes follow, and the reason Sprint 8's
provisional D-339/D-340 landed as D-342/D-343. The live tail closes at **D-346**;
next-free is **D-347**. Provisional allocation, confirmed in-increment:

- **Array-indexer emission (Increment A).** `VisitIndex` emission, out-of-bounds →
  `IndexError` via `E5101`. Provisionally **D-347**.
- **Sprint 8 guid Increment D reconciliation (Increment A).** The append-only landing
  record and count correction to 118. Provisionally **D-348**.
- **`mapAs<T>` typed deserialisation (Increment D).** The explicit-type-argument
  consumption model — compile-time resolution and runtime coercion. Provisionally
  **D-349**.
- **Regex literals deferred to post-MVP; `regex.compile()` added (Increment G).** The
  scope-cut activation call, the constructor addition, the requirements reconciliation
  and the v1-gate release-script note. Provisionally **D-350**.
- **`date`/`fs`/`csv`/`process` module landings.** Each records a landing decision at
  its real next-free number in-increment, on the D-344 precedent (a module-landing
  record), fold-versus-new judged live — including the `IFileSystem` (C) and
  `IProcessRunner` (F) capability-interface additions that extend D-343/D-319, and the
  playground VFS/unsupported seams. If a landing carries no structural surprise beyond
  the D-342/D-343 pattern, a short record suffices; if it does, surface it.

Each decision is recorded in three-location lockstep (summary row, full entry, footer
changelog), with D-349 extending D-080 and D-350 superseding the regex-literal line of
D-186/D-283.

## Error-code budget — reuse-heavy, few new codes

Sprint 9 wires far more diagnostics than it mints. All confirmed next-free against the
**live** registry at their increment (the base after Sprint 8 is **118**, E0601
included). Runtime throws reuse the Sprint 7/8 leaves: `fs` → `IoError`, `date.parse`/
`regex.compile` on a bad pattern → `ParseError`, `json` shape faults → `JsonError`,
`process` timeout → `ProcessError`, array bounds → `IndexError` via the existing
`E5101`. `mapAs<T>` compile-time arg-count/constraint reuses `E0401`/`E0402`; its runtime
shape mismatch reuses the existing `json.Node`/`mapAs<T>` shape-mismatch code. New codes
are expected to be **few and compile-time** — each a fold-versus-new call in-increment
via `allocating-an-error-code` (D-331), registered at the next free number from the live
registry, count reconciled, D-316 ratified. Do not invent a literal; confirm every code
against the live registry before use.

## The close-gate

Sprint 9 closes (Increment H) on `grob run` over a script that exercises the Sprint-9
module surface — a `date` value, an `fs` read/write, a `json.read().mapAs<T>()`, a `csv`
round-trip, a `process.run` capture and a `regex.compile()` match — gold-mastered under
`Grob.Integration.Tests`, its contract being stdout, stderr **and** exit code (D-337).
Because Sprint 9 unblocks most of the eleven release-gate validation scripts, the
close also runs the **file-organiser real-program target** (§ acceptance) and
re-characterises the stability ritual against the now-larger Sprint-9-runnable set
(the full-suite run deferred by D-346 partly activates), locking any revised
iteration/warmup/tolerance in `stability.json` as a D-302/D-346 addendum. Sprint 9 is
closeable when Increment H's acceptance is green, the next VM-execution benchmark
baseline passes the two-axis gate (D-313/D-333), and the stability test produces a
passing run against the locked calibration numbers.

## Acceptance to hit (whole sprint)

The § acceptance, met across the eight increments: `arr[i]` emits and out-of-bounds
throws `IndexError`; each module's full API works; `File` properties return `date`
values; `json.read().mapAs<T>()` and `csv.read().mapAs<T>()` deserialise to typed values
and reject a shape mismatch; `json.Node["key"]` and `row[name]`/`row[index]` index
correctly; `process.run` captures stdout/stderr/exit code and throws `ProcessError` on
timeout; `regex.compile()` produces a reusable `Regex` and matching/replacement work;
the file-organiser real-program target runs correctly; the stability re-characterisation
is complete with locked numbers and a passing run. Sprint 9 is closeable when Increment
H's acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh corpus zip uploaded at the start of each
  increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 9 code is C# 14 from the first line.
- **`ErrorCatalog` (D-308).** Every diagnostic references a central catalog descriptor;
  the `"Exxxx"` string for any code appears exactly once. Any new Sprint 9 code goes in
  `grob-error-codes.md` and the catalog at its next-free number in its increment — never
  a literal at the call site, never invented. The D-316 gate asserts catalog↔registry
  agreement on every commit.
- **No new `GrobValueKind` variant (D-303).** All six new types share the `Struct`
  discriminator. Each registers a `toString()` so `ValueDisplay` (D-336) renders it
  canonically, not as its structural fields — the same registered-`toString()`
  precedence that keeps `AuthHeader` opaque and now renders `guid`. Verify it holds for
  every new type on the real print and interpolation paths.
- **Tests are plain xUnit.** `FsCheck` and `FluentAssertions` are not in
  `Directory.Packages.props` and are not used; `[Theory]` rows cover the layer
  invariant. Route module tests to `Grob.Stdlib.Tests`; type-checker/compiler tests to
  `Grob.Compiler.Tests`; VM tests to `Grob.Vm.Tests`; smoke/gold-master to
  `Grob.Integration.Tests` (§3.5). Follow the `tdd-cycle` skill.
- **§3.1.1 holds on every new node.** Every identifier, member and indexer node the new
  surface introduces carries a non-null `ResolvedType` and a non-null `Declaration`,
  asserted by reference (`Assert.Same`).
- **Coverage.** The affected projects hold at or above the 90% line+branch floor (D-328,
  ADR-0018); nothing is excluded to make the number — the `mapAs<T>` coercion paths, the
  indexer emission and the `process` failure paths especially.
- **DAG.** `Grob.Compiler` and `Grob.Vm` still do not reference each other; `Grob.Core`
  remains the only shared ground; the capability interfaces live in `Grob.Runtime`;
  `Grob.Stdlib` references only `Grob.Core` + `Grob.Runtime`. No new cross-edge.
- **Model (D-314).** Sonnet 4.6 (High) is the code-gen workhorse throughout. The one
  Opus carve-out is the `mapAs<T>` type-argument-resolution sub-problem in Increment D,
  run through an Opus 4.8 subagent.
- Start with `/sprint-9-a`.
