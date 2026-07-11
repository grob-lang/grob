using System.Diagnostics.CodeAnalysis;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The lookup seam <see cref="ValueDisplay"/> consults before any scalar or structural
/// rendering (D-336). It answers a single question: does this value's runtime type have
/// a registered <c>toString()</c> that should render it? This ordering — registry
/// before the structural arm — is what makes a credential-bearing type (D-159) render
/// through its own guarded <c>toString()</c> rather than leaking its fields
/// (D-297: plugin and user types share the <c>Struct</c> discriminator).
/// </summary>
/// <remarks>
/// Real built-in and plugin registration into this seam is a later increment's job. The
/// increment that introduced <see cref="ValueDisplay"/> ships only the
/// <see cref="NullRegistry"/> default; no real type is wired in yet.
/// </remarks>
internal interface IValueToStringRegistry {
    /// <summary>
    /// Attempts to render <paramref name="value"/> through a registered
    /// <c>toString()</c>. Returns <c>true</c> and sets <paramref name="rendered"/> when
    /// the value's runtime type has one; <c>false</c> otherwise.
    /// </summary>
    bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered);
}
