using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
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
        // D-289: enforce that the RHS is a compile-time constant expression.
        // Catching this here produces a proper diagnostic instead of a
        // GrobInternalException from the compiler's constant folder.
        if (!IsConstantExpr(node.Value)) {
            EmitError(ErrorCatalog.E0205,
                $"The right-hand side of 'const {node.Name}' is not a compile-time constant expression (D-289). "
              + "Change the binding to 'readonly' if a runtime value is needed.",
                node.Value.Range);
        }
        GrobType symbolType = ResolveBinding(node.AnnotatedType, initType, node.Value.Range);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node);
        return GrobType.Unknown;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="expr"/> is in the
    /// compile-time constant form defined by D-289.
    /// </summary>
    private static bool IsConstantExpr(Expression expr) => expr switch {
        IntLiteralExpr => true,
        FloatLiteralExpr => true,
        RawStringLiteralExpr => true,
        BoolLiteralExpr => true,
        NilLiteralExpr => true,
        // Double-quoted strings without ${} interpolation segments.
        InterpolatedStringExpr istr => istr.Parts.All(p => p is StringTextPart),
        GroupingExpr g => IsConstantExpr(g.Inner),
        // Binary operators on constant operands (NilCoalesce is not in D-289).
        BinaryExpr b when b.Operator != BinaryOperator.NilCoalesce
            => IsConstantExpr(b.Left) && IsConstantExpr(b.Right),
        // Unary - and !.
        UnaryExpr u => IsConstantExpr(u.Operand),
        // References to other const-bound identifiers.
        IdentifierExpr id => id.Declaration is ConstDecl,
        // Error nodes — a prior diagnostic already covers this.
        ErrorExpr => true,
        _ => false,
    };

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
