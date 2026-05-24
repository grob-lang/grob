namespace Grob.Compiler.Ast;

/// <summary>A lambda body of the form <c>x =&gt; expr</c>.</summary>
/// <param name="Expression">The expression evaluated as the lambda result.</param>
public sealed record LambdaExpressionBody(Expression Expression) : LambdaBody;
