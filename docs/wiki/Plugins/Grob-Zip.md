# Grob.Zip

Archive compression and extraction. First-party plugin.

```grob
import Grob.Zip
```

## Functions

| Function | Signature |
|----------|-----------|
| `zip.create(dest, source: string, overwrite: bool = false)` | `→ void` |
| `zip.create(dest, source: File, overwrite: bool = false)` | `→ void` |
| `zip.create(dest, source: string[], overwrite: bool = false)` | `→ void` |
| `zip.extract(archive, dest: string, overwrite: bool = false)` | `→ void` |
| `zip.list(archive: string)` | `→ ZipEntry[]` |

## Examples

```grob
import Grob.Zip

zip.create("C:\backup\reports.zip", "C:\Reports")
zip.extract("C:\backup\reports.zip", "C:\Restored")

entries := zip.list("C:\backup\reports.zip")
for entry in entries {
    print("${entry.name}  ${entry.size} bytes")
}
```

See also: [ZipEntry](../Type-Registry/ZipEntry.md)
