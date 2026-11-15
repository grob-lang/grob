# Types

Grob is statically typed with type inference. Types are resolved at compile time
and never checked at runtime.

## Built-in Types

| Type | Description | Literal examples |
|------|-------------|-----------------|
| `int` | 64-bit integer | `42`, `0xFF`, `0b1010`, `1_000_000` |
| `float` | 64-bit floating point | `3.14`, `0.5` |
| `string` | UTF-8 text | `"hello"`, `"${name}"`, `` `raw` `` |
| `bool` | Boolean | `true`, `false` |
| `nil` | Absence of value | `nil` |

## Arrays

```grob
numbers := [1, 2, 3]           // int[]
names   := ["Alice", "Bob"]    // string[]
items: int[] := []              // empty — annotation required
```

Arrays are typed — all elements must be the same type. Empty array literals
require a type annotation because the element type cannot be inferred.

## Type Inference

The compiler infers types from the right-hand side of declarations:

```grob
x := 42              // int
name := "Alice"      // string
active := true       // bool
ratio := 3.14        // float
```

Explicit annotations are always valid but only required where inference cannot
resolve the type: `nil` initialisations and empty array literals.

```grob
x: int := 42             // valid, not required
name: string? := nil     // required — nil has no type
items: int[] := []        // required — [] has no element type
```

## Nullable Types

The `?` suffix marks a type as nullable. Non-nullable types are guaranteed
non-nil at compile time.

```grob
name: string? := nil     // may be nil
count := 42              // never nil — int is non-nullable

print(name)              // prints nil — print() accepts a nullable value

if (name != nil) {
    print(name)          // safe — compiler narrows type to string
}
```

### Nil coalescing

```grob fragment
display := name ?? "Anonymous"
```

### Optional chaining

```grob fragment
length := name?.length ?? 0
```

### Flow-sensitive narrowing

Inside an `if (x != nil)` block the type checker narrows the variable from
`T?` to `T`. The narrowing is removed when the block exits.

## User-Defined Types

The `type` keyword declares named structural types.

```grob
type Repo {
    name:    string
    ssh_url: string
}

type Config {
    host:    string
    port:    int = 8080       // field default
    timeout: int = 30
}
```

Fields without defaults are required at construction. Fields with defaults are
optional — the default is used if omitted.

### Construction

```grob fragment
r := Repo {
    name:    "grob",
    ssh_url: "git@github.com/grob-lang/grob",
}

c := Config {
    host: "localhost",    // port and timeout use defaults
}
```

Fields are named and unordered. Unknown field names are a compile error.
Omitting a required field (one with no default) is a compile error.

### Nested construction

```grob
type Address {
    city:    string
    country: string
}

type Person {
    name:    string
    address: Address
}

p := Person {
    name:    "Chris",
    address: Address { city: "London", country: "UK" },
}
```

### Anonymous structs

```grob
item := #{ name: "report.xlsx", size: 2048 }
```

Anonymous structs are structurally typed — field names and types are known at
compile time. Accessing an undefined field is a compile error. If a type needs
a name, declare it with `type`.

## Conversions

Conversions are methods on the source type. There is no `int.parse()`.

```grob
"42".toInt()         // string → int?
42.toString()        // int → string
3.14.toInt()         // float → int (truncates)
42.toFloat()         // int → float
```

## Static Utilities

Functions with no natural receiver live on the type as a namespace:

```grob fragment
int.min(a, b)
int.max(a, b)
int.clamp(v, lo, hi)
float.min(a, b)
float.max(a, b)
```

The rule: conversions are methods on the source value. Static utilities live
on the type namespace. There is no overlap.

See also: [Type Registry](../Type-Registry/string.md), [Operators](Operators.md),
[Functions](Functions.md)
