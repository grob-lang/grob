# Grob plugin authoring

These rules apply to `plugins/**/*.cs`. A plugin is a NuGet package tagged
`grob-plugin` that implements `IGrobPlugin` and registers native functions and types
with the VM. The first-party plugins (`Grob.Http`, `Grob.Crypto`, `Grob.Zip`) live
under `plugins/` and are built and tested **exactly as a third-party plugin would
be** — they reference `Grob.Runtime` and nothing else. If a first-party plugin needs
special treatment from the runtime, the plugin model is broken; fix the model, not
the plugin.

The three first-party plugin projects are empty shells until Sprint 11 builds them; each
holds only a `.csproj` referencing `Grob.Runtime`. Until then the working examples of
the contract are the stdlib modules in `src/Grob.Stdlib/` (`MathPlugin`, `DatePlugin`,
`IoPlugin` and the rest), which implement `IGrobPlugin` the same way. `Grob.Http` is
planned as the reference implementation once it exists.

## The contract

The `IGrobPlugin` and `IPluginRegistrar` interfaces are defined in `src/Grob.Runtime/` —
read them there rather than from a snapshot here. `Register` takes an
`IPluginRegistrar`, never the VM.

- Reference the `Grob.Runtime` NuGet package for `IGrobPlugin`, `IPluginRegistrar`,
  `FunctionSignature`, `SignatureParameter`, `SignatureType` and `NamedTypeDefinition`;
  `Grob.Core` (`GrobValue`, `NativeFunction`, `NativeFaultException`) comes with it
  transitively. Reference nothing else from the Grob solution. A plugin that needs `Grob.Vm` or `Grob.Compiler` is
  reaching across a boundary it must not cross.
- `Register` runs once per catalogue build: the host adds every plugin to one
  `NativeCatalogueBuilder`, and the type checker and the VM both read the
  `NativeCatalogue` it builds (D-426). It must not perform a capability call.
- Every native is registered with a `FunctionSignature` — qualified name, named and
  typed parameters and a return type. `RegisterNative(FunctionSignature,
  NativeFunction)` is the only overload, so an untyped registration does not compile.
  The signature is what the type checker validates calls against.

## Knowledge enters at the type-checker layer, not the grammar

Plugins do not extend syntax. The TextMate grammar is static. A plugin's knowledge
enters the pipeline through `IGrobPlugin.Register()` at the type-checker layer — it
teaches the checker about new functions and types, it does not add new keywords or
operators. Do not attempt to add grammar from a plugin.

## Type namespacing

This section is the plugin design, not yet built: `Grob.Runtime` has no alias or
collision handling today, and Sprint 11 delivers it. A plugin's registered types are
namespaced by the plugin's default alias. `Grob.Http` will register `Response`,
`Headers`, `Auth` under its namespace; a different
vendor's `AcmeCorp.Http` registering its own `Response` becomes `acme.Response`. The
two coexist. Collisions are compile errors with a clear message — a third-party type
colliding with a stdlib type name is rejected. Keep registered type names specific
and unambiguous.

## Runtime behaviour

- Plugins never auto-download at runtime. `grob install` and `grob restore` are
  deliberate steps. Do not write code that fetches an assembly on demand.
- Honour the output conventions: results to stdout, errors and diagnostics to stderr,
  no emoji, British English. A plugin that prints is part of the same product.
- Raise Grob errors only through the native-throw seam (D-342): throw
  `NativeFaultException`, naming a `GrobError` leaf (`"NetworkError"`, `"IoError"` and
  so on) and the plugin's registered error code. `GrobError` is a Grob-language
  hierarchy, not a C# type. A plugin that lets a raw .NET exception escape into the VM
  has a bug — catch it and rethrow as the right leaf with a useful message.

## Testing

A plugin is tested by registering it into a VM instance and asserting the output of
its functions — the same approach as `Grob.Stdlib.Tests`. The Definition of Done for
a plugin includes publication to NuGet with the `grob-plugin` tag. The
`authoring-a-plugin` skill walks the full procedure end to end.

For HTTP, crypto and archive APIs, ground unfamiliar .NET BCL usage against the
Microsoft Learn MCP server rather than guessing signatures.
