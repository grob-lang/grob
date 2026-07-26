# map\<K, V\>

First-class built-in type. Keys must be `string` in v1. Statically typed by value
(`MapTypeDescriptor`, D-374) — a `map<string, int>` annotation now carries a real `V`
through the indexer and `for...in`.

## Construction

```grob
headers := map<string, string>{
    "Content-Type":  "application/json"
    "X-Api-Version": "2024-01-01"
}
```

**Not yet built (D-374):** this literal construction syntax has no parser support —
maps can be consumed via a typed parameter, field or `var` annotation, but not yet
constructed from a `map<K, V>{...}` literal in source.

## Members

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `length` | property | `→ int` | |
| `isEmpty` | property | `→ bool` | |
| `keys` | property | `→ K[]` | Insertion order |
| `values` | property | `→ V[]` | Order matches keys |
| `get(key: K)` | method | `→ V?` | Nil if absent |
| `set(key: K, value: V)` | method | `→ void` | Insert or overwrite |
| `contains(key: K)` | method | `→ bool` | |
| `remove(key: K)` | method | `→ void` | No-op if absent |
| `clear()` | method | `→ void` | |
| `[key: K]` | indexer | `→ V?` | Sugar for `get(key)` — built (D-374) |
| `[key: K] = value` | indexer | `→ void` | Sugar for `set(key, value)` |

**Not yet built (D-374):** every member in the table above except the `[key: K]`
indexer — `map` has no member-access dispatch in the type checker at all, so
`length`/`isEmpty`/`keys`/`values`/`get()`/`contains()`/`set()`/`remove()`/`clear()`
all fail to compile today. Scheduled as a follow-on increment.

Mutation methods are a compile error on `const`-bound maps.

## Indexing and iteration — built (D-374)

```grob
value := headers["Content-Type"]  // string? — nil if the key is absent

for k, v in headers {
    print("${k}: ${v}")           // k: string, v: the map's real V
}
```

`headers["k"] += "x"` and `headers["k"]++`-style compound-assignment/increment on an
existing entry stay legal — the nullable `V?` is unwrapped to `V` before the operand
check runs, matching pre-D-374 behaviour rather than newly rejecting it.

*Updated July 2026 — `MapTypeDescriptor` substrate built (Sprint 9 Increment C0b-1,
rescoped, D-374): the indexer types `V?` and `for k, v in m` binds `v` as the map's real
`V`, both previously `Unknown`. The six query members and map-literal construction
syntax remain unbuilt — split off after the increment's own plan-mode gate found the
former needs a new descriptor-carriage mechanism and the latter has no parser support at
all, despite being documented above as working syntax.*
