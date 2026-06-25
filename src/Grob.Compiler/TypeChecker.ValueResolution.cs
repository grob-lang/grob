using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Phase 1.5 — top-level value-binding type resolution (D-323).
    //
    // After pass-1 registration every top-level value binding sits in the symbol
    // table with GrobType.Unknown as a provisional placeholder. Phase 1.5 walks
    // the bindings in initialiser-dependency order using a DFS (mirroring §17.1's
    // type-field cycle detection) and updates each placeholder to the binding's
    // actual inferred type before pass 2 validates function bodies.
    //
    // Key invariant: UpdateProvisionalType keeps Provisional=true, so pass 2's
    // E1102 guard (which skips re-registration for provisional entries) still works.
    //
    // Dependency edges: only unannotated bindings contribute value-type dep edges.
    // An annotated binding (readonly a: int := expr) is resolved from its annotation
    // regardless of expr's type — no dep edge on the initialisers of identifiers
    // referenced by expr. This means annotated mutual cycles are structurally fine
    // for the type checker; they surface at runtime as E5902.
    // -----------------------------------------------------------------------

    private enum BindingResolutionColor { Unvisited, Visiting, Visited }

    private void ResolveTopLevelValueBindingTypes(CompilationUnit unit) {
        Dictionary<string, AstNode> bindings = [];
        foreach (AstNode item in unit.TopLevel) {
            string? name = item switch {
                ReadonlyDecl ro => ro.Name,
                VarDeclStmt vd => vd.Name,
                _ => null,
            };
            // Keep the first declaration for a duplicated name authoritative — it is
            // the one pass 1 registered and pass 2 finalises (PR #92 review). A later
            // duplicate is reported as E1102 and must not displace the first's type.
            if (name is not null) bindings.TryAdd(name, item);
        }

        if (bindings.Count == 0) return;

        HashSet<string> nameSet = new(bindings.Keys, StringComparer.Ordinal);
        Dictionary<string, BindingResolutionColor> colors = new(StringComparer.Ordinal);
        foreach (string name in bindings.Keys) {
            colors[name] = BindingResolutionColor.Unvisited;
        }

        foreach (string name in bindings.Keys) {
            if (colors[name] == BindingResolutionColor.Unvisited) {
                ResolveValueBindingType(name, bindings, nameSet, colors);
            }
        }
    }

    private void ResolveValueBindingType(
        string name,
        Dictionary<string, AstNode> bindings,
        HashSet<string> nameSet,
        Dictionary<string, BindingResolutionColor> colors) {

        colors[name] = BindingResolutionColor.Visiting;

        HashSet<string> deps = ExtractTypeDependencies(bindings[name], nameSet);

        foreach (string dep in deps) {
            if (colors[dep] == BindingResolutionColor.Unvisited) {
                ResolveValueBindingType(dep, bindings, nameSet, colors);
            } else if (colors[dep] == BindingResolutionColor.Visiting) {
                // Back-edge: unannotated mutual cycle — type unresolvable at compile time.
                SourceRange range = GetBindingRange(bindings[name]);
                _diagnostics.Add(Diagnostic.Of(
                    ErrorCatalog.E0303,
                    range,
                    $"Top-level value binding '{name}' has a circular type dependency through '{dep}'. "
                  + "Add a type annotation to break the cycle, or restructure the initialisers."));
                UpdateProvisionalType(name, GrobType.Error);
                UpdateProvisionalType(dep, GrobType.Error);
                colors[name] = BindingResolutionColor.Visited;
                return;
            }
            // Visited: already resolved — nothing to do.
        }

        GrobType resolvedType = SilentInferTypeFromBinding(bindings[name]);
        UpdateProvisionalType(name, resolvedType);
        colors[name] = BindingResolutionColor.Visited;
    }

    /// <summary>
    /// Returns the set of top-level value-binding names that <paramref name="binding"/>
    /// directly depends on for type resolution. Returns an empty set for annotated
    /// bindings — their type is taken from the annotation regardless of the initialiser.
    /// </summary>
    private static HashSet<string> ExtractTypeDependencies(AstNode binding, HashSet<string> nameSet) {
        TypeRef? annotation = binding switch {
            ReadonlyDecl ro => ro.AnnotatedType,
            VarDeclStmt vd => vd.AnnotatedType,
            _ => null,
        };
        if (annotation is not null) return [];

        Expression? initializer = binding switch {
            ReadonlyDecl ro => ro.Value,
            VarDeclStmt vd => vd.Initializer,
            _ => null,
        };
        if (initializer is null) return [];

        HashSet<string> deps = new(StringComparer.Ordinal);
        CollectDependencies(initializer, nameSet, deps);
        return deps;
    }

    private static void CollectDependencies(Expression expr, HashSet<string> nameSet, HashSet<string> deps) {
        switch (expr) {
            case IdentifierExpr id when nameSet.Contains(id.Name):
                deps.Add(id.Name);
                break;
            case BinaryExpr bin:
                CollectDependencies(bin.Left, nameSet, deps);
                CollectDependencies(bin.Right, nameSet, deps);
                break;
            case UnaryExpr un:
                CollectDependencies(un.Operand, nameSet, deps);
                break;
            case GroupingExpr grp:
                CollectDependencies(grp.Inner, nameSet, deps);
                break;
            case TernaryExpr tern:
                CollectDependencies(tern.Condition, nameSet, deps);
                CollectDependencies(tern.Then, nameSet, deps);
                CollectDependencies(tern.Else, nameSet, deps);
                break;
            case CallExpr:
                // A call result's type comes from the callee's declared return type
                // (D-323), not from its arguments. Recursing into arguments would
                // forge a false value-type dependency edge and report a bogus E0303
                // for cycles that only close through a call argument; those surface at
                // runtime as E5902 instead (PR #92 review).
                break;
        }
    }

    private GrobType SilentInferTypeFromBinding(AstNode binding) {
        TypeRef? annotation = binding switch {
            ReadonlyDecl ro => ro.AnnotatedType,
            VarDeclStmt vd => vd.AnnotatedType,
            _ => null,
        };
        Expression? initializer = binding switch {
            ReadonlyDecl ro => ro.Value,
            VarDeclStmt vd => vd.Initializer,
            _ => null,
        };

        GrobType initType = initializer is not null ? SilentInferType(initializer) : GrobType.Unknown;
        return SilentResolveBinding(annotation, initType);
    }

    private GrobType SilentInferType(Expression expr) => expr switch {
        IntLiteralExpr => GrobType.Int,
        FloatLiteralExpr => GrobType.Float,
        StringLiteralExpr => GrobType.String,
        RawStringLiteralExpr => GrobType.String,
        InterpolatedStringExpr => GrobType.String,
        BoolLiteralExpr => GrobType.Bool,
        NilLiteralExpr => GrobType.Nil,
        IdentifierExpr id => LookupSymbol(id.Name)?.Type ?? GrobType.Unknown,
        CallExpr call => SilentInferCallType(call),
        BinaryExpr bin => SilentInferBinaryType(bin),
        UnaryExpr un => SilentInferUnaryType(un),
        GroupingExpr grp => SilentInferType(grp.Inner),
        TernaryExpr tern => SilentUnifyTypes(SilentInferType(tern.Then), SilentInferType(tern.Else)),
        _ => GrobType.Unknown,
    };

    private GrobType SilentInferCallType(CallExpr call) {
        if (call.Callee is not IdentifierExpr callee) return GrobType.Unknown;
        Symbol? sym = LookupSymbol(callee.Name);
        if (sym?.DeclarationNode is FnDecl fn) return ResolveTypeRef(fn.ReturnType);
        return GrobType.Unknown;
    }

    private GrobType SilentInferBinaryType(BinaryExpr bin) {
        if (IsComparisonOperator(bin.Operator)) return GrobType.Bool;
        if (bin.Operator is BinaryOperator.And or BinaryOperator.Or) return GrobType.Bool;

        GrobType left = SilentInferType(bin.Left);
        GrobType right = SilentInferType(bin.Right);

        if (left == GrobType.Unknown || right == GrobType.Unknown) return GrobType.Unknown;
        if (left == GrobType.Error || right == GrobType.Error) return GrobType.Error;
        if (left == GrobType.String && right == GrobType.String) return GrobType.String;
        if (BothNumeric(left, right)) {
            return (left == GrobType.Float || right == GrobType.Float) ? GrobType.Float : GrobType.Int;
        }
        return GrobType.Unknown;
    }

    private GrobType SilentInferUnaryType(UnaryExpr un) {
        GrobType operand = SilentInferType(un.Operand);
        return un.Operator switch {
            UnaryOperator.Negate when operand == GrobType.Int => GrobType.Int,
            UnaryOperator.Negate when operand == GrobType.Float => GrobType.Float,
            UnaryOperator.Not => GrobType.Bool,
            _ => GrobType.Unknown,
        };
    }

    private static GrobType SilentUnifyTypes(GrobType a, GrobType b) {
        if (a == b) return a;
        if (a == GrobType.Error || b == GrobType.Error) return GrobType.Error;
        if ((a == GrobType.Int && b == GrobType.Float) || (a == GrobType.Float && b == GrobType.Int))
            return GrobType.Float;
        return GrobType.Unknown;
    }

    /// <summary>
    /// Silent analogue of <see cref="ResolveBinding"/>: resolves the type of a binding
    /// from its optional annotation and its silently-inferred initialiser type, without
    /// emitting any diagnostics. When annotation and initialiser are incompatible, returns
    /// <see cref="GrobType.Error"/> — pass 2 will emit the full E0001 with source location.
    /// </summary>
    private static GrobType SilentResolveBinding(TypeRef? annotation, GrobType initType) {
        if (annotation is null) return initType;

        GrobType annotated = ResolveTypeRef(annotation);
        if (annotated == GrobType.Unknown) return initType;
        if (initType == GrobType.Error) return GrobType.Error;
        // initType == Unknown means the init expr couldn't be typed silently
        // (e.g. references a not-yet-resolved dep); the annotation wins.
        if (initType == GrobType.Unknown) return annotated;
        if (TypesAreAssignable(initType, annotated)) return annotated;
        // Incompatible: pass 2 will emit E0001 with full diagnostics.
        return GrobType.Error;
    }

    private static SourceRange GetBindingRange(AstNode binding) => binding switch {
        ReadonlyDecl ro => ro.Range,
        VarDeclStmt vd => vd.Range,
        _ => new SourceRange(SourceLocation.Unknown, SourceLocation.Unknown),
    };
}
