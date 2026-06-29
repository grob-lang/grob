---
name: defining-a-type
description: >
  Use when an increment introduces, resolves or constructs a Grob type — a `type`
  declaration, a struct construction, an anonymous struct, a new type-annotation
  position, a new built-in type, or any feature where a value carries a type the
  checker must resolve and the VM must represent. Encodes the four failure modes that
  produced the Sprint 5 post-close interludes (type-reference-grammar gaps, conflated
  cycle walks, non-uniform redeclaration and lifecycle objects escaping through new
  containers) as a standing pre-flight and a closing checklist. Reuses, does not
  replace, `adding-an-opcode` and `tdd-cycle`.
---

# defining-a-type

Landing a type feature in Grob is mostly type-checker, compiler-emission and
VM-representation work over an already-parsed AST. The front end is built incrementally,
so the parser and AST are closed *within an increment* and the `OpCode` enum is closed
under its wire-format contract — most type increments touch neither. But a feature that
introduces a new surface form (a construction expression, a new literal) may genuinely
need a parser production or an AST node: that is the `extending-the-grammar` job, done
deliberately, not a forbidden edit. What usually goes wrong is not the parts that are
obviously hard; it is four specific seams that read as done on paper and bite at runtime.
This skill is the discipline that checks them every time. Three of the four are lessons
paid for in the Sprint 5 interludes.

## Before writing any code — the pre-flight

Run these four checks against the live corpus first. Each maps to an interlude.

**1. Type-reference-grammar completeness (D-326, D-327).** A type feature almost always
introduces a new place a type is *written* or a new shape a value can *have*. Two
Sprint 5 interludes were a `TypeRef` form the corpus used pervasively that did not
actually parse or resolve — `int[]` as an annotation, `fn(): int` as a return type.
Before relying on the grammar:

- Read §9 (`TypeRef := TypePrimary TypeSuffix*`) and enumerate every annotation form
  the feature will resolve: named user type, self-reference, `T?`, `T[]`, nested
  `T[][]`, `map<K, V>`, `fn(...): T`, grouped `(fn(): int)[]`.
- `grep` the sample scripts and the stdlib reference for how the feature's surface is
  actually written. Every occurrence must resolve. If one does not, that is a finding —
  surface it; do not paper over it with a special case. If the increment is authorised to
  extend the grammar and the gap is a missing production or node, the route is
  `extending-the-grammar`; if not, stop and propose first.
- If the feature is value-position-only (an anonymous struct literal), confirm it has
  **no** annotation production and do not add one. Inferred is not annotated.

**2. Which cycle walk (D-323).** Grob has two distinct cycle DFSs and they must never be
conflated:

- **§17.1 structural field cycle** — required non-nullable named-type fields that would
  produce an infinite value. Codes **E0301** (multi-type) / **E0302** (trivial
  self-reference). Walks the type-declaration graph. Terminators: `T?`, `T[]`,
  `map<K, V>`.
- **§17.2 value-binding type cycle** — top-level initialisers whose types depend on each
  other. Code **E0303** (D-323), run in the checker's phase 1.5. Walks the initialiser-
  dependency graph.

Same three-colour `Unvisited`/`Visiting`/`Visited` algorithm, different graphs,
different codes. Name which one the feature touches and do not route the other through
it.

**3. Redeclaration is uniform (D-324).** A type feature usually adds a name to the
top-level binding space. Collisions across **all** binding-introducing forms —
`type`, `fn`, `readonly`, `const`, `:=` — are **E1102** "name already declared in this
scope", reported at the **second** declaration in source order. Reuse the uniform D-324
predicate (provisional registration in pass 1, finalised in pass 2); do not re-derive a
form-specific check, and do not let the diagnostic land on the earlier declaration.

**4. New escape routes for lifecycle-managed values (D-325).** If the feature adds a new
container a value can live in — a struct field, a map value, an array element — and a
**closure** (or any value with a frame-bound lifecycle) can be stored there, the value
can now escape its creating frame through the new route. D-325 made upvalue closing
location-based and route-agnostic precisely so every route closes identically. The
feature does not re-derive that mechanism; it **regression-tests the new route**: store
a capturing closure in the new container, let it escape the enclosing function, call it
afterwards, and assert the upvalue closed with no value-stack underflow. If it does not
close, that is a D-325 regression and a finding — surface it, do not patch around it.

## While building — the standing rules

- **Pass 1 registers the name; pass 2 resolves the body.** D-166 registers top-level
  type and function names in pass 1 so forward references resolve. Field-type
  resolution, body validation and cycle detection happen in pass 2 (and phase 1.5 for
  value-binding types). Reuse pass-1 registration; do not rebuild it.
- **Resolve to the type the checker already owns.** An array annotation resolves to the
  existing `T[]` type; a struct field of a declared type resolves to its registered
  entry. Wiring an annotation path to an existing representation introduces no new
  runtime type unless the feature genuinely is one.
- **Opcodes are closed — wire, do not add casually.** The struct opcodes (`NewStruct`,
  `NewAnonStruct`, `GetProperty`, `SetProperty`) and every other opcode were closed in
  Sprint 2 under the wire-format contract. Follow `adding-an-opcode`'s
  emit-and-dispatch-together discipline; for a struct feature the enum step is a no-op.
  If a feature genuinely needs an opcode the enum lacks, that is `adding-an-opcode`'s
  warranted path, not a casual edit — stop and surface before adding.
- **Every value-position `[` and `{` is unchanged.** A type-annotation feature must not
  disturb value-position array literals, indexing or blocks. `{ }` is always a block,
  `#{ }` always an anonymous struct, `TypeName { }` always named construction.
- **The §3.1.1 invariant holds for every node introduced.** Every identifier and member
  node carries a non-null `ResolvedType` and a non-null `Declaration` after type
  checking — by reference (`Assert.Same`), not by value.

## Diagnostics — allocate through the ladder, never invent

- Raise every diagnostic through its `ErrorCatalog` descriptor (D-308). The `"Exxxx"`
  string for any code appears exactly once. No literal at a call site.
- Confirm each code against the **live** `grob-error-codes.md` before use. Most type
  diagnostics already exist (the struct surface was specced with its codes) and most are
  in the increment's declared budget. If a diagnostic needs a code that is **not** in the
  budget and not already registered, do not invent and do not fold into an ill-fitting
  code: walk the `allocating-an-error-code` ladder — surface the fold-versus-new
  judgement, get the decision, then register.
- When the increment does register a code, do it through `allocating-an-error-code`:
  three-location lockstep (summary row, full entry, standing total) against the live
  registry, the `ErrorCatalog` descriptor, the count reconciled to the live total, and
  the authorising `D-###` cited. The D-316 gate asserts catalog↔registry agreement and
  the count on the commit.
- When the spec prose cites a placeholder code (`E—cycle`) that the registry has since
  replaced, that is drift — correct the prose to the real code as a sanctioned mechanical
  fix, surfaced not swept.

## Before the hand-off — the closing checklist

- Every §9 annotation form the feature touches resolves; the corpus grep is clean.
- The correct cycle walk fired (E0301/E0302 for fields, E0303 for value bindings) and
  legitimate recursive patterns (`Tree`/`Node`) did not.
- Collisions are E1102 at the second declaration, across every binding form.
- Any new closure-bearing container was regression-tested for the escape-and-close path
  (or a D-325 regression was surfaced).
- No opcode added, no parser/AST edit, no value-position regression.
- Every diagnostic routes through the catalog; no literal, no invented code; the count
  and the consistency gate are correct.
- The §3.1.1 invariant holds for every node introduced.
