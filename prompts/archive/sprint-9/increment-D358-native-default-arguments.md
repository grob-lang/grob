# Sprint 9 — Increment: native default arguments (D-358), proven on `date.parse`

**Branch:** `feat/native-default-arguments`
**One concern:** build the default-argument synthesis mechanism for native calls (D-358)
and land `date.parse`'s optional `pattern` argument on it. Not the string default methods,
not `fs` — those ride the mechanism later.

Runs against the fresh corpus zip carrying D-356 through D-363. Corpus-first discipline
throughout; read the live decisions log and registry tails, do not trust this prompt or
memory for D-### numbers or error codes.

---

## Authority and context

- **D-358** is the authority: native functions support optional trailing parameters with
  compile-time constant defaults; the compiler synthesises the missing trailing arguments
  before the `Call` so the runtime native keeps a **fixed** arity. Default arguments, not
  overload resolution.
- **The metadata field is half-present.** `NamedTypeMethod.ParameterDefaults` (C0c) and
  `PrimitiveMemberMethod.ParameterDefaults` (D-363) already carry an inert defaults field —
  but `NamespaceRegistry.NativeMember` (`Grob.Compiler/NamespaceRegistry.cs`) has **none**;
  its own comment reads "v1 core-module natives take no named or defaulted arguments." Since
  `date.parse` is a namespace native (`NamespaceRegistry.cs:146` —
  `["parse"] = new NativeMember([GrobType.String], GrobType.Struct, NamedTypeName: "date")`),
  adding that field is this increment's first edit.
- **The injection shape already exists.** `EmitConstant(GrobValue, line)` pushes a constant,
  and `Compiler.Expressions.cs:743` already injects a synthesised `GrobValue.FromString(
  string.Empty)` argument (the `input()`/`formatAs` precedents, D-344/D-345). D-358
  generalises that one-off shape into a reusable default-fill.
