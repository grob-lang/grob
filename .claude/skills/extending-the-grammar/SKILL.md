---
name: extending-the-grammar
description: >
  The sanctioned procedure for adding a parser production or an AST node to Grob —
  the parser/AST analogue of adding-an-opcode. Use when an increment genuinely needs a
  new surface form or a new node that is not already there (a new construction
  expression, a new literal, a new annotation position). The front end is built
  incrementally, so this is a real and expected job — not a wall to route around with an
  improvised gap-fix. It is deliberate and reviewable, never silent.
---

# Extending the grammar

The front end is built incrementally: a feature's grammar surface lands with that
feature. So the parser and AST are treated as closed **within an increment** — an
increment never silently grows the grammar — but adding a production or a node when a
feature genuinely needs one is a normal, sanctioned act. This skill is that act, done
deliberately.

Read this when you find an increment needs a node that is not there. Sprint 4E and
Sprint 6B both hit this — a construction node and its parser hook were needed and added.
That is the case this skill is for. The wrong response is an undocumented gap-fix; the
right response is the procedure below.

## How this differs from adding-an-opcode

`adding-an-opcode` governs a surface under a **wire-format stability contract** — the
`OpCode` enum is part of the `.grobc` format (ADR-0013), so adding an opcode has
versioning consequences and earns a decision-log entry on the wire-format grounds. The
AST has **no** such contract: it is compiler-internal, never serialised, with zero
backward-compatibility surface. So the ceremony here is **scope discipline and
reviewability**, not stability. A back-filled node that the spec already described is a
fix, not a design change; a genuinely new language surface form is a design decision and
earns a `D-###`. Know which one you have before you start.

## Step 1 — Confirm it is warranted, and authorised

- **Warranted by the spec?** Is the surface form required by an acceptance criterion, a
  `§` of the fundamentals, or a `D-###`? If the form is not in the spec, this is a
  language-design decision, not an implementation task — stop and propose it (a `D-###`
  via `logging-a-decision`), do not invent surface syntax.
- **Authorised by this increment?** Did the increment prompt scope grammar work? If the
  prompt says the increment is back-end wiring over already-parsed nodes and you find a
  node missing, **stop and surface** before editing — confirm the node is genuinely
  absent (grep the AST and the parser) and that adding it belongs in this increment
  rather than a separate one. Surfacing first is the gate; it is not a blocker, it is a
  one-line confirmation.
- **Can an existing node or a lowering express it?** Reuse beats addition. A new infix
  form may desugar to an existing node; a new statement may be a parse-level rewrite. Add
  a node only when nothing existing fits.
- **Is a settled decision forbidding it?** Some "stop and surface" instructions are hard
  by design — e.g. D-327 forbids a `#{...}` type-annotation production. If a decision
  says the production must not exist, this skill does not override it: stop and surface,
  do not add.

## Step 2 — The parser production (`Grob.Compiler`)

Add or extend the production in the recursive-descent parser at the correct precedence
level. A postfix form (member access, construction, indexing) is wired where the other
postfix forms are; a primary form where the other primaries are. Keep value-position and
type-position grammar separate — a value-position `{`/`[` must not disturb
type-annotation parsing, and vice versa.

## Step 3 — The AST node (`Grob.Core` or the AST project)

- The node carries a **`SourceLocation`** — line, column, file — set at parse time. This
  is day-one and non-negotiable (the §3.1.1 invariant depends on it).
- An identifier-bearing node also carries the **`Declaration`** back-reference slot the
  type checker fills, and after type-check a non-null `ResolvedType`. The error-path
  sentinels are `GrobType.Error` and `UnresolvedDecl.Instance`, asserted by reference —
  never literal `null`.
- Records and node shapes follow the existing AST conventions; a construction node
  carries its field initialisers as their own small record, as `FieldInit` does.

## Step 4 — Every visitor handles it (including error recovery)

A new node that some visitor does not handle is a latent crash. Add the node to **every**
visitor — the type checker, the compiler, the disassembler's any AST walk, the formatter
if one exists — and to the **error-recovery path**: the parser must be able to degrade a
malformed instance of the new form to `ErrorExpr`/`ErrorStmt`/`ErrorDecl` and continue,
and every visitor must already absorb those error nodes via the `Error` type's universal
assignability (D-300). The stateless, no-cap, error-recovering parser contract holds for
the new form exactly as for the old.

## Step 5 — TDD both sides

- **Parser test:** the new source form parses to the expected node with the right
  `SourceLocation`; a malformed instance recovers to an error node and the parser
  continues (no cap, no crash).
- **Layer-invariant rows:** pathological but parseable inputs through the new production
  parse to a node or a diagnostic, never throw.
- The downstream type-checker/compiler/VM tests for the *feature* come from its own
  skill (`defining-a-type`, `tdd-cycle`); this skill covers the parse surface.

## Step 6 — Record it, proportionately

- **A back-filled node the spec already described** (the Sprint 6B construction node) is
  a fix. Note it in the commit body — what was missing, what now parses — and reference
  the `§` or `D-###` that already mandated the form. No new decision needed.
- **A genuinely new language surface form** is a design decision. Log a `D-###` via
  `logging-a-decision` before or alongside the implementation, and cite it in the commit.
- Either way there is **no `.grobc` impact** to record — the AST is not the wire format.
  Do not add an ADR-0013 note for an AST change.

## Checklist

- [ ] Warranted by the spec, and adding it is authorised by this increment (or surfaced
      first)
- [ ] Not expressible by reuse or lowering; not forbidden by a settled decision
- [ ] Parser production added at the correct precedence; value/type position kept separate
- [ ] AST node carries `SourceLocation` (and the `Declaration` slot where identifier-bearing)
- [ ] Every visitor handles the node, and the error-recovery path degrades it cleanly (D-300)
- [ ] Parser test and layer-invariant rows present and green
- [ ] Recorded proportionately — commit note for a back-fill, a `D-###` for a new surface form
- [ ] `dotnet build` and `dotnet test` green; coverage at or above 90%
