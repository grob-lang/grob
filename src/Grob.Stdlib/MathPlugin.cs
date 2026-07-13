using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>math</c> module (D-342): constants (<c>pi</c>, <c>e</c>, <c>tau</c>), the pow/log/
/// trig surface, degree/radian conversion, and <c>random</c>/<c>randomInt</c>/<c>randomSeed</c>
/// on the injected <see cref="IRandomSource"/> (D-343 — this is its first real consumer).
/// Domain-error throws (<c>sqrt</c>, <c>log</c>, <c>log10</c>, <c>asin</c>, <c>acos</c>) all
/// reuse <see cref="ErrorCatalog.E5006"/> through the native-throw seam; every other function
/// follows plain IEEE 754 semantics and never throws. Registers exactly the qualified names
/// listed in the compile-time twin, <c>NamespaceRegistry</c>'s <c>math</c> entry in
/// <c>Grob.Compiler</c>.
/// </summary>
public sealed class MathPlugin : IGrobPlugin {
    private readonly IRandomSource _randomSource;

    /// <summary>Initialises the plugin with the <see cref="IRandomSource"/> its random natives draw from.</summary>
    public MathPlugin(IRandomSource randomSource) {
        ArgumentNullException.ThrowIfNull(randomSource);
        _randomSource = randomSource;
    }

    /// <inheritdoc/>
    public string Name => "math";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterConstant("math.pi", GrobValue.FromFloat(Math.PI));
        registrar.RegisterConstant("math.e", GrobValue.FromFloat(Math.E));
        registrar.RegisterConstant("math.tau", GrobValue.FromFloat(2.0 * Math.PI));

        registrar.RegisterNative("math.sqrt", new NativeFunction("math.sqrt", 1, (args, _) => {
            double x = args[0].AsFloat();
            if (x < 0.0) throw DomainFault($"math.sqrt: domain error — argument {x} is negative");
            return GrobValue.FromFloat(Math.Sqrt(x));
        }));

        registrar.RegisterNative("math.pow", new NativeFunction("math.pow", 2, (args, _) =>
            GrobValue.FromFloat(Math.Pow(args[0].AsFloat(), args[1].AsFloat()))));

        registrar.RegisterNative("math.log", new NativeFunction("math.log", 1, (args, _) => {
            double x = args[0].AsFloat();
            if (x <= 0.0) throw DomainFault($"math.log: domain error — argument {x} is not positive");
            return GrobValue.FromFloat(Math.Log(x));
        }));

        registrar.RegisterNative("math.log10", new NativeFunction("math.log10", 1, (args, _) => {
            double x = args[0].AsFloat();
            if (x <= 0.0) throw DomainFault($"math.log10: domain error — argument {x} is not positive");
            return GrobValue.FromFloat(Math.Log10(x));
        }));

        registrar.RegisterNative("math.sin", new NativeFunction("math.sin", 1, (args, _) =>
            GrobValue.FromFloat(Math.Sin(args[0].AsFloat()))));

        registrar.RegisterNative("math.cos", new NativeFunction("math.cos", 1, (args, _) =>
            GrobValue.FromFloat(Math.Cos(args[0].AsFloat()))));

        registrar.RegisterNative("math.tan", new NativeFunction("math.tan", 1, (args, _) =>
            GrobValue.FromFloat(Math.Tan(args[0].AsFloat()))));

        registrar.RegisterNative("math.asin", new NativeFunction("math.asin", 1, (args, _) => {
            double x = args[0].AsFloat();
            if (x is < -1.0 or > 1.0) throw DomainFault($"math.asin: domain error — argument {x} is outside [-1, 1]");
            return GrobValue.FromFloat(Math.Asin(x));
        }));

        registrar.RegisterNative("math.acos", new NativeFunction("math.acos", 1, (args, _) => {
            double x = args[0].AsFloat();
            if (x is < -1.0 or > 1.0) throw DomainFault($"math.acos: domain error — argument {x} is outside [-1, 1]");
            return GrobValue.FromFloat(Math.Acos(x));
        }));

        registrar.RegisterNative("math.atan", new NativeFunction("math.atan", 1, (args, _) =>
            GrobValue.FromFloat(Math.Atan(args[0].AsFloat()))));

        registrar.RegisterNative("math.atan2", new NativeFunction("math.atan2", 2, (args, _) =>
            GrobValue.FromFloat(Math.Atan2(args[0].AsFloat(), args[1].AsFloat()))));

        registrar.RegisterNative("math.toRadians", new NativeFunction("math.toRadians", 1, (args, _) =>
            GrobValue.FromFloat(args[0].AsFloat() * Math.PI / 180.0)));

        registrar.RegisterNative("math.toDegrees", new NativeFunction("math.toDegrees", 1, (args, _) =>
            GrobValue.FromFloat(args[0].AsFloat() * 180.0 / Math.PI)));

        registrar.RegisterNative("math.random", new NativeFunction("math.random", 0, (_, _) =>
            GrobValue.FromFloat(_randomSource.NextDouble())));

        registrar.RegisterNative("math.randomInt", new NativeFunction("math.randomInt", 2, (args, _) =>
            GrobValue.FromInt(_randomSource.NextInt(args[0].AsInt(), args[1].AsInt()))));

        registrar.RegisterNative("math.randomSeed", new NativeFunction("math.randomSeed", 1, (args, _) => {
            _randomSource.Reseed(args[0].AsInt());
            return GrobValue.Nil;
        }));
    }

    private static NativeFaultException DomainFault(string message) =>
        new("ArithmeticError", ErrorCatalog.E5006.Code, message);
}
