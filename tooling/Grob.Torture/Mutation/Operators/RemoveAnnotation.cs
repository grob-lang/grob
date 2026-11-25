using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 1: removes a type annotation from a binding, a parameter or a field, so the checker
/// must infer a type it was told (R-57, R-63: values of unknown type).
/// </summary>
internal sealed class RemoveAnnotation : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "remove-annotation";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach (AstEntry entry in context.Nodes) {
            TypeRef? type = AnnotationOf(entry.Node);
            if (type is null) continue;

            int first = context.FirstToken(type);
            int last = first < 1 ? -1 : context.Tokens.TypeLast(first);
            if (last < first || context.Tokens[first - 1].Kind != TokenKind.Colon) continue;
            candidates.Add([SourceEdit.Delete(context.Tokens.Start(first - 1), context.Tokens.End(last))]);
        }
        return candidates;
    }

    // The annotation a node carries, for the node kinds that have one in a colon position.
    internal static TypeRef? AnnotationOf(object node) => node switch {
        VarDeclStmt v => v.AnnotatedType,
        ConstDecl c => c.AnnotatedType,
        ReadonlyDecl r => r.AnnotatedType,
        Parameter p => p.Type,
        TypeField f => f.Type,
        _ => null,
    };
}