- **Proving case.** `date.parse` gains `parse(input: string, pattern: string = "")` — empty
  pattern means ISO-8601 (today's one-arg behaviour, unchanged), non-empty means
  `DateTimeOffset.ParseExact`. Failure reuses the existing `ParseError`
  (`ExceptionHierarchy.cs:34`) the one-arg form already throws.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Map on the live source tree and report:

1. **`NamespaceRegistry.NativeMember`** — add a `ParameterDefaults` field mirroring
   `NamedTypeMethod.ParameterDefaults` (`IReadOnlyList<GrobValue?>?`, `null` for every
   existing entry). Confirm the type and that every current entry is unaffected.
2. **The namespace-native validation path** (`TypeChecker.Expressions.cs` ~959–1010, the
   `NativeMember` case) — where arity (E0003) and per-argument type (E0004) are checked
   positionally today. Determine how to accept an under-supplied call when the missing
   trailing parameters all have non-null defaults, while still raising E0003 when fewer than
   the **required** (non-defaulted) count is supplied. Confirm the interaction with the
   existing `VariadicElementType` path (defaults and variadics must not both apply to one
   entry — reject or assert that at registry-authoring time).
3. **The namespace-native emission** (`Compiler.Expressions.cs`, the `GetGlobal`-callee-first
   shape `EmitFormatAsCall` at line 777 exemplifies) — where to emit the synthesised trailing
   default constants (via `EmitConstant`) so the runtime native receives its full fixed arity.
4. **A branch-agnostic synthesis helper.** Design the default-fill so it operates on (supplied
   argument count, full arity, `ParameterDefaults` list) and can be called from **any** native
   emission branch — namespace-native now, the primitive-member branch (D-363) and
   nominal-instance (`NamedTypeMethod`) later — rather than being welded into the namespace
   path. Wire only the namespace path in this increment; report how the other branches would
   call it.
5. **The `date.parse` runtime native** (`Grob.Stdlib`, the date plugin) — currently arity 1.
   It becomes arity 2 `(input, pattern)`; `pattern == ""` takes the ISO path, non-empty takes
   `ParseExact`, and a parse failure raises `ParseError` exactly as the one-arg form does.
6. **`ParseError`/the `date.parse` error code** — confirm the existing code the one-arg form
   throws, reused verbatim. No new code.

Report the `NativeMember` field, the validation change, the synthesis helper's signature and
call sites, the `date.parse` entry and runtime change, and the test list. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

1. Add `ParameterDefaults` to `NamespaceRegistry.NativeMember`; update its doc comment (the
   "no defaulted arguments" line is now false).
2. Build the branch-agnostic default-fill helper generalising the `Compiler.Expressions.cs:743`
   injection: for a call supplying N of an M-arity native whose trailing M−N parameters carry
   non-null defaults, `EmitConstant` each default in order after the supplied arguments, before
   `Call`. Runtime arity stays fixed at M.
3. Modify the namespace-native validation to accept N ≥ required-count and validate each
   supplied argument against its slot; raise E0003 only below the required count.
4. `date.parse`: extend its `NativeMember` to `[String, String]` with
   `ParameterDefaults: [null, GrobValue.FromString("")]`; extend the runtime native to arity 2
   with the ISO/`ParseExact` split; `ParseError` on failure, unchanged.

---

## Scope boundaries — do NOT

- **Do not build the string default methods** (`padLeft`/`padRight`/`truncate`). They live in
  the primitive-member registry (D-363) — a different emission branch — and are their own
  small follow-on that applies this increment's helper. This increment leaves the D-363
  build-status note as-is (still "3 pending").
- **Do not touch `fs`** — `fs.copy`/`move`'s `overwrite = false` rides this helper when `fs` is
  built, not here.
- **Do not wire the nominal-instance (`NamedTypeMethod`) or primitive-member branches** — build
  the helper so they *can* call it, but wire only the namespace-native path (date.parse is the
  sole consumer this increment).
- **Do not add overload resolution.** One function, optional trailing tail, constant defaults
  only — no dispatch on argument count or type.
- **No new opcode** (reuse `EmitConstant`). **No new error code** (`ParseError` reused, arity via
  existing E0003). Count stays 118.

---

## Tests — TDD, red first, same commit

- `date.parse("2026-04-05")` (one-arg, ISO) still parses — unchanged behaviour, and the
  **bytecode-shape test** confirms the compiler now emits the synthesised `""` default as the
  second argument before `Call` (fixed arity 2).
- `date.parse("05/04/2026", "dd/MM/yyyy")` (two-arg) parses via `ParseExact`.
- `date.parse("nonsense")` and `date.parse("x", "dd/MM/yyyy")` with mismatched input raise
  `ParseError` — same code as today.
- An under-supplied call to a native **without** defaults still raises E0003; a call supplying
  fewer than the required count of a defaulted native still raises E0003.
- The synthesis helper unit-tested directly on the (supplied, arity, defaults) shape.
- Existing `date`/namespace gold masters unchanged except `date.parse`'s new bytecode shape.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and
  pre-push (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never
  "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-364**; confirm, do not assume. The
  entry records: the `NativeMember.ParameterDefaults` field added (mirroring
  `NamedTypeMethod`/`PrimitiveMemberMethod`); the branch-agnostic default-fill helper
  generalising the D-344/D-345 `EmitConstant`-injection one-off, wired into the namespace-native
  path only; the validation change (accept ≥ required count, E0003 below it, no default+variadic
  overlap); `date.parse`'s optional `pattern` argument (empty → ISO, non-empty → `ParseExact`,
  `ParseError` reused); the runtime native taken to fixed arity 2; and that the string default
  methods, `fs.copy`/`move` and nominal-instance defaults now ride the helper additively. No new
  opcode, no new error code, count 118. Cite D-358, D-342, D-344, D-345, D-363.
- **Update `grob-stdlib-reference.md`** — `date.parse`'s two-argument form is now real, not
  aspirational, citing this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
