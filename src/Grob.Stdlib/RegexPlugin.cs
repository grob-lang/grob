using System.Text.RegularExpressions;

using Grob.Core;
using Grob.Runtime;

using static Grob.Stdlib.Signatures;

namespace Grob.Stdlib;

/// <summary>
/// The <c>regex</c> module (D-089, D-095, D-432, D-440): <c>regex.compile</c>, the
/// run-time route to a <c>Regex</c>; the one-shot functions over a string pattern; and
/// the <c>Regex</c> and <c>Match</c> named types with their members and renderers. Both
/// values are <see cref="GrobStruct"/>s laid out as <see cref="RegexLayout"/> defines
/// them. A <c>Regex</c>'s compiled .NET form lives in <see cref="RegexCache"/>, built once
/// per value.
/// </summary>
/// <remarks>
/// <para>Every matching operation runs under a timeout: the value's own for a
/// <c>Regex</c>, five seconds for a one-shot function. Expiry throws <c>RuntimeError</c>
/// (E5903), naming the operation and the limit. A pattern, flag string or timeout that is
/// invalid at run time throws <c>ParseError</c> (E5702). Neither message quotes the
/// pattern (D-077).</para>
/// <para>The <c>Regex</c> methods <c>replace</c> and <c>replaceAll</c> take a replacement
/// string or a function from <c>Match</c> to string, as one native with an overload
/// (D-443). The one-shot functions take a string only.</para>
/// </remarks>
public sealed class RegexPlugin : IGrobPlugin {
    private const string ParseErrorLeaf = "ParseError";
    private const string RuntimeErrorLeaf = "RuntimeError";
    private const string PatternName = "pattern";
    private const string InputName = "input";
    private const string ReplacementName = "replacement";

    private static readonly SignatureType.Concrete RegexType = SignatureType.Named(GrobType.Struct, RegexLayout.RegexTypeName);
    private static readonly SignatureType.Concrete MatchType = SignatureType.Named(GrobType.Struct, RegexLayout.MatchTypeName);
    private static readonly SignatureType.Concrete NullableMatchType =
        SignatureType.Named(GrobType.NullableStruct, RegexLayout.MatchTypeName);
    private static readonly SignatureType.Concrete MatchArrayType = SignatureType.ArrayOf(MatchType);
    private static readonly SignatureType.FunctionShape ReplacementFunctionType = new([MatchType], StringType);

    private readonly RegexCache _cache = new();

    /// <inheritdoc/>
    public string Name => "regex";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative(Signature("regex.compile", RegexType,
                Param(PatternName, StringType),
                Optional("flags", StringType, GrobValue.FromString("")),
                Optional("timeout", IntType, GrobValue.FromInt(RegexLayout.DefaultTimeoutSeconds))),
            new NativeFunction("regex.compile", 3, (args, _) => Compile(args[0].AsString(), args[1].AsString(), args[2].AsInt())));

        RegisterOneShot(registrar, "isMatch", BoolType, (regex, input) => GrobValue.FromBool(regex.IsMatch(input)));
        RegisterOneShot(registrar, "match", NullableMatchType, FirstMatch);
        RegisterOneShot(registrar, "matchAll", MatchArrayType, AllMatches);
        RegisterOneShot(registrar, "split", StringArrayType, Split);
        RegisterOneShotReplace(registrar, "replace", count: 1);
        RegisterOneShotReplace(registrar, "replaceAll", count: -1);

        registrar.RegisterNative(Signature("regex.escape", StringType, Param("text", StringType)),
            new NativeFunction("regex.escape", 1, (args, _) => GrobValue.FromString(Regex.Escape(args[0].AsString()))));

