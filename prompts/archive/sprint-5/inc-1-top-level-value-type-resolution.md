## Increment: Top-level value-binding type resolution

### Why

D-321 hoists top-level `fn` bindings and registers top-level value-binding names
in pass 1, but a forward reference to a top-level value binding still resolves to
`GrobType.Unknown` until pass 2 reaches that binding's declaration line. A function
body that reads such a binding receives `Unknown`, which every typed position
rejects:

    fn f(): int { return x }
    readonly x := 5

fails E0005 ("cannot return 'unknown' from 'int'") though it is valid. Confirmed
Case A — `Unknown` rejects loudly, no soundness hole — but valid forward reads do
not compile. This same gap blocks the `circular-initialisation` E5902 gold master:
its function body reads a forward value binding, fails type-checking, and never
reaches the VM, so E5902 cannot fire.

Close it: resolve top-level value-binding TYPES before body validation. Type only —
no value evaluation, no runtime ordering change.

### In scope

Type checker only. A new resolution phase between §17's registration pass and
validation pass. The `circular-initialisation` gold master (regenerate, lift
quarantine). Spec §17, §17.1, §19.1. One new compile-time error code for value-type
cycles.

### Closed surfaces — do not touch

VM and the runtime three-state slot tag (§19.1) and E5902 detection — correct,
this is a compile-time typing change only. D-321's compiler prologue. Stdlib,
plugins, formatter, benchmarks, every error example except circular-initialisation.
INC-2's redeclaration work (separate increment). E1101 shadowing.

### Plan-before-code gate

Read TypeChecker.cs pass structure, §17, §17.1, §19.1. Post a numbered plan: the
resolution-phase insertion point; the dependency-ordering algorithm; the value-type
cycle diagnostic; the regenerated gold master; the spec edits; the decisions-log
entry. Wait for approval.

### TDD sequence

1. Probe pair (currently both wrong): `fn f(): int { return x }` then
   `readonly x := 5` must PASS; with `readonly x := "hello"` must FAIL E0005 naming
   `'string'`, not `'unknown'`. Keep as a permanent TypeCheckerTests case.
2. Forward read in other typed positions (binary op `"s" + x`, call argument)
   resolves to the real type.
3. Unannotated mutual value cycle `readonly a := b` / `readonly b := a` raises the
   new compile-time cycle diagnostic, does not infinite-loop, does not reach the
   runtime E5902.
4. If value-binding type annotations exist in the grammar: annotated mutual cycle
   (`readonly a: int := b` / `readonly b: int := a`) resolves types and surfaces as
   the runtime E5902 instead — distinct path. If annotations do not exist, drop this
   test and note in the plan that every mutual value cycle is a type cycle.
5. circular-initialisation gold master: the §19.1 forward-read example now compiles,
   reaches the VM and raises E5902 at the value binding, line-only (`:5`, per D-322).
   Regenerate the pair, lift the quarantine.

### Implementation notes

Phase ordering becomes: register -> resolve value-binding types -> validate. After
registration, fn signatures (including return types) and value-binding names are
known. The new phase infers each top-level value binding's static type from its
initialiser, in dependency order.

Dependency edges run binding->binding only on DIRECT value-binding references in an
initialiser. A function-call result contributes the function's declared return type
(known from registration), so `readonly x := f()` with `f(): int` resolves to `int`
with no value-binding edge. Topologically order via DFS with Unvisited/Visiting/
Visited, mirroring §17.1; resolve in post-order. A back-edge to a Visiting binding
is a circular type dependency: emit the new code, set the involved bindings' type to
the `Error` cascade type, continue (collect-all, D-039 compile mode). Detection is
mandatory — a resolution phase that loops on cyclic input is unacceptable.

Document the split: an unannotated mutual value cycle fails at COMPILE time (type
unresolvable); an annotated one passes type-checking and surfaces as the RUNTIME
E5902 value cycle. Two shapes, two correct diagnostics.

### Error catalogue

Allocate one new compile-time code (verify the next free slot against the live
`ErrorCatalog` and registry — do not take a number from this prompt) for "circular
type dependency among top-level bindings". Register once in `ErrorCatalog` (D-308).
Update the count line; the consistency gate (D-316) verifies agreement. Long-form
`docs/errors/Exxxx.md` stub deferred to the error-docs session.

### Spec edits

§17 — the two-pass description becomes register -> resolve value-binding types ->
validate; confirm registration covers value bindings (D-321). §17.1 — add a sibling
subsection for value-binding type cycles, distinct from type-field cycles, with the
new code. §19.1 — the forward-read note now resolves the type; remove any wording
implying `Unknown`; D-321's "resolve and run" is now literally true. Cite the new
D-### at each site.

### Decisions log — verify tail first

Read the live log tail and allocate the next free D-### (expected D-323 if this runs
first — verify, do not assume). Refines D-321 and D-166. Record: resolution phase
between registration and validation; value-binding types inferred in initialiser-
dependency order; unannotated mutual value cycles are a compile-time type-dependency
error (new code), distinct from runtime E5902; closes the forward-typed-value-read
gap and unblocks the circular-initialisation gold master. Lockstep: index row, full
entry, footer changelog.

### Done

Probe 1 passes; probe 2 fails E0005 naming `'string'`; value-type cycle caught at
compile time with no non-termination; circular-init gold master regenerated and
green; quarantine lifted; full validation suite and consistency tests green; D-###
and spec edits in lockstep.
