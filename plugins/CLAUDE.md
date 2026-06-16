# Grob plugin authoring

These rules apply to `plugins/**/*.cs`. A plugin is a NuGet package tagged
`grob-plugin` that implements `IGrobPlugin` and registers native functions and types
with the VM. The first-party plugins (`Grob.Http`, `Grob.Crypto`, `Grob.Zip`) live
under `plugins/` and are built and tested **exactly as a third-party plugin would
be** — they reference `Grob.Runtime` and nothing else. If a first-party plugin needs
special treatment from the runtime, the plugin model is broken; fix the model, not
the plugin.

`Grob.Http` is the reference implementation — the most complete example of a
well-authored plugin. When in doubt, follow its shape.

## The contract

```csharp
public interface IGrobPlugin
{
    string Name { get; }
    void Register(GrobVM vm);
}
```

- Reference the `Grob.Runtime` NuGet package for `IGrobPlugin`, `GrobVM`,
  `FunctionSignature`, `Parameter` and the `GrobError` hierarchy. Reference nothing
  else from the Grob solution. A plugin that needs `Grob.Vm` or `Grob.Compiler` is
  reaching across a boundary it must not cross.
- `Register` is called at compile time for third-party plugins, and at VM startup
  (alphabetically by module name) for core stdlib plugins.
- Always supply a `FunctionSignature` for every registered native — name, parameter
  types and return type. This is what the type checker validates calls against; an
  untyped registration defeats Grob's static checking.

## Knowledge enters at the type-checker layer, not the grammar

Plugins do not extend syntax. The TextMate grammar is static. A plugin's knowledge
enters the pipeline through `IGrobPlugin.Register()` at the type-checker layer — it
teaches the checker about new functions and types, it does not add new keywords or
operators. Do not attempt to add grammar from a plugin.

## Type namespacing

A plugin's registered types are namespaced by the plugin's default alias.
`Grob.Http` registers `Response`, `Headers`, `Auth` under its namespace; a different
vendor's `AcmeCorp.Http` registering its own `Response` becomes `acme.Response`. The
two coexist. Collisions are compile errors with a clear message — a third-party type
colliding with a stdlib type name is rejected. Keep registered type names specific
and unambiguous.

## Runtime behaviour

- Plugins never auto-download at runtime. `grob install` and `grob restore` are
  deliberate steps. Do not write code that fetches an assembly on demand.
- Honour the output conventions: results to stdout, errors and diagnostics to stderr,
  no emoji, British English. A plugin that prints is part of the same product.
- Throw only subtypes of `GrobError`. A plugin that lets a raw .NET exception escape
  into the VM has a bug — wrap it in the appropriate `GrobError` leaf (`NetworkError`,
  `IoError`, etc.) with a useful message.

## Testing

A plugin is tested by registering it into a VM instance and asserting the output of
its functions — the same approach as `Grob.Stdlib.Tests`. The Definition of Done for
a plugin includes publication to NuGet with the `grob-plugin` tag. The
`authoring-a-plugin` skill walks the full procedure end to end.

For HTTP, crypto and archive APIs, ground unfamiliar .NET BCL usage against the
Microsoft Learn MCP server rather than guessing signatures.
