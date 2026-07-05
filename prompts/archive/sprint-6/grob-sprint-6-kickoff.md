# Sprint 6 Kickoff — User-Defined Types

> **A record, not a gate.** Like the Sprint 4 and Sprint 5 kickoffs, this prompt
> does no setup and unblocks nothing. It is not a slash command — it is the durable
> record of the agreed A–E breakdown, the load-bearing ordering calls, the
> error-code positions and the close-gate, kept under `prompts/sprint-6/` with the
> QA brief. The increment commands live in `.claude/commands/sprint-6-{a..e}.md`,
> with archive copies under `prompts/archive/sprint-6/`. **Start by invoking
> `/sprint-6-a`.**

Begin Sprint 6 — user-defined types. Sprint 5 made the language call, return and
close over. Sprint 6 makes it **declare, construct and access structured data** —
the `type` keyword and the type registry, named construction with field defaults,
field access and assignment, anonymous structs, and the field-cycle check. The full
scope and acceptance are in `docs/design/grob-v1-requirements.md` **§7 (Sprint 6 —
User-Defined Types)**. Read it word for word; it is the build contract. The live
construct spec is `docs/design/grob-language-fundamentals.md` **§9** (type-reference
grammar), **§10** (type declarations and construction) and **§17.1** (type cycles).

## Still on Claude Code (D-314)

Sprint 6 runs on the same Claude Code harness — durable rules in `CLAUDE.md`, plan
mode as the approval gate, increment prompts as slash commands, the Husky.NET
pre-push gate, CodeRabbit pre-PR, a PR per increment, `main` protected, GPT-5.3
Codex as the external cold-read via Codex CLI against the merged branch. Archive
copies of the increment commands live under `prompts/archive/sprint-6/`; the kickoff
and QA brief stay under `prompts/sprint-6/`. Full harness rationale in D-314.

**No Opus carve-out this sprint.** Sprint 5 D reached for the `grob-closure-specialist`
subagent on the open/closed-upvalue sub-problem. Sprint 6 has no equivalent: the
struct machinery is settled by §9/§10/§17.1, the diagnostics already exist bar one,
and the one closure interaction (Increment C's field-escape) is **verification over
the D-325 mechanism, not new closure work** — D-325 already made upvalue closing
location-based and route-agnostic so the struct-field route closes identically to the
array and map routes. Sonnet 4.6 (High) throughout. If any increment turns out to
carry a genuine load-bearing structural decision, stop and surface it rather than
reaching for Opus on a task that merely feels hard.

## No new project, no new opcodes

Sprint 6 adds no `src/` project. It is type-checker, compiler-emission and
VM-opcode-arm work over already-parsed nodes. The struct opcodes — `GetProperty`,
`SetProperty`, `NewStruct`, `NewAnonStruct` — were all defined when the `OpCode`
enum was **closed** in Sprint 2 A. Sprint 6 **implements their compiler emission and
VM dispatch arms** for the first time; it does **not** grow the enum. This is the
same shape as Sprint 5 reusing `Call`/`Return`/`Closure` and Sprint 4 reusing the
jump opcodes: the instruction exists, the arm that emits and executes it is the
increment's work. An implementer who reaches to add an `OpCode` case stops and
surfaces — the enum is complete (§3.3, ADR-0013).

The parser and AST are grammar-complete from Sprint 1: `type` declarations, named
construction `TypeName { f: v }`, anonymous-struct literals `#{ f: v }`, member
access `a.b`, member assignment `a.b = v` and the post-D-326/D-327 type-reference
grammar (`int[]`, `T?`, `fn(): T`, `map<K, V>`, grouping) all parse already. No
increment edits the parser, the AST or the `OpCode` enum.

## The agreed increment breakdown

§7 is one section with one acceptance block, carrying type declarations, the type
registry, named construction, field defaults, field access and assignment, nested
access, anonymous structs, the brace-disambiguation rule and field-cycle detection.
Sliced into five on the dependency seams:

- **A — Type declarations, the registry and cycle detection.** Register every
  `type Name { ... }` in the type checker's type registry (pass 1 already registers
  the **name** per D-166; A resolves the **fields**); resolve each field's type
  through the full §9 type-reference grammar (named user types, `T?`, `T[]`,
  `map<K, V>`, `fn(): T`, nested and grouped forms); the §17.1 required-non-nullable
  field-cycle DFS raising **E0301** (multi-type) / **E0302** (trivial self-reference);
  **E1102** when a `type` name collides with any other top-level binding (D-324),
  reported at the second declaration in source order; **E2208** duplicate field name
  in a declaration. The §17.1 `E—cycle` → E0301/E0302 spec drift is fixed here. No
  construction, no runtime, no opcodes. The structural increment — B, C and D all
  reuse the registry. Branch `feat/type-declarations`.
- **B — Named construction and field defaults.** `TypeName { f: v }`: required-field
  validation (**E0103**), unknown-field-name (**E0012**, registered here citing
  D-330), the field-default mechanism — default expressions compiled into each
  construction site that omits the field, evaluated at construction time in the
  construction-site scope (§10), the **same emit-at-site shape** as Sprint 5 B's
  default *arguments*; the no-sibling-field-reference rule; nested construction;
  **E2102** empty construction missing braces; `NewStruct` emission and dispatch
  (closed enum); the runtime struct value. The one new error code lands here.
  Branch `feat/struct-construction`.
