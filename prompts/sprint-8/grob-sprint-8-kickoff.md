# Sprint 8 Kickoff — Core Standard Library (Part 1)

> **A record, not a gate.** Like the Sprint 5, 6 and 7 kickoffs, this prompt does no
> setup and unblocks nothing. It is not a slash command — it is the durable record of
> the agreed A–F breakdown, the load-bearing ordering calls, the error-code budget,
> the two new structural decisions (the module-namespace resolution model, D-339, and
> the capability-injection seam scope, D-340, refining D-319), the two scoping calls
> settled at this kickoff, and the close-gate, kept under `prompts/sprint-8/` with the
> QA brief. The increment commands live in `.claude/commands/sprint-8-{a..f}.md`, with
> archive copies under `prompts/archive/sprint-8/`. **Start by invoking `/sprint-8-a`.**

Begin Sprint 8 — the core standard library, part one. Sprint 7 made the language
**fail and recover**. Sprint 8 gives it a **standard library**: the plugin and
native-module infrastructure stood up for the first time, then nine modules delivered
on top of it — `print`/`exit`/`input`, `math`, `strings`, `path`, `env`, `log`,
`formatAs`, `guid` — plus the stability-test calibration ritual at close. The full
scope and acceptance are in `docs/design/grob-v1-requirements.md` **§ (Sprint 8 —
Core Standard Library (Part 1))**. Read it word for word; it is the build contract.
The module shapes are in `docs/design/grob-stdlib-reference.md` (`math`, `env`,
`formatAs`, `guid` sections) and the requirements § signature lists (`path`, `log`,
`strings.join`, `input`). The stability ritual is `docs/design/grob-benchmarking-strategy.md`
**§6** and **§11** under D-302.

## Still on Claude Code (D-314)

Sprint 8 runs on the same Claude Code harness — durable rules in `CLAUDE.md`, plan
mode as the approval gate, increment prompts as slash commands, the Husky.NET
pre-push gate, CodeRabbit pre-PR, a PR per increment, `main` protected, GPT-5.3 Codex
as the external cold-read via Codex CLI against the merged branch. Archive copies of
the increment commands live under `prompts/archive/sprint-8/`; the kickoff and QA
brief stay under `prompts/sprint-8/`. Full harness rationale in D-314.

**One Opus carve-out this sprint — Increment A.** Sprint 8 stands up a machine that
did not exist before: how a *module namespace* (`math`, `path`, `env`, `log`, `guid`,
`formatAs`) resolves a member access to a native, when the same member-access syntax
already serves struct fields, built-in instance methods and the `.select(...)`/
`.formatAs` reserved-identifier transforms. Getting that dispatch precedence right,
once, is the sprint's load-bearing structural call (D-339) — the direct analogue of
Sprint 7's D-334 finally-compilation model and Sprint 6's D-336 `ValueDisplay`
dispatch ordering. Sonnet 4.6 (High) drives Increment A, and the member-access
dispatch sub-problem escalates to an Opus 4.8 subagent (the Sprint 5 D
`grob-closure-specialist` mechanism), config added under `.claude/agents/` as part of
A. B–F are Sonnet throughout. If any other increment turns out to carry a load-bearing
structural decision, stop and surface it rather than reaching for Opus on a task that
merely feels hard.

## One new project, no new opcodes

Sprint 8 adds **one** `src/` project — `Grob.Stdlib`, the first new project since
Sprint 6. It is the home for the nine modules, one `IGrobPlugin` implementation per
module, referencing `Grob.Core` + `Grob.Runtime` only (§ solution architecture; DAG
strict). It also adds one test project — `Grob.Stdlib.Tests` — which must be created
**and added to `Grob.slnx`**; the D-335 test-project membership check now enforces
that, so an unreferenced `Grob.Stdlib.Tests` is a build failure, not a silent gap.

