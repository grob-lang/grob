# File

Returned by `fs.list()`. Properties and methods for file system entries.

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `name` | property | `→ string` | Filename with extension |
| `path` | property | `→ string` | Full absolute path |
| `directory` | property | `→ string` | Parent directory |
| `extension` | property | `→ string` | Lowercased, includes dot |
| `size` | property | `→ int` | Bytes |
| `modified` | property | `→ date` | Last write time |
| `created` | property | `→ date` | Creation time |
| `isDirectory` | property | `→ bool` | |
| `rename(newName: string)` | method | `→ void` | |
| `moveTo(destDir, overwrite: bool = false)` | method | `→ void` | |
| `copyTo(destDir, overwrite: bool = false)` | method | `→ void` | |
| `delete()` | method | `→ void` | |

See also: [fs module](../Standard-Library/fs.md)
