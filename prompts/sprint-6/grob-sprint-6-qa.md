# Sprint 6 QA Brief — User-Defined Types (cold-read)

> **What this is.** The adversarial cold-read brief for Sprint 6, run by GPT-5.3 Codex
> via Codex CLI against the **merged** Sprint 6 branch after Increment E lands and
> before the sprint is declared closed. It is the external second pair of eyes on the
> A–E work — the same role the Sprint 5 QA brief played. It is **not** a slash command
> and unblocks nothing; it is the durable QA contract, kept under `prompts/sprint-6/`
> with the kickoff.
>
> **Standing instruction to the cold-reader.** Read the merged branch directly. The
> design corpus under `docs/design/` is authority and **the decisions log wins on
> conflict**. Assume each seam below is wrong until the code proves it right — this
> brief is a list of where Sprint 5 actually broke after it looked done, not a list of
> things that are probably fine. Report findings against the acceptance and the four
> interlude classes; do not fix, do not open PRs.

## Why this brief is shaped the way it is

Sprint 5 closed clean and then took **six numbered post-close interludes** to harden.
Stripped to causes they fall into four classes, and three of the four are live again in
Sprint 6 because Sprint 6 is the first heavy **user-written type** sprint:

1. **Type-reference-grammar gaps** (D-326, D-327) — a `TypeRef` form the corpus used
   pervasively did not parse or resolve. Sprint 6 resolves arbitrary field annotations
   for the first time.
2. **Conflated cycle walks** (D-323) — Sprint 6 carries two cycle DFSs (§17.1 field,
   §17.2 value-binding) that must not be crossed.
3. **Non-uniform redeclaration** (D-324) — Sprint 6 adds `type` to the top-level
   binding space.
4. **Lifecycle objects escaping through a new container** (D-325) — the struct field is
   a brand-new escape route for closures, deferred to Sprint 6 by name.

The per-increment and cross-cutting probes below are built to catch exactly these
before they become Sprint 6 interludes.

## Per-increment adversarial probes

### Increment A — declarations, registry, cycle detection

- **Field-annotation resolution (interlude class 1).** Confirm every §9 field-annotation
  form resolves: named user type, self-reference (`children: Tree[]`), `T?`, `T[]`,
  `T[][]`, `map<K, V>`, `fn(...): T`, grouped `(fn(): int)[]`. Try a field whose
  annotation names an **undefined** type — confirm a clean diagnostic with a source
  location, not a crash or `Unknown`. Try a forward reference to a type declared later
  in the file — confirm it resolves (D-166 pass-1 registration).
- **§17.1 cycle walk (interlude class 2).** Confirm `A.b: B` / `B.a: A` (both required,
  non-nullable) is **E0301** with the full path; `type A { a: A }` is **E0302**. Confirm
  `type Tree { children: Tree[] }` and `type Node { next: Node? }` do **not** fire — the
  array and nullable terminate the walk. Probe the boundary: a required non-nullable
  field cycle broken by **one** nullable or array hop must not fire.
- **The other cycle walk (interlude class 2).** Confirm the §17.1 field walk is
  **separate** from the §17.2 value-binding walk (E0303). A field cycle must not be
  reported as E0303 and a value-binding cycle must not be reported as E0301/E0302. Two
  graphs, two codes.
- **Collision (interlude class 3).** Confirm a `type` name colliding with another
  `type`, with an `fn` and with a value binding is **E1102** at the **second**
  declaration in source order — including the **value-before-`type`** order (the D-324
  reverse-order case that was itself an interlude). Confirm **E2208** for a duplicate
  field within a declaration.
- **Spec-drift fix.** Confirm §17.1 in the merged fundamentals cites **E0301/E0302** and
  the `E—cycle` placeholder is gone everywhere (grep the whole corpus for `E—cycle`).
- **Scope discipline.** Confirm Increment A emitted **no** bytecode, built **no** runtime
  struct value and added **no** opcode case. The error-code count is unchanged at 109
  after A.

### Increment B — construction and defaults

- **Required and unknown fields.** Confirm a construction omitting a required field is
  **E0103** and one naming an undeclared field is **E0012**, both with source locations.
- **E0012 registration (interlude class 4 — latent gaps).** Confirm E0012 was registered
  in **three-location lockstep** (summary row, full entry, total **109 → 110**) citing
  **D-330**, that it sits in the E00xx Type block, that no `"E0012"` literal appears at
  any call site (it routes through `ErrorCatalog`), and that the D-316 consistency gate
  is green. Confirm E0012 is **distinct** from E1002 — construction-site unknown field
  vs member access on a value.