Sprint 8 adds **no opcode**. Module member access resolves at compile time to a
qualified native (`"math.sqrt"`) and emits the **existing** `Call` over a function
constant — the same shape Sprint 5 gave native functions (`RegisterNative`, §17). The
closed `OpCode` enum (§3.3) is untouched: `Import` already exists for third-party
plugins, but the auto-available core modules need no `Import` — they are registered
before any script is checked and resolve as compile-time namespaces. An implementer
who reaches to add an `OpCode` case stops and surfaces — the enum is complete (§3.3,
ADR-0013). Growing it would be the `adding-an-opcode` procedure (D-331) and a
wire-format version consideration, not an incidental edit.

**The grammar is verified, not assumed.** D-331 recorded that the "grammar-complete
from Sprint 1" premise was false twice. Sprint 8's surface is member access
(`math.sqrt`, `item.formatAs.table()`), which D-320 already made grammatical as
member access when it landed the `.select(...)` and `.formatAs` transforms in Sprint
5. Increment A's **first** act is to confirm `math.sqrt(...)`, `path.join(...)` and
`item.formatAs.table()` actually parse to member-access nodes against the merged
tree. If a production is missing or malformed, that is a finding — extend the grammar
through the `extending-the-grammar` skill (D-331), surfaced not swept, before building
on it.

## The agreed increment breakdown

§ (Sprint 8) is one section with one acceptance block, carrying the plugin
infrastructure, the nine modules, the `guid` primitive type and the stability
calibration. Sliced into six on the dependency seams:

- **A — Plugin & native-module infrastructure + module-namespace resolution.** *The
  load-bearing increment.* The `IGrobPlugin` contract in `Grob.Runtime`, the new
  `Grob.Stdlib` project, CLI auto-registration at VM startup; the compile-time
  **module-namespace resolution model** (D-339 — a namespace name-category in the
  checker, member access folded to a qualified native, emitted as function-constant +
  `Call`); the **native-throw seam** (a native raising a catchable `GrobError` that
  unwinds the Sprint-7 handler table); and the **capability-injection seam** (D-340,
  refining D-319 — the four interfaces Sprint 8 consumes, with OS-backed defaults
  injected by the CLI). Formalise `print`/`exit` as stdlib registrations. Proved
  end-to-end with a minimal `math` vertical (`math.pi` constant, `math.sqrt` — one
  pure native, one throwing native). The module-resolution dispatch sub-problem is the
  Opus carve-out. Branch `feat/stdlib-infrastructure`.
- **B — `math` (complete), `path`, `strings.join`.** The pure and near-pure breadth
  over the A foundation. `math` completed — all trig/log/pow, the domain-error throws
  (`ArithmeticError` on `sqrt`/`log`/`asin`/`acos` out of domain), and
  `random`/`randomInt`/`randomSeed` on the A-established `IRandomSource`. `path` — the
  full decompose/join/normalise surface, Windows-native separators. `strings.join`.
  Branch `feat/stdlib-pure`.
- **C — `env`, `log`, `input()`.** The host-capability and CLI-touching modules.
  `env` via `IEnvironment` (`get`→`string?`, `require`→`string` throwing
  `LookupError`, `set`/`has`/`all`). `log` — four levels to stderr via
  `IStandardStreams`, `setLevel`, the `--verbose` CLI flag gating `debug` visibility.
  `input()` — via stdin, `IoError` on closed stdin. Branch `feat/stdlib-host`.
- **D — `guid` module + the `guid` primitive type.** The new primitive: type-registry
  registration, `Struct`-discriminated `GrobValue` storage (boxed `Guid`, **no new
  `GrobValueKind` variant** — D-303), checker distinctness (`guid == string` is a
  compile error via the existing mismatch code), VM value equality on `Equal`/
  `NotEqual`, a **registered `toString()`** so `ValueDisplay` (D-336) renders the
  canonical string rather than structural fields, generation (`newV4`/`newV7`/
  deterministic variadic `newV5`), `parse`/`tryParse` (`ParseError`/nil), the
  well-known namespaces, `guid.empty`, `version`/`isEmpty`, and the D-149 compile-time
  literal validation. Branch `feat/guid`.
