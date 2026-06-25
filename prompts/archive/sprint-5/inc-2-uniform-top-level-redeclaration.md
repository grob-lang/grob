## Increment: Uniform top-level redeclaration semantics

### Why

E1102 ("variable already declared in this scope") fires on duplicate value bindings,
but `VisitFnDecl` and `VisitTypeDecl` perform no collision check, so duplicate
top-level `fn` and `type` declarations are SILENTLY accepted — a correctness gap
(verified against the live catalogue: no code covers fn/fn or type/type duplicates;
E2208 covers only duplicate fields within a type). Separately, a reverse-order
collision (`foo := 1` before `fn foo()` or `type foo`) reports E1102 at the earlier
declaration rather than the offending later one — the caret location differs by
declaration kind. Make redeclaration detection uniform across all top-level binding-
introducing forms, reported consistently at the offending declaration.

### In scope

Type checker collision detection across value, fn and type top-level declarations.
E1102 broadening (description and message). Any E1102 fixtures in the error-examples
library whose caret moves. Spec redeclaration/scoping section and error-codes.md
E1102 entry.

### Closed surfaces — do not touch

VM, compiler, stdlib, plugins, formatter, benchmarks. E1101 shadowing (nested-scope
warning — a different rule). INC-1's resolution phase. The D-321 provisional flag is
respected, not changed. No new error code; the count must not move.

### Plan-before-code gate

Read the top-level collision check, `VisitVarDecl`/`VisitFnDecl`/`VisitTypeDecl`,
§17 and the scoping section, and error-codes.md E1102. Grep the error-examples
library for existing E1102 gold masters. Post a numbered plan: the unified collision
predicate; the E1102 broadening; the report-at-offending-decl convention with a
first-declaration note; the affected fixtures; spec edits; decisions-log entry.
Wait for approval.

### TDD sequence

1. fn/fn duplicate: two top-level `fn foo()...` -> E1102 at the second, note pointing
   at the first. Currently silent.
2. type/type duplicate: two top-level `type Bar...` -> E1102 at the second. Currently
   silent.
3. value-before-fn reverse order: `foo := 1` then `fn foo()` -> E1102 at the `fn foo`
   line (offending later decl), not at `foo := 1`. Currently reports at the var.
4. cross-kind pairs (fn-before-value, type-before-value, type/fn same name) all
   report at the second declaration uniformly.
5. provisional-flag interaction: a pass-1 provisional value entry does not false-
   positive against its own real declaration (D-321 preserved); a genuine duplicate
   against an already-real entry still fires.
6. Existing value/value E1102 fixtures: caret now at the offending decl — regenerate
   the affected gold masters.

### Implementation notes

A single collision predicate consulted by VisitVarDecl, VisitFnDecl and VisitTypeDecl
at the point each introduces a top-level name: if a REAL (non-provisional) binding of
that name already exists in the top-level scope, raise E1102 at the introducing
declaration's location, with a note carrying the prior declaration's location. The
report site is the declaration processed second in source order — the offending one —
which falls out naturally because the provisional->real transition lands at the real
declaration. This aligns with the D-321 provisional flag rather than fighting it.

Broaden E1102: "variable already declared in this scope" -> "name already declared in
this scope". Pre-release, so the message/description change is permitted (ADR-0017
immutability is post-ship). No new code; count unchanged. E1102 is a compile-time
diagnostic with full column info (D-137), so carets are line:col — the D-322 line-
only limitation is runtime-only and does not apply here.

Run the full validation suite and consistency tests after: any newly-caught duplicate
in the 13 release-gate scripts or existing fixtures is a real bug to surface, not
suppress.

### Error catalogue

E1102 description and message broadened in `ErrorCatalog` (D-308) and the registry,
kept in agreement (D-316). No new code.

### Spec edits

The redeclaration/scoping section and §17 — state that all top-level binding-
introducing forms (`:=`, `readonly`, `const`, `fn`, `type`) share one redeclaration
rule, E1102, reported at the offending declaration with a note at the first. Cite the
new D-###.

### Decisions log — verify tail first

Read the live log tail and allocate the next free D-### (expected D-324 if this runs
after INC-1 — verify, do not assume). Refines D-321; relates to E1102's origin.
Record: uniform top-level redeclaration detection across value/fn/type; E1102
broadened to "name already declared in this scope"; reported at the offending later
declaration with a first-declaration note; closes silent fn/fn and type/type
duplicates. Lockstep: index row, full entry, footer changelog.

### Done

fn/fn and type/type duplicates caught; all collisions report at the offending decl
with a first-declaration note; provisional-flag behaviour preserved; affected E1102
fixtures regenerated; full validation suite and consistency tests green; D-### and
spec edits in lockstep.