- **Default emission shape.** Confirm a default is compiled into **each** construction
  site that omits the field, evaluated at construction time in the **construction-site
  scope** — the Sprint 5 B default-argument shape. Probe side effects: a defaulted field
  whose default increments a counter evaluates **once** when omitted and **never** when
  supplied.
- **Sibling reference.** Confirm a default that references a **sibling field** of the
  type being constructed is a compile error.
- **Nested and disassembly.** Confirm nested construction disassembles to inner
  `NewStruct` then outer `NewStruct`; confirm a construction disassembles to its field
  values (supplied and defaulted) then `NewStruct` with the right type index — verified
  through the **disassembler**, not only the VM's answer. Confirm **E2102** for a named
  construction missing `{ }`.

### Increment C — access, assignment, and the closure escape

- **Access and assignment.** Confirm `instance.field` reads via `GetProperty`,
  `instance.field = v` writes via `SetProperty`, nested `a.b.c` resolves each step,
  undefined field access is **E1002** at the offending step and mutation through a
  `readonly` binding is **E0204**.
- **The closure-in-field escape (interlude class 4 — the deferred D-325 route). This is
  the highest-value probe in the sprint.** Confirm a closure that captures an
  enclosing-function local, is stored in a **struct field** and **escapes** (the struct
  is returned), closes its upvalue on frame exit and is callable **through the field**
  afterwards with the correct value and **no value-stack underflow**. Confirm
  per-call independence holds through the field route (two constructions, two
  independent captured slots). Confirm a struct field holding a no-upvalue top-level
  `fn` is trivially correct. If any of this is wrong, it is a **D-325 regression** — flag
  it as the headline finding, not a local bug.

### Increment D — anonymous structs

- **Structural type.** Confirm `#{ }` synthesises a structural type with type-safe field
  access; two literals with the same field set and types share a structural type;
  differing literals do not. Confirm an undefined field on an anonymous struct is a clean
  diagnostic.
- **Projections (the acceptance vehicle).** Confirm `.select(e => #{ ... })` yields an
  array of the synthesised structural type with type-safe field access on the result.
  Confirm nested `#{ }` resolves (the ARM-template-style depth from the sample scripts).
- **No annotation form (interlude class 1).** Confirm there is **no** `#{...}`
  type-reference production — anonymous structs are inferred, value-position only. Grep
  for any attempt to admit `#{...}` as a `TypePrimary`.
- **Disambiguation.** Confirm `{ }` is always a block, `#{ }` always an anonymous struct,
  `TypeName { }` always named construction; **E2101** fires where a bare `{ }` was
  written expecting a struct.

### Increment E — close

- Confirm `grob run types.grob` meets the §7 acceptance and that `types.grob` exercises
  every bullet (defaults both ways, nested construction, access and assignment, a
  recursive type, a `#{ }` projection, the closure-in-field escape) using **no** stdlib
  modules. Confirm `hello.grob`, `calculator.grob` and `functions.grob` still run.
- Confirm the **fourth** VM-execution benchmark baseline was produced from the workflow
  (D-309) and passes the two-axis gate (D-313).

## Cross-cutting probes

- **Grammar completeness sweep (interlude class 1).** Grep the sample scripts and the
  stdlib reference for every struct-syntax occurrence (`type `, `#{`, `TypeName {`,
  `.field`, `.field =`) and confirm each parses and type-checks against the merged tree.
  An occurrence that does not resolve is the exact latent gap that produced D-326/D-327.
- **OpCode enum untouched.** Confirm `NewStruct`, `NewAnonStruct`, `GetProperty`,
  `SetProperty` are the **same enum members** closed in Sprint 2 — no case added, the
  enum unchanged (ADR-0013). Confirm no increment edited the parser or the AST.
- **Error-code integrity.** Final count is **110**; the one addition is **E0012** (D-330);
  catalog↔registry agreement holds; no `"Exxxx"` literal anywhere a `ErrorCatalog`
  descriptor should be; no `E—cycle` placeholder remains.
- **§3.1.1 invariant.** Spot-check that identifier and member nodes across the new
  surface carry non-null `ResolvedType` and `Declaration` (the LSP day-one properties).
- **DAG.** Confirm `Grob.Compiler` and `Grob.Vm` still do not reference each other and
  `Grob.Core` remains the only shared ground.
- **Coverage.** Confirm the affected projects hold at or above the 90% line+branch floor
  (D-328, ADR-0018) and that nothing was excluded to make the number.

## Report format

For each finding: the increment, the interlude class (1–4) or "new", the file and line,
the observed behaviour, the expected behaviour with its corpus citation (decisions log
wins) and severity (blocker / should-fix / note). Lead with any **D-325 closure-in-
field regression** or any **type-reference-grammar gap** — those are the two classes
most likely to recur and most expensive if they ship. Close with an overall verdict:
closeable, or the blocker list that must clear first.
