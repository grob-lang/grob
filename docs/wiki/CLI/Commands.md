# CLI Commands

## Core Commands

| Command | Description |
|---------|-------------|
| `grob run <script>` | Run a Grob script |
| `grob repl` | Start an interactive session |
| `grob check <script>` | Type-check without running |
| `grob fmt <script>` | Format a Grob script |
| `grob new <script>` | Create a new script with boilerplate |
| `grob install <plugin>` | Install a plugin from NuGet |
| `grob uninstall <plugin>` | Remove an installed plugin |
| `grob restore` | Install all dependencies in `grob.json` |
| `grob list` | List installed plugins |
| `grob search <query>` | Search for available plugins |
| `grob update <package>` | Update an installed plugin |
| `grob init` | Create a minimal `grob.json` |

## Options

| Option | Description |
|--------|-------------|
| `--dev-plugin <path>` | Load a plugin `.dll` for development |
| `--verbose` | Show compilation details |
| `--version` | Print version and exit |
| `--params <file>` | Load script parameters from file |

## Output Behaviour

Quiet on success, clear on failure. Errors to stderr, results to stdout.

See also: [Install Strategy](Install-Strategy.md),
[Error Messages](Error-Messages.md)
