using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 12: wraps a call's receiver in a nullable binding: <c>x.m()</c> becomes
/// <c>mn: T? := x</c> before the statement and <c>mn?.m()</c> in it (R-66 beyond strings, and the
/// natives a <c>?.</c> call reaches). The receiver's type <c>T</c> is read from its declaration's
/// own syntax, an annotation or a literal initialiser, so a receiver whose declaration says
/// neither is left alone, and so is one whose name has declarations that disagree.
/// </summary>
internal sealed class NullableReceiver : IMutationOperator {
    private const string BindingName = "mn";

    /// <inheritdoc/>
    public string Name => "nullable-receiver";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((CallExpr call, _) in context.Of<CallExpr>()) {
            if (call.Callee is not MemberAccessExpr { IsOptional: false, Target: IdentifierExpr receiver }) continue;

            string? type = DeclaredType(context, receiver.Name);
            AstNode? statement = context.EnclosingStatement(call);
            int token = context.FirstToken(receiver);
            if (type is null || statement is null || token < 0 || context.Tokens[token + 1].Kind != TokenKind.Dot) continue;

            int statementStart = context.Tokens.Start(context.FirstToken(statement));
            candidates.Add([
                SourceEdit.Insert(statementStart, $"{BindingName}: {type}? := {receiver.Name}\n"),
                SourceEdit.Replace(context.Tokens.Start(token), context.Tokens.End(token), BindingName),
                SourceEdit.Replace(context.Tokens.Start(token + 1), context.Tokens.End(token + 1), "?."),
            ]);
        }
        return candidates;
    }

    // The tree has no scopes, so the receiver's own declaration cannot be told from another of the
    // same name. A type is given only when every declaration of the name gives it; a declaration
    // that gives none, such as a loop variable, or two that disagree leave the receiver alone. A
    // lambda's parameter gives none: the binding goes before the statement, outside its scope.
    private static string? DeclaredType(MutationContext context, string name) {
        string? found = null;
        foreach (object node in context.Nodes.Select(entry => entry.Node).Where(node => Declares(node, name))) {
            string? type = TypeOf(context, node);
            if (type is null || (found is not null && found != type)) return null;
            found = type;
        }
        return found;
    }

    private static bool Declares(object node, string name) => node switch {
        VarDeclStmt v => v.Name == name,
        ConstDecl c => c.Name == name,
        ReadonlyDecl r => r.Name == name,
        Parameter p => p.Name == name,
        FnDecl f => f.Name == name,
        ForInStmt f => f.Variables.Contains(name),
        CatchClause c => c.ExceptionVariable == name,
        _ => false,
    };

    private static string? TypeOf(MutationContext context, object declaration) => declaration switch {
        VarDeclStmt v => v.AnnotatedType is { IsNullable: false } annotated ? TypeText(context, annotated) : LiteralType(v.Initializer),
        Parameter { Type: { IsNullable: false } annotated } p when context.ParentOf(p) is not LambdaExpr => TypeText(context, annotated),
        _ => null,
    };

    private static string? TypeText(MutationContext context, TypeRef type) {
        int first = context.FirstToken(type);
        int last = first < 0 ? -1 : context.Tokens.TypeLast(first);
        return last < first ? null : context.Tokens.Text(context.Tokens.Start(first), context.Tokens.End(last));
    }

    private static string? LiteralType(Expression value) => value switch {
        IntLiteralExpr => "int",
        FloatLiteralExpr => "float",
        StringLiteralExpr or InterpolatedStringExpr { Parts: [] or [StringTextPart] } => "string",
        BoolLiteralExpr => "bool",
        _ => null,
    };
}
