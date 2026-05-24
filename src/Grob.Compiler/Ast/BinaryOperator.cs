namespace Grob.Compiler.Ast;

/// <summary>The binary operators that appear in <see cref="BinaryExpr"/>.</summary>
public enum BinaryOperator {
    /// <summary><c>+</c></summary>
    Add,

    /// <summary><c>-</c></summary>
    Subtract,

    /// <summary><c>*</c></summary>
    Multiply,

    /// <summary><c>/</c></summary>
    Divide,

    /// <summary><c>%</c></summary>
    Modulo,

    /// <summary><c>==</c></summary>
    Equal,

    /// <summary><c>!=</c></summary>
    NotEqual,

    /// <summary><c>&lt;</c></summary>
    Less,

    /// <summary><c>&lt;=</c></summary>
    LessEqual,

    /// <summary><c>&gt;</c></summary>
    Greater,

    /// <summary><c>&gt;=</c></summary>
    GreaterEqual,

    /// <summary><c>&amp;&amp;</c> — short-circuit logical AND.</summary>
    And,

    /// <summary><c>||</c> — short-circuit logical OR.</summary>
    Or,

    /// <summary><c>??</c> — nil-coalescing.</summary>
    NilCoalesce,
}
