# Sprint 9 — Increment: primitive instance-method and property dispatch (string surface)

**Branch:** `feat/string-instance-methods`
**One concern:** build the compile-time-sugar dispatch mechanism for instance methods and
properties on a **primitive-typed value receiver** (D-066), and deliver the `string`
surface on it — the no-default subset. This closes a v1 release-gate blocker.

Runs against the fresh corpus zip carrying D-356 through D-362. Corpus-first discipline
throughout; read the live decisions log and registry tails, do not trust this prompt or
memory for D-### numbers or error codes.

---

## Authority and context

- **The gap.** D-362's investigation established there is **no compiler-side dispatch for
  any primitive-receiver method call** — no arm in `ResolveMemberAccessCall`
  (`TypeChecker.Expressions.cs`) for a `String`/`Int`/`Float`/`Bool` receiver, no emission
  path in `VisitCall`. `grob-type-registry.md` documents the `string` surface as "confirmed
  additions", and `grob-v1-requirements.md` line 843 states string operations are "instance
  methods on the `string` type (already in the type registry)" — but the implementing
  mechanism was never built or scheduled. `StringsPlugin` correctly registers only
  `strings.join` (its receiver is an array, D-071); its docstring names every other string
  operation as "an instance method on the `string` type, out of scope here" — that is the
  surface this increment builds.
- **Release-gate blocker.** The validation scripts call `file.name.replace(from, to)`,
  `line.split("|")` and `.contains()`, and the scripts doc demonstrates "fluent string
  methods" as a headline feature. The v1 gate cannot pass until these dispatch.
- **The design is D-066.** "Primitives never boxed; method-call syntax is compile-time
  sugar — the compiler rewrites to native function calls at compile time. Zero runtime
  overhead." This increment finally implements that model, `string` first. The other
  primitives (`int`/`float`/`bool` instance methods, and the `int.min`/`float.clamp`
  type-static functions, line 1077) ride the same mechanism in follow-on increments.
- **Precedent to mirror.** `NamespaceRegistry` (D-342) and `NamedTypeRegistry` (D-361) are
  the two compile-time-twin-of-runtime-registration registries, both agreement-tested in the
  D-308 mould. This adds a third, keyed on **(primitive type, member)**.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Map on the live source tree and report:

1. **`ResolveMemberAccessCall`'s existing routing** at a member-access node — how it currently
   dispatches a namespace receiver (`NamespaceRegistry`, D-342), an array/map receiver
   (descriptor, D-351), a nominal receiver (`NamedTypeRegistry`, D-361) and a struct-field
   access. Determine exactly where a **primitive-value receiver** route slots in without
   disturbing the others.
2. **Property access on a primitive** — `s.length` / `s.isEmpty` (no parens) versus a method
   call `s.trim()`. How the parser and type checker distinguish property from method access,
   so the registry can carry both (mirroring `NamedTypeProperty`/`NamedTypeMethod`).
3. **The native-call emission shape** the rewrite reuses: D-342's "emit args, then
   `GetGlobal` by qualified name, then `Call`". Confirm a rewritten `s.split(sep)` becomes a
   native call with the **receiver injected as arg[0]** (e.g. `string.split` invoked with
   `[s, sep]`), and that no new opcode is needed.
4. **Where the runtime natives live.** `StringsPlugin` scopes instance methods out. Determine
   whether they belong in a new `StringMethodsPlugin` or an added registration surface, and how
   the composition root wires it — so the agreement test can diff compile-time registry against
   live registration.
5. **The nullable-return and throwing-method seams.** `toInt()`/`toFloat()` return `int?`/
   `float?` (nil if unparseable); `substring`/`left`/`right` throw `IndexError` out of range —
   confirm the existing nullable-return handling and the native-throw seam (the same `IndexError`
   the array indexer already raises; reuse its code, do not mint a new one).
6. **`CallExpr.ResolvedReturnType` (D-362)** — the primitive-method type-checker arm must set it
   so a numeric-returning method used as an arithmetic operand (`s.indexOf(x) + 1`,
   `s.length`) selects the correct opcode.
7. **The error code for an undefined method on a primitive** — reuse the existing undefined-member
   code (the one C0c/D-361 cites, `E1002`); confirm, do not mint new.

Report the registry schema and placement, the routing insertion, the emission rewrite, the
runtime-native home, the agreement-test design and the test list. Then STOP.

---

## Design — recommended, confirm or adjust in plan

