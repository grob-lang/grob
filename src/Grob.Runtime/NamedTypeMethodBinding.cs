using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A method of a plugin-owned named type: its signature (a bare name such as
/// <c>addDays</c>) and the binder the VM's <c>GetProperty</c> dispatch calls to close
/// the method over its receiver struct (D-356, moved into the plugins by D-426
/// Decision 3).
/// </summary>
/// <remarks>
/// A method may carry <see cref="Overloads"/>: further signatures of the same name served
/// by the same binder (D-441). The VM binds a method by name before its arguments exist,
/// so one native serves every form and tells them apart by its arguments' kinds; the
/// checker picks the signature a call matches. <see cref="NamedTypeDefinition"/> rejects
/// overloads a call could not tell apart.
/// </remarks>
public sealed record NamedTypeMethodBinding(FunctionSignature Signature, Func<GrobStruct, NativeFunction> Bind) {
    private readonly FunctionSignature[] _overloads = [];

    /// <summary>
    /// The further signatures this binder serves, beyond <see cref="Signature"/>; empty for
    /// a method with one form. The list is copied on assignment.
    /// </summary>
    public IReadOnlyList<FunctionSignature> Overloads {
        get => _overloads;
        init => _overloads = [.. value ?? throw new ArgumentNullException(nameof(value))];
    }

    /// <summary>Every signature the method accepts: <see cref="Signature"/> first, then <see cref="Overloads"/>.</summary>
    public IReadOnlyList<FunctionSignature> Signatures => [Signature, .. _overloads];
}
