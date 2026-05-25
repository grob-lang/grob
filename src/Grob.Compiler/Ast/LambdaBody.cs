using System.Diagnostics.CodeAnalysis;

namespace Grob.Compiler.Ast;

/// <summary>The body of a <see cref="LambdaExpr"/> — either an expression or a block.</summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Sum-type marker for LambdaBlockBody | LambdaExpressionBody.")]
public abstract record LambdaBody;
