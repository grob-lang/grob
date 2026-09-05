# Increment prompt — finish the param concern

**Authority: D-424.** Read that entry in `docs/design/grob-decisions-log.md`, and
`grob-language-fundamentals.md` §19 including its new **Decorators** subsection, before
starting. This prompt implements them; where prompt and decision differ, D-424 wins and
the difference is a finding to report.

**Model:** Opus, or the top model available. This is the sprint's one named load-bearing
structural sub-problem — a new AST node with a type-checker consumer, plus the first
enforcement of a declaration-order rule that has been specified and unbuilt since April
2026. Everything else in the pre-9C run is bounded; this is not.

**Branch:** one concern, one branch. The concern is `param`.

**Archive this prompt verbatim** in this increment's own commit, exactly as issued. Never
retrofit it. If the work diverges, the divergence goes in the decisions-log entry.

---

## 1. Read-only investigation gate

**No source edits until this gate is complete and reported.** Building and running the
CLI is expected and required during it — you need a working `dotnet build` and hand-run
`.grob` files. What you may not do is change production code.

Write the report to a **scratch file outside the working tree**. Do not paste it into
chat; in chat give the path and any STOP conditions that fired.

### 1.1 Confirm the unenforced surface

D-424's claim is that six codes have zero throw sites. Confirm each independently, and
**state the expected result before running**. For each, write a `.grob` file that should
be rejected and record what actually happens:

| Code | Probe | D-424 expects today |
|---|---|---|
| E2201 | `param x: int = 1` then `import Grob.Http` | compiles clean |
| E2202 | `fn f(): int { return 1 }` then `param x: int = 1` | compiles clean |
| E4001 | `@bogus` above a `param` | compiles clean |
| E4002 | `@secure` on an `int` param | compiles clean |
| E4101 | `@allowed(1, "two")` — heterogeneous | compiles clean |
| E4102 | `@minLength("x")` — wrong literal kind | compiles clean |

Also probe the cases D-424 adds beyond the registry descriptions: the same decorator
applied twice, and a decorator on a `type` field.

### 1.2 The three registry items — verify the ordering constraint still holds

D-414 scheduled R-01 here with a technical reason: **E4202 may not be removed before
ordering is enforced**, because until E2202 fires, removing E4202 leaves the ordering
condition with no code at all. Confirm that reasoning still holds against this tree
before relying on it — if E2202's throw site turns out to land later in the increment
than E4202's removal, the commit order matters and you should say so.

### 1.3 Enumerate breaking changes before editing

D-424 names one:
`ParserParamDeclTests.DecoratorInlineInFunctionParameterList_StillParses`.

Find the rest. Search `tests/`, `docs/errors/examples/` and
`tests/fixtures/` for anything that depends on:

- a decorator being accepted in a function parameter list;
- `SkipParameterDecorators`' `requireNewline: false` path;
- `ParamDecl.Range` starting at the `param` keyword rather than the decorator stack;
- a misordered declaration compiling clean;
- an invalid decorator compiling clean.

`ParserParamDeclRecoveryTests` line ~221 and ~238 both use `@secure` inside a function
parameter list as *incidental* fixture material while testing something else. Those are
the dangerous ones: they will start failing for a reason unrelated to what they assert.
Report them before touching anything, and rewrite their fixtures rather than their
assertions.

**Tests may be updated to assert new correct behaviour. Never weakened, never deleted.**

### 1.4 Decide where the ordering walk lives, and say why

D-424 fixes *what* the walk does, not *where*. Two candidates: a dedicated pass before
pass 1, or folded into pass 1's existing walk over `CompilationUnit.Items`. Report which
you chose and the reasoning. The two constraints from D-424 Decision 5 are binding on
either choice:

- diagnostics emitted in source order;
- an ordering error never suppresses pass-1 registration (D-039 — a misordered file with
  three type errors reports four diagnostics, not one).

### 1.5 Confirm Decision 6's separability empirically

D-424 asserts that registering a `param` name in pass 1 is separable from parameter
binding. `VisitParamDecl`'s comment says binding is Sprint 10. Confirm that adding
`ParamDecl` to pass 1's registration walk does **not** require resolving the param's
type, evaluating its default, or anything else Sprint 10 owns. If it does, that is a STOP
condition — report the entanglement rather than pulling Sprint 10 forward.

---

## 2. Scope

Six change groups, independently reviewable. Land them in this order.

1. **`Decorator` AST node.** Record with name, arguments (`Expression` list) and range.
   `ParamDecl` gains the decorator list; its `Range` extends to cover the stack.
   `ParseParamDecl` captures instead of skipping. `AstWalker`/`AstVisitor` gain the hook.
2. **Decorators become `param`-only.** `ParseDeclaredParameter` reports E4002 and
   recovers. `SkipParameterDecorators`' `requireNewline` flag and its second call site
   are retired. Fix the fixtures found in §1.3.
3. **Ordering enforcement.** E2201 and E2202 throw sites, per §1.4.
4. **`param` in the pass-1 name space.** E1102 collisions and D-412's coincident
   ordering-and-collision cascade — a misplaced `param` that also collides reports
   **only** E2202.
5. **Static decorator validation.** E4001, E4002, E4101, E4102 throw sites per §19's
   Decorators table. `@pattern` recognised; its pattern unvalidated (D-424 Decision 4).