- **E — `formatAs`.** `table`/`list`/`csv`; the **compile-time column derivation**
  from the Sprint-6 field registry; the chained-form **compiler rewrite**
  (`<expr>.formatAs.table()` → `formatAs.table(<expr>)`, the D-320 mechanism); the
  namespace-misuse compile errors (bare `.formatAs`; unknown `.formatAs.X`); cell
  rendering through `ValueDisplay.Inspect`/`Display` (D-336) with culture-pinned
  floats. Branch `feat/formatas`.
- **F — Sprint close.** The stability-calibration ritual (single-iteration
  characterisation against the Sprint-8-runnable script set → locked iteration/warmup/
  tolerance in `stability.json`, D-302 addendum), the stability test itself producing
  a first passing run, the **sixth** VM-execution benchmark baseline against the
  two-axis gate (D-313/D-333), the gold-mastered close-gate smoke script
  (`stdlib.grob`, the D-337 family) and the § acceptance. Branch `feat/stdlib-close`.

Run them in order, each building and testing green before the next, a fresh chat per
increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **Infrastructure first (A), because every module resolves against it.** A `math.sqrt`
  call needs the module-namespace name-category and the qualified-native emission; a
  throwing native needs the native-throw seam; an `env` call needs the capability
  injection. Standing all three up first — and proving them end-to-end with one pure
  and one throwing `math` function before the full module set exists — nails the
  resolution, throw and injection discipline the rest reuse. A also carries the first
  **module namespace** the type system exercises; get the dispatch right once, here.
- **Pure breadth before capability breadth (B before C).** `math`/`path`/`strings` are
  pure or draw only on `IRandomSource`; `env`/`log`/`input` draw on `IEnvironment` and
  `IStandardStreams` and touch the CLI (`--verbose`). Build the wide-but-mechanical
  pure surface on the proven qualified-native path first, then let C carry the
  injection seam under real load. The Sprint 6 nominal-before-structural rhythm.
- **The two heaviest module features late (D, E).** The `guid` primitive type (a new
  registered type, `GrobValue` storage, equality, `ValueDisplay` registration and
  compile-time literal validation) and `formatAs`'s field-registry-driven terminators
  (compile-time column derivation, the chained-form rewrite) are the two features that
  are more than a native registration. They sit after the seam is stable so their own
  complexity is the only new risk in the increment.
- **Close last (F), off the feature path.** The calibration ritual, the benchmark
  baseline and the smoke script round off the sprint once A–E are green.

## The one new structural decision — the module-namespace resolution model (D-339)

