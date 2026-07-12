using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>math</c> module's Sprint 8 Increment A proving vertical (D-342): a namespace
/// constant (<c>math.pi</c>) and a pure native that throws on a domain error
/// (<c>math.sqrt</c>). The rest of <c>math</c> — <c>pow</c>, <c>log</c>, trig,
/// <c>random*</c> — is Increment B. Registers exactly these two qualified names, matching
/// the compile-time <c>NamespaceRegistry</c>'s <c>math</c> entry in <c>Grob.Compiler</c>.
/// </summary>
public sealed class MathPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "math";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterConstant("math.pi", GrobValue.FromFloat(Math.PI));

        registrar.RegisterNative("math.sqrt", new NativeFunction("math.sqrt", 1, (args, _) => {
            double x = args[0].AsFloat();
            if (x < 0.0) {
                throw new NativeFaultException(
                    "ArithmeticError",
                    ErrorCatalog.E5006.Code,
                    $"math.sqrt: domain error — argument {x} is negative");
            }
            return GrobValue.FromFloat(Math.Sqrt(x));
        }));
    }
}
