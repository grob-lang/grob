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
        // Check for same-scope re-declaration (E1102) before visiting the initializer
        // so we don't clobber a valid symbol if the name is already in this scope.
        if (_scopes.Peek().ContainsKey(node.Name)) {
            EmitError(ErrorCatalog.E1102,
                $"'{node.Name}' is already declared in this scope. Use '=' to reassign.",
                node.Range);
            // Still visit the initializer for cascade suppression on its sub-expressions.
            Visit(node.Initializer);
            return GrobType.Unknown;
        }

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

        GrobType expected = _functionReturnTypes.Peek();

        // A bare 'return' yields nil. Since 'void' is not a user-declarable return
        // type (only print() is void — §"print() and void"), a bare return must
        // still satisfy the declared type: it is accepted only by a nullable or nil
        // return type, and is E0005 against a non-nullable one.
        GrobType actual = node.Value is not null ? Visit(node.Value) : GrobType.Nil;

        // An Unknown declared return type (a deferred type) is permissive; cascade
        // suppression covers an already-errored value.
        if (expected != GrobType.Unknown && actual != GrobType.Error &&
            !TypesAreAssignable(actual, expected)) {
            EmitError(ErrorCatalog.E0005,
                node.Value is not null
                    ? $"Cannot return a value of type '{TypeName(actual)}' from a function declared to return '{TypeName(expected)}'."
                    : $"A bare 'return' yields 'nil', which is not assignable to the declared return type '{TypeName(expected)}'.",
                node.Value is not null ? node.Value.Range : node.Range);
        }
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitAssignment(AssignmentStmt node) {
        if (node.Target is not IdentifierExpr target) {
            // Field/index targets are deferred (Sprint 6 / collections).
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

        // ++/-- is int-only; float is a compile error (E0002).
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
}