A **primitive-member registry** (compile-time twin), keyed on (primitive `GrobType`, member
name), one entry per member carrying: kind (property or method); for methods — arity, parameter
types, return type; the qualified native name the call rewrites to. Mirror the
`NamedTypeProperty`/`NamedTypeMethod` field shapes; primitives are scalar `GrobValue` kinds, not
`Struct`-discriminated, so this is a **parallel** registry to `NamedTypeRegistry`, not an entry
within it — and the dispatch differs (rewrite to a qualified native with the receiver as arg[0],
not `NamedTypeMethod`'s `Bind(GrobStruct)` binder), so mirror the *shape*, not the binder.

**Include an (empty) `ParameterDefaults` field on the method entry now, even though this
increment builds no default-parameter methods.** `NamedTypeMethod` already carries
`ParameterDefaults` (`IReadOnlyList<GrobValue?>?`, added by C0c, `null` on every current
entry) — mirroring it here, `null` for all 22 members, means D-358 is **purely additive**: it
populates the field and wires the call-site synthesis for `padLeft`/`padRight`/`truncate`
rather than having to alter this entry type after the fact. Do not build the synthesis
mechanism here — the field is inert this increment (D-358 owns it).

- **Type checker:** `ResolveMemberAccessCall` gains a primitive-value-receiver arm — look up
  (receiver type, member) in the registry; resolve parameter/return types from the entry; set
  `CallExpr.ResolvedReturnType`. Property access resolves the property entry's type.
- **Emission:** `VisitCall` (and the property path) rewrites `receiver.member(args)` to the
  entry's native — emit receiver as arg[0], then args, then `GetGlobal` by qualified name, then
  `Call` (D-342 shape). No new opcode.
- **Runtime:** a plugin registers each native (`string.split`, `string.replace`, …), captured by
  the agreement test's recording registrar.
- **Agreement test:** `PrimitiveMemberRegistryAgreementTests`, mirroring
  `NamedTypeRegistryAgreementTests` (D-361) — the compile-time registry diffed both ways against
  live native registration; a member advertised without a native, or a native without an entry,
  fails CI.

---

## Surface — `string`, the no-default subset (build all of these)

Properties: `length → int`, `isEmpty → bool`.
Methods: `toInt() → int?`, `toFloat() → float?`, `trim()`, `trimStart()`, `trimEnd()`,
`upper()`, `lower()`, `split(sep: string) → string[]`, `contains(s: string) → bool`,
`startsWith(s: string) → bool`, `endsWith(s: string) → bool`,
`replace(from: string, to: string) → string`, `indexOf(s: string) → int`,
`lastIndexOf(s: string) → int`, `substring(start: int, length: int) → string` (throws
`IndexError`), `repeat(count: int) → string`, `left(n: int) → string` (throws `IndexError`),
`right(n: int) → string` (throws `IndexError`), `toString() → string` (identity).

Semantics per `grob-type-registry.md`'s `string` section — reproduce exactly (`replace` is
replace-**all**; `indexOf`/`lastIndexOf` return `-1` when absent; `substring` is zero-based).

---

## Scope boundaries — do NOT

- **Do not build `padLeft`, `padRight` or `truncate`.** They carry default parameters
  (`char = " "`, `suffix = "..."`) and depend on D-358's default-argument mechanism, which is
  its own next increment. Update `grob-type-registry.md`'s build-status note to record the
  `string` surface as built **except** these three, pending D-358 — do not ship them with
  required parameters (that would break the advertised signature).
- **Do not build the `int`/`float`/`bool` instance-method surfaces** or the `int.min`/
  `float.clamp` type-static functions — same mechanism, follow-on increments. This increment
  proves the mechanism on `string`.
- **Do not touch array/map method dispatch** (descriptor-based, D-351) or the nominal/namespace
  registries beyond adding the primitive route alongside them.
- **No new opcode.** Reuse the `GetGlobal`+`Call` native shape.
- **No new error code** — reuse the undefined-member code and the existing `IndexError`. Count
  stays 118. If plan-mode finds no suitable existing code for a genuinely new condition, STOP
  and escalate via the `allocating-an-error-code` ladder rather than minting inline.

---

## Tests — TDD, red first, same commit

- Each built member: a type-checker resolution test, a compiler bytecode-shape test (rewrite to
  the correct native with receiver as arg[0]), and an end-to-end value test through the CLI.
- The **release-gate unblock**: `"a|b|c".split("|")`, `"x".replace("x", "y")`,
  `"abc".contains("b")` compile and run correctly — the exact shapes the validation scripts use.
- Property access: `s.length`, `s.isEmpty`.
- Numeric-return-as-operand: `s.indexOf("x") + 1` and `s.length * 2` select the int path
  (`CallExpr.ResolvedReturnType` wired).
- Nullable returns: `s.toInt()` is `int?` — using it unguarded in an int context is the
  existing nullable diagnostic; with `??`/guard it unwraps.
- Throwing: `substring`/`left`/`right` out of range raise `IndexError`.
- The `PrimitiveMemberRegistryAgreementTests` consistency check.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and
  pre-push (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never
  "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-363**; confirm, do not assume. The
  entry records: implements D-066's primitive-method-as-compile-time-sugar model, proven on
  `string`; the new primitive-member registry (schema, placement, keyed on primitive
  `GrobType` × member, parallel to `NamedTypeRegistry`, its method entry mirroring
  `NamedTypeMethod`'s shape including an inert `ParameterDefaults` field so D-358 is additive); the `ResolveMemberAccessCall`
  primitive-value route and the `VisitCall` native rewrite (receiver as arg[0], no new opcode);
  the 22-member no-default `string` surface delivered; `padLeft`/`padRight`/`truncate` deferred
  to D-358; `CallExpr.ResolvedReturnType` wired for numeric returns; `IndexError` and the
  undefined-member code reused; the `PrimitiveMemberRegistryAgreementTests` added; and that this
  **closes the release-gate blocker** — the validation scripts' `.split`/`.replace`/`.contains`
  now dispatch. No new opcode, no new error code, count 118. Cite D-066, D-091, D-071, D-362,
  D-342, D-361, D-358 (the deferred-methods dependency).
- **Update `grob-type-registry.md`** — the `string` build-status note now reads "built except
  `padLeft`/`padRight`/`truncate`, pending D-358", citing this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
