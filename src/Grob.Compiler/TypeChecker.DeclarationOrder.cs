using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // §19 declaration order (D-424 Decision 5) — E2201 and E2202's first throw
    // sites. The rules have been normative since April 2026 with nothing
    // enforcing them.
    //
    // The check is a linear walk over CompilationUnit.TopLevel, which is already
    // a flat ordered list, classifying each item into §19's categories and
    // reporting a backward step. It is folded into pass 2's walk rather than run
    // as a pre-pass, and that placement is load-bearing: DiagnosticBag is
    // insertion-ordered with no sort anywhere, so a pre-pass would emit every
    // ordering diagnostic ahead of every type error, including type errors that
    // sit earlier in the file. Decision 5 requires source order. Its other
    // constraint — that an ordering error never suppresses pass-1 registration —
    // is satisfied by construction here: pass 1 has already run.
    // -----------------------------------------------------------------------

    /// <summary>
    /// §19's declaration categories. The numbering is §19's own, with <c>type</c>
    /// and <c>fn</c> merged into one rank because §19 leaves them unordered
    /// relative to each other, and with a zero rank for parser error placeholders.
    /// </summary>
    private enum DeclarationCategory {
        /// <summary>
        /// A §29 error placeholder. Category-neutral: it neither trips the check
        /// nor advances the high-water mark, so one recovered parse error never
        /// cascades an ordering error onto the declaration below it.
        /// </summary>
        Neutral = 0,

        /// <summary>§19 category 1 — <c>import</c>.</summary>
        Import = 1,

        /// <summary>§19 category 2 — <c>param</c>.</summary>
        Param = 2,

        /// <summary>
        /// §19 categories 3 and 4 — <c>type</c> and <c>fn</c>, merged because
        /// §19 permits them in any order relative to each other.
        /// </summary>
        TypeOrFunction = 3,

        /// <summary>
        /// §19 category 5 — top-level code, which includes <c>const</c> and
        /// <c>readonly</c> (D-412) as well as ordinary statements.
        /// </summary>
        TopLevelCode = 5,
    }

    /// <summary>The highest §19 category seen so far in this compilation unit.</summary>
    private DeclarationCategory _orderHighWater = DeclarationCategory.Import;

    /// <summary>
    /// The item that set <see cref="_orderHighWater"/>, so a diagnostic can name
    /// what the offending declaration came after and on which line.
    /// </summary>
    private AstNode? _orderHighWaterItem;

    /// <summary>
    /// Classifies one top-level item and reports a backward step against §19.
    /// Returns <see langword="true"/> when the item is a <c>param</c> declaration
    /// that is out of order — D-412's cascade rule suppresses that declaration's
    /// name collision in favour of the ordering error, one root cause giving one
    /// diagnostic.
    /// </summary>
    private bool CheckDeclarationOrder(AstNode item) {
        DeclarationCategory category = CategoryOf(item);
        if (category == DeclarationCategory.Neutral) return false;

        bool misordered = category < _orderHighWater;
        if (misordered) {
            ReportOrderingError(item, category);
        } else {
            _orderHighWater = category;
            _orderHighWaterItem = item;
        }

        return misordered && category == DeclarationCategory.Param;
    }

    /// <summary>
    /// Emits the ordering diagnostic for a backward step. Only categories 1 and 2
    /// have a code: E2201 for an <c>import</c> that follows anything, E2202 for a
    /// <c>param</c> that follows a <c>type</c>, <c>fn</c>, <c>const</c>,
    /// <c>readonly</c> or top-level statement. A <c>type</c> or <c>fn</c>
    /// following top-level code also steps backwards against §19's prose, but no
    /// code has ever been allocated for it and D-424 does not mint one; the walk
    /// stays silent there rather than borrowing a code that names something else.
    /// </summary>
    private void ReportOrderingError(AstNode item, DeclarationCategory category) {
        string after = DescribePrecedingItem();
        switch (category) {
            case DeclarationCategory.Import:
                EmitError(ErrorCatalog.E2201,
                    "'import' must come before every declaration and every top-level statement, "
                  + $"but this one follows {after}.",
                    item.Range);
                break;
            case DeclarationCategory.Param:
                EmitError(ErrorCatalog.E2202,
                    "'param' declarations must come before every 'type', 'fn', 'const', 'readonly' "
                  + $"and top-level statement, but this one follows {after}.",
                    item.Range);
                break;
            default:
                break;
        }
    }

    private string DescribePrecedingItem() =>
        _orderHighWaterItem is null
            ? "an earlier item"
            : $"the {Describe(_orderHighWaterItem)} on line {_orderHighWaterItem.Range.Start.Line}";

    private static string Describe(AstNode item) => item switch {
        ImportDecl => "'import'",
        ParamDecl => "'param' declaration",
        TypeDecl => "'type' declaration",
        FnDecl => "'fn' declaration",
        ConstDecl => "'const' declaration",
        ReadonlyDecl => "'readonly' declaration",
        _ => "top-level statement",
    };

    private static DeclarationCategory CategoryOf(AstNode item) => item switch {
        ImportDecl => DeclarationCategory.Import,
        ParamDecl => DeclarationCategory.Param,
        TypeDecl or FnDecl => DeclarationCategory.TypeOrFunction,
        ErrorDecl or ErrorStmt => DeclarationCategory.Neutral,
        _ => DeclarationCategory.TopLevelCode,
    };
}
