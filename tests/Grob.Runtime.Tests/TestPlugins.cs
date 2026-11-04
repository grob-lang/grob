using Grob.Core;

namespace Grob.Runtime.Tests;

/// <summary>
/// Minimal plugins for catalogue tests: a public-surface plugin driven by a delegate,
/// and a first-party one that also reaches the internal primitive-receiver surface.
/// </summary>
internal static class TestPlugins {
    internal static readonly SignatureType.Concrete Int = SignatureType.Of(GrobType.Int);
    internal static readonly SignatureType.Concrete Float = SignatureType.Of(GrobType.Float);
    internal static readonly SignatureType.Concrete String = SignatureType.Of(GrobType.String);

    internal static NativeFunction Native(string name, int arity = 0) =>
        new(name, arity, (_, _) => GrobValue.Nil);

    internal static FunctionSignature Signature(string name, params SignatureParameter[] parameters) =>
        new(name, parameters, Int);

    internal static NamedTypeDefinition NamedType(string name, string? renderPrefix = null) =>
        new(
            name,
            [new NamedTypePropertyBinding(new PropertySignature("value", Int), s => s.GetField("value"))],
            [new NamedTypeMethodBinding(Signature("next"), s => Native("next"))],
            v => (renderPrefix ?? name) + ":" + v.AsStruct().GetField("value").AsInt());

    internal static GrobValue StructOf(string typeName, long value) =>
        GrobValue.FromStruct(new GrobStruct(typeName, [new KeyValuePair<string, GrobValue>("value", GrobValue.FromInt(value))]));

    /// <summary>A plugin exposing only the public <see cref="IGrobPlugin"/> surface.</summary>
    internal sealed class Plugin(string name, Action<IPluginRegistrar> register) : IGrobPlugin {
        public string Name => name;

        public void Register(IPluginRegistrar registrar) => register(registrar);
    }

    /// <summary>A first-party plugin that also registers primitive members and built-ins.</summary>
    internal sealed class PrimitivePlugin(
            string name,
            Action<IPrimitiveMemberRegistrar> registerPrimitives,
            Action<IPluginRegistrar>? register = null) : IPrimitiveMemberPlugin {
        public string Name => name;

        public void Register(IPluginRegistrar registrar) => register?.Invoke(registrar);

        public void RegisterPrimitiveMembers(IPrimitiveMemberRegistrar registrar) => registerPrimitives(registrar);
    }
}
