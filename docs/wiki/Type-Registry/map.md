# map\<K, V\>

First-class built-in type. Keys must be `string` in v1.

## Construction

```grob
headers := map<string, string>{
    "Content-Type":  "application/json"
    "X-Api-Version": "2024-01-01"
}
```

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
| `[key: K]` | indexer | `→ V?` | Sugar for `get(key)` |
| `[key: K] = value` | indexer | `→ void` | Sugar for `set(key, value)` |

Mutation methods are a compile error on `const`-bound maps.
