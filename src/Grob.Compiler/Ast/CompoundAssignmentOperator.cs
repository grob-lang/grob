namespace Grob.Compiler.Ast;

/// <summary>The compound-assignment operators that appear in <see cref="CompoundAssignmentStmt"/>.</summary>
public enum CompoundAssignmentOperator {
    /// <summary><c>+=</c></summary>
    PlusAssign,

    /// <summary><c>-=</c></summary>
    MinusAssign,

    /// <summary><c>*=</c></summary>
    StarAssign,

    /// <summary><c>/=</c></summary>
    SlashAssign,

    /// <summary><c>%=</c></summary>
    PercentAssign,
}
