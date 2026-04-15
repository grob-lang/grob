# regex — Regular Expressions

Regular expression operations. Core module — auto-available, no import required.

## Regex Literals

```grob
pattern := /^\d+$/
pattern := /error|warning/i     // case-insensitive
```

Supported flags: `i` (case-insensitive), `m` (multiline `^`/`$`).

## Module Functions (Convenience)

| Function | Signature | Description |
|----------|-----------|-------------|
| `regex.isMatch(pattern, input)` | `→ bool` | |
| `regex.match(pattern, input)` | `→ Match?` | |
| `regex.matchAll(pattern, input)` | `→ Match[]` | |
| `regex.replace(pattern, input, replacement)` | `→ string` | |
| `regex.replaceAll(pattern, input, replacement)` | `→ string` | |
| `regex.split(pattern, input)` | `→ string[]` | |
| `regex.escape(input: string)` | `→ string` | Escape special chars |

Module-level functions take string patterns and compile on each call. For
repeated use, prefer a regex literal — compiled once at declaration.

## Examples

```grob
pattern := /\d{3}-\d{4}/
if (pattern.isMatch(phone)) {
    m := pattern.match(phone)
    print(m.value)
}
```

See also: [Regex type](../Type-Registry/Regex.md),
[Match type](../Type-Registry/Match.md)
