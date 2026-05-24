namespace Grob.Compiler.Ast;

/// <summary>The direction of an <see cref="IncrementStmt"/>.</summary>
public enum IncrementKind {
    /// <summary><c>++</c></summary>
    Increment,

    /// <summary><c>--</c></summary>
    Decrement,
}
