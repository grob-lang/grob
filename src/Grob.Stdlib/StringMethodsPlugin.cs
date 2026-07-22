using System.Globalization;
using System.Text;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>string</c> instance-method/property surface's runtime natives (D-066: primitive
/// method-call syntax is compile-time sugar, rewritten by the compiler to a call against
/// one of these, receiver injected as arg[0]). Registers exactly the qualified names
/// listed in the compile-time twin, <c>PrimitiveMemberRegistry.String</c> in
/// <c>Grob.Core</c>. Pure — no capability injection. Comparisons and case changes use
/// <see cref="StringComparison.Ordinal"/>/invariant-culture forms throughout, never the
/// current-culture default, so behaviour does not vary by host locale (mirrors
/// <c>GuidPlugin</c>'s existing invariant-culture convention).
/// </summary>
public sealed class StringMethodsPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "string";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("string.length", new NativeFunction("string.length", 1,
            (args, _) => GrobValue.FromInt(args[0].AsString().Length)));
        registrar.RegisterNative("string.isEmpty", new NativeFunction("string.isEmpty", 1,
            (args, _) => GrobValue.FromBool(args[0].AsString().Length == 0)));

        registrar.RegisterNative("string.toInt", new NativeFunction("string.toInt", 1, (args, _) => ToInt(args[0])));
        registrar.RegisterNative("string.toFloat", new NativeFunction("string.toFloat", 1, (args, _) => ToFloat(args[0])));

        registrar.RegisterNative("string.trim", new NativeFunction("string.trim", 1,
            (args, _) => GrobValue.FromString(args[0].AsString().Trim())));
        registrar.RegisterNative("string.trimStart", new NativeFunction("string.trimStart", 1,
            (args, _) => GrobValue.FromString(args[0].AsString().TrimStart())));
        registrar.RegisterNative("string.trimEnd", new NativeFunction("string.trimEnd", 1,
            (args, _) => GrobValue.FromString(args[0].AsString().TrimEnd())));
        registrar.RegisterNative("string.upper", new NativeFunction("string.upper", 1,
            (args, _) => GrobValue.FromString(args[0].AsString().ToUpperInvariant())));
        registrar.RegisterNative("string.lower", new NativeFunction("string.lower", 1,
            (args, _) => GrobValue.FromString(args[0].AsString().ToLowerInvariant())));

        registrar.RegisterNative("string.split", new NativeFunction("string.split", 2, (args, _) => {
            string[] parts = args[0].AsString().Split(args[1].AsString(), StringSplitOptions.None);
            return GrobValue.FromArray(new GrobArray([.. parts.Select(GrobValue.FromString)]));
        }));
        registrar.RegisterNative("string.contains", new NativeFunction("string.contains", 2,
            (args, _) => GrobValue.FromBool(args[0].AsString().Contains(args[1].AsString(), StringComparison.Ordinal))));
        registrar.RegisterNative("string.startsWith", new NativeFunction("string.startsWith", 2,
            (args, _) => GrobValue.FromBool(args[0].AsString().StartsWith(args[1].AsString(), StringComparison.Ordinal))));
        registrar.RegisterNative("string.endsWith", new NativeFunction("string.endsWith", 2,
            (args, _) => GrobValue.FromBool(args[0].AsString().EndsWith(args[1].AsString(), StringComparison.Ordinal))));
        registrar.RegisterNative("string.replace", new NativeFunction("string.replace", 3, (args, _) =>
            GrobValue.FromString(args[0].AsString().Replace(args[1].AsString(), args[2].AsString(), StringComparison.Ordinal))));

        registrar.RegisterNative("string.indexOf", new NativeFunction("string.indexOf", 2,
            (args, _) => GrobValue.FromInt(args[0].AsString().IndexOf(args[1].AsString(), StringComparison.Ordinal))));
        registrar.RegisterNative("string.lastIndexOf", new NativeFunction("string.lastIndexOf", 2,
            (args, _) => GrobValue.FromInt(args[0].AsString().LastIndexOf(args[1].AsString(), StringComparison.Ordinal))));

        registrar.RegisterNative("string.substring", new NativeFunction("string.substring", 3,
            (args, _) => Substring(args[0], args[1], args[2])));
        registrar.RegisterNative("string.repeat", new NativeFunction("string.repeat", 2,
            (args, _) => Repeat(args[0], args[1])));
        registrar.RegisterNative("string.left", new NativeFunction("string.left", 2, (args, _) => Left(args[0], args[1])));
        registrar.RegisterNative("string.right", new NativeFunction("string.right", 2, (args, _) => Right(args[0], args[1])));

        registrar.RegisterNative("string.toString", new NativeFunction("string.toString", 1, (args, _) => args[0]));

        registrar.RegisterNative("string.padLeft", new NativeFunction("string.padLeft", 3,
            (args, _) => PadLeft(args[0], args[1], args[2])));
        registrar.RegisterNative("string.padRight", new NativeFunction("string.padRight", 3,
            (args, _) => PadRight(args[0], args[1], args[2])));
        registrar.RegisterNative("string.truncate", new NativeFunction("string.truncate", 3,
            (args, _) => Truncate(args[0], args[1], args[2])));
    }

    /// <summary>Reduces a pad-char argument to the single <see cref="char"/> .NET's pad overload takes — the
    /// first character when supplied, or a space when the argument is empty (D-365's pinned edge case: the
    /// parameter is conceptually a single character but typed <c>string</c>, there being no <c>char</c>
    /// primitive in Grob).</summary>
    private static char PadCharacter(GrobValue charArg) {
        string s = charArg.AsString();
        return s.Length == 0 ? ' ' : s[0];
    }

    /// <summary>The ceiling on any single native-seam allocation driven by a user-supplied
    /// count/width/size argument (D-366). Chosen well below <see cref="int.MaxValue"/>: the
    /// cast-safety boundary alone (D-365) only rejects values that cannot fit an <c>int</c>
    /// at all, leaving a valid-but-enormous width or count free to ask the CLR to allocate
    /// an unreasonable buffer and throw an uncoded, uncatchable <see cref="OutOfMemoryException"/>.
    /// 10,000,000 characters (~20 MB as UTF-16) comfortably covers any real script's output
    /// while still being cheap for an adversarial input to exceed.</summary>
    private const int MaxAllocationLength = 10_000_000;

    /// <summary>The <c>GrobError</c> leaf every range-bound <c>string</c> member raises through
    /// the native-throw seam (Sonar S1192: one spelling, not five literal repetitions).</summary>
    private const string IndexErrorLeaf = "IndexError";

    private static GrobValue PadLeft(GrobValue receiver, GrobValue widthArg, GrobValue charArg) {
        string s = receiver.AsString();
        long width = widthArg.AsInt();
        if (width <= s.Length) return GrobValue.FromString(s);
        RejectOversizedWidth("padLeft", width);
        return GrobValue.FromString(s.PadLeft((int)width, PadCharacter(charArg)));
    }

    private static GrobValue PadRight(GrobValue receiver, GrobValue widthArg, GrobValue charArg) {
        string s = receiver.AsString();
        long width = widthArg.AsInt();
        if (width <= s.Length) return GrobValue.FromString(s);
        RejectOversizedWidth("padRight", width);
        return GrobValue.FromString(s.PadRight((int)width, PadCharacter(charArg)));
    }

    /// <summary>Rejects a pad <c>width</c> above <see cref="MaxAllocationLength"/> — both the
    /// case that would not fit a 32-bit <see cref="string.PadLeft(int,char)"/> total-width
    /// argument at all (the unchecked cast of a <c>long</c> above <see cref="int.MaxValue"/>
    /// wraps to a negative value, whereupon .NET's pad overloads throw an uncoded CLR fault)
    /// and the case that fits an <c>int</c> comfortably but would still ask .NET to allocate
    /// an unreasonable buffer (D-366). Routing either through <see cref="NativeFaultException"/>
    /// surfaces the same <c>IndexError</c>/<c>E5101</c> the other range-bound string members
    /// raise.</summary>
    private static void RejectOversizedWidth(string method, long width) {
        if (width > MaxAllocationLength) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"{method}: width {width} exceeds the maximum supported value {MaxAllocationLength}.");
        }
    }

    /// <summary>
    /// Truncates <paramref name="receiver"/> to <paramref name="maxLengthArg"/> characters
    /// total, appending <paramref name="suffixArg"/> when truncation is needed (D-365's
    /// pinned semantics: <c>maxLength</c> is the total result length including the suffix,
    /// so the result never exceeds it). When <c>maxLength</c> does not exceed the suffix's
    /// own length — including a negative <c>maxLength</c> — the result is the suffix itself
    /// clamped to <c>maxLength</c> characters (empty when <c>maxLength &lt;= 0</c>).
    /// </summary>
    private static GrobValue Truncate(GrobValue receiver, GrobValue maxLengthArg, GrobValue suffixArg) {
        string s = receiver.AsString();
        string suffix = suffixArg.AsString();
        long maxLength = maxLengthArg.AsInt();
        if (maxLength >= s.Length) return GrobValue.FromString(s);
        if (maxLength <= suffix.Length) {
            int clamped = (int)Math.Clamp(maxLength, 0, suffix.Length);
            return GrobValue.FromString(suffix[..clamped]);
        }
        return GrobValue.FromString(s[..(int)(maxLength - suffix.Length)] + suffix);
    }

    private static GrobValue ToInt(GrobValue receiver) =>
        long.TryParse(receiver.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? GrobValue.FromInt(parsed)
            : GrobValue.Nil;

    private static GrobValue ToFloat(GrobValue receiver) =>
        double.TryParse(receiver.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? GrobValue.FromFloat(parsed)
            : GrobValue.Nil;

    private static GrobValue Substring(GrobValue receiver, GrobValue startArg, GrobValue lengthArg) {
        string s = receiver.AsString();
        long start = startArg.AsInt();
        long length = lengthArg.AsInt();
        // Compare start against Length before subtracting so `start + length` cannot wrap:
        // long.MaxValue + 1 would overflow to a negative value and slip past an additive guard.
        if (start < 0 || length < 0 || start > s.Length || length > s.Length - start) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"substring: start {start} and length {length} are out of range for a string of length {s.Length}.");
        }
        return GrobValue.FromString(s.Substring((int)start, (int)length));
    }

    private static GrobValue Repeat(GrobValue receiver, GrobValue countArg) {
        string s = receiver.AsString();
        long count = countArg.AsInt();
        if (count <= 0) return GrobValue.FromString(string.Empty);
        RejectOversizedRepeat(s.Length, count);
        var builder = new StringBuilder(checked((int)(s.Length * count)));
        for (long i = 0; i < count; i++) builder.Append(s);
        return GrobValue.FromString(builder.ToString());
    }

    /// <summary>Rejects a <c>repeat</c> whose result length would exceed
    /// <see cref="MaxAllocationLength"/> (D-366) — checked via division rather than computing
    /// <c>length * count</c> directly, so the guard itself cannot overflow ahead of the
    /// existing <c>checked(...)</c> cast it guards. Mirrors <see cref="RejectOversizedWidth"/>'s
    /// treatment of the same allocation-ceiling class for <c>padLeft</c>/<c>padRight</c>.</summary>
    private static void RejectOversizedRepeat(int length, long count) {
        if (length > 0 && count > MaxAllocationLength / length) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"repeat: result length exceeds the maximum supported value {MaxAllocationLength}.");
        }
    }

    private static GrobValue Left(GrobValue receiver, GrobValue nArg) {
        string s = receiver.AsString();
        long n = nArg.AsInt();
        if (n < 0 || n > s.Length) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"left: {n} exceeds string length {s.Length}.");
        }
        return GrobValue.FromString(s[..(int)n]);
    }

    private static GrobValue Right(GrobValue receiver, GrobValue nArg) {
        string s = receiver.AsString();
        long n = nArg.AsInt();
        if (n < 0 || n > s.Length) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"right: {n} exceeds string length {s.Length}.");
        }
        return GrobValue.FromString(s[(s.Length - (int)n)..]);
    }
}
