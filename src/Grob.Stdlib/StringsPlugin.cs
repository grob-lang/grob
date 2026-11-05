using Grob.Core;
using Grob.Runtime;

using static Grob.Stdlib.Signatures;

namespace Grob.Stdlib;

/// <summary>
/// The <c>strings</c> module (D-342): one function, <c>strings.join(parts: string[],
/// separator: string): string</c> — its receiver is an array, not a string instance, so
/// it lives on the module rather than as an instance method (D-071). Every other string
/// operation is an instance method on the <c>string</c> type, registered by
/// <see cref="StringMethodsPlugin"/> (D-066/D-363), not here. Pure — no capability
/// injection, no throw sites. The native registers with its signature, which the type
/// checker reads from the catalogue (D-426).
/// </summary>
public sealed class StringsPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "strings";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative(Native("strings.join", StringType, Param("parts", StringArrayType), Param("separator", StringType)),
            new NativeFunction("strings.join", 2, (args, _) => {
                string separator = args[1].AsString();
                string joined = string.Join(separator, args[0].AsArray().Elements.Select(e => e.AsString()));
                return GrobValue.FromString(joined);
            }));
    }
}
