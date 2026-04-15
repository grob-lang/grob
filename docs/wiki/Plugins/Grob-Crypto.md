# Grob.Crypto

Checksums and hashing. First-party plugin.

```grob
import Grob.Crypto
```

## Functions

| Function | Signature | Notes |
|----------|-----------|-------|
| `crypto.sha256File(path: string)` | `→ string` | Streams internally |
| `crypto.md5File(path: string)` | `→ string` | Streams internally |
| `crypto.sha256(value: string)` | `→ string` | UTF-8 encoded |
| `crypto.md5(value: string)` | `→ string` | UTF-8 encoded |
| `crypto.verifySha256(path, expected: string)` | `→ bool` | Constant-time comparison |
| `crypto.verifyMd5(path, expected: string)` | `→ bool` | Constant-time comparison |

All hex output is lowercase. File functions stream internally — never load full
file into memory.

## Examples

```grob
import Grob.Crypto

hash := crypto.sha256File("C:\releases\tool.zip")
ok := crypto.verifySha256("C:\releases\tool.zip", expected_hash)
```
