using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

// -----------------------------------------------------------------------
// int to float widening (R-58, D-460).
//
// The checker accepts an int wherever a float is expected, the one implicit
// conversion (D-178), so the compiler must make it real: a value left as an int
// reaches the first float operation and faults, or, compared with a float, is
// quietly unequal. An arithmetic or comparison operand, a ternary or switch arm
// and an argument bound by the checker widen where they are emitted; these
// helpers serve every other position, where the target type is a declaration's
// annotation or a type the checker already recorded on the node.
// -----------------------------------------------------------------------
public sealed partial class Compiler {
    /// <summary>
    /// Whether the function being compiled declares the return type <c>float</c>, so each
    /// <c>return</c> widens an <c>int</c> value. Set by the sub-compiler of a declared
    /// <c>fn</c>. A lambda declares no return type, and its sub-compiler leaves it
    /// <see langword="false"/>: the enclosing function's return type is not its own.
    /// </summary>
    private bool _returnsFloat;

    /// <summary>
    /// Whether <paramref name="type"/> is the annotation <c>float</c>, so a value written
    /// there is widened. A nullable <c>float?</c> is not: the checker refuses an <c>int</c>
    /// there (<c>T</c> is assignable to <c>T?</c>, but <c>int</c> is not <c>float</c>), and an
    /// array, map or function annotation has another name.
    /// </summary>
    private static bool IsFloatAnnotation(TypeRef? type) => type is { Name: "float", IsNullable: false };

    /// <summary>
    /// Emits <paramref name="value"/> and, when <paramref name="targetIsFloat"/> and its static
    /// type is <c>int</c>, <see cref="OpCode.IntToFloat"/> after it, on the value's own line as a
    /// binary operand's widening is. An <c>int</c> beside a <c>float</c> inside the value
    /// is widened where it is emitted, so the value's own type here is already <c>float</c> and
    /// is not widened twice.
    /// </summary>
    private void VisitWidened(Expression value, bool targetIsFloat) {
        Visit(value);
        if (targetIsFloat && GetExprType(value) == GrobType.Int) {
            _chunk.WriteOpCode(OpCode.IntToFloat, value.Range.Start.Line);
        }
    }

    /// <summary><see cref="VisitWidened(Expression, bool)"/> for a target the checker typed.</summary>
    private void VisitWidened(Expression value, GrobType target) =>
        VisitWidened(value, target == GrobType.Float);

    /// <summary>
    /// Folds a <c>const</c>'s value to the annotation's type: an <c>int</c> constant annotated
    /// <c>float</c> is the float. A <c>const</c> emits no code and every reference inlines
    /// the folded value, so the conversion is made once here, not with an opcode at each use.
    /// </summary>
    private static GrobValue WidenConstant(GrobValue value, TypeRef? annotation) =>
        value.IsInt && IsFloatAnnotation(annotation) ? GrobValue.FromFloat(value.AsInt()) : value;

    /// <summary>
    /// Whether a switch's value pattern is compared as a <c>float</c>: the scrutinee is a
    /// <c>float</c> or a <c>float?</c>, where the checker accepts an <c>int</c> pattern
    /// (<c>IsPatternAssignable</c> reads a nullable scrutinee as its element type). An <c>int</c>
    /// pattern left unwidened is a different kind from the <c>float</c> it is compared with,
    /// so it never matches.
    /// </summary>
    private static bool IsFloatScrutinee(GrobType subjectType) =>
        (GrobTypeHelpers.IsNullable(subjectType) ? GrobTypeHelpers.ElementType(subjectType) : subjectType) == GrobType.Float;

    /// <summary>
    /// Emits an array literal's elements in source order, each <c>int</c> widened when another
    /// element is a <c>float</c>: the literal is then a <c>float</c> array, as the checker's
    /// element unification makes it.
    /// </summary>
    private void VisitArrayElements(IReadOnlyList<Expression> elements) {
        bool hasFloat = false;
        for (int i = 0; i < elements.Count && !hasFloat; i++) {
            hasFloat = GetExprType(elements[i]) == GrobType.Float;
        }
        foreach (Expression element in elements) VisitWidened(element, hasFloat);
    }

    /// <summary>
    /// Whether a map literal's declared value type, the second type argument of
    /// <c>map&lt;K, V&gt;</c>, is <c>float</c>.
    /// </summary>
    private static bool IsFloatMapValue(TypeRef mapType) =>
        mapType.TypeArguments.Count >= 2 && IsFloatAnnotation(mapType.TypeArguments[1]);
}