- **C — Field access and assignment.** `.field` (**GetProperty**), `.field = v`
  (**SetProperty**), nested `a.b.c` (type checker resolves each step),
  undefined-member access (**E1002**), readonly-field mutation (**E0204**); **and the
  load-bearing cross-cutting test** — a closure stored in a struct field that
  escapes its enclosing function closes its upvalue correctly (D-325, the route the
  Sprint 5 interlude deferred to this sprint). Branch `feat/field-access`.
- **D — Anonymous structs.** `#{ f: v }` (**NewAnonStruct**), the internal
  structural type, type-safe structural field access, anonymous structs in lambdas
  and `.select()` projections (the §7 acceptance vehicle), nested `#{ }`, the
  `{ }` / `#{ }` / `TypeName { }` disambiguation (**E2101**). There is **no**
  `#{...}` type-annotation form — anon structs are inferred, value-position only. The
  structural variant after the nominal path. Branch `feat/anonymous-structs`.
- **E — Sprint close.** The `types.grob` smoke script, the §7 acceptance and the
  fourth VM-execution benchmark baseline against the two-axis gate (D-313). Branch
  `feat/types-close`.

Run them in order, each building and testing green before the next, a fresh chat per
increment with a fresh corpus zip as the known-good state.

## The load-bearing ordering calls

- **The registry first (A), because everything resolves against it.** Construction
  (B), access (C) and anonymous structs (D) all need declared types registered and
  their field types resolved. Standing the registry up first nails the field-type
  resolution discipline the rest reuse, and it is where the §9 grammar gets its first
  heavy **user-written annotation** exercise — the fault line that produced two
  Sprint 5 interludes (D-326, D-327). Get every field-annotation form resolving
  before any value is ever constructed.
- **Construction before access (B before C).** You access and assign fields **on a
  constructed value**, so the runtime struct object and the `NewStruct` path must
  exist before `GetProperty`/`SetProperty` have anything to read or write. B also
  reuses Sprint 5 B's default-expression-at-call-site machinery verbatim for
  field defaults — the binding-then-fill shape is identical, so build it where the
  precedent is freshest.
- **Nominal structs before the structural variant (A–C before D).** A named struct
  resolves its fields against a registered declaration; an anonymous struct
  synthesises an **internal structural type** from the literal and threads it through
  the same field-access machinery C builds. Build the nominal path first, then the
  structural variant on top of it. This is exactly the Sprint 5 lambdas-before-
  closures order — the simpler shared surface first, the harder structural case
  bolted on.
- **Close last (E), off the feature path.** The smoke script and the benchmark
  baseline round off the sprint once A–D are green.

## Planning constraints recorded here (durable context for the increment prompts)

- **Most struct diagnostics already exist — Sprint 6 reuses them, it does not mint
  them.** Unlike Sprint 5, the struct surface was specced with its diagnostics.
  `grob-error-codes.md` already carries **E0301** (type cycle with no terminating
  field), **E0302** (recursive type without indirection — the trivial self-reference
  case), **E0103** (non-nullable field requires initialiser — the missing-required-
  field-at-construction case), **E2102** (empty type construction missing `{ }`),
  **E2208** (duplicate field name in declaration), **E2101** (bare `{` is a block —
  the disambiguation diagnostic), **E1002** (undefined member — field access on a
  value), **E0204** (mutation of `readonly` value, including a `readonly` struct's
  fields) and **E1103** (reserved identifier used as a binding name, covering field
  and parameter names). Each is raised through its existing `ErrorCatalog` descriptor
  (D-308). Confirm every code against the registry before use; if a diagnostic needs
  a code not listed here and not already registered, **stop and surface** — do not
  invent.
- **One error code is assigned this sprint, at the next-free number.** Unknown-field-
  at-construction (`TypeName { notAField: v }`) gets a **dedicated** code **E0012**
  (D-330), not a fold into E1002 (which is member *access* on a value, not a
  construction site). This is the construction analogue of E0011 "unknown parameter
  name" and sits in the E00xx Type block beside E0103. Registered in **Increment B**
  citing **D-330**, raised through an `ErrorCatalog` descriptor — no `"E0012"`
  literal at a call site. Increment B confirms the real next-free number against
  `grob-error-codes.md` before assigning, and updates the registry total **109 →
  110** in three-location lockstep (summary row, full entry, footer changelog). The
  D-316 consistency gate asserts catalog↔registry agreement on the commit.
- **The §17.1 placeholder is stale — corrected mechanically in A.** `grob-language-
  fundamentals.md` §17.1 still prints the placeholder `E—cycle`, but **E0301 already
  replaced it** (D-287, recorded in the registry entry). This is live drift: the
  registry moved on, the spec prose did not. Increment A corrects §17.1 to cite
  **E0301** (multi-type cycle) and **E0302** (trivial self-reference) — a mechanical
  spec fix, surfaced not swept, the same shape as Sprint 5's `map` → `select` §6
  correction. No design change, no new number.
