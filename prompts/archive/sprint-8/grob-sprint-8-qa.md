# Sprint 8 QA Brief — Core Standard Library (Part 1) (cold-read)

> **What this is.** The adversarial cold-read brief for Sprint 8, run by GPT-5.3 Codex
> via Codex CLI against the **merged** Sprint 8 branch after Increment F lands and
> before the sprint is declared closed. It is the external second pair of eyes on the
> A–F work — the same role the Sprint 5, 6 and 7 QA briefs played. It is **not** a slash
> command and unblocks nothing; it is the durable QA contract, kept under
> `prompts/sprint-8/` with the kickoff.
>
> **Standing instruction to the cold-reader.** Read the merged branch directly. The
> design corpus under `docs/design/` is authority and **the decisions log wins on
> conflict**. Assume each seam below is wrong until the code proves it right — this brief
> is a list of where the standard library breaks after it looks done, not a list of
> things that are probably fine. Report findings against the acceptance and the risk
> classes; do not fix, do not open PRs.

## Why this brief is shaped the way it is

Sprint 8 is the first sprint that stands up a whole subsystem — the plugin/native-module
infrastructure — and then builds nine modules on it. Unlike an exception or a struct,
the failure modes are not one mechanism but several thin ones, and they cluster where
the type checker's new **module-namespace dispatch** meets the compiler's emission, the
VM's throw path and the host boundary. Stripped to causes, the sprint's risk falls into
seven classes; the first two are where a shipped bug would be most expensive:

1. **Module-namespace member-access dispatch (D-339).** At an `x.y` node the checker
   must pick exactly one arm — namespace → qualified native / namespace constant; value
   → instance method / field — and reject a namespace in value position and an unknown
   namespace member with the **right** codes. This is the sprint's headline risk, the
   analogue of Sprint 7's finally-chain and Sprint 6's `ValueDisplay` dispatch.
2. **The native-throw seam.** A throwing native (`math.sqrt(-1)`, `env.require`,
   `guid.parse`, `input()` on closed stdin) must unwind the **same** handler table as a
   user `throw`, run `finally` correctly and produce the same top-level diagnostic — no
   bespoke native-error path.
3. **`guid` primitive integrity.** A distinct primitive, `Struct`-discriminated with
   **no** new `GrobValueKind`, value equality, and a registered `toString()` so
   `ValueDisplay` never renders its internal representation.
4. **`formatAs` correctness.** The chained-form rewrite, compile-time column derivation,
   the namespace-misuse errors and culture-pinned float rendering.
5. **The capability boundary (D-319/D-340).** No direct OS contact in `Grob.Stdlib`; the
   playground seam intact.
6. **Closed-surface and error-code integrity.** No new opcode, no new `GrobValueKind`
   variant, the new codes in lockstep, D-316 green.
7. **Calibration and close integrity.** No locally-produced baseline committed, the
   calibration against the runnable set, no frozen defect.

## Per-increment adversarial probes

### Increment A — infrastructure + module-namespace resolution

- **Dispatch (class 1). The core probe.** Confirm the `x.y` dispatch is **total and
  ordered**: `math` resolves as a namespace, `math.pi` to a namespace constant,
  `math.sqrt` to a qualified native `(float): float`. Confirm `x := math` and
  `print(math)` are the namespace-as-value code with a location; `math.nope()` is the
  unknown-namespace-member code and **not** E1002 (value-position undefined member).
  Grep for a fall-through that treats a namespace as a value, or that resolves an unknown
  namespace member through the struct-field/instance-method path.
- **Emission (class 6).** Confirm `math.sqrt(9.0)` disassembles to arg `Constant` +
  `"math.sqrt"` native `Constant` + `Call`, and `math.pi` to a single `Constant` — no
  new opcode, no `GetProperty` against a namespace, no `Import` for a core module. Grep
  the enum; a new case is a closed-surface breach and a blocker regardless of correctness.
- **Native-throw seam (class 2). The second-highest probe.** Confirm `math.sqrt(-1.0)`
  raises a `GrobError` that unwinds the **Sprint-7 handler table** — caught by
  `catch (e: ArithmeticError)` the script resumes; unhandled it produces the quality
  top-level diagnostic (`file:line`, D-322; type; message) and exit 1; a `finally` around
  it runs **exactly once**. Grep for a bespoke native-error path that bypasses the
  handler table or the `finallyOffset` — a parallel path is the latent gap that lets
  `finally` be skipped for native throws.