6. **The three registry items, as one D-316 co-commit each.** E4202 removed (count
   121 → 120); E2202 retitled to name `type`, `const` and `readonly`; E4102 retitled to
   name `@minValue` and `@maxValue` alongside the length pair. `ErrorCatalog.cs` and
   `grob-error-codes.md` edited together — a title change or removal in one without the
   other fails the D-316 gate.

### Out of scope — report, do not fix

- **Binding-time enforcement of any decorator.** That is R-15 and Sprint 10. After this
  increment a statically valid decorator compiles clean and applies no constraint to any
  value. That is the intended end state here; do not partially implement it.
- **`@pattern`'s pattern validation** — waits on `regex`.
- **Release-gate decorator coverage** — R-08, Sprint 10.
- Anything in the Deferred Work Register.

**D-420's stopping rule applies.** Findings go to the register, not into the run, unless
they block this increment or Sprint 9C.

---

## 3. Implementation notes

**`CompilationUnit.Items` is already a flat ordered `List<AstNode>`.** The ordering check
is a single classify-and-compare pass. §19's categories: `import` (1), `param` (2), `type`
(3), `fn` (4), everything else including `const`, `readonly` and statements (5), with 3
and 4 unordered relative to each other.

**Decorator arguments are expressions, deliberately.** Do not restrict them at the parse
layer. `@minLength(x)` must reach the type checker so the diagnostic lands at the
argument's own position and says what is wrong with it. The checker must visit them —
§3.1.1 admits no exemption, and D-311's sentinels cover the error path. This mirrors what
`VisitParamDecl` already does with a `param` default.

**E4002 carries four distinct conditions** — wrong target, wrong param type for the
decorator, duplicate application, and decorator outside a `param` declaration. Each needs
a distinct message; the code is shared, the wording is not. Raise through
`ErrorCatalog.E4002`, never a literal (D-308). Follow
`grob-personality-identity.md` on tone.

**The cascade in Decision 6 is a suppression, not a precedence.** A misplaced `param` that
also collides reports E2202 alone — one root cause, one diagnostic, the same principle
§29 applies to parser cascades. Pin the suppression with its own test, and pin that a
*correctly placed* colliding `param` still reports E1102.

**Retiring `requireNewline` removes a branch, not just a call.** Once decorators are
`param`-only there is one production, so the scanner's two-mode design has no reason to
exist. If you find yourself keeping the flag, say why in the report — that would mean
Decision 2 is wrong about something.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code. Quarantine with a
documented reason rather than weakening a test or merging red.

Required:

- **One positive and one negative per decorator**, all seven, covering name, arity,
  literal kind and target type.
- **`@pattern` with a valid string literal parses and type-checks clean**, and its
  pattern is *not* validated — pin the deferral explicitly so the `regex` increment finds
  a test to change rather than a silent gap.
- **Every §19 ordering pair**: `import` after each of the four later categories;
  `param` after each of `type`, `fn`, `const`, `readonly` and a top-level statement.
- **Two-mode preserved**: a file with a misordered `param` *and* three unrelated type
  errors reports four diagnostics, in source order.
- **The D-412 cascade**, both directions: misplaced-and-colliding reports E2202 alone;
  correctly-placed-and-colliding reports E1102.
- **Decorator in a function parameter list is E4002**, with recovery — the rest of the
  signature and the function body still parse.
- **`ParamDecl.Range` covers the decorator stack**, asserted on start line and column.
- **Parser recovery unchanged** for every case in `ParserParamDeclRecoveryTests` whose
  fixture is not itself being corrected.
- **All eleven validation scripts still parse and type-check with zero diagnostics.**
  Script 09's `warn_percent`/`crit_percent` are the obvious `@minValue`/`@maxValue`
  candidates — **do not add decorators to them here.** That is R-08's work, and adding
  them would put an unenforced constraint in the release gate.

**Mutation-verify the ordering walk and the cascade suppression.** For each: remove the
guard, **predict the exact failure signature before running**, confirm the prediction,
restore. A guard verified in one direction is half a guard.

---

## 5. Deliverables

- Six change groups per §2.
- **Error-code count 120.** Confirm the D-316 gate is green and say so explicitly, naming
  which commit carried each catalog/registry pair.
- Coverage: `Grob.Compiler` at or above the D-328 90% bar. Report the figure.
- Full solution `dotnet test` green. Report the test count.
- A decisions-log entry in three-location lockstep. **Take the D-number from the live
  tail of the merged log**, never from memory or this prompt.
- R-01, R-02 and R-03 moved to the register's Closed Items table with the D-number that
  closed them.

---

## 6. STOP conditions

Stop and report, do not proceed:

- Any row in §1.1 behaves differently from what D-424 expects — including a code that
  turns out to *have* a throw site.
- §1.5 shows that pass-1 `param` registration cannot be separated from Sprint 10's
  parameter binding.
- §1.3 finds a breaking change beyond the decorator fixtures — particularly a gold master
  or validation script that depends on a misordered declaration compiling.
- The ordering walk cannot satisfy both Decision 5 constraints at once.
- Static validation of any decorator turns out to need a bound parameter value, which
  would mean D-424's Decision 3 split is in the wrong place.
- The change requires a new error code, an opcode, or a `GrobValueKind` variant.
