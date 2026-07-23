using System.Globalization;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>int</c>/<c>float</c>/<c>bool</c> instance-method surfaces' runtime natives
/// (D-066: primitive method-call syntax is compile-time sugar, rewritten by the compiler
/// to a call against one of these, receiver injected as arg[0]). Registers exactly the
/// qualified names listed in the compile-time twins, <c>PrimitiveMemberRegistry.Int</c>/
/// <c>.Float</c>/<c>.Bool</c> in <c>Grob.Core</c> (D-369). Pure — no capability injection,
/// mirroring <see cref="StringMethodsPlugin"/>.
/// <para>
/// <c>int.abs()</c> and <c>float.toInt()</c> fault via a plain <c>checked(...)</c> C# cast
/// or negation rather than a manual <see cref="NativeFaultException"/> guard ("Pattern A"):
/// the resulting <see cref="OverflowException"/> propagates through the VM's per-instruction
/// dispatch loop unchanged (the native-call inner <c>try</c> only catches
/// <see cref="NativeFaultException"/>) to the same outer <c>catch (OverflowException)</c>
/// every other <c>checked(...)</c> opcode (<c>NegateInt</c> et al.) already relies on,
/// converting it to the existing <c>E5001</c>/<c>ArithmeticError</c> — no new error code.
/// </para>
/// <para>
/// <c>round()</c>/<c>roundTo()</c> use <see cref="MidpointRounding.AwayFromZero"/> — a
/// fresh pinned decision (D-369) with no prior codebase precedent, chosen over .NET's
/// banker's-rounding default as the "half rounds away from zero" convention most scripting
/// users expect.
/// </para>
/// </summary>
public sealed class NumericMethodsPlugin : IGrobPlugin {
    /// <summary>The <c>GrobError</c> leaf every fault this plugin raises through the
    /// native-throw seam reuses (Sonar S1192: one spelling, not several literal
    /// repetitions) — reused rather than a new leaf, matching D-369's "no new error
    /// code" scope.</summary>
    private const string ArithmeticErrorLeaf = "ArithmeticError";

    /// <summary>
    /// The valid <c>decimals</c> range <see cref="Math.Round(double, int, MidpointRounding)"/>
    /// itself enforces (0 to 15 inclusive) — guarded explicitly here rather than left to that
    /// method's own <see cref="ArgumentOutOfRangeException"/>, which the VM's native-call seam
    /// does not translate to a catchable <c>GrobError</c> (D-353's "fails well" contract,
    /// applied to a range class Pattern A's <c>checked(...)</c> cast alone cannot cover: this
    /// is a library-level argument-validity range, not an overflow).
    /// </summary>
    private const int MinRoundToDecimals = 0;
    private const int MaxRoundToDecimals = 15;

    /// <inheritdoc/>
    public string Name => "numeric";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        RegisterIntNatives(registrar);
        RegisterFloatNatives(registrar);

