using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Statements — visit children; return Unknown (statements have no type).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitBlock(BlockStmt node) {
        _scopes.Push(new Dictionary<string, Symbol>());
        foreach (Statement stmt in node.Statements) Visit(stmt);
        _scopes.Pop();
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitVarDecl(VarDeclStmt node) {
        GrobType initType = Visit(node.Initializer);
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Initializer.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitExpressionStmt(ExpressionStmt node) {
        Visit(node.Expression);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitReturn(ReturnStmt node) {
        if (node.Value is not null) Visit(node.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitAssignment(AssignmentStmt node) {
        Visit(node.Target);
        Visit(node.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitCompoundAssignment(CompoundAssignmentStmt node) {
        Visit(node.Target);
        Visit(node.Value);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitIncrement(IncrementStmt node) {
        Visit(node.Target);
        return GrobType.Unknown;
    }
}
