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

        // Flow-sensitive narrowing (§6): an `if (x != nil)` guard narrows x from T?
        // to T for the extent of the then-block. The narrowing is added only when x
        // is a nullable binding not already narrowed by an enclosing guard, and is
        // removed on leaving the block — it does not reach the else-branch.
        string? narrowedName = TryExtractNilGuard(node.Condition);
        bool addedNarrowing = false;
        if (narrowedName is not null && !_narrowedTypes.ContainsKey(narrowedName)) {
            Symbol? sym = LookupSymbol(narrowedName);
            if (sym is not null && GrobTypeHelpers.IsNullable(sym.Type)) {
                _narrowedTypes[narrowedName] = GrobTypeHelpers.ElementType(sym.Type);
                addedNarrowing = true;
            }
        }

        Visit(node.Then);

        if (addedNarrowing) _narrowedTypes.Remove(narrowedName!);

        if (node.Else is not null) Visit(node.Else);
        return GrobType.Unknown;
    }

    /// <summary>
    /// Returns the binding name narrowed by <paramref name="condition"/> when it has
    /// the form <c>x != nil</c> or <c>nil != x</c> (a single level of parenthesised
    /// grouping is unwrapped). Returns <see langword="null"/> when the condition is
    /// not a bare nil-guard — only the <c>!= nil</c> form narrows (§6); other guard
    /// forms are not introduced.
    /// </summary>
    private static string? TryExtractNilGuard(Expression condition) {
        if (condition is GroupingExpr grouping) condition = grouping.Inner;

        if (condition is BinaryExpr { Operator: BinaryOperator.NotEqual } binary) {
            if (binary.Left is IdentifierExpr left && binary.Right is NilLiteralExpr)
                return left.Name;
            if (binary.Right is IdentifierExpr right && binary.Left is NilLiteralExpr)
                return right.Name;
        }
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that the condition is <c>bool</c> (E0001), then walks the body
    /// with a <see cref="ControlFrame.Loop"/> pushed so that <c>break</c> and
    /// <c>continue</c> inside the body resolve to this loop.
    /// </remarks>
    public override GrobType VisitWhile(WhileStmt node) {
        GrobType condType = Visit(node.Condition);
        if (condType != GrobType.Bool && condType != GrobType.Error) {
            EmitError(ErrorCatalog.E0001,
                $"'while' condition must be 'bool'; found '{TypeName(condType)}'.",
                node.Condition.Range);
        }
        _controlFrames.Push(ControlFrame.Loop);
        Visit(node.Body);
        _controlFrames.Pop();
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves <c>break</c> against the control-frame stack (D-315): the nearest
    /// frame being a <see cref="ControlFrame.Select"/> is E2211 (<c>break</c> has no
    /// meaning in a <c>select</c> and is not retargeted at an enclosing loop); a
    /// <see cref="ControlFrame.Loop"/> on top is valid; an empty stack is E2212.
    /// The nearest frame being <see cref="ControlFrame.Finally"/> is E2207
    /// (Sprint 7 Increment C, D-275) — a loop nested inside the finally becomes
    /// the nearest frame before reaching that loop's own break/continue, so this
    /// only fires when the break would exit the finally itself.
    /// </remarks>
    public override GrobType VisitBreak(BreakStmt node) {
        if (_controlFrames.Count == 0)
            EmitError(ErrorCatalog.E2212, "'break' used outside a loop.", node.Range);
        else if (_controlFrames.Peek() == ControlFrame.Select)
            EmitError(ErrorCatalog.E2211,
                "'break' is not permitted inside a 'select'. " +
                "To exit an enclosing loop from inside a 'select', restructure into a function and use 'return', or use a flag variable.",
                node.Range);
        else if (_controlFrames.Peek() == ControlFrame.Finally)
            EmitError(ErrorCatalog.E2207, "'break' is not permitted inside a 'finally' block.", node.Range);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves <c>continue</c> against the control-frame stack (D-315): it skips any
    /// <see cref="ControlFrame.Select"/> frames and targets the nearest enclosing
    /// <see cref="ControlFrame.Loop"/>. When the stack contains no loop at all, it is
    /// E2212. Sprint 7 Increment C (D-275): if a <see cref="ControlFrame.Finally"/>
    /// frame is reached before any <see cref="ControlFrame.Loop"/> — the continue
    /// would have to exit the finally to reach its target — it is E2207 instead. A
    /// loop nested inside the finally is reached first and shields its own
    /// continue.
    /// </remarks>
    public override GrobType VisitContinue(ContinueStmt node) {
        bool foundLoop = false;
        bool foundFinally = false;
        foreach (ControlFrame frame in _controlFrames) {
            if (frame == ControlFrame.Loop) { foundLoop = true; break; }
            if (frame == ControlFrame.Finally) { foundFinally = true; break; }
        }

        if (foundFinally)
            EmitError(ErrorCatalog.E2207, "'continue' is not permitted inside a 'finally' block.", node.Range);
        else if (!foundLoop)
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
    /// §3.1.1 invariant holds on references. The body is walked with a
    /// <see cref="ControlFrame.Loop"/> pushed, so <c>break</c> and <c>continue</c>
    /// inside it resolve to this loop.
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

        _controlFrames.Push(ControlFrame.Loop);
        Visit(node.Body);
        _controlFrames.Pop();
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
    /// <remarks>
    /// Evaluates the subject type, then checks each case value is comparable to it —
    /// an incompatible case value is E0001 (type mismatch) at the offending pattern.
    /// No exhaustiveness check: <c>select</c> is non-exhaustive by design (D-301).
    /// A <see cref="ControlFrame.Select"/> is pushed for the duration of the case and
    /// default bodies so that <c>break</c>/<c>continue</c> resolve per D-315.
    /// </remarks>
    public override GrobType VisitSelect(SelectStmt node) {
        GrobType subjectType = Visit(node.Subject);

        _controlFrames.Push(ControlFrame.Select);
        foreach (CaseClause c in node.Cases) {
            foreach (Expression pattern in c.Patterns) {
                GrobType patternType = Visit(pattern);
                if (subjectType != GrobType.Error && subjectType != GrobType.Unknown
                    && patternType != GrobType.Error && patternType != GrobType.Unknown
                    && patternType != subjectType) {
                    EmitError(ErrorCatalog.E0001,
                        $"'case' value of type '{TypeName(patternType)}' is not comparable to " +
                        $"the 'select' subject of type '{TypeName(subjectType)}'.",
                        pattern.Range);
                }
            }
            Visit(c.Body);
        }
        if (node.Default is not null) Visit(node.Default);
        _controlFrames.Pop();
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 7 Increment B. A <c>try</c> with neither a catch nor a finally is
    /// E2204. Catches are walked in source order: any clause after a catch-all is
    /// E2205 (D-083); a typed clause naming a non-<c>GrobError</c> type is E0015;
    /// a typed clause repeating an already-seen type is E2213. Every clause still
    /// gets its binding declared and its body visited — even an erroring one — for
    /// cascade suppression (§29). No can-throw analysis (§27 permissiveness): a
    /// catch type is never checked against what the try body can actually throw.
    /// <c>finally</c>, when present, is visited with a
    /// <see cref="ControlFrame.Finally"/> frame pushed (Sprint 7 Increment C) so
    /// <c>return</c>/<c>break</c>/<c>continue</c> resolve E2207 correctly.
    /// "finally not last" is E2206, but it is raised by the parser, not here —
    /// <see cref="TryStmt"/>'s shape (a Catches list plus one optional Finally
    /// field) cannot represent a clause after the finally, so by the time an
    /// AST reaches the checker the ordering already holds.
    /// </remarks>
    public override GrobType VisitTry(TryStmt node) {
        Visit(node.Body);

        if (node.Catches.Count == 0 && node.Finally is null) {
            EmitError(ErrorCatalog.E2204,
                "'try' must be followed by at least one 'catch' or a 'finally'.",
                node.Range);
        }

        bool catchAllSeen = false;
        HashSet<string> seenTypes = new(StringComparer.Ordinal);
        foreach (CatchClause c in node.Catches) {
            if (catchAllSeen) {
                EmitError(ErrorCatalog.E2205,
                    "A 'catch' cannot appear after the catch-all clause.",
                    c.Range);
            } else if (c.ExceptionType is null) {
                catchAllSeen = true;
            } else if (!ExceptionHierarchy.IsSubtypeOf(c.ExceptionType.Name, ExceptionHierarchy.Root)) {
                EmitError(ErrorCatalog.E0015,
                    $"'catch' type must be '{ExceptionHierarchy.Root}' or a subtype; " +
                    $"found '{c.ExceptionType.Name}'.",
                    c.ExceptionType.Range);
            } else if (!seenTypes.Add(c.ExceptionType.Name)) {
                EmitError(ErrorCatalog.E2213,
                    $"A 'catch' for type '{c.ExceptionType.Name}' is already declared on this 'try'.",
                    c.ExceptionType.Range);
            }

            VisitCatchClause(c);
        }

        if (node.Finally is not null) {
            _controlFrames.Push(ControlFrame.Finally);
            Visit(node.Finally);
            _controlFrames.Pop();
        }
        return GrobType.Unknown;
    }

    /// <summary>
    /// Declares the catch binding as a fresh immutable local (D-274) — a synthetic
    /// <see cref="ReadonlyDecl"/>, never itself visited, so the existing
    /// <c>symbol.DeclarationNode is ReadonlyDecl</c> reassignment checks (E0202) fire
    /// with no changes to that machinery — then visits the clause body in that scope.
    /// </summary>
    private void VisitCatchClause(CatchClause c) {
        _scopes.Push(new Dictionary<string, Symbol>());
        if (c.ExceptionVariable is not null) {
            string boundTypeName = c.ExceptionType?.Name ?? ExceptionHierarchy.Root;
            ReadonlyDecl syntheticDecl = new(c.Range, c.ExceptionVariable, c.ExceptionType, new NilLiteralExpr(c.Range));
            RegisterSymbol(c.ExceptionVariable, GrobType.Struct, c.Range.Start, syntheticDecl,
                typeIdentity: new(NamedStructTypeName: boundTypeName));
        }
        Visit(c.Body);
        _scopes.Pop();
    }
}
