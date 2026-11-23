using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 12: wraps a call's receiver in a nullable binding: <c>x.m()</c> becomes
/// <c>mn: T? := x</c> before the statement and <c>mn?.m()</c> in it (R-66 beyond strings, and the
/// natives a <c>?.</c> call reaches). The receiver's type <c>T</c> is read from its declaration's
/// own syntax, an annotation or a literal initialiser, so a receiver whose declaration says
/// neither is left alone.
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

    private static string? DeclaredType(MutationContext context, string name) {
        foreach (AstEntry entry in context.Nodes) {
            string? type = entry.Node switch {
                VarDeclStmt v when v.Name == name => v.AnnotatedType is { IsNullable: false } annotated
                    ? TypeText(context, annotated)
                    : LiteralType(v.Initializer),
                Parameter { Type: { IsNullable: false } annotated } p when p.Name == name => TypeText(context, annotated),
                _ => null,
            };
            if (type is not null) return type;
        }
        return null;
    }

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
