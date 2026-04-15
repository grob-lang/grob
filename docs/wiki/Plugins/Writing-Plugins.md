# Writing Plugins

## The Plugin Interface

```csharp
public interface IGrobPlugin
{
    string Name { get; }
    void Register(GrobVM vm);
}
```

Reference the `Grob.Runtime` NuGet package. Implement `IGrobPlugin`. Register
native functions with type signatures.

```csharp
public class MyPlugin : IGrobPlugin
{
    public string Name => "MyCompany.MyPlugin";

    public void Register(GrobVM vm)
    {
        vm.RegisterNative("myplugin.hello",
            signature: new FunctionSignature(
                parameters: [new Parameter("name", GrobType.String)],
                returnType: GrobType.String
            ),
            implementation: args => {
                return new StringValue($"Hello {args[0].AsString()}!");
            }
        );
    }
}
```

## Publishing

Publish to NuGet tagged `grob-plugin`. Users install with `grob install`.

## Development

Use `--dev-plugin` to load a local `.dll` during development:

```
grob run script.grob --dev-plugin MyPlugin.dll
```

See also: [Overview](Overview.md)
