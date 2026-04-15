# strings — String Utilities

String operations. Core module — auto-available, no import required.

The `strings` module contains one function. All other string operations are
instance methods on the [string](../Type-Registry/string.md) type.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `strings.join(parts: string[], separator: string = "")` | `→ string` | Join array with separator |

## Examples

```grob
names := ["Alice", "Bob", "Charlie"]
result := strings.join(names, ", ")    // "Alice, Bob, Charlie"

parts := "hello world".split(" ")     // string method, not module function
upper := "grob".upper()               // string method
```

`strings.join()` lives on the module because its receiver is an array, not a
string instance. All other operations (split, trim, upper, lower, contains,
replace, etc.) are methods on the `string` type.

See also: [string type](../Type-Registry/string.md)