- **Two cycle DFSs, do not conflate (A).** §17.1 is the **structural field cycle**
  walk (required non-nullable named-type fields form an infinite value; E0301/E0302),
  over the type-declaration graph. §17.2 is the **value-binding type cycle** walk
  (E0303, D-323), over top-level initialiser dependencies, run in the checker's phase
  1.5. They are different walks over different graphs with different codes. A's cycle
  work is the §17.1 field walk only; it does not touch the §17.2 value-binding walk.
- **The closure-in-field escape is a mandatory C acceptance line (D-325).** A struct
  field can now hold a closure. D-325 made upvalue closing **location-based and
  route-agnostic** precisely so array element, map value, struct field, parameter and
  direct return all close identically, and recorded the struct-field route as absent
  only because structs were Sprint 6. Increment C must drive a closure stored in a
  struct field that escapes its enclosing function and confirm the upvalue closes with
  no value-stack underflow. This is verification over the settled D-325 mechanism, not
  new closure work — but it is the single highest-value cross-cutting test this sprint
  inherits, and it is not optional.
- **Anonymous structs have no annotation form (D).** `#{ ... }` appears in value
  position only and the type checker synthesises an internal structural type from the
  literal. There is **no** `field: #{...}` type-reference production, and none is to
  be added — the §9 grammar is complete (D-327). An implementer reaching to admit
  `#{...}` as a `TypePrimary` stops and surfaces.
- **The close-gate script is `types.grob`, built from the Sprint 1–6 surface only,
  with no stdlib modules.** Like Sprint 3's `hello.grob`, Sprint 4's `calculator.grob`
  and Sprint 5's `functions.grob`, it is a planning-defined smoke test, **not** one of
  the thirteen release-gate scripts. It exercises a declared type with field defaults,
  nested construction, field access and assignment, a recursive type (`Tree` with
  `children: Tree[]` or `Node` with `next: Node?`), a `#{ }` projection through
  `.select()` and a closure stored in a struct field that escapes its enclosing
  function. It uses no `fs`, `json`, `process` or any other module, none of which
  exist until Sprint 8.

## The close-gate

Sprint 6 closes (Increment E) on `grob run types.grob` over a script that exercises
declarations, construction with defaults, nested construction, field access and
assignment, a recursive type, an anonymous-struct projection and a closure-in-field
escape — the §7 acceptance surface — the way Sprint 5 closed on `grob run
functions.grob`. Sprint 6 is closeable when Increment E's acceptance is green and the
fourth VM-execution benchmark baseline passes the two-axis gate (D-313).

## Acceptance to hit (whole sprint)

The §7 acceptance, met across the five increments: types can be declared, constructed
and accessed; required fields are validated and defaults fill omitted fields; unknown
field names at construction and undefined field access are compile errors; nested
construction and nested access resolve; field assignment mutates through
`SetProperty`; required-non-nullable field cycles are caught at compile time;
anonymous structs work in lambdas and `select()` projections with type-safe field
access; the brace forms disambiguate without ambiguity; `grob run types.grob` runs.
Sprint 6 is closeable when Increment E's acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh corpus zip uploaded at the start of
  each increment as the known-good state.
- **C# 14 / .NET 10 (D-310).** Sprint 6 code is C# 14 from the first line.
- **`ErrorCatalog` (D-308).** Every diagnostic references a central catalog
  descriptor; the `"Exxxx"` string for any code appears exactly once. The one new
  Sprint 6 code (E0012) goes in `grob-error-codes.md` and the catalog at the next-free
  number in Increment B — never as a literal at the call site, never invented. The
  consistency gate (D-316) asserts catalog↔registry agreement on every commit.
- **Parser, AST and the `OpCode` enum are stable.** Every Sprint 6 increment is
  type-checker, compiler-emission, VM-opcode-arm and (E) fixture work over
  already-parsed nodes. No increment edits the parser, the AST or the closed enum.
- **Tests are plain xUnit.** Routed per §3.5 to the project matching their kind.
  `[Theory]` rows cover the layer invariant for pipeline work; `FsCheck` and
  `FluentAssertions` are not in `Directory.Packages.props` and are not used — follow
  the `tdd-cycle` skill.
- **The `defining-a-type` skill.** Increments A–D follow `.claude/skills/defining-a-type`
  — the end-to-end discipline for landing a user-type feature, with the four Sprint 5
  interlude lessons baked in as its standing checklist (type-reference-grammar
  completeness, the two-DFS distinction, collision uniformity across binding forms,
  and the escape-route regression for lifecycle-managed values).
- **§7 stays as written**, save the mechanical §17.1 `E—cycle` → E0301/E0302
  correction in A. Whether to rewrite §7 to the A–E structure is a documentation-
  authority call deferred to Sprint 6 close, as it was for §5 and §6.
- **Model (D-314).** Sonnet 4.6 (High) is the code-gen workhorse throughout. No Opus
  carve-out this sprint.
- Start with `/sprint-6-a`.
