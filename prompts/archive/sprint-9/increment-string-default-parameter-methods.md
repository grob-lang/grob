# Sprint 9 — Increment: `padLeft`/`padRight`/`truncate` — string default-parameter methods

**Branch:** `feat/string-default-parameter-methods`
**One concern:** land the three deferred `string` methods that carry default parameters, by
wiring D-364's synthesis helper into the primitive-member emission branch. This completes the
`string` surface and retires the D-363 build-status caveat.

Runs against the fresh corpus zip carrying D-356 through D-364. Corpus-first discipline
throughout; read the live decisions log and registry tails, do not trust this prompt or memory
for D-### numbers or error codes.

---

## Authority and context

- **D-363** delivered 21 of the 24 documented `string` members and deferred exactly three —
  `padLeft(width: int, char: string = " ")`, `padRight(width: int, char: string = " ")`,
  `truncate(maxLength: int, suffix: string = "...")` — because they carry default parameters and
  shipping them with *required* parameters would have been a signature narrower than advertised.
  `PrimitiveMemberMethod` was given an **inert** `ParameterDefaults` field at that point
  specifically so this increment would be additive.
- **D-364 built the mechanism.** `NativeDefaultArgumentFill` (`Grob.Compiler`) is a pure,
  branch-agnostic `(suppliedCount, fullArity, defaults) -> IReadOnlyList<GrobValue>` helper with
  no `Chunk`/emission dependency, explicitly designed to be callable from the primitive-member
  and named-type-method branches once they carry default metadata. D-364 wired it into the
  **namespace-native** path only. This increment wires the **second** branch.
- **Nothing new is being designed.** The registry field exists, the helper exists, the arity
  range logic (`RequiredArgumentCount`, contiguous trailing defaults) exists. This is
  application: populate three entries, wire the branch, add three runtime natives.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Map on the live source tree and report:

1. **`PrimitiveMemberMethod.ParameterDefaults`** — confirm the field's exact type and that it is
   `null` on all 19 current `string` method entries.
2. **The primitive-member arity/type validation** (`ValidatePrimitiveMemberCall`,
   `TypeChecker.Expressions.cs`) — how it currently checks exact arity (E0003) and per-argument
   type (E0004). Determine how to apply D-364's **`RequiredArgumentCount`** walk-back here.
   **Reuse D-364's helper if it is accessible from this path; if it is private to the namespace
   arm, promote it to a shared location rather than writing a second copy** — two divergent
   copies of the required-count rule is the failure mode to avoid. Report which.
3. **The primitive-member emission branch** (`VisitCall`'s `ResolvedPrimitiveNativeName` arm,
   `Compiler.Expressions.cs`) — where the receiver and supplied arguments are emitted before
   `Call`. Determine where `NativeDefaultArgumentFill.Resolve`'s constants are `EmitConstant`ed
   (after the supplied arguments, before `Call`) and confirm the `Call` operand must become
   `1 + full declared arity` rather than `1 + node.Arguments.Count` — the receiver-as-arg[0]
   equivalent of D-364's namespace-path fix.
4. **`StringMethodsPlugin`** (`Grob.Stdlib`) — the registration shape for the three new
   qualified natives (`string.padLeft`, `string.padRight`, `string.truncate`), each at **fixed**
   arity (receiver + full parameter count).
5. **`truncate`'s semantics** — confirm against `grob-type-registry.md`: truncates to max length
   and appends the suffix **if truncated**. Pin the edge cases before coding: is `maxLength` the
   total length including the suffix, or the pre-suffix cut? What happens when `maxLength` is
   shorter than the suffix, and when it is negative? Report the intended answers; if the registry
   is silent, propose the least-surprising reading and flag it as a decision to record.
6. **`padLeft`/`padRight` edge cases** — a `char` argument that is not exactly one character, and
   a `width` shorter than the input (should return the input unchanged, not truncate). Report
   whether a multi-character `char` is an error or takes the first character; propose
   least-surprising if the registry is silent.

Report the three registry entries, the validation change (and whether `RequiredArgumentCount`
was reused or promoted), the emission wiring, the runtime natives, the pinned edge-case
semantics, and the test list. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

1. Populate `ParameterDefaults` on the three `PrimitiveMemberRegistry` `string` entries:
   `padLeft`/`padRight` → `[null, GrobValue.FromString(" ")]`;
   `truncate` → `[null, GrobValue.FromString("...")]`.
2. Apply the required-to-full arity range in `ValidatePrimitiveMemberCall`, reusing D-364's
   `RequiredArgumentCount` (promoted to shared if needed). Natives without defaults keep exact-count
   `E0003` behaviour unchanged; only supplied arguments are type-checked (E0004).
3. Wire `NativeDefaultArgumentFill.Resolve` into the primitive-member emission branch —
   `EmitConstant` the returned defaults after the supplied arguments, and emit `Call` with the
   full arity (receiver included).
4. Add the three runtime natives to `StringMethodsPlugin` at fixed arity, implementing the pinned
   semantics.

---

## Scope boundaries — do NOT

- **Do not touch the namespace-native path** (D-364, landed) or `date.parse`.
- **Do not wire the named-type-method (`NamedTypeMethod`) branch** — no nominal method declares
  defaults yet; it rides the same helper when one does.
- **Do not build `int`/`float`/`bool` instance methods** — separate follow-on increments.
- **Do not duplicate `RequiredArgumentCount`.** Reuse or promote; never a second copy.
- **No new opcode** (`EmitConstant` reused). **No new error code** — arity via existing `E0003`,
  argument types via `E0004`. If a pinned edge case genuinely needs a new runtime error, STOP and
  escalate via the `allocating-an-error-code` ladder rather than minting inline. Count stays 118.

---

## Tests — TDD, red first, same commit

- Each of the three methods called **with** and **without** its optional argument, end-to-end:
  `"7".padLeft(3)` → `"  7"`; `"7".padLeft(3, "0")` → `"007"`; same for `padRight`;
  `"hello world".truncate(8)` and `"hello world".truncate(8, "…")`.
- **Bytecode-shape tests** for the omitted-argument form: the synthesised default constant is
  emitted after the supplied arguments and `Call`'s operand is the full arity (receiver
  included).
- The pinned edge cases from the gate: `width` shorter than input returns input unchanged;
  `maxLength` shorter than the suffix; multi-character `char`; negative `width`/`maxLength`.
- Under-supplying below the required count (`"x".padLeft()`) still raises `E0003`; a supplied
  argument of the wrong type (`"x".padLeft("3")`) still raises `E0004`.
- The `PrimitiveMemberRegistryAgreementTests` still passes with the three new natives registered.
- Every existing `string`-member test unchanged.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-365**; confirm, do not assume. The
  entry records: the three deferred `string` methods landed, completing the documented `string`
  surface; D-364's `NativeDefaultArgumentFill` wired into its **second** branch (primitive-member),
  proving the helper's branch-agnostic design; whether `RequiredArgumentCount` was reused in place
  or promoted to shared; the `Call`-operand full-arity fix on the primitive path; the pinned
  `truncate`/`padLeft`/`padRight` edge-case semantics (and any that warranted recording as a
  decision in their own right); and that `NamedTypeMethod` remains the one branch still unwired,
  pending a nominal method that declares defaults. No new opcode, no new error code, count 118.
  Cite D-363, D-364, D-358, D-066.
- **Update `grob-type-registry.md`** — the `string` build-status note currently reads "built,
  except `padLeft`/`padRight`/`truncate` … pending D-358". Rewrite it to record the `string`
  surface as **fully built**, citing this D-###.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt under
  `prompts/archive/sprint-9/`.
