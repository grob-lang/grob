# guid — GUID Generation

GUID generation, parsing and formatting. Core module — auto-available, no import
required. Registers the `guid` built-in type.

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `guid.newV4()` | `→ guid` | Random UUID (version 4) |
| `guid.newV5(namespace: guid, name: string...)` | `→ guid` | Deterministic name-based UUID (version 5, SHA-1); the name segments are concatenated before hashing |
| `guid.newV7()` | `→ guid` | Time-ordered UUID (version 7) |
| `guid.parse(value: string)` | `→ guid` | Parse from string; throws `ParseError` if invalid |
| `guid.tryParse(value: string)` | `→ guid?` | Parse from string; returns nil if invalid |
| `guid.empty` | `guid` | The nil GUID (all zeros) |
| `guid.namespaces.dns`, `.url`, `.oid` | `guid` | The RFC 4122 well-known namespaces |

## guid Type Methods

| Member | Signature | Description |
|--------|-----------|-------------|
| `toString()` | `→ string` | Lowercase hyphenated format (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) |
| `toStringUpper()` | `→ string` | Uppercase hyphenated format |
| `toStringN()` | `→ string` | 32 hex digits, no hyphens |
| `isEmpty` | `→ bool` | True if all zeros |

## Examples

```grob
id := guid.newV4()
print(id.toString())
// e.g. 3f2504e0-4f89-11d3-9a0c-0305e82c3301

// Deterministic ID from a name
user_id := guid.newV5(guid.namespaces.url, "chris@example.com")

// Time-ordered (sortable) ID — useful for database keys
record_id := guid.newV7()

// Parsing
parsed := guid.tryParse("not-a-guid")
if (parsed == nil) {
    print("Invalid GUID")
}
```

## Notes

- `guid.newV4()` uses a cryptographically secure random number generator.
- `guid.newV7()` produces lexicographically sortable GUIDs — preferred for
  database primary keys.
- For deterministic IDs derived from content, use `guid.newV5()` with a
  well-known namespace from `guid.namespaces`.
