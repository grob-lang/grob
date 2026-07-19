using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
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
        // A reserved identifier (formatAs, select) may not be bound (E1103, D-320).
        CheckReservedBindingName(node.Name, node.Range);

        // Check for same-scope re-declaration (E1102) before visiting the initializer
        // so we don't clobber a valid symbol if the name is already in this scope.
        // A pass-1 forward-reference placeholder (D-321, D-324) is not a redeclaration —
        // pass 2 finalises it here — so only a non-provisional entry counts as a duplicate.
        // Skipped entirely for a reserved identifier (Sprint 8 Increment E: 'formatAs' is
        // both reserved and a pre-registered NamespaceDecl symbol, D-342) — the E1103
        // above already fully explains the error; a name collision with the namespace
        // sentinel it also happens to overwrite is not a second, independent mistake.
        if (!_reservedIdentifiers.Contains(node.Name) &&
                _scopes.Peek().TryGetValue(node.Name, out Symbol? existing) && !existing.Provisional) {
            // Only suggest '=' when the prior binding is a mutable variable (a ':='
            // declaration). const, readonly, fn and type bindings cannot be reassigned,
            // so the hint would be false advice for a cross-kind collision (PR #92 review).
            string hint = existing.DeclarationNode is VarDeclStmt ? " Use '=' to reassign." : "";
            EmitError(ErrorCatalog.E1102,
                $"'{node.Name}' is already declared in this scope "
              + $"(first declared at line {existing.DeclaredAt.Line}).{hint}",
                node.Range);
            // Still visit the initializer for cascade suppression on its sub-expressions.
            Visit(node.Initializer);
            return GrobType.Unknown;
        }

        GrobType initType = Visit(node.Initializer);
        FunctionTypeDescriptor? initDesc = InitialiserDescriptor(node.Initializer);
        (GrobType symbolType, FunctionTypeDescriptor? symbolDesc, ArrayTypeDescriptor? symbolArrayDesc) =
            ResolveBindingFull(node.AnnotatedType, initType, initDesc, node.Initializer.Range, node.Initializer);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node,
            typeIdentity: new(symbolDesc, ArrayDescriptor: symbolArrayDesc));
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitExpressionStmt(ExpressionStmt node) {
        Visit(node.Expression);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitReturn(ReturnStmt node) {
        // A return outside any function body is a script-level return (E2203).
        // §22: use exit(<n>) to terminate a script. Still visit the value so its
        // sub-expressions are resolved (§3.1.1) and any nested errors surface.
        if (_functionReturnTypes.Count == 0) {
            EmitError(ErrorCatalog.E2203,
                "'return' is not valid at script level. Use 'exit(<n>)' to terminate a script with a status code.",
                node.Range);
            if (node.Value is not null) Visit(node.Value);
            return GrobType.Unknown;
        }

        if (IsInsideOwnFunctionsFinally()) {
            EmitError(ErrorCatalog.E2207, "'return' is not permitted inside a 'finally' block.", node.Range);
            if (node.Value is not null) Visit(node.Value);
            return GrobType.Unknown;
        }

        GrobType expected = _functionReturnTypes.Peek();

        // A bare 'return' yields nil. Since 'void' is not a user-declarable return
        // type (only print() is void — §"print() and void"), a bare return must
        // still satisfy the declared type: it is accepted only by a nullable or nil
        // return type, and is E0005 against a non-nullable one.
        GrobType actual = node.Value is not null ? Visit(node.Value) : GrobType.Nil;

        // An Unknown declared return type (a deferred type) is permissive; cascade
        // suppression covers an already-errored value.
        if (expected != GrobType.Unknown && actual != GrobType.Error
            && !ComputeReturnCompatibility(actual, expected, node.Value)) {
            EmitReturnTypeMismatch(node, actual, expected);
        }
        return GrobType.Unknown;
    }

    /// <summary>Emits E0005 for a return value or bare return incompatible with the
    /// enclosing function's declared return type.</summary>
    private void EmitReturnTypeMismatch(ReturnStmt node, GrobType actual, GrobType expected) {
        EmitError(ErrorCatalog.E0005,
            node.Value is not null
                ? $"Cannot return a value of type '{TypeName(actual)}' from a function declared to return '{TypeName(expected)}'."
                : $"A bare 'return' yields 'nil', which is not assignable to the declared return type '{TypeName(expected)}'.",
            node.Value is not null ? node.Value.Range : node.Range);
    }

    /// <summary>
    /// True when the current <c>return</c> site has a <see cref="ControlFrame.Finally"/>
    /// frame pushed since the enclosing function/lambda body began (Sprint 7
    /// Increment C, D-275/E2207). Bounded by <see cref="_controlFrameFloors"/> so a
    /// <c>finally</c> belonging to an <em>outer</em> function does not leak in — the
    /// D-276 carve-out for a nested block-body lambda.
    /// </summary>
    private bool IsInsideOwnFunctionsFinally() {
        int floor = _controlFrameFloors.Count > 0 ? _controlFrameFloors.Peek() : 0;
        return _controlFrames.Count > floor
            && _controlFrames.Take(_controlFrames.Count - floor).Contains(ControlFrame.Finally);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="actual"/> is assignable to
    /// <paramref name="expected"/> in the context of a return statement.
    /// For function-typed returns the structural descriptor is compared (D-326 Fix K).
    /// </summary>
    private bool ComputeReturnCompatibility(
        GrobType actual, GrobType expected, Expression? valueNode) {
        bool isFunctionReturn = expected == GrobType.Function || expected == GrobType.NullableFunction;
        bool actualIsFunction = actual == GrobType.Function || actual == GrobType.NullableFunction;
        if (isFunctionReturn && actualIsFunction && valueNode is not null) {
            // Structural descriptor comparison for any function-typed return value —
            // a direct lambda, a call result, or a bound function variable (D-326; Fix K).
            FunctionTypeDescriptor? expectedDesc =
                _functionReturnDescriptors.TryPeek(out FunctionTypeDescriptor? peeked) ? peeked : null;
            FunctionTypeDescriptor? actualDesc = ExpressionDescriptor(valueNode);
            return TypesAreAssignable(actual, expected, actualDesc, expectedDesc);
        }
        bool compatible = TypesAreAssignable(actual, expected);
        if (compatible && valueNode is not null) {
            compatible = IsReturnValueIdentityCompatible(expected, valueNode);
        }
        return compatible;
    }

    /// <summary>
    /// Checks a compatible-by-flat-kind return value against the declared return type's
    /// element/nominal identity — array element type (D-351), then struct nominal
    /// identity. Split from <see cref="ComputeReturnCompatibility"/> to keep that method's
    /// cognitive complexity under the analyser bar.
    /// </summary>
    private bool IsReturnValueIdentityCompatible(GrobType expected, Expression valueNode) {
        bool isArrayReturn = expected == GrobType.Array || expected == GrobType.NullableArray;
        if (isArrayReturn) {
            // Array element type (D-351): the flat GrobType.Array tag alone does not
            // distinguish int[] from string[], so a returned array whose element type
            // disagrees with the declared return element type must still be rejected.
            ArrayTypeDescriptor? expectedArrayDescriptor =
                _functionReturnArrayDescriptors.TryPeek(out ArrayTypeDescriptor? peekedArray) ? peekedArray : null;
            if (!ArrayElementAssignable(ArrayDescriptorOf(valueNode), expectedArrayDescriptor)) return false;
        }
        // Struct nominal identity (fix/compiler-struct-nominal-identity, Site C): the flat
        // GrobType.Struct tag alone does not distinguish the declared return type's struct
        // name from a differently-named struct actually returned.
        string? expectedNamedTypeName =
            _functionReturnStructNames.TryPeek(out string? name) ? name : null;
        return !IsStructNominalMismatch(expected, expectedNamedTypeName, valueNode);
    }

    /// <summary>
    /// Type-checks a <c>throw</c> statement's operand (Sprint 7 Increment A,
    /// D-274). The operand must resolve to <c>GrobError</c> or one of its ten
    /// leaves (D-284); anything else is E0014. Reuses the existing struct-type-name
    /// machinery (<see cref="GetFieldValueStructTypeName"/>) that field-value
    /// struct identity already relies on — no new resolution path.
    /// </summary>
    public override GrobType VisitThrow(ThrowStmt node) {
        GrobType operandType = Visit(node.Value);
        if (operandType == GrobType.Error) return GrobType.Unknown; // cascade suppression

        string? typeName = GetFieldValueStructTypeName(node.Value);
        if (typeName is null || !ExceptionHierarchy.IsSubtypeOf(typeName, ExceptionHierarchy.Root)) {
            EmitError(ErrorCatalog.E0014,
                $"'throw' operand must be a '{ExceptionHierarchy.Root}' or a subtype; " +
                $"found '{typeName ?? TypeName(operandType)}'.",
                node.Value.Range);
        }
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitAssignment(AssignmentStmt node) {
        if (node.Target is MemberAccessExpr memberTarget) {
            if (memberTarget.IsOptional) {
                EmitError(ErrorCatalog.E0206,
                    "Optional chaining '?.' cannot appear in an assignment target. Use '.' for field assignment.",
                    memberTarget.Range);
                return GrobType.Unknown;
            }
            GrobType fieldType = Visit(memberTarget);
            if (FindReadonlyRoot(memberTarget) is not null) {
                EmitError(ErrorCatalog.E0204,
                    "Cannot assign to field of `readonly` binding.",
                    memberTarget.Range);
            }
            GrobType rhsType = Visit(node.Value);
            if (fieldType is not (GrobType.Unknown or GrobType.Error) &&
                rhsType is not (GrobType.Unknown or GrobType.Error) &&
                !TypesAreAssignable(rhsType, fieldType)) {
                _diagnostics.Add(Diagnostic.Of(PickAssignabilityError(rhsType, fieldType),
                    node.Value.Range,
                    $"Cannot assign '{TypeName(rhsType)}' to field of type '{TypeName(fieldType)}'."));
            }
            return GrobType.Unknown;
        }

        if (node.Target is IndexExpr indexTarget) return VisitIndexAssignmentTarget(node, indexTarget);

        if (node.Target is not IdentifierExpr target) {
            // Any other assignment-target shape (none exist in the current grammar).
            Visit(node.Target);
            Visit(node.Value);
            return GrobType.Unknown;
        }

        Symbol? symbol = TryResolveAndBindMutableTarget(target);
        if (symbol is null) {
            Visit(node.Value);
            return GrobType.Unknown;
        }

        GrobType valueType = Visit(node.Value);

        // Type-check: value must be assignable to the binding's declared type.
        // An Unknown target type (e.g. a not-yet-tracked array element binding) is
        // permissive — there is nothing concrete to check against.
        // An Unknown value type (e.g. a lambda parameter or a deferred inference) is
        // also permissive — it could be the right type at runtime; the VM validates.
        if (symbol.Type != GrobType.Error && symbol.Type != GrobType.Unknown &&
            valueType != GrobType.Error && valueType != GrobType.Unknown &&
            !TypesAreAssignable(valueType, symbol.Type)) {
            EmitError(PickAssignabilityError(valueType, symbol.Type),
                $"Cannot assign value of type '{TypeName(valueType)}' to binding '{target.Name}' of type '{TypeName(symbol.Type)}'.",
                node.Value.Range);
        }

        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 9 Increment A4 (D-359): an <see cref="IndexExpr"/> target
    /// (<c>arr[i] += v</c>) is delegated to <see cref="VisitIndexCompoundAssignmentTarget"/>,
    /// mirroring <see cref="VisitIndexAssignmentTarget"/>'s shape. Sprint 9 Increment A4b
    /// (D-360): a <see cref="MemberAccessExpr"/> target (<c>obj.field += v</c>) is
    /// delegated to <see cref="VisitMemberCompoundAssignmentTarget"/> — the identifier-target
    /// path below is otherwise unchanged.
    /// </remarks>
    public override GrobType VisitCompoundAssignment(CompoundAssignmentStmt node) {
        if (node.Target is MemberAccessExpr memberTarget) {
            return VisitMemberCompoundAssignmentTarget(node, memberTarget);
        }

        if (node.Target is IndexExpr indexTarget) {
            return VisitIndexCompoundAssignmentTarget(node, indexTarget);
        }

        if (node.Target is not IdentifierExpr target) {
            Visit(node.Target);
            Visit(node.Value);
            return GrobType.Unknown;
        }

        Symbol? symbol = TryResolveAndBindMutableTarget(target);
        if (symbol is null) {
            Visit(node.Value);
            return GrobType.Unknown;
        }

        GrobType valueType = Visit(node.Value);
        EmitCompoundOperatorTypeCheck(symbol.Type, valueType, node.Operator, node.Range);
        return GrobType.Unknown;
    }

    /// <summary>
    /// Sprint 9 Increment A4 (D-359): array/map index compound-assignment target
    /// (<c>arr[i] += v</c>). Mirrors <see cref="VisitIndexAssignmentTarget"/>'s shape:
    /// visiting the target resolves the receiver's real element type (D-351) via the
    /// existing <c>VisitIndex</c> path (permissively <see cref="GrobType.Unknown"/> for a
    /// map receiver — the same honest gap D-350/D-351 already carry), <see
    /// cref="FindReadonlyRoot"/> raises <see cref="ErrorCatalog.E0204"/> exactly as the
    /// plain-assignment path does, and the operator/operand check reuses
    /// <see cref="EmitCompoundOperatorTypeCheck"/> — the identical rule the
    /// identifier-target path already applies.
    /// </summary>
    private GrobType VisitIndexCompoundAssignmentTarget(CompoundAssignmentStmt node, IndexExpr indexTarget) {
        GrobType elementType = Visit(indexTarget);
        GrobType valueType = Visit(node.Value);
        if (FindReadonlyRoot(indexTarget) is not null) {
            EmitError(ErrorCatalog.E0204,
                "Cannot mutate element of `readonly` binding.",
                node.Range);
        }
        if (elementType != GrobType.Unknown) {
            EmitCompoundOperatorTypeCheck(elementType, valueType, node.Operator, node.Range);
        }
        return GrobType.Unknown;
    }

    /// <summary>
    /// Sprint 9 Increment A4b (D-360): struct field compound-assignment target
    /// (<c>obj.field += v</c>), closing the sibling gap D-359 named. Mirrors
    /// <see cref="VisitIndexCompoundAssignmentTarget"/>'s shape: visiting the target
    /// resolves the field's real type via the pre-existing <c>VisitMemberAccess</c> path
    /// (<see cref="MemberAccessExpr.ResolvedFieldType"/>, live since Sprint 6 Increment C —
    /// permissively <see cref="GrobType.Unknown"/> when the receiver's own type could not be
    /// resolved, the same honest gap the index-target map case carries), <see
    /// cref="FindReadonlyRoot"/> raises <see cref="ErrorCatalog.E0204"/> exactly as the
    /// plain-assignment path does, and the operator/operand check reuses <see
    /// cref="EmitCompoundOperatorTypeCheck"/> — the identical rule every other
    /// compound-assignment target path already applies.
    /// </summary>
    private GrobType VisitMemberCompoundAssignmentTarget(CompoundAssignmentStmt node, MemberAccessExpr memberTarget) {
        if (memberTarget.IsOptional) {
            EmitError(ErrorCatalog.E0206,
                "Optional chaining '?.' cannot appear in an assignment target. Use '.' for field assignment.",
                memberTarget.Range);
            return GrobType.Unknown;
        }
        GrobType fieldType = Visit(memberTarget);
        GrobType valueType = Visit(node.Value);
        if (FindReadonlyRoot(memberTarget) is not null) {
            EmitError(ErrorCatalog.E0204,
                "Cannot mutate field of `readonly` binding.",
                node.Range);
        }
        if (fieldType != GrobType.Unknown) {
            EmitCompoundOperatorTypeCheck(fieldType, valueType, node.Operator, node.Range);
        }
        return GrobType.Unknown;
    }

    /// <summary>
    /// The compound-assignment operator/operand validity rule shared by the
    /// identifier-target and index-target paths — arithmetic operators need matching
    /// int/float (with int-to-float widening either way), and <c>+=</c> also accepts
    /// <c>string</c>/<c>string</c> (concatenation). Skipped by callers when either side
    /// is already <see cref="GrobType.Error"/>.
    /// </summary>
    private void EmitCompoundOperatorTypeCheck(
            GrobType leftType, GrobType valueType, CompoundAssignmentOperator op, SourceRange range) {
        if (leftType == GrobType.Error || valueType == GrobType.Error) return;

        BinaryOperator binOp = CompoundOpToBinary(op);
        string opSym = OperatorSymbol(binOp);
        bool valid = (leftType == GrobType.Int && valueType == GrobType.Int) ||
                     (leftType == GrobType.Float && valueType == GrobType.Float) ||
                     (leftType == GrobType.Float && valueType == GrobType.Int) ||
                     (binOp == BinaryOperator.Add && leftType == GrobType.String && valueType == GrobType.String);
        if (!valid)
            EmitError(ErrorCatalog.E0002,
                $"Operator '{opSym}=' cannot be applied to types '{TypeName(leftType)}' and '{TypeName(valueType)}'.",
                range);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 9 Increment A4 (D-359): an <see cref="IndexExpr"/> target
    /// (<c>arr[i]++</c>/<c>--</c>) is delegated to <see cref="VisitIndexIncrementTarget"/>.
    /// Sprint 9 Increment A4b (D-360): a <see cref="MemberAccessExpr"/> target
    /// (<c>obj.field++</c>/<c>--</c>) is delegated to <see cref="VisitMemberIncrementTarget"/>.
    /// </remarks>
    public override GrobType VisitIncrement(IncrementStmt node) {
        if (node.Target is MemberAccessExpr memberTarget) {
            return VisitMemberIncrementTarget(node, memberTarget);
        }
        if (node.Target is IndexExpr indexTarget) {
            return VisitIndexIncrementTarget(node, indexTarget);
        }
        if (node.Target is not IdentifierExpr target) {
            Visit(node.Target);
            return GrobType.Unknown;
        }
        return VisitIdentifierIncrementTarget(node, target);
    }

    /// <summary>
    /// The pre-existing identifier-target increment/decrement rule — extracted verbatim
    /// from <see cref="VisitIncrement"/> so the new <see cref="IndexExpr"/> dispatch above
    /// does not push the dispatcher itself over the analyser's complexity bar.
    /// </summary>
    private GrobType VisitIdentifierIncrementTarget(IncrementStmt node, IdentifierExpr target) {
        Symbol? symbol = LookupSymbol(target.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001,
                $"Undefined identifier '{target.Name}'.", target.Range);
            target.ResolvedType = GrobType.Error;
            target.Declaration = UnresolvedDecl.Instance;
            return GrobType.Unknown;
        }

        if (symbol.DeclarationNode is ConstDecl)
            EmitError(ErrorCatalog.E0201,
                $"Cannot apply '{(node.Kind == IncrementKind.Increment ? "++" : "--")}' to 'const' binding '{target.Name}'.",
                target.Range);
        else if (symbol.DeclarationNode is ReadonlyDecl)
            EmitError(ErrorCatalog.E0202,
                $"Cannot apply '{(node.Kind == IncrementKind.Increment ? "++" : "--")}' to 'readonly' binding '{target.Name}'.",
                target.Range);

        target.ResolvedType = symbol.Type;
        target.Declaration = symbol.DeclarationNode;

        if (symbol.Type == GrobType.Float) {
            EmitError(ErrorCatalog.E0002,
                $"Operator '{(node.Kind == IncrementKind.Increment ? "++" : "--")}' cannot be applied to type 'float'.",
                node.Range);
        } else if (symbol.Type != GrobType.Int && symbol.Type != GrobType.Error) {
            EmitError(ErrorCatalog.E0002,
                $"Operator '{(node.Kind == IncrementKind.Increment ? "++" : "--")}' cannot be applied to type '{TypeName(symbol.Type)}'.",
                node.Range);
        }

        return GrobType.Unknown;
    }

    /// <summary>
    /// Sprint 9 Increment A4 (D-359): array/map index increment/decrement target
    /// (<c>arr[i]++</c>/<c>--</c>). Mirrors the identifier-target int-only rule, except a
    /// map receiver's <see cref="GrobType.Unknown"/> element stays permissive (D-350's
    /// established map-write gap) rather than being rejected — the same latitude
    /// <see cref="VisitIndexCompoundAssignmentTarget"/> gives compound assignment.
    /// </summary>
    private GrobType VisitIndexIncrementTarget(IncrementStmt node, IndexExpr indexTarget) {
        GrobType elementType = Visit(indexTarget);
        if (FindReadonlyRoot(indexTarget) is not null) {
            EmitError(ErrorCatalog.E0204,
                "Cannot mutate element of `readonly` binding.",
                node.Range);
        }

        string opSym = node.Kind == IncrementKind.Increment ? "++" : "--";
        if (elementType == GrobType.Float) {
            EmitError(ErrorCatalog.E0002,
                $"Operator '{opSym}' cannot be applied to type 'float'.", node.Range);
        } else if (elementType != GrobType.Int && elementType != GrobType.Unknown && elementType != GrobType.Error) {
            EmitError(ErrorCatalog.E0002,
                $"Operator '{opSym}' cannot be applied to type '{TypeName(elementType)}'.", node.Range);
        }

        return GrobType.Unknown;
    }

    /// <summary>
    /// Sprint 9 Increment A4b (D-360): struct field increment/decrement target
    /// (<c>obj.field++</c>/<c>--</c>). Mirrors <see cref="VisitIndexIncrementTarget"/>'s
    /// int-only rule, including the same <see cref="GrobType.Unknown"/> latitude for a
    /// field whose type could not be resolved because the receiver's own type is unknown.
    /// </summary>
    private GrobType VisitMemberIncrementTarget(IncrementStmt node, MemberAccessExpr memberTarget) {
        if (memberTarget.IsOptional) {
            EmitError(ErrorCatalog.E0206,
                "Optional chaining '?.' cannot appear in an assignment target. Use '.' for field assignment.",
                memberTarget.Range);
            return GrobType.Unknown;
        }
        GrobType fieldType = Visit(memberTarget);
        if (FindReadonlyRoot(memberTarget) is not null) {
            EmitError(ErrorCatalog.E0204,
                "Cannot mutate field of `readonly` binding.",
                node.Range);
        }

        string opSym = node.Kind == IncrementKind.Increment ? "++" : "--";
        if (fieldType == GrobType.Float) {
            EmitError(ErrorCatalog.E0002,
                $"Operator '{opSym}' cannot be applied to type 'float'.", node.Range);
        } else if (fieldType != GrobType.Int && fieldType != GrobType.Unknown && fieldType != GrobType.Error) {
            EmitError(ErrorCatalog.E0002,
                $"Operator '{opSym}' cannot be applied to type '{TypeName(fieldType)}'.", node.Range);
        }

        return GrobType.Unknown;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Looks up <paramref name="target"/> as a mutable binding. On success emits any
    /// immutability diagnostics (E0201/E0202) and sets
    /// <see cref="IdentifierExpr.ResolvedType"/>/<see cref="IdentifierExpr.Declaration"/>.
    /// Returns <c>null</c> (with E1001 already emitted) when the symbol is undefined.
    /// </summary>
    private Symbol? TryResolveAndBindMutableTarget(IdentifierExpr target) {
        Symbol? symbol = LookupSymbol(target.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001,
                $"Undefined identifier '{target.Name}'. Use ':=' to declare a new variable.",
                target.Range);
            target.ResolvedType = GrobType.Error;
            target.Declaration = UnresolvedDecl.Instance;
            return null;
        }

        if (symbol.DeclarationNode is ConstDecl)
            EmitError(ErrorCatalog.E0201,
                $"Cannot reassign 'const' binding '{target.Name}'.", target.Range);
        else if (symbol.DeclarationNode is ReadonlyDecl)
            EmitError(ErrorCatalog.E0202,
                $"Cannot reassign 'readonly' binding '{target.Name}'.", target.Range);
        else if (symbol.DeclarationNode is ForInStmt)
            EmitError(ErrorCatalog.E0504,
                $"Cannot reassign 'for...in' iteration variable '{target.Name}'; it is immutable within the loop body.",
                target.Range);

        target.ResolvedType = symbol.Type;
        target.Declaration = symbol.DeclarationNode;
        return symbol;
    }

    /// <summary>
    /// Sprint 9 Increment A2 (D-350) / A3 (D-351): array/map index-store target
    /// (<c>arr[i] = v</c>). Visiting the target cascades into the pre-existing
    /// <c>VisitIdentifier</c> path on the root identifier (or a nested <c>VisitIndex</c>
    /// read for a chained target like <c>matrix[r][c]</c>), setting
    /// <c>ResolvedType</c>/<c>Declaration</c> there exactly as the read side (D-348)
    /// already does, and now also resolving the receiver's real element type (D-351) —
    /// closing the A2 gap: <c>arr[0] = "x"</c> on an <c>int[]</c> is a mismatch. A map
    /// target (or any array whose element type could not be determined) resolves the
    /// target's element type as <see cref="GrobType.Unknown"/> and stays permissive,
    /// exactly as before.
    /// </summary>
    private GrobType VisitIndexAssignmentTarget(AssignmentStmt node, IndexExpr indexTarget) {
        GrobType elementType = Visit(indexTarget);
        GrobType valueType = Visit(node.Value);
        if (FindReadonlyRoot(indexTarget) is not null) {
            EmitError(ErrorCatalog.E0204,
                "Cannot mutate element of `readonly` binding.",
                node.Range);
        }
        if (elementType != GrobType.Unknown && elementType != GrobType.Error && valueType != GrobType.Error &&
                !IsIndexRhsCompatible(elementType, indexTarget, node.Value, valueType)) {
            EmitError(ErrorCatalog.E0001,
                $"Cannot assign value of type '{TypeName(valueType)}' to array element of type '{TypeName(elementType)}'.",
                node.Value.Range);
        }
        return GrobType.Unknown;
    }

    /// <summary>
    /// Checks an index-write's right-hand side against the receiver's element type
    /// (D-351) — flat kind, array element type (for a <c>T[][]</c> write), then struct
    /// nominal identity. Split from <see cref="VisitIndexAssignmentTarget"/> to keep that
    /// method's cognitive complexity under the analyser bar.
    /// </summary>
    private bool IsIndexRhsCompatible(GrobType elementType, IndexExpr indexTarget, Expression valueExpr, GrobType valueType) {
        ArrayTypeDescriptor? receiverElementDescriptor = ArrayDescriptorOf(indexTarget.Target);
        bool compatible = TypesAreAssignable(valueType, elementType);
        if (compatible && elementType is GrobType.Array or GrobType.NullableArray) {
            compatible = ArrayElementAssignable(ArrayDescriptorOf(valueExpr), receiverElementDescriptor?.ElementArrayDescriptor);
        }
        if (compatible && IsStructNominalMismatch(elementType, receiverElementDescriptor?.ElementNamedTypeName, valueExpr)) {
            compatible = false;
        }
        return compatible;
    }

    private static BinaryOperator CompoundOpToBinary(CompoundAssignmentOperator op) => op switch {
        CompoundAssignmentOperator.PlusAssign => BinaryOperator.Add,
        CompoundAssignmentOperator.MinusAssign => BinaryOperator.Subtract,
        CompoundAssignmentOperator.StarAssign => BinaryOperator.Multiply,
        CompoundAssignmentOperator.SlashAssign => BinaryOperator.Divide,
        CompoundAssignmentOperator.PercentAssign => BinaryOperator.Modulo,
        _ => throw new GrobInternalException($"Unknown compound assignment operator: {op}"),
    };

    /// <summary>
    /// Walks the receiver chain of an assignment target — through any mix of
    /// <see cref="MemberAccessExpr"/> and <see cref="IndexExpr"/> nesting (Sprint 9
    /// Increment A2, D-350) — to find the root identifier. Returns the
    /// <see cref="ReadonlyDecl"/> if the root binding is readonly (D-291 deep
    /// immutability); otherwise returns <see langword="null"/>.
    /// </summary>
    private static ReadonlyDecl? FindReadonlyRoot(Expression target) {
        Expression current = target;
        while (true) {
            current = current switch {
                MemberAccessExpr ma => ma.Target,
                IndexExpr idx => idx.Target,
                _ => current,
            };
            if (current is not (MemberAccessExpr or IndexExpr)) break;
        }
        return current is IdentifierExpr { Declaration: ReadonlyDecl ro } ? ro : null;
    }
}
