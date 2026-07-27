# map\<K, V\>

First-class built-in type. Keys must be `string` in v1. Statically typed by value
(`MapTypeDescriptor`, D-374) — a `map<string, int>` annotation now carries a real `V`
through the indexer and `for...in`.

## Construction

```grob
headers := map<string, string>{
    "Content-Type": "application/json",
    "X-Api-Version": "2024-01-01",
}
```

**Built (D-376):** map-literal construction. Entries are separated by commas —
newlines inside the braces are insignificant (skipped), matching every other literal
form in the language. A trailing comma is permitted. Each key must be a plain
double-quoted string literal (not raw, not interpolated); duplicate keys are a compile
error. `V` is checked against each entry's value; `K` (fixed `string` in v1) is not
separately validated.

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

**Built (D-377):** `length`/`isEmpty`/`keys`/`values`/`get()`/`contains()` — the six
non-mutating query members. `keys`/`values` carry a populated element descriptor, so a
chained call (`m.keys.first()`, `m.values.contains(x)`) resolves correctly. `get(key)`
agrees with the indexer `m[k]` on type and value. `contains(key)` is key-membership,
distinct from the array's value-membership `contains(v)`.

**Not yet built:** `set()`/`remove()`/`clear()` — deferred to the separate C0b-2b
increment, along with the `const`-/`readonly`-bound mutation rejection for those three.

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
`V`, both previously `Unknown`. The six query members remain unbuilt — split off after
the increment's own plan-mode gate found the query-member surface needs a new
descriptor-carriage mechanism.*

*Updated July 2026 — map-literal construction lands (D-376): the grammar (with a
non-consuming lookahead disambiguating `map<K, V>{` from the relational comparison
`map < x`), the `MapLiteralExpr`/`MapEntry` AST, `MapDescriptorOf`'s third (literal)
tier, and the `NewMap` opcode. Duplicate keys are E0016. The six query members and
mutation (`set`/`remove`/`clear`) remain unbuilt, per the note above.*

*Updated July 2026 — map query member surface lands (Sprint 9 Increment C0b-2a, D-377):
`length`/`isEmpty`/`keys`/`values`/`get()`/`contains()`, mirroring the array query-member
dispatch (D-371) exactly. `set`/`remove`/`clear` and their `readonly` mutation rejection
remain unbuilt, deferred to C0b-2b.*
