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
        (GrobType symbolType, FunctionTypeDescriptor? symbolDesc) =
            ResolveBindingFull(node.AnnotatedType, initType, initDesc, node.Initializer.Range, node.Initializer);
        RegisterSymbol(node.Name, symbolType, node.Range.Start, node, functionDescriptor: symbolDesc);
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
            // Struct nominal identity (fix/compiler-struct-nominal-identity, Site C): the
            // flat GrobType.Struct tag alone does not distinguish the declared return
            // type's struct name from a differently-named struct actually returned.
            string? expectedNamedTypeName =
                _functionReturnStructNames.TryPeek(out string? name) ? name : null;
            if (IsStructNominalMismatch(expected, expectedNamedTypeName, valueNode)) compatible = false;
        }
        return compatible;
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

        if (node.Target is not IdentifierExpr target) {
            // Index targets are deferred (collections sprint).
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
    public override GrobType VisitCompoundAssignment(CompoundAssignmentStmt node) {
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

        // Validate the underlying binary operator's type rules using the target's resolved type.
        if (symbol.Type != GrobType.Error && valueType != GrobType.Error) {
            BinaryOperator op = CompoundOpToBinary(node.Operator);
            // Inline the arithmetic-type-rule check: reuse the same logic as VisitBinary.
            string opSym = OperatorSymbol(op);
            bool valid = (symbol.Type == GrobType.Int && valueType == GrobType.Int) ||
                         (symbol.Type == GrobType.Float && valueType == GrobType.Float) ||
                         (symbol.Type == GrobType.Float && valueType == GrobType.Int) ||
                         (op == BinaryOperator.Add && symbol.Type == GrobType.String && valueType == GrobType.String);
            if (!valid)
                EmitError(ErrorCatalog.E0002,
                    $"Operator '{opSym}=' cannot be applied to types '{TypeName(symbol.Type)}' and '{TypeName(valueType)}'.",
                    node.Range);
        }

        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitIncrement(IncrementStmt node) {
        if (node.Target is not IdentifierExpr target) {
            Visit(node.Target);
            return GrobType.Unknown;
        }

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

    private static BinaryOperator CompoundOpToBinary(CompoundAssignmentOperator op) => op switch {
        CompoundAssignmentOperator.PlusAssign => BinaryOperator.Add,
        CompoundAssignmentOperator.MinusAssign => BinaryOperator.Subtract,
        CompoundAssignmentOperator.StarAssign => BinaryOperator.Multiply,
        CompoundAssignmentOperator.SlashAssign => BinaryOperator.Divide,
        CompoundAssignmentOperator.PercentAssign => BinaryOperator.Modulo,
        _ => throw new GrobInternalException($"Unknown compound assignment operator: {op}"),
    };

    /// <summary>
    /// Walks the receiver chain of a member-access expression to find the root identifier.
    /// Returns the <see cref="ReadonlyDecl"/> if the root binding is readonly (D-291 deep
    /// immutability); otherwise returns <see langword="null"/>.
    /// </summary>
    private static ReadonlyDecl? FindReadonlyRoot(MemberAccessExpr ma) {
        Expression current = ma.Target;
        while (current is MemberAccessExpr inner) {
            current = inner.Target;
        }
        return current is IdentifierExpr { Declaration: ReadonlyDecl ro } ? ro : null;
    }
}
