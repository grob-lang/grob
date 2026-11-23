using Grob.Compiler.Ast.Expressions;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 6: replaces an identifier with another name the program declares (R-57, R-67). The
/// names are the declarations' own syntax: bindings, constants, parameters and functions. The
/// replacement need not be in scope or of the same type, since a mismatch is a diagnostic the
/// oracle checks as it checks any other.
/// </summary>
internal sealed class SwapIdentifier : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "swap-identifier";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        IReadOnlyList<string> names = context.DeclaredNames();
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((IdentifierExpr identifier, _) in context.Of<IdentifierExpr>()) {
            int index = context.FirstToken(identifier);
            if (index < 0 || context.Tokens.End(index) - context.Tokens.Start(index) != identifier.Name.Length) continue;

            foreach (string name in names.Where(n => n != identifier.Name)) {
                candidates.Add([SourceEdit.Replace(context.Tokens.Start(index), context.Tokens.End(index), name)]);
            }
        }
        return candidates;
    }
}
