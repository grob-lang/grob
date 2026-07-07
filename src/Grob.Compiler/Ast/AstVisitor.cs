namespace Grob.Compiler.Ast;

/// <summary>
/// Generic visitor over the Grob AST. Subclasses implement the
/// <c>VisitXxx</c> hooks for the node kinds they care about and override
/// <see cref="DefaultVisit(AstNode)"/> to handle anything they leave to the
/// default.
/// </summary>
/// <typeparam name="T">The result type produced by each visit.</typeparam>
/// <remarks>
/// The three <c>VisitErrorXxx</c> hooks are <see langword="abstract"/> by
/// design — §29.2 of the language fundamentals requires every AST traversal
/// to handle error nodes, and we enforce that with the type system rather
/// than discipline. A new visitor cannot compile without supplying all
/// three.
/// </remarks>
public abstract class AstVisitor<T> {
    /// <summary>Dispatches to the appropriate <c>VisitXxx</c> hook for <paramref name="node"/>.</summary>
    /// <param name="node">The node to visit. Must not be <see langword="null"/>.</param>
    /// <returns>The value produced by the dispatched hook.</returns>
    public T Visit(AstNode node) {
        ArgumentNullException.ThrowIfNull(node);
        return node.Accept(this);
    }

    // -----------------------------------------------------------------------
    // Expressions
    // -----------------------------------------------------------------------

    /// <summary>Hook for <see cref="IntLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitIntLiteral(IntLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="FloatLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitFloatLiteral(FloatLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="StringLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitStringLiteral(StringLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="RawStringLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitRawStringLiteral(RawStringLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="InterpolatedStringExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitInterpolatedString(InterpolatedStringExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="RegexLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitRegexLiteral(RegexLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="BoolLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitBoolLiteral(BoolLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="NilLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitNilLiteral(NilLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="IdentifierExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitIdentifier(IdentifierExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="UnaryExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitUnary(UnaryExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="BinaryExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitBinary(BinaryExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="GroupingExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitGrouping(GroupingExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="TernaryExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitTernary(TernaryExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="SwitchExprNode"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitSwitchExpr(SwitchExprNode node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ArrayLiteralExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitArrayLiteral(ArrayLiteralExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="IndexExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitIndex(IndexExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="MemberAccessExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitMemberAccess(MemberAccessExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="CallExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitCall(CallExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="LambdaExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitLambda(LambdaExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="NumericRangeExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitNumericRange(NumericRangeExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="StructConstructionExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitStructConstruction(StructConstructionExpr node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="AnonStructExpr"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitAnonStruct(AnonStructExpr node) => DefaultVisit(node);

    /// <summary>
    /// Hook for <see cref="ErrorExpr"/>. Abstract by design — every visitor
    /// must handle parser error placeholders.
    /// </summary>
    public abstract T VisitErrorExpr(ErrorExpr node);

    // -----------------------------------------------------------------------
    // Statements
    // -----------------------------------------------------------------------

    /// <summary>Hook for <see cref="BlockStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitBlock(BlockStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="VarDeclStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitVarDecl(VarDeclStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="AssignmentStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitAssignment(AssignmentStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="CompoundAssignmentStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitCompoundAssignment(CompoundAssignmentStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="IncrementStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitIncrement(IncrementStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ExpressionStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitExpressionStmt(ExpressionStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="IfStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitIf(IfStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="WhileStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitWhile(WhileStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ForInStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitForIn(ForInStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="SelectStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitSelect(SelectStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="BreakStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitBreak(BreakStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ContinueStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitContinue(ContinueStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ReturnStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitReturn(ReturnStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="TryStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitTry(TryStmt node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ThrowStmt"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitThrow(ThrowStmt node) => DefaultVisit(node);

    /// <summary>
    /// Hook for <see cref="ErrorStmt"/>. Abstract by design — every visitor
    /// must handle parser error placeholders.
    /// </summary>
    public abstract T VisitErrorStmt(ErrorStmt node);

    // -----------------------------------------------------------------------
    // Declarations
    // -----------------------------------------------------------------------

    /// <summary>Hook for <see cref="FnDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitFnDecl(FnDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="TypeDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitTypeDecl(TypeDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ParamBlockDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitParamBlockDecl(ParamBlockDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ImportDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitImportDecl(ImportDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ConstDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitConstDecl(ConstDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="ReadonlyDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitReadonlyDecl(ReadonlyDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="BuiltinDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitBuiltinDecl(BuiltinDecl node) => DefaultVisit(node);

    /// <summary>Hook for <see cref="UnresolvedDecl"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitUnresolvedDecl(UnresolvedDecl node) => DefaultVisit(node);

    /// <summary>
    /// Hook for <see cref="ErrorDecl"/>. Abstract by design — every visitor
    /// must handle parser error placeholders.
    /// </summary>
    public abstract T VisitErrorDecl(ErrorDecl node);

    // -----------------------------------------------------------------------
    // Root
    // -----------------------------------------------------------------------

    /// <summary>Hook for <see cref="CompilationUnit"/>. Defaults to <see cref="DefaultVisit(AstNode)"/>.</summary>
    public virtual T VisitCompilationUnit(CompilationUnit node) => DefaultVisit(node);

    // -----------------------------------------------------------------------
    // Fallback
    // -----------------------------------------------------------------------

    /// <summary>
    /// Catch-all hook invoked from every non-error <c>VisitXxx</c> that the
    /// concrete visitor has not overridden. Throws by default so that
    /// missing handlers fail loudly during development; override to provide
    /// a sensible neutral value.
    /// </summary>
    /// <param name="node">The node that hit the fallback.</param>
    /// <returns>The neutral result for nodes the visitor does not explicitly handle.</returns>
    /// <exception cref="NotSupportedException">Thrown by the default implementation.</exception>
    protected virtual T DefaultVisit(AstNode node) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not handle nodes of type {node.GetType().Name}.");
}
