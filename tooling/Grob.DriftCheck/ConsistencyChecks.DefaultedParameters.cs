using System.Globalization;
using System.Text.RegularExpressions;
using Grob.Compiler.Binding;
using Grob.Core;
using Grob.Runtime;
using Grob.Stdlib;

namespace Grob.DriftCheck;

public static partial class ConsistencyChecks {
    // -------------------------------------------------------------------------
    // 4.1.7 — Defaulted parameter names (R-55, D-446 Decision 3)
    // -------------------------------------------------------------------------

    private const string DefaultedParameterCheckName = "defaulted parameter names";

    /// <summary>
    /// Asserts every defaulted parameter of a registered callable is shown, by name and
    /// default, in a signature the stdlib reference or the type registry gives, and that
    /// no document defaults a parameter the code requires. A defaulted parameter's name is
    /// contract once a native can be called with it by name (D-446 Decision 3), so a
    /// rename in either place is drift.
    /// </summary>
    /// <remarks>
    /// A callable may match several documented signatures: a named-argument call example
    /// parses as one with no defaults. The forward check therefore accepts the parameter
    /// when any signature of that callable agrees, and fails only when none does.
    /// </remarks>
    /// <param name="code">Every defaulted parameter the code registers.</param>
    /// <param name="registeredCallables">Every callable the code registers, defaulted or not.</param>
    /// <param name="documents">Every signature the documents show.</param>
    /// <returns>A passing result when code and documents agree, otherwise the disagreements.</returns>
    public static CheckResult CheckDefaultedParameterNames(
        IReadOnlyList<DefaultedParameter> code,
        IReadOnlySet<string> registeredCallables,
        IReadOnlyList<DocumentedSignature> documents) {
        var failures = new List<string>();

        foreach (DefaultedParameter parameter in code) {
            string? failure = ForwardFailure(parameter, documents);
            if (failure is not null) failures.Add(failure);
        }

        failures.AddRange(documents
            .Where(sig => registeredCallables.Contains(sig.Callable))
            .SelectMany(sig => sig.Parameters
                .Where(p => p.Default is not null)
                .Where(p => !code.Any(c => c.Callable == sig.Callable && c.Name == p.Name))
                .Select(p =>
                    $"{sig.Document}:{sig.Line} shows '{sig.Callable}' parameter '{p.Name}' with a default " +
                    "but the code registers it as required.")));

        return failures.Count == 0
            ? CheckResult.Pass(DefaultedParameterCheckName)
            : CheckResult.Fail(DefaultedParameterCheckName, [.. failures.Distinct()]);
    }

