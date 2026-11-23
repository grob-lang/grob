namespace Grob.Torture.Mutation;

/// <summary>
/// One semantic mutation operator (prompt §1.6): a way to edit a program's source at a node the
/// parser returned, aimed at a shape the type checker mishandled before. An operator lists every
/// place it could apply; the mutator draws one. An edit need not leave a valid program: the oracle
/// checks invalid programs as much as valid ones.
/// </summary>
internal interface IMutationOperator {
    /// <summary>The operator's name, the middle part of a program id.</summary>
    string Name { get; }

    /// <summary>
    /// Every edit set the operator could make to the program in <paramref name="context"/>, in a
    /// fixed order. An edit set is applied whole; its edits do not overlap.
    /// </summary>
    IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context);
}