- **Capability seam (class 5).** Confirm the injection mechanism is in place, `print`
  routes through `IStandardStreams`, and `IEnvironment`/`IClock`/`IRandomSource` are
  declared but `IFileSystem`/`IProcessRunner` are **not** landed. Confirm `Grob.Stdlib`
  references only `Grob.Core` + `Grob.Runtime` and touches no OS API directly.
- **Scope and lockstep (class 6).** Confirm A emitted only `Call`/`Constant`; the enum
  is unchanged; `Grob.Stdlib.Tests` is in `Grob.slnx` (D-335 check green); D-339 and
  D-340 are logged at real next-free numbers in three-location lockstep, D-339 extending
  D-282/D-320; the new error code(s) reconciled.

### Increment B — `math` (complete), `path`, `strings.join`

- **Domain throws (class 2).** Confirm `math.sqrt(x<0)`, `math.log(x<=0)`,
  `math.asin`/`acos` outside `[-1,1]` throw a catchable `ArithmeticError` through the
  seam, while `math.pow(-2.0, 0.5)` → `NaN` and `math.atan2(0.0, 0.0)` → `0.0` do **not**
  throw, and a `NaN` propagates silently. The D-278 "no silent domain errors, but
  IEEE 754 results pass" line must hold exactly.
- **No duplication (class 6).** Confirm `abs`/`floor`/`ceil`/`round`/`truncate`/`min`/
  `max`/`clamp` are **not** on `math` (D-070/D-071) and no string instance method was
  added to the `strings` module.
- **Random (class 3).** Confirm `randomSeed` gives a reproducible sequence, `randomInt`
  is inclusive of both ends, and the PRNG is reached only through `IRandomSource` — grep
  for a direct `System.Random` in `Grob.Stdlib`.
- **`path` (class 3).** Confirm extension normalisation (`".jpg"` leading-dot lowercase),
  `join`/`joinAll`/`normalise`/`isAbsolute`/`isRelative`/`changeExtension` and the
  `separator` constant on Windows-native paths.

### Increment C — `env`, `log`, `input()`

- **`env` (class 3/5).** Confirm `require` throws `LookupError` naming the variable for
  absent **and** empty; `has` is false for both; `set` is process-scoped; all five go
  through `IEnvironment`, never `System.Environment` directly (the playground substitutes
  a synthetic map — a direct OS call would leak past it).
- **`log` (class 5).** Confirm the four levels write to stderr through `IStandardStreams`,
  `debug` is dropped at default level and emitted under `--verbose`/`setLevel`, and a
  logged value renders through `ValueDisplay` — an `AuthHeader`-shaped opaque value shows
  its bracket tag, **never** the credential, including under `--verbose`. A leak here is
  a blocker.
- **`input()` (class 2).** Confirm it reads one line via `IStandardStreams`, strips the
  newline, writes the prompt with no trailing newline, and throws a catchable `IoError`
  on a closed stream.

### Increment D — the `guid` primitive type

- **Storage (class 3/6). The core probe.** Confirm `guid` is `Struct`-discriminated with
  a boxed `Guid` and **no new `GrobValueKind` variant** — grep the enum; a new variant is
  a closed-surface breach and a blocker. Confirm `guid == string` (and `guid`↔`string`
  assignment/argument) is a compile error at the existing mismatch code, not a new one.
- **Equality (class 3).** Confirm `guid == guid`/`!=` compare by value (not reference)
  through the existing `Equal`/`NotEqual`, and `guid.empty` round-trips.
- **Display (class 3).** Confirm `print(id)` and `"${id}"` render the canonical
  lowercase-hyphenated string through `ValueDisplay`'s registered-`toString()` precedence
  (step 2), **never** structural fields — the same precedence that keeps `AuthHeader`
  opaque. Confirm `toUpperString`/`toCompactString`.
- **Generation/parsing (class 2/7).** Confirm `newV4`/`newV7` on `IRandomSource`/`IClock`
  (v7 time-ordered), deterministic variadic `newV5`, `parse` throwing a catchable
  `ParseError` at the **runtime E5701**, `tryParse` returning `guid?`, and the
  **compile-time** literal validation of `guid.parse("<literal>")` firing at a **new
  compile code** distinct from the Runtime E5701. Confirm the `toCompactString`
  requirements-§ reconciliation was recorded.

### Increment E — `formatAs`

- **The rewrite (class 4). The core probe.** Confirm `<expr>.formatAs.table()` rewrites
  **unconditionally** to `formatAs.table(<expr>)` at compile time — verified at the
  AST/compile level, not only by output — and that the checker then validates the
  receiver against `T[]`. Confirm columns derive from a named struct **and** an anonymous
  `#{ }` element type at compile time (no runtime reflection).
