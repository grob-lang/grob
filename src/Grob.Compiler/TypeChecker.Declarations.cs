using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Core.NamedTypes;

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
            (GrobType paramType, string? paramStructName, FunctionTypeDescriptor? paramDesc, ArrayTypeDescriptor? paramArrayDesc) =
                p.Type is not null ? ResolveSignatureType(p.Type) : (GrobType.Unknown, null, null, null);
            // Use the owning FnDecl as the declaring node — Parameter is not an AstNode.
            // The struct name/array descriptor travel on the symbol itself (not recoverable
            // from DeclarationNode alone) so a struct-typed or T[]-typed parameter resolves
            // inside the function body the same way a `:=`-inferred local does.
            RegisterSymbol(p.Name, paramType, p.Range.Start, node,
                typeIdentity: new(paramDesc, paramStructName, paramArrayDesc));
        }

        // Track the declared return type so VisitReturn can check returned values
        // (E0005) and distinguish an in-function return from a top-level one (E2203).
        // _functionReturnDescriptors is pushed in lockstep for function-type returns (D-326).
        // _functionReturnStructNames is pushed in lockstep for named-struct returns
        // (fix/compiler-struct-nominal-identity, Site C) — never pushed for a lambda, see
        // that stack's declaration comment. _functionReturnArrayDescriptors mirrors it for
        // T[] returns (D-351).
        (GrobType returnKind, string? returnStructName, FunctionTypeDescriptor? returnDesc, ArrayTypeDescriptor? returnArrayDesc) =
            ResolveSignatureType(node.ReturnType);
        _functionReturnTypes.Push(returnKind);
        _functionReturnDescriptors.Push(returnDesc);
        _functionReturnStructNames.Push(returnStructName);
        _functionReturnArrayDescriptors.Push(returnArrayDesc);
        _controlFrameFloors.Push(_controlFrames.Count);
        Visit(node.Body);
        _controlFrameFloors.Pop();
        _functionReturnTypes.Pop();
        _functionReturnDescriptors.Pop();
        _functionReturnStructNames.Pop();
        _functionReturnArrayDescriptors.Pop();

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
            (GrobType paramType, string? paramStructName, FunctionTypeDescriptor? paramDesc, ArrayTypeDescriptor? paramArrayDesc) =
                p.Type is not null ? ResolveSignatureType(p.Type) : (GrobType.Unknown, null, null, null);
            if (paramType != GrobType.Unknown && defaultType != GrobType.Error &&
                    !IsParameterDefaultCompatible(paramType, paramStructName, paramDesc, paramArrayDesc, defaultType, p.DefaultValue)) {
                EmitError(ErrorCatalog.E0004,
                    $"Default value for parameter '{p.Name}' has type '{TypeName(defaultType)}', which is not assignable to '{TypeName(paramType)}'.",
                    p.DefaultValue.Range);
            }
        }
    }

    /// <summary>
    /// Reports whether a parameter default's value (<paramref name="defaultValue"/>, already
    /// known to have <paramref name="defaultType"/>) may be assigned to the parameter's
    /// declared type — flat kind, then structural function descriptor (D-326), array element
    /// type (D-351), and struct nominal identity in turn. Mirrors
    /// <see cref="IsFieldValueCompatible"/> and the argument-side check in <c>VisitCall</c>;
    /// split from <see cref="CheckParameterDefaults"/> to keep that method under the
    /// analyser's cognitive-complexity bar.
    /// </summary>
    private bool IsParameterDefaultCompatible(
            GrobType paramType, string? paramStructName, FunctionTypeDescriptor? paramDesc,
            ArrayTypeDescriptor? paramArrayDesc, GrobType defaultType, Expression defaultValue) {
        bool isFunctionParam = paramType == GrobType.Function || paramType == GrobType.NullableFunction;
        bool isArrayParam = paramType == GrobType.Array || paramType == GrobType.NullableArray;
        bool compatible = isFunctionParam
            ? TypesAreAssignable(defaultType, paramType, ExpressionDescriptor(defaultValue), paramDesc)
            : TypesAreAssignable(defaultType, paramType);
        if (compatible && isArrayParam &&
                !ArrayElementAssignable(ArrayDescriptorOf(defaultValue), paramArrayDesc)) {
            compatible = false;
        }
        if (compatible && IsStructNominalMismatch(paramType, paramStructName, defaultValue)) {
            compatible = false;
        }
        return compatible;
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
            (GrobType kind, string? namedTypeName, FunctionTypeDescriptor? fnDesc, ArrayTypeDescriptor? arrayDesc) =
                ResolveFieldAnnotationType(field.Type);
            bool isRequired = field.DefaultValue is null;
            resolvedFields.Add(new ResolvedFieldInfo(field.Name, kind, namedTypeName, field.Range, isRequired, fnDesc, arrayDesc));
        }

        // Type-check field default expressions and flag sibling-field references (E0013).
        CheckFieldDefaults(node, resolvedFields);

        // Register this type's resolved field list for phase 2.5 cycle detection.
        _userTypeRegistry.Register(new UserTypeInfo {
            Name = node.Name,
            Fields = resolvedFields,
            Range = node.Range,
        });

        return GrobType.Unknown;
    }

    // -----------------------------------------------------------------------
    // Anonymous struct construction (§10, Sprint 6D).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitAnonStruct(AnonStructExpr node) {
        List<ResolvedFieldInfo> resolvedFields = new(node.Fields.Count);
        foreach (FieldInit fi in node.Fields) {
            GrobType valueType = Visit(fi.Value);
            // For struct-typed fields, record the type name so chained member access
            // can resolve through the correct registry entry.
            string? namedTypeName = (valueType == GrobType.Struct || valueType == GrobType.AnonStruct)
                ? GetFieldValueStructTypeName(fi.Value)
                : null;
            ArrayTypeDescriptor? arrayDesc = valueType is GrobType.Array or GrobType.NullableArray
                ? ArrayDescriptorOf(fi.Value)
                : null;
            resolvedFields.Add(new ResolvedFieldInfo(fi.Name, valueType, namedTypeName, fi.Range, IsRequired: true, ArrayDescriptor: arrayDesc));
        }

        // Build the canonical structural signature: sorted field-name:GrobType pairs.
        // An empty literal uses "<anon>" so GrobStruct (which requires non-empty TypeName)
        // is always constructed with a valid name.
        string sig = resolvedFields.Count == 0
            ? "<anon>"
            : string.Join(",",
                resolvedFields
                    .OrderBy(f => f.Name, StringComparer.Ordinal)
                    .Select(f => f.NamedTypeName is not null
                        ? $"{f.Name}:{f.Kind}:{f.NamedTypeName}"
                        : $"{f.Name}:{f.Kind}"));

        if (!_structuralTypes.ContainsKey(sig)) {
            _structuralTypes[sig] = new UserTypeInfo {
                Name = sig,
                Fields = resolvedFields,
                Range = node.Range,
            };
        }

        node.SynthesisedTypeName = sig;
        return GrobType.AnonStruct;
    }

    // Returns the struct or anon-struct type name for a field-value expression.
    // GetStructTypeNameFromDecl (TypeChecker.Expressions.cs) handles both Struct
    // and AnonStruct initialisers via ExtractFromBinding; a struct-typed parameter
    // resolves via its symbol's NamedStructTypeName first (Sprint 6 close), same as
    // GetStructTypeName.
    private string? GetFieldValueStructTypeName(Expression expr) => expr switch {
        StructConstructionExpr sc => sc.TypeName,
        AnonStructExpr anon => anon.SynthesisedTypeName,
        IdentifierExpr id => LookupSymbol(id.Name)?.NamedStructTypeName ?? GetStructTypeNameFromDecl(id.Declaration),
        MemberAccessExpr ma => ma.ResolvedStructTypeName,
        _ => null,
    };

    // -----------------------------------------------------------------------
    // Struct construction (§10, Sprint 6B).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitStructConstruction(StructConstructionExpr node) {
        (TypeDecl? typeDecl, UserTypeInfo? typeInfo) = ResolveConstructionTypeName(node);
        if (typeDecl is null || typeInfo is null) return GrobType.Error;

        HashSet<string> declaredNames = typeInfo.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        (Dictionary<string, FieldInit> suppliedByName, int unknownCount) =
            CollectSuppliedFields(node, declaredNames);
        EmitMissingFieldErrors(typeInfo.Fields, suppliedByName, node.TypeName, node.Range, unknownCount);
        TypeCheckFieldValues(node.Fields, typeInfo);
        node.ResolvedTypeDecl = typeDecl;
        return GrobType.Struct;
    }

    /// <summary>
    /// Resolves the construction's type name to its <see cref="TypeDecl"/> and
    /// <see cref="UserTypeInfo"/>. Visits field values for §3.1.1 annotations and emits
    /// E1001 on failure; returns <see langword="null"/> pairs when the type cannot be
    /// resolved.
    /// </summary>
    private (TypeDecl? TypeDecl, UserTypeInfo? TypeInfo) ResolveConstructionTypeName(StructConstructionExpr node) {
        Symbol? symbol = LookupSymbol(node.TypeName);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001, $"'{node.TypeName}' is not defined.", node.Range);
            foreach (FieldInit fi in node.Fields) Visit(fi.Value);
            return (null, null);
        }
        if (symbol.DeclarationNode is not TypeDecl typeDecl) {
            EmitError(ErrorCatalog.E1001, $"'{node.TypeName}' is not a type.", node.Range);
            foreach (FieldInit fi in node.Fields) Visit(fi.Value);
            return (null, null);
        }
        UserTypeInfo? typeInfo = _userTypeRegistry.TryGet(node.TypeName);
        if (typeInfo is null) {
            foreach (FieldInit fi in node.Fields) Visit(fi.Value);
            return (null, null);
        }
        return (typeDecl, typeInfo);
    }

    /// <summary>
    /// Walks the supplied fields, emits E0012 for unknown field names, and returns
    /// the map of known supplied fields together with the count of unknown ones.
    /// </summary>
    private (Dictionary<string, FieldInit> SuppliedByName, int UnknownCount) CollectSuppliedFields(
        StructConstructionExpr node, HashSet<string> declaredNames) {
        Dictionary<string, FieldInit> suppliedByName = new(StringComparer.Ordinal);
        int unknownCount = 0;
        foreach (FieldInit fi in node.Fields) {
            if (!declaredNames.Contains(fi.Name)) {
                EmitError(ErrorCatalog.E0012,
                    $"'{fi.Name}' is not a declared field of type '{node.TypeName}'.",
                    fi.Range);
                unknownCount++;
            } else {
                suppliedByName[fi.Name] = fi;
            }
        }
        return (suppliedByName, unknownCount);
    }

    /// <summary>
    /// Emits E0103 for required fields not present in <paramref name="suppliedByName"/>.
    /// Each unknown field (tracked by <paramref name="unknownCount"/>) absorbs one
    /// missing-field diagnostic on the assumption that the user misspelled a required
    /// field name; only the excess missing fields are reported.
    /// </summary>
    private void EmitMissingFieldErrors(
        IReadOnlyList<ResolvedFieldInfo> fields,
        Dictionary<string, FieldInit> suppliedByName,
        string typeName,
        SourceRange constructionRange,
        int unknownCount) {
        int unknownQuota = unknownCount;
        foreach (ResolvedFieldInfo f in fields.Where(f => f.IsRequired && !suppliedByName.ContainsKey(f.Name))) {
            if (unknownQuota > 0) {
                unknownQuota--;
            } else {
                EmitError(ErrorCatalog.E0103,
                    $"Required field '{f.Name}' of type '{typeName}' has no initialiser at this construction site.",
                    constructionRange);
            }
        }
    }

    /// <summary>
    /// Type-checks each supplied field value and emits E0001 when the value's type is
    /// not assignable to the declared field type (including structural function-type
    /// checking, D-326).
    /// </summary>
    private void TypeCheckFieldValues(IReadOnlyList<FieldInit> fields, UserTypeInfo typeInfo) {
        foreach (FieldInit fi in fields) {
            GrobType valueType = Visit(fi.Value);
            ResolvedFieldInfo? fieldInfo = typeInfo.Fields.FirstOrDefault(f => f.Name == fi.Name);
            if (fieldInfo is not null && fieldInfo.Kind != GrobType.Unknown && valueType != GrobType.Error &&
                    !IsFieldValueCompatible(fieldInfo, valueType, fi.Value)) {
                EmitError(ErrorCatalog.E0001,
                    $"Cannot assign value of type '{TypeName(valueType)}' to field '{fi.Name}' of type '{TypeName(fieldInfo.Kind)}'.",
                    fi.Value.Range);
            }
        }
    }

    /// <summary>
    /// Reports whether <paramref name="valueExpr"/>'s value (already known to have
    /// <paramref name="valueType"/>) may be assigned to <paramref name="fieldInfo"/>'s
    /// declared field type — flat kind, then structural function descriptor (D-326),
    /// array element type (D-351), and struct nominal identity in turn. Split from
    /// <see cref="TypeCheckFieldValues"/> to keep that method's cognitive complexity
    /// under the analyser bar.
    /// </summary>
    private bool IsFieldValueCompatible(ResolvedFieldInfo fieldInfo, GrobType valueType, Expression valueExpr) {
        bool isFunctionField = fieldInfo.Kind == GrobType.Function || fieldInfo.Kind == GrobType.NullableFunction;
        bool isArrayField = fieldInfo.Kind == GrobType.Array || fieldInfo.Kind == GrobType.NullableArray;
        bool compatible = isFunctionField
            ? TypesAreAssignable(valueType, fieldInfo.Kind, ExpressionDescriptor(valueExpr), fieldInfo.FunctionDescriptor)
            : TypesAreAssignable(valueType, fieldInfo.Kind);
        if (compatible && isArrayField && !ArrayElementAssignable(ArrayDescriptorOf(valueExpr), fieldInfo.ArrayDescriptor)) {
            compatible = false;
        }
        if (compatible && IsStructNominalMismatch(fieldInfo.Kind, fieldInfo.NamedTypeName, valueExpr)) {
            compatible = false;
        }
        return compatible;
    }

    /// <summary>
    /// Type-checks each field default expression in the enclosing scope and detects
    /// sibling-field references (E0013). Mirrors <see cref="CheckParameterDefaults"/>.
    /// </summary>
    private void CheckFieldDefaults(TypeDecl node, List<ResolvedFieldInfo> fields) {
        HashSet<string> fieldNames = fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (TypeField field in node.Fields.Where(field => field.DefaultValue is not null)) {
            CheckSingleFieldDefault(field, node, fieldNames, fields.First(f => f.Name == field.Name));
        }
    }

    /// <summary>
    /// Type-checks a single field's default expression, emitting E0013 for sibling-field
    /// references and E0001 for type mismatches. Stamps §3.1.1 sentinels on identifier
    /// nodes under the default when a sibling reference is detected (prevents E1001
    /// cascade errors from a subsequent <see cref="VisitIdentifier"/> call).
    /// </summary>
    private void CheckSingleFieldDefault(
        TypeField field, TypeDecl node, HashSet<string> fieldNames, ResolvedFieldInfo resolved) {
        Expression defaultValue = field.DefaultValue!;
        SiblingRefWalker walker = new(fieldNames);
        walker.Visit(defaultValue);
        if (walker.SiblingRefs.Count > 0) {
            foreach (IdentifierExpr sibRef in walker.SiblingRefs) {
                EmitError(ErrorCatalog.E0013,
                    $"Field default for '{field.Name}' may not reference sibling field '{sibRef.Name}' of type '{node.Name}'.",
                    sibRef.Range);
            }
            // §3.1.1: stamp error sentinels on all identifier nodes without calling
            // VisitIdentifier, which would emit E1001 cascades for sibling field names
            // that are not in scope at the construction site.
            new SentinelFillWalker().Visit(defaultValue);
            return;
        }
        GrobType defaultType = Visit(defaultValue);
        if (resolved.Kind != GrobType.Unknown && defaultType != GrobType.Error) {
            bool isFunctionField = resolved.Kind == GrobType.Function || resolved.Kind == GrobType.NullableFunction;
            FunctionTypeDescriptor? defaultDesc = ExpressionDescriptor(defaultValue);
            bool compatible = isFunctionField
                ? TypesAreAssignable(defaultType, resolved.Kind, defaultDesc, resolved.FunctionDescriptor)
                : TypesAreAssignable(defaultType, resolved.Kind);
            if (compatible && IsStructNominalMismatch(resolved.Kind, resolved.NamedTypeName, defaultValue)) {
                compatible = false;
            }
            if (!compatible) {
                EmitError(ErrorCatalog.E0001,
                    $"Default value for field '{field.Name}' has type '{TypeName(defaultType)}', which is not assignable to '{TypeName(resolved.Kind)}'.",
                    defaultValue.Range);
            }
        }
    }

    /// <summary>
    /// Stamps every <see cref="IdentifierExpr"/> in a sub-tree with the D-311 error
    /// sentinels (<see cref="GrobType.Error"/> / <see cref="UnresolvedDecl.Instance"/>)
    /// so the §3.1.1 invariant holds after an E0013 error path without calling
    /// <see cref="TypeChecker.VisitIdentifier"/> (which would emit E1001 cascades for
    /// sibling field names that are not in scope at the construction site).
    /// </summary>
    private sealed class SentinelFillWalker : AstWalker {
        public override Unit VisitIdentifier(IdentifierExpr node) {
            node.ResolvedType = GrobType.Error;
            node.Declaration = UnresolvedDecl.Instance;
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    /// <summary>
    /// Walks a default expression and records any <see cref="IdentifierExpr"/> nodes
    /// whose names match a sibling field of the declaring type (E0013).
    /// </summary>
    private sealed class SiblingRefWalker(HashSet<string> fieldNames) : AstWalker {
        public List<IdentifierExpr> SiblingRefs { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) {
            if (fieldNames.Contains(node.Name)) SiblingRefs.Add(node);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
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
    private (GrobType Kind, string? NamedTypeName, FunctionTypeDescriptor? FunctionDescriptor, ArrayTypeDescriptor? ArrayDescriptor)
        ResolveFieldAnnotationType(TypeRef typeRef) {
        // Array suffix: T[] or T[]? — cycle walk terminates at array fields (§17.1).
        // Still validate the element type so that Missing[] emits E1001. The element's
        // descriptor (D-351) is resolved separately via ResolveArrayElementDescriptor,
        // which is quiet (no diagnostic) — the validation above already covers E1001.
        if (typeRef is ArrayTypeRef arr) {
            ResolveFieldAnnotationType(arr.ElementType);
            return (arr.IsNullable ? GrobType.NullableArray : GrobType.Array, null, null,
                ResolveArrayElementDescriptor(arr.ElementType));
        }

        // Function type: fn(T…): R or (fn(T…): R)? — erased at runtime (D-326).
        // Validate parameter and return types so that fn(Missing): int emits E1001.
        // Also resolve the full structural descriptor (D-326) so CheckFieldDefaults and
        // VisitStructConstruction can perform descriptor-aware assignability checks.
        if (typeRef is FunctionTypeRef fnRef) {
            foreach (TypeRef param in fnRef.ParameterTypes)
                ResolveFieldAnnotationType(param);
            ResolveFieldAnnotationType(fnRef.ReturnType);
            (GrobType kind, FunctionTypeDescriptor? fnDesc) = ResolveTypeRefFull(typeRef);
            return (kind, null, fnDesc, null);
        }

        // Plain named type reference. Check built-ins first, then user-defined.
        GrobType builtin = typeRef.Name switch {
            "int" or "float" or "string" or "bool" or "nil" or "array" or "map" =>
                ResolveTypeRef(typeRef),
            _ => GrobType.Unknown,
        };

        return builtin != GrobType.Unknown
            ? (builtin, null, null, null)
            : ResolveNamedFieldType(typeRef);
    }

    /// <summary>
    /// Resolves a plain named (non-built-in) field type reference — a
    /// <see cref="NamedTypeRegistry"/> entry (D-356) or a user-defined <c>type</c>.
    /// Split from <see cref="ResolveFieldAnnotationType"/> to keep that method's
    /// cognitive complexity under the analyser bar.
    /// </summary>
    private (GrobType Kind, string? NamedTypeName, FunctionTypeDescriptor? FunctionDescriptor, ArrayTypeDescriptor? ArrayDescriptor)
            ResolveNamedFieldType(TypeRef typeRef) {
        // D-356: a registered nominal type (guid, date, ...) resolves here for the
        // field-annotation position — the shared TryResolveRegisteredNamedType helper
        // (TypeChecker.cs) also serves the signature position, keeping the two lookup
        // sites from drifting.
        if (TryResolveRegisteredNamedType(typeRef) is (GrobType namedKind, string namedName)) {
            return (namedKind, namedName, null, null);
        }

        // User-defined type: look up the symbol registered in pass 1.
        Symbol? symbol = LookupSymbol(typeRef.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001,
                $"'{typeRef.Name}' is not defined.",
                typeRef.Range);
            return (GrobType.Error, null, null, null);
        }

        if (symbol.DeclarationNode is not TypeDecl) {
            EmitError(ErrorCatalog.E1001,
                $"'{typeRef.Name}' is not a type.",
                typeRef.Range);
            return (GrobType.Error, null, null, null);
        }

        GrobType structKind = typeRef.IsNullable ? GrobType.NullableStruct : GrobType.Struct;
        return (structKind, typeRef.Name, null, null);
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
        (GrobType symbolType, FunctionTypeDescriptor? symbolDesc, ArrayTypeDescriptor? symbolArrayDesc) =
            ResolveBindingFull(node.AnnotatedType, initType, initDesc, node.Value.Range, node.Value);
        // Finalise the pass-1 provisional entry (D-324). Detects collisions with prior
        // real bindings and registers as real when free.
        FinalizeTopLevelBinding(node.Name, symbolType, node.Range.Start, node, node.Range, symbolDesc, symbolArrayDesc);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitParamBlockDecl(ParamBlockDecl node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitImportDecl(ImportDecl node) => GrobType.Unknown;
}
