using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>strings</c> module (D-342): one function, <c>strings.join(parts: string[],
/// separator: string): string</c> — its receiver is an array, not a string instance, so
/// it lives on the module rather than as an instance method (D-071). Every other string
/// operation is an instance method on the <c>string</c> type, out of scope here. Pure —
/// no capability injection, no throw sites. Registers exactly the qualified name listed
/// in the compile-time twin, <c>NamespaceRegistry</c>'s <c>strings</c> entry in
/// <c>Grob.Compiler</c>.
/// </summary>
public sealed class StringsPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "strings";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("strings.join", new NativeFunction("strings.join", 2, (args, _) => {
            string separator = args[1].AsString();
            string joined = string.Join(separator, args[0].AsArray().Elements.Select(e => e.AsString()));
            return GrobValue.FromString(joined);
        }));
    }
}
