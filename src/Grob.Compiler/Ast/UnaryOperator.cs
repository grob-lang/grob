namespace Grob.Compiler.Ast;

/// <summary>The unary operators that appear in <see cref="UnaryExpr"/>.</summary>
public enum UnaryOperator {
    /// <summary>Arithmetic negation: <c>-x</c>.</summary>
    Negate,

    /// <summary>Logical NOT: <c>!x</c>.</summary>
    Not,
}
