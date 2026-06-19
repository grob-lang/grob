using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Control flow — visit children; return Unknown (control flow has no type).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that the condition is <c>bool</c> (E0001) then visits
    /// the then-block and the optional else-block or else-if chain.
    /// </remarks>
    public override GrobType VisitIf(IfStmt node) {
        GrobType condType = Visit(node.Condition);
        if (condType != GrobType.Bool && condType != GrobType.Error) {
            EmitError(ErrorCatalog.E0001,
                $"'if' condition must be 'bool'; found '{TypeName(condType)}'.",
                node.Condition.Range);
        }
        Visit(node.Then);
        if (node.Else is not null) Visit(node.Else);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that the condition is <c>bool</c> (E0001), then walks the body
    /// with <see cref="_loopDepth"/> incremented so that <c>break</c> and
    /// <c>continue</c> inside the body are accepted.
    /// </remarks>
    public override GrobType VisitWhile(WhileStmt node) {
        GrobType condType = Visit(node.Condition);
        if (condType != GrobType.Bool && condType != GrobType.Error) {
            EmitError(ErrorCatalog.E0001,
                $"'while' condition must be 'bool'; found '{TypeName(condType)}'.",
                node.Condition.Range);
        }
        _loopDepth++;
        Visit(node.Body);
        _loopDepth--;
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that <c>break</c> appears inside a loop (<see cref="_loopDepth"/> &gt; 0).
    /// A <c>break</c> inside a <c>select</c> case that is itself inside a loop is valid
    /// because <c>select</c> (Increment D) does not push to <see cref="_loopDepth"/>.
    /// </remarks>
    public override GrobType VisitBreak(BreakStmt node) {
        if (_loopDepth == 0)
            EmitError(ErrorCatalog.E2211, "'break' used outside a loop.", node.Range);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that <c>continue</c> appears inside a loop (<see cref="_loopDepth"/> &gt; 0).
    /// A <c>continue</c> inside a <c>select</c> case that is itself inside a loop is valid
    /// because <c>select</c> (Increment D) does not push to <see cref="_loopDepth"/>.
    /// </remarks>
    public override GrobType VisitContinue(ContinueStmt node) {
        if (_loopDepth == 0)
            EmitError(ErrorCatalog.E2212, "'continue' used outside a loop.", node.Range);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates the iteration subject (array, map or numeric range; else E0501),
    /// enforces the two-identifier map requirement (single-ident on a map is
    /// E0502) and the descending-needs-negative-step rule (E0503), and infers the
    /// iteration-variable types (<c>item</c> from the element type, the index
    /// <c>i</c> as <c>int</c>, <c>k</c>/<c>v</c> from the map). The variables are
    /// registered in a scope spanning the body with the <see cref="ForInStmt"/> as
    /// their declaration node, so a reassignment is detected as E0504 and the
    /// §3.1.1 invariant holds on references. The body is walked with
    /// <see cref="_loopDepth"/> incremented, so <c>break</c> and <c>continue</c>
    /// inside it are accepted.
    /// </remarks>
    public override GrobType VisitForIn(ForInStmt node) {
        (GrobType firstType, GrobType secondType) = ResolveIterationVariableTypes(node);

        // The iteration variables live in a scope that spans the loop body. The
        // body block pushes its own nested scope; the variables remain visible to
        // it through the scope stack.
        _scopes.Push(new Dictionary<string, Symbol>());
        RegisterSymbol(node.Variables[0], firstType, node.Range.Start, node);
        if (node.Variables.Count == 2)
            RegisterSymbol(node.Variables[1], secondType, node.Range.Start, node);

        _loopDepth++;
        Visit(node.Body);
        _loopDepth--;
        _scopes.Pop();
        return GrobType.Unknown;
    }

    /// <summary>
    /// Resolves the types of a <c>for...in</c> loop's one or two iteration
    /// variables from its subject, emitting the iteration diagnostics
    /// (E0501/E0502/E0503) as it goes. Returns a pair; the second element is unused
    /// for single-variable forms.
    /// </summary>
    private (GrobType First, GrobType Second) ResolveIterationVariableTypes(ForInStmt node) {
        if (node.Iterable is NumericRangeExpr) {
            // Dispatches to VisitNumericRange, which validates the bounds, the step
            // and the descending-needs-negative-step rule.
            Visit(node.Iterable);
            return (GrobType.Int, GrobType.Int);
        }

        GrobType subject = Visit(node.Iterable);
        switch (subject) {
            case GrobType.Array:
                // Element type tracking awaits generics (Sprint 5): item is Unknown.
                // The index form binds i as the zero-based int counter.
                return node.Variables.Count == 2
                    ? (GrobType.Int, GrobType.Unknown)
                    : (GrobType.Unknown, GrobType.Unknown);

            case GrobType.Map:
                if (node.Variables.Count == 1) {
                    EmitError(ErrorCatalog.E0502,
                        $"Iterating a 'map' requires two variables, as in 'for {node.Variables[0]}, v in m'. " +
                        $"To iterate the keys alone, write 'for {node.Variables[0]} in m.keys'.",
                        node.Iterable.Range);
                    return (GrobType.Error, GrobType.Error);
                }
                // Map keys are strings; value type tracking awaits generics (Sprint 5).
                return (GrobType.String, GrobType.Unknown);

            case GrobType.Error:
                return (GrobType.Error, GrobType.Error); // cascade suppression

            case GrobType.Unknown:
                // A subject of unknown type (e.g. a stdlib call result) is treated
                // permissively rather than rejected — the value may well be iterable.
                return (GrobType.Unknown, GrobType.Unknown);

            default:
                EmitError(ErrorCatalog.E0501,
                    $"'for...in' subject of type '{TypeName(subject)}' is not iterable. " +
                    "Only an array, a map or a numeric range can be iterated.",
                    node.Iterable.Range);
                return (GrobType.Error, GrobType.Error);
        }
    }

    private void RequireIntRangeComponent(GrobType type, SourceRange range, string role) {
        if (type != GrobType.Int && type != GrobType.Error)
            EmitError(ErrorCatalog.E0001,
                $"Numeric-range {role} must be 'int'; found '{TypeName(type)}'.", range);
    }

    /// <summary>
    /// Returns <see langword="true"/> when both bounds of <paramref name="range"/>
    /// are integer literals and the start is greater than the end.
    /// </summary>
    private static bool IsLiteralDescending(NumericRangeExpr range) =>
        range.Start is IntLiteralExpr start &&
        range.End is IntLiteralExpr end &&
        start.Value > end.Value;

    /// <inheritdoc/>
    public override GrobType VisitSelect(SelectStmt node) {
        Visit(node.Subject);
        foreach (CaseClause c in node.Cases) {
            foreach (Expression pattern in c.Patterns) Visit(pattern);
            Visit(c.Body);
        }
        if (node.Default is not null) Visit(node.Default);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitTry(TryStmt node) {
        Visit(node.Body);
        foreach (CatchClause c in node.Catches) Visit(c.Body);
        if (node.Finally is not null) Visit(node.Finally);
        return GrobType.Unknown;
    }
}
