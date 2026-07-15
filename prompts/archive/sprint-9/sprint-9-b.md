---
description: "Sprint 9 · Increment B — `date`. The first type-carrying Sprint 9 module: the `date` plugin type (Struct-discriminated, D-303, no new GrobValueKind), a registered `toString()` so ValueDisplay (D-336) renders it canonically not structurally, the full now/today/of/ofTime/parse surface on IClock (declared D-343, first heavy consumer here), the property and method API, date.parse throwing ParseError through the native-throw seam. Proves the value-semantics-plugin-type pattern C–G reuse. Foundation fs (Increment C) consumes."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment B — `date`

Increment A fixed the array-indexer emission and cleared the Sprint 8 reconciliation.
This increment lands the first **type-carrying** Sprint 9 module — `date` — and with it
the pattern every later Sprint 9 type reuses: a plugin type registered with the type
checker, `Struct`-discriminated at runtime (D-303, **no new `GrobValueKind` variant**),
with a registered `toString()` so `ValueDisplay` (D-336) renders it as its canonical
string rather than its internal fields. `date` is the lightest of the type-carrying
modules and the foundation `fs` (Increment C) depends on — `File.modified` and
`File.created` return `date` values — so it lands first.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 9** `date` scope (the full
   signature list), §3.1.1 (the day-one LSP properties), and the solution-architecture §
   (the `Grob.Stdlib` project, the DAG, the `Grob.Runtime` capability surface).
2. `docs/design/grob-stdlib-reference.md` — the **`date`** section (constants, the
   static constructors, the instance properties and methods, the zone/UTC surface).
3. `docs/design/grob-vm-architecture.md` — the `GrobValue` representation (D-303 — nine
   variants, `Struct` shared discriminator), how a plugin type is `Struct`-boxed and
   discriminated at the type-registry level, and the `ValueDisplay` protocol (D-336 —
   the registered-`toString()` precedence, step 2 ahead of structural rendering).
4. Decisions: **D-342** (module-namespace resolution — `date` is a compile-time
   namespace, members fold to qualified natives), **D-303** (the settled `GrobValue`
   tagged union and the `Struct` discriminator), **D-336** (the `ValueDisplay` protocol
   and the registered-`toString()` precedence), **D-343** (the capability seam and
   `IClock`, declared there with no Sprint 8 consumer — `date` is its first heavy
   consumer), **D-284** (the `ParseError` leaf `date.parse` throws), **D-308**
   (`ErrorCatalog`). Grep the log for the next-free D-number for the `date` landing
   record (D-344 is the precedent — a module-landing entry).

> **Verify before relying on cited decisions and sections.** Grep
> `grob-stdlib-reference.md` for the `date` section and `grob-vm-architecture.md` for the
> `GrobValue`/`ValueDisplay` sections. If a signature has moved or a decision been
> superseded, surface it rather than proceeding.
>
> **Grammar-first gate (D-331).** `date.now()`, `date.parse(s)` and `d.addDays(1)` are
> member access on a namespace and on a value — grammatical since D-320/D-342. Confirm
> they parse to member-access nodes against the merged tree before building on them.
>
> **`date` — inline reference.**
>
> - **`date` is a `Struct`-discriminated plugin type, not a new `GrobValueKind`
>   variant.** D-303 fixed the nine variants; `date` (like `guid`, `File`, `json.Node`
>   and the rest) shares the `Struct` discriminator, reference-stored, carrying a boxed
>   .NET value (a `DateTimeOffset`, given `utcOffset`/`toUtc`/`toLocal`/`toZone`). Its
>   distinctness is a **type-checker and type-registry** concern (a registered
>   `GrobType`), not a discriminator concern. Do not add a `GrobValueKind` case; if you
>   reach to, stop and surface.
> - **`date` registers a `toString()` so `ValueDisplay` renders it, not its fields.**
>   D-336's dispatch places a registered `toString()` (step 2) ahead of structural
>   rendering (step 5) precisely so a `Struct`-discriminated value is not shown as its
>   internal representation. `date` registers `toString()` (canonical ISO-8601), so
>   `print(d)` and `"${d}"` emit the canonical string. This is the same precedence that
>   keeps `AuthHeader` opaque and renders `guid` — verify it holds for `date` on the real
>   print and interpolation paths.
> - **`date` members resolve through the D-342 namespace model.** The static
>   constructors (`now`, `today`, `of`, `ofTime`, `parse`, `fromUnixSeconds`,
>   `fromUnixMillis`) are namespace-qualified natives; the instance properties and
>   methods are type-registry members on a `date` value (D-070/D-071 pattern). `date.now`
>   and `date.today` read the injected `IClock` — never `DateTime.Now` directly, so the
>   playground and tests can pin the clock.
> - **`date.parse` throws `ParseError` through the native-throw seam.** A malformed input
>   raises a catchable `ParseError` (D-284) that unwinds the Sprint-7 handler table
>   (D-334/D-342) — one mechanism, no bespoke native-error path. Unlike `guid.parse`,
>   `date.parse` has **no** compile-time literal validation in v1 (the requirements list
>   no such rule) — it is a runtime `ParseError` path only.
> - **`IClock` is consumed, never bypassed.** `date.now`/`date.today` and any
>   time-dependent method reach the clock through the injected `IClock` (D-343). Grep for
>   a direct `DateTime.Now`/`DateTimeOffset.Now` in `Grob.Stdlib` — a direct call breaks
>   the playground seam and the test pinning.

