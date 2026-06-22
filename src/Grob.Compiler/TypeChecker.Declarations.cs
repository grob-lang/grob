using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Declarations — Pass 2 validation.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitFnDecl(FnDecl node) {
        // A reserved identifier (formatAs, select) may not be a function name
        // (E1103, D-320). The fn name carries no standalone source location, so the
        // diagnostic points at the declaration head.
        CheckReservedBindingName(node.Name, node.Range);

        // The fn name was already registered in pass 1; don't re-register here.
        // Default expressions materialise at the call site (D-113), so they are
        // type-checked in the enclosing scope — before the parameter scope opens.
        // A default that references a sibling parameter therefore resolves to E1001
        // here, rather than binding to the parameter and then silently compiling
        // against caller scope.
        CheckParameterDefaults(node);

        // Push a scope for parameters, then visit the body (which pushes its own scope).
        _scopes.Push(new Dictionary<string, Symbol>());
        foreach (Parameter p in node.Parameters) {
            // A reserved identifier (formatAs, select) may not be a parameter name
            // (E1103, D-320).
            CheckReservedBindingName(p.Name, p.Range);
            GrobType paramType = p.Type is not null ? ResolveTypeRef(p.Type) : GrobType.Unknown;
            // Use the owning FnDecl as the declaring node — Parameter is not an AstNode.
            RegisterSymbol(p.Name, paramType, p.Range.Start, node);
        }

        // Track the declared return type so VisitReturn can check returned values
        // (E0005) and distinguish an in-function return from a top-level one (E2203).
        _functionReturnTypes.Push(ResolveTypeRef(node.ReturnType));
        Visit(node.Body);
        _functionReturnTypes.Pop();

        _scopes.Pop();
        return GrobType.Unknown;
    }

    /// <summary>
    /// Type-checks each parameter default in the function's enclosing scope (the
    /// parameter scope is not yet open), checking the default's type against its
    /// parameter (E0004). A default referencing a sibling parameter resolves to
    /// E1001 here, which is the intended behaviour: defaults compile at the call
    /// site, where sibling parameters are not in scope.
    /// </summary>
    private void CheckParameterDefaults(FnDecl node) {
        foreach (Parameter p in node.Parameters) {
            if (p.DefaultValue is null) continue;
            GrobType defaultType = Visit(p.DefaultValue);
            GrobType paramType = p.Type is not null ? ResolveTypeRef(p.Type) : GrobType.Unknown;
            if (paramType != GrobType.Unknown && defaultType != GrobType.Error
                && !TypesAreAssignable(defaultType, paramType)) {
                EmitError(ErrorCatalog.E0004,
                    $"Default value for parameter '{p.Name}' has type '{TypeName(defaultType)}', which is not assignable to '{TypeName(paramType)}'.",
                    p.DefaultValue.Range);
            }
        }
    }

    /// <inheritdoc/>
    public override GrobType VisitTypeDecl(TypeDecl node) {
        // Full type-field checking lands in Sprint 6. The one rule that applies now:
        // a reserved identifier (formatAs, select) may not be a field name (E1103,
        // D-320).
        foreach (TypeField field in node.Fields) {
            CheckReservedBindingName(field.Name, field.Range);
        }
        return GrobType.Unknown;
    }

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
