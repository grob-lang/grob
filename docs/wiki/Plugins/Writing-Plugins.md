# Writing Plugins

## Minimal Example

```csharp
using Grob.Core;
using Grob.Runtime;

public sealed class HelloPlugin : IGrobPlugin
{
    public string Name => "AcmeCorp.Hello";

    public void Register(IPluginRegistrar registrar)
    {
        SignatureType.Concrete stringType = SignatureType.Of(GrobType.String);

        registrar.RegisterNative(
            new FunctionSignature(
                "hello.greet",
                [new SignatureParameter("name", stringType)],
                stringType),
            new NativeFunction("hello.greet", 1, (args, _) =>
                GrobValue.FromString($"Hello, {args[0].AsString()}!")));
    }
}
```

`Register` receives an `IPluginRegistrar`, never the VM. Every native is registered
with a `FunctionSignature` — a qualified name, named and typed parameters and a return
type — and `RegisterNative` has no overload without one. The host builds one catalogue
from every plugin, which the type checker and the VM both read (D-426). Named types
register through `RegisterNamedType` with a `NamedTypeDefinition`.

Build as a class library targeting the same .NET version as the Grob runtime.
Reference the `Grob.Runtime` NuGet package for interfaces and types.

## During Development

```
grob run script.grob --dev-plugin C:\dev\bin\Debug\MyPlugin.dll
```

`--dev-plugin` is a development tool only. Published plugins are loaded via
`import` after `grob install`.

## Conventions

**Namespace your functions** — prefix all names with your plugin's alias:
`xml.parse()`, `pnp.getSite()`.

**Name your parameters deliberately** — the `FunctionSignature` every registration
requires records them, and they are public API once published. The signature gives
scripts compile-time type safety.

**Fail with `GrobRuntimeException`** — do not let raw C# exceptions surface.

**Document your functions** — README with signatures, descriptions and examples.

## Naming

| Name | Valid |
|------|-------|
| `Grob.Xml` | Yes — first-party style |
| `AcmeCorp.Xml` | Yes — community style |
| `MyXmlPlugin` | No — will not appear in `grob search` |

## Generic Functions

Plugins that expose generic functions (like `mapAs<T>()`) express type parameters
through `FunctionSignature`'s `TypeParameter` list in `Grob.Runtime`. The shape is in
the SDK now, but the type checker does not yet substitute type parameters: a signature
that declares one fails loudly with an internal error until the `mapAs<T>` increment
lands (D-426 Decision 4).

## Recommended Repository Structure

```
grob-plugin-xml/
├── src/
│   └── AcmeCorp.Xml/
│       ├── AcmeCorp.Xml.csproj
│       ├── XmlPlugin.cs
│       └── ...
├── tests/
│   └── AcmeCorp.Xml.Tests/
├── examples/
│   └── parse-config.grob
├── README.md
└── LICENSE
```

## Publishing

Publish to NuGet with the `grob-plugin` tag. This makes the plugin discoverable
via `grob search`.

`Grob.Runtime` is versioned independently from the runtime. Your plugin declares
which version it targets — this is the compatibility contract.

See also: [Overview](Overview.md), [Community Registry](Community-Registry.md)