        registrar.RegisterNative("bool.toString", new NativeFunction("bool.toString", 1,
            (args, _) => GrobValue.FromString(args[0].AsBool() ? "true" : "false")));
    }

    // -----------------------------------------------------------------------
    // int — toString/toFloat/abs/format(pattern).
    // -----------------------------------------------------------------------

    private static void RegisterIntNatives(IPluginRegistrar registrar) {
        registrar.RegisterNative("int.toString", new NativeFunction("int.toString", 1,
            (args, _) => GrobValue.FromString(args[0].AsInt().ToString(CultureInfo.InvariantCulture))));
        registrar.RegisterNative("int.toFloat", new NativeFunction("int.toFloat", 1,
            (args, _) => GrobValue.FromFloat(args[0].AsInt())));
        registrar.RegisterNative("int.abs", new NativeFunction("int.abs", 1,
            (args, _) => GrobValue.FromInt(IntAbs(args[0].AsInt()))));
        registrar.RegisterNative("int.format", new NativeFunction("int.format", 2,
            (args, _) => GrobValue.FromString(Format(args[0].AsInt(), args[1].AsString()))));
    }

    /// <summary>Pattern A: <c>checked(...)</c> — <c>-long.MinValue</c> is not representable,
    /// overflows exactly as <c>NegateInt</c>'s own <c>checked(-a)</c> already detects.</summary>
    private static long IntAbs(long value) => checked(value < 0 ? -value : value);

    // -----------------------------------------------------------------------
    // float — toString/toInt/round/roundTo(decimals)/floor/ceil/abs/format(pattern).
    // -----------------------------------------------------------------------

    private static void RegisterFloatNatives(IPluginRegistrar registrar) {
        registrar.RegisterNative("float.toString", new NativeFunction("float.toString", 1,
            (args, _) => GrobValue.FromString(ValueDisplay.FormatFloat(args[0].AsFloat()))));
        registrar.RegisterNative("float.toInt", new NativeFunction("float.toInt", 1,
            (args, _) => GrobValue.FromInt(checked((long)args[0].AsFloat()))));
        registrar.RegisterNative("float.round", new NativeFunction("float.round", 1,
            (args, _) => GrobValue.FromInt(
                checked((long)Math.Round(args[0].AsFloat(), MidpointRounding.AwayFromZero)))));
        registrar.RegisterNative("float.roundTo", new NativeFunction("float.roundTo", 2,
            (args, _) => GrobValue.FromFloat(RoundTo(args[0].AsFloat(), args[1].AsInt()))));
        registrar.RegisterNative("float.floor", new NativeFunction("float.floor", 1,
            (args, _) => GrobValue.FromInt(checked((long)Math.Floor(args[0].AsFloat())))));
        registrar.RegisterNative("float.ceil", new NativeFunction("float.ceil", 1,
            (args, _) => GrobValue.FromInt(checked((long)Math.Ceiling(args[0].AsFloat())))));
        registrar.RegisterNative("float.abs", new NativeFunction("float.abs", 1,
            (args, _) => GrobValue.FromFloat(Math.Abs(args[0].AsFloat()))));
        registrar.RegisterNative("float.format", new NativeFunction("float.format", 2,
            (args, _) => GrobValue.FromString(Format(args[0].AsFloat(), args[1].AsString()))));
    }

    /// <summary>
    /// Applies a numeric format <paramref name="pattern"/> under the invariant culture,
    /// translating the <see cref="FormatException"/> that <c>long</c>/<c>double</c>
    /// <c>ToString</c> raise on a malformed specifier into a catchable
    /// <c>E5001</c>/<c>ArithmeticError</c> — the pattern is user-supplied data that reaches
    /// the native seam, so a host exception must not escape it (D-353's "fails well"
    /// contract, the same range-guard reasoning <see cref="RoundTo"/> applies, reusing the
    /// existing code rather than minting a new one). <paramref name="value"/> is boxed to
    /// <see cref="IFormattable"/> so <c>int.format</c> and <c>float.format</c> share one
    /// guard; neither is a hot path.
    /// </summary>
    private static string Format(IFormattable value, string pattern) {
        try {
            return value.ToString(pattern, CultureInfo.InvariantCulture);
        } catch (FormatException) {
            throw new NativeFaultException(ArithmeticErrorLeaf, ErrorCatalog.E5001.Code,
                $"format: '{pattern}' is not a valid numeric format string.");
        }
    }

    private static double RoundTo(double value, long decimals) {
        if (decimals < MinRoundToDecimals || decimals > MaxRoundToDecimals) {
            throw new NativeFaultException(ArithmeticErrorLeaf, ErrorCatalog.E5001.Code,
                $"roundTo: decimals {decimals} is outside the supported range " +
                $"{MinRoundToDecimals}-{MaxRoundToDecimals}.");
        }
        return Math.Round(value, (int)decimals, MidpointRounding.AwayFromZero);
    }
}