This is the single load-bearing call of the sprint and it is settled here so the
increment prompts can cite it. Record it as the next-free `D-###` (provisionally
**D-339**, confirmed against the live decisions-log tail when Increment A lands — the
same verify-before-allocate discipline the error codes follow, and the reason Sprint
7's provisional D-332 landed as D-334) extending **D-282** and **D-320**.

D-282 and D-320 already made `formatAs` and `select` reserved identifiers and coined
the phrase "compiler-namespace" for `formatAs`, but no decision generalises the
mechanism to the core module set, nor specifies the type checker's member-access
dispatch when the receiver is a namespace rather than a value or a type. What is
decided now:

- **Core modules are compile-time namespaces, not runtime values.** `math`, `path`,
  `env`, `log`, `guid` and `formatAs` are registered as a distinct **namespace
  name-category** in the global scope, resolvable by the checker before any script is
  checked — neither value bindings nor type bindings. A namespace name in value
  position (`x := math`, `print(path)`) is a compile error.
- **Member access on a namespace folds to a qualified native.** At a member-access
  node `x.y`, the checker's dispatch precedence is fixed and total: **namespace**
  receiver → the member is a qualified native (`"math.sqrt"`) or a namespace constant
  (`math.pi`, `guid.namespaces.dns`), resolved from the registry; **reserved
  `formatAs`** receiver → the `formatAs` transform (Increment E); **value of a
  built-in type** → an instance method (D-070/D-071, existing); **struct value** →
  a field access / `GetProperty` (existing). An unknown member on a namespace is a
  dedicated compile error, not the value-position undefined-member code.
- **No runtime module object, no new opcode.** A resolved qualified native is emitted
  as a function constant followed by the existing `Call`. There is no module value on
  the stack, no `GetProperty` against a namespace, no `Import` for a core module. The
  `Import` opcode remains reserved for third-party `import Grob.Http` plugins.
- **Natives may throw catchable `GrobError`s.** A native implementation that raises a
  `GrobError` (e.g. `math.sqrt(-1.0)` → `ArithmeticError`) enters the same unwinding
  path Sprint 7 built — the handler table, the `finallyOffset`, the top-level
  diagnostic — through **one** mechanism, not a bespoke native-error path. This is the
  native-throw seam, proved in A by `math.sqrt`'s domain error and reused by every
  throwing native in B–E.

Rationale: the compile-time-namespace model keeps `GrobValueKind` and the opcode set
closed (a runtime module object would need either a new discriminator or a
`GetProperty` arm against namespaces), matches the "no runtime type checks — the
checker already verified correctness" philosophy (§3.3 rationale), and unifies with
the `formatAs`/`select` mechanism D-282/D-320 already shipped. The one genuinely new
piece is the checker's member-access dispatch precedence — hence the Opus carve-out.

## The second new structural decision — capability-injection seam scope (D-340)

Provisionally **D-340**, refining **D-319** (which already decided the seam exists and
its playground shape). D-319 names six capability interfaces in `Grob.Runtime`:
`IFileSystem`, `IEnvironment`, `IProcessRunner`, `IStandardStreams`, `IClock`,
`IRandomSource`. Sprint 8 realises the seam and lands **only the four it consumes** —
`IEnvironment` (Increment C `env`), `IStandardStreams` (Increment C `log`/`input`,
Increment A `print`), `IClock` (Increment D `guid.newV7`) and `IRandomSource`
(Increment B `math.random*`, Increment D `guid.newV4`). `IFileSystem` and
`IProcessRunner` land in Sprint 9 with `fs` and `process`. The **injection mechanism**
— OS-backed default implementations constructed by `Grob.Cli` and passed to the VM
registration surface — is established once, in A, so Sprint 9 wires two more
implementations into a proven seam rather than retrofitting the seam itself. The DAG
holds: capability interfaces in `Grob.Runtime`, consumed by `Grob.Vm` and
`Grob.Stdlib`, injected by `Grob.Cli`; no new cross-edge, and the `Grob.Compiler` ↔
`Grob.Vm` non-reference invariant is untouched.

## Planning constraints recorded here (durable context for the increment prompts)

- **Sprint 8 wires far more diagnostics than it mints.** Runtime throws reuse the
  Sprint-7 leaves — `math` domain errors → `ArithmeticError`, `env.require` →
  `LookupError`, `input()` on closed stdin → `IoError`, `guid.parse` on a bad string
  → `ParseError` at the existing **E5701**. `guid == string` reuses the existing
  type-mismatch code. **E1103** (reserved identifier as a binding name, D-320) already
  ships. The new codes are **compile-time** and few — confirm every code against the
  live registry before use, and if a diagnostic needs a code not already present,
  follow `allocating-an-error-code` (D-331); do not invent a literal.
- **Error-code budget — two or three new codes, each a fold-vs-new call in-increment.**
  All confirmed next-free against the **live** registry at their increment (the base
  in the current snapshot is **116**, with E0014/E0015 in the Type block, E2213 in the
  Syntax block and E5701 already present):
  - **module namespace used as a value / unknown member on a namespace** — the
    D-339 dispatch's two error arms (`x := math`; `math.nope()`). Whether these are
    one code with two messages or two codes, and whether they sit in the Name-resolution
    block (beside E1103, next-free provisionally **E1104**) or the Type block, is an
    `allocating-an-error-code` fold-vs-new call to make in **Increment A**. Lean: one
    code, two messages, Name-resolution — the two arms are one concept (a namespace is
    not a value and has a closed member set).
  - **`formatAs` namespace misuse** — bare `<expr>.formatAs` (no method call) and
    `<expr>.formatAs.X` where `X` is not `table`/`list`/`csv`. The stdlib reference
    mandates both diagnostics by name. Fold-vs-new against the D-339 namespace code
    made in **Increment E**. Lean: fold into the D-339 namespace-misuse code — `formatAs`
    is a namespace and this is the same "not a value / closed member set" concept —
    with a `formatAs`-specific message naming the three valid methods.
  - **compile-time `guid` literal validation** — `guid.parse("not-a-guid")` on a
    string **literal** argument (D-149) caught at compile time, distinct from the
    runtime **E5701**. Fold-vs-new in **Increment D**. Lean: dedicated compile code —
    E5701 is explicitly Runtime and re-using a Runtime code for a compile diagnostic
    breaks the category scheme (ADR-0014).
  Each new code is registered in three-location lockstep (summary row, full entry,
  footer changelog), the count reconciled, and the D-316 consistency gate asserts
  catalog↔registry agreement on the commit.
- **`guid` is `Struct`-discriminated, not a new `GrobValueKind` variant (D).** D-303
  fixed the nine-variant tagged union; `date`, `guid`, `File`, `ProcessResult` and
  `json.Node` all share the `Struct` discriminator, reference-stored, so plugin and
  built-in reference types never grow `GrobValueKind`. `guid`'s primitive
  distinctness is therefore a **type-checker and type-registry** concern (a registered
  `GrobType`, `guid == string` rejected at check time), not a discriminator concern.
  At runtime a `guid` value is a `Struct`-discriminated `GrobValue` carrying a boxed
  `System.Guid`; `Equal`/`NotEqual` compare it by value. Do not add a `GrobValueKind`
  case; if you reach to, stop and surface.
- **`guid` must register a `toString()` so D-336 renders it, not its fields.** D-336's
  dispatch places a registered `toString()` (step 2) ahead of structural rendering
  (step 5) precisely so that a `Struct`-discriminated primitive is not rendered as its
  internal fields. `guid` registers `toString()` (canonical lowercase-hyphenated),
  and `print(id)` / `"${id}"` therefore emit the canonical string, not a structural
  form. This is the same registered-`toString()` precedence that keeps `AuthHeader`
  opaque — verify it holds for `guid` on the real print and interpolation paths.
- **The `formatAs` chained-form rewrite is in v1 (E).** It was retired from the
  scope-cut list (requirements §"not on the list"), simplified via the D-282/D-320
  reserved-identifier mechanism, and stays in v1. `<expr>.formatAs.table()` parses as
  member access already (D-320); Increment E adds the compile-time rewrite to the
  function form and the checker's receiver-type validation. Do not defer it.
- **The stability calibration runs against the Sprint-8-runnable script set, not all
  thirteen (F).** `grob-benchmarking-strategy.md` §6 step 1 says the ritual
  characterises "all thirteen" validation-suite scripts, but scripts 3/5/7/8/12 depend
  on `fs`/`csv`/`process`/`regex` — all Sprint 9. They cannot run at Sprint 8 close.
  The calibration characterises the trio (iteration/warmup/tolerance) against the
  scripts that **do** run at Sprint 8 — the five smoke scripts (`hello`, `calculator`,
  `functions`, `types`, `errors`) plus `stdlib.grob` and any validation script whose
  modules all exist — and the full-thirteen stability run becomes a v1 release-gate
  step. Increment F records this as a **D-302 calibration addendum** and a mechanical
  correction to `grob-benchmarking-strategy.md` §6 citing D-302 — surfaced, not swept.
  No new decision number beyond the addendum.
- **`guid.newV5` and `print` are variadic natives.** The language has no user-facing
  variadic function declaration (§ fundamentals), but variadic **natives** are
  supported — `print` proves it. `guid.newV5(namespace, ...names: string)` concatenates
  its variadic name segments before hashing (D-149). Register it as a variadic native
  the same way `print` is registered; do not invent a user-facing variadic grammar.

## The close-gate

Sprint 8 closes (Increment F) on `grob run stdlib.grob` over a script that exercises
the Sprint-8 module surface — `print`, a `math` call, a `path.join`, an `env.get`/
`require` with a caught `LookupError`, `log.info`/`log.error` to stderr, a `guid`
generation and `parse` with interpolation, and a `formatAs.table()` over a small
struct array — using **no** Sprint-9 module (`fs`, `json`, `csv`, `date`, `regex`,
`process`). Like the prior five, it is a planning-defined smoke test gold-mastered
under `Grob.Integration.Tests`, its contract being stdout, stderr **and** exit code
(D-337). Sprint 8 is closeable when Increment F's acceptance is green and the sixth
VM-execution benchmark baseline passes the two-axis gate (D-313/D-333) and the
stability test produces its first passing run against the locked calibration numbers.

## Acceptance to hit (whole sprint)

The § acceptance, met across the six increments: each module's full API works;
`math.sqrt(9.0)` returns `3.0`; `math.sqrt(-1.0)` throws a catchable `ArithmeticError`;
`env.require("MISSING")` throws `LookupError`; `log.error()` writes to stderr;
`input()` reads a line from stdin and throws `IoError` on a closed stream;
`guid.newV4()` produces a v4 GUID and `guid.parse` rejects a bad string; `guid ==
string` is a compile error; `formatAs.table()` produces aligned column output and the
chained `.formatAs.table()` form compiles to the function form; a module namespace in
value position and an unknown namespace member are compile errors; `grob run
stdlib.grob` runs; the stability-test calibration ritual is complete with locked
numbers in `stability.json` and a first passing run. Sprint 8 is closeable when
Increment F's acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh corpus zip uploaded at the start of
  each increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 8 code is C# 14 from the first line.
- **`ErrorCatalog` (D-308).** Every diagnostic references a central catalog
  descriptor; the `"Exxxx"` string for any code appears exactly once. The new Sprint 8
  codes go in `grob-error-codes.md` and the catalog at their next-free numbers in
  their increments — never as a literal at the call site, never invented. The
  consistency gate (D-316) asserts catalog↔registry agreement on every commit.
- **`Grob.Stdlib.Tests` is new and must be in `Grob.slnx`.** Increment A creates it;
  the D-335 membership check in `Grob.Consistency.Tests` enforces its presence. Route
  module-implementation tests here; type-checker/compiler tests to `Grob.Compiler.Tests`,
  VM tests to `Grob.Vm.Tests`, smoke/gold-master to `Grob.Integration.Tests`, per §3.5.
- **Tests are plain xUnit.** `FsCheck` and `FluentAssertions` are not in
  `Directory.Packages.props` and are not used — `[Theory]` rows cover the layer
  invariant for pipeline work; follow the `tdd-cycle` skill.
- **§3.1.1 holds on every new node.** Every identifier and member node the new surface
  introduces carries a non-null `ResolvedType` and a non-null `Declaration` (the LSP
  day-one properties), asserted by reference (`Assert.Same`).
- **Coverage.** The affected projects — including the new `Grob.Stdlib` — hold at or
  above the 90% line+branch floor (D-328, ADR-0018); nothing is excluded to make the
  number.
- **§ (Sprint 8) stays as written**, save the mechanical `grob-benchmarking-strategy.md`
  §6 calibration-set correction in F. Whether to rewrite the § to the A–F structure is
  a documentation-authority call deferred to Sprint 8 close, as it was for §5, §6 and
  §7.
- **Model (D-314).** Sonnet 4.6 (High) is the code-gen workhorse throughout. The one
  Opus carve-out is the module-namespace member-access dispatch sub-problem in
  Increment A, run through an Opus 4.8 subagent (the Sprint 5 D `grob-closure-specialist`
  mechanism).
- Start with `/sprint-8-a`.
