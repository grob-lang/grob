---
description: "Sprint 9 · Increment D — `json` + `mapAs<T>` + `json.Node` indexer. THE load-bearing increment. The json.Node plugin type, the node[\"key\"]: json.Node? indexer over A's array-indexer emission, asString/asInt/… throwing JsonError, and mapAs<T>() typed deserialisation — the checker's explicit-type-argument resolution (compile-time arg-count/constraint via existing E0401/E0402) and the runtime coercion (shape mismatch via the existing json.Node/mapAs<T> code). The <T>-consumption dispatch is the Opus carve-out."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment D — `json` + `mapAs<T>` + `json.Node` indexer

This is the **load-bearing increment** of Sprint 9. The `math`-vertical of Sprint 8 was
D-342; the finally-chain of Sprint 7 was D-334; here it is `mapAs<T>()` — the language's
first real **explicit type-argument consumption** at a call site, resolving `<T>` in the
checker and producing a value of that type from a `json.Node` at runtime. It is
release-gate-critical: four validation scripts call `.mapAs<…>()`. Get the
`<T>`-consumption dispatch right, once, here. The increment also lands the `json.Node`
plugin type and its `node["key"]` indexer — which builds directly on the array-indexer
emission Increment A restored.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 9** `json` scope (`read`/`write`/
   `parse`/`encode`/`stdin`/`stdout` with `compact`, the `json.Node` type, indexer
   access, the `asX()` accessors, `mapAs<T>()`), §3.1.1, the solution-architecture §.
2. `docs/design/grob-stdlib-reference.md` — the **`json`** section and the **`json.Node`**
   type (the indexer `node["key"]: json.Node?` returning nil for missing keys and never
   throwing; the `asString`/`asInt`/`asFloat`/`asBool`/`asArray` accessors throwing
   `JsonError` on a wrong-type node; the `isNull`/`isString`/… predicates;
   `mapAs<T>(): T` throwing `JsonError` on shape mismatch).
3. `docs/design/grob-type-registry.md` — the constrained-generics model (D-080 — users
   **consume** generic functions and cannot declare them), how `map<K, V>` and array
   type-refs are resolved as built-in constrained generics, and how a type argument is
   written and resolved.
4. `docs/design/grob-error-codes.md` — `E0401` (generic type argument count mismatch),
   `E0402` (generic constraint violation) and the existing `json.Node`/`mapAs<T>`
   **runtime shape-mismatch** code (`JsonError` category). Confirm all three exist; D
   wires them and is expected to mint few or no new codes.
