using System.Diagnostics.CodeAnalysis;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Core.NamedTypes;
using Grob.Core.PrimitiveMembers;

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

        // <, <=, >, >= require numeric (int/float, mixed ok), same-string operands, or a
        // date-vs-date pair (Sprint 9 Increment B, D-354 — LessDate/GreaterDate). Any other
        // Struct pairing (date vs. an unrelated struct, or a struct vs. a scalar) still
        // falls through to the E0002 below.
        if (BothNumeric(left, right) || (left == GrobType.String && right == GrobType.String) ||
                (left == GrobType.Struct && right == GrobType.Struct && IsDateStructPair(node))) {
            return GrobType.Bool;
        }

        return EmitErrorAndReturn(ErrorCatalog.E0002,
            $"Operator '{OperatorSymbol(node.Operator)}' cannot be applied to types '{TypeName(left)}' and '{TypeName(right)}'.",
            node.Range);
    }

    /// <summary>
    /// Reports whether both operands of a relational comparison (<c>&lt;</c>/<c>&lt;=</c>/
    /// <c>&gt;</c>/<c>&gt;=</c>) are nominally <c>date</c> — the gate that lets
    /// <see cref="Compiler"/>'s <c>ComparisonCategory</c> safely default any <c>Struct</c>
    /// category reaching its opcode-selection switch to <c>LessDate</c>/<c>GreaterDate</c>
    /// without re-deriving the struct name itself (Sprint 9 Increment B, D-354).
    /// </summary>
    private bool IsDateStructPair(BinaryExpr node) =>
        GetStructTypeName(node.Left) == "date" && GetStructTypeName(node.Right) == "date";

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
            return ResolveMemberAccessCall(node, memberAccess);
        }

        Visit(node.Callee);
        var callArgTypes = new GrobType[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++) {
            callArgTypes[i] = Visit(node.Arguments[i].Value);
        }

        // Sprint 8 Increment C: input() gets its own arity/type validation ahead of the
        // permissive built-in fallback below (D-342 — the one no-namespace native this
        // checker validates). print/exit stay fully permissive — both are void and stay
        // on their own dedicated opcodes, never reaching this call-checking machinery.
        if (node.Callee is IdentifierExpr { Declaration: BuiltinDecl { BuiltinName: "input" } }) {
            return CheckInputCall(node, callArgTypes);
        }

        // Only user-defined functions are checked positionally here. Built-ins
        // (print/exit) are permissive. A callee bound to a function-typed value
        // (D-362) still resolves a return type — via its descriptor rather than a
        // declared FnDecl signature — so GetExprType can pick the right typed
        // opcode when the call is used as an arithmetic operand; no argument
        // validation is added for this shape, that stays out of scope. Any other
        // unresolved callee is permissive.
        if (node.Callee is not IdentifierExpr { Declaration: FnDecl fn }) {
            if (ExpressionDescriptor(node.Callee) is FunctionTypeDescriptor calleeDescriptor) {
                node.ResolvedReturnType = calleeDescriptor.ReturnType;
                return calleeDescriptor.ReturnType;
            }
            return GrobType.Unknown;
        }

        CheckCall(node, fn, callArgTypes);
        // Resolve the declared return type with its structural descriptor so a call that
        // returns a function type (makeCounter(): fn(): int) flows its descriptor to the
        // binding annotation via _callResultDescriptors (D-326; Fix I). ResolveSignatureType
        // additionally recognises a struct-typed return (Sprint 6 close) and flows the
        // declared name via _callResultStructNames so `box := makeBox()` resolves field
        // access the same way a direct struct-construction initialiser already does.
        (GrobType resultKind, string? resultStructName, FunctionTypeDescriptor? resultDesc, ArrayTypeDescriptor? resultArrayDesc) =
            ResolveSignatureType(fn.ReturnType);
        if (resultDesc is not null) _callResultDescriptors[node] = resultDesc;
        if (resultStructName is not null) _callResultStructNames[node] = resultStructName;
        if (resultArrayDesc is not null) _callResultArrayDescriptors[node] = resultArrayDesc;
        node.ResolvedReturnType = resultKind;
        return resultKind;
    }

    /// <summary>
    /// Resolves a call whose callee is a member access — a namespace-qualified native
    /// (<c>math.sqrt(...)</c>, D-342), an array higher-order method (Sprint 5C), a
    /// <c>guid</c> instance method (Sprint 8 Increment D), or an unresolved member (stays
    /// permissive). Extracted from <see cref="VisitCall"/> to keep its cognitive
    /// complexity under the analyser bar.
    /// </summary>
    private GrobType ResolveMemberAccessCall(CallExpr node, MemberAccessExpr memberAccess) {
        // Sprint 8 Increment E: the chained form '<expr>.formatAs.table(...)' — checked
        // before the namespace-receiver peek below (mutually exclusive by AST shape: the
        // function form 'formatAs.table(...)' has an IdentifierExpr callee target, which
        // TryDetectFormatAsChainReceiver's MemberAccessExpr pattern never matches). Handles
        // both the valid-method and unknown-method cases itself, so neither
        // memberAccess.Target (the inner '.formatAs' node) nor the outer node is ever
        // separately visited via the generic fall-through below — avoiding a duplicate
        // bare-access diagnostic on the inner node.
        if (TryDetectFormatAsChainReceiver(memberAccess, out Expression formatAsReceiver)) {
            return ResolveFormatAsCall(node, memberAccess.Member, memberAccess.Range, formatAsReceiver, node.Arguments);
        }

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

        // D-066: primitive instance-method dispatch (string first) — compile-time sugar,
        // rewritten to a qualified native call rather than a runtime GetProperty/Bind
        // (primitives are never GrobValueKind.Struct, so NamedTypeRegistry's shape below
        // does not apply). Checked ahead of the Struct-only arms since they never overlap.
        if (PrimitiveMemberRegistry.TryGet(receiverType, out PrimitiveMemberEntry primitiveEntry)) {
            return ValidatePrimitiveMemberCall(node, memberAccess, argTypes, primitiveEntry);
        }

        // A non-optional '.' method call on a nullable registered-named-type (D-356)
        // receiver may dereference nil — reject at compile time (E0101), mirroring
        // VisitMemberAccess's identical guard for plain property access (CodeRabbit
        // review, PR #143 — this call-site arm had no nullable guard at all, so
        // `d.toIso()` on a `date?` silently resolved Unknown instead of erroring).
        // '?.' stays permissive, matching the existing property-access pattern.
        string? nullableStructName = receiverType == GrobType.NullableStruct
            ? GetStructTypeName(memberAccess.Target)
            : null;
        if (!memberAccess.IsOptional && nullableStructName is not null &&
                NamedTypeRegistry.TryGet(nullableStructName, out _)) {
            return EmitErrorAndReturn(ErrorCatalog.E0101,
                $"Member access via '.' on nullable type '{nullableStructName}?' may dereference nil. Use '?.' to chain or '??' to unwrap first.",
                memberAccess.Range);
        }

        // D-356: instance methods on a registered named type (guid, date, ...) —
        // mirrors the array higher-order-method arm above, since a named type has no
        // UserTypeInfo/declared fields for ResolveStructFieldAccess to consult (it is
        // never constructed via '{ }' braces).
        if (receiverType == GrobType.Struct && GetStructTypeName(memberAccess.Target) is string namedTypeName &&
                NamedTypeRegistry.TryGet(namedTypeName, out NamedTypeEntry namedEntry)) {
            return ValidateNamedTypeMethodCall(node, memberAccess, argTypes, namedEntry);
        }
        return GrobType.Unknown;
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
    /// Validates a registered-named-type (D-356) instance-method call and returns its
    /// result type, driven entirely by <paramref name="entry"/>'s method table rather
    /// than a per-type switch. Delegates arity/per-argument-type checking to
    /// <see cref="CheckNamedTypeMethodArgs"/>. A method flagged
    /// <see cref="NamedTypeMethod.ReturnsNominalSelf"/> (e.g. <c>date.addDays</c>
    /// returning <c>date</c>) threads the nominal name through
    /// <c>_callResultStructNames</c> (mirrors <see cref="ResolveNamespaceMemberCall"/>'s
    /// native-call threading) so a <c>:=</c>-bound result resolves further member
    /// access. An unrecognised member is <see cref="ErrorCatalog.E1002"/>, matching a
    /// user struct's unknown-member handling.
    /// </summary>
    private GrobType ValidateNamedTypeMethodCall(
            CallExpr node, MemberAccessExpr memberAccess, GrobType[] argTypes, NamedTypeEntry entry) {
        if (!entry.Methods.TryGetValue(memberAccess.Member, out NamedTypeMethod? method) || method is null) {
            return EmitErrorAndReturn(ErrorCatalog.E1002,
                $"Type '{entry.CanonicalName}' has no member '{memberAccess.Member}'.",
                memberAccess.Range);
        }
        CheckNamedTypeMethodArgs(node, memberAccess, argTypes, method.Parameters, entry.CanonicalName);
        if (method.ReturnsNominalSelf) {
            _callResultStructNames[node] = entry.CanonicalName;
            node.ResolvedReturnType = GrobType.Struct;
            return GrobType.Struct;
        }
        node.ResolvedReturnType = method.ReturnType;
        return method.ReturnType;
    }

    /// <summary>
    /// Checks a registered-named-type instance-method call's argument list against
    /// <paramref name="expected"/> — arity (<see cref="ErrorCatalog.E0003"/>) then
    /// per-argument type (<see cref="ErrorCatalog.E0004"/>). A parameter of kind
    /// <see cref="NamedTypeParameterKind.NominalSelf"/> means "must be exactly
    /// <paramref name="canonicalName"/>" (nominal, not the flat <see cref="GrobType.Struct"/>
    /// tag every struct shares — e.g. <c>date.isBefore</c>'s <c>other: date</c>
    /// parameter). Cascade suppression for an already-errored argument.
    /// </summary>
    private void CheckNamedTypeMethodArgs(
            CallExpr node, MemberAccessExpr memberAccess, GrobType[] argTypes,
            IReadOnlyList<NamedTypeParameter> expected, string canonicalName) {
        if (argTypes.Length != expected.Count) {
            string suppliedVerb = argTypes.Length == 1 ? "was" : "were";
            EmitError(ErrorCatalog.E0003,
                $"'{memberAccess.Member}' expects {expected.Count} {Plural(expected.Count, ArgumentNoun)}, but {argTypes.Length} {suppliedVerb} supplied.",
                node.Range);
            return;
        }
        for (int i = 0; i < expected.Count; i++) {
            if (argTypes[i] == GrobType.Error || argTypes[i] == GrobType.Unknown) continue;
            Expression argExpr = node.Arguments[i].Value;
            bool compatible = expected[i].Kind == NamedTypeParameterKind.NominalSelf
                ? argTypes[i] == GrobType.Struct && GetStructTypeName(argExpr) == canonicalName
                : TypesAreAssignable(argTypes[i], expected[i].Type);
            if (!compatible) {
                string expectedName = expected[i].Kind == NamedTypeParameterKind.NominalSelf
                    ? canonicalName
                    : TypeName(expected[i].Type);
                EmitError(ErrorCatalog.E0004,
                    $"Argument to '{memberAccess.Member}' has type '{TypeName(argTypes[i])}', which is not assignable to parameter of type '{expectedName}'.",
                    argExpr.Range);
            }
        }
    }

    /// <summary>
    /// Validates a primitive-receiver (D-066, <c>string</c> first) instance-method call
    /// and returns its result type, driven entirely by <paramref name="entry"/>'s method
    /// table — the primitive analogue of <see cref="ValidateNamedTypeMethodCall"/>, minus
    /// the nominal-self-return threading no primitive method needs. Sets
    /// <see cref="CallExpr.ResolvedPrimitiveNativeName"/> so the compiler rewrites the
    /// call to the qualified native, receiver injected as arg[0]. <c>split</c> is the one
    /// array-returning member in the current surface; its element is always <c>string</c>,
    /// threaded through <c>_callResultArrayDescriptors</c> (D-351) the same way a
    /// declared-return-type array threads it, so a chained index/for-in over the result
    /// resolves a real element type rather than falling back to the generic
    /// untracked-array <see cref="GrobType.Unknown"/> permissiveness.
    /// </summary>
    private GrobType ValidatePrimitiveMemberCall(
            CallExpr node, MemberAccessExpr memberAccess, GrobType[] argTypes, PrimitiveMemberEntry entry) {
        if (!entry.Methods.TryGetValue(memberAccess.Member, out PrimitiveMemberMethod? method) || method is null) {
            return EmitErrorAndReturn(ErrorCatalog.E1002,
                $"Type '{TypeName(entry.ReceiverType)}' has no member '{memberAccess.Member}'.",
                memberAccess.Range);
        }
        CheckPrimitiveMemberArgs(node, memberAccess, argTypes, method.ParameterTypes);
        node.ResolvedReturnType = method.ReturnType;
        node.ResolvedPrimitiveNativeName = method.QualifiedNativeName;
        if (method.ReturnType == GrobType.Array) {
            _callResultArrayDescriptors[node] = new ArrayTypeDescriptor(GrobType.String, null, null);
        }
        return method.ReturnType;
    }

    /// <summary>
    /// Checks a primitive-member method call's argument list against
    /// <paramref name="expected"/> — arity (<see cref="ErrorCatalog.E0003"/>) then
    /// per-argument type (<see cref="ErrorCatalog.E0004"/>). The primitive analogue of
    /// <see cref="CheckNamedTypeMethodArgs"/>, minus the <c>NominalSelf</c> parameter
    /// kind — no primitive parameter in the current surface needs "must be this exact
    /// nominal type" (every parameter is a flat <see cref="GrobType"/>).
    /// </summary>
    private void CheckPrimitiveMemberArgs(
            CallExpr node, MemberAccessExpr memberAccess, GrobType[] argTypes, IReadOnlyList<GrobType> expected) {
        if (argTypes.Length != expected.Count) {
            string suppliedVerb = argTypes.Length == 1 ? "was" : "were";
            EmitError(ErrorCatalog.E0003,
                $"'{memberAccess.Member}' expects {expected.Count} {Plural(expected.Count, ArgumentNoun)}, but {argTypes.Length} {suppliedVerb} supplied.",
                node.Range);
            return;
        }
        for (int i = 0; i < expected.Count; i++) {
            if (argTypes[i] == GrobType.Error || argTypes[i] == GrobType.Unknown) continue;
            Expression argExpr = node.Arguments[i].Value;
            if (!TypesAreAssignable(argTypes[i], expected[i])) {
                EmitError(ErrorCatalog.E0004,
                    $"Argument to '{memberAccess.Member}' has type '{TypeName(argTypes[i])}', which is not assignable to parameter of type '{TypeName(expected[i])}'.",
                    argExpr.Range);
            }
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
            ? $"{paramCount} {Plural(paramCount, ArgumentNoun)}"
            : $"between {required} and {paramCount} arguments";
        string suppliedVerb = supplied == 1 ? "was" : "were";
        EmitError(ErrorCatalog.E0003,
            $"Function '{fn.Name}' expects {expectation}, but {supplied} {suppliedVerb} supplied.",
            node.Range);
    }

    /// <summary>Returns <paramref name="noun"/> pluralised for a count of <paramref name="n"/>.</summary>
    private static string Plural(int n, string noun) => n == 1 ? noun : noun + "s";

    // SonarCloud S1192: "argument" is the noun in every arity-mismatch message across
    // this file (positional-call, native-call and date-method arity checks) — a shared
    // constant rather than four independent literals.
    private const string ArgumentNoun = "argument";

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
        (GrobType paramType, string? paramNamedTypeName, FunctionTypeDescriptor? paramDesc, ArrayTypeDescriptor? paramArrayDesc) = fn.Parameters[paramIndex].Type is not null
            ? ResolveSignatureType(fn.Parameters[paramIndex].Type!)
            : (GrobType.Unknown, null, null, null);
        bool isFunctionParam = paramType == GrobType.Function || paramType == GrobType.NullableFunction;
        bool isArrayParam = paramType == GrobType.Array || paramType == GrobType.NullableArray;
        bool compatible;
        if (isFunctionParam) {
            FunctionTypeDescriptor? argDesc = argExpr is not null ? InitialiserDescriptor(argExpr) : null;
            compatible = TypesAreAssignable(argType, paramType, argDesc, paramDesc);
        } else {
            compatible = TypesAreAssignable(argType, paramType);
        }
        // Array element type (D-351): the flat GrobType.Array tag alone does not
        // distinguish int[] from string[], so an argument whose element type disagrees
        // with the parameter's declared element type must still be rejected.
        if (compatible && isArrayParam && argExpr is not null &&
                !ArrayElementAssignable(ArrayDescriptorOf(argExpr), paramArrayDesc)) {
            compatible = false;
        }
        // Struct nominal identity (originally CodeRabbit review, PR #133, scoped to guid;
        // generalised to all named structs — fix/compiler-struct-nominal-identity): the
        // flat GrobType.Struct tag alone does not distinguish one named struct from
        // another, so an argument constructed as a different struct than the parameter
        // declares (Config vs Other, or guid vs a user struct) must still be rejected.
        if (compatible && argExpr is not null && IsStructNominalMismatch(paramType, paramNamedTypeName, argExpr)) {
            compatible = false;
        }
        if (paramType != GrobType.Unknown && argType != GrobType.Error && !compatible) {
            EmitError(ErrorCatalog.E0004,
                $"Argument to '{fn.Name}' has type '{TypeName(argType)}', which is not assignable to parameter '{fn.Parameters[paramIndex].Name}' of type '{TypeName(paramType)}'.",
                valueRange);
        }
    }

    /// <summary>
    /// Reports whether a struct-typed target (a parameter, field or return position with
    /// <paramref name="paramType"/> <see cref="GrobType.Struct"/> or
    /// <see cref="GrobType.NullableStruct"/> and declared name <paramref name="paramNamedTypeName"/>)
    /// and <paramref name="argExpr"/>'s own struct name disagree — nominal identity, not
    /// merely the flat <see cref="GrobType.Struct"/> tag, which every named struct shares.
    /// Either side missing a resolvable name (a struct-typed value the checker cannot name,
    /// or a non-struct target) is left alone — this only rejects a definite mismatch between
    /// two known names, one of which may be <c>"guid"</c>.
    /// <para>
    /// The <c>GrobError</c> hierarchy (D-284) is an exception to plain name equality: a
    /// subtype (a leaf, or the root itself) is assignable to any of its ancestors, mirroring
    /// <c>catch</c>'s subtype-matching semantics (the <c>throw</c> check in
    /// <c>TypeChecker.Statements.cs</c> and the <c>catch</c> check in
    /// <c>TypeChecker.ControlFlow.cs</c>, both of which already consult
    /// <see cref="ExceptionHierarchy.IsSubtypeOf"/> directly). Nominal identity applies
    /// only across the hierarchy (an unrelated struct is still rejected), never within it.
    /// </para>
    /// </summary>
    private bool IsStructNominalMismatch(GrobType paramType, string? paramNamedTypeName, Expression argExpr) {
        if (paramType is not (GrobType.Struct or GrobType.NullableStruct)) return false;
        if (paramNamedTypeName is null) return false;

        string? argNamedTypeName = GetStructTypeName(argExpr);
        if (argNamedTypeName is null) return false;

        if (paramNamedTypeName == argNamedTypeName) return false;

        // A supplied value's type that is a hierarchy subtype of the declared type is
        // assignable regardless of the differing name (leaf-to-root, or leaf-to-leaf via a
        // shared ancestor — the root has no leaf descendants other than itself under
        // IsSubtypeOf's reflexive walk, so this only ever admits leaf-to-root or root-to-root).
        if (ExceptionHierarchy.IsHierarchyMember(paramNamedTypeName)
                && ExceptionHierarchy.IsHierarchyMember(argNamedTypeName)
                && ExceptionHierarchy.IsSubtypeOf(argNamedTypeName, paramNamedTypeName)) {
            return false;
        }

        return true;
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

        // Sprint 8 Increment D: the two-level namespace chain guid.namespaces.dns.
        // TryAnnotateNamespaceReceiver only recognises a bare identifier receiver, so a
        // chain this deep (node.Target is itself guid.namespaces, a MemberAccessExpr, not
        // an IdentifierExpr) falls through the check above. Rather than generalise
        // namespace-receiver resolution to arbitrary dotted depth, this flattens the
        // specific two-level shape into one lookup key ("namespaces.dns") registered as a
        // flat member of the "guid" namespace (D-149) — see NamespaceRegistry.
        if (TryFlattenNestedNamespaceMember(node, out string flatNamespaceName, out string flatMemberName)) {
            return ResolveNamespaceMemberAccess(node, flatNamespaceName, flatMemberName);
        }

        GrobType targetType = Visit(node.Target);

        // Cascade suppression: if the target already errored, don't pile on.
        if (targetType == GrobType.Error) return GrobType.Error;

        // Sprint 8 Increment E: a bare '<expr>.formatAs' (D-282/D-320's reserved
        // identifier), not consumed by ResolveFormatAsCall's chain detection — which
        // handles '<expr>.formatAs.table()' etc. without ever visiting this node — means
        // the source wrote '.formatAs' with no valid method chained after it. 'formatAs'
        // can never be a real field/namespace member (E1103 blocks any user binding named
        // it), so this check is unconditional on node.Target's type.
        if (node.Member == "formatAs") {
            node.ResolvedFieldType = GrobType.Error;
            return EmitErrorAndReturn(ErrorCatalog.E1004,
                "formatAs is a compiler-namespace, not a property. Use .formatAs.table(), .formatAs.list(), or .formatAs.csv().",
                node.Range);
        }

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

        // D-356: instance properties on a registered named type (guid, date, ...) —
        // none has UserTypeInfo/declared fields (never constructed via '{ }' braces) for
        // ResolveStructFieldAccess to consult, so their property surfaces are resolved
        // directly by struct name ahead of the field-access fall-through below.
        if (TryResolveKnownStructPropertyAccess(node, targetType) is GrobType knownStructResult) {
            return knownStructResult;
        }

        // D-066: primitive instance-property dispatch (string first). Only ever reached
        // for a non-nullable receiver — the generic nullable guards above (lines checking
        // GrobTypeHelpers.IsNullable(targetType)) already reject or short-circuit a
        // nullable receiver before this point, so no extra nullable handling is needed
        // here (mirrors TryResolveKnownStructPropertyAccess's identical latitude).
        if (PrimitiveMemberRegistry.TryGet(targetType, out PrimitiveMemberEntry primitiveEntry)) {
            return ResolvePrimitiveMemberPropertyAccess(node, primitiveEntry);
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

    /// <summary>
    /// Resolves a bare property access on a registered named type's (D-356) struct
    /// receiver by name, or <see langword="null"/> when <paramref name="targetType"/> is
    /// not <see cref="GrobType.Struct"/> or the receiver names no registered type — the
    /// shared dispatch <see cref="VisitMemberAccess"/> consults, extracted to keep its
    /// own cognitive complexity under the analyser bar.
    /// </summary>
    private GrobType? TryResolveKnownStructPropertyAccess(MemberAccessExpr node, GrobType targetType) {
        if (targetType != GrobType.Struct) return null;
        string? structTypeName = GetStructTypeName(node.Target);
        return structTypeName is not null && NamedTypeRegistry.TryGet(structTypeName, out NamedTypeEntry entry)
            ? ResolveNamedTypePropertyAccess(node, entry)
            : null;
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
    /// Reports whether <paramref name="node"/> is the outer step of a two-level namespace
    /// member chain (<c>guid.namespaces.dns</c>, Sprint 8 Increment D) — <paramref
    /// name="node"/>'s own target is itself a <see cref="MemberAccessExpr"/> whose target
    /// is a namespace identifier, AND the flattened two-segment key
    /// (<c>"namespaces.dns"</c>) is an actually-registered member. That last check is
    /// what distinguishes a genuine nested-namespace chain from an ordinary instance-member
    /// access on a namespace CONSTANT's value (<c>guid.empty.isEmpty</c> — <c>guid.empty</c>
    /// is one namespace-qualified segment, and <c>.isEmpty</c> is a plain instance property
    /// on the resulting guid value, not a second namespace segment); without the registry
    /// gate the flattening would misfire on that shape, looking up the nonsensical key
    /// <c>"empty.isEmpty"</c>. Flattens rather than generalising namespace-receiver
    /// resolution to arbitrary dotted depth (D-149; see <see cref="NamespaceRegistry"/>'s
    /// flat-keyed registration). Annotates the innermost identifier for §3.1.1, exactly as
    /// <see cref="TryAnnotateNamespaceReceiver"/> does for the one-level case.
    /// </summary>
    private bool TryFlattenNestedNamespaceMember(MemberAccessExpr node, out string namespaceName, out string memberName) {
        namespaceName = string.Empty;
        memberName = string.Empty;
        if (node.Target is not MemberAccessExpr inner) return false;
        if (!TryAnnotateNamespaceReceiver(inner.Target, out string innerNamespaceName)) return false;

        string candidateMemberName = $"{inner.Member}.{node.Member}";
        if (NamespaceRegistry.TryGetMember(innerNamespaceName, candidateMemberName) is null) return false;

        namespaceName = innerNamespaceName;
        memberName = candidateMemberName;
        return true;
    }

    /// <summary>
    /// Resolves a bare namespace member access (<c>math.pi</c>). A constant member returns
    /// its declared type; a native member accessed without a call is a callable member typed
    /// as <see cref="GrobType.Function"/> (mirroring a bare user-fn reference); an unknown
    /// member is <see cref="ErrorCatalog.E1003"/>. <paramref name="memberNameOverride"/> is
    /// supplied by <see cref="TryFlattenNestedNamespaceMember"/> for the two-level chain,
    /// where the lookup key is not <paramref name="node"/>'s own <c>Member</c> alone.
    /// </summary>
    private GrobType ResolveNamespaceMemberAccess(
            MemberAccessExpr node, string namespaceName, string? memberNameOverride = null) {
        string memberName = memberNameOverride ?? node.Member;
        object? member = NamespaceRegistry.TryGetMember(namespaceName, memberName);
        switch (member) {
            case NamespaceRegistry.ConstantMember constant:
                node.ResolvedFieldType = constant.Type;
                node.ResolvedStructTypeName = constant.NamedTypeName;
                return constant.Type;
            case NamespaceRegistry.NativeMember:
                node.ResolvedFieldType = GrobType.Function;
                return GrobType.Function;
            default:
                node.ResolvedFieldType = GrobType.Error;
                return EmitErrorAndReturn(ErrorCatalog.E1003,
                    $"Namespace '{namespaceName}' has no member '{memberName}'.",
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
        // Sprint 8 Increment E: the function form 'formatAs.table(items, ...)' — bespoke
        // resolution (ResolveFormatAsCall), not the generic ConstantMember/NativeMember
        // dispatch below. Here the receiver is the call's own first argument (unlike the
        // chained form, where it is synthesised from the AST shape); a 0-argument call has
        // no receiver to hand over, so ResolveFormatAsCall reports the arity error itself.
        if (namespaceName == "formatAs") {
            return ResolveFormatAsCall(node, memberAccess.Member, memberAccess.Range,
                node.Arguments.Count > 0 ? node.Arguments[0].Value : null,
                node.Arguments.Count > 0 ? node.Arguments.Skip(1).ToList() : node.Arguments);
        }

        var argTypes = new GrobType[node.Arguments.Count];
        for (int i = 0; i < node.Arguments.Count; i++)
            argTypes[i] = Visit(node.Arguments[i].Value);

        object? member = NamespaceRegistry.TryGetMember(namespaceName, memberAccess.Member);
        if (member is NamespaceRegistry.NativeMember native) {
            memberAccess.ResolvedFieldType = native.ReturnType;
            node.ResolvedReturnType = native.ReturnType;
            CheckNativeCall(node, namespaceName, memberAccess.Member, native, argTypes);
            // Sprint 8 Increment D: mirrors the user-fn struct-return threading a few
            // lines up in VisitCall — a `:=`-inferred binding from a struct-returning
            // native call (id := guid.newV4()) resolves field/instance-member access the
            // same way a direct struct-construction initialiser already does.
            if (native.NamedTypeName is not null) _callResultStructNames[node] = native.NamedTypeName;
            CheckGuidParseLiteral(node, namespaceName, memberAccess.Member);
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

    // -----------------------------------------------------------------------
    // formatAs (Sprint 8 Increment E) — the collection-to-string terminators.
    // Not modelled as ordinary NamespaceRegistry ConstantMember/NativeMember entries
    // (see NamespaceRegistry's "formatAs" entry): compile-time column derivation from
    // the Sprint 6 field registry, and the chained-form receiver rewrite, need bespoke
    // resolution the generic positional NativeMember model does not fit. Both call
    // shapes route into the one resolution core below, ResolveFormatAsCall:
    //   function form  formatAs.table(items)   — via ResolveNamespaceMemberCall
    //   chained form   items.formatAs.table()  — via ResolveMemberAccessCall
    // logically one compile-time rewrite (D-282/D-320), implemented as shared
    // resolution rather than a literal AST substitution (CallExpr/MemberAccessExpr are
    // immutable records with no settable Callee/Target).
    // -----------------------------------------------------------------------

    private enum FormatAsShape { Array, Scalar }

    private static readonly IReadOnlyDictionary<string, FormatAsShape> FormatAsMethods =
        new Dictionary<string, FormatAsShape>(StringComparer.Ordinal) {
            ["table"] = FormatAsShape.Array,
            ["csv"] = FormatAsShape.Array,
            ["list"] = FormatAsShape.Scalar,
        };

    /// <summary>
    /// Detects the chained form's inner receiver: <c>items.formatAs.table(...)</c> parses
    /// as <c>CallExpr(Callee: MemberAccessExpr(Member: methodName, Target:
    /// MemberAccessExpr(Member: "formatAs", Target: items)))</c>. <c>formatAs</c> is a
    /// reserved identifier (D-282/D-320), so no struct field, namespace member or declared
    /// binding can ever be named <c>formatAs</c> — any <see cref="MemberAccessExpr"/> whose
    /// <c>Member</c> is <c>"formatAs"</c> is unambiguously this mechanism, regardless of
    /// what its own <c>Target</c> resolves to (including the degenerate
    /// <c>formatAs.formatAs.table()</c>, which simply fails later when the receiver
    /// — the bare namespace identifier — is visited as an ordinary value and trips the
    /// existing E1004 namespace-as-value arm).
    /// </summary>
    private static bool TryDetectFormatAsChainReceiver(MemberAccessExpr callee, out Expression receiverExpr) {
        if (callee.Target is MemberAccessExpr { Member: "formatAs" } inner) {
            receiverExpr = inner.Target;
            return true;
        }
        receiverExpr = null!;
        return false;
    }

    /// <summary>
    /// Resolves a <c>formatAs</c> call, unifying the function form
    /// (<paramref name="receiverExpr"/> is the call's own first argument, <see
    /// langword="null"/> when none was supplied) and the chained form (<paramref
    /// name="receiverExpr"/> is the chain's inner receiver, always present). Validates the
    /// method name (E1003), the receiver's shape (E0004 — array for <c>table</c>/<c>csv</c>,
    /// a struct value for <c>list</c>), derives the ordered column list from the Sprint 6
    /// field registry, and stores it on <paramref name="node"/> for the compiler.
    /// </summary>
    private GrobType ResolveFormatAsCall(
            CallExpr node, string methodName, SourceRange methodNameRange,
            Expression? receiverExpr, IReadOnlyList<CallArgument> extraArgs) {
        if (!FormatAsMethods.TryGetValue(methodName, out FormatAsShape shape)) {
            if (receiverExpr is not null) Visit(receiverExpr);
            foreach (CallArgument arg in extraArgs) Visit(arg.Value);
            return EmitErrorAndReturn(ErrorCatalog.E1003,
                $"formatAs has no method '{methodName}'. Valid methods are .table(), .list(), and .csv().",
                methodNameRange);
        }

        if (receiverExpr is null) {
            foreach (CallArgument arg in extraArgs) Visit(arg.Value);
            return EmitErrorAndReturn(ErrorCatalog.E0003,
                $"'formatAs.{methodName}' expects at least 1 argument, but 0 were supplied.",
                node.Range);
        }

        GrobType receiverType = Visit(receiverExpr);
        bool receiverShapeOk = IsFormatAsReceiverShapeValid(methodName, shape, receiverType, receiverExpr);
        CallArgument? columnsArg = BindFormatAsExtraArgs(methodName, extraArgs);

        IReadOnlyList<string>? fullFields = shape == FormatAsShape.Array
            ? GetArrayElementFieldNames(receiverExpr)
            : GetStructFieldNames(receiverExpr);

        IReadOnlyList<string>? finalColumns = fullFields;
        if (columnsArg is not null) {
            finalColumns = ResolveExplicitColumns(columnsArg, fullFields);
        } else if (receiverShapeOk && fullFields is null) {
            EmitError(ErrorCatalog.E0004,
                $"'formatAs.{methodName}' cannot determine its columns at compile time from this argument. " +
                "Use a directly-typed array/struct value, or select 'columns:' explicitly.",
                receiverExpr.Range);
        }

        node.ResolvedFormatAsColumns = finalColumns ?? [];
        // A formatAs.table/list/csv call renders to string — persist it for GetExprType
        // (D-362) so the result can be used as a string-concat operand without falling to
        // the Unknown→Int arithmetic default.
        node.ResolvedReturnType = GrobType.String;
        return GrobType.String;
    }

    /// <summary>
    /// Validates an explicit <c>columns: [...]</c> selection against the receiver's full
    /// derived field list, in the order written. A non-literal array (a variable, a call
    /// result) stays permissive and keeps the full list — v1 only validates the literal
    /// case, since that is the only one knowable at this call site. An unrecognised name
    /// reuses E0004 (the selection cannot be satisfied by the receiver's actual shape).
    /// </summary>
    /// <summary>
    /// Validates a <c>formatAs</c> receiver's shape (argument-type-mismatch, E0004) — an
    /// array for <c>table</c>/<c>csv</c>, a struct/anon-struct value for <c>list</c>.
    /// <see cref="GrobType.Error"/>/<see cref="GrobType.Unknown"/> stay permissive. Extracted
    /// from <see cref="ResolveFormatAsCall"/> to keep its cognitive complexity under the
    /// analyser bar.
    /// </summary>
    private bool IsFormatAsReceiverShapeValid(
            string methodName, FormatAsShape shape, GrobType receiverType, Expression receiverExpr) {
        bool ok = receiverType is GrobType.Error or GrobType.Unknown ||
            (shape == FormatAsShape.Array
                ? receiverType == GrobType.Array
                : receiverType is GrobType.Struct or GrobType.AnonStruct);
        if (!ok) {
            string expected = shape == FormatAsShape.Array ? "an array" : "a struct value";
            EmitError(ErrorCatalog.E0004,
                $"'formatAs.{methodName}' expects {expected}, but the argument has type '{TypeName(receiverType)}'.",
                receiverExpr.Range);
        }
        return ok;
    }

    /// <summary>
    /// Visits and binds a <c>formatAs</c> call's arguments beyond the receiver: <c>columns:</c>
    /// (<c>table</c> only) selects/reorders the derived field list; any other extra
    /// argument is a call-shape error (E0011). Extracted from <see cref="ResolveFormatAsCall"/>
    /// to keep its cognitive complexity under the analyser bar.
    /// </summary>
    private CallArgument? BindFormatAsExtraArgs(string methodName, IReadOnlyList<CallArgument> extraArgs) {
        CallArgument? columnsArg = null;
        foreach (CallArgument arg in extraArgs) {
            Visit(arg.Value);
            if (arg.Name == "columns" && methodName == "table") {
                columnsArg = arg;
            } else {
                EmitError(ErrorCatalog.E0011,
                    arg.Name is not null
                        ? $"'formatAs.{methodName}' has no argument named '{arg.Name}'."
                        : $"'formatAs.{methodName}' does not accept a second positional argument.",
                    arg.Range);
            }
        }
        return columnsArg;
    }

    private IReadOnlyList<string>? ResolveExplicitColumns(CallArgument columnsArg, IReadOnlyList<string>? fullFields) {
        if (columnsArg.Value is not ArrayLiteralExpr arrayLit) return fullFields;

        // Explicit .Where filter over the literal elements that actually resolve to a
        // compile-time string (CodeQL cs/linq/missed-where — the prior foreach's leading
        // 'if (...) continue;' was an implicit filter).
        var literalNames = arrayLit.Elements
            .Select(element => (Element: element, Ok: TryGetGuidParseLiteralValue(element, out string name, out _), Name: name))
            .Where(entry => entry.Ok);

        var selected = new List<string>(arrayLit.Elements.Count);
        foreach ((Expression element, _, string name) in literalNames) {
            if (fullFields is not null && !fullFields.Contains(name)) {
                EmitError(ErrorCatalog.E0004,
                    $"'columns' names '{name}', which is not a field of the table's element type.",
                    element.Range);
                continue;
            }
            selected.Add(name);
        }
        return selected;
    }

    /// <summary>
    /// Derives the ordered field-name list for a <c>formatAs.list</c> single-item receiver:
    /// a literal anonymous-struct's own field order, or a named struct's registered field
    /// order (via <see cref="GetStructTypeName"/>, reused unchanged from the existing
    /// scalar struct-field-access machinery). <see langword="null"/> when the shape cannot
    /// be determined statically.
    /// </summary>
    private IReadOnlyList<string>? GetStructFieldNames(Expression scalarExpr) {
        if (TryGetAnonStructLiteral(scalarExpr) is AnonStructExpr anon) {
            return anon.Fields.Select(f => f.Name).ToList();
        }
        // An indexed array element (items[0].formatAs.list()) shares its array's element
        // shape — peek the array itself via GetArrayElementFieldNames rather than the
        // (non-existent) type name of an IndexExpr.
        if (scalarExpr is IndexExpr index) {
            return GetArrayElementFieldNames(index.Target);
        }
        string? typeName = GetStructTypeName(scalarExpr);
        return typeName is not null ? TryGetTypeInfo(typeName)?.Fields.Select(f => f.Name).ToList() : null;
    }

    /// <summary>
    /// Derives the ordered field-name list for a <c>formatAs.table</c>/<c>csv</c> array
    /// receiver by pattern-matching the argument expression's own shape — a local,
    /// formatAs-scoped peek. Handles: an array literal (from its first element's shape); a
    /// <c>.select(lambda)</c> result (from the lambda body's returned expression — reusing
    /// <see cref="GetStructFieldNames"/> directly against that expression node, since
    /// <c>AnonStructExpr.SynthesisedTypeName</c>/field list is already set by the time the
    /// lambda body was visited); a <c>.filter(...)</c>/<c>.sort(...)</c> result
    /// (pass-through — neither changes element shape, so recurses into their own receiver);
    /// and an identifier bound to a <c>T[]</c>-annotated parameter or a <c>:=</c>-inferred
    /// local (via <see cref="Symbol.ArrayDescriptor"/>, D-351, or the declaration's
    /// annotation/initialiser). <see langword="null"/> when none of these shapes match.
    /// </summary>
    private IReadOnlyList<string>? GetArrayElementFieldNames(Expression arrayExpr) {
        switch (arrayExpr) {
            case ArrayLiteralExpr { Elements: [var first, ..] }:
                return GetStructFieldNames(first);

            case CallExpr { Callee: MemberAccessExpr { Member: "select" } } call
                    when call.Arguments is [{ Value: LambdaExpr { Body: LambdaExpressionBody body } }, ..]:
                return GetStructFieldNames(body.Expression);

            case CallExpr { Callee: MemberAccessExpr { Member: "filter" or "sort" } passThrough }:
                return GetArrayElementFieldNames(passThrough.Target);

            case IdentifierExpr id:
                return LookupSymbol(id.Name) is { ArrayDescriptor.ElementNamedTypeName: string elementName }
                    ? TryGetTypeInfo(elementName)?.Fields.Select(f => f.Name).ToList()
                    : GetArrayElementFieldNamesFromDecl(id.Declaration);

            default:
                return null;
        }
    }

    /// <summary>
    /// Falls back to a <c>:=</c>-inferred array local's declaration (no <see
    /// cref="Symbol.ArrayDescriptor"/> match — a locally-declared struct array whose
    /// element name resolution reaches this method some other way): an explicit
    /// <c>T[]</c> annotation names the element type directly, otherwise the initialiser
    /// expression is peeked the same way <see cref="GetArrayElementFieldNames"/> peeks any
    /// other array expression.
    /// </summary>
    private IReadOnlyList<string>? GetArrayElementFieldNamesFromDecl(AstNode? decl) {
        (TypeRef? annotation, Expression? init) = decl switch {
            ReadonlyDecl ro => (ro.AnnotatedType, ro.Value),
            VarDeclStmt vd => (vd.AnnotatedType, vd.Initializer),
            _ => (null, null),
        };
        if (annotation is ArrayTypeRef arrayAnnotation) {
            string? elementName = TryGetNamedStructTypeName(arrayAnnotation.ElementType);
            if (elementName is not null) return TryGetTypeInfo(elementName)?.Fields.Select(f => f.Name).ToList();
        }
        return init is not null ? GetArrayElementFieldNames(init) : null;
    }

    /// <summary>
    /// Peeks whether <paramref name="expr"/> is (or, for a <c>:=</c>-inferred local,
    /// resolves to) an anonymous-struct literal directly — the node itself, so its field
    /// order (<see cref="AnonStructExpr.Fields"/>, source order) is read straight off the
    /// AST rather than re-derived from <see cref="AnonStructExpr.SynthesisedTypeName"/>'s
    /// sorted canonical signature, which does not preserve source order.
    /// </summary>
    private static AnonStructExpr? TryGetAnonStructLiteral(Expression expr) => expr switch {
        AnonStructExpr anon => anon,
        IdentifierExpr id => id.Declaration switch {
            ReadonlyDecl ro => ro.Value as AnonStructExpr,
            VarDeclStmt vd => vd.Initializer as AnonStructExpr,
            _ => null,
        },
        _ => null,
    };

    // -----------------------------------------------------------------------
    // input() — the one no-namespace native validated here (Sprint 8 Increment C).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates an <c>input()</c> call: 0 or 1 arguments (E0003 outside that range), and
    /// when supplied, the single argument must be (or widen to) <c>string</c> (E0004).
    /// Reuses the same two codes <see cref="CheckNativeCall"/> uses for namespaced natives
    /// rather than allocating dedicated ones for this single bare-name call site. Always
    /// resolves to <c>string</c> — the runtime native's own return type — even on an
    /// arity/type mismatch, so a caller's own annotation mismatch is not double-reported.
    /// </summary>
    private GrobType CheckInputCall(CallExpr node, GrobType[] argTypes) {
        // input() always resolves to string — persist it for GetExprType (D-362) on both
        // the arity-error and success returns below, so `input() + "x"` selects Concat
        // rather than falling to the Unknown→Int arithmetic default.
        node.ResolvedReturnType = GrobType.String;
        if (argTypes.Length > 1) {
            EmitError(ErrorCatalog.E0003,
                $"'input' expects 0 or 1 arguments, but {argTypes.Length} were supplied.",
                node.Range);
            return GrobType.String;
        }

        if (argTypes.Length == 1 && argTypes[0] != GrobType.Error &&
                !TypesAreAssignable(argTypes[0], GrobType.String)) {
            EmitError(ErrorCatalog.E0004,
                $"Argument 1 to 'input' has type '{TypeName(argTypes[0])}', which is not assignable to parameter of type 'string'.",
                node.Arguments[0].Value.Range);
        }

        return GrobType.String;
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
            string suppliedVerb = argTypes.Length == 1 ? "was" : "were";
            EmitError(ErrorCatalog.E0003,
                $"'{namespaceName}.{memberName}' expects {expected} {Plural(expected, ArgumentNoun)}, but {argTypes.Length} {suppliedVerb} supplied.",
                node.Range);
            return;
        }

        for (int i = 0; i < expected; i++) {
            CheckNativeArgumentType(node, namespaceName, memberName, argTypes, i, member.ParameterTypes[i],
                ParameterNamedTypeNameAt(member, i));
        }
    }

    /// <summary>Returns the declared nominal struct name for fixed parameter <paramref name="index"/>, if any.</summary>
    private static string? ParameterNamedTypeNameAt(NamespaceRegistry.NativeMember member, int index) =>
        member.ParameterNamedTypeNames is { } names && index < names.Count ? names[index] : null;

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
            string suppliedVerb = argTypes.Length == 1 ? "was" : "were";
            EmitError(ErrorCatalog.E0003,
                $"'{namespaceName}.{memberName}' expects at least {minimum} {Plural(minimum, ArgumentNoun)}, but {argTypes.Length} {suppliedVerb} supplied.",
                node.Range);
            return;
        }

        for (int i = 0; i < fixedCount; i++) {
            CheckNativeArgumentType(node, namespaceName, memberName, argTypes, i, member.ParameterTypes[i],
                ParameterNamedTypeNameAt(member, i));
        }

        for (int i = fixedCount; i < argTypes.Length; i++)
            CheckNativeArgumentType(node, namespaceName, memberName, argTypes, i, variadicType);
    }

    /// <summary>
    /// Checks one native-call argument against <paramref name="targetType"/> (E0004, at
    /// the argument's own location), with cascade suppression for an already-errored
    /// argument. Shared by <see cref="CheckNativeCall"/>'s fixed-arity loop and
    /// <see cref="CheckVariadicNativeCall"/>'s fixed-prefix and variadic-tail loops, so
    /// the assignability check, cascade suppression and error format stay in one place.
    /// </summary>
    private void CheckNativeArgumentType(CallExpr node, string namespaceName, string memberName,
            GrobType[] argTypes, int index, GrobType targetType, string? targetNamedTypeName = null) {
        if (argTypes[index] == GrobType.Error) return; // cascade suppression
        Expression argExpr = node.Arguments[index].Value;
        bool compatible = TypesAreAssignable(argTypes[index], targetType);
        // Struct nominal identity — see IsStructNominalMismatch's doc comment. guid.newV5's
        // namespace parameter is the native argument this most commonly guards today, but
        // the check is general across any named struct target.
        if (compatible && targetNamedTypeName is not null &&
                IsStructNominalMismatch(targetType, targetNamedTypeName, argExpr)) {
            compatible = false;
        }
        if (!compatible) {
            EmitError(ErrorCatalog.E0004,
                $"Argument {index + 1} to '{namespaceName}.{memberName}' has type '{TypeName(argTypes[index])}', which is not assignable to parameter of type '{TypeName(targetType)}'.",
                argExpr.Range);
        }
    }

    /// <summary>
    /// Compile-time literal validation for <c>guid.parse(s)</c> (D-149): when the sole
    /// argument is a plain string literal — either a hole-free
    /// <see cref="InterpolatedStringExpr"/> (the same "is this actually a literal" test
    /// the constant folder in <c>Compiler.cs</c> uses) or a <see cref="RawStringLiteralExpr"/>
    /// (backtick string; CodeRabbit review, PR #133 — the raw form is a compile-time
    /// literal too and was previously left unchecked) — and its value is not a parseable
    /// GUID, emits <see cref="ErrorCatalog.E0601"/> instead of leaving the malformed
    /// literal to fail at runtime. A non-literal argument (a variable, a call result, a
    /// genuinely interpolated string, …) is unaffected — it stays on the ordinary runtime
    /// <c>ParseError</c> (E5701) path, since its value cannot be known at compile time.
    /// </summary>
    private void CheckGuidParseLiteral(CallExpr node, string namespaceName, string memberName) {
        if (namespaceName != "guid" || memberName != "parse") return;
        if (node.Arguments.Count != 1) return; // arity mismatch already reported by CheckNativeCall
        if (!TryGetGuidParseLiteralValue(node.Arguments[0].Value, out string value, out SourceRange literalRange)) {
            return;
        }
        if (Guid.TryParse(value, out _)) return;

        EmitError(ErrorCatalog.E0601,
            $"'{value}' is not a valid guid literal.",
            literalRange);
    }

    /// <summary>
    /// Extracts the literal string value of <paramref name="argument"/> when it is a
    /// compile-time string literal — a hole-free <see cref="InterpolatedStringExpr"/> or
    /// a <see cref="RawStringLiteralExpr"/> — and <see langword="false"/> for anything
    /// else (a variable, a call result, a genuinely interpolated string).
    /// </summary>
    private static bool TryGetGuidParseLiteralValue(Expression argument, out string value, out SourceRange range) {
        switch (argument) {
            case InterpolatedStringExpr { Parts: var parts } interpolated when parts.All(p => p is StringTextPart):
                value = string.Concat(parts.OfType<StringTextPart>().Select(p => p.Text));
                range = interpolated.Range;
                return true;
            case RawStringLiteralExpr raw:
                value = raw.Value;
                range = raw.Range;
                return true;
            default:
                value = string.Empty;
                range = argument.Range;
                return false;
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

    /// <summary>
    /// Resolves a bare instance-property access on a registered named type (D-356) —
    /// e.g. <c>id.version</c>, <c>d.year</c>. Method-family members are resolved only
    /// via a call (<see cref="ValidateNamedTypeMethodCall"/>) — mirrors how a bare array
    /// higher-order-method reference (<c>arr.filter</c>, no call) is not specially
    /// resolved here either.
    /// </summary>
    private GrobType ResolveNamedTypePropertyAccess(MemberAccessExpr node, NamedTypeEntry entry) {
        if (entry.Properties.TryGetValue(node.Member, out NamedTypeProperty? property) && property is not null) {
            node.ResolvedFieldType = property.Type;
            return property.Type;
        }

        // A bare unrecognised member must not survive as Unknown — it would otherwise
        // reach the VM, which has no dispatch for it and throws an internal exception
        // rather than failing cleanly (CodeRabbit review, PR #133). Matches how an
        // unrecognised method call is already rejected (ValidateNamedTypeMethodCall) and
        // how a user struct's unknown field is rejected (ResolveStructFieldAccess).
        return EmitErrorAndReturn(ErrorCatalog.E1002,
            $"Type '{entry.CanonicalName}' has no member '{node.Member}'.",
            node.Range);
    }

    /// <summary>
    /// Resolves a bare (non-call) property access on a primitive receiver (D-066,
    /// <c>string</c> first), the primitive analogue of
    /// <see cref="ResolveNamedTypePropertyAccess"/>. Consults <c>entry.Properties</c>
    /// only — a method name accessed without a call (<c>s.trim</c>, no parens) is not a
    /// property and stays <see cref="ErrorCatalog.E1002"/>, matching the named-type
    /// precedent exactly. Sets <see cref="MemberAccessExpr.ResolvedPrimitiveNativeName"/>
    /// so the compiler rewrites the access to the qualified native, receiver as its sole
    /// argument.
    /// </summary>
    private GrobType ResolvePrimitiveMemberPropertyAccess(MemberAccessExpr node, PrimitiveMemberEntry entry) {
        if (entry.Properties.TryGetValue(node.Member, out PrimitiveMemberProperty? property) && property is not null) {
            node.ResolvedFieldType = property.Type;
            node.ResolvedPrimitiveNativeName = property.QualifiedNativeName;
            return property.Type;
        }

        return EmitErrorAndReturn(ErrorCatalog.E1002,
            $"Type '{TypeName(entry.ReceiverType)}' has no member '{node.Member}'.",
            node.Range);
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
        // A direct struct-returning call not yet stored in a binding (date.now() <
        // date.today(), or d.addDays(1) < d2) — _callResultStructNames is already
        // populated by the time this runs: Visit(target) (VisitCall) always precedes
        // the caller's own GetStructTypeName consultation, in every call site (D-355 —
        // CodeRabbit review PR #143 reported this for date; generalised here since the
        // same gap silently affected every struct-returning call, guid included).
        CallExpr call => _callResultStructNames.GetValueOrDefault(call),
        GroupingExpr g => GetStructTypeName(g.Inner),
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
    /// <remarks>
    /// Resolves to the receiver's element type (D-351) via <see cref="ArrayDescriptorOf"/>,
    /// which already handles a chained target (<c>matrix[r][c]</c>, D-112) by recursing
    /// into <see cref="ArrayTypeDescriptor.ElementArrayDescriptor"/> for a nested
    /// <c>IndexExpr</c>. A map receiver (or any target whose element type could not be
    /// determined) stays <see cref="GrobType.Unknown"/> — maps carry the same
    /// unparameterised-value gap arrays had before this decision and are out of its scope.
    /// §3.1.1 does not extend to <c>IndexExpr</c> (D-348) — no <c>ResolvedType</c>/
    /// <c>Declaration</c> is set here. The result is also stashed on
    /// <see cref="IndexExpr.ElementType"/> (Sprint 9 Increment A4, D-359) so the
    /// compiler's <c>GetExprType</c> can select the right typed opcode for an index
    /// operand — mirroring <see cref="MemberAccessExpr.ResolvedFieldType"/>.
    /// </remarks>
    public override GrobType VisitIndex(IndexExpr node) {
        Visit(node.Target);
        Visit(node.Index);
        GrobType elementType = ArrayDescriptorOf(node.Target)?.ElementKind ?? GrobType.Unknown;
        node.ElementType = elementType;
        return elementType;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves to <see cref="GrobType.Array"/> so a <c>for...in</c> subject can be
    /// recognised as iterable, and infers the element-type descriptor (D-351) from the
    /// elements — stored in <see cref="_arrayLiteralDescriptors"/>, not returned directly,
    /// mirroring how <see cref="_lambdaDescriptors"/> carries a lambda's structural shape
    /// alongside its flat <see cref="GrobType"/>. Elements are unified pairwise via
    /// <see cref="UnifyArrayElementType"/> (int/float widening, E0001 on a genuine
    /// mismatch — <c>[1, "a"]</c>). An empty literal infers nothing; its element type
    /// comes from context (an annotation), consistent with §9's empty-literal rule.
    /// </remarks>
    public override GrobType VisitArrayLiteral(ArrayLiteralExpr node) {
        if (node.Elements.Count == 0) return GrobType.Array;

        GrobType elementKind = GrobType.Unknown;
        string? elementNamedTypeName = null;
        ArrayTypeDescriptor? elementArrayDescriptor = null;
        bool haveFirst = false;

        foreach (Expression element in node.Elements) {
            GrobType elemType = Visit(element);
            if (elemType == GrobType.Error) continue; // cascade suppression

            if (!haveFirst) {
                elementKind = elemType;
                elementNamedTypeName = GetStructTypeName(element);
                elementArrayDescriptor = ArrayDescriptorOf(element);
                haveFirst = true;
                continue;
            }

            GrobType unified = UnifyArrayElementType(elementKind, elemType, element.Range);
            // A matching flat kind is not sufficient identity for structs and nested arrays:
            // [A{}, B{}] and [[1], ["a"]] both share a flat kind (Struct, Array) yet differ
            // nominally / structurally. Reject those so a T[] literal cannot smuggle a U
            // element (D-351). Only checked when the flat unify itself did not already fail.
            if (unified != GrobType.Error) {
                CheckArrayElementIdentity(unified, elementNamedTypeName, elementArrayDescriptor, element);
            }
            elementKind = unified;
        }

        _arrayLiteralDescriptors[node] = new ArrayTypeDescriptor(elementKind, elementNamedTypeName, elementArrayDescriptor);
        return GrobType.Array;
    }

    /// <summary>
    /// Enforces that an array-literal element beyond the first shares not just the running
    /// flat element kind (already unified by <see cref="UnifyArrayElementType"/>) but the
    /// running <em>identity</em> — the same named struct for a <c>Struct</c> element, and a
    /// compatible nested descriptor for an <c>Array</c> element (<c>T[][]</c>). Emits E0001
    /// on a mismatch. Split out to keep <see cref="VisitArrayLiteral"/> under the analyser's
    /// cognitive-complexity bar.
    /// </summary>
    private void CheckArrayElementIdentity(
            GrobType elementKind, string? runningNamedTypeName, ArrayTypeDescriptor? runningArrayDescriptor, Expression element) {
        if (elementKind is GrobType.Struct or GrobType.NullableStruct && runningNamedTypeName is not null) {
            string? elementName = GetStructTypeName(element);
            if (elementName is not null && !string.Equals(elementName, runningNamedTypeName, StringComparison.Ordinal)) {
                EmitError(ErrorCatalog.E0001,
                    $"Array literal elements must share a type; found '{runningNamedTypeName}' and '{elementName}'.",
                    element.Range);
            }
            return;
        }

        if (elementKind is GrobType.Array or GrobType.NullableArray && runningArrayDescriptor is not null) {
            ArrayTypeDescriptor? elementDescriptor = ArrayDescriptorOf(element);
            if (elementDescriptor is not null && !ArrayElementAssignable(elementDescriptor, runningArrayDescriptor)) {
                EmitError(ErrorCatalog.E0001,
                    "Array literal elements must share a nested element type.",
                    element.Range);
            }
        }
    }

    /// <summary>
    /// Unifies an array literal's running element type with the next element's type
    /// (D-351), emitting E0001 on a genuine mismatch. Duplicates
    /// <see cref="UnifyTernaryArms"/>'s int/float-widening shape deliberately rather than
    /// reusing it directly — an array-literal mismatch needs array-specific wording
    /// ("array literal elements", not "ternary arms"), and <see cref="UnifyTernaryArms"/>'s
    /// T/T? nullable-widening arm has no clean array-literal analogue worth forcing through
    /// a shared parameterised helper for two call sites.
    /// </summary>
    private GrobType UnifyArrayElementType(GrobType running, GrobType next, SourceRange range) {
        if (running == GrobType.Error || next == GrobType.Error) return GrobType.Error;
        if (running == GrobType.Unknown || next == GrobType.Unknown) return GrobType.Unknown;
        if (running == next) return running;

        if ((running == GrobType.Int && next == GrobType.Float) ||
            (running == GrobType.Float && next == GrobType.Int)) {
            return GrobType.Float;
        }

        EmitError(ErrorCatalog.E0001,
            $"Array literal elements must share a type; found '{TypeName(running)}' and '{TypeName(next)}'.",
            range);
        return GrobType.Error;
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
