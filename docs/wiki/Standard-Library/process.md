# process — External Command Execution

Run external commands and capture output. Core module — auto-available, no
import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `process.run(cmd, args: string[], timeout: int = 0)` | `→ ProcessResult` | Safe form — no shell interpolation |
| `process.runOrFail(cmd, args: string[], timeout: int = 0)` | `→ ProcessResult` | Throws on non-zero exit |
| `process.runShell(cmd: string, timeout: int = 0)` | `→ ProcessResult` | Shell form — full command string |
| `process.runShellOrFail(cmd: string, timeout: int = 0)` | `→ ProcessResult` | Shell form — throws on non-zero exit |

`timeout` is in seconds. `0` means infinite. On timeout, throws `ProcessError`.

## Examples

```grob
result := process.run("az", ["group", "show", "--name", groupName])
print(result.stdout)

process.runOrFail("git", ["commit", "-m", message])

result := process.runShell("az group list")
result := process.run("az", ["deployment", "wait"], timeout: 300)
```

`process.run()` is the primary form — arguments are never shell-interpolated.
`process.runShell()` is for full command strings where shell interpretation is
intentional.

See also: [ProcessResult](../Type-Registry/ProcessResult.md)
