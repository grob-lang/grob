using Grob.Core;
using Grob.Runtime;

using static Grob.Stdlib.Signatures;

namespace Grob.Stdlib;

/// <summary>
/// The <c>math</c> module (D-342): constants (<c>pi</c>, <c>e</c>, <c>tau</c>), the pow/log/
/// trig surface, degree/radian conversion, and <c>random</c>/<c>randomInt</c>/<c>randomSeed</c>
/// on the injected <see cref="IRandomSource"/> (D-343 — this is its first real consumer).
/// Domain-error throws (<c>sqrt</c>, <c>log</c>, <c>log10</c>, <c>asin</c>, <c>acos</c>) all
/// reuse <see cref="ErrorCatalog.E5006"/> through the native-throw seam; every other function
/// follows plain IEEE 754 semantics and never throws. Each native registers with its
/// signature, which the type checker reads from the catalogue (D-426).
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

        registrar.RegisterNative(Signature("math.sqrt", FloatType, Param("n", FloatType)),
            new NativeFunction("math.sqrt", 1, (args, _) => {
                double x = args[0].AsFloat();
                if (x < 0.0) throw DomainFault($"math.sqrt: domain error — argument {x} is negative");
                return GrobValue.FromFloat(Math.Sqrt(x));
            }));

        registrar.RegisterNative(Signature("math.pow", FloatType, Param("base", FloatType), Param("exp", FloatType)),
            new NativeFunction("math.pow", 2, (args, _) =>
                GrobValue.FromFloat(Math.Pow(args[0].AsFloat(), args[1].AsFloat()))));

        registrar.RegisterNative(Signature("math.log", FloatType, Param("n", FloatType)),
            new NativeFunction("math.log", 1, (args, _) => {
                double x = args[0].AsFloat();
                if (x <= 0.0) throw DomainFault($"math.log: domain error — argument {x} is not positive");
                return GrobValue.FromFloat(Math.Log(x));
            }));

        registrar.RegisterNative(Signature("math.log10", FloatType, Param("n", FloatType)),
            new NativeFunction("math.log10", 1, (args, _) => {
                double x = args[0].AsFloat();
                if (x <= 0.0) throw DomainFault($"math.log10: domain error — argument {x} is not positive");
                return GrobValue.FromFloat(Math.Log10(x));
            }));

        registrar.RegisterNative(Signature("math.sin", FloatType, Param("radians", FloatType)),
            new NativeFunction("math.sin", 1, (args, _) =>
                GrobValue.FromFloat(Math.Sin(args[0].AsFloat()))));

        registrar.RegisterNative(Signature("math.cos", FloatType, Param("radians", FloatType)),
            new NativeFunction("math.cos", 1, (args, _) =>
                GrobValue.FromFloat(Math.Cos(args[0].AsFloat()))));

        registrar.RegisterNative(Signature("math.tan", FloatType, Param("radians", FloatType)),
            new NativeFunction("math.tan", 1, (args, _) =>
                GrobValue.FromFloat(Math.Tan(args[0].AsFloat()))));

        registrar.RegisterNative(Signature("math.asin", FloatType, Param("n", FloatType)),
            new NativeFunction("math.asin", 1, (args, _) => {
                double x = args[0].AsFloat();
                if (x is < -1.0 or > 1.0) throw DomainFault($"math.asin: domain error — argument {x} is outside [-1, 1]");
                return GrobValue.FromFloat(Math.Asin(x));
            }));

        registrar.RegisterNative(Signature("math.acos", FloatType, Param("n", FloatType)),
            new NativeFunction("math.acos", 1, (args, _) => {
                double x = args[0].AsFloat();
                if (x is < -1.0 or > 1.0) throw DomainFault($"math.acos: domain error — argument {x} is outside [-1, 1]");
                return GrobValue.FromFloat(Math.Acos(x));
            }));

        registrar.RegisterNative(Signature("math.atan", FloatType, Param("n", FloatType)),
            new NativeFunction("math.atan", 1, (args, _) =>
                GrobValue.FromFloat(Math.Atan(args[0].AsFloat()))));

        registrar.RegisterNative(Signature("math.atan2", FloatType, Param("y", FloatType), Param("x", FloatType)),
            new NativeFunction("math.atan2", 2, (args, _) =>
                GrobValue.FromFloat(Math.Atan2(args[0].AsFloat(), args[1].AsFloat()))));

        registrar.RegisterNative(Signature("math.toRadians", FloatType, Param("degrees", FloatType)),
            new NativeFunction("math.toRadians", 1, (args, _) =>
                GrobValue.FromFloat(args[0].AsFloat() * Math.PI / 180.0)));

        registrar.RegisterNative(Signature("math.toDegrees", FloatType, Param("radians", FloatType)),
            new NativeFunction("math.toDegrees", 1, (args, _) =>
                GrobValue.FromFloat(args[0].AsFloat() * 180.0 / Math.PI)));

        registrar.RegisterNative(Signature("math.random", FloatType), new NativeFunction("math.random", 0, (_, _) =>
            GrobValue.FromFloat(_randomSource.NextDouble())));

        registrar.RegisterNative(Signature("math.randomInt", IntType, Param("min", IntType), Param("max", IntType)),
            new NativeFunction("math.randomInt", 2, (args, _) =>
                GrobValue.FromInt(_randomSource.NextInt(args[0].AsInt(), args[1].AsInt()))));

        registrar.RegisterNative(Signature("math.randomSeed", NilType, Param("seed", IntType)),
            new NativeFunction("math.randomSeed", 1, (args, _) => {
                _randomSource.Reseed(args[0].AsInt());
                return GrobValue.Nil;
            }));
    }

    private static NativeFaultException DomainFault(string message) =>
        new("ArithmeticError", ErrorCatalog.E5006.Code, message);
}
