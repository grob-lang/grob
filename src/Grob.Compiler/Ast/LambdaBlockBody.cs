namespace Grob.Compiler.Ast;

/// <summary>A lambda body of the form <c>x =&gt; { ... }</c>.</summary>
/// <param name="Block">The block executed as the lambda body.</param>
public sealed record LambdaBlockBody(BlockStmt Block) : LambdaBody;
