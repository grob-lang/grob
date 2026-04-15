# T[] (All Arrays)

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `length` | property | `→ int` | |
| `isEmpty` | property | `→ bool` | |
| `first()` | method | `→ T?` | Nil if empty |
| `last()` | method | `→ T?` | Nil if empty |
| `contains(v: T)` | method | `→ bool` | |
| `filter(fn: T → bool)` | method | `→ T[]` | New array |
| `map(fn: T → U)` | method | `→ U[]` | New array |
| `each(fn: T → void)` | method | `→ void` | |
| `sort(fn: T → U, descending: bool = false)` | method | `→ T[]` | New sorted array |
| `select(fn: T → U)` | method | `→ U[]` | Alias for map |
| `append(value: T)` | method | `→ void` | Mutates in place |
| `insert(index: int, value: T)` | method | `→ void` | Mutates in place |
| `remove(index: int)` | method | `→ void` | Mutates in place |
| `clear()` | method | `→ void` | Mutates in place |

`filter`, `map`, `sort`, `select` always return a new array. `append`, `insert`,
`remove`, `clear` mutate in place — compile error on `const`-bound arrays.