    private static string? ForwardFailure(DefaultedParameter parameter, IReadOnlyList<DocumentedSignature> documents) {
        List<DocumentedSignature> signatures = [.. documents.Where(d => d.Callable == parameter.Callable)];
        if (signatures.Count == 0) {
            return $"'{parameter.Callable}' parameter '{parameter.Name}' has a default in code, but no " +
                   "signature for it appears in grob-stdlib-reference.md or grob-type-registry.md.";
        }

        List<DocumentedParameter> named = [.. signatures
            .SelectMany(s => s.Parameters)
            .Where(p => p.Name == parameter.Name)];
        if (named.Count == 0) {
            string documented = string.Join(", ", signatures
                .SelectMany(s => s.Parameters)
                .Select(p => p.Name)
                .Distinct());
            return $"'{parameter.Callable}' parameter '{parameter.Name}' has a default in code, but the " +
                   $"documents show no parameter of that name (they show: {documented}).";
        }

        List<DocumentedParameter> defaulted = [.. named.Where(p => p.Default is not null)];
        if (defaulted.Count == 0) {
            return $"'{parameter.Callable}' parameter '{parameter.Name}' has a default in code, but the " +
                   "documents show it as required.";
        }

        if (parameter.Default is not null && !defaulted.Any(p => p.Default == parameter.Default)) {
            string shown = string.Join(", ", defaulted.Select(p => p.Default).Distinct());
            return $"'{parameter.Callable}' parameter '{parameter.Name}' defaults to {parameter.Default} " +
                   $"in code but the documents show {shown}.";
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Live registry adapters (the "actual" side)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads every defaulted parameter of every callable the core catalogue registers
    /// (namespace functions, primitive methods, named-type methods and the <c>input</c>
    /// built-in), plus array <c>sort</c>'s, which has no signature and is read from the
    /// compiler's own view of it.
    /// </summary>
    /// <returns>The defaulted parameters, in registration order.</returns>
    public static IReadOnlyList<DefaultedParameter> ActualDefaultedParameters() {
        var result = new List<DefaultedParameter>();
        foreach ((string callable, FunctionSignature signature) in RegisteredSignatures()) {
            result.AddRange(signature.Parameters
                .Where(p => p.Default is not null)
                .Select(p => new DefaultedParameter(callable, p.Name, RenderDefault(p.Default!.Value))));
        }
        result.AddRange(ArrayMethodParameterNames.SortDefaulted()
            .Select(name => new DefaultedParameter("sort", name, null)));
        return result;
    }

    /// <summary>Every callable the core catalogue registers, plus the array methods that bind by name.</summary>
    /// <returns>The callables' names as the documents write them.</returns>
    public static IReadOnlySet<string> ActualRegisteredCallables() {
        var names = RegisteredSignatures().Select(s => s.Callable).ToHashSet(StringComparer.Ordinal);
        names.Add("sort");
        return names;
    }

    private static IEnumerable<(string Callable, FunctionSignature Signature)> RegisteredSignatures() {
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();

        foreach (string key in catalogue.Globals.Keys) {
            int dot = key.IndexOf('.', StringComparison.Ordinal);
            if (dot < 0) {
                if (catalogue.TryGetBuiltin(key, out NativeEntry? builtin)) yield return (key, builtin.Signature);
            } else if (catalogue.TryGetNative(key[..dot], key[(dot + 1)..], out NativeEntry? native)) {
                yield return (key, native.Signature);
            }
        }

        GrobType[] receivers = [GrobType.Int, GrobType.Float, GrobType.String, GrobType.Bool];
        foreach (GrobType receiver in receivers) {
            if (!catalogue.TryGetPrimitiveMembers(receiver, out PrimitiveMemberSurface? surface)) continue;
            foreach ((string name, PrimitiveMethodEntry method) in surface.Methods) {
                yield return (name, method.Signature);
            }
        }

        foreach (string typeName in catalogue.NamedTypeNames) {
            if (!catalogue.TryGetNamedType(typeName, out NamedTypeSurface? surface)) continue;
            foreach ((string name, NamedTypeMethodBinding binding) in surface.Methods) {
                foreach (FunctionSignature signature in binding.Signatures) yield return (name, signature);
            }
        }
    }

    // Internal so the arms no first-party default uses yet (float, bool, nil) are tested
    // directly; a plugin's signature may carry any scalar default.
    internal static string RenderDefault(GrobValue value) => value.Kind switch {
        GrobValueKind.String => $"\"{value.AsString()}\"",
        GrobValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        GrobValueKind.Float => value.AsFloat().ToString("R", CultureInfo.InvariantCulture),
        GrobValueKind.Bool => value.AsBool() ? "true" : "false",
        _ => "nil",
    };

    // -------------------------------------------------------------------------
    // Document parser (the "stated" side)
    // -------------------------------------------------------------------------

    // A signature: an optional namespace, a name, an optional generic list, then a
    // one-line parenthesised parameter list. The inner alternation is disjoint (a
    // non-parenthesis character, or one balanced pair), so it is linear.
    [GeneratedRegex(
        @"(?<![\w.])((?:[A-Za-z_]\w*\.)?[A-Za-z_]\w*)(?:<[^>\n]*>)?\(((?:[^()\n]|\([^()\n]*\))*)\)",
        RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex SignatureRegex();

    [GeneratedRegex(@"^\s*[A-Za-z_]\w*\s*:", RegexOptions.None, RegexTimeoutMs)]
    private static partial Regex NamedParameterRegex();

    /// <summary>
    /// Finds every callable signature in the given documents: a name followed by a
    /// parameter list whose first parameter is written <c>name: type</c>. A positional
    /// call example (<c>date.parse("05/04/2026", "dd/MM/yyyy")</c>) does not match.
    /// </summary>
    /// <param name="paths">The documents to read.</param>
    /// <returns>Every signature found, with document and line.</returns>
    /// <exception cref="AnchorNotFoundException">No document shows a signature.</exception>
    public static IReadOnlyList<DocumentedSignature> ParseDocumentedSignatures(params string[] paths) {
        var signatures = new List<DocumentedSignature>();
        foreach (string path in paths) {
            string document = Path.GetFileName(path);
            int lineNo = 0;
            foreach (string line in File.ReadLines(path)) {
                lineNo++;
                IEnumerable<Match> named = SignatureRegex().Matches(line)
                    .Where(m => NamedParameterRegex().IsMatch(m.Groups[2].Value));
                foreach (Match match in named) {
                    signatures.Add(new DocumentedSignature(
                        document, lineNo, match.Groups[1].Value, ParseParameters(match.Groups[2].Value)));
                }
            }
        }

        if (signatures.Count == 0) {
            throw new AnchorNotFoundException(
                string.Join(", ", paths.Select(Path.GetFileName)),
                "callable signatures of the form name(param: type, ...)");
        }
        return signatures;
    }

    private static List<DocumentedParameter> ParseParameters(string list) =>
        [.. SplitTopLevel(list).Select(ParseParameter)];

    private static DocumentedParameter ParseParameter(string text) {
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        string name = (colon < 0 ? text : text[..colon]).Trim();
        int equals = colon < 0 ? -1 : TopLevelEquals(text, colon);
        string? value = equals < 0 ? null : text[(equals + 1)..].Trim();
        return new DocumentedParameter(name, value);
    }

    private static int TopLevelEquals(string text, int from) {
        int depth = 0;
        bool quoted = false;
        for (int i = from; i < text.Length; i++) {
            char c = text[i];
            if (c == '"') quoted = !quoted;
            if (quoted) continue;
            depth += DepthChange(text, i);
            if (c == '=' && depth == 0 && (i + 1 >= text.Length || text[i + 1] != '>')) return i;
        }
        return -1;
    }

    // How the character at index changes bracket depth. The '>' of a lambda arrow '=>'
    // closes nothing, so a function type such as '(int) => int' leaves depth as it was.
    private static int DepthChange(string text, int index) => text[index] switch {
        '(' or '[' or '<' => 1,
        '>' when index > 0 && text[index - 1] == '=' => 0,
        ')' or ']' or '>' => -1,
        _ => 0,
    };

    private static IEnumerable<string> SplitTopLevel(string list) {
        int depth = 0;
        bool quoted = false;
        int start = 0;
        for (int i = 0; i < list.Length; i++) {
            char c = list[i];
            if (c == '"') quoted = !quoted;
            if (quoted) continue;
            depth += DepthChange(list, i);
            if (c == ',' && depth == 0) {
                yield return list[start..i];
                start = i + 1;
            }
        }
        yield return list[start..];
    }
}
