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
            FunctionTypeDescriptor? defaultDesc = ExpressionDescriptor(p.DefaultValue);
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
        // Finalise the pass-1 provisional entry as a real binding (D-324). Emits E1102
        // if the name collides with any prior real binding (fn, type, value).
        FinalizeTopLevelBinding(node.Name, GrobType.Unknown, node.Range.Start, node, node.Range);

        // E2208 — duplicate field name. Collect seen names; report the second occurrence.
        HashSet<string> seenFieldNames = new(StringComparer.Ordinal);
        List<ResolvedFieldInfo> resolvedFields = new(node.Fields.Count);

        foreach (TypeField field in node.Fields) {
            // E1103 — reserved identifier may not be a field name (D-320).
            CheckReservedBindingName(field.Name, field.Range);

            // E2208 — duplicate field name within this declaration.
            if (!seenFieldNames.Add(field.Name)) {
                EmitError(ErrorCatalog.E2208,
                    $"Duplicate field name '{field.Name}' in type declaration '{node.Name}'.",
                    field.Range);
            }

            // Resolve the field's type annotation through the full §9 grammar.
            (GrobType kind, string? namedTypeName) = ResolveFieldAnnotationType(field.Type);
            bool isRequired = field.DefaultValue is null;
            resolvedFields.Add(new ResolvedFieldInfo(field.Name, kind, namedTypeName, field.Range, isRequired));
        }

        // Register this type's resolved field list for phase 2.5 cycle detection.
        _userTypeRegistry.Register(new UserTypeInfo {
            Name = node.Name,
            Fields = resolvedFields,
            Range = node.Range,
        });

        return GrobType.Unknown;
    }

    /// <summary>
    /// Resolves a field's <see cref="TypeRef"/> to a <see cref="GrobType"/> and,
    /// when the result is a user-defined struct type, the declared type name so the
    /// §17.1 cycle-detection DFS can follow the edge.
    /// </summary>
    /// <remarks>
    /// Handles all §9 forms: array (<see cref="ArrayTypeRef"/>), function type
    /// (<see cref="FunctionTypeRef"/>), built-in named types and user-defined named
    /// types. For user-defined names the symbol table is consulted — valid after
    /// pass-1 registration (D-166). An unknown name emits E1001; a name that resolves
    /// to a non-type symbol also emits E1001.
    /// </remarks>
    private (GrobType Kind, string? NamedTypeName) ResolveFieldAnnotationType(TypeRef typeRef) {
        // Array suffix: T[] or T[]? — cycle walk terminates at array fields (§17.1).
        if (typeRef is ArrayTypeRef arr)
            return (arr.IsNullable ? GrobType.NullableArray : GrobType.Array, null);

        // Function type: fn(T…): R or (fn(T…): R)? — erased at runtime (D-326).
        if (typeRef is FunctionTypeRef) {
            (GrobType kind, _) = ResolveTypeRefFull(typeRef);
            return (kind, null);
        }

        // Plain named type reference. Check built-ins first, then user-defined.
        GrobType builtin = typeRef.Name switch {
            "int" or "float" or "string" or "bool" or "nil" or "array" or "map" =>
                ResolveTypeRef(typeRef),
            _ => GrobType.Unknown,
        };

        if (builtin != GrobType.Unknown)
            return (builtin, null);

        // User-defined type: look up the symbol registered in pass 1.
        Symbol? symbol = LookupSymbol(typeRef.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001,
                $"'{typeRef.Name}' is not defined.",
                typeRef.Range);
            return (GrobType.Error, null);
        }

        if (symbol.DeclarationNode is not TypeDecl) {
            EmitError(ErrorCatalog.E1001,
                $"'{typeRef.Name}' is not a type.",
                typeRef.Range);
            return (GrobType.Error, null);
        }

        GrobType structKind = typeRef.IsNullable ? GrobType.NullableStruct : GrobType.Struct;
        return (structKind, typeRef.Name);
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
