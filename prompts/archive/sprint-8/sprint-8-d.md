---
description: "Sprint 8 · Increment D — the `guid` module + the `guid` primitive type. New primitive: Struct-discriminated GrobValue storage (boxed Guid, NO new GrobValueKind — D-303), type-checker distinctness (guid == string is a compile error), VM value equality on Equal/NotEqual, a registered toString() so ValueDisplay (D-336) renders the canonical string not fields, generation (newV4/newV7/deterministic-variadic newV5 on IRandomSource/IClock), parse/tryParse (ParseError/nil), the well-known namespaces, guid.empty, version/isEmpty, and the D-149 compile-time literal validation. No new opcode; no new GrobValueKind variant."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 8 · Increment D — the `guid` module + the `guid` primitive type

Increments A–C stood up the infrastructure and the function-shaped modules. This
increment adds the first **new primitive type** since the language's scalars: `guid`.
It is more than a native registration — it is a registered `GrobType`, a `GrobValue`
storage decision, an equality rule, a `ValueDisplay` registration, an interpolation
path and a compile-time literal check — but it introduces **no** new `GrobValueKind`
variant and **no** new opcode. Get the type-identity and the display-opacity right;
the generation and parsing are ordinary natives on the seams A/B built.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 8 `guid` bullet (the full method
   and property surface; `guid` a primitive distinct from `string`; compile-time
   validation on `guid.parse()` with a string-literal argument), §3.5.
2. `docs/design/grob-stdlib-reference.md` — the **`guid`** section in full (generation
   incl. the variadic `newV5(namespace, ...names)`; the well-known namespaces;
   `parse`/`tryParse`; `guid.empty`; `version`/`isEmpty`; `toString`/`toUpperString`/
   `toCompactString`; `==`/`!=`; `guid == string` a compile error; the map-key
   constraint). Note it lists `toCompactString()` in addition to the requirements §
   surface — reconcile: build the stdlib-reference surface and record the addition.
3. `docs/design/grob-vm-architecture.md` — the `GrobValue` representation (D-303): the
   `Struct` discriminator is shared by `date`/`guid`/`File`/`ProcessResult`/`json.Node`;
   `guid` is reference-stored despite being a 128-bit primitive, boxing `System.Guid`.
4. `docs/design/grob-type-registry.md` — how a built-in primitive type is registered
   and carries instance methods/properties; how `int`/`float`/`string`/`bool` are
   represented; where `guid`'s methods and properties slot in.
