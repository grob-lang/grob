using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// What a plugin registers for a named type it owns (<c>date</c>, <c>guid</c>, and every
/// Sprint 9C and Sprint 11 type): the type's name, its property and method bindings and
/// the renderer <c>print()</c> and string interpolation use (D-336). Handed to
/// <see cref="IPluginRegistrar.RegisterNamedType"/>; the definition validates itself so
/// the catalogue never holds two members of one name or a method with a qualified name.
/// </summary>
public sealed record NamedTypeDefinition {
    /// <summary>
    /// Creates a definition. Throws <see cref="ArgumentNullException"/> for a null
    /// argument and <see cref="ArgumentException"/> when <paramref name="name"/> is not an
    /// identifier, a binding or one of its parts is null, a method's signature name is
    /// not bare, or two members share a name.
    /// </summary>
    public NamedTypeDefinition(
            string name,
            IReadOnlyList<NamedTypePropertyBinding> properties,
            IReadOnlyList<NamedTypeMethodBinding> methods,
            Func<GrobValue, string> render) {
        Identifiers.RequireIdentifier(name, nameof(name), "A named type name");
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(methods);
        ArgumentNullException.ThrowIfNull(render);

        NamedTypePropertyBinding[] copiedProperties = [.. properties];
        NamedTypeMethodBinding[] copiedMethods = [.. methods];
        var names = new HashSet<string>(StringComparer.Ordinal);
        ValidateProperties(copiedProperties, names);
        ValidateMethods(copiedMethods, names);

        Name = name;
        Properties = copiedProperties;
        Methods = copiedMethods;
        Render = render;
    }

    /// <summary>The type's name, as a type annotation spells it.</summary>
    public string Name { get; }

    /// <summary>The property bindings, in declaration order.</summary>
    public IReadOnlyList<NamedTypePropertyBinding> Properties { get; }

    /// <summary>The method bindings, in declaration order.</summary>
    public IReadOnlyList<NamedTypeMethodBinding> Methods { get; }

    /// <summary>Renders a value of this type for <c>print()</c> and interpolation.</summary>
    public Func<GrobValue, string> Render { get; }

    private static void ValidateProperties(NamedTypePropertyBinding[] properties, HashSet<string> names) {
        for (int i = 0; i < properties.Length; i++) {
            NamedTypePropertyBinding binding = properties[i]
                ?? throw new ArgumentException($"Property binding {i} is null.", nameof(properties));
            if (binding.Signature is null) {
                throw new ArgumentException($"Property binding {i} has no signature.", nameof(properties));
            }
            if (binding.Get is null) {
                throw new ArgumentException($"Property '{binding.Signature.Name}' has no getter.", nameof(properties));
            }
            if (!names.Add(binding.Signature.Name)) {
                throw new ArgumentException(
                    $"Member '{binding.Signature.Name}' is declared twice.", nameof(properties));
            }
        }
    }

    private static void ValidateMethods(NamedTypeMethodBinding[] methods, HashSet<string> names) {
        for (int i = 0; i < methods.Length; i++) {
            NamedTypeMethodBinding binding = methods[i]
                ?? throw new ArgumentException($"Method binding {i} is null.", nameof(methods));
            if (binding.Signature is null) {
                throw new ArgumentException($"Method binding {i} has no signature.", nameof(methods));
            }
            string memberName = binding.Signature.Name;
            if (binding.Bind is null) {
                throw new ArgumentException($"Method '{memberName}' has no binder.", nameof(methods));
            }
            if (memberName.Contains('.')) {
                throw new ArgumentException(
                    $"Method '{memberName}' must have a bare name; an instance member is not qualified.", nameof(methods));
            }
            if (!names.Add(memberName)) {
                throw new ArgumentException($"Member '{memberName}' is declared twice.", nameof(methods));
            }
        }
    }
}
