# json.Node

Returned by `json.read()`, `json.parse()`, `json.stdin()` and the node indexer.

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `isNull` | property | `→ bool` | |
| `isString` | property | `→ bool` | |
| `isInt` | property | `→ bool` | |
| `isFloat` | property | `→ bool` | |
| `isBool` | property | `→ bool` | |
| `isArray` | property | `→ bool` | |
| `isObject` | property | `→ bool` | |
| `asString()` | method | `→ string` | Throws `JsonError` if wrong type |
| `asInt()` | method | `→ int` | Throws `JsonError` if wrong type |
| `asFloat()` | method | `→ float` | Throws `JsonError` if wrong type |
| `asBool()` | method | `→ bool` | Throws `JsonError` if wrong type |
| `asArray()` | method | `→ json.Node[]` | Throws `JsonError` if wrong type |
| `mapAs<T>()` | method | `→ T` | Throws `JsonError` on shape mismatch |
| `[key: string]` | indexer | `→ json.Node?` | Nil for missing keys |
| `toString()` | method | `→ string` | Raw JSON text |

See also: [json module](../Standard-Library/json.md)
