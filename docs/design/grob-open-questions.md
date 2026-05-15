# Grob — Open Questions

> Design questions requiring decisions before implementation reaches them,
> and resolved questions with their full rationale preserved.
> Decisions authorised in the decisions log — April 2026 design sessions.
> This document preserves the reasoning behind each question and resolution.
> When this document and the decisions log conflict, the decisions log wins.

-----

## Open Questions

These are unresolved. They require a decision before implementation reaches them.
Listed in rough priority order — earlier questions affect more downstream design.

-----

### OQ-005 — Value Representation

**Question:** Tagged union struct or NaN boxing?

**Tentative direction:** Tagged union. NaN boxing is worth understanding (clox
covers it) but the C# idiom argues against it.
**Defer until:** clox complete.

-----

### OQ-006 — GC Strategy (Confirm or Revise)

**Question:** Lean entirely on C#’s GC, or implement a custom mark-and-sweep?

**Tentative direction:** Lean on C#. Revisit only if profiling shows a problem.
**Defer until:** clox complete.

-----

## Resolved Questions

These questions have been decided. Full rationale is preserved here for reference.
One-line resolutions are recorded in the confirmed decisions table of
`grob-decisions-log.md`.

-----

### OQ-009 — `GrobValue` Provisional Representation

**Status: RESOLVED — April 2026 (D-297)**

**Decision:** `GrobValue` is a hand-rolled `readonly struct GrobValue : IEquatable<GrobValue>` under .NET 10 LTS. Three private fields:
a `GrobValueKind` discriminator, a `long _scalar` slot holding `int`/`bool`/`float`
(floats stored via `BitConverter.DoubleToInt64Bits` to avoid allocation), and an
`object? _reference` slot for reference types. Total 24 bytes on x64 with alignment.

**Discriminator set — nine variants:** `Nil`, `Bool`, `Int`, `Float`, `String`,
`Array`, `Map`, `Struct`, `Function`. Plugin types (`date`, `guid`, `File`,
`ProcessResult`, `json.Node`, `Regex`, `Match`, `csv.Table`, `CsvRow`,
`Response`, `AuthHeader`, `ZipEntry`) and user-declared `type`s all use the
`Struct` kind; runtime type discrimination happens at the type-registry level
via the boxed reference. This keeps `GrobValueKind` small and stable as plugins
register new types.

**Encapsulation contract:** private fields, public factory statics
(`FromBool`, `FromInt`, …, plus `Nil` singleton); inspection via `Kind` and
`IsX` predicates; strict accessors (`AsX()`) that throw `GrobInternalException`
on kind mismatch; try-accessors (`TryAsX(out)`) for plugin and runtime
defensive code; full `Equals`/`GetHashCode`/`==`/`!=`. No callers outside
`Grob.Core` access the fields directly.

**Provisional-pending-OQ-005:** the internal layout is the only thing OQ-005
may change; the public API surface is stable. Documented in code (XML doc on
the struct), in `grob-vm-architecture.md`, and as the supersession chain
D-142 → D-297. The .NET 11 `[Union]` attribute migration path (post-GA, when
.NET 11 is battle-tested) is signposted in `grob-vm-architecture.md` as a
future one-commit upgrade — adding `[Union]` and `IUnion` to the existing
struct gains compile-time exhaustiveness checking on `switch` without
disturbing layout, factories, or accessors.

**Rationale:** `GrobValue` must be defined before the first line of `Grob.Core`
is written. OQ-005’s full value representation decision (tagged union vs NaN
boxing) is deferred until clox is complete because that decision requires
real bytecode-VM experience to make well. The provisional shape isolates the
OQ-005 decision behind a clean boundary so the eventual retrofit, whatever
shape it takes, is localised to `Grob.Core` and does not leak into
`Grob.Compiler` or `Grob.Vm`. Hand-rolled rather than .NET 11 `union` because
the compiler-generated `union` form boxes value-type cases on every
assignment — wrong cost profile for a VM hot path — and the `[Union]`
escape hatch produces the same hand-rolled struct anyway, only with an
attribute attached. .NET 10 LTS rather than .NET 11 STS because LTS gives
v1 room to ship and stabilise without a forced migration.

Full byte-level layout, encapsulation contract and rationale in
`grob-vm-architecture.md`.

