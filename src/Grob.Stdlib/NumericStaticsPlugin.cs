using Grob.Core;
using Grob.Runtime;

using static Grob.Stdlib.Signatures;

namespace Grob.Stdlib;

/// <summary>
/// The <c>int</c>/<c>float</c> type-static function surface's runtime natives
/// (<c>int.min</c>/<c>.max</c>/<c>.clamp</c>, <c>float.min</c>/<c>.max</c>/<c>.clamp</c>,
/// Sprint 9 Increment A1b/D-370), each with its signature (D-426 change group 5). The
/// namespace-receiver counterpart of D-369's instance-method surface
/// (<see cref="NumericMethodsPlugin"/>). <c>int</c> and <c>float</c> are primitive type
/// names, which the public registrar refuses as namespaces (gate report F8), so this
/// plugin registers through the internal primitive surface
/// (<see cref="IPrimitiveMemberRegistrar.RegisterPrimitiveStatic"/>) and its public
/// <see cref="Register"/> registers nothing. Kept as a separate plugin rather than folded
/// into <see cref="NumericMethodsPlugin"/>: a type-static call (<c>int.min(a, b)</c>) and
/// an instance member (<c>a.abs()</c>) are different call shapes the checker resolves
/// through different catalogue tables, even though both register under the same
/// <c>"int."</c>/<c>"float."</c> global prefix (<c>min</c>/<c>max</c>/<c>clamp</c> are
/// distinct names from D-369's <c>toString</c>/<c>toFloat</c>/<c>abs</c>/etc., so there is
/// no collision).
/// Pure — no capability injection, mirroring <see cref="NumericMethodsPlugin"/> and
/// <see cref="StringMethodsPlugin"/>.
/// <para>
/// <c>min</c>/<c>max</c> never overflow — they select an existing operand rather than
/// computing a new one, so no <c>checked(...)</c> guard is needed for either the
/// <c>int</c> or <c>float</c> pair. <c>clamp</c> guards an inverted range
/// (<c>lo &gt; hi</c>) explicitly before calling <see cref="Math.Clamp(long, long, long)"/>/
/// <see cref="Math.Clamp(double, double, double)"/>, which otherwise throws an
/// uncatchable <see cref="ArgumentException"/> for the same condition — faulting through
/// the native-throw seam (D-366's idiom) as a catchable <c>ArithmeticError</c>/<c>E5001</c>
/// rather than silently clamping to a plausible-looking number that would hide a caller
/// bug (D-370).
/// </para>
/// <para>
/// <c>float.min</c>/<c>float.max</c> defer entirely to .NET's
/// <see cref="Math.Min(double, double)"/>/<see cref="Math.Max(double, double)"/>: <c>NaN</c>
/// in either argument position propagates to the result (IEEE 754, consistent with
/// D-315's float-equality semantics), and <c>-0.0</c> sorts below <c>+0.0</c>. Neither is
/// special-cased — both are pinned and tested exactly as .NET already behaves.
/// </para>
/// </summary>
public sealed class NumericStaticsPlugin : IPrimitiveMemberPlugin {
    /// <summary>The <c>GrobError</c> leaf every fault this plugin raises through the
    /// native-throw seam reuses (Sonar S1192: one spelling, not several literal
    /// repetitions) — the same leaf D-369's <see cref="NumericMethodsPlugin"/> reuses,
    /// no new error code.</summary>
    private const string ArithmeticErrorLeaf = "ArithmeticError";

    /// <inheritdoc/>
    public string Name => "numericStatics";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) => ArgumentNullException.ThrowIfNull(registrar);

    void IPrimitiveMemberPlugin.RegisterPrimitiveMembers(IPrimitiveMemberRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterPrimitiveStatic(Signature("int.min", IntType, Param("a", IntType), Param("b", IntType)),
            new NativeFunction("int.min", 2,
                (args, _) => GrobValue.FromInt(Math.Min(args[0].AsInt(), args[1].AsInt()))));
        registrar.RegisterPrimitiveStatic(Signature("int.max", IntType, Param("a", IntType), Param("b", IntType)),
            new NativeFunction("int.max", 2,
                (args, _) => GrobValue.FromInt(Math.Max(args[0].AsInt(), args[1].AsInt()))));
        registrar.RegisterPrimitiveStatic(Signature("int.clamp", IntType,
                Param("v", IntType), Param("lo", IntType), Param("hi", IntType)),
            new NativeFunction("int.clamp", 3,
                (args, _) => GrobValue.FromInt(IntClamp(args[0].AsInt(), args[1].AsInt(), args[2].AsInt()))));

        registrar.RegisterPrimitiveStatic(Signature("float.min", FloatType, Param("a", FloatType), Param("b", FloatType)),
            new NativeFunction("float.min", 2,
                (args, _) => GrobValue.FromFloat(Math.Min(args[0].AsFloat(), args[1].AsFloat()))));
        registrar.RegisterPrimitiveStatic(Signature("float.max", FloatType, Param("a", FloatType), Param("b", FloatType)),
            new NativeFunction("float.max", 2,
                (args, _) => GrobValue.FromFloat(Math.Max(args[0].AsFloat(), args[1].AsFloat()))));
        registrar.RegisterPrimitiveStatic(Signature("float.clamp", FloatType,
                Param("v", FloatType), Param("lo", FloatType), Param("hi", FloatType)),
            new NativeFunction("float.clamp", 3,
                (args, _) => GrobValue.FromFloat(FloatClamp(args[0].AsFloat(), args[1].AsFloat(), args[2].AsFloat()))));
    }

    private static long IntClamp(long value, long lo, long hi) {
        if (lo > hi) {
            throw new NativeFaultException(ArithmeticErrorLeaf, ErrorCatalog.E5001.Code,
                $"clamp: lo {lo} is greater than hi {hi}.");
        }
        return Math.Clamp(value, lo, hi);
    }

    private static double FloatClamp(double value, double lo, double hi) {
        if (lo > hi) {
            throw new NativeFaultException(ArithmeticErrorLeaf, ErrorCatalog.E5001.Code,
                $"clamp: lo {lo} is greater than hi {hi}.");
        }
        return Math.Clamp(value, lo, hi);
    }
}
