# guid

First-class primitive type. Distinct from `string` — `guid == string` is a
compile error.

## Static Members

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `guid.newV4()` | static | `→ guid` | Random |
| `guid.newV7()` | static | `→ guid` | Time-ordered (RFC 9562) |
| `guid.newV5(namespace, name: string...)` | static | `→ guid` | Deterministic |
| `guid.parse(value: string)` | static | `→ guid` | Throws if invalid |
| `guid.tryParse(value: string)` | static | `→ guid?` | Nil if invalid |
| `guid.empty` | static | `→ guid` | All-zeros sentinel |

## Instance Members

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `version` | property | `→ int` | The version encoded in the value: 4, 5 or 7 for a generated GUID, whatever a parsed GUID carries, 0 for `guid.empty` |
| `isEmpty` | property | `→ bool` | |
| `toString()` | method | `→ string` | Lowercase with hyphens |
| `toUpperString()` | method | `→ string` | Uppercase with hyphens |
| `toCompactString()` | method | `→ string` | 32 hex chars, no hyphens |

See also: [guid module](../Standard-Library/guid.md)
