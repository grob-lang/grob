using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Core;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 3: makes a declared type nullable, so a value the program uses as a plain type may be
/// <c>nil</c> (R-59, R-66).
/// </summary>
internal sealed class MakeNullable : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "make-nullable";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach (AstEntry entry in context.Nodes) {
            TypeRef? type = entry.Node is FnDecl fn ? fn.ReturnType : RemoveAnnotation.AnnotationOf(entry.Node);
            if (type is null || type.IsNullable) continue;

            int first = context.FirstToken(type);
            int last = first < 0 ? -1 : context.Tokens.TypeLast(first);
            if (last < first || context.Tokens[last].Kind == TokenKind.Question) continue;
            candidates.Add([SourceEdit.Insert(context.Tokens.End(last), "?")]);
        }
        return candidates;
    }
}
