using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// One positional parameter of a native signature (D-426 Decision 4). The four shapes
/// the hand tables accreted collapse onto this one record: a variadic tail is
/// <see cref="IsVariadic"/> on a signature's last parameter, whose <see cref="Type"/>
/// is then the element type of the tail; a trailing default is <see cref="Default"/>;
/// a nominal struct parameter and a named-type self parameter are both a
/// <see cref="SignatureType.Concrete"/> with a named type name.
/// </summary>
/// <remarks>
/// Named here <c>SignatureParameter</c>, not <c>Parameter</c>, because
/// <c>Grob.Compiler.Ast.Parameter</c> already exists and the checker — which reads both —
/// would otherwise disambiguate at every site. A variadic tail requires at least one
/// argument at the call site; that minimum is the checker's rule and is not a field
/// here (gate report F9).
/// </remarks>
public sealed record SignatureParameter {
    private const string DefaultParamName = "defaultValue";

    /// <summary>
    /// Creates a parameter. Throws <see cref="ArgumentException"/> when
    /// <paramref name="name"/> is not an identifier, when <paramref name="type"/> is
    /// <c>nil</c>, when a variadic parameter carries a default, when a type-parameter-typed
    /// parameter carries a default, or when <paramref name="defaultValue"/> is not a scalar
    /// of the parameter's own type (<c>nil</c> is permitted only on a nullable type).
    /// </summary>
    public SignatureParameter(string name, SignatureType type, bool isVariadic = false, GrobValue? defaultValue = null) {
        Identifiers.RequireIdentifier(name, nameof(name), "A parameter name");
        ArgumentNullException.ThrowIfNull(type);
        if (type is SignatureType.Concrete { Type: GrobType.Nil }) {
            throw new ArgumentException("A parameter cannot have nil as its type.", nameof(type));
        }
        if (defaultValue is GrobValue value) {
            RequireDefaultFits(type, isVariadic, value);
        }
        Name = name;
        Type = type;
        IsVariadic = isVariadic;
        Default = defaultValue;
    }

    /// <summary>The parameter's name, as the stdlib reference documents it.</summary>
    public string Name { get; }

    /// <summary>The parameter's type; for a variadic parameter, the type of each tail element.</summary>
    public SignatureType Type { get; }

    /// <summary><see langword="true"/> when this parameter is a variable-length tail.</summary>
    public bool IsVariadic { get; }

    /// <summary>
    /// The compile-time constant a call omitting this argument is filled with (D-358),
    /// or <see langword="null"/> when the argument is required.
    /// </summary>
    public GrobValue? Default { get; }

    /// <summary><see langword="true"/> when <see cref="Default"/> is set.</summary>
    public bool HasDefault => Default.HasValue;

    private static void RequireDefaultFits(SignatureType type, bool isVariadic, GrobValue value) {
        if (isVariadic) {
            throw new ArgumentException("A variadic parameter cannot have a default.", DefaultParamName);
        }
        if (type is not SignatureType.Concrete concrete) {
            throw new ArgumentException(
                "A parameter typed by a type parameter cannot have a default.", DefaultParamName);
        }
        if (value.IsNil) {
            if (!concrete.IsNullable) {
                throw new ArgumentException(
                    $"A nil default is permitted only on a nullable type, not {concrete.Type}.", DefaultParamName);
            }
            return;
        }
        GrobType? expected = value.Kind switch {
            GrobValueKind.Int => GrobType.Int,
            GrobValueKind.Float => GrobType.Float,
            GrobValueKind.String => GrobType.String,
            GrobValueKind.Bool => GrobType.Bool,
            _ => null,
        };
        if (expected is null) {
            throw new ArgumentException(
                $"A default must be a scalar (int, float, string, bool or nil); a {value.Kind} value is not.", DefaultParamName);
        }
        if (GrobTypeHelpers.ElementType(concrete.Type) != expected) {
            throw new ArgumentException(
                $"A {value.Kind} default does not fit a parameter of type {concrete.Type}.", DefaultParamName);
        }
    }
}
