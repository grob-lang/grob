# T[] (Array) — Type Registry

All members known to the type checker at compile time. Calling an undefined
member is a compile error. Arrays are typed — all elements must be the same type.

## Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `length` | `→ int` | |
| `isEmpty` | `→ bool` | |

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `first()` | `→ T?` | nil if empty |
| `last()` | `→ T?` | nil if empty |
| `contains(v: T)` | `→ bool` | |
| `filter(fn: T → bool)` | `→ T[]` | |
| `select(fn: T → U)` | `→ U[]` | Projection — maps elements to a new shape |
| `each(fn: T → void)` | `→ void` | |
| `sort(fn: T → U, descending: bool = false)` | `→ T[]` | Returns new sorted array. **Stable.** `U` must be `int`, `float`, `string`, `bool`, `date` or `guid` (D-371) |
| `append(value: T)` | `→ void` | Appends one element. Mutates in place. Binding must not be `const` or `readonly` (D-373) |
| `insert(index: int, value: T)` | `→ void` | Inserts before index. `index == length` is a valid append-position insert; out of range otherwise throws `IndexError` (D-373) |
| `remove(index: int)` | `→ void` | Removes element at index. Throws `IndexError` if out of range — including `index == length`, unlike `insert` (D-373) |
| `clear()` | `→ void` | Removes all elements. Mutates in place. Binding must not be `const` or `readonly` (D-373) |
| `mapAs<T>()` | `→ T[]` | Typed deserialisation from JSON or CSV result sets |

`.select()` is the projection method for arrays. It reads naturally in data
pipelines (`Select-Object` equivalent in PowerShell). The name `.map()` is
not used in Grob — use `.select()` for projection.

`.mapAs<T>()` is distinct: it is a typed-deserialisation operation on JSON and
CSV result sets, not a general projection.

## Aliasing semantics

Array assignment and argument-passing are **reference semantics**, not value/copy
semantics (D-372): `b := a` and passing `a` to a function both bind the same
underlying array instance, not a clone. Mutation through any binding is visible
through every other binding aliasing the same array:

```grob
a := [1, 2]
b := a
b.append(3)
print(a.length)  // 3 — a and b are the same array
```

This makes `readonly`'s mutation rejection **binding-scoped, not object-scoped**:
`readonly a := [1, 2]; b := a; b.append(3)` is not caught — the type checker can only
reject mutation reached through the `readonly` name itself, the same guarantee C#'s
`readonly` and JavaScript's `const` give.

## Formatting

| Member | Description |
|--------|-------------|
| `T[].formatAs.table()` | Auto-column table output |
| `T[].formatAs.table(columns: string[])` | Explicit column selection |
| `T[].formatAs.list()` | One item per line |
| `T[].formatAs.csv()` | CSV to stdout |

See [formatAs module](../Standard-Library/formatAs.md).

## Indexing

```grob
items[0]            // first element
items[items.length - 1]  // last element
matrix[r][c]        // multi-dimensional via chained indexing
```

Zero-based. Out-of-range access throws `IndexError` (E5101).

## Examples

### Filter and sort

```grob
large_files := files
    .filter(f => f.size > 1024 * 1024)
    .sort(f => f.size, descending: true)
```

### Project fields

```grob
names := users.select(u => u.name)

// Project to anonymous struct
summaries := users.select(u => #{ name: u.name, active: u.isActive })
```

### Typed deserialisation

```grob
type User { name: string, email: string }
users := json.read("C:\\data\\users.json").mapAs<User>()
```

*Updated July 2026 — `append(value: T)`, `insert(index: int, value: T)`,
`remove(index: int)` and `clear()` built (Sprint 9 Increment C0a-2, D-373), completing
the thirteen-member `T[]` surface. Aliasing semantics section added, citing D-372
(reference semantics — mutation through any binding is visible through every other
binding aliasing the same array). Corrected a stray `IndexError (E5002)` reference under
Indexing to the actual code, `E5101` (matching the VM's array indexer and
`StringMethodsPlugin`'s native-throw seam, both of which use `ErrorCatalog.E5101.Code`).*

*Updated July 2026 — `length`, `isEmpty`, `first()`, `last()` and `contains(v: T)`
built (Sprint 9 Increment C0a-1, D-371); `sort`'s documented `Comparable` key set
(`int`/`float`/`string`/`bool`/`date`/`guid`) now fully built, `date` ordering on the
instant basis (D-367) and `guid` ordinally (D-357). The four mutating members
(`append`/`insert`/`remove`/`clear`) remain pending — Increment C0a-2.*

*Updated April 2026 — `.map()` removed; `.select()` is the projection method
(D-280). `.mapAs<T>()` documented as distinct typed-deserialisation operation.
`format` references updated to `formatAs` (D-282). `IndexError` error type updated.*