5. Decisions: **D-080** (constrained generics — consume-not-declare), **D-342**
   (module-namespace resolution), **D-303** (`Struct` discriminator — `json.Node` is
   `Struct`-discriminated), **D-336** (`ValueDisplay` — `json.Node` registers a
   `toString()` returning raw JSON text), **D-284** (the `JsonError` leaf), **D-308**
   (`ErrorCatalog`), **D-112**/**D-347** (the array-indexer emission the `node["key"]`
   indexer builds on). Grep for the next-free D-number for the `mapAs<T>` decision (this
   prompt provisionally names it **D-349**, extending D-080).

> **Verify before relying on cited decisions and sections.** Grep the `json`/`json.Node`
> sections, the type-registry constrained-generics prose, and the three error codes. If a
> signature has moved or a code is absent where assumed, surface it — do not invent a
> code.
>
> **Grammar-first gate (D-331).** `json.read(p).mapAs<Config>()` writes a type argument
> `<Config>` at a call site. Confirm the parser produces the type-argument node the
> checker needs (the `sort<U>` / `map<K, V>` machinery already parses `<…>` in the
> relevant positions). If the type-argument production for a `mapAs<…>()` call is missing
> or malformed, that is a finding — extend it through the `extending-the-grammar` skill,
> surfaced not swept, before building on it. `node["key"]` parses as an index expression
> (D-112) — confirm it reaches the compiler.
>
> **`mapAs<T>` — inline reference (the Opus sub-problem is the `<T>`-consumption
> dispatch).**
>
> - **`mapAs<T>` is constrained-generic *consumption*, not declaration.** D-080 permits
>   users to consume a generic function and forbids declaring one. `mapAs<T>()` is a
>   built-in generic consumed with an explicit type argument. The checker resolves `<T>`
>   to a concrete type (a named struct, or an array-of-struct `T[]` — the sample scripts
>   use both `mapAs<Repo>()` and `mapAs<PsDrive[]>()`) and types the call result as that
>   type. This is the first real use of the machinery against a plugin type — get the
>   resolution and the result-typing right once.
> - **Compile-time diagnostics reuse `E0401`/`E0402`.** A wrong type-argument count is
>   `E0401` (generic type argument count mismatch); a type argument that violates the
>   consumption constraint is `E0402` (generic constraint violation). Both exist — wire
>   them through their `ErrorCatalog` descriptors, do not mint new codes for them.
> - **Runtime coercion reuses the existing shape-mismatch code.** At runtime the
>   `json.Node` is coerced field-by-field into the target type; a JSON shape that does not
>   match the target throws the existing `json.Node`/`mapAs<T>` shape-mismatch `JsonError`
>   (confirm the code number against the live registry) through the native-throw seam
>   (D-334/D-342) — one mechanism, no bespoke coercion-error path.
> - **`json.Node["key"]` never throws.** The indexer returns `json.Node?` — nil for a
>   missing key — and builds on the array-indexer emission Increment A restored. The
>   `asX()` accessors are where a wrong-type node throws `JsonError`; the indexer itself
>   is total.
> - **`json.Node` registers a `toString()`** returning the node's raw JSON text, so
>   `ValueDisplay` (D-336) renders it as JSON, not structural fields. `Struct`-
>   discriminated (D-303), no new `GrobValueKind`.
> - **No runtime module object, no new opcode.** `json` module members resolve through
>   the D-342 namespace model; `mapAs<T>` is a native call with the resolved `<T>` carried
>   as compile-time information the runtime coercion reads — no new opcode, no new
>   `GrobValueKind`. If you reach to add either, stop and surface.

> **Sequencing note.** This is Increment D: C (`fs`) → **D (`json`/`mapAs<T>`/indexer)** →
> E (`csv`). E reuses the `mapAs<T>` machinery stood up here over `csv.Table`. Do not pull
> `csv` forward; stand the machinery up once, here.

## What you're building

1. **The `json.Node` plugin type.** `Struct`-discriminated (D-303), the `asString`/
   `asInt`/`asFloat`/`asBool`/`asArray` accessors (throwing `JsonError` on a wrong-type
   node), the `isNull`/`isString`/`isInt`/`isFloat`/`isBool`/`isArray`/`isObject`
   predicates, a registered `toString()` returning raw JSON text. §3.1.1 on every node.
2. **The `node["key"]` indexer.** Returning `json.Node?`, total (nil for missing),
   building on the Increment A array-indexer emission.
3. **`mapAs<T>()` typed deserialisation.** *The Opus sub-problem.* The checker's
   explicit-type-argument resolution and result-typing; compile-time diagnostics via
   `E0401`/`E0402`; runtime coercion `json.Node → T` (struct or `T[]`) with shape
   mismatch via the existing `JsonError` shape-mismatch code through the native-throw
   seam.
4. **The `json` module natives.** `read`/`write`/`parse`/`encode`/`stdin`/`stdout` with
   the `compact: bool = false` parameter, through the D-342 namespace model; `stdin`/
   `stdout` through `IStandardStreams` (D-343).
5. **The `mapAs<T>` decision (D-349).** Recorded at its real next-free number in
   three-location lockstep, extending D-080.

No new opcode, no new `GrobValueKind` variant. Expected to mint few or no new codes —
`E0401`/`E0402` and the runtime shape code all pre-exist; any genuinely new diagnostic is
a fold-versus-new call via `allocating-an-error-code` (D-331).

## Out of scope

`csv` (Increment E — it reuses this increment's `mapAs<T>` machinery). `process`/`regex`.
Do not edit the `OpCode` enum or add a `GrobValueKind` variant. Do not build a bespoke
generic-declaration mechanism — `mapAs<T>` is consumption only (D-080).

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - `json.read(p).mapAs<Config>()` types the result as `Config`; `mapAs<Config[]>()` types
    it as `Config[]`.
  - `mapAs<>()` / a wrong type-argument count is `E0401`; a constraint-violating argument
    is `E0402`.
  - `node["key"]` types as `json.Node?`; the `asX()` accessors type correctly.
  - §3.1.1: the type-argument node, the indexer node and the accessor nodes carry
    non-null `ResolvedType` and `Declaration` (`Assert.Same`).
- **Stdlib tests (`Grob.Stdlib.Tests`):** the accessors return correct values and throw
  `JsonError` on a wrong-type node; the predicates are correct; `node["missing"]` is nil;
  `mapAs<T>` on a matching shape deserialises and on a mismatched shape throws
  `JsonError`; `read`/`write`/`parse`/`encode` round-trip, `compact` honoured.
- **VM tests (`Grob.Vm.Tests`):** a `mapAs<T>` shape mismatch unhandled produces the
  quality diagnostic and exit 1, and caught by `catch (e: JsonError)` resumes; the
  coercion runs through the handler table; `print(node)` renders raw JSON via
  `ValueDisplay`.
- **Integration / spec-consistency:** D-316 green; catalog↔registry agreement holds; the
  D-349 decision in lockstep; the count reconciled.

## Acceptance

- `json.read().mapAs<T>()` deserialises to a typed value (struct or `T[]`) and rejects a
  shape mismatch with a catchable `JsonError` through the handler table.
- Wrong type-argument count is `E0401`; constraint violation is `E0402`.
- `node["key"]` returns `json.Node?` and never throws; the `asX()` accessors throw
  `JsonError` on a wrong-type node.
- `print(node)` renders raw JSON via `ValueDisplay`.
- No new opcode, no new `GrobValueKind` variant; D-349 logged in lockstep; D-316 green.
- §3.1.1 holds; the DAG holds; coverage at or above 90% — the `mapAs<T>` coercion paths
  especially, not excluded to make the number.

## Model

Sonnet 4.6 (High) drives the increment. The **`mapAs<T>` type-argument resolution and
result-typing** — the checker resolving an explicit `<T>` at a call site against the
constrained-generic consumption model (D-080) and typing the call result, plus the
runtime coercion contract it implies — is the Opus carve-out. Escalate it to an Opus 4.8
subagent (the `grob-closure-specialist` mechanism), config under `.claude/agents/`. The
`json.Node` type registration, the accessors, the indexer over A's emission and the
module natives are Sonnet — registration and straight-line work over settled machinery.

## Hand-off

Summarise: how `<T>` is resolved and the call result typed; the `E0401`/`E0402` compile
arms and the runtime shape-mismatch path through the handler table; the `json.Node` type
and its `toString()`; the `node["key"]` indexer over A's emission; the `json` module
natives; D-349 and its lockstep entry. Note for the next chat: Increment E is `csv` — it
reuses this increment's `mapAs<T>` machinery over `csv.Table`, adds the `CsvRow` indexer
and RFC 4180 parsing.
