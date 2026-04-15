# Types

Grob is statically typed with type inference. Types are resolved at compile time
and never checked at runtime.

## Built-in Types

| Type | Description | Literal examples |
|------|-------------|-----------------|
| `int` | 64-bit signed integer | `42`, `0xFF`, `0b1010`, `1_000_000` |
| `float` | 64-bit floating point | `3.14`, `1.5e10` |
| `string` | UTF-8 text | `"hello"`, `"${name}"`, `` `raw` `` |
| `bool` | Boolean | `true`, `false` |
| `nil` | Absence of value | `nil` |
| `guid` | RFC 4122/9562 GUID | `guid.newV4()` |

## Arrays

```grob
numbers := [1, 2, 3]           // int[]
names   := ["Alice", "Bob"]    // string[]
items: int[] := []              // empty — annotation required
matrix := [[1, 0], [0, 1]]     // int[][] — nested arrays valid
```

Arrays are typed — all elements must be the same type. Empty array literals
require a type annotation because the element type cannot be inferred.

## Maps

```grob
headers := map<string, string>{
    "Content-Type":  "application/json"
    "X-Api-Version": "2024-01-01"
}

flags := map<string, bool>{ "verbose": true, "dryRun": false }
```

Maps are first-class built-in types. Keys must be `string` in v1. Entries are
separated by newlines or commas.

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

## Nullable Types

The `?` suffix marks a type as nullable. Non-nullable types are guaranteed
non-nil at compile time.

```grob
name: string? := nil     // may be nil
count := 42              // never nil — int is non-nullable

if (name != nil) {
    print(name)          // safe — compiler narrows type to string
}
```

`??` for nil coalescing: `display := name ?? "Anonymous"`

`?.` for optional chaining: `length := name?.length ?? 0`

Flow-sensitive narrowing: inside an `if (x != nil)` block the type checker
narrows the variable from `T?` to `T`.

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

```grob
c := Config {
    host: "localhost"          // port and timeout use defaults
}
```

Construction uses named, unordered fields. `TypeName { }` is an expression that
produces a value of that type.

## Numeric Precision

`int` is 64-bit signed. Overflow throws `RuntimeError` — checked arithmetic,
never wraps silently. `float` is 64-bit IEEE 754. Division by zero throws
`RuntimeError`. The only implicit conversion is `int` → `float` in mixed
arithmetic.

## Equality Semantics

Primitives compare by value. User-defined types and anonymous structs use
field-by-field value equality. Arrays compare element-wise. Maps compare
entry-wise. `==` between incompatible types is a compile error.

See also: [Operators](Operators.md), [Control Flow](Control-Flow.md),
[Functions](Functions.md)
