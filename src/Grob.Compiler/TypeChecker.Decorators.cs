using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Static decorator validation (D-424 Decision 3) — E4001, E4002, E4101 and
    // E4102's first throw sites.
    //
    // Everything here is checked with NO parameter value in hand: the decorator
    // name, its arity, its arguments' literal kind, duplicate application and
    // applicability to the param's declared type. Enforcing a constraint against
    // a supplied value needs a bound parameter and is Sprint 10 (R-15).
    //
    // §19's table assigns E4101 to `@allowed` and E4102 to the four length and
    // value constraints. It assigns no code to `@secure`'s or `@pattern`'s arity
    // and argument-kind checks, and D-424 mints none, so those are raised through
    // E4002 — "decorator not permitted here" — with their own wording. Recorded
    // as a spec gap in the decisions-log entry rather than resolved silently.
    // -----------------------------------------------------------------------

    /// <summary>D-411's v1 decorator set. It does not grow by usage; there is no
    /// user-defined decorator mechanism in v1, so a name outside this set is E4001.</summary>
    private static readonly string[] _v1Decorators =
        ["secure", "allowed", "minLength", "maxLength", "minValue", "maxValue", "pattern"];

    private static readonly HashSet<string> _v1DecoratorSet =
        new(_v1Decorators, StringComparer.Ordinal);

    /// <summary>
    /// Validates a <c>param</c> declaration's decorator stack. Arguments are
    /// visited first and unconditionally, including under an unknown decorator:
    /// §3.1.1 admits no exemption, so every identifier appearing in one carries a
    /// non-null <c>ResolvedType</c> and <c>Declaration</c> afterwards, with
    /// D-311's sentinels on the error path.
    /// </summary>
    private void CheckDecorators(ParamDecl node) {
        if (node.Decorators.Count == 0) return;

        GrobType paramType = ResolveTypeRef(node.Type);
        Dictionary<string, int> applied = new(StringComparer.Ordinal);

        foreach (Decorator decorator in node.Decorators) {
            foreach (Expression argument in decorator.Arguments) {
                Visit(argument);
            }

            if (!_v1DecoratorSet.Contains(decorator.Name)) {
                EmitError(ErrorCatalog.E4001,
                    $"'@{decorator.Name}' is not a recognised decorator. The v1 set is "
                  + $"{string.Join(", ", _v1Decorators.Select(d => $"'@{d}'"))}.",
                    decorator.Range);
                continue;
            }

            if (applied.TryGetValue(decorator.Name, out int firstLine)) {
                EmitError(ErrorCatalog.E4002,
                    $"'@{decorator.Name}' is already applied to '{node.Name}' on line {firstLine}; "
                  + "a decorator may be applied only once.",
                    decorator.Range);
                continue;
            }

            applied[decorator.Name] = decorator.Range.Start.Line;
            ValidateDecorator(decorator, node, paramType);
        }
    }

    private void ValidateDecorator(Decorator decorator, ParamDecl node, GrobType paramType) {
        switch (decorator.Name) {
            case "secure":
                CheckSecure(decorator, node, paramType);
                break;
            case "allowed":
                CheckAllowed(decorator, node, paramType);
                break;
            case "minLength":
            case "maxLength":
                CheckLengthConstraint(decorator, node, paramType);
                break;
            case "minValue":
            case "maxValue":
                CheckValueConstraint(decorator, node, paramType);
                break;
            default:
                CheckPattern(decorator, node, paramType);
                break;
        }
    }

    // ---- @secure -----------------------------------------------------------

    private void CheckSecure(Decorator decorator, ParamDecl node, GrobType paramType) {
        if (decorator.Arguments.Count != 0) {
            EmitError(ErrorCatalog.E4002,
                "'@secure' takes no arguments; it marks the parameter's value as sensitive.",
                decorator.Range);
            return;
        }

        if (!IsStringLike(paramType)) {
            EmitError(ErrorCatalog.E4002, TargetTypeMessage("@secure", node, "a 'string'"), decorator.Range);
        }
    }

    // ---- @allowed ----------------------------------------------------------

    private void CheckAllowed(Decorator decorator, ParamDecl node, GrobType paramType) {
        if (decorator.Arguments.Count == 0) {
            EmitError(ErrorCatalog.E4101,
                "'@allowed' needs at least one permitted value.", decorator.Range);
            return;
        }

        GrobType? expected = null;
        foreach (Expression argument in decorator.Arguments) {
            GrobType? literal = LiteralTypeOf(argument);
            if (literal is null) {
                EmitError(ErrorCatalog.E4101,
                    "'@allowed' takes literal values only; this one is an expression, which has "
                  + "no value at compile time.",
                    argument.Range);
                continue;
            }

            if (expected is null) {
                expected = literal;
            } else if (literal != expected) {
                EmitError(ErrorCatalog.E4101,
                    $"'@allowed' values must all be the same type: this one is '{TypeName(literal.Value)}' "
                  + $"and the first is '{TypeName(expected.Value)}'.",
                    argument.Range);
                continue;
            }

            if (paramType != GrobType.Unknown && !TypesAreAssignable(literal.Value, paramType)) {
                EmitError(ErrorCatalog.E4101,
                    $"'@allowed' value of type '{TypeName(literal.Value)}' is not assignable to "
                  + $"'{node.Name}', which is declared '{TypeName(paramType)}'.",
                    argument.Range);
            }
        }
    }

    // ---- @minLength / @maxLength -------------------------------------------

    private void CheckLengthConstraint(Decorator decorator, ParamDecl node, GrobType paramType) {
        if (decorator.Arguments.Count != 1) {
            EmitError(ErrorCatalog.E4102,
                $"'@{decorator.Name}' takes exactly one non-negative integer literal.",
                decorator.Range);
            return;
        }

        Expression argument = decorator.Arguments[0];
        if (IsNegatedNumericLiteral(argument)) {
            EmitError(ErrorCatalog.E4102,
                $"'@{decorator.Name}' needs a non-negative integer literal; a length is never negative.",
                argument.Range);
        } else if (LiteralTypeOf(argument) != GrobType.Int) {
            EmitError(ErrorCatalog.E4102,
                $"'@{decorator.Name}' needs a non-negative integer literal.", argument.Range);
        }

        if (!IsStringLike(paramType) && !IsArrayLike(paramType)) {
            EmitError(ErrorCatalog.E4102,
                TargetTypeMessage($"@{decorator.Name}", node, "a 'string' or an array"),
                decorator.Range);
        }
    }

    // ---- @minValue / @maxValue ---------------------------------------------

    private void CheckValueConstraint(Decorator decorator, ParamDecl node, GrobType paramType) {
        if (decorator.Arguments.Count != 1) {
            EmitError(ErrorCatalog.E4102,
                $"'@{decorator.Name}' takes exactly one numeric literal.", decorator.Range);
            return;
        }

        Expression argument = decorator.Arguments[0];
        if (LiteralTypeOf(argument) is not (GrobType.Int or GrobType.Float)) {
            EmitError(ErrorCatalog.E4102,
                $"'@{decorator.Name}' needs a numeric literal.", argument.Range);
        }

        if (!IsNumeric(paramType)) {
            EmitError(ErrorCatalog.E4102,
                TargetTypeMessage($"@{decorator.Name}", node, "an 'int' or a 'float'"),
                decorator.Range);
        }
    }

    // ---- @pattern ----------------------------------------------------------

    /// <summary>
    /// D-424 Decision 4: <c>@pattern</c> is in the v1 set, so it is not E4001.
    /// Its name, arity, string-literal argument kind and <c>string</c> target
    /// type are checked here; <b>the pattern itself is not compiled or
    /// validated</b> until the <c>regex</c> increment supplies the machinery
    /// (D-411, tracked as R-15's sibling deferral). Treating it as unknown in the
    /// meantime would make a specified v1 decorator an error for one sprint and
    /// then silently stop being one.
    /// </summary>
    private void CheckPattern(Decorator decorator, ParamDecl node, GrobType paramType) {
        if (decorator.Arguments.Count != 1) {
            EmitError(ErrorCatalog.E4002,
                "'@pattern' takes exactly one string literal.", decorator.Range);
            return;
        }

        if (LiteralTypeOf(decorator.Arguments[0]) != GrobType.String) {
            EmitError(ErrorCatalog.E4002,
                "'@pattern' needs a string literal.", decorator.Arguments[0].Range);
        }

        if (!IsStringLike(paramType)) {
            EmitError(ErrorCatalog.E4002, TargetTypeMessage("@pattern", node, "a 'string'"), decorator.Range);
        }
    }

    // ---- Shared helpers ----------------------------------------------------

    private static string TargetTypeMessage(string decoratorName, ParamDecl node, string expected) =>
        $"'{decoratorName}' applies to {expected} parameter, but '{node.Name}' is declared "
      + $"'{TypeName(ResolveTypeRef(node.Type))}'.";

    /// <summary>
    /// The <see cref="GrobType"/> of <paramref name="expression"/> when it is a
    /// literal, or <see langword="null"/> when it is not one. A negated numeric
    /// literal counts — <c>@minValue(-10)</c> is an ordinary constraint — which
    /// is why <see cref="IsNegatedNumericLiteral"/> exists separately, for the
    /// length constraints where a negative value is meaningless.
    /// </summary>
    private static GrobType? LiteralTypeOf(Expression expression) => expression switch {
        IntLiteralExpr => GrobType.Int,
        FloatLiteralExpr => GrobType.Float,
        StringLiteralExpr => GrobType.String,
        RawStringLiteralExpr => GrobType.String,
        // A double-quoted string is an InterpolatedStringExpr; only one with no
        // interpolation segments is a literal.
        InterpolatedStringExpr str when str.Parts.All(p => p is StringTextPart) => GrobType.String,
        BoolLiteralExpr => GrobType.Bool,
        NilLiteralExpr => GrobType.Nil,
        UnaryExpr { Operator: UnaryOperator.Negate } unary =>
            LiteralTypeOf(unary.Operand) is GrobType.Int or GrobType.Float
                ? LiteralTypeOf(unary.Operand)
                : null,
        _ => null,
    };

    private static bool IsNegatedNumericLiteral(Expression expression) =>
        expression is UnaryExpr { Operator: UnaryOperator.Negate } unary
        && LiteralTypeOf(unary.Operand) is GrobType.Int or GrobType.Float;

    private static bool IsStringLike(GrobType type) =>
        type is GrobType.String or GrobType.NullableString;

    private static bool IsArrayLike(GrobType type) =>
        type is GrobType.Array or GrobType.NullableArray;

    private static bool IsNumeric(GrobType type) =>
        type is GrobType.Int or GrobType.Float or GrobType.NullableInt or GrobType.NullableFloat;
}