> **Sequencing note.** This is Increment B: A (indexer fix) → **B (`date`)** → C
> (`fs`/`IFileSystem`) → … `date` is the foundation `fs` consumes. Do not pull `fs`
> forward.

## What you're building

1. **The `date` plugin type.** Registered `GrobType`, `Struct`-discriminated `GrobValue`
   storage (boxed `DateTimeOffset`), the full property surface (`year`, `month`, `day`,
   `hour`, `minute`, `second`, `dayOfWeek`, `dayOfYear`, `utcOffset`) and method surface
   (`addDays`, `minusDays`, `addMonths`, `addHours`, `addMinutes`, `isBefore`, `isAfter`,
   `toIso`, `toIsoDateTime`, `format`, `toUnixSeconds`, `toUnixMillis`, `toUtc`,
   `toLocal`, `toZone`, `daysUntil`, `daysSince`). `ResolvedType`/`Declaration` on every
   member node (§3.1.1).
2. **The static constructors as namespace natives.** `now`, `today`, `of`, `ofTime`,
   `parse`, `fromUnixSeconds`, `fromUnixMillis` — `now`/`today` on `IClock`.
3. **The registered `toString()`.** Canonical ISO-8601, so `ValueDisplay` (D-336)
   renders `date` values on the print and interpolation paths, not their fields.
4. **`date.parse` → `ParseError`** through the native-throw seam, runtime-only (no
   compile-time literal validation).
5. **The `date` landing decision.** Recorded at its real next-free number in
   three-location lockstep, on the D-344 module-landing precedent.

No new opcode, no new `GrobValueKind` variant. Confirm every error code against the live
registry — `date.parse` reuses the existing `ParseError` code; if any new compile-time
code is needed it is a fold-versus-new call via `allocating-an-error-code` (D-331).

## Out of scope

`fs` and the `File` type (Increment C). Every other module. Compile-time date-literal
validation (not in v1). Do not edit the `OpCode` enum or add a `GrobValueKind` variant.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):** `date` resolves as a namespace;
  `date.now()` resolves to `(): date`; a `date` value's members resolve; `date == string`
  and other cross-type misuse are the existing mismatch code. §3.1.1 holds on every
  member node.
- **Stdlib tests (`Grob.Stdlib.Tests`):** the property and method surface returns correct
  values against a pinned `IClock`; `toUnixSeconds`/`fromUnixSeconds` round-trip;
  `toUtc`/`toLocal`/`toZone` behave; `date.parse` on a valid string succeeds and on a bad
  string throws `ParseError`.
- **VM tests (`Grob.Vm.Tests`):** `print(date.of(...))` renders the canonical ISO string
  through `ValueDisplay` (D-336), not structural fields; `date.parse("nonsense")`
  unhandled produces the quality diagnostic and exit 1, and caught by
  `catch (e: ParseError)` resumes.
- **Integration / spec-consistency:** D-316 green; the `date` landing decision present in
  lockstep; no direct `DateTime.Now` in `Grob.Stdlib` (grep-asserted).

## Acceptance

- Each `date` member works against a pinned `IClock`; `date.parse` succeeds on valid
  input and throws catchable `ParseError` on bad input through the handler table.
- `print(d)` and `"${d}"` render the canonical ISO-8601 string via `ValueDisplay`, never
  structural fields.
- No `DateTime.Now` reached directly in `Grob.Stdlib`; the clock is the injected
  `IClock`.
- No new opcode, no new `GrobValueKind` variant; the `date` landing logged in lockstep;
  D-316 green.
- §3.1.1 holds; the DAG holds; coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High). A type registration plus native surface over the settled D-342/D-303/
D-336 machinery — no Opus carve-out.

## Hand-off

Summarise: how the `date` type is registered and `Struct`-stored; the registered
`toString()` and the `ValueDisplay` rendering; the `IClock` consumption; the `ParseError`
path; the landing decision and its lockstep entry. Note for the next chat: Increment C is
`fs` + `IFileSystem` — the `File` type whose `modified`/`created` return the `date`
values landed here, the fifth capability interface, and the playground VFS seam.
