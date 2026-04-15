# Plugin Overview

Plugins extend Grob with additional capabilities beyond the core modules.

## Architecture

Every plugin is a C# class library implementing `IGrobPlugin`. The standard
library itself is implemented as `IGrobPlugin` — the mechanism is identical.

Plugins provide type signatures alongside implementations. The type checker
registers these and verifies call sites at compile time.

## First-Party Plugins

| Plugin | Purpose |
|--------|---------|
| [Grob.Http](Grob-Http.md) | HTTP client with auth helpers |
| [Grob.Crypto](Grob-Crypto.md) | Checksums and hashing |
| [Grob.Zip](Grob-Zip.md) | Archive compression and extraction |

## Security

Loading a plugin is equivalent to running arbitrary code. This is documented
prominently. No sandbox claims are made.

See also: [Writing Plugins](Writing-Plugins.md),
[Community Registry](Community-Registry.md)