-----

### OQ-010 — `.grobc` Binary Format Specification

**Status: RESOLVED — April 2026 (D-298)**

**Decision:** `.grobc` files use a skeleton binary format with a fixed-shape
header (magic bytes `0x47 0x52 0x4F 0x42` — ASCII “GROB” — followed by a
`uint16` format version field starting at 1, little-endian throughout),
followed by sectioned content for the constant pool, instruction stream,
source map, and symbol table. Cache files live in a `.grob/cache/` side
directory next to the source `.grob` file, mtime-driven invalidation,
`.gitignore`-friendly. The `.grob` source file is canonical; `.grobc` is
optional cache.

The `.grob/cache/` side directory matches the convention used by Python’s
`__pycache__` and similar tools — generated artefacts stay separate from
source, are trivial to `.gitignore` and never clutter the working directory.

Per-opcode operand encoding remains incremental, governed by ADR-0008 —
opcodes land sprint-by-sprint and the operand layout is documented at the
opcode’s source of definition. The skeleton spec covers the framing; the
per-opcode detail follows.

**Rationale:** The format needs to be versionable from day one because
retrofitting versioning is expensive. ADR-0008 already locked the stability
rule (immutable opcode numbers once shipped, format version increment on
breaking change). What was left open — magic bytes, header layout, constant-
pool wire format, source-map shape — is now fixed at the level needed for
Sprint 1 implementation. Cryptographic signing, compression, encryption,
and multi-chunk packaging are explicit non-features for v1.

Full byte-level layout, implementation notes and rationale in
`grob-grobc-format.md`. Supersession chain: D-143 → D-298.

-----

### OQ-007 — `for...in` Loop and Iterable Protocol

**Status: RESOLVED — April 2026**

**Decision:** `for...in` is special-cased in v1. The compiler handles exactly
three cases:

1. **Numeric range** (`for i in 0..10 { }`) — already confirmed. Lowered to `while`.
2. **`T[]` array** (`for item in arr { }`, `for i, item in arr { }`) — index-based
   lowering to `while`. Both single and two-identifier forms supported.
3. **`map<K, V>`** (`for k, v in myMap { }`) — iterates insertion-order keys.
   Two-identifier form **required**. Single-identifier form on a map is a compile
   error with a suggestion to use `for k in myMap.keys` instead. Lowered to a
   `while` loop over an internal keys array.

Any other type in `for...in` subject position is a compile error.

**Formal iterable protocol:** Post-MVP. The compiler architecture accommodates it
without rework — the three special cases become the first implementors when the
protocol is defined.

**Rationale:** Every v1 use case in the sample scripts is array or range iteration.
A formal protocol adds `Grob.Runtime` surface, type checker conformance checking,
and plugin author complexity — none of which is justified in v1. `map<K, V>` is
special-cased because `for k, v in myMap` is immediately natural and the
alternative (`for k in myMap.keys { v := myMap[k] }`) is visibly clunky for a
type that is now first-class.

-----

### OQ-001 — Generics Scope

**Status: RESOLVED — April 2026**

**Decision:** Constrained generics. The type checker and compiler understand generic
type parameters internally. Users consume generic functions via stdlib and plugins
(`mapAs<T>()`, `filter`, `map` etc) but cannot declare generic functions or types
in v1. Evolution to user-facing generics is an additive grammar extension — no
architectural rework required.

**Rationale:** Gets type-safe collections and JSON deserialisation without committing
to full user-facing generic syntax on day one. Implementation scope is meaningfully
smaller than full generics. Closes no doors — the type checker already understands
generics, the grammar simply doesn’t expose the declaration syntax yet. Analogous
to Go pre-1.18.

**Plugin constraint:** Plugins that expose generic functions must express type
parameters via `FunctionSignature` in `Grob.Runtime`. Designed in from the start.

-----

### OQ-002 — Struct / Record Types

**Status: RESOLVED — April 2026 (SharpBASIC retrospective)**

**Decision:** Grob needs user-defined struct/record types.

**Evidence:** The Sunken Crown required parallel arrays as a substitute for records.
The retrospective verdict: *“Messy, wasteful, and slow.”* The absence of a `type`
keyword was the single biggest language limitation revealed by writing a real program.

