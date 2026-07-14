using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>env</c> module (D-342): process environment-variable access through the
/// injected <see cref="IEnvironment"/> capability (D-343 — its first real consumer).
/// <c>env.require</c> throws <see cref="ErrorCatalog.E5801"/> (<c>LookupError</c>) through
/// the native-throw seam when the named variable is unset or empty — an empty value counts
/// as absent, matching the stdlib reference's "required means present and non-empty"
/// reading. Registers exactly the qualified names listed in the compile-time twin,
/// <c>NamespaceRegistry</c>'s <c>env</c> entry in <c>Grob.Compiler</c>.
/// </summary>
public sealed class EnvPlugin : IGrobPlugin {
    private readonly IEnvironment _env;

    /// <summary>Initialises the plugin with the <see cref="IEnvironment"/> its natives read/write.</summary>
    public EnvPlugin(IEnvironment env) {
        ArgumentNullException.ThrowIfNull(env);
        _env = env;
    }

    /// <inheritdoc/>
    public string Name => "env";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("env.get", new NativeFunction("env.get", 1, (args, _) => {
            string? value = _env.Get(args[0].AsString());
            return value is null ? GrobValue.Nil : GrobValue.FromString(value);
        }));

        registrar.RegisterNative("env.require", new NativeFunction("env.require", 1, (args, _) => {
            string key = args[0].AsString();
            string? value = _env.Get(key);
            if (string.IsNullOrEmpty(value)) {
                throw new NativeFaultException("LookupError", ErrorCatalog.E5801.Code,
                    $"Required environment variable '{key}' is not set");
            }
            return GrobValue.FromString(value);
        }));

        registrar.RegisterNative("env.has", new NativeFunction("env.has", 1, (args, _) =>
            GrobValue.FromBool(!string.IsNullOrEmpty(_env.Get(args[0].AsString())))));

        registrar.RegisterNative("env.set", new NativeFunction("env.set", 2, (args, _) => {
            _env.Set(args[0].AsString(), args[1].AsString());
            return GrobValue.Nil;
        }));

        registrar.RegisterNative("env.all", new NativeFunction("env.all", 0, (_, _) => {
            var map = new GrobMap();
            foreach (KeyValuePair<string, string> entry in _env.All())
                map.Set(entry.Key, GrobValue.FromString(entry.Value));
            return GrobValue.FromMap(map);
        }));
    }
}
