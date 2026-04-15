# GC Strategy

## Current Decision

Lean on C#'s garbage collector (tentative — OQ-006 defers final confirmation
until clox is complete).

Since the VM is written in C#, C#'s GC handles heap memory automatically. The
design work is in value representation — minimising heap allocations so GC
pressure is low.

`struct` for value types (int, float, bool) — stack allocated, zero GC pressure.
`class` only for heap objects (string, array, function).

For scripting use cases (file operations, automation) GC pauses are invisible.
Custom mark-and-sweep is not justified.

See also: [Value Representation](Value-Representation.md)
