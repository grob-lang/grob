namespace Grob.Compiler.Ast;

/// <summary>
/// A read-only walker over the AST. Recursively visits every child of every
/// node it encounters and returns <see cref="Unit"/>. Subclasses override
/// the hooks they care about and call <c>base.VisitXxx(node)</c> to keep the
/// recursion going.
/// </summary>
/// <remarks>
/// Inherits the three abstract <c>VisitErrorXxx</c> hooks from
/// <see cref="AstVisitor{T}"/>; concrete walkers must still supply them. This
/// is the §29.2 enforcement — no traversal compiles without handling error
/// nodes.
/// </remarks>
public abstract class AstWalker : AstVisitor<Unit> {
    /// <inheritdoc/>
    protected override Unit DefaultVisit(AstNode node) => default;

    // -----------------------------------------------------------------------
    // Expressions with children
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override Unit VisitInterpolatedString(InterpolatedStringExpr node) {
        foreach (StringInterpolationPart part in node.Parts) {
            if (part is StringExpressionPart expr) {
                Visit(expr.Expression);
            }
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitUnary(UnaryExpr node) {
        Visit(node.Operand);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitBinary(BinaryExpr node) {
        Visit(node.Left);
        Visit(node.Right);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitGrouping(GroupingExpr node) {
        Visit(node.Inner);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitTernary(TernaryExpr node) {
        Visit(node.Condition);
        Visit(node.Then);
        Visit(node.Else);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitSwitchExpr(SwitchExprNode node) {
        Visit(node.Subject);
        foreach (SwitchArm arm in node.Arms) {
            switch (arm.Pattern) {
                case ValuePattern vp:
                    Visit(vp.Value);
                    break;
                case RelationalPattern rp:
                    Visit(rp.Operand);
                    break;
            }
            Visit(arm.Result);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitArrayLiteral(ArrayLiteralExpr node) {
        foreach (Expression element in node.Elements) {
            Visit(element);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitIndex(IndexExpr node) {
        Visit(node.Target);
        Visit(node.Index);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitMemberAccess(MemberAccessExpr node) {
        Visit(node.Target);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitCall(CallExpr node) {
        Visit(node.Callee);
        foreach (CallArgument arg in node.Arguments) {
            Visit(arg.Value);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitLambda(LambdaExpr node) {
        VisitParameterDefaults(node.Parameters);
        switch (node.Body) {
            case LambdaExpressionBody e:
                Visit(e.Expression);
                break;
            case LambdaBlockBody b:
                Visit(b.Block);
                break;
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitNumericRange(NumericRangeExpr node) {
        Visit(node.Start);
        Visit(node.End);
        if (node.Step is not null) {
            Visit(node.Step);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitStructConstruction(StructConstructionExpr node) {
        foreach (FieldInit field in node.Fields) {
            Visit(field.Value);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitAnonStruct(AnonStructExpr node) {
        foreach (FieldInit field in node.Fields) {
            Visit(field.Value);
        }
        return default;
    }

    // -----------------------------------------------------------------------
    // Statements with children
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override Unit VisitBlock(BlockStmt node) {
        foreach (Statement stmt in node.Statements) {
            Visit(stmt);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitVarDecl(VarDeclStmt node) {
        Visit(node.Initializer);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitAssignment(AssignmentStmt node) {
        Visit(node.Target);
        Visit(node.Value);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitCompoundAssignment(CompoundAssignmentStmt node) {
        Visit(node.Target);
        Visit(node.Value);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitIncrement(IncrementStmt node) {
        Visit(node.Target);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitExpressionStmt(ExpressionStmt node) {
        Visit(node.Expression);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitIf(IfStmt node) {
        Visit(node.Condition);
        Visit(node.Then);
        if (node.Else is not null) {
            Visit(node.Else);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitWhile(WhileStmt node) {
        Visit(node.Condition);
        Visit(node.Body);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitForIn(ForInStmt node) {
        Visit(node.Iterable);
        Visit(node.Body);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitSelect(SelectStmt node) {
        Visit(node.Subject);
        foreach (CaseClause c in node.Cases) {
            foreach (Expression pattern in c.Patterns) {
                Visit(pattern);
            }
            Visit(c.Body);
        }
        if (node.Default is not null) {
            Visit(node.Default);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitReturn(ReturnStmt node) {
        if (node.Value is not null) {
            Visit(node.Value);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitTry(TryStmt node) {
        Visit(node.Body);
        foreach (CatchClause c in node.Catches) {
            Visit(c.Body);
        }
        if (node.Finally is not null) {
            Visit(node.Finally);
        }
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitThrow(ThrowStmt node) {
        Visit(node.Value);
        return default;
    }

    // -----------------------------------------------------------------------
    // Declarations with children
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override Unit VisitFnDecl(FnDecl node) {
        VisitParameterDefaults(node.Parameters);
        Visit(node.Body);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitTypeDecl(TypeDecl node) {
#pragma warning disable S3267 // hot path: avoid per-visit Where iterator allocation
        foreach (TypeField field in node.Fields) {
            if (field.DefaultValue is not null) {
                Visit(field.DefaultValue);
            }
        }
#pragma warning restore S3267
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitParamBlockDecl(ParamBlockDecl node) {
        VisitParameterDefaults(node.Parameters);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitConstDecl(ConstDecl node) {
        Visit(node.Value);
        return default;
    }

    /// <inheritdoc/>
    public override Unit VisitReadonlyDecl(ReadonlyDecl node) {
        Visit(node.Value);
        return default;
    }

    // -----------------------------------------------------------------------
    // Root
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override Unit VisitCompilationUnit(CompilationUnit node) {
        foreach (AstNode item in node.TopLevel) {
            Visit(item);
        }
        return default;
    }

    private void VisitParameterDefaults(IReadOnlyList<Parameter> parameters) {
#pragma warning disable S3267 // hot path: avoid per-visit Where iterator allocation
        foreach (Parameter p in parameters) {
            if (p.DefaultValue is not null) {
                Visit(p.DefaultValue);
            }
        }
#pragma warning restore S3267
    }
}
