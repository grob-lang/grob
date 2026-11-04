namespace Grob.Runtime;

/// <summary>
/// The inert description of a native's type (D-419, D-426 Decision 4): its name, its
/// positional parameters, its return type and the type parameters it declares. A
/// signature holds no delegate and no reference to an implementation (D-419 Decision 1);
/// registration binds one to a <see cref="Core.NativeFunction"/>, and on its own a
/// signature is something a manifest could serialise.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Name"/> is qualified for a namespace native (<c>"math.sqrt"</c>,
/// <c>"guid.newV5"</c>) and bare for an instance member (<c>"addDays"</c>,
/// <c>"padLeft"</c>). The runtime global, the manifest and the diagnostic text all want
/// the qualified name in one place, so the namespace is not a separate field.
/// </para>
/// <para>
/// The constructor enforces the rules the hand tables documented as conventions: at most
/// one variadic parameter and it is last; defaults form a trailing contiguous run; a
/// signature never has both a variadic tail and a default; every declared type parameter
/// is unique and referenced at least once, and every reference names a declared type
/// parameter. A class rather than a record: two signatures with equal content are still
/// two registrations, and record equality over list properties would be reference
/// equality in disguise.
/// </para>
/// </remarks>
public sealed class FunctionSignature {
    /// <summary>
    /// Creates a signature. Throws <see cref="ArgumentNullException"/> for a null
    /// argument and <see cref="ArgumentException"/>, naming the offending argument, for
    /// any shape the type remarks rule out.
    /// </summary>
    public FunctionSignature(
            string name,
            IReadOnlyList<SignatureParameter> parameters,
            SignatureType returnType,
            IReadOnlyList<TypeParameter>? typeParameters = null) {
        Identifiers.RequireQualifiedName(name, nameof(name), "A signature name");
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(returnType);

        SignatureParameter[] copiedParameters = [.. parameters];
        TypeParameter[] copiedTypeParameters = typeParameters is null ? [] : [.. typeParameters];
        ValidateParameters(copiedParameters);
        ValidateTypeParameters(copiedTypeParameters, copiedParameters, returnType);

        Name = name;
        Parameters = copiedParameters;
        ReturnType = returnType;
        TypeParameters = copiedTypeParameters;
        IsVariadic = copiedParameters.Length > 0 && copiedParameters[^1].IsVariadic;
        RequiredArgumentCount = copiedParameters.Count(p => !p.IsVariadic && !p.HasDefault);
    }

    /// <summary>The qualified or bare name. See the type remarks.</summary>
    public string Name { get; }

    /// <summary>The positional parameters, in call order.</summary>
    public IReadOnlyList<SignatureParameter> Parameters { get; }

    /// <summary>The return type; <c>nil</c> means the native returns nothing.</summary>
    public SignatureType ReturnType { get; }

    /// <summary>The declared type parameters; empty for a non-generic signature.</summary>
    public IReadOnlyList<TypeParameter> TypeParameters { get; }

    /// <summary>
    /// The number of leading parameters a call must supply: those neither defaulted nor
    /// variadic. A variadic tail's own minimum of one argument is the checker's rule and
    /// is not counted here (gate report F9).
    /// </summary>
    public int RequiredArgumentCount { get; }

    /// <summary><see langword="true"/> when the last parameter is a variable-length tail.</summary>
    public bool IsVariadic { get; }

    /// <summary>
    /// <see langword="true"/> when the signature declares a type parameter. A checker
    /// meeting such a signature throws until the <c>mapAs&lt;T&gt;</c> increment teaches
    /// it substitution (D-426 Decision 4).
    /// </summary>
    public bool IsGeneric => TypeParameters.Count > 0;

    private static void ValidateParameters(SignatureParameter[] parameters) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        bool anyVariadic = false;
        bool anyDefault = false;
        bool inDefaultRun = false;
        for (int i = 0; i < parameters.Length; i++) {
            SignatureParameter parameter = parameters[i]
                ?? throw new ArgumentException($"Parameter {i} is null.", nameof(parameters));
            if (!names.Add(parameter.Name)) {
                throw new ArgumentException($"Parameter name '{parameter.Name}' is declared twice.", nameof(parameters));
            }
            if (parameter.IsVariadic) {
                if (i != parameters.Length - 1) {
                    throw new ArgumentException(
                        $"Variadic parameter '{parameter.Name}' must be the last parameter.", nameof(parameters));
                }
                anyVariadic = true;
            }
            if (parameter.HasDefault) {
                anyDefault = true;
                inDefaultRun = true;
            } else if (inDefaultRun && !parameter.IsVariadic) {
                throw new ArgumentException(
                    $"Required parameter '{parameter.Name}' follows a defaulted one; defaults must be a trailing run.",
                    nameof(parameters));
            }
        }
        if (anyVariadic && anyDefault) {
            throw new ArgumentException(
                "A signature cannot have both a default and a variadic tail.", nameof(parameters));
        }
    }

    private static void ValidateTypeParameters(
            TypeParameter[] typeParameters, SignatureParameter[] parameters, SignatureType returnType) {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < typeParameters.Length; i++) {
            TypeParameter typeParameter = typeParameters[i]
                ?? throw new ArgumentException($"Type parameter {i} is null.", nameof(typeParameters));
            if (!declared.Add(typeParameter.Name)) {
                throw new ArgumentException(
                    $"Type parameter '{typeParameter.Name}' is declared twice.", nameof(typeParameters));
            }
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (SignatureParameter parameter in parameters) {
            RequireDeclared(parameter.Type, declared, referenced, nameof(parameters));
        }
        RequireDeclared(returnType, declared, referenced, nameof(returnType));

        foreach (string name in declared) {
            if (!referenced.Contains(name)) {
                throw new ArgumentException(
                    $"Type parameter '{name}' is declared but no parameter or the return type references it.",
                    nameof(typeParameters));
            }
        }
    }

    private static void RequireDeclared(
            SignatureType type, HashSet<string> declared, HashSet<string> referenced, string paramName) {
        string? name = type switch {
            SignatureType.TypeParameterReference reference => reference.Name,
            SignatureType.TypeParameterArray array => array.Name,
            _ => null,
        };
        if (name is null) return;
        if (!declared.Contains(name)) {
            throw new ArgumentException($"Type parameter '{name}' is referenced but not declared.", paramName);
        }
        referenced.Add(name);
    }
}