5. Decisions: **D-149** (the guid module decision — type semantics, excluded versions,
   compile-time literal validation, the v1 map-key constraint), **D-303** (the settled
   `GrobValue` — no new variant), **D-336** (`ValueDisplay` — a registered `toString()`
   at step 2 renders `guid` as its canonical string, not structural fields; the same
   precedence that keeps `AuthHeader` opaque), **D-339**/**D-340** (the resolution and
   capability seams `guid.newV4`/`newV7` use — `IRandomSource`/`IClock`), **D-334** (the
   native-throw seam `guid.parse` reuses for `ParseError`), **D-308**, **E5701** (the
   existing runtime guid-parse code). Grep the log and the registry.

> **Verify before relying on cited decisions.** Grep `grob-vm-architecture.md` to
> confirm `guid` is `Struct`-discriminated and not a distinct `GrobValueKind`; grep the
> registry for the primitive-type registration pattern; confirm E5701 is a **Runtime**
> code. If any disagrees, surface it before building — a `guid` `GrobValueKind` case or
> a compile-time reuse of the Runtime E5701 would each be a closed-surface breach.
>
> **`guid` rules — inline reference (authoritative source is D-149, the `guid` section,
> D-303 and D-336; reproduced here).**
>
> - **`guid` is a registered primitive `GrobType`, `Struct`-discriminated at runtime.**
>   The checker treats `guid` as a primitive distinct from `string`: `guid == string`
>   (and any `guid`↔`string` assignment/argument) is a compile error at the **existing**
>   type-mismatch code — no new code for this. At runtime a `guid` value is a
>   `Struct`-discriminated `GrobValue` carrying a boxed `System.Guid`. **Do not add a
>   `GrobValueKind` variant.**
> - **Equality is by value.** `guid == guid` and `guid != guid` compare the underlying
>   128-bit value through the **existing** `Equal`/`NotEqual` opcodes — the VM's equality
>   for a `Struct`-discriminated `guid` compares the boxed `Guid`, not reference
>   identity. `guid.empty` (all zeros) equals `guid.empty`.
> - **A registered `toString()` makes `ValueDisplay` render the canonical string
>   (D-336).** `guid` registers `toString()` returning the canonical lowercase-hyphenated
>   form. Because D-336's dispatch places a registered `toString()` (step 2) ahead of
>   structural rendering (step 5), `print(id)` and `"${id}"` emit the canonical string,
>   never the internal fields — the same precedence that keeps `AuthHeader` opaque.
>   `toUpperString()` (ARM endpoints) and `toCompactString()` (storage names) are the
>   other renderings; `id.toCompactString().upper()` composes the uppercase-no-hyphen form.
> - **Generation is on the A/B capability seams.** `newV4()` and the random parts of
>   `newV5` draw on `IRandomSource`; `newV7()` (time-ordered) draws on `IClock`.
>   `newV5(namespace: guid, ...names: string)` is a **variadic native** (the `print`
>   mechanism, kickoff note) — its name segments are concatenated before hashing, so
>   the same inputs always produce the same GUID (Bicep-style idempotent naming). No new
>   user-facing variadic grammar.
> - **Parsing throws or returns nil.** `guid.parse(s)` throws `ParseError` (the
>   native-throw seam) at the **existing runtime E5701** on a bad string; `guid.tryParse(s)`
>   returns `guid?` (nil on failure). **Compile-time literal validation (D-149):**
>   `guid.parse("<literal>")` with a **string-literal** argument is validated at compile
>   time — a malformed literal is a **compile** error, not a runtime `ParseError`. That
>   compile diagnostic needs a code distinct from the Runtime E5701; allocate it
>   next-free per `allocating-an-error-code` (D-331), a fold-vs-new call (lean:
>   dedicated, since E5701 is Runtime and the category scheme (ADR-0014) forbids reusing
>   it for a compile diagnostic).
> - **The namespaces and sentinels are namespace constants.** `guid.namespaces.dns`/
>   `url`/`oid` (the RFC 4122 values) and `guid.empty` resolve as namespace constants
>   through the D-339 machinery (`guid.namespaces` is a nested namespace). `version`
>   (int 4/5/7) and `isEmpty` (bool) are instance properties. The v1 map-key constraint
>   holds — map keys are `string`, so a `guid` key is `id.toString()` (D-149).
>
> **Sequencing note.** A → B → C → **D** → E → F. Do not build `formatAs` (E) or the
> calibration/close (F). This increment is the `guid` type and module only.

## Branching, planning and commits

Not on `main`. Plan mode → approval → `/start-branch` `feat/guid` → TDD (failing
first) → `/commit-message` → stop after the local commit. Flag in the plan whether the
compile-time literal-validation code folds or is dedicated (lean: dedicated).

## What is already done

Sprints 1–7, the interlude, and **Increments A–C**: the infrastructure and resolution
model (D-339), the throw and capability seams (D-340) with `IRandomSource`/`IClock`
declared, `math`/`path`/`strings`/`env`/`log`/`input` complete, `print`/`exit`
formalised. Error-code base as C left it.

## Deliverable for this increment

1. **The `guid` `GrobType`.** Registered as a primitive distinct from `string`;
   `guid == string`/assignment/argument a compile error at the existing mismatch code;
   `Struct`-discriminated `GrobValue` storage (boxed `Guid`, **no new `GrobValueKind`**).
2. **Value equality.** `Equal`/`NotEqual` compare `guid`s by value; `guid.empty`
   round-trips.
3. **Display.** A registered `toString()` (canonical), `toUpperString()`,
   `toCompactString()`; `ValueDisplay` renders the canonical string via the step-2
   precedence; interpolation calls `toString()`.
4. **Generation.** `newV4`/`newV7`/variadic `newV5` on `IRandomSource`/`IClock`.
5. **Parsing.** `parse` (runtime `ParseError`/E5701), `tryParse` (`guid?`), and the
   **compile-time** literal validation (new code, D-149).
6. **Namespaces and properties.** `guid.namespaces.{dns,url,oid}`, `guid.empty` as
   namespace constants; `version`/`isEmpty` instance properties.
7. **The new compile code + the `toCompactString` addition, registered in lockstep.**
   The literal-validation code in `grob-error-codes.md` and the catalog at next-free,
   three-location lockstep, count reconciled, D-316 green; the `toCompactString()`
   addition to the requirements § surface recorded (a stdlib-reference vs requirements-§
   reconciliation, surfaced not swept).

## Out of scope

`formatAs` (E). Calibration and close (F). Any `GrobValueKind` change. Map keys other
than `string` (v1 constraint holds). Do not edit the `OpCode` enum.

## Tests

Per §3.5.

- **Type-checker tests (`Grob.Compiler.Tests`):** `guid` resolves as a primitive;
  `guid == string` and `guid`↔`string` assignment are the mismatch code with a location;
  `guid.parse("not-a-guid")` on a **literal** is the new compile code; `guid.parse(var)`
  on a non-literal is **not** a compile error (runtime path). §3.1.1 holds on the new nodes.
- **Stdlib tests (`Grob.Stdlib.Tests`), against fake `IRandomSource`/`IClock`:**
  `newV4().version` is 4; `newV7().version` is 7 and two successive v7s are time-ordered;
  `newV5(ns, "a", "b")` is deterministic for the same inputs; `parse`/`tryParse`
  round-trip a canonical string; `tryParse` returns nil on a bad string; `guid.empty.isEmpty`
  is true; `toString`/`toUpperString`/`toCompactString` render the three forms;
  `namespaces.dns` is the RFC value.
- **VM tests (`Grob.Vm.Tests`):** `guid == guid` is value equality; `guid.parse(bad)` at
  runtime is a catchable `ParseError` (E5701); `print(id)` and `"${id}"` emit the
  canonical string through `ValueDisplay`, never structural fields.
- **Consistency:** D-316 green; catalog↔registry agreement; count reconciled for the one
  new compile code.

## Acceptance

- `dotnet build`/`dotnet test` green. `guid` is a distinct primitive; `guid == string`
  is a compile error; runtime equality is by value; the three renderings work and
  `ValueDisplay` shows the canonical string; generation and parsing work on the seams;
  the compile-time literal validation fires and the runtime `parse` throws E5701.
- **No new `GrobValueKind` variant; no new opcode.** The DAG holds; §3.1.1 holds.
- The one new compile code registered in lockstep and the count reconciled; the
  `toCompactString` reconciliation recorded; D-316 green.
- Coverage at or above 90% line+branch on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The new primitive follows the settled type-registry and
`GrobValue` patterns (D-303) and the D-336 display precedence — bounded, specified work,
no Opus carve-out.

## Hand-off

Summarise: how `guid` is registered as a primitive and stored `Struct`-discriminated;
the equality rule; the `toString`/`ValueDisplay` registration and how opacity precedence
applies; generation on `IRandomSource`/`IClock` incl. the variadic `newV5`; the runtime
vs compile-time parse split and the new compile code; the namespaces/properties; the
`toCompactString` reconciliation; the test files added. Note for the next chat:
Increment E is `formatAs` — `table`/`list`/`csv`, compile-time column derivation from
the field registry, the chained-form compiler rewrite (D-320), the namespace-misuse
compile errors, and cell rendering through `ValueDisplay`.
