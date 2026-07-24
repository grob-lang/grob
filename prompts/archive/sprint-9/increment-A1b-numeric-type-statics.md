# Consolidation — Increment A1b: `int`/`float` type-static functions

**Branch:** `feat/numeric-type-statics`
**One concern:** deliver the six documented type-static functions — `int.min`, `int.max`,
`int.clamp`, `float.min`, `float.max`, `float.clamp` — as namespace-receiver calls. Completes
the numeric surface A1a began.

Runs against the fresh corpus zip carrying D-356 through D-369. Corpus-first discipline
throughout; read the live decisions log, error-code and opcode registry tails, do not trust this
prompt or memory for D-### numbers or error codes.

---

## Authority and context

- **The gap.** The advertised-vs-built audit found the numeric surface unbuilt; D-369 delivered
  the **instance** members (`int.abs()`, `float.roundTo()`, …) on `PrimitiveMemberRegistry` and
  explicitly deferred the **type-static** functions to this increment, because they are
  namespace-receiver calls belonging to `NamespaceRegistry`, not instance members.
- **The advertised surface is exactly six functions**, and the design corpus and wiki agree —
  `grob-type-registry.md` (`int` §, `float` §) and `wiki/Type-Registry/{int,float}.md`:

  | `int.min(a, b)` | `(int, int) → int` |
  | `int.max(a, b)` | `(int, int) → int` |
  | `int.clamp(v, lo, hi)` | `(int, int, int) → int` |
  | `float.min(a, b)` | `(float, float) → float` |
  | `float.max(a, b)` | `(float, float) → float` |
  | `float.clamp(v, lo, hi)` | `(float, float, float) → float` |

  No `bool` or `string` statics are advertised — do not invent any.
- **No `math` overlap.** `MathPlugin` registers 14 natives (`sqrt`, `pow`, `log`, the trig
  family, `random*`, `toDegrees`/`toRadians`) plus three `RegisterConstant` entries
  (`math.pi`, `math.e`, `math.tau`). It has **no** `min`, `max` or `clamp`. These six functions
  have no existing home and nothing to delegate to — confirm this rather than assume it.
- **The parser needs no change.** `int`, `float`, `bool` and `string` are **not** keywords —
  `Lexer.LookupKeyword` has no entry for them, so they fall through to `TokenKind.Identifier`.
  Type names are recognised *contextually* in annotation position (`TypeChecker.cs`'s
  `"int" => GrobType.Int` mapping in `ResolveSignatureType`). Therefore `int.min(1, 2)` already
  lexes and parses **identically** to `math.min(1, 2)` — an identifier in receiver position
  followed by `.` and a member name. This increment is namespace registration, not grammar work.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **The `NamespaceRegistry` entry shape** and how an existing namespace (`math`) declares its
   members — arity, parameter types, return type. Confirm adding `int` and `float` namespace
   entries is purely additive.
2. **Namespace symbol pre-registration.** Namespaces are also pre-registered as `NamespaceDecl`
   symbols in the global scope. Confirm where that happens and that adding `int`/`float` follows
   the `math`/`date`/`path` pattern exactly.
3. **The shadowing consequence — report before deciding.** `_reservedIdentifiers` currently holds
   only `{"formatAs", "select"}`; type names are **not** reserved. Once `int` is a pre-registered
   non-provisional namespace symbol, a user writing `int := 5` will collide in
   `FinalizeTopLevelBinding` and receive **E1102** ("already declared"), where today it is legal.
   Confirm this is exactly what already happens for `math := 5` — if the precedent holds, adopt
   it unchanged (consistency with existing namespaces beats a special case). **Do not add
   `int`/`float` to `_reservedIdentifiers`** — `formatAs` is reserved for the separate D-320
   reason, not because it is a namespace. Report the confirmed behaviour either way.
4. **Annotation position must be unaffected.** `x: int` resolves through `ResolveSignatureType`'s
   string mapping, not symbol lookup. Confirm registering `int` as a namespace symbol cannot
   disturb annotation resolution, struct-field annotations or return-type annotations.
5. **THE AGREEMENT-TEST INTERACTION — this is the known trap.** D-369 extended
   `PrimitiveMemberRegistryAgreementTests`'s orphan-detection filter from the `"string."` prefix
   to `"int."`, `"float."` and `"bool."`, so that any registered native under those prefixes with
   no `PrimitiveMemberRegistry` entry is reported as an orphan. Registering `int.min` through
   `NamespaceRegistry` puts a native under the `"int."` prefix that is **deliberately** not a
   primitive member — it will trip that check. Reconcile the two agreement tests so each still
   catches real drift: the primitive-member orphan check must exclude names owned by
   `NamespaceRegistry`, and the namespace agreement test must cover the new entries. **Weakening
   either check to make the build green is not acceptable** — both must still fail on genuine
   drift. Report the reconciliation design before implementing it.
