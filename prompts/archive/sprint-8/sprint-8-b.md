---
description: "Sprint 8 · Increment B — `math` (complete), `path`, `strings.join`. The pure and near-pure module breadth over the A foundation: all trig/log/pow, the ArithmeticError domain throws through the native-throw seam, random/randomInt/randomSeed on the A-declared IRandomSource; the full path decompose/join/normalise surface with Windows-native separators; strings.join. No new opcode, no new structural decision. Registration and native bodies over the D-339 machinery A stood up."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 8 · Increment B — `math` (complete), `path`, `strings.join`

Increment A stood up the plugin infrastructure, the module-namespace resolution model
(D-339), the native-throw seam and the capability-injection seam (D-340), and proved
them with `math.pi`/`math.sqrt`. This increment is the first **breadth** increment:
complete `math`, add `path`, add `strings.join` — the modules that are pure or draw
only on `IRandomSource`. It is registration and native-body work over the machinery A
built; there is **no** new structural decision and **no** new opcode.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 8 `math`, `path` and `strings`
   signature lists, §3.5 (test routing).
2. `docs/design/grob-stdlib-reference.md` — the **`math`** section in full (the
   complete signature set; the "no duplication of type-level functions" table — `abs`,
   `floor`, `min` etc. live on the type registry, **not** `math`; the `ArithmeticError`
   domain-throw sites; the random semantics and the per-execution PRNG). The `path`
   one-liner (the requirements § carries the full signatures). `strings.join`.
