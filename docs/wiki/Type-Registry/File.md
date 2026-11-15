# File — Type Registry

Returned by `fs.list()`. Built-in type registered by the `fs` stdlib plugin at
startup. The type checker knows its full shape — calling an undefined member is a
compile error.

## Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `name` | `→ string` | Filename with extension, no path |
| `path` | `→ string` | Full absolute path |
| `directory` | `→ string` | Parent directory path |
| `extension` | `→ string` | Lowercased, includes dot — e.g. `.xlsx` |
| `size` | `→ int` | Size in bytes |
| `modified` | `→ date` | Last write time |
| `created` | `→ date` | Creation time |
| `isDirectory` | `→ bool` | True if entry is a directory |

`extension` is always lowercased and always includes the dot. Callers never need
to normalise it: `".jpg"`, `".xlsx"`, `".csv"`.

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `rename(newName: string)` | `→ void` | Rename in place — new name only, not path |
| `moveTo(destDir: string)` | `→ void` | Move to destination directory |
| `copyTo(destDir: string)` | `→ void` | Copy to destination directory |
| `delete()` | `→ void` | Delete the file |

`file.moveTo()` and `fs.move()` both exist. The fluent form is for collection
pipelines; the module form is for explicit path manipulation.

## Examples

### File properties

```grob
files := fs.list("C:\\Reports")

for file in files {
    print("${file.name}")
    print("  Path:      ${file.path}")
    print("  Extension: ${file.extension}")
    print("  Size:      ${file.size} bytes")
    print("  Modified:  ${file.modified.toIso()}")
}
```

### Bulk rename

```grob
for file in fs.list("C:\\Photos") {
    if (file.extension == ".jpeg") {
        new_name := file.name.replace(".jpeg", ".jpg")
        file.rename(new_name)
    }
}
```

### Move old files to archive

```grob
cutoff := date.today().addDays(-90)

fs.list("C:\\Reports")
    .filter(f => f.modified < cutoff)
    .each(f => {
        f.moveTo("C:\\Archive")
        print("Archived: ${f.name}")
    })
```

See also: [fs module](../Standard-Library/fs.md),
[path module](../Standard-Library/path.md)
