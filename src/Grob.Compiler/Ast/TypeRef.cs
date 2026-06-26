using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A syntactic reference to a type — a name plus optional generic arguments
/// and an optional <c>?</c> nullable suffix. The type checker resolves these
/// against the type registry in Sprint 2+.
/// </summary>
/// <param name="Range">Source range covered by the type reference.</param>
/// <param name="Name">The unqualified type name.</param>
/// <param name="TypeArguments">Generic type arguments, empty when the type is non-generic.</param>
/// <param name="IsNullable"><see langword="true"/> when the reference ends with <c>?</c>.</param>
public record TypeRef(
    SourceRange Range,
    string Name,
    IReadOnlyList<TypeRef> TypeArguments,
    bool IsNullable);
