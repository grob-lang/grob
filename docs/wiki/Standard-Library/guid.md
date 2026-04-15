# guid — GUID Generation and Parsing

GUID generation, parsing and formatting. Core module — auto-available, no import
required. `guid` is a first-class primitive type distinct from `string`.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `guid.newV4()` | `→ guid` | Random |
| `guid.newV7()` | `→ guid` | Time-ordered (RFC 9562) |
| `guid.newV5(namespace, name: string...)` | `→ guid` | Deterministic, variadic |
| `guid.parse(value: string)` | `→ guid` | Throws if invalid |
| `guid.tryParse(value: string)` | `→ guid?` | Nil if invalid |
| `guid.empty` | `→ guid` | All-zeros sentinel |

## Well-Known Namespaces

```grob
guid.namespaces.dns   // RFC 4122
guid.namespaces.url
guid.namespaces.oid
```

## Examples

```grob
id := guid.newV4()
storageId := guid.newV5(guid.namespaces.url, rgId, "storage", env)

print("Resource: ${id}")
storageName := "sa${storageId.toCompactString()}"
```

Compile-time validation: `guid.parse()` with a string literal argument is
validated at compile time.

See also: [guid type](../Type-Registry/guid.md)
