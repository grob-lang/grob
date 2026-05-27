using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Declarations — Pass 2 validation.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitFnDecl(FnDecl node) {
        // The fn name was already registered in pass 1; don't re-register here.
        // Push a scope for parameters, then visit the body (which pushes its own scope).
        _scopes.Push(new Dictionary<string, Symbol>());
        foreach (Parameter p in node.Parameters) {
            GrobType paramType = p.Type is not null ? ResolveTypeRef(p.Type) : GrobType.Unknown;
            // Use the owning FnDecl as the declaring node — Parameter is not an AstNode.
            RegisterSymbol(p.Name, paramType, p.Range.Start, node);
            if (p.DefaultValue is not null) Visit(p.DefaultValue);
        }
        Visit(node.Body);
        _scopes.Pop();
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitTypeDecl(TypeDecl node) => GrobType.Unknown; // Sprint 6

    /// <inheritdoc/>
    public override GrobType VisitConstDecl(ConstDecl node) {
        GrobType initType = Visit(node.Value);
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Value.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitReadonlyDecl(ReadonlyDecl node) {
        GrobType initType = Visit(node.Value);
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Value.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitParamBlockDecl(ParamBlockDecl node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitImportDecl(ImportDecl node) => GrobType.Unknown;
}
