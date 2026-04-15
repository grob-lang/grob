# Value Representation

## Current Decision

Tagged union struct (tentative — OQ-005 defers final decision until clox
experience is complete).

```csharp
struct GrobValue
{
    public GrobType Kind;
    public long Raw;      // int/float/bool stored directly
    public object? Ref;   // string/array/function — only when needed
}
```

## Design Principles

- `struct` for value types — stack allocated, zero GC pressure
- `class` only for heap objects (string, array, function)
- Primitives are never boxed
- Method-call syntax on all types is compiler sugar — no runtime dispatch

NaN boxing (packing type info into unused bits of a 64-bit float) is understood
from clox but the C# idiom argues against it.

See also: [GC Strategy](GC-Strategy.md)
