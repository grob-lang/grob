using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Grob.Core;

namespace Grob.Stdlib;

/// <summary>
/// The compiled .NET <see cref="Regex"/> behind each <c>Regex</c> value (D-440). Keyed by
/// the value's <see cref="GrobStruct"/> instance, so a regex literal, which the compiler
/// emits as one constant struct per site, is compiled once per site per run, however
/// often its loop runs (D-432 Decision 7). The table holds its keys weakly, so it cannot
/// grow without bound as scripts build values with <c>regex.compile</c>.
/// </summary>
internal class RegexCache {
    private readonly ConditionalWeakTable<GrobStruct, Regex> _compiled = [];

    /// <summary>
    /// The compiled form of <paramref name="value"/>, built from its hidden fields on
    /// first use. Every <c>Regex</c> value was validated before it existed: a literal by
    /// the type checker (E0602) and a run-time one by <c>regex.compile</c>.
    /// </summary>
    internal Regex Get(GrobStruct value) => _compiled.GetValue(value, Build);

    /// <summary>Records the <see cref="Regex"/> <c>regex.compile</c> already built for <paramref name="value"/>.</summary>
    internal void Add(GrobStruct value, Regex compiled) => _compiled.AddOrUpdate(value, compiled);

    private static Regex Build(GrobStruct value) {
        string flags = value.GetField(RegexLayout.FlagsFieldName).AsString();
        RegexLayout.TryToTimeout(value.GetField(RegexLayout.TimeoutFieldName).AsInt(), out TimeSpan timeout);
        return new Regex(value.GetField(RegexLayout.PatternFieldName).AsString(), RegexLayout.ToOptions(flags), timeout);
    }
}
