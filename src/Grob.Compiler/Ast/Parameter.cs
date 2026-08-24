using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A formal parameter — used in <see cref="FnDecl"/> and <see cref="LambdaExpr"/>.
/// Not used by <see cref="ParamDecl"/> (a top-level <c>param</c> declaration),
/// which carries its name/type/default directly since its type annotation is
/// mandatory, unlike this record's nullable <see cref="Type"/>.
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
