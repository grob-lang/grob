# Match — Type Registry

Returned by `Regex.match()` and `Regex.matchAll()`.

## Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `value` | `→ string` | The matched text |
| `index` | `→ int` | Zero-based position in input |
| `length` | `→ int` | Length of match |
| `groups` | `→ string[]` | `groups[0]` is full match; `groups[1]+` are capture groups |

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `group(name: string)` | `→ string?` | Named capture group. Nil if not present |

## Examples

```grob
pattern := /(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})/
m := pattern.match("Date: 2026-04-05")

if (m != nil) {
    print(m.value)           // "2026-04-05"
    print(m.index)           // 6
    print(m.groups[1])       // "2026"
    print(m.group("month"))  // "04"
}
```

See also: [Regex type](Regex.md), [regex module](../Standard-Library/regex.md)