3. Decisions: **D-339** (module-namespace resolution — the machinery every function
   here registers into), **D-340** (`IRandomSource` was declared in A; B is its first
   consumer), **D-070**/**D-071** (the type-level vs module-namespace split — do not
   duplicate `abs`/`floor`/`min`/`clamp` onto `math`), **D-334** (the native-throw seam
   the domain errors reuse), **D-308** (`ErrorCatalog`). Grep to confirm.

> **Verify before relying on cited sections.** Grep `grob-stdlib-reference.md` for the
> `math` domain-throw list and the type-level-function table, and confirm A actually
> landed `IRandomSource` on the injection seam. If A's hand-off and the merged tree
> disagree, surface it before building.
>
> **Module rules — inline reference (authoritative source is the `math`/`path`/
> `strings` sections and the requirements §; reproduced here).**
>
> - **`math` is completed, not re-founded.** A registered `math.pi`/`math.sqrt`. B adds
>   the remaining constants (`e`, `tau`) and functions (`pow`, `log`, `log10`, `sin`,
>   `cos`, `tan`, `asin`, `acos`, `atan`, `atan2`, `toRadians`, `toDegrees`, `random`,
>   `randomInt`, `randomSeed`) — all through the same `IGrobPlugin`/qualified-native
>   path A established. No `abs`/`floor`/`ceil`/`round`/`truncate`/`min`/`max`/`clamp`
>   on `math`: those live on the type registry (D-070/D-071) and are out of scope here.
> - **The domain-error throws reuse the native-throw seam (D-334).** `math.sqrt(x < 0)`,
>   `math.log(x <= 0)`, `math.log10(x <= 0)`, `math.asin`/`math.acos` outside `[-1, 1]`
>   throw a catchable `ArithmeticError` through the handler table — the same mechanism
>   A proved. Functions with a defined IEEE 754 result the caller might want do **not**
>   throw: `math.pow(0.0, -1.0)` → `+Infinity`, `math.pow(-2.0, 0.5)` → `NaN`,
>   `math.atan2(0.0, 0.0)` → `0.0`, and `NaN`/`±Infinity` propagate silently. Only the
>   initial domain-error throw is explicit (the D-278 rule — no silent domain errors).
> - **Random is per-execution, seeded from the clock unless `randomSeed` is called
>   (D-340 `IRandomSource`).** `math.random()` → uniform `float` in `[0.0, 1.0)`;
>   `math.randomInt(min, max)` → uniform `int` in `[min, max]` **inclusive both ends**;
>   `math.randomSeed(seed)` → deterministic sequence for replay. No shared global state
>   survives VM exit. The PRNG is reached only through `IRandomSource`, never
>   `System.Random` directly in `Grob.Stdlib`.
> - **`path` is Windows-native.** `join`, `joinAll`, `extension`, `filename`, `stem`,
>   `directory`, `resolve`, `normalise`, `isAbsolute`, `isRelative`, `changeExtension`,
>   and the `separator` constant. Extensions are always normalised to the leading-dot
>   lowercase form (`".jpg"`). `path` is pure — no host capability, no throw sites
>   beyond ordinary argument handling.
> - **`strings.join(parts: string[], separator: string): string`** is the only `strings`
>   module function; every other string operation is an instance method on the `string`
>   type (already in the registry, D-071). Do not add string instance methods here.
>
> **Sequencing note.** Increment B of A → **B** → C → D → E → F. Do not build `env`/
> `log`/`input` (they need `IEnvironment`/`IStandardStreams` and the `--verbose` flag —
> Increment C), the `guid` type (D) or `formatAs` (E). This increment is pure/near-pure
> module breadth only.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the completed `math` surface with
   its domain throws, `path`, `strings.join`, and the tests — and wait for approval.
2. On approval, `/start-branch` and propose `feat/stdlib-pure`. Wait for the branch.
3. TDD — tests failing first (`tdd-cycle`).
4. `/commit-message`; stop after the local commit.

## What is already done

Sprints 1–7 and the interlude (see Increment A's summary). **Increment A** landed the
`IGrobPlugin` contract, `Grob.Stdlib`/`Grob.Stdlib.Tests`, the module-namespace
resolution (D-339), the native-throw seam, the capability-injection seam with
`IRandomSource` declared (D-340), the `print`/`exit` formalisation, and the
`math.pi`/`math.sqrt` vertical. Error-code base as A left it.

## Deliverable for this increment

1. **`math` complete.** All constants and functions registered through the A machinery;
   the domain-error throws through the native-throw seam; random on `IRandomSource`.
2. **`path` complete.** The full decompose/join/normalise surface, Windows-native
   separators, extension normalisation.
3. **`strings.join`.** The one module function.
4. **No new opcode, no new structural decision, no new error code** unless a genuinely
   unanticipated diagnostic surfaces — in which case follow `allocating-an-error-code`
   (D-331), surfaced not swept. Reused throws (`ArithmeticError`) mint no code.

## Out of scope

`env`/`log`/`input` (C). `guid` (D). `formatAs` (E). The calibration and close (F).
Type-level functions (`abs`/`floor`/`min`/`clamp` — they live on the type registry,
not `math`, and are not Sprint 8 work). Do not edit the `OpCode` enum.

## Tests

Per §3.5.

- **Stdlib tests (`Grob.Stdlib.Tests`):** `math.pow(2.0, 10.0)` → `1024.0`;
  `math.log(math.e)` → `1.0`; `math.sin(math.pi / 2.0)` → `1.0`; `math.toDegrees(math.pi)`
  → `180.0`; `math.randomSeed(42)` then `math.random()`/`math.randomInt(1, 6)` produce a
  reproducible sequence; `math.randomInt(1, 6)` is inclusive of both ends over many draws.
  `path.join`, `path.extension` (`".jpg"` form), `path.normalise`, `isAbsolute`/
  `isRelative`, `changeExtension`, `separator`. `strings.join(["a", "b"], ", ")` → `"a, b"`.
- **VM tests (`Grob.Vm.Tests`):** `math.sqrt(-1.0)`, `math.log(0.0)`, `math.asin(2.0)`
  throw a catchable `ArithmeticError`; `math.pow(-2.0, 0.5)` returns `NaN` and does
  **not** throw; a `NaN` propagates silently through a subsequent operation. `[Theory]`
  rows: pathological finite/non-finite inputs return a value or a diagnostic, never a
  host exception.
- **Consistency:** D-316 green; catalog↔registry agreement; count reconciled if any
  code was added (expected: none).

## Acceptance

- `dotnet build`/`dotnet test` green. Each `math`/`path`/`strings` signature works to
  its spec; the domain throws are catchable `ArithmeticError`s through the handler
  table; random is reproducible under a seed and per-execution otherwise.
- No `abs`/`floor`/`min`/`clamp` on `math`; no string instance methods added to `strings`.
- No new opcode; no new structural decision; the DAG holds; §3.1.1 holds on any new node.
- Coverage at or above 90% line+branch on the affected projects.

## Model

Sonnet 4.6 (High) throughout. This is registration and native-body work over the D-339
machinery A settled and the D-334 throw seam A proved — no Opus carve-out.

## Hand-off

Summarise: the completed `math` surface and its domain-throw sites; how random routes
through `IRandomSource`; the `path` surface and extension-normalisation rule;
`strings.join`; any diagnostic decisions; the test files added. Note for the next chat:
Increment C is `env`, `log` and `input()` — the host-capability modules on
`IEnvironment` and `IStandardStreams`, and the `--verbose` CLI flag gating `log.debug`.
