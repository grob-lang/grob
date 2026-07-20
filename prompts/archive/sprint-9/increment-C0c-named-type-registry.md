# Sprint 9 — Increment C0c: named-type registration table (D-356)

**Branch:** `feat/named-type-registry`
**One concern:** build the declarative named-type registry (D-356) and migrate `guid` and
`date` onto it as behaviour-preserving proving cases. No other type, no new surface.

Runs against the fresh corpus zip carrying D-356 through D-360. **The corpus zip must
include `grob-type-registry.md`** — this increment updates it at landing. Corpus-first
discipline throughout; read the live decisions log and registry tails, do not trust this
prompt or memory for D-### numbers or error codes.

---

## Authority and context

- **D-356** is the authority. A nominal, `Struct`-discriminated type is currently wired into
  the compiler and runtime by hand-added, string-matched arms across **six** dispatch
  surfaces, each keyed off the type name:
  1. `ResolveSignatureType` (`TypeChecker.cs`) — type name in parameter/return annotation.
  2. `ResolveNamedFieldType` (`TypeChecker.Declarations.cs`) — type name in a struct-field annotation.
  3. `TryGetNamedStructTypeName` (`TypeChecker.cs`) — nominal-name recovery.
  4. The method/property validator (`ValidateDateMethodCall`/`CheckDateMethodArgs`; guid's equivalents).
  5. VM instance dispatch — the property/method arms keyed off the struct's type name.
  6. `ValueDisplay` (D-336) — the registered `toString()` renderer.
- D-355 demonstrated the failure mode: `date` needed a `"date"` arm added beside `"guid"` at
  each annotation site, and a missed arm resolves a valid type as `E1001`. Seven more nominal
  types land across Sprint 9 and 11; a missed arm somewhere is close to certain.
- **The move** is the one `NamespaceRegistry` (D-342) already made for *module members* — a
  compile-time twin of runtime registration, agreement-tested in the D-308/D-342 pattern with
  drift a CI failure — extended from module namespaces to **instance surfaces** on nominal
  types. This increment builds that table and proves it on `guid` and `date`; `File` through
  `ProcessResult` then land as registry **data**, one entry each, in later increments.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

Map on the live source tree and report:

1. **Every one of the six dispatch arms, for both `guid` and `date`** — exact methods, exact
   behaviour. Enumerate each guid/date property, each method (name, arity, parameter types
   including any nominal-identity rule, return type), and the `ValueDisplay` `toString()`
   renderer for each. This enumeration is the data the two registry entries must reproduce
   **exactly**.
2. **The `NamespaceRegistry` / `IGrobPlugin` agreement-test pattern** (D-308, D-342) — the
   compile-time-twin-vs-runtime-registration consistency test to mirror for nominal types.
3. **`ValueDisplay`'s D-336 security ordering** — the registered `toString()` lookup runs
   **before** structural `Struct` rendering to prevent credential leakage through the shared
   `Struct` discriminator. Confirm exactly where the guid/date `toString()` arms sit in that
   order; the migration must preserve that position precisely.
4. **Registry placement.** The table is consulted by the type checker (`Grob.Compiler`), VM
   dispatch (`Grob.Vm`) and `ValueDisplay`. `Grob.Compiler` and `Grob.Vm` must not reference
   each other (solution architecture: `Grob.Core` is the only shared ground). Determine the
   lowest shared assembly that lets all three consult one table without creating a
   `Compiler`↔`Vm` edge — `Grob.Core` if `GrobType`/`GrobValue`/the render delegate all live
   there, otherwise the correct shared home. Report the choice and why.
5. **The `Struct` discriminator / nominal-name mechanism** the six arms currently key off, so
   the table lookup reproduces it.
6. **The guid/date gold-master suite** — the behaviour-preservation lock. Confirm its extent
   so every case is known to pass unchanged.

Report the **entry schema**, the **placement**, the **migration plan** surface by surface,
the **agreement/consistency test** design, and the **behaviour-preservation strategy** (gold
masters unchanged). Then STOP.

---

## Design — recommended, confirm or adjust in plan

A single `NamedTypeRegistry` in the shared assembly (step 4), one entry per nominal type:

- **canonical name** — the string the annotation resolvers match;
- **property table** — name → `GrobType`;
- **method table** — name → { arity, parameter types (with the nominal-identity rule for
  `date`/`guid`-typed params), return type, **and an optional/default-parameter metadata
  field** per D-358 };
- **`toString()` renderer** — the delegate `ValueDisplay` invokes, registered so D-336's
  security ordering is preserved.

The three annotation resolvers ask the table "is this name a registered nominal type, and
what is its `GrobType`?"; the method/property validator reads the entry's tables; VM instance
dispatch resolves type name → entry → member; `ValueDisplay` invokes the entry's renderer in
its existing security-ordered slot. `guid` and `date` are authored as the first two entries,
reproducing the enumerated arms exactly.

---

## Scope boundaries — do NOT

- **Do not migrate any other nominal type.** `File`/`json.Node`/`csv.Table`/`CsvRow`/`Regex`/
  `Match`/`ProcessResult` land as registry data in their own increments — not here.
- **Do not touch arrays or maps.** They are structural (`ArrayTypeDescriptor` D-351, and the
  scheduled `MapTypeDescriptor`), dispatched on the descriptor, never on this registry.
- **Do not touch or "fix" `date` equality or comparison.** The `==`/relational operator
  handlers are D-357's territory (instant-basis, not yet implemented). Behaviour-preserving
  means preserving the current — deliberately unfixed — equality; migrate only the
  method/property/annotation/`toString` surfaces. Do not alter any equality gold master.
- **Do not build the default-argument call-site synthesis mechanism.** Only the metadata
  *field* on the method entry exists here (empty for guid/date, which have no optional
  params). The synthesis mechanism and `date.parse`'s second argument are D-358's session,
  which needs a proving case this increment does not provide.
- **Do not move static constructors.** `date.now()`/`guid.parse()` stay `NamespaceRegistry`
  entries (namespace-receiver calls, D-342's domain). The two registries compose; this one is
  the **instance** surface only.
- **No new opcode. No new error code** — annotation misses stay `E1001`; method/argument
  errors stay `E0003`/`E0004`/`E0005`/`E0011`. Count stays 118.

---

## The safety invariant

**Behaviour-preserving.** Every existing `guid` and `date` gold master must pass **unchanged**
— that is the proof the table reproduces the hand-rolled arms exactly. Any diff in guid/date
behaviour is a migration bug, not an accepted change. If plan-mode finds an arm whose
behaviour is subtle (a nominal-identity parameter rule, a `toString` edge, the D-336 ordering),
reproduce it precisely rather than approximating.

---

## Tests — TDD where adding behaviour, lock where preserving

- **All existing guid/date gold masters pass unchanged** — the behaviour-preservation lock,
  run and green before and after migration.
- **The agreement/consistency test** (mirroring D-308/D-342): every `Struct`-discriminated
  nominal type the VM dispatches has a registry entry and vice versa — no orphan hand-rolled
  arm survives for guid/date, and a future nominal type added without an entry fails this
  test. This is the mechanism that makes "File etc. as data" safe.
- Annotation-position resolution via the table: `d: date` / `g: guid` in parameter, struct
  field and return positions resolve (not `E1001`), across all three resolvers.
- Method/property validation via the table: arity and type errors still surface the existing
  codes.
- `ValueDisplay` `toString()` for guid and date via the entry's renderer, in the D-336
  security-ordered position (add a test asserting a `Struct`-discriminated value does not leak
  structural fields ahead of the registered renderer).

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and
  pre-push (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never
  "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-361**; confirm, do not assume. The
  entry records: the `NamedTypeRegistry` built and placed (assembly, with the no-`Compiler`↔
  `Vm`-edge rationale); `guid` and `date` migrated behaviour-preserving (gold masters
  unchanged); the six surfaces now table-driven; the optional/default-parameter metadata field
  present but unused (D-358's mechanism deferred to its own session); the agreement/consistency
  test added; D-336's `ValueDisplay` security ordering preserved; and that `File` through
  `ProcessResult` now land as registry data. No new opcode, no new error code, count 118. Cite
  D-356 / D-342 / D-308 / D-355 / D-336.
- **Update `grob-type-registry.md`** — describe the guid/date instance surfaces as table
  entries and note the registry mechanism, citing D-356. (Requires the file in the corpus zip.)
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
