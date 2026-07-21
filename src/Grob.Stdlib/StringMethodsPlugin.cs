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
        if (start < 0 || length < 0 || start + length > s.Length) {
            throw new NativeFaultException("IndexError", ErrorCatalog.E5101.Code,
                $"substring: start {start} and length {length} are out of range for a string of length {s.Length}.");
        }
        return GrobValue.FromString(s.Substring((int)start, (int)length));
    }

    private static GrobValue Repeat(GrobValue receiver, GrobValue countArg) {
        string s = receiver.AsString();
        long count = countArg.AsInt();
        if (count <= 0) return GrobValue.FromString(string.Empty);
        var builder = new StringBuilder(checked((int)(s.Length * count)));
        for (long i = 0; i < count; i++) builder.Append(s);
        return GrobValue.FromString(builder.ToString());
    }

    private static GrobValue Left(GrobValue receiver, GrobValue nArg) {
        string s = receiver.AsString();
        long n = nArg.AsInt();
        if (n < 0 || n > s.Length) {
            throw new NativeFaultException("IndexError", ErrorCatalog.E5101.Code,
                $"left: {n} exceeds string length {s.Length}.");
        }
        return GrobValue.FromString(s[..(int)n]);
    }

    private static GrobValue Right(GrobValue receiver, GrobValue nArg) {
        string s = receiver.AsString();
        long n = nArg.AsInt();
        if (n < 0 || n > s.Length) {
            throw new NativeFaultException("IndexError", ErrorCatalog.E5101.Code,
                $"right: {n} exceeds string length {s.Length}.");
        }
        return GrobValue.FromString(s[(s.Length - (int)n)..]);
    }
}
