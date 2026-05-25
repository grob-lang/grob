using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A formal parameter — used in <see cref="FnDecl"/>, <see cref="LambdaExpr"/>,
/// and <see cref="ParamBlockDecl"/>.
/// </summary>
/// <param name="Range">Source range covered by the parameter declaration.</param>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The declared parameter type, or <see langword="null"/> when inferred (lambdas).</param>
/// <param name="DefaultValue">The default value expression, or <see langword="null"/> when the parameter is required.</param>
public sealed record Parameter(
    SourceRange Range,
    string Name,
    TypeRef? Type,
    Expression? DefaultValue);
