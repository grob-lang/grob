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
        if (node.Value is not null) Visit(node.Value);
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

        Symbol? symbol = LookupSymbol(target.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001,
                $"Undefined identifier '{target.Name}'. Use ':=' to declare a new variable.",
                target.Range);
            target.ResolvedType = GrobType.Error;
            target.Declaration = UnresolvedDecl.Instance;
            Visit(node.Value);
            return GrobType.Unknown;
        }

        // Reject reassignment of immutable bindings.
        if (symbol.DeclarationNode is ConstDecl)
            EmitError(ErrorCatalog.E0201,
                $"Cannot reassign 'const' binding '{target.Name}'.", target.Range);
        else if (symbol.DeclarationNode is ReadonlyDecl)
            EmitError(ErrorCatalog.E0202,
                $"Cannot reassign 'readonly' binding '{target.Name}'.", target.Range);

        target.ResolvedType = symbol.Type;
        target.Declaration = symbol.DeclarationNode;

        GrobType valueType = Visit(node.Value);

        // Type-check: value must be assignable to the binding's declared type.
        if (symbol.Type != GrobType.Error && valueType != GrobType.Error &&
            !TypesAreAssignable(valueType, symbol.Type)) {
            EmitError(ErrorCatalog.E0001,
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

        Symbol? symbol = LookupSymbol(target.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001,
                $"Undefined identifier '{target.Name}'. Use ':=' to declare a new variable.",
                target.Range);
            target.ResolvedType = GrobType.Error;
            target.Declaration = UnresolvedDecl.Instance;
            Visit(node.Value);
            return GrobType.Unknown;
        }

        if (symbol.DeclarationNode is ConstDecl)
            EmitError(ErrorCatalog.E0201,
                $"Cannot reassign 'const' binding '{target.Name}'.", target.Range);
        else if (symbol.DeclarationNode is ReadonlyDecl)
            EmitError(ErrorCatalog.E0202,
                $"Cannot reassign 'readonly' binding '{target.Name}'.", target.Range);

        target.ResolvedType = symbol.Type;
        target.Declaration = symbol.DeclarationNode;

        GrobType valueType = Visit(node.Value);

        // Validate the underlying binary operator's type rules using the target's resolved type.
        if (symbol.Type != GrobType.Error && valueType != GrobType.Error) {
            BinaryOperator op = CompoundOpToBinary(node.Operator);
            // Inline the arithmetic-type-rule check: reuse the same logic as VisitBinary.
            string opSym = OperatorSymbol(op);
            bool valid = (symbol.Type == GrobType.Int && valueType == GrobType.Int) ||
                         (symbol.Type == GrobType.Float && valueType == GrobType.Float) ||
                         (symbol.Type == GrobType.Float && valueType == GrobType.Int) ||
                         (symbol.Type == GrobType.Int && valueType == GrobType.Float) ||
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

    private static BinaryOperator CompoundOpToBinary(CompoundAssignmentOperator op) => op switch {
        CompoundAssignmentOperator.PlusAssign => BinaryOperator.Add,
        CompoundAssignmentOperator.MinusAssign => BinaryOperator.Subtract,
        CompoundAssignmentOperator.StarAssign => BinaryOperator.Multiply,
        CompoundAssignmentOperator.SlashAssign => BinaryOperator.Divide,
        CompoundAssignmentOperator.PercentAssign => BinaryOperator.Modulo,
        _ => throw new GrobInternalException($"Unknown compound assignment operator: {op}"),
    };
}