6. **Runtime plugin home.** A1a added `NumericMethodsPlugin` for the instance natives. Determine
   whether the six statics belong there or in a separate plugin, given they are registered
   against a different compile-time registry. Note that instance natives are qualified
   `int.abs` and statics `int.min` — same prefix, distinct names, one shared global native
   table. Report the choice and confirm no name collision.
7. **`CallExpr.ResolvedReturnType` (D-362)** — confirm the namespace-native path already sets it,
   so `int.max(a, b) + 1` selects the correct typed opcode. D-362 covered native/stdlib calls;
   verify it needs no change for the new namespaces.

Report the registry entries, the symbol registration, the confirmed shadowing behaviour, the
agreement-test reconciliation, the plugin home, and the test list. Then STOP.

---

## Semantics to pin — decide in plan, do not improvise

- **`clamp` with `lo > hi`.** Undefined by the corpus. `Math.Clamp` in .NET **throws** for an
  inverted range. Decide deliberately: fault through the native seam as a catchable `GrobError`
  (the D-366 idiom, reusing an existing leaf/code — `ArithmeticError`/`E5001` is the expected
  fit), or define a total function (e.g. clamp to `hi` then `lo`). Recommended: **fault** — an
  inverted range is a caller bug and silently returning a plausible number hides it. Whatever is
  chosen, it must be documented in the corpus and tested.
- **`float.min`/`max` with `NaN`.** .NET's `Math.Min(double, double)` returns `NaN` if either
  argument is `NaN`. Confirm and pin this explicitly — it is the IEEE-consistent answer and
  aligns with D-315's float-equality semantics. Test both argument positions.
- **`float.min`/`max` with `+0.0`/`-0.0`.** .NET treats `-0.0` as less than `+0.0` here. Pin and
  test, consistent with D-315.
- **`int` overflow.** None of `min`/`max`/`clamp` can overflow — they select an existing operand,
  never compute. Confirm this and state it, so no unnecessary `checked(...)` guard is added.

---

## Scope boundaries — do NOT

- **Do not add `bool` or `string` statics** — none are advertised.
- **Do not touch the instance-member surface** (D-369) or `math`'s own functions.
- **Do not add `int`/`float` to `_reservedIdentifiers`** — follow the `math` namespace pattern.
- **Do not make type names keywords.** They lex as identifiers by design; changing that is a
  grammar change with wide blast radius and is not in scope.
- **Do not weaken either agreement test** to resolve the prefix interaction.
- **Do not build array or map members** — C0a and C0b.
- **No new opcode.** Reuse the `GetGlobal`+`Call` namespace-native shape.
- **No new error code** — reuse an existing leaf/code for the `clamp` fault if faulting is
  chosen. Count stays 118. If nothing fits, STOP and escalate via `allocating-an-error-code`.

---

## Tests — TDD, red first, same commit

- Each of the six functions: a type-checker resolution test, a compiler bytecode-shape test
  (`GetGlobal` by qualified name then `Call`), and an end-to-end value test through the CLI.
- Argument-type errors: `int.min(1.0, 2)` and `float.max(1, 2.0)` raise the existing
  argument-type code; wrong arity raises the existing arity code.
- The pinned semantics: `clamp` with `lo > hi`; `float.min`/`max` with `NaN` in each position;
  `float.min`/`max` with `+0.0`/`-0.0`; `int.clamp` at and outside both bounds.
- **Shadowing behaviour asserted explicitly** — whatever the gate confirms for `int := 5`, lock
  it with a test so the behaviour is deliberate and cannot drift silently.
- **Annotation position unaffected** — `x: int`, a struct field typed `int`, and a function
  returning `float` all still resolve. This is the regression that would matter most.
- Operand typing: `int.max(a, b) + 1` and `float.min(x, y) * 2.0` select the correct typed
  opcode (D-362).
- **Both agreement tests still fail on genuine drift** — add a test or a documented mechanism
  proving the reconciled primitive-member and namespace checks each still catch an orphan and a
  missing registration. This is the increment's most important test.
- Every existing `math`, instance-member and `string` test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-370**; confirm, do not assume. The
  entry records: the six type-statics delivered as namespace-receiver calls, completing the
  numeric surface D-369 began; that type names lex as identifiers so no grammar change was
  needed; the namespace symbol registration following the `math` pattern and the confirmed
  `int := 5` shadowing behaviour (with the `math := 5` precedent that justifies it); the
  agreement-test reconciliation between the `"int."`/`"float."` prefix orphan check and
  `NamespaceRegistry` ownership, and how both still catch drift; the pinned `clamp` inverted-range,
  `NaN` and signed-zero semantics; the confirmed absence of `math` overlap; the plugin home. No
  new opcode, no new error code, count 118. Cite D-369, D-368, D-363, D-342, D-362, D-366,
  D-315, D-320, and the advertised-vs-built audit.
- **Update the corpus** — `grob-type-registry.md`'s `int`/`float` build-status notes now cover the
  statics; document the pinned `clamp`/`NaN` semantics in the corresponding rows and in
  `wiki/Type-Registry/{int,float}.md`, citing this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs, updated wiki pages).
  Archive this prompt under `prompts/archive/sprint-9/`.
