using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler.Tests;

/// <summary>Shared helpers for the AST test suite.</summary>
internal static class AstTestHelpers {
    /// <summary>A throwaway source range to attach to synthetic AST nodes.</summary>
    public static SourceRange R => SourceRange.Unknown;

    /// <summary>A throwaway diagnostic for ErrorExpr/ErrorStmt/ErrorDecl construction.</summary>
    public static Diagnostic ErrDiag(string code = "E9999") =>
        new(code, "synthetic", SourceRange.Unknown, Severity.Error);

    /// <summary>Build an identifier expression with the given name.</summary>
    public static IdentifierExpr Id(string name) => new(R, name);

    /// <summary>Build an int literal.</summary>
    public static IntLiteralExpr Int(long v) => new(R, v);
}
