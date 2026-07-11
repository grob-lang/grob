using System.Diagnostics.CodeAnalysis;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The default <see cref="IValueToStringRegistry"/>: nothing is registered, so every
/// lookup misses and <see cref="ValueDisplay"/> falls through to its scalar and
/// structural rendering. This keeps the scalar and composite paths free of any registry
/// dependency (D-336).
/// </summary>
internal sealed class NullRegistry : IValueToStringRegistry {
    /// <summary>The shared, stateless instance.</summary>
    public static readonly NullRegistry Instance = new();

    private NullRegistry() {
    }

    /// <inheritdoc/>
    public bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) {
        rendered = null;
        return false;
    }
}