**Confirmed direction:** `type` keyword, structural types, fields declared inside
the block. Immutable by default, opt-in mutability. JSON deserialisation (`mapAs<T>()`)
maps JSON keys to fields by name.

```grob
type Repo {
    org:     string
    project: string
    name:    string
}
```

-----

### OQ-003 — JSON and the Type System Boundary

**Status: RESOLVED — April 2026**

**Decision:** `mapAs<T>()` is the confirmed boundary mechanism — a generic method
understood by the type checker, consuming the constrained generics infrastructure.
JSON nodes are accessed via `json.Node` with typed accessors (`asString()`, `asArray()`
etc.) and mapped to user-defined types via `mapAs<T>()`. Full json module API specified
in `grob-stdlib-reference.md`.

-----

### OQ-004 — Error Handling Model

**Status: RESOLVED — April 2026**

**Decision:** Exceptions as the runtime error model. See confirmed decisions for detail.

-----

### OQ-008 — `date` as a Built-in or Stdlib Type

**Status: RESOLVED — April 2026**

**Decision:** `date` is a core stdlib type — auto-available, no import required.
Single type holds both date and time. Full API locked — see confirmed decisions.

-----

### OQ-011 — `Grob.Crypto` API Shape

**Status: RESOLVED — April 2026**

**Decision:** First-party plugin (`import Grob.Crypto`). Full API:

- `crypto.sha256File(path: string): string` — lowercase hex, streams internally
- `crypto.md5File(path: string): string` — lowercase hex, streams internally
- `crypto.sha256(value: string): string` — lowercase hex, UTF-8 encoded
- `crypto.md5(value: string): string` — lowercase hex, UTF-8 encoded
- `crypto.verifySha256(path: string, expected: string): bool` — constant-time comparison
- `crypto.verifyMd5(path: string, expected: string): bool` — constant-time comparison

All hex output is lowercase. File functions stream internally — never load full file
into memory. Verify functions use constant-time comparison for security. SHA-1,
SHA-512, HMAC, byte array output — all post-MVP.

-----

### OQ-012 — `process.run()` Timeout Behaviour

**Status: RESOLVED — April 2026**

**Decision:** All four process functions get `timeout: int = 0` as a named parameter.
`0` means infinite — runs until the process completes or the OS kills it. On timeout
expiry, throws `ProcessError("Process timed out after {n} seconds: {cmd}")`.
`ProcessResult` is unchanged — no `timedOut` property. The throw is the signal.

Full signatures:

```
process.run(cmd: string, args: string[], timeout: int = 0): ProcessResult
process.runShell(cmd: string, timeout: int = 0): ProcessResult
process.runOrFail(cmd: string, args: string[], timeout: int = 0): ProcessResult
process.runShellOrFail(cmd: string, timeout: int = 0): ProcessResult
```

**Rationale:** Option 3 from the original question. No silent default kill avoids
surprising behaviour for long-running legitimate processes. `timeout: int` is
available when the caller needs it. `ProcessError` on timeout with a clear message.
`ProcessResult` does not need `timedOut` — the throw communicates the condition.

-----

*Resolved questions are summarised as one-line entries in the confirmed decisions*
*table of `grob-decisions-log.md`. The full rationale is preserved here.*

-----

*Document updated April 2026 — post-Session-G cleanup: OQ-009 and OQ-010*
*body sections relocated from “Open Questions” to “Resolved Questions”*
*to match their resolved status. No content change to the resolutions*
*themselves — full rationale preserved.*
*Previous: April 2026 — OQ-009 resolved (`GrobValue` provisional representation,*
*hand-rolled tagged-union struct under .NET 10 LTS, see D-297);*
*OQ-010 resolved (`.grobc` binary format skeleton spec, see D-298 and `grob-grobc-format.md`).*
*Previous: OQ-011 resolved (`Grob.Crypto` API shape);*
*OQ-012 resolved (`process.run()` timeout behaviour).*
*Previous: OQ-007 resolved (`for...in` iterable types).*
*OQ-005 (full value representation — tagged union vs NaN boxing) and*
*OQ-006 (GC strategy) remain open, both deferred until clox is complete.*
*Document created April 2026 — extracted from grob-decisions-log.md.*
*Authorised decisions recorded in grob-decisions-log.md.*
*This document is the implementation reference — the decisions log is the authority.*
