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

        // Finalise the pass-1 provisional entry as a real binding (D-324). If the name
        // is already real — a prior fn/type/value decl was finalised first — emits E1102
        // at this declaration (the offending later one). Still proceeds to validate the
        // body regardless, so nested errors are reported without suppression.
        FinalizeTopLevelBinding(node.Name, GrobType.Unknown, node.Range.Start, node, node.Range);

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
            (GrobType paramType, FunctionTypeDescriptor? paramDesc) =
                p.Type is not null ? ResolveTypeRefFull(p.Type) : (GrobType.Unknown, null);
            // Use the owning FnDecl as the declaring node — Parameter is not an AstNode.
            RegisterSymbol(p.Name, paramType, p.Range.Start, node, functionDescriptor: paramDesc);
        }

        // Track the declared return type so VisitReturn can check returned values
        // (E0005) and distinguish an in-function return from a top-level one (E2203).
        // _functionReturnDescriptors is pushed in lockstep for function-type returns (D-326).
        (GrobType returnKind, FunctionTypeDescriptor? returnDesc) = ResolveTypeRefFull(node.ReturnType);
        _functionReturnTypes.Push(returnKind);
        _functionReturnDescriptors.Push(returnDesc);
        Visit(node.Body);
        _functionReturnTypes.Pop();
        _functionReturnDescriptors.Pop();

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
            // Resolve the parameter type with its structural descriptor so a function-type
            // parameter default (action: fn(): int = () => "s") is checked structurally,
            // not merely as fn-to-fn (D-326; Fix H).
            (GrobType paramType, FunctionTypeDescriptor? paramDesc) =
                p.Type is not null ? ResolveTypeRefFull(p.Type) : (GrobType.Unknown, null);
            FunctionTypeDescriptor? defaultDesc = p.DefaultValue is LambdaExpr lambdaDefault
                ? _lambdaDescriptors.GetValueOrDefault(lambdaDefault) : null;
            bool isFunctionParam = paramType == GrobType.Function || paramType == GrobType.NullableFunction;
            bool compatible = isFunctionParam
                ? TypesAreAssignable(defaultType, paramType, defaultDesc, paramDesc)
                : TypesAreAssignable(defaultType, paramType);
            if (paramType != GrobType.Unknown && defaultType != GrobType.Error && !compatible) {
                EmitError(ErrorCatalog.E0004,
                    $"Default value for parameter '{p.Name}' has type '{TypeName(defaultType)}', which is not assignable to '{TypeName(paramType)}'.",
                    p.DefaultValue.Range);
            }
        }
    }

    /// <inheritdoc/>
    public override GrobType VisitTypeDecl(TypeDecl node) {
        // Finalise the pass-1 provisional entry as a real binding (D-324).
        FinalizeTopLevelBinding(node.Name, GrobType.Unknown, node.Range.Start, node, node.Range);

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
        // const has no pass-1 provisional entry, so FinalizeTopLevelBinding both detects
        // collisions with prior real entries and registers the symbol as real (D-324).
        FinalizeTopLevelBinding(node.Name, symbolType, node.Range.Start, node, node.Range);
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
        // Carry the initialiser's structural descriptor through binding so a function-type
        // annotation (readonly f: fn(): int := () => 1, or := makeCounter()) is checked
        // structurally and the descriptor is stored on the symbol (D-326; Fixes G and I).
        FunctionTypeDescriptor? initDesc = InitialiserDescriptor(node.Value);
        (GrobType symbolType, FunctionTypeDescriptor? symbolDesc) =
            ResolveBindingFull(node.AnnotatedType, initType, initDesc, node.Value.Range);
        // Finalise the pass-1 provisional entry (D-324). Detects collisions with prior
        // real bindings and registers as real when free.
        FinalizeTopLevelBinding(node.Name, symbolType, node.Range.Start, node, node.Range, symbolDesc);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitParamBlockDecl(ParamBlockDecl node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitImportDecl(ImportDecl node) => GrobType.Unknown;
}