- **Namespace misuse (class 4).** Confirm bare `<expr>.formatAs` is a compile error with
  the exact message, and `<expr>.formatAs.X` (unknown method) names the three valid
  methods — through the D-339 code (confirm the fold-vs-new outcome matches the registry).
- **Culture (class 4). A blocker if it leaks.** Confirm `formatAs.csv` (and any float
  cell) renders `1.5`, never `1,5`, under a `de-DE` ambient culture — the D-336 pinning
  end-to-end. Confirm string-left / number-right alignment and auto-sized widths.
- **Scope.** Confirm no scalar formatter (`.format(...)`) was added to `formatAs` (D-282).

### Increment F — close

- **Calibration set (class 7).** Confirm the calibration characterised the
  **Sprint-8-runnable** script set, not all thirteen, and that this was recorded as a
  D-302 addendum **and** a mechanical `grob-benchmarking-strategy.md` §6 correction.
  `stability.json` carries the locked iteration/warmup/tolerance and the date; the
  stability test produces a first passing run.
- **Baseline (class 7).** Confirm the sixth VM-execution baseline was produced from
  `benchmark.yml` on `windows-latest` (D-309 — **not** locally committed) and passes the
  two-axis gate (D-313/D-333). Confirm no known defect was frozen.
- **Smoke script (class 7).** Confirm `grob run stdlib.grob` meets its stdout/stderr/exit
  contract using **no** Sprint-9 module, and `hello`/`calculator`/`functions`/`types`/
  `errors` still run.

## Cross-cutting probes

- **The unwind is one mechanism (class 1/2).** Confirm user `throw`, routed runtime
  errors (Sprint 7 D) and **native throws** (Sprint 8) all unwind through the **same**
  code path — grep for a second bespoke unwind for native errors that bypasses the
  handler table or the `finallyOffset`.
- **No runtime module object (class 1/6).** Confirm no core module is a runtime value —
  no `GetProperty` against a namespace, no module object on the stack, no `Import` for a
  core module. Module members resolve at compile time to qualified natives / namespace
  constants.
- **The host boundary holds (class 5).** Confirm `Grob.Stdlib` reaches the OS only
  through the `Grob.Runtime` capability interfaces — grep for `System.Environment`,
  `Console.`, `System.Random`, `DateTime.Now`, file APIs used directly in `Grob.Stdlib`.
  Any direct call breaks the playground seam (D-319) and is a should-fix at least.
- **OpCode and GrobValueKind untouched (class 6).** Confirm the `OpCode` enum is
  unchanged (ADR-0013) and `GrobValueKind` is the same nine variants (D-303) — no case
  added for `guid` or a module.
- **Error-code integrity (class 6).** Confirm the final count reconciles from **116**
  through the sprint's additions (the D-339 namespace-misuse arm(s) in A, the `guid`
  compile-time literal code in D, and the E-fold outcome); catalog↔registry agreement
  holds; no `"Exxxx"` literal where an `ErrorCatalog` descriptor should be.
- **§3.1.1 invariant.** Spot-check that the new nodes — module members, the `guid`
  operands, the `formatAs` receiver — carry non-null `ResolvedType` and `Declaration`.
- **DAG.** Confirm `Grob.Compiler` and `Grob.Vm` still do not reference each other,
  `Grob.Core` remains the only shared ground, the capability interfaces live in
  `Grob.Runtime`, and `Grob.Stdlib` references only `Grob.Core` + `Grob.Runtime` — no new
  cross-edge.
- **Coverage.** Confirm the affected projects, **including the new `Grob.Stdlib`**, hold
  at or above the 90% line+branch floor (D-328, ADR-0018) and that nothing was excluded
  to make the number — the native-throw paths and the `formatAs` rewrite especially.

## Report format

For each finding: the increment, the risk class (1–7) or "new", the file and line, the
observed behaviour, the expected behaviour with its corpus citation (decisions log wins)
and severity (blocker / should-fix / note). Lead with any **dispatch defect** (class 1 —
a namespace resolved as a value, an unknown member giving the wrong code, a new opcode or
`GetProperty`-against-namespace arm) or any **native-throw defect** (class 2 — a native
error bypassing the handler table, a skipped `finally`, a wrong top-level diagnostic) —
those are the two classes most likely to ship and most expensive if they do. Flag any
**culture leak** in `formatAs` (class 4), any **credential leak** through `log` (class 5)
and any **`GrobValueKind` growth** for `guid` (class 6) as blockers. Close with an overall
verdict: closeable, or the blocker list that must clear first.
