using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>
/// A sentinel declaration node used by the type checker to pre-register core stdlib
/// module namespaces (<c>math</c>, and in later Sprint 8 increments <c>path</c>,
/// <c>env</c>, <c>log</c>, <c>guid</c>, <c>formatAs</c>) in the global scope before the
/// first pass runs (D-342). A namespace is a third name category alongside value and
/// type bindings — neither. Not produced by the parser and never part of a
/// <see cref="CompilationUnit"/>'s <c>TopLevel</c> list, mirroring
/// <see cref="BuiltinDecl"/>. Stored as a <see cref="Symbol.DeclarationNode"/> so
/// <c>VisitIdentifier</c> can detect "this name is a namespace, not a value" (emitting
/// E1004) the same way it already detects "this name is a type, not a value" via
/// <c>TypeDecl</c> (E2102), and so go-to-definition has a typed node to return.
/// </summary>
/// <param name="NamespaceName">The namespace name exactly as it appears in source (e.g. <c>"math"</c>).</param>
public sealed record NamespaceDecl(string NamespaceName) : Declaration(SourceRange.Unknown) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitNamespaceDecl(this);
}
