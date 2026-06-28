---
description: "Sprint 6 · Increment B — named construction and field defaults. TypeName { f: v } construction, required-field validation (E0103), unknown-field E0012 (registered here, D-330, count 109→110), construction-site default-expression emission (the Sprint 5 B default-argument shape), nested construction, E2102, NewStruct emission and dispatch, the runtime struct value."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 6 · Increment B — named construction and field defaults

Increment A registered and validated type declarations. This increment makes a type
**constructible**: `TypeName { field: value }` validates that every required field is
supplied (**E0103**) and every named field exists (**E0012**, the one new code this
sprint), fills omitted defaulted fields by compiling each default expression into the
construction site, builds the runtime struct value through the `NewStruct` opcode arm,
and handles nested construction. It sits on Increment A's registry and on Sprint 5 B's
default-expression-at-call-site machinery, which it reuses verbatim for field defaults.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 6 construction bullets (named
   construction, required-field validation, defaults, nested construction), §3.1.1,
   §3.3 (confirm `NewStruct` is present in the closed enum), §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§10** construction and **§10's
   field-default-evaluation** rules: construction is named and unordered; all field
   names must match the declared type; omitting a required field is an error; defaults
   evaluate at construction time in the **construction-site scope**; a default may
   reference any identifier in scope at the construction site but **not** a sibling
   field of the type being constructed; a supplied field's default does not evaluate.
   Grep §10 for the sibling-reference prohibition and the construction-site-scope rule.
3. `docs/design/grob-vm-architecture.md` — the runtime struct value representation and
   the `NewStruct` handler sketch (operand: type index; the field set assembled on the
   stack).
4. `docs/design/grob-error-codes.md` — confirm **E0103** (non-nullable field requires
   initialiser — the missing-required-field case) and **E2102** (empty type
   construction missing `{ }`) exist with their descriptors, and confirm the **next
   free E00xx Type-block number is E0012** before registering it.
5. Decisions: confirm **D-330** (unknown-field-at-construction → dedicated E0012, not a
   fold into E1002; registered in this increment; count 109 → 110), and the Sprint 5 B
   decision governing **default parameter values** (the construction-site-emission shape
   this increment mirrors — grep for the default-argument decision). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep §10 to confirm the
> construction and default-evaluation rules, and the decisions log for D-330 and the
> default-argument decision. Confirm E0012 is genuinely the next free E00xx number in
> the registry before assigning it. If a section has moved or a number is taken,
> surface it rather than proceeding.
>
> **Construction rules — inline reference (authoritative source is §10 and
> `grob-vm-architecture.md`; reproduced here so the implementation does not depend on a
> fetch landing well).**
>
> - **Named construction.** `TypeName { field: value, ... }` is an **expression** that
>   produces a value of that type, and may appear anywhere that type is expected — so
>   nested construction (`Person { address: Address { city: "London" } }`) needs no
>   special case. Fields are **named and unordered**; declaration order need not match.
> - **Required-field validation (E0103).** Every field with no default is required.
>   Omitting a required field at construction is **E0103** with a source location.
> - **Unknown-field validation (E0012, the new code).** A field name at the
>   construction site that is not declared on the type is **E0012** "unknown field
>   name", with a source location. This is a **dedicated** code (D-330), the
>   construction analogue of E0011 "unknown parameter name" — **not** a fold into E1002
>   (which is member *access* on an existing value). Register E0012 in the registry and
>   the `ErrorCatalog` in this increment; raise it through its descriptor — no `"E0012"`
>   literal at the call site.
> - **Field defaults — construction-site emission (the Sprint 5 B shape).** A default
>   expression is compiled into **each construction site that omits the field**, exactly
>   as Sprint 5 B compiled a default *argument* expression into each call site that
>   omitted the parameter. The default evaluates at **construction time** in the
>   **construction-site scope** — it sees the `const`/`readonly`/mutable bindings,
>   function names and modules in scope where the construction is written. A default may
>   be any expression legal in a general expression position (literal, call,
>   interpolated string, method chain, anonymous-struct literal, nested construction) —
>   there is **no** compile-time-constant restriction. A default may **not** reference a
>   sibling field of the type being constructed (siblings may not yet be assigned) — that
>   reference is a compile error; confirm the code against the registry and surface if
>   none fits. When a construction supplies a field explicitly, its default does **not**
>   evaluate (no side effects for supplied fields). When a construction omits a field,
>   its default evaluates **once** for that construction.
> - **`NewStruct` emission and dispatch.** Compile a named construction to: the field
>   values assembled on the stack (supplied values and emitted defaults, in a
>   deterministic field order), then `NewStruct` with the type index. The VM handler
>   builds the runtime struct value from the stack. Match the field-ordering and stack
>   arithmetic in `grob-vm-architecture.md` exactly. `NewStruct` already exists in the
>   closed enum (Sprint 2 A) — this increment writes its compiler-emission and VM-dispatch
>   arms; it does not add an enum case.
> - **E2102 empty construction.** A named construction written without the required
>   `{ }` body — even when every field is defaultable — is **E2102**.
> - **No field access yet.** Reading or assigning a field on a constructed value
>   (`.field`, `.field = v`) is Increment C. This increment constructs values; it does
>   not read or mutate them. The smoke check is that a constructed value round-trips
>   (e.g. printed, or compared) — not field access.
>
> **Sequencing note.** This is Increment B: A (declarations) → **B (construction +
> defaults)** → C (access + assignment) → D (anonymous structs) → E (close). Do not pull
> field access, assignment, anonymous structs or the closure-in-field escape forward.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the type-checker construction
   validation (E0103, E0012, sibling-reference rule), the default-expression emission,
   the `NewStruct` compiler emission, the VM dispatch arm and the runtime struct value,
   the E0012 registration (registry + catalog, count 109 → 110, three-location
   lockstep), and the tests — and wait for Chris's approval. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/struct-construction`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests confirmed **failing** first. Follow the `tdd-cycle`,
   `defining-a-type` and `adding-an-opcode` skills (the `NewStruct` arm follows the
   emit-and-dispatch-together discipline even though the enum case already exists — the
   enum step is a no-op; emission and dispatch are not).