        registrar.RegisterNamedType(BuildRegexType());
        registrar.RegisterNamedType(BuildMatchType());
    }

    // -----------------------------------------------------------------------
    // regex.compile and the one-shot functions
    // -----------------------------------------------------------------------

    private GrobValue Compile(string pattern, string flags, long timeoutSeconds) {
        if (!RegexLayout.TryNormaliseFlags(flags, out string normalised)) {
            throw new NativeFaultException(ParseErrorLeaf, ErrorCatalog.E5702.Code,
                "regex.compile: flags may contain only 'i' and 'm'.");
        }
        if (!RegexLayout.TryToTimeout(timeoutSeconds, out TimeSpan timeout)) {
            throw new NativeFaultException(ParseErrorLeaf, ErrorCatalog.E5702.Code,
                $"regex.compile: timeout must be from 0 to {RegexLayout.MaxTimeoutSeconds} seconds; 0 means no limit.");
        }
        Regex compiled = Build("regex.compile", pattern, RegexLayout.ToOptions(normalised), timeout);
        GrobStruct value = RegexLayout.NewRegex(pattern, normalised, timeoutSeconds);
        _cache.Add(value, compiled);
        return GrobValue.FromStruct(value);
    }

    private static void RegisterOneShot(
            IPluginRegistrar registrar, string member, SignatureType.Concrete returnType, Func<Regex, string, GrobValue> body) {
        string name = $"regex.{member}";
        registrar.RegisterNative(Signature(name, returnType, Param(PatternName, StringType), Param(InputName, StringType)),
            new NativeFunction(name, 2, (args, _) =>
                Bounded(name, RegexLayout.DefaultTimeoutSeconds, () => body(OneShot(name, args[0].AsString()), args[1].AsString()))));
    }

    private static void RegisterOneShotReplace(IPluginRegistrar registrar, string member, int count) {
        string name = $"regex.{member}";
        registrar.RegisterNative(Signature(name, StringType,
                Param(PatternName, StringType), Param(InputName, StringType), Param(ReplacementName, StringType)),
            new NativeFunction(name, 3, (args, _) => Bounded(name, RegexLayout.DefaultTimeoutSeconds, () =>
                GrobValue.FromString(OneShot(name, args[0].AsString()).Replace(args[1].AsString(), args[2].AsString(), count)))));
    }

    private static Regex OneShot(string operation, string pattern) =>
        Build(operation, pattern, RegexLayout.ToOptions(""), TimeSpan.FromSeconds(RegexLayout.DefaultTimeoutSeconds));

    private static Regex Build(string operation, string pattern, RegexOptions options, TimeSpan timeout) {
        try {
            return new Regex(pattern, options, timeout);
        } catch (RegexParseException ex) {
            throw new NativeFaultException(ParseErrorLeaf, ErrorCatalog.E5702.Code,
                $"{operation}: invalid pattern at offset {ex.Offset}: {RegexLayout.DescribeParseError(ex)}.");
        }
    }

    /// <summary>
    /// Runs a matching operation, turning .NET's timeout into <c>RuntimeError</c> (D-432
    /// Decision 8). The message names the operation and the limit, never the pattern.
    /// </summary>
    private static GrobValue Bounded(string operation, long timeoutSeconds, Func<GrobValue> body) {
        try {
            return body();
        } catch (RegexMatchTimeoutException) {
            throw new NativeFaultException(RuntimeErrorLeaf, ErrorCatalog.E5903.Code,
                $"{operation}: matching exceeded the {timeoutSeconds}-second limit.");
        }
    }

    // -----------------------------------------------------------------------
    // The Regex named type
    // -----------------------------------------------------------------------

    private NamedTypeDefinition BuildRegexType() => new(
        RegexLayout.RegexTypeName,
        [
            new NamedTypePropertyBinding(new PropertySignature(PatternName, StringType),
                r => r.GetField(RegexLayout.PatternFieldName)),
            new NamedTypePropertyBinding(new PropertySignature("flags", StringType),
                r => r.GetField(RegexLayout.FlagsFieldName)),
            new NamedTypePropertyBinding(new PropertySignature("timeout", IntType),
                r => r.GetField(RegexLayout.TimeoutFieldName)),
        ],
        [
            InputMethod("isMatch", BoolType, (regex, input) => GrobValue.FromBool(regex.IsMatch(input))),
            InputMethod("match", NullableMatchType, FirstMatch),
            InputMethod("matchAll", MatchArrayType, AllMatches),
            InputMethod("split", StringArrayType, Split),
            ReplaceMethod("replace", count: 1),
            ReplaceMethod("replaceAll", count: -1),
        ],
        v => RenderRegex(v.AsStruct()));

    private NamedTypeMethodBinding InputMethod(string name, SignatureType.Concrete returnType, Func<Regex, string, GrobValue> body) =>
        new(Signature(name, returnType, Param(InputName, StringType)),
            receiver => new NativeFunction($"Regex.{name}", 1, (args, _) =>
                Bounded($"Regex.{name}", TimeoutOf(receiver), () => body(_cache.Get(receiver), args[0].AsString()))));

    /// <summary>
    /// <c>replace</c> or <c>replaceAll</c>: the string form, with the function form as its
    /// overload (D-443). One native serves both and tells them apart by the second
    /// argument's kind; the checker has already proved it is one or the other.
    /// </summary>
    private NamedTypeMethodBinding ReplaceMethod(string name, int count) {
        string operation = $"Regex.{name}";
        return new NamedTypeMethodBinding(
            Signature(name, StringType, Param(InputName, StringType), Param(ReplacementName, StringType)),
            receiver => new NativeFunction(operation, 2, (args, invoker) => Bounded(operation, TimeoutOf(receiver), () => {
                Regex regex = _cache.Get(receiver);
                string input = args[0].AsString();
                if (args[1].Kind != GrobValueKind.Function) {
                    return GrobValue.FromString(regex.Replace(input, args[1].AsString(), count));
                }
                GrobValue fn = args[1];
                return ReplaceWith(operation, regex, input, count, match => invoker.Invoke(fn, [match]));
            }))) {
            Overloads = [Signature(name, StringType, Param(InputName, StringType), Param("fn", ReplacementFunctionType))],
        };
    }

    /// <summary>
    /// Replaces up to <paramref name="count"/> matches (<c>-1</c> for all) with what
    /// <paramref name="evaluate"/> returns for each <c>Match</c> (D-432 Decision 6). A
    /// result that is not a string — reachable only through a function whose return the
    /// checker could not type — is <c>RuntimeError</c> (E5903), naming
    /// <paramref name="operation"/>. An exception from <paramref name="evaluate"/>, a Grob
    /// <c>throw</c> included, propagates unchanged.
    /// </summary>
    internal static GrobValue ReplaceWith(string operation, Regex regex, string input, int count, Func<GrobValue, GrobValue> evaluate) =>
        GrobValue.FromString(regex.Replace(input, match => {
            GrobValue replacement = evaluate(GrobValue.FromStruct(ToMatch(regex, match)));
            if (replacement.Kind != GrobValueKind.String) {
                throw new NativeFaultException(RuntimeErrorLeaf, ErrorCatalog.E5903.Code,
                    $"{operation}: the replacement function must return a string, not {replacement.Kind.ToString().ToLowerInvariant()}.");
            }
            return replacement.AsString();
        }, count));

    private static long TimeoutOf(GrobStruct regex) => regex.GetField(RegexLayout.TimeoutFieldName).AsInt();

    private static string RenderRegex(GrobStruct regex) =>
        $"/{regex.GetField(RegexLayout.PatternFieldName).AsString()}/{regex.GetField(RegexLayout.FlagsFieldName).AsString()}";

    // -----------------------------------------------------------------------
    // Matching, shared by the one-shot functions and the Regex methods
    // -----------------------------------------------------------------------

    private static GrobValue FirstMatch(Regex regex, string input) {
        Match match = regex.Match(input);
        return match.Success ? GrobValue.FromStruct(ToMatch(regex, match)) : GrobValue.Nil;
    }

    private static GrobValue AllMatches(Regex regex, string input) =>
        GrobValue.FromArray(new GrobArray(
            regex.Matches(input).Select(m => GrobValue.FromStruct(ToMatch(regex, m)))));

    private static GrobValue Split(Regex regex, string input) =>
        GrobValue.FromArray(new GrobArray(regex.Split(input).Select(GrobValue.FromString)));

    // -----------------------------------------------------------------------
    // The Match named type
    // -----------------------------------------------------------------------

    private static GrobStruct ToMatch(Regex regex, Match match) {
        // By group number, not by enumerating match.Groups: the enumerator drops an
        // explicitly numbered group such as (?<2>a). GetGroupNumbers is ascending, so
        // groups lists every defined group in number order without gaps (D-440).
        GrobArray groups = new(regex.GetGroupNumbers().Select(n => GrobValue.FromString(match.Groups[n].Value)));
        GrobMap named = new();
        foreach (string name in regex.GetGroupNames().Where(n => !int.TryParse(n, out _))) {
            Group group = match.Groups[name];
            if (group.Success) named.Set(name, GrobValue.FromString(group.Value));
        }
        return new GrobStruct(RegexLayout.MatchTypeName, [
            new(RegexLayout.ValueFieldName, GrobValue.FromString(match.Value)),
            new(RegexLayout.IndexFieldName, GrobValue.FromInt(match.Index)),
            new(RegexLayout.LengthFieldName, GrobValue.FromInt(match.Length)),
            new(RegexLayout.GroupsFieldName, GrobValue.FromArray(groups)),
            new(RegexLayout.NamedGroupsFieldName, GrobValue.FromMap(named)),
        ]);
    }

    private static NamedTypeDefinition BuildMatchType() => new(
        RegexLayout.MatchTypeName,
        [
            new NamedTypePropertyBinding(new PropertySignature("value", StringType),
                m => m.GetField(RegexLayout.ValueFieldName)),
            new NamedTypePropertyBinding(new PropertySignature("index", IntType),
                m => m.GetField(RegexLayout.IndexFieldName)),
            new NamedTypePropertyBinding(new PropertySignature("length", IntType),
                m => m.GetField(RegexLayout.LengthFieldName)),
            // A copy, so a script that changes the array it was given cannot change the match.
            new NamedTypePropertyBinding(new PropertySignature("groups", StringArrayType),
                m => GrobValue.FromArray(new GrobArray(m.GetField(RegexLayout.GroupsFieldName).AsArray().Elements))),
        ],
        [
            new NamedTypeMethodBinding(Signature("group", NullableStringType, Param("name", StringType)),
                receiver => new NativeFunction("Match.group", 1, (args, _) =>
                    receiver.GetField(RegexLayout.NamedGroupsFieldName).AsMap().TryGetValue(args[0].AsString(), out GrobValue text)
                        ? text
                        : GrobValue.Nil)),
        ],
        v => v.AsStruct().GetField(RegexLayout.ValueFieldName).AsString());
}
