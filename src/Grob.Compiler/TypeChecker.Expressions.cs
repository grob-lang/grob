using System.Diagnostics.CodeAnalysis;
using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Literals — return the exact scalar type so Increment D can choose opcodes.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitIntLiteral(IntLiteralExpr node) => GrobType.Int;

    /// <inheritdoc/>
    public override GrobType VisitFloatLiteral(FloatLiteralExpr node) => GrobType.Float;

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="StringLiteralExpr"/> is never produced by the parser — all
    /// <c>"..."</c> strings are represented as <see cref="InterpolatedStringExpr"/>.
    /// Kept as a defensive override; excluded from coverage because it is unreachable.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "StringLiteralExpr is never created by the parser; all double-quoted strings are InterpolatedStringExpr.")]
    public override GrobType VisitStringLiteral(StringLiteralExpr node) => GrobType.String;

    /// <inheritdoc/>
    public override GrobType VisitRawStringLiteral(RawStringLiteralExpr node) => GrobType.String;

    /// <inheritdoc/>
    public override GrobType VisitBoolLiteral(BoolLiteralExpr node) => GrobType.Bool;

    /// <inheritdoc/>
    public override GrobType VisitNilLiteral(NilLiteralExpr node) => GrobType.Nil;

    /// <inheritdoc/>
    public override GrobType VisitRegexLiteral(RegexLiteralExpr node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitInterpolatedString(InterpolatedStringExpr node) {
        // D-279, E0102: interpolating a nullable expression is a compile error.
        // Each ${expr} slot is type-checked; if its resolved type is nullable the
        // slot is an error unless the expression itself has already resolved the
        // nullability (e.g. x ?? fallback → non-nullable).
        foreach (StringInterpolationPart part in node.Parts) {
            if (part is StringExpressionPart exprPart) {
                GrobType slotType = Visit(exprPart.Expression);
                if (GrobTypeHelpers.IsNullable(slotType)) {
                    EmitError(ErrorCatalog.E0102,
                        $"Interpolated expression has nullable type '{TypeName(slotType)}'; resolve with '??' before interpolating.",
                        exprPart.Range);
                }
            }
        }
        return GrobType.String;
    }

    // -----------------------------------------------------------------------
    // Identifier resolution (§3.1.1).
    // Sets ResolvedType and Declaration on the node; emits E1001 if undefined.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitIdentifier(IdentifierExpr node) {
        Symbol? symbol = LookupSymbol(node.Name);
        if (symbol is null) {
            EmitError(ErrorCatalog.E1001, $"Undefined identifier '{node.Name}'.", node.Range);
            node.ResolvedType = GrobType.Error;
            node.Declaration = UnresolvedDecl.Instance; // §3.1.1 invariant: Declaration is never null after type-check (D-311).
            return GrobType.Error;
        }
        // E2102 — a type name used bare in expression position without the required
        // '{ }' construction body (§10). The StructConstructionExpr parser handles the
        // brace case; reaching VisitIdentifier for a TypeDecl symbol means the braces
        // were omitted.
        if (symbol.DeclarationNode is TypeDecl) {
            EmitError(ErrorCatalog.E2102,
                $"Type '{node.Name}' cannot be used as a value; did you mean '{node.Name} {{ … }}'?",
                node.Range);
            node.ResolvedType = GrobType.Error;
            node.Declaration = UnresolvedDecl.Instance;
            return GrobType.Error;
        }

        // E1004 — a namespace name (math, path, …; D-342) used bare in value position.
        // Reaching VisitIdentifier for a NamespaceDecl symbol means the name was not the
        // receiver of a member access (VisitMemberAccess/VisitCall peek at the receiver
        // without visiting it, precisely so a valid `math.pi` never trips this arm). Same
        // shape as the TypeDecl arm above: Error type, UnresolvedDecl on the §3.1.1 path.
        if (symbol.DeclarationNode is NamespaceDecl) {
            EmitError(ErrorCatalog.E1004,
                $"Namespace '{node.Name}' cannot be used as a value; access a member such as '{node.Name}.…'.",
                node.Range);
            node.ResolvedType = GrobType.Error;
            node.Declaration = UnresolvedDecl.Instance;
            return GrobType.Error;
        }

        // Flow-sensitive narrowing (§6): inside an `if (x != nil)` block a binding
        // is narrowed from T? to T. Use the narrowed type when one is active for
        // this name; the declaration is unchanged (§3.1.1 still holds).
        GrobType resolvedType = _narrowedTypes.TryGetValue(node.Name, out GrobType narrowed)
            ? narrowed
            : symbol.Type;
        node.ResolvedType = resolvedType;
        node.Declaration = symbol.DeclarationNode;
        return resolvedType;
    }

    // -----------------------------------------------------------------------
    // Unary expressions.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitUnary(UnaryExpr node) {
        GrobType operand = Visit(node.Operand);
        if (operand == GrobType.Error) return GrobType.Error; // cascade suppression

        return node.Operator switch {
            UnaryOperator.Negate when operand == GrobType.Int => GrobType.Int,
            UnaryOperator.Negate when operand == GrobType.Float => GrobType.Float,
            UnaryOperator.Not when operand == GrobType.Bool => GrobType.Bool,
            // Unknown operand (e.g. lambda parameter) — be permissive; propagate Unknown.
            _ when operand == GrobType.Unknown => GrobType.Unknown,
            UnaryOperator.Negate => EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '-' cannot be applied to type '{TypeName(operand)}'.", node.Range),
            UnaryOperator.Not => EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '!' cannot be applied to type '{TypeName(operand)}'.", node.Range),
            _ => ThrowUnknownUnaryOperator(node),
        };
    }

    // -----------------------------------------------------------------------
    // Binary expressions — arithmetic, comparison, logical.
    // The resolved type is exact (Int vs Float vs String vs Bool) so Increment D
    // can select AddInt / AddFloat / Concat without any further inspection.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitBinary(BinaryExpr node) {
        GrobType left = Visit(node.Left);
        GrobType right = Visit(node.Right);

        // Cascade suppression: if either side already errored, suppress derived diagnostics.
        if (left == GrobType.Error || right == GrobType.Error) return GrobType.Error;

        if (IsComparisonOperator(node.Operator)) return ResolveComparison(node, left, right);
        if (node.Operator == BinaryOperator.And || node.Operator == BinaryOperator.Or) return ResolveLogical(node, left, right);
        if (node.Operator == BinaryOperator.NilCoalesce) return ResolveNilCoalesce(node, left, right);

        return ResolveArithmetic(node, left, right);
    }

    private GrobType ResolveArithmetic(BinaryExpr node, GrobType left, GrobType right) {
        // string + string → string (Concat)
        if (node.Operator == BinaryOperator.Add && left == GrobType.String && right == GrobType.String) {
            return GrobType.String;
        }

        // int op int → int (including int / int → int, truncating)
        if (left == GrobType.Int && right == GrobType.Int) return GrobType.Int;

        // float op float → float
        if (left == GrobType.Float && right == GrobType.Float) return GrobType.Float;

        // int op float or float op int → float (only implicit conversion in Grob)
        if ((left == GrobType.Int && right == GrobType.Float) ||
            (left == GrobType.Float && right == GrobType.Int)) {
            return GrobType.Float;
        }

        // One or both operands are of unknown type (e.g. a lambda parameter whose type
        // is inferred at the call site, or a deferred member type).  Be permissive and
        // propagate Unknown — the VM will validate types at runtime.  This mirrors the
        // Unknown pass-through in ResolveComparison below.
        if (left == GrobType.Unknown || right == GrobType.Unknown) return GrobType.Unknown;

        // All other combinations are type errors — e.g. int + string.
        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    private GrobType ResolveComparison(BinaryExpr node, GrobType left, GrobType right) {
        // An operand of unknown type (e.g. an array element, whose element type is
        // untracked until generics) is permissive: a comparison always yields bool.
        // '==' / '!=' compile to the type-agnostic Equal/NotEqual; a relational
        // operator falls to the int comparison, correct for the int element case.
        if (left == GrobType.Unknown || right == GrobType.Unknown) return GrobType.Bool;

        // == and != accept same-type operands or mixed numeric operands.
        if (node.Operator == BinaryOperator.Equal || node.Operator == BinaryOperator.NotEqual) {
            if (left == right || BothNumeric(left, right)) return GrobType.Bool;
            // A comparison against the nil literal is valid for any operand (§20:
            // `x == nil` resolves to bool). `x != nil` is the form flow-sensitive
            // narrowing keys off (§6); it stays valid for an already-narrowed value.
            if (IsNilComparison(left, right)) return GrobType.Bool;
            return EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
                node.Range);
        }

        // <, <=, >, >= require numeric (int/float, mixed ok) or same-string operands.
        if (BothNumeric(left, right) || (left == GrobType.String && right == GrobType.String)) {
            return GrobType.Bool;
        }

        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    private GrobType ResolveLogical(BinaryExpr node, GrobType left, GrobType right) {
        if (left == GrobType.Bool && right == GrobType.Bool) return GrobType.Bool;
        string sym = node.Operator == BinaryOperator.And ? "&&" : "||";
        if (left != GrobType.Bool) {
            return EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '{sym}' cannot be applied to type '{TypeName(left)}'.", node.Left.Range);
        }
        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{sym}' cannot be applied to type '{TypeName(right)}'.", node.Right.Range);
    }

    /// <summary>
    /// Resolves the type of a nil-coalescing expression <c>a ?? b</c>.
    /// <para>
    /// The <c>??</c> operator is eager (D-271): both operands are evaluated
    /// before the opcode runs — there is no short-circuit branching.
    /// </para>
    /// <para>Rules (Sprint 3 Increment D):</para>
    /// <list type="bullet">
    /// <item><description><c>T? ?? T  → T</c>  — left nullable, right non-nullable: result is the non-nullable element type.</description></item>
    /// <item><description><c>T? ?? T? → T?</c> — left nullable, right nullable: result stays nullable.</description></item>
    /// <item><description><c>T  ?? T  → T</c>  — left non-nullable (the ?? is a no-op at runtime): result is T.</description></item>
    /// </list>
    /// </summary>
    private GrobType ResolveNilCoalesce(BinaryExpr node, GrobType left, GrobType right) {
        // A nil literal on the left always yields the right operand's type —
        // the fallback always wins because nil ?? T ≡ T.
        if (left == GrobType.Nil) return right;

        // If the left side is nullable, the fallback (right) determines whether the
        // result is fully unwrapped or stays nullable.
        GrobType leftElem = GrobTypeHelpers.IsNullable(left)
            ? GrobTypeHelpers.ElementType(left)
            : left;

        // Right side must be the same base type or a nullable version of it.
        GrobType rightElem = GrobTypeHelpers.IsNullable(right)
            ? GrobTypeHelpers.ElementType(right)
            : right;

        if (leftElem != GrobType.Error && rightElem != GrobType.Error &&
            leftElem != GrobType.Unknown && rightElem != GrobType.Unknown &&
            leftElem != rightElem) {
            return EmitErrorAndReturn(ErrorCatalog.E0002,
                $"Operator '??' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}': element types '{TypeName(leftElem)}' and '{TypeName(rightElem)}' do not match.",
                node.Range);
        }

        // When both sides are Unknown (e.g. deferred member types), be permissive.
        if (leftElem == GrobType.Unknown || rightElem == GrobType.Unknown) return GrobType.Unknown;

        // If the right side is nullable the result stays nullable; otherwise it is non-nullable.
        return GrobTypeHelpers.IsNullable(right) ? left : leftElem;
    }

    // -----------------------------------------------------------------------
    // Grouping — transparent wrapper; result type is the inner expression's type.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitGrouping(GroupingExpr node) => Visit(node.Inner);

    // -----------------------------------------------------------------------
    // Deferred expressions (Sprint 5+) — visit children to keep identifier
    // resolution working even inside deferred constructs.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves the callee, then binds and validates the argument set through
    /// <see cref="CheckCall"/> under the D-113 calling convention — positionals,
    /// then named arguments into defaulted parameters, then defaults — raising the
    /// call-site diagnostics (E0008–E0011) and the arity/type diagnostics
    /// (E0003/E0004) on the bound set. Resolves to the function's declared return
    /// type. Built-in and unresolved callees stay permissive.
    /// </remarks>
    public override GrobType VisitCall(CallExpr node) {
        // Sprint 5C: array higher-order method calls (filter/select/sort/each).
        // The callee is a member access on an array receiver; we visit the target
        // directly (not the whole MemberAccessExpr) so we can branch on the receiver type
        // without a double-visit.
        if (node.Callee is MemberAccessExpr memberAccess) {
            // Precedence (D-342): a namespace receiver (math.sqrt(...)) is resolved by
            // peeking at the receiver — never visiting it, which would emit E1004 on the
            // namespace itself — BEFORE the array higher-order-method arm and the generic
            // fallback below.
            if (TryAnnotateNamespaceReceiver(memberAccess.Target, out string namespaceName)) {
                return ResolveNamespaceMemberCall(node, memberAccess, namespaceName);
            }

            GrobType receiverType = Visit(memberAccess.Target);
            // Visit argument values to satisfy §3.1.1 on any identifiers inside them.
            var argTypes = new GrobType[node.Arguments.Count];
            for (int i = 0; i < node.Arguments.Count; i++)
                argTypes[i] = Visit(node.Arguments[i].Value);

            if (receiverType == GrobType.Array && IsArrayHigherOrderMethod(memberAccess.Member)) {
                return ValidateArrayMethodCall(node, memberAccess.Member, argTypes);
            }
            return GrobType.Unknown;
        }

        Visit(node.Callee);
        var callArgTypes = new GrobType[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++) {
            callArgTypes[i] = Visit(node.Arguments[i].Value);
        }

        // Only user-defined functions are checked positionally here. Built-ins
        // (print/exit/input) and unresolved callees are permissive.
        if (node.Callee is not IdentifierExpr { Declaration: FnDecl fn }) {
            return GrobType.Unknown;
        }

        CheckCall(node, fn, callArgTypes);
        // Resolve the declared return type with its structural descriptor so a call that
        // returns a function type (makeCounter(): fn(): int) flows its descriptor to the
        // binding annotation via _callResultDescriptors (D-326; Fix I). ResolveSignatureType
        // additionally recognises a struct-typed return (Sprint 6 close) and flows the
        // declared name via _callResultStructNames so `box := makeBox()` resolves field
        // access the same way a direct struct-construction initialiser already does.
        (GrobType resultKind, string? resultStructName, FunctionTypeDescriptor? resultDesc) =
            ResolveSignatureType(fn.ReturnType);
        if (resultDesc is not null) _callResultDescriptors[node] = resultDesc;
        if (resultStructName is not null) _callResultStructNames[node] = resultStructName;
        return resultKind;
    }

    private static bool IsArrayHigherOrderMethod(string name) =>
        name is "filter" or "select" or "sort" or "each";

    /// <summary>
    /// Validates an array higher-order method call and returns the result type.
    /// Emits E0004 when a <c>filter</c> predicate's inferred return type is known
    /// to be non-bool (neither <see cref="GrobType.Unknown"/> nor
    /// <see cref="GrobType.Error"/> — those are permissive).
    /// </summary>
    private GrobType ValidateArrayMethodCall(
            CallExpr node, string methodName, GrobType[] argTypes) {
        switch (methodName) {
            case "filter": {
                    // First argument must be a predicate returning bool.
                    if (node.Arguments.Count >= 1 &&
                        node.Arguments[0].Value is LambdaExpr lambdaPred &&
                        _lambdaReturnTypes.TryGetValue(lambdaPred, out GrobType bodyType) &&
                        bodyType != GrobType.Unknown && bodyType != GrobType.Error &&
                        bodyType != GrobType.Bool) {
                        EmitError(ErrorCatalog.E0004,
                            $"'filter' predicate must return 'bool'; found '{TypeName(bodyType)}'.",
                            node.Arguments[0].Value.Range);
                    }
                    return GrobType.Array;
                }
            case "select":
                return GrobType.Array;
            case "sort":
                // Optional second arg must be bool (the 'descending' flag).
                if (node.Arguments.Count >= 2 &&
                    argTypes[1] != GrobType.Bool && argTypes[1] != GrobType.Unknown &&
                    argTypes[1] != GrobType.Error) {
                    EmitError(ErrorCatalog.E0004,
                        $"'sort' second argument ('descending') must be 'bool'; found '{TypeName(argTypes[1])}'.",
                        node.Arguments[1].Value.Range);
                }
                return GrobType.Array;
            case "each":
                return GrobType.Unknown; // void
            default:
                return GrobType.Unknown;
        }
    }

    /// <summary>
    /// Validates a call against a resolved <paramref name="fn"/> under the D-113
    /// calling convention: positional arguments first, then named arguments, with
    /// only defaulted parameters eligible to be named. Binding runs in three phases —
    /// positionals into the leading slots, named arguments into the remaining
    /// defaulted slots, then defaults fill the rest — and detects the four call-site
    /// diagnostics (E0008 named-before-positional, E0009 naming a required parameter,
    /// E0010 duplicate, E0011 unknown name) during binding. Arity (E0003) and per
    /// argument type (E0004) are checked on the bound set, and a binding error
    /// suppresses them to keep one diagnostic per root cause.
    /// </summary>
    private void CheckCall(CallExpr node, FnDecl fn, GrobType[] argTypes) {
        int paramCount = fn.Parameters.Count;
        var boundType = new GrobType?[paramCount];
        var boundRange = new SourceRange?[paramCount];
        // The bound argument expression per slot, so a function-typed parameter can be
        // checked against the argument's structural descriptor (D-326; Fix J).
        var boundExpr = new Expression?[paramCount];

        // Binding errors (E0008–E0011) are all collected, then suppress the
        // downstream arity/type checks to keep one diagnostic per root cause.
        if (!BindArguments(node, fn, argTypes, boundType, boundRange, boundExpr, out int positionalCount)) {
            return;
        }

        // Arity (E0003) on the bound set — too many positionals, or a required
        // (defaultless) parameter left unbound.
        if (positionalCount > paramCount || HasUnboundRequired(fn, boundType)) {
            EmitArityError(node, fn);
            return;
        }

        // Type (E0004) on each caller-supplied argument. Default-filled slots were
        // checked at the declaration site.
        for (int i = 0; i < paramCount; i++) {
            if (boundRange[i] is SourceRange range && boundType[i] is GrobType argType) {
                CheckBoundArgumentType(fn, i, argType, range, boundExpr[i]);
            }
        }
    }

    /// <summary>
    /// Binds the arguments of <paramref name="node"/> to the parameters of
    /// <paramref name="fn"/>: positionals into the leading slots, named arguments
    /// into their parameters. Emits the call-site binding diagnostics (E0008 named
    /// before positional, E0009/E0010/E0011 via <see cref="TryBindNamed"/>) and
    /// collects every independent one. Returns <see langword="true"/> when binding
    /// was clean, <see langword="false"/> when any binding error was emitted.
    /// </summary>
    private bool BindArguments(CallExpr node, FnDecl fn, GrobType[] argTypes,
            GrobType?[] boundType, SourceRange?[] boundRange, Expression?[] boundExpr, out int positionalCount) {
        bool ok = true;
        bool seenNamed = false;
        positionalCount = 0;

        for (int i = 0; i < node.Arguments.Count; i++) {
            CallArgument arg = node.Arguments[i];
            if (arg.Name is not null) {
                seenNamed = true;
                if (!TryBindNamed(fn, arg, argTypes[i], boundType, boundRange, boundExpr)) ok = false;
                continue;
            }

            // A positional argument after a named one breaks the convention. Keep
            // scanning so independent later errors still surface.
            if (seenNamed) {
                EmitError(ErrorCatalog.E0008,
                    $"In the call to '{fn.Name}', a positional argument follows a named argument. Move all named arguments after the positional ones.",
                    arg.Value.Range);
                ok = false;
                continue;
            }
            if (positionalCount < boundType.Length) {
                boundType[positionalCount] = argTypes[i];
                boundRange[positionalCount] = arg.Value.Range;
                boundExpr[positionalCount] = arg.Value;
            }
            positionalCount++;
        }

        return ok;
    }

    /// <summary>
    /// Binds one named argument to its parameter, emitting E0011 (unknown name),
    /// E0009 (names a required parameter) or E0010 (already supplied) when it cannot.
    /// Returns <see langword="true"/> when the argument bound cleanly.
    /// </summary>
    private bool TryBindNamed(FnDecl fn, CallArgument arg, GrobType argType,
            GrobType?[] boundType, SourceRange?[] boundRange, Expression?[] boundExpr) {
        int p = ParameterIndex(fn, arg.Name!);
        if (p < 0) {
            EmitError(ErrorCatalog.E0011,
                $"Function '{fn.Name}' has no parameter named '{arg.Name}'.",
                arg.Range);
            return false;
        }
        if (fn.Parameters[p].DefaultValue is null) {
            EmitError(ErrorCatalog.E0009,
                $"Parameter '{arg.Name}' of '{fn.Name}' is required and has no default, so it cannot be passed by name. Pass it positionally.",
                arg.Range);
            return false;
        }
        if (boundType[p] is not null) {
            EmitError(ErrorCatalog.E0010,
                $"Parameter '{arg.Name}' of '{fn.Name}' is already supplied.",
                arg.Range);
            return false;
        }
        boundType[p] = argType;
        boundRange[p] = arg.Value.Range;
        boundExpr[p] = arg.Value;
        return true;
    }

    /// <summary>
    /// Emits the arity diagnostic (E0003) for a call whose bound argument set does
    /// not satisfy <paramref name="fn"/>. The expected count renders as a single
    /// number when every parameter is required, or a range when some have defaults.
    /// </summary>
    private void EmitArityError(CallExpr node, FnDecl fn) {
        int paramCount = fn.Parameters.Count;
        int required = fn.Parameters.Count(pm => pm.DefaultValue is null);
        int supplied = node.Arguments.Count;
        string expectation = required == paramCount
            ? $"{paramCount} {Plural(paramCount, "argument")}"
            : $"between {required} and {paramCount} arguments";
        string suppliedVerb = supplied == 1 ? "was" : "were";
        EmitError(ErrorCatalog.E0003,
            $"Function '{fn.Name}' expects {expectation}, but {supplied} {suppliedVerb} supplied.",
            node.Range);
    }

    /// <summary>Returns <paramref name="noun"/> pluralised for a count of <paramref name="n"/>.</summary>
    private static string Plural(int n, string noun) => n == 1 ? noun : noun + "s";

    /// <summary>
    /// Returns the index of the parameter named <paramref name="name"/> in
    /// <paramref name="fn"/>, or <c>-1</c> when no parameter has that name.
    /// </summary>
    private static int ParameterIndex(FnDecl fn, string name) {
        for (int i = 0; i < fn.Parameters.Count; i++) {
            if (fn.Parameters[i].Name == name) return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns <see langword="true"/> when a required (defaultless) parameter has no
    /// bound argument after the positional and named binding passes.
    /// </summary>
    private static bool HasUnboundRequired(FnDecl fn, GrobType?[] boundType) {
        for (int i = 0; i < fn.Parameters.Count; i++) {
            if (fn.Parameters[i].DefaultValue is null && boundType[i] is null) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks one bound argument's type against its parameter (E0004). An Unknown
    /// parameter type (a deferred type) is permissive, and cascade suppression
    /// covers an argument that already errored.
    /// </summary>
    private void CheckBoundArgumentType(
            FnDecl fn, int paramIndex, GrobType argType, SourceRange valueRange, Expression? argExpr) {
        // Resolve the parameter with its structural descriptor so a function-typed
        // parameter is checked against the argument's descriptor, not merely fn-to-fn
        // (D-326; Fix J).
        // ResolveSignatureType (not ResolveTypeRefFull) so a user-defined struct parameter
        // annotation resolves to a concrete struct kind instead of Unknown; otherwise a
        // non-struct argument to a struct parameter (takesConfig(1)) silently bypasses E0004
        // because the permissive Unknown short-circuits the assignability check (Sprint 6 close).
        (GrobType paramType, _, FunctionTypeDescriptor? paramDesc) = fn.Parameters[paramIndex].Type is not null
            ? ResolveSignatureType(fn.Parameters[paramIndex].Type!)
            : (GrobType.Unknown, null, null);
        bool isFunctionParam = paramType == GrobType.Function || paramType == GrobType.NullableFunction;
        bool compatible;
        if (isFunctionParam) {
            FunctionTypeDescriptor? argDesc = argExpr is not null ? InitialiserDescriptor(argExpr) : null;
            compatible = TypesAreAssignable(argType, paramType, argDesc, paramDesc);
        } else {
            compatible = TypesAreAssignable(argType, paramType);
        }
        if (paramType != GrobType.Unknown && argType != GrobType.Error && !compatible) {
            EmitError(ErrorCatalog.E0004,
                $"Argument to '{fn.Name}' has type '{TypeName(argType)}', which is not assignable to parameter '{fn.Parameters[paramIndex].Name}' of type '{TypeName(paramType)}'.",
                valueRange);
        }
    }

    /// <inheritdoc/>
    public override GrobType VisitMemberAccess(MemberAccessExpr node) {
        // Precedence (D-342): a namespace receiver (math.pi) is resolved by peeking at the
        // receiver — never visiting it, which would emit E1004 on the namespace itself —
        // BEFORE the struct/anon-struct field-access fall-through below. Every non-namespace
        // receiver falls through to the existing arms unchanged.
        if (TryAnnotateNamespaceReceiver(node.Target, out string namespaceName)) {
            return ResolveNamespaceMemberAccess(node, namespaceName);
        }

        GrobType targetType = Visit(node.Target);

        // Cascade suppression: if the target already errored, don't pile on.
        if (targetType == GrobType.Error) return GrobType.Error;

        // Using '.' (non-optional) on a nullable receiver is a compile-time error (E0101).
        if (!node.IsOptional && GrobTypeHelpers.IsNullable(targetType)) {
            return EmitErrorAndReturn(ErrorCatalog.E0101,
                $"Member access via '.' on nullable type '{TypeName(targetType)}' may dereference nil. Use '?.' to chain or '??' to unwrap first.",
                node.Range);
        }

        // '?.' on a nullable receiver: stay permissive so downstream '??' and
        // nullable-aware operators do not see a spuriously concrete field type.
        // Full '?.' type propagation (returning the nullable variant of the field type)
        // is deferred until nullable-struct construction is fully wired.
        if (node.IsOptional && GrobTypeHelpers.IsNullable(targetType)) {
            return GrobType.Unknown;
        }

        // Resolve struct field access: look up the field in the user type registry and
        // annotate the node so the compiler can emit typed opcodes and chain nested access.
        if (targetType == GrobType.Struct || targetType == GrobType.NullableStruct) {
            return ResolveStructFieldAccess(node);
        }

        // Anonymous struct: structural field access via the synthesised type registry.
        if (targetType == GrobType.AnonStruct || targetType == GrobType.NullableAnonStruct) {
            return ResolveStructFieldAccess(node);
        }

        // For '?.' chains or Unknown-typed targets the result type is Unknown so
        // downstream '??' operators remain permissive and do not emit false positives.
        return GrobType.Unknown;
    }

    // -----------------------------------------------------------------------
    // Namespace member access (D-342) — the shared precedence rule for the three
    // call sites (VisitIdentifier E1004, VisitCall, VisitMemberAccess).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reports whether <paramref name="target"/> is a registered-namespace receiver, and
    /// if so annotates the receiver identifier for the §3.1.1 invariant WITHOUT visiting
    /// it. Visiting the receiver would route it through <see cref="VisitIdentifier"/> and
    /// emit E1004 (namespace-as-value) on a perfectly valid <c>math.pi</c> — the ordering
    /// hazard D-342 exists to close. The identifier resolves to its <see cref="NamespaceDecl"/>
    /// (a namespace is not a value, so its type stays <see cref="GrobType.Unknown"/>).
    /// </summary>
    /// <remarks>
    /// Resolves through <see cref="LookupSymbol"/> — not a bare
    /// <see cref="NamespaceRegistry.IsNamespace"/> name check — so a local variable or
    /// parameter that happens to share a namespace's name (e.g. a <c>Config</c>-typed
    /// parameter called <c>math</c>) correctly shadows the global namespace and falls
    /// through to ordinary member-access resolution instead of always winning (PR #127
    /// review). Only the closest-scoped symbol resolving to an actual
    /// <see cref="NamespaceDecl"/> takes the namespace branch.
    /// </remarks>
    private bool TryAnnotateNamespaceReceiver(Expression target, out string namespaceName) {
        namespaceName = string.Empty;
        if (target is not IdentifierExpr id) return false;

        Symbol? symbol = LookupSymbol(id.Name);
        if (symbol?.DeclarationNode is not NamespaceDecl namespaceDecl) return false;

        namespaceName = id.Name;
        id.ResolvedType = GrobType.Unknown;
        id.Declaration = namespaceDecl;
        return true;
    }

    /// <summary>
    /// Resolves a bare namespace member access (<c>math.pi</c>). A constant member returns
    /// its declared type; a native member accessed without a call is a callable member typed
    /// as <see cref="GrobType.Function"/> (mirroring a bare user-fn reference); an unknown
    /// member is <see cref="ErrorCatalog.E1003"/>.
    /// </summary>
    private GrobType ResolveNamespaceMemberAccess(MemberAccessExpr node, string namespaceName) {
        object? member = NamespaceRegistry.TryGetMember(namespaceName, node.Member);
        switch (member) {
            case NamespaceRegistry.ConstantMember constant:
                node.ResolvedFieldType = constant.Type;
                return constant.Type;
            case NamespaceRegistry.NativeMember:
                node.ResolvedFieldType = GrobType.Function;
                return GrobType.Function;
            default:
                node.ResolvedFieldType = GrobType.Error;
                return EmitErrorAndReturn(ErrorCatalog.E1003,
                    $"Namespace '{namespaceName}' has no member '{node.Member}'.",
                    node.Range);
        }
    }

    /// <summary>
    /// Resolves a namespace member CALL (<c>math.sqrt(9.0)</c>). A native member is validated
    /// positionally (arity E0003, per-argument type E0004) and resolves to its declared return
    /// type; a constant member or an unknown member is <see cref="ErrorCatalog.E1003"/>. Arguments
    /// are visited regardless so the §3.1.1 invariant holds for any identifier inside them.
    /// </summary>
    private GrobType ResolveNamespaceMemberCall(CallExpr node, MemberAccessExpr memberAccess, string namespaceName) {
        var argTypes = new GrobType[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++)
            argTypes[i] = Visit(node.Arguments[i].Value);

        object? member = NamespaceRegistry.TryGetMember(namespaceName, memberAccess.Member);
        if (member is NamespaceRegistry.NativeMember native) {
            memberAccess.ResolvedFieldType = native.ReturnType;
            CheckNativeCall(node, namespaceName, memberAccess.Member, native, argTypes);
            return native.ReturnType;
        }

        // A constant member cannot be called, and an unknown member does not exist — both
        // are E1003 (no such callable member). No error code distinguishes "not callable"
        // from "unknown" in v1; the message covers both.
        memberAccess.ResolvedFieldType = GrobType.Error;
        return EmitErrorAndReturn(ErrorCatalog.E1003,
            $"Namespace '{namespaceName}' has no member '{memberAccess.Member}'.",
            memberAccess.Range);
    }

    /// <summary>
    /// Validates a native member call positionally: arity (E0003) then per-argument
    /// assignability (E0004, at the argument's location). A native has no named or defaulted
    /// parameters, so a straight index-by-index check suffices; an already-errored argument
    /// is suppressed to keep one diagnostic per root cause.
    /// </summary>
    private void CheckNativeCall(CallExpr node, string namespaceName, string memberName,
            NamespaceRegistry.NativeMember member, GrobType[] argTypes) {
        if (member.VariadicElementType is GrobType variadicType) {
            CheckVariadicNativeCall(node, namespaceName, memberName, member, variadicType, argTypes);
            return;
        }

        int expected = member.ParameterTypes.Count;
        if (argTypes.Length != expected) {
            string argWord = expected == 1 ? "argument" : "arguments";
            string suppliedVerb = argTypes.Length == 1 ? "was" : "were";
            EmitError(ErrorCatalog.E0003,
                $"'{namespaceName}.{memberName}' expects {expected} {argWord}, but {argTypes.Length} {suppliedVerb} supplied.",
                node.Range);
            return;
        }

        for (int i = 0; i < expected; i++) {
            if (argTypes[i] == GrobType.Error) continue; // cascade suppression
            if (!TypesAreAssignable(argTypes[i], member.ParameterTypes[i])) {
                EmitError(ErrorCatalog.E0004,
                    $"Argument {i + 1} to '{namespaceName}.{memberName}' has type '{TypeName(argTypes[i])}', which is not assignable to parameter of type '{TypeName(member.ParameterTypes[i])}'.",
                    node.Arguments[i].Value.Range);
            }
        }
    }

    /// <summary>
    /// Validates a native member call whose tail is variadic (<c>path.join</c>, the one
    /// consumer of <see cref="NamespaceRegistry.NativeMember.VariadicElementType"/>): the
    /// fixed prefix is checked exactly as <see cref="CheckNativeCall"/> does, then at least
    /// one further argument is required and every one of them is checked against
    /// <paramref name="variadicType"/> instead of a fixed per-slot type.
    /// </summary>
    private void CheckVariadicNativeCall(CallExpr node, string namespaceName, string memberName,
            NamespaceRegistry.NativeMember member, GrobType variadicType, GrobType[] argTypes) {
        int fixedCount = member.ParameterTypes.Count;
        int minimum = fixedCount + 1;
        if (argTypes.Length < minimum) {
            string argWord = minimum == 1 ? "argument" : "arguments";
            string suppliedVerb = argTypes.Length == 1 ? "was" : "were";
            EmitError(ErrorCatalog.E0003,
                $"'{namespaceName}.{memberName}' expects at least {minimum} {argWord}, but {argTypes.Length} {suppliedVerb} supplied.",
                node.Range);
            return;
        }

        for (int i = 0; i < fixedCount; i++) {
            if (argTypes[i] == GrobType.Error) continue; // cascade suppression
            if (!TypesAreAssignable(argTypes[i], member.ParameterTypes[i])) {
                EmitError(ErrorCatalog.E0004,
                    $"Argument {i + 1} to '{namespaceName}.{memberName}' has type '{TypeName(argTypes[i])}', which is not assignable to parameter of type '{TypeName(member.ParameterTypes[i])}'.",
                    node.Arguments[i].Value.Range);
            }
        }

        for (int i = fixedCount; i < argTypes.Length; i++) {
            if (argTypes[i] == GrobType.Error) continue; // cascade suppression
            if (!TypesAreAssignable(argTypes[i], variadicType)) {
                EmitError(ErrorCatalog.E0004,
                    $"Argument {i + 1} to '{namespaceName}.{memberName}' has type '{TypeName(argTypes[i])}', which is not assignable to parameter of type '{TypeName(variadicType)}'.",
                    node.Arguments[i].Value.Range);
            }
        }
    }

    // Looks up node.Member in the user type registry and annotates the node. Written as
    // guard clauses (rather than nested ifs) to keep VisitMemberAccess under the cognitive
    // complexity bar. An unresolvable type name or registry miss is permissive (Unknown);
    // a resolved type with no such member is the hard error E1002.
    private GrobType ResolveStructFieldAccess(MemberAccessExpr node) {
        string? typeName = GetStructTypeName(node.Target);
        if (typeName is null) return GrobType.Unknown;

        UserTypeInfo? typeInfo = TryGetTypeInfo(typeName);
        if (typeInfo is null) return GrobType.Unknown;

        ResolvedFieldInfo? field = typeInfo.Fields.FirstOrDefault(f => f.Name == node.Member);
        if (field is null) {
            return EmitErrorAndReturn(ErrorCatalog.E1002,
                $"Type '{typeName}' has no member '{node.Member}'.",
                node.Range);
        }

        node.ResolvedFieldType = field.Kind;
        node.ResolvedStructTypeName = field.NamedTypeName;
        return field.Kind;
    }

    private string? GetStructTypeName(Expression target) => target switch {
        StructConstructionExpr sc => sc.TypeName,
        AnonStructExpr anon => anon.SynthesisedTypeName,
        // A struct-typed parameter carries its name on the symbol (ResolveSignatureType,
        // Sprint 6 close) since its DeclarationNode is the owning FnDecl, not a Parameter
        // (Parameter is not an AstNode) — GetStructTypeNameFromDecl cannot recover it from
        // the declaration node alone. `:=`-inferred locals still resolve via that path.
        IdentifierExpr id => LookupSymbol(id.Name)?.NamedStructTypeName ?? GetStructTypeNameFromDecl(id.Declaration),
        MemberAccessExpr ma => ma.ResolvedStructTypeName,
        _ => null
    };

    // Reached when targetType is Struct/AnonStruct and the identifier's symbol carried no
    // NamedStructTypeName (i.e. a `:=`/`readonly` local, not a struct-typed parameter —
    // see GetStructTypeName) — the binding was initialised from a StructConstructionExpr,
    // an AnonStructExpr, a call returning a struct type, or has a user-defined annotation.
    private string? GetStructTypeNameFromDecl(AstNode? decl) => decl switch {
        ReadonlyDecl ro => ExtractFromBinding(ro.AnnotatedType, ro.Value),
        VarDeclStmt vd => ExtractFromBinding(vd.AnnotatedType, vd.Initializer),
        _ => null
    };

    private string? ExtractFromBinding(TypeRef? annotation, Expression? init) =>
        annotation is not null
            ? ExtractStructName(annotation)
            : init switch {
                StructConstructionExpr sc => sc.TypeName,
                AnonStructExpr anon => anon.SynthesisedTypeName,
                // A struct-returning call (box := makeBox()) — the name was recorded by
                // VisitCall via ResolveSignatureType (Sprint 6 close).
                CallExpr call => _callResultStructNames.GetValueOrDefault(call),
                _ => null,
            };

    // Only called via the Struct/NullableStruct resolution path, where the TypeRef
    // is always a user-defined type annotation — never Array ("[]"), Function ("fn"),
    // or a builtin name. The downstream TryGet call handles any non-registered name
    // by returning null and falling through to GrobType.Unknown.
    private static string ExtractStructName(TypeRef tr) => tr.Name;

    /// <inheritdoc/>
    public override GrobType VisitIndex(IndexExpr node) {
        Visit(node.Target);
        Visit(node.Index);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves to <see cref="GrobType.Array"/> so a <c>for...in</c> subject can
    /// be recognised as iterable. Element-type tracking awaits generics (Sprint 5),
    /// so the array's element type is not inferred here — iteration over the array
    /// binds <c>item</c> as <see cref="GrobType.Unknown"/>.
    /// </remarks>
    public override GrobType VisitArrayLiteral(ArrayLiteralExpr node) {
        foreach (Expression element in node.Elements) Visit(element);
        return GrobType.Array;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates the condition is <c>bool</c> (E0001), visits both arms, and
    /// unifies their types via <see cref="UnifyTernaryArms"/> (E0001 on mismatch).
    /// </remarks>
    public override GrobType VisitTernary(TernaryExpr node) {
        GrobType condType = Visit(node.Condition);
        if (condType != GrobType.Bool && condType != GrobType.Error) {
            EmitError(ErrorCatalog.E0001,
                $"Ternary condition must be 'bool'; found '{TypeName(condType)}'.",
                node.Condition.Range);
        }
        GrobType thenType = Visit(node.Then);
        GrobType elseType = Visit(node.Else);
        return UnifyTernaryArms(thenType, elseType, node.Range);
    }

    /// <summary>
    /// Unifies the types of two ternary (or switch-expression) arms into a single
    /// result type.  Emits E0001 when the arms are incompatible.
    /// </summary>
    /// <remarks>
    /// Rules (int/float widening and T → T? promotion are the only implicit
    /// conversions; everything else is an error):
    /// <list type="bullet">
    ///   <item><description>Error or Unknown propagates silently (cascade suppression).</description></item>
    ///   <item><description>Identical types → that type.</description></item>
    ///   <item><description><c>int</c> + <c>float</c> (either order) → <c>float</c>.</description></item>
    ///   <item><description><c>T</c> + <c>T?</c> (either order) → <c>T?</c>.</description></item>
    ///   <item><description>All other combinations → E0001, returns <see cref="GrobType.Error"/>.</description></item>
    /// </list>
    /// This method is <c>internal</c> so Sprint 4E's switch-expression arm unification
    /// can reuse it without code duplication.
    /// </remarks>
    internal GrobType UnifyTernaryArms(GrobType thenType, GrobType elseType, SourceRange range) {
        if (thenType == GrobType.Error || elseType == GrobType.Error) return GrobType.Error;
        if (thenType == GrobType.Unknown || elseType == GrobType.Unknown) return GrobType.Unknown;
        if (thenType == elseType) return thenType;

        // int ↔ float implicit widening across arms.
        if ((thenType == GrobType.Int && elseType == GrobType.Float) ||
            (thenType == GrobType.Float && elseType == GrobType.Int))
            return GrobType.Float;

        // T + T? widening: one arm is nullable, the other is the non-nullable element type.
        if (GrobTypeHelpers.ToNullable(thenType) == elseType) return elseType;
        if (GrobTypeHelpers.ToNullable(elseType) == thenType) return thenType;

        EmitError(ErrorCatalog.E0001,
            $"Ternary arms must produce the same type; " +
            $"found '{TypeName(thenType)}' and '{TypeName(elseType)}'.",
            range);
        return GrobType.Error;
    }

    // -----------------------------------------------------------------------
    // Switch expression (§3.1) — patterns (D-277), exhaustiveness, arm unification.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Type-checks each arm pattern against the scrutinee (D-277), proves exhaustiveness
    /// (§3.1) — a non-exhaustive switch is <see cref="ErrorCatalog.E0505"/> — and unifies
    /// every arm result to one type via <see cref="UnifyTernaryArms"/> (E0001 on
    /// mismatch), the same mechanism the ternary uses (Increment A). The resolved type
    /// of the whole expression is the unified arm type.
    /// </remarks>
    public override GrobType VisitSwitchExpr(SwitchExprNode node) {
        GrobType subjectType = Visit(node.Subject);

        GrobType resultType = GrobType.Error;
        bool haveResult = false;
        foreach (SwitchArm arm in node.Arms) {
            CheckPattern(arm.Pattern, subjectType);
            GrobType armType = Visit(arm.Result);
            if (!haveResult) {
                resultType = armType;
                haveResult = true;
            } else {
                resultType = UnifyTernaryArms(resultType, armType, arm.Result.Range);
            }
        }

        // Cascade suppression: a subject that already failed to type-check would
        // otherwise produce a derived, misleading non-exhaustiveness diagnostic.
        if (subjectType != GrobType.Error && !IsExhaustive(subjectType, node.Arms)) {
            EmitError(ErrorCatalog.E0505,
                "Switch expression is not exhaustive; add a '_' arm or cover every possible value.",
                node.Range);
        }

        return haveResult ? resultType : GrobType.Error;
    }

    /// <summary>
    /// Type-checks one switch-expression pattern against the scrutinee type (§3.1).
    /// A value pattern must be assignable to the scrutinee (E0001), with <c>nil</c>
    /// legal only on a nullable scrutinee; a relational pattern requires an ordered
    /// scrutinee and an assignable operand (E0001); a catch-all needs no check.
    /// </summary>
    private void CheckPattern(SwitchPattern pattern, GrobType subjectType) {
        switch (pattern) {
            case ValuePattern vp:
                CheckValuePattern(vp, subjectType);
                break;
            case RelationalPattern rp:
                CheckRelationalPattern(rp, subjectType);
                break;
            case CatchAllPattern:
                break;
        }
    }

    private void CheckValuePattern(ValuePattern vp, GrobType subjectType) {
        GrobType patternType = Visit(vp.Value);
        if (patternType == GrobType.Error || subjectType == GrobType.Error) return;

        if (patternType == GrobType.Nil) {
            if (!GrobTypeHelpers.IsNullable(subjectType)) {
                EmitError(ErrorCatalog.E0001,
                    $"'nil' pattern is not valid for non-nullable scrutinee type '{TypeName(subjectType)}'.",
                    vp.Range);
            }
            return;
        }

        if (!IsPatternAssignable(patternType, subjectType)) {
            EmitError(ErrorCatalog.E0001,
                $"Switch pattern type '{TypeName(patternType)}' is not assignable to scrutinee type '{TypeName(subjectType)}'.",
                vp.Range);
        }
    }

    private void CheckRelationalPattern(RelationalPattern rp, GrobType subjectType) {
        GrobType operandType = Visit(rp.Operand);
        if (operandType == GrobType.Error || subjectType == GrobType.Error) return;

        if (!IsOrdered(subjectType)) {
            EmitError(ErrorCatalog.E0001,
                $"Relational pattern requires an ordered scrutinee ('int', 'float' or 'string'); found '{TypeName(subjectType)}'.",
                rp.Range);
            return;
        }

        if (!IsPatternAssignable(operandType, subjectType)) {
            EmitError(ErrorCatalog.E0001,
                $"Relational pattern operand type '{TypeName(operandType)}' is not assignable to scrutinee type '{TypeName(subjectType)}'.",
                rp.Range);
        }
    }

    private static bool IsOrdered(GrobType type) =>
        type == GrobType.Int || type == GrobType.Float || type == GrobType.String;

    /// <summary>
    /// A pattern type is assignable to the scrutinee when it equals the scrutinee's
    /// (nullable-unwrapped) type, or it is an <c>int</c> literal widening to a
    /// <c>float</c> scrutinee — the only implicit numeric conversion in Grob.
    /// </summary>
    private static bool IsPatternAssignable(GrobType patternType, GrobType subjectType) {
        GrobType target = GrobTypeHelpers.IsNullable(subjectType)
            ? GrobTypeHelpers.ElementType(subjectType)
            : subjectType;
        if (patternType == target) return true;
        return patternType == GrobType.Int && target == GrobType.Float;
    }

    /// <summary>
    /// Proves switch exhaustiveness (§3.1): a <c>_</c> arm, or a <c>bool</c> scrutinee
    /// with both <c>true</c> and <c>false</c> matched, or a nullable scrutinee with
    /// <c>nil</c> matched and the element type otherwise covered. Relational patterns
    /// never contribute.
    /// </summary>
    private static bool IsExhaustive(GrobType subjectType, IReadOnlyList<SwitchArm> arms) {
        if (arms.Any(arm => arm.Pattern is CatchAllPattern)) return true;

        if (subjectType == GrobType.Bool) {
            return HasBoolValueArm(arms, true) && HasBoolValueArm(arms, false);
        }

        if (GrobTypeHelpers.IsNullable(subjectType)) {
            if (!HasNilArm(arms)) return false;
            List<SwitchArm> nonNil = arms.Where(arm => !IsNilArm(arm)).ToList();
            return IsExhaustive(GrobTypeHelpers.ElementType(subjectType), nonNil);
        }

        return false;
    }

    private static bool HasBoolValueArm(IReadOnlyList<SwitchArm> arms, bool value) =>
        arms.Any(arm => arm.Pattern is ValuePattern { Value: BoolLiteralExpr b } && b.Value == value);

    private static bool IsNilArm(SwitchArm arm) =>
        arm.Pattern is ValuePattern { Value: NilLiteralExpr };

    private static bool HasNilArm(IReadOnlyList<SwitchArm> arms) =>
        arms.Any(IsNilArm);

    /// <inheritdoc/>
    /// <remarks>
    /// A numeric range only appears as a <c>for...in</c> subject. Validates that
    /// the bounds and any <c>step</c> are <c>int</c> (E0001) and that a literal
    /// descending range carries an explicit negative <c>step</c> (E0503).
    /// </remarks>
    public override GrobType VisitNumericRange(NumericRangeExpr node) {
        GrobType startType = Visit(node.Start);
        GrobType endType = Visit(node.End);
        GrobType stepType = node.Step is not null ? Visit(node.Step) : GrobType.Int;

        RequireIntRangeComponent(startType, node.Start.Range, "start bound");
        RequireIntRangeComponent(endType, node.End.Range, "end bound");
        if (node.Step is not null)
            RequireIntRangeComponent(stepType, node.Step.Range, "step");

        // A descending range needs an explicit negative step. Detectable only when
        // both bounds are integer literals; otherwise the direction is a runtime
        // property and is not rejected here.
        if (node.Step is null && IsLiteralDescending(node)) {
            EmitError(ErrorCatalog.E0503,
                "A descending numeric range requires an explicit negative 'step', as in '3..0 step -1'.",
                node.Range);
        }
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sprint 5 Increment C — D-296 categories 1–3 only (top-level references).
    ///
    /// Registers each lambda parameter as <see cref="GrobType.Unknown"/> (inferred)
    /// with the <see cref="LambdaExpr"/> as its declaring node, satisfying the §3.1.1
    /// invariant: every identifier that resolves to a parameter carries a non-null
    /// <see cref="IdentifierExpr.Declaration"/>. The body is then visited in the
    /// parameter scope so all nested identifier nodes are resolved.
    ///
    /// The inferred body return type is stored in
    /// <see cref="_lambdaReturnTypes"/> keyed by this node, so
    /// <see cref="ValidateArrayMethodCall"/> can check predicate types (E0004 on
    /// <c>filter</c>). Category-4 upvalue capture is Increment D.
    /// </remarks>
    public override GrobType VisitLambda(LambdaExpr node) {
        // Open a scope for the lambda's parameters.
        _scopes.Push(new Dictionary<string, Symbol>());
        foreach (Parameter p in node.Parameters) {
            // Lambda parameter types are inferred (not declared), so register as Unknown.
            // Use the LambdaExpr as the declaring node — Parameter is not an AstNode.
            RegisterSymbol(p.Name, GrobType.Unknown, p.Range.Start, node);
        }

        // Track a return-type context so VisitReturn can validate early-return stmts
        // inside a block-body lambda (E0005) and distinguish them from top-level
        // returns (E2203).
        _functionReturnTypes.Push(GrobType.Unknown);
        _controlFrameFloors.Push(_controlFrames.Count);

        // Infer the body type and store it for callers (e.g. ValidateArrayMethodCall).
        GrobType bodyType = node.Body switch {
            LambdaExpressionBody exprBody => Visit(exprBody.Expression),
            LambdaBlockBody blockBody => VisitLambdaBlock(blockBody),
            _ => GrobType.Unknown,
        };
        _lambdaReturnTypes[node] = bodyType;

        // Build and store the structural descriptor for this lambda (D-326).
        // Lambda parameter types are inferred (Unknown) in v1; arity is exact.
        List<GrobType> lambdaParamTypes = node.Parameters.Select(_ => GrobType.Unknown).ToList();
        _lambdaDescriptors[node] = new FunctionTypeDescriptor(lambdaParamTypes, bodyType);

        _controlFrameFloors.Pop();
        _functionReturnTypes.Pop();
        _scopes.Pop();

        // Lambdas are now typed as GrobType.Function (D-326). Callers that need
        // the body return type use _lambdaReturnTypes; callers that need the structural
        // descriptor use _lambdaDescriptors.
        return GrobType.Function;
    }

    /// <summary>
    /// Visits all statements in a block-body lambda and infers the return type.
    /// Returns the type of the last <see cref="ExpressionStmt"/>'s expression (the
    /// implicit last-expression result per D-276), or <see cref="GrobType.Nil"/> when
    /// the last statement is not an expression.
    /// </summary>
    private GrobType VisitLambdaBlock(LambdaBlockBody blockBody) {
        IReadOnlyList<AstNode> stmts = blockBody.Block.Statements;
        if (stmts.Count == 0) return GrobType.Nil;

        for (int i = 0; i < stmts.Count - 1; i++)
            Visit(stmts[i]);

        // The last statement determines the implicit return type.
        AstNode last = stmts[stmts.Count - 1];
        if (last is ExpressionStmt exprStmt)
            return Visit(exprStmt.Expression); // implicit return value; don't emit Pop
        Visit(last);
        return GrobType.Nil;
    }

    // -----------------------------------------------------------------------
    // Internal guards
    // -----------------------------------------------------------------------

    /// <summary>
    /// Unreachable: all <see cref="UnaryOperator"/> values are handled by the
    /// switch arms above. Throws an <see cref="InvalidOperationException"/> if
    /// a future sprint adds a new operator without updating <see cref="VisitUnary"/>.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Defensive guard: all UnaryOperator values are enumerated in the VisitUnary switch.")]
    private static GrobType ThrowUnknownUnaryOperator(UnaryExpr node) =>
        throw new InvalidOperationException(
            $"Unhandled unary operator '{node.Operator}' in TypeChecker.VisitUnary.");
}