4. Run `/commit-message` against the staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–5** as in Increment A's hand-off, plus **Increment A**: the type
  registry with field types resolved through the full §9 grammar, the §17.1 cycle
  detection (E0301/E0302), the E1102 type-name collision, the E2208 duplicate field and the
  §17.1 `E—cycle` → E0301/E0302 spec-drift fix. The registry is the surface this
  increment constructs against.
- **Sprint 5 B's default-argument machinery** — default expressions compiled into each
  omitting call site, evaluated at call time — is the precedent this increment reuses
  for field defaults. The shape is identical; only the site (construction vs call)
  differs.

## Deliverable for this increment

Named construction with required/unknown-field validation, field defaults and the
runtime struct value, across the type checker, the compiler and the VM. **No field
access or assignment, no anonymous structs.**

1. **Type checker — construction sites.** Validate that every required field is
   supplied (**E0103**) and every named field is declared on the type (**E0012**);
   reject a default that references a sibling field; annotate the construction
   expression with the constructed type. Set `ResolvedType` and `Declaration` on every
   identifier node; the §3.1.1 invariant holds.
2. **Register E0012 (D-330).** Add E0012 "unknown field name" to `grob-error-codes.md`
   (summary row, full entry, total **109 → 110**) and to the `ErrorCatalog`, in
   three-location lockstep. Confirm the next-free number first. The D-316 consistency
   gate must be green on the commit.
3. **Compiler — construction.** Emit each supplied field value and each emitted default
   (for omitted defaulted fields) in deterministic field order, then `NewStruct` with
   the type index. A supplied field's default is not emitted.
4. **Compiler — defaults.** Compile each default expression into each construction site
   that omits the field, in the construction-site scope, mirroring the Sprint 5 B
   default-argument emission.
5. **VM — `NewStruct` dispatch.** Build the runtime struct value from the assembled
   stack against the representation in `grob-vm-architecture.md`.
6. **E2102** for a named construction missing its `{ }` body.
7. **Diagnostics via `ErrorCatalog` (D-308).** E0103, E0012, E2102 (and the sibling-
   reference code) raise through catalog descriptors. No literals; no invented codes.

No new opcodes beyond wiring the closed-enum `NewStruct`, no parser or AST edits, no
field access or assignment.

## Out of scope

Field access and assignment, `GetProperty`/`SetProperty`, nested access and the
closure-in-field escape (Increment C). Anonymous structs and the brace-disambiguation
diagnostic (Increment D). Do not edit the parser, the AST or the `OpCode` enum.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A construction omitting a required field is **E0103**; a construction naming a
    field the type does not declare is **E0012**; both carry source locations.
  - A construction supplying every field compiles with no default expression emitted; a
    construction omitting a defaulted field emits that field's default at the
    construction site.
  - A default that references a sibling field of the type being constructed is a compile
    error with a source location.
  - Nested construction (`Person { address: Address { ... } }`) type-checks and
    disassembles to the inner `NewStruct` then the outer `NewStruct`.
  - A construction disassembles to the field values (supplied and defaulted) followed by
    `NewStruct` with the correct type-index operand (verified through the disassembler,
    not only the VM's answer).
  - **E2102** fires on a named construction missing `{ }`.
  - §3.1.1 invariant holds for every identifier node introduced.
  - Layer-invariant `[Theory]` rows: pathological but parseable constructions type-check
    to a result or a diagnostic, never throw.
- **VM tests (`Grob.Vm.Tests`):**
  - A hand-built chunk for a construction produces a struct value with the supplied and
    defaulted fields set correctly, and leaves the stack at the expected depth.
  - A defaulted field whose default has a side effect (e.g. a counter-incrementing
    call) evaluates its default exactly once when omitted, and not at all when supplied.
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script declaring a type with a default and constructing it both ways (field
    supplied / field omitted) runs end-to-end via `grob run`.
- **Consistency:** the D-316 gate is green; error-code count is 110; catalog↔registry
  agreement holds.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Named construction validates required and unknown fields (E0103, E0012), fills
  defaults at the construction site, evaluates each omitted default once and supplied
  defaults never, handles nested construction and builds the runtime struct value via
  `NewStruct`.
- E0012 is registered in three-location lockstep; count is 110; the D-316 gate is green.
- The §3.1.1 invariant holds; the DAG holds; diagnostics raise through catalog
  descriptors; no literals, no invented codes.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. Construction and defaults are settled by §10, the runtime
shape by `grob-vm-architecture.md`, and the one new code is pre-assigned by D-330 — this
is transcription over a specified machine, plus the default-emission shape already built
in Sprint 5 B. No Opus carve-out.

## Hand-off

Summarise: the construction validation and its codes (E0103, E0012, sibling-reference),
the E0012 registration as landed (count 110, lockstep), the default-expression emission
as built, the `NewStruct` arm, the runtime struct value's shape and the test files
added. Note for the next chat: Increment C is field access and assignment — `.field`
(`GetProperty`), `.field = v` (`SetProperty`), nested `a.b.c`, undefined-member (E1002),
readonly-field mutation (E0204), **and the mandatory D-325 closure-in-field escape
regression**.
