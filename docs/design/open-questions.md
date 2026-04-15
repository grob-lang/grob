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

### OQ-009 — `GrobValue` Provisional Representation

**Question:** `GrobValue` in `Grob.Core` must be defined before Sprint 1 begins.
The full value representation decision (OQ-005) is deferred until clox is complete.
How should `GrobValue` be defined in the interim to avoid a costly retrofit?

**Context:** Sprint 1 writes `Chunk` and `ConstantPool`. Both depend on `GrobValue`.
OQ-005 will resolve to either tagged union struct or NaN boxing — but that decision
requires clox experience to make well. Starting Sprint 1 without a `GrobValue`
definition is not possible.

**Tentative direction:** Define `GrobValue` as a tagged union struct from day one —
this is the most likely final answer anyway, and it is the idiomatic C# approach.
Encapsulate it behind a clean boundary so that if OQ-005 resolves differently, the
change is localised to `Grob.Core`. Document in code that this is provisional pending
OQ-005 resolution.

**Defer until:** Start of Sprint 1 — must be decided before first line of `Grob.Core` is written.

-----

### OQ-010 — `.grobc` Binary Format Specification

**Question:** What is the binary format for compiled Grob bytecode (`.grobc` files)?

**Context:** `.grobc` is confirmed for optional bytecode caching. The format needs at
least a skeleton specification before implementation so that the binary is versionable
from day one. Retrofitting versioning after the fact is expensive.

**Minimum required before implementation:**

- Magic bytes and version header
- Endianness (little-endian, matching x86/x64 Windows target)
- How the constant pool serialises
- How source location maps are included or omitted (stripped builds vs debug builds)
- Format version field — so that the VM can detect and reject stale `.grobc` files
  compiled against an older instruction set

**Defer until:** Sprint 1 is complete and the `Chunk` and `ConstantPool` structures
are stable. The format follows from the in-memory structures.

-----

## Resolved Questions

These questions have been decided. Full rationale is preserved here for reference.
One-line resolutions are recorded in the confirmed decisions table of
`Grob___Decisions___Context_Log.md`.

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
in `Grob___Stdlib___Reference.md`.

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
*table of `Grob___Decisions___Context_Log.md`. The full rationale is preserved here.*

-----

*Document updated April 2026 — OQ-011 resolved (`Grob.Crypto` API shape);*
*OQ-012 resolved (`process.run()` timeout behaviour).*
*Previous: OQ-007 resolved (`for...in` iterable types);*
*OQ-009 (`GrobValue` provisional representation),*
*OQ-010 (`.grobc` binary format) remain open.*
*Document created April 2026 — extracted from Grob___Decisions___Context_Log.md.*
*Authorised decisions recorded in Grob___Decisions___Context_Log.md.*
*This document is the implementation reference — the decisions log is the authority.*