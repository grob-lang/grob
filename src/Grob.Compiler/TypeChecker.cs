using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Core.NamedTypes;

namespace Grob.Compiler;

/// <summary>
/// Three-pass type checker (D-166, D-323). Annotates the AST in place — sets
/// <see cref="IdentifierExpr.ResolvedType"/> and <see cref="IdentifierExpr.Declaration"/>
/// on every identifier node — and accumulates all type diagnostics into the
/// <see cref="DiagnosticBag"/> without stopping at the first error.
/// Emits no bytecode; that is Increment D.
/// </summary>
/// <remarks>
/// <para><b>Pass 1 — registration.</b> Walks every top-level <c>fn</c>, <c>type</c>
/// and value binding declaration and registers the name in the global scope with a
/// provisional <see cref="GrobType.Unknown"/> placeholder (D-166, D-321).</para>
/// <para><b>Pass 1.5 — value-binding type resolution.</b> Resolves the static type of
/// every top-level value binding (<c>readonly</c> / <c>var</c>) from its initialiser,
/// in initialiser-dependency order using a DFS. This means pass 2 sees the real type
/// when a function body reads a forward-declared value binding, rather than
/// <see cref="GrobType.Unknown"/> (D-323). Unannotated mutual cycles are detected
/// here and reported as E0303; annotated cycles surface at runtime as E5902.</para>
/// <para><b>Pass 2 — validation.</b> Walks all top-level items in source order,
/// resolving names, inferring types, checking arithmetic and comparison rules, and
/// reporting every violation via the diagnostic bag.</para>
/// <para><b>Cascade suppression.</b> When a sub-expression already produced an error,
/// its parent resolves to the compiler-internal <see cref="GrobType.Error"/> sentinel,
/// which is universally assignable. This ensures a single mistake does not cascade
/// into a storm of derived diagnostics.</para>
/// </remarks>
public sealed partial class TypeChecker : AstVisitor<GrobType> {
    private readonly DiagnosticBag _diagnostics;
    private readonly Stack<Dictionary<string, Symbol>> _scopes = new();

    // -----------------------------------------------------------------------
    // Control-frame stack (Sprint 4 Increments B and D; D-315).
    //
    // A frame is pushed on entering a loop (while / for...in) or a select, and
    // popped on exit. break / continue resolve against the stack, distinguishing
    // LOOP frames from SELECT frames:
    //   - break:    nearest frame is a SELECT  -> E2211 (break has no meaning in a
    //               select — D-301 removed fall-through, and it is not retargeted at
    //               an enclosing loop). A LOOP frame on top -> valid. Empty -> E2212.
    //   - continue: skip SELECT frames, target the nearest LOOP frame. None -> E2212.
    // select is therefore NOT loop-control-transparent (D-315).
    //
    // Sprint 7 Increment C adds a FINALLY frame, pushed around a try's finally
    // block when present. break/continue resolve it the same way as Select —
    // Peek()==Finally (break) or hitting Finally before any Loop while skipping
    // Select (continue) is E2207, "control flow inside finally" (D-275).
    // -----------------------------------------------------------------------
    private enum ControlFrame { Loop, Select, Finally }

    private readonly Stack<ControlFrame> _controlFrames = new();

    // -----------------------------------------------------------------------
    // Control-frame floor stack (Sprint 7 Increment C).
    //
    // Pushed in lockstep with _functionReturnTypes (VisitFnDecl, VisitLambda)
    // with the current _controlFrames.Count, so VisitReturn's E2207 check can
    // ask "is there a Finally frame pushed since MY function/lambda body
    // began" rather than seeing an enclosing function's frames. return exits
    // the enclosing function, not a loop, so — unlike break/continue, which
    // resolve via nearest-frame order and so are naturally shielded by any
    // construct nested inside the finally — it needs this floor to implement
    // the D-276 carve-out (return inside a nested block-body lambda inside a
    // finally exits only the lambda).
    // -----------------------------------------------------------------------
    private readonly Stack<int> _controlFrameFloors = new();

    // -----------------------------------------------------------------------
    // Function return-type stack (Sprint 5 Increment A).
    //
    // Pushed on entering a fn body and popped on exit, so VisitReturn can check a
    // returned value against the enclosing function's declared return type (E0005)
    // and reject a return that has no enclosing function (E2203). A non-empty stack
    // means "inside a function body".
    // -----------------------------------------------------------------------
    private readonly Stack<GrobType> _functionReturnTypes = new();

    // -----------------------------------------------------------------------
    // Lambda return-type inference (Sprint 5 Increment C).
    //
    // VisitLambda stores the inferred body return type here so that
    // ValidateArrayMethodCall can check 'filter' predicates against bool (E0004)
    // without exposing the inferred type as the lambda expression's GrobType.
    // -----------------------------------------------------------------------

    // LambdaExpr is a record whose GetHashCode recurses into nested AST nodes and
    // would stack-overflow on deeply nested lambdas. Use reference identity so the
    // dictionary entry is keyed by object identity, not structural equality.
    private readonly Dictionary<LambdaExpr, GrobType> _lambdaReturnTypes =
        new(ReferenceEqualityComparer.Instance);

    // -----------------------------------------------------------------------
    // Function-type structural descriptors (Sprint 5 Increment D — D-326).
    //
    // _functionReturnDescriptors is parallel to _functionReturnTypes: pushed and
    // popped in lockstep with it inside VisitFnDecl. Non-null when the enclosing
    // fn declares a function-type return annotation; null for primitive/collection
    // return types. Lambdas do NOT push to this stack (they push GrobType.Unknown
    // to _functionReturnTypes, so VisitReturn's expected-Unknown guard fires first).
    //
    // _lambdaDescriptors maps each lambda expression to its inferred structural
    // descriptor (arity + inferred body type), keyed by reference identity for the
    // same reason _lambdaReturnTypes is.
    // -----------------------------------------------------------------------
    private readonly Stack<FunctionTypeDescriptor?> _functionReturnDescriptors = new();

    // -----------------------------------------------------------------------
    // Function return-type struct name (fix/compiler-struct-nominal-identity,
    // Site C).
    //
    // Parallel to _functionReturnDescriptors: pushed and popped in lockstep with
    // it inside VisitFnDecl only — never for a lambda, which pushes GrobType.Unknown
    // to _functionReturnTypes so VisitReturn's expected != Unknown guard short-circuits
    // before this stack (or _functionReturnDescriptors) is ever consulted. Non-null
    // when the enclosing fn's declared return type is a named struct; null otherwise.
    // Read by ComputeReturnCompatibility so a returned value's own struct name can be
    // compared against the declared return type's name (IsStructNominalMismatch),
    // mirroring how a struct-typed parameter, field or binding annotation is checked.
    // -----------------------------------------------------------------------
    private readonly Stack<string?> _functionReturnStructNames = new();

    // Parallel to _functionReturnStructNames (D-351): non-null when the enclosing fn's
    // declared return type is T[] and the element type is known, so a returned array
    // value's own element type can be checked against the declared return element type
    // (ComputeReturnCompatibility) exactly as the struct name stack does for structs.
    private readonly Stack<ArrayTypeDescriptor?> _functionReturnArrayDescriptors = new();

    private readonly Dictionary<LambdaExpr, FunctionTypeDescriptor> _lambdaDescriptors =
        new(ReferenceEqualityComparer.Instance);

    // _callResultDescriptors maps a call expression to the structural descriptor of its
    // result, when the callee's declared return type is a function type (D-326; Fix I).
    // VisitVarDecl / VisitReadonlyDecl read it the same way they read _lambdaDescriptors
    // for a lambda initialiser, so `c: fn(): int := makeCounter()` checks structurally.
    // Keyed by reference identity, matching _lambdaDescriptors.
    private readonly Dictionary<CallExpr, FunctionTypeDescriptor> _callResultDescriptors =
        new(ReferenceEqualityComparer.Instance);

    // _callResultStructNames mirrors _callResultDescriptors for struct-typed return
    // values (Sprint 6 close): when a call's callee has a user-defined-type return
    // annotation, this records the declared type name so a `:=`-inferred binding from
    // that call (`box := makeBox()`) can resolve field access the same way a direct
    // struct-construction initialiser already does. Keyed by reference identity,
    // matching _callResultDescriptors.
    private readonly Dictionary<CallExpr, string> _callResultStructNames =
        new(ReferenceEqualityComparer.Instance);

    // _callResultArrayDescriptors mirrors _callResultStructNames for T[]-returning calls
    // (D-351): when a call's callee has a T[] return annotation with a known element type,
    // this records the descriptor so a `:=`-inferred binding from that call
    // (`items := makeItems()`) carries the element type onward the same way a direct
    // array-literal initialiser does. Keyed by reference identity, matching
    // _callResultDescriptors.
    private readonly Dictionary<CallExpr, ArrayTypeDescriptor> _callResultArrayDescriptors =
        new(ReferenceEqualityComparer.Instance);

    // _arrayLiteralDescriptors maps each array-literal expression to its inferred element
    // descriptor (D-351), keyed by reference identity for the same reason
    // _lambdaDescriptors is. Populated by VisitArrayLiteral; consulted wherever an
    // array-typed binding or index expression needs to resolve its element type from a
    // literal initialiser.
    private readonly Dictionary<ArrayLiteralExpr, ArrayTypeDescriptor> _arrayLiteralDescriptors =
        new(ReferenceEqualityComparer.Instance);

    // -----------------------------------------------------------------------
    // Flow-sensitive narrowing (Sprint 5 Increment E; §6, §19.1 narrowing rule).
    //
    // Inside an `if (x != nil)` block, x is added here mapped to its non-nullable
    // element type. VisitIdentifier consults this map before falling back to the
    // symbol's declared type, so a reference to x resolves to T rather than T?
    // for the block's extent. The entry is removed on leaving the block, so x is
    // T? again outside it (and in the else-branch). Keyed by name; a nested
    // `if (x != nil)` does not re-add or remove an entry already present, so the
    // outer narrowing survives the inner block.
    // -----------------------------------------------------------------------
    private readonly Dictionary<string, GrobType> _narrowedTypes = new(StringComparer.Ordinal);

    // -----------------------------------------------------------------------
    // User-type registry (Sprint 6 Increment A).
    //
    // Populated during pass 2's visits to TypeDecl nodes (one entry per type).
    // Read by phase 2.5's cycle-detection DFS. Scoped to the compilation unit.
    // -----------------------------------------------------------------------
    private readonly UserTypeRegistry _userTypeRegistry = new();

    // Structural type registry (Sprint 6 Increment D).
    //
    // Holds synthesised UserTypeInfo entries keyed by the canonical structural
    // signature ("fieldA:GrobType1,fieldB:GrobType2" in sorted-name order).
    // Two #{ } literals with identical field names and value types share one entry.
    private readonly Dictionary<string, UserTypeInfo> _structuralTypes =
        new(StringComparer.Ordinal);

    // Returns type info from either the user-type or the structural registry.
    private UserTypeInfo? TryGetTypeInfo(string typeName) =>
        _userTypeRegistry.TryGet(typeName) ?? _structuralTypes.GetValueOrDefault(typeName);

    /// <summary>Initialises a new <see cref="TypeChecker"/> that writes into <paramref name="diagnostics"/>.</summary>
    public TypeChecker(DiagnosticBag diagnostics) {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Runs the three-pass type check over <paramref name="unit"/>, mutating
    /// identifier nodes in-place and accumulating all diagnostics into the bag
    /// supplied at construction.
    /// </summary>
    public void Check(CompilationUnit unit) {
        ArgumentNullException.ThrowIfNull(unit);

        // Global scope lives for the whole compilation unit. Pre-sized for the
        // built-in functions (RegisterBuiltins, 3 names), the Sprint 7 GrobError
        // hierarchy (RegisterExceptionHierarchy) and the Sprint 8 namespace
        // registry (RegisterNamespaces) registered into it below — all three run
        // unconditionally on every compile, so without a capacity hint the
        // dictionary pays for repeated resize-and-copy growth on every single
        // compile regardless of source size (D-338).
        _scopes.Push(new Dictionary<string, Symbol>(
            3 + ExceptionHierarchy.AllNames.Count + NamespaceRegistry.Count));

        // Seed the global scope with built-in functions (D-270). Must run
        // before Pass 1 so that user-defined names cannot shadow built-ins
        // at the top-level scope, and so that call sites do not get E1001.
        RegisterBuiltins();

        // Seed the global scope with the Sprint 7 GrobError hierarchy (D-284) as
        // built-in nominal types — not user 'type' declarations, so they carry no
        // pass-1/pass-2 registration of their own. Must also run before Pass 1,
        // for the same reason as RegisterBuiltins: so a colliding user 'type'
        // declaration (e.g. 'type IoError { ... }') is detected as a real E1102
        // redeclaration rather than silently shadowing the hierarchy leaf.
        RegisterExceptionHierarchy();

        // Seed the global scope with the Sprint 8 core-module namespaces (D-342) —
        // a third name category alongside value and type bindings. Must also run
        // before Pass 1, for the same shadowing reason as the two registrations
        // above.
        RegisterNamespaces();

        // Pass 1 — register top-level fn, type, and value-binding declarations with
        // provisional GrobType.Unknown placeholders (D-166, D-321, D-324). This lets any
        // top-level item reference any other top-level name without an E1001.
        // All declarations are registered as provisional so that pass 2 can detect
        // collisions uniformly at the finalising visit, always at the offending later decl.
        foreach (AstNode item in unit.TopLevel) {
            switch (item) {
                case FnDecl fn:
                    RegisterSymbol(fn.Name, GrobType.Unknown, fn.Range.Start, fn, provisional: true);
                    break;
                case TypeDecl td:
                    RegisterProvisionalTypeBinding(td.Name, td.Range.Start, td);
                    break;
                case ReadonlyDecl ro:
                    RegisterProvisionalValueBinding(ro.Name, ro.Range.Start, ro);
                    break;
                case VarDeclStmt vd:
                    RegisterProvisionalValueBinding(vd.Name, vd.Range.Start, vd);
                    break;
            }
        }

        // Pass 1.5 — resolve the static type of each top-level value binding from
        // its initialiser, in dependency order, before pass 2 validates function
        // bodies. Without this pass a function body reading a forward-declared value
        // binding would see GrobType.Unknown and trigger a false E0005 (D-323).
        ResolveTopLevelValueBindingTypes(unit);

        // Pass 2 — validate all top-level items in source order.
        foreach (AstNode item in unit.TopLevel) {
            Visit(item);
        }

        // Phase 2.5 — §17.1 required-non-nullable field-cycle detection (D-287).
        // Must run after all TypeDecl pass-2 visits so every type's fields are resolved.
        DetectTypeCycles();

        _scopes.Pop();
    }

    // -----------------------------------------------------------------------
    // Fallback — return Unknown rather than throwing, so deferred node kinds
    // (Sprint 4+) are silently tolerated in Sprint 2 programs.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    protected override GrobType DefaultVisit(AstNode node) => GrobType.Unknown;

    // -----------------------------------------------------------------------
    // Error nodes — required abstract overrides (§29.2 contract).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override GrobType VisitErrorExpr(ErrorExpr node) => GrobType.Error;

    /// <inheritdoc/>
    public override GrobType VisitErrorStmt(ErrorStmt node) => GrobType.Unknown;

    /// <inheritdoc/>
    public override GrobType VisitErrorDecl(ErrorDecl node) => GrobType.Unknown;

    // -----------------------------------------------------------------------
    // Private helpers.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Seeds the innermost scope with sentinel symbols for every Grob built-in
    /// function so that call sites resolve without emitting E1001.
    /// </summary>
    private void RegisterBuiltins() {
        RegisterBuiltinFn("print");
        RegisterBuiltinFn("exit");
        RegisterBuiltinFn("input");
    }

    /// <summary>
    /// Seeds the global scope and the user-type registry with the Sprint 7
    /// <c>GrobError</c> hierarchy (D-284) as built-in nominal types, registering the
    /// shared, pre-built sentinel <see cref="Symbol"/>, <see cref="TypeDecl"/> and
    /// <see cref="UserTypeInfo"/> <see cref="ExceptionHierarchy"/> caches per hierarchy
    /// member (all content-identical on every compile, so built once rather than
    /// re-synthesised per <see cref="Check"/> call — direct dictionary assignment here
    /// bypasses <see cref="RegisterSymbol"/>, whose job is building a fresh <c>Symbol</c>
    /// from per-call arguments that are constant in this case) so the existing Sprint 6B
    /// construction path
    /// (<see cref="ResolveConstructionTypeName"/>, <see cref="TypeCheckFieldValues"/>,
    /// <see cref="CollectSuppliedFields"/>, <see cref="EmitMissingFieldErrors"/>)
    /// resolves and constructs <c>throw IoError { ... }</c> completely unmodified
    /// (D-043). These are not user <c>type</c> declarations and carry no pass-1/
    /// pass-2 registration of their own.
    /// </summary>
    private void RegisterExceptionHierarchy() {
        foreach (string name in ExceptionHierarchy.AllNames) {
            _scopes.Peek()[name] = ExceptionHierarchy.SymbolFor(name);
            _userTypeRegistry.Register(ExceptionHierarchy.UserTypeInfoFor(name));
        }
    }

    /// <summary>
    /// Seeds the global scope with a <see cref="NamespaceDecl"/> sentinel symbol per
    /// registered <see cref="NamespaceRegistry"/> namespace (D-342) — a name category
    /// that is neither a value nor a type binding. Mirrors
    /// <see cref="RegisterExceptionHierarchy"/>'s registration shape (including its D-338
    /// object-caching fix, via <see cref="NamespaceRegistry.SymbolFor"/>); unlike that
    /// registration, a namespace has no <see cref="UserTypeRegistry"/> entry, since it is
    /// never constructed as a struct value.
    /// </summary>
    private void RegisterNamespaces() {
        foreach (string name in NamespaceRegistry.NamespaceNames) {
            _scopes.Peek()[name] = NamespaceRegistry.SymbolFor(name);
        }
    }

    private void RegisterBuiltinFn(string name) {
        BuiltinDecl decl = new(name);
        RegisterSymbol(name, GrobType.Unknown, SourceLocation.Unknown, decl);
    }

    /// <summary>
    /// Resolves a binding's final type and optional function descriptor from its
    /// annotation and the initializer's inferred type and descriptor. Emits E0104
    /// when a nullable value targets a non-nullable annotation, otherwise E0001 when
    /// annotation and initializer are incompatible.
    /// </summary>
    /// <param name="annotation">The optional syntactic type annotation on the binding.</param>
    /// <param name="initType">The resolved type of the initializer expression.</param>
    /// <param name="initDescriptor">
    /// The structural descriptor of the initializer, when it is a lambda expression
    /// (D-326). <see langword="null"/> for non-lambda initialisers.
    /// </param>
    /// <param name="initRange">The source range of the initializer, used for diagnostic placement.</param>
    /// <param name="initExpr">
    /// The initializer expression itself, when one exists and may hold a resolvable struct
    /// nominal identity (a struct/anon-struct construction, or an identifier/member access
    /// carrying one). <see langword="null"/> for a const binding, which cannot hold a
    /// struct-construction initialiser (<see cref="IsConstantExpr"/>). Used only to reject a
    /// struct-annotated binding whose initialiser is a differently-named struct — see
    /// <see cref="IsStructNominalMismatch"/>.
    /// </param>
    private (GrobType Type, FunctionTypeDescriptor? Descriptor, ArrayTypeDescriptor? ArrayDescriptor) ResolveBindingFull(
        TypeRef? annotation, GrobType initType, FunctionTypeDescriptor? initDescriptor, SourceRange initRange,
        Expression? initExpr) {
        ArrayTypeDescriptor? initArrayDescriptor = initExpr is not null ? ArrayDescriptorOf(initExpr) : null;
        if (annotation is null) return (initType, initDescriptor, initArrayDescriptor);

        // ResolveSignatureType (not ResolveTypeRefFull) so a user-defined struct annotation
        // resolves to a concrete struct kind and carries its declared name — otherwise every
        // struct-typed binding annotation resolves to Unknown and is never checked against
        // its initialiser at all, flatly or nominally (fix/compiler-struct-nominal-identity,
        // Site B; mirrors the parameter/field/native-argument sites, Sprint 6 close onward).
        (GrobType annotated, string? annotatedNamedTypeName, FunctionTypeDescriptor? annotatedDesc, ArrayTypeDescriptor? annotatedArrayDesc) =
            ResolveSignatureType(annotation);
        if (annotated == GrobType.Unknown) return (initType, initDescriptor, initArrayDescriptor); // unrecognised — permissive

        if (initType == GrobType.Error) return (GrobType.Error, null, null); // cascade suppression

        bool isFunctionAnnotation = annotated == GrobType.Function || annotated == GrobType.NullableFunction;
        bool isArrayAnnotation = annotated == GrobType.Array || annotated == GrobType.NullableArray;
        bool compatible = isFunctionAnnotation
            ? TypesAreAssignable(initType, annotated, initDescriptor, annotatedDesc)
            : TypesAreAssignable(initType, annotated);

        if (compatible && isArrayAnnotation && !ArrayElementAssignable(initArrayDescriptor, annotatedArrayDesc)) {
            compatible = false;
        }

        if (compatible && initExpr is not null && IsStructNominalMismatch(annotated, annotatedNamedTypeName, initExpr)) {
            compatible = false;
        }

        if (!compatible) {
            EmitError(PickAssignabilityError(initType, annotated),
                $"Cannot assign value of type '{TypeName(initType)}' to binding of type '{TypeName(annotated)}'.",
                initRange);
            return (GrobType.Error, null, null);
        }

        // Annotation wins (e.g. int → float widening is recorded as float).
        return (annotated, annotatedDesc, annotatedArrayDesc);
    }

    /// <summary>
    /// Resolves a binding's final type from its optional annotation and its
    /// initializer's inferred type. Emits E0104 when a nullable value targets a
    /// non-nullable annotation, otherwise E0001 when annotation and initializer
    /// are incompatible. Used by <c>const</c>, which cannot hold a struct-construction
    /// initialiser (<see cref="IsConstantExpr"/>), so no initialiser expression is
    /// threaded through for the nominal-identity check.
    /// </summary>
    private GrobType ResolveBinding(TypeRef? annotation, GrobType initType, SourceRange initRange) =>
        ResolveBindingFull(annotation, initType, null, initRange, null).Type;

    /// <summary>
    /// Returns the structural function descriptor of a binding initialiser, when the
    /// initialiser is a lambda (from <see cref="_lambdaDescriptors"/>) or a call that
    /// returns a function type (from <see cref="_callResultDescriptors"/>); otherwise
    /// <see langword="null"/> (D-326; Fixes G and I). The initialiser must already have
    /// been visited so its descriptor is recorded.
    /// </summary>
    /// <summary>
    /// Returns the structural function descriptor of a binding initialiser. Delegates to
    /// <see cref="ExpressionDescriptor"/> so a lambda, a call returning a function type, or
    /// an identifier bound to a function-typed symbol all carry their descriptor through
    /// binding (D-326; Fixes G, I and the annotation-to-annotation case).
    /// </summary>
    private FunctionTypeDescriptor? InitialiserDescriptor(Expression initialiser) =>
        ExpressionDescriptor(initialiser);

    /// <summary>
    /// Returns the structural function descriptor of an arbitrary expression — a lambda,
    /// a call returning a function type, or an identifier bound to a function-typed symbol
    /// (D-326; Fix K). Used where a returned or assigned value may be a bound function
    /// variable rather than a direct lambda. The expression must already have been visited.
    /// </summary>
    private FunctionTypeDescriptor? ExpressionDescriptor(Expression expr) => expr switch {
        LambdaExpr lambda => _lambdaDescriptors.GetValueOrDefault(lambda),
        CallExpr call => _callResultDescriptors.GetValueOrDefault(call),
        IdentifierExpr id => LookupSymbol(id.Name)?.FunctionDescriptor,
        GroupingExpr grp => ExpressionDescriptor(grp.Inner),
        _ => null,
    };

    /// <summary>
    /// Returns the element-type descriptor of an arbitrary array-typed expression (D-351) —
    /// an array literal, a call returning <c>T[]</c>, an identifier bound to an array-typed
    /// symbol, or a chained index into a <c>T[][]</c> value. Mirrors
    /// <see cref="ExpressionDescriptor"/>'s shape for the function-descriptor channel. The
    /// expression must already have been visited so its descriptor is recorded.
    /// </summary>
    private ArrayTypeDescriptor? ArrayDescriptorOf(Expression expr) => expr switch {
        ArrayLiteralExpr literal => _arrayLiteralDescriptors.GetValueOrDefault(literal),
        CallExpr call => _callResultArrayDescriptors.GetValueOrDefault(call),
        IdentifierExpr id => LookupSymbol(id.Name)?.ArrayDescriptor,
        GroupingExpr grp => ArrayDescriptorOf(grp.Inner),
        IndexExpr index => ArrayDescriptorOf(index.Target)?.ElementArrayDescriptor,
        _ => null,
    };

    /// <summary>
    /// Maps a syntactic <see cref="TypeRef"/> to a <see cref="GrobType"/>,
    /// applying the nullable modifier when <see cref="TypeRef.IsNullable"/> is
    /// <c>true</c> (Sprint 3 Increment D — D-014).
    /// </summary>
    internal static GrobType ResolveTypeRef(TypeRef typeRef) {
        // Array type-ref (D-327): T[] resolves to Array; T[]? resolves to NullableArray.
        // The element type is not tracked at the GrobType level (deferred to generics sprint).
        if (typeRef is ArrayTypeRef arr)
            return arr.IsNullable ? GrobType.NullableArray : GrobType.Array;

        GrobType baseType = typeRef.Name switch {
            "int" => GrobType.Int,
            "float" => GrobType.Float,
            "string" => GrobType.String,
            "bool" => GrobType.Bool,
            "nil" => GrobType.Nil,
            // Unparameterised collection tags (Sprint 4 Increment C). Element/key/value
            // type tracking awaits generics (Sprint 5); the bare tag is enough for the
            // for...in lowering to select an iteration shape.
            "array" => GrobType.Array,
            "map" => GrobType.Map,
            _ => GrobType.Unknown, // void, user-defined types, generics — deferred Sprint 5+
        };
        return typeRef.IsNullable ? GrobTypeHelpers.ToNullable(baseType) : baseType;
    }

    /// <summary>
    /// Extended resolution that also returns the structural <see cref="FunctionTypeDescriptor"/>
    /// when <paramref name="typeRef"/> is a <see cref="FunctionTypeRef"/> (D-326).
    /// For all other type references the descriptor is <see langword="null"/> and the
    /// kind is identical to <see cref="ResolveTypeRef"/>.
    /// </summary>
    internal static (GrobType Kind, FunctionTypeDescriptor? Descriptor) ResolveTypeRefFull(TypeRef typeRef) {
        // Array type-ref has no structural descriptor — arrays are not function types.
        if (typeRef is ArrayTypeRef)
            return (ResolveTypeRef(typeRef), null);

        if (typeRef is FunctionTypeRef fnRef) {
            // Resolve each parameter and the return type recursively so that a nested
            // function type (fn(fn(): int): int) carries its inner descriptor and is
            // structurally distinguished from fn(fn(): string): int (D-326). The flat
            // GrobType kind alone collapses both to a single GrobType.Function parameter.
            var paramTypes = new List<GrobType>(fnRef.ParameterTypes.Count);
            var paramDescriptors = new List<FunctionTypeDescriptor?>(fnRef.ParameterTypes.Count);
            foreach (TypeRef paramRef in fnRef.ParameterTypes) {
                (GrobType paramKind, FunctionTypeDescriptor? paramDesc) = ResolveTypeRefFull(paramRef);
                paramTypes.Add(paramKind);
                paramDescriptors.Add(paramDesc);
            }

            (GrobType returnKind, FunctionTypeDescriptor? returnDesc) = ResolveTypeRefFull(fnRef.ReturnType);
            FunctionTypeDescriptor descriptor = new(paramTypes, returnKind, paramDescriptors, returnDesc);
            GrobType kind = fnRef.IsNullable ? GrobType.NullableFunction : GrobType.Function;
            return (kind, descriptor);
        }
        return (ResolveTypeRef(typeRef), null);
    }

    /// <summary>
    /// Resolves a function parameter or return-type annotation, additionally
    /// recognising user-defined type names as struct kinds (Sprint 6) and returning
    /// the declared name — unlike the static <see cref="ResolveTypeRef"/>, written
    /// before the type registry existed, which maps every user-defined name to
    /// <see cref="GrobType.Unknown"/> (the gap noted at
    /// <see cref="GetStructTypeNameFromDecl"/>'s call site). Quiet, like
    /// <see cref="ResolveTypeRef"/>: an unrecognised name resolves to
    /// <see cref="GrobType.Unknown"/> with no diagnostic — the name has already been
    /// validated (or not) wherever the annotation's owning declaration was itself
    /// checked, so re-validating here on every reference would duplicate E1001.
    /// <see cref="ArrayTypeDescriptor"/> (D-351) is resolved separately from
    /// <see cref="ResolveTypeRefFull"/>'s <c>FunctionTypeRef</c> arm — building an element's
    /// named-type identity needs the instance-level <see cref="TryGetNamedStructTypeName"/>,
    /// which the static <see cref="ResolveTypeRefFull"/> cannot call.
    /// </summary>
    private (GrobType Kind, string? NamedTypeName, FunctionTypeDescriptor? FunctionDescriptor, ArrayTypeDescriptor? ArrayDescriptor)
            ResolveSignatureType(TypeRef typeRef) {
        if (typeRef is ArrayTypeRef arrayRef) {
            GrobType arrayKind = arrayRef.IsNullable ? GrobType.NullableArray : GrobType.Array;
            return (arrayKind, null, null, ResolveArrayElementDescriptor(arrayRef.ElementType));
        }

        if (typeRef is FunctionTypeRef) {
            (GrobType kind, FunctionTypeDescriptor? desc) = ResolveTypeRefFull(typeRef);
            return (kind, null, desc, null);
        }

        GrobType builtin = ResolveTypeRef(typeRef);
        if (builtin != GrobType.Unknown) return (builtin, null, null, null);

        // D-356: a registered nominal type (guid, date, ...) is a primitive type
        // distinct from string, never constructed via '{ }' braces, so it does not get
        // an ExceptionHierarchy-style TypeDecl/UserTypeInfo/Symbol registration — there
        // is no construction site for that machinery to serve. Its symbol IS a
        // NamespaceDecl (registered via NamespaceRegistry, D-342) so its static
        // constructors (guid.newV4(), date.now(), ...) resolve through the ordinary
        // namespace-call path unchanged; this branch gives the name used in signature
        // position (parameters, fields) the correct Struct + NamedTypeName identity,
        // which is what makes e.g. 'guid == string' fail as an ordinary type mismatch
        // (D-149).
        if (NamedTypeRegistry.TryGet(typeRef.Name, out _)) {
            GrobType namedKind = typeRef.IsNullable ? GrobType.NullableStruct : GrobType.Struct;
            return (namedKind, typeRef.Name, null, null);
        }

        if (LookupSymbol(typeRef.Name)?.DeclarationNode is TypeDecl) {
            GrobType structKind = typeRef.IsNullable ? GrobType.NullableStruct : GrobType.Struct;
            return (structKind, typeRef.Name, null, null);
        }

        return (GrobType.Unknown, null, null, null);
    }

    /// <summary>
    /// Resolves an array element type reference to its <see cref="ArrayTypeDescriptor"/>
    /// (D-351) — the flat element kind plus, where the flat kind alone is not enough to
    /// distinguish two element shapes, the element's named-type identity (a user
    /// <c>type</c> or <c>guid</c>, via <see cref="TryGetNamedStructTypeName"/>) or its own
    /// nested array descriptor (a <c>T[][]</c> element). A function-typed element
    /// (<c>(fn(): int)[]</c>) resolves its flat <see cref="GrobType.Function"/> kind via the
    /// static <see cref="ResolveTypeRefFull"/> but carries no nested structural descriptor —
    /// distinguishing <c>(fn(): int)[]</c> from <c>(fn(): string)[]</c> element-for-element
    /// is out of scope for D-351 (named as a residual gap, not built ad hoc). Quiet, like
    /// <see cref="ResolveSignatureType"/>: an unrecognised element name resolves to
    /// <see cref="GrobType.Unknown"/> with no diagnostic.
    /// </summary>
    private ArrayTypeDescriptor ResolveArrayElementDescriptor(TypeRef elementType) {
        if (elementType is ArrayTypeRef nestedArray) {
            GrobType nestedKind = ResolveTypeRef(nestedArray);
            return new ArrayTypeDescriptor(nestedKind, null, ResolveArrayElementDescriptor(nestedArray.ElementType));
        }

        if (elementType is FunctionTypeRef) {
            (GrobType functionKind, _) = ResolveTypeRefFull(elementType);
            return new ArrayTypeDescriptor(functionKind);
        }

        GrobType kind = ResolveTypeRef(elementType);
        if (kind == GrobType.Unknown) {
            string? namedTypeName = TryGetNamedStructTypeName(elementType);
            if (namedTypeName is not null) {
                GrobType structKind = elementType.IsNullable ? GrobType.NullableStruct : GrobType.Struct;
                return new ArrayTypeDescriptor(structKind, namedTypeName);
            }
        }
        return new ArrayTypeDescriptor(kind);
    }

    /// <summary>
    /// Returns <see langword="true"/> when a value of <paramref name="from"/> can
    /// be used where <paramref name="to"/> is expected.
    /// Rules (D-178, D-014):
    /// <list type="bullet">
    /// <item><description><c>int → float</c> is the only implicit widening conversion.</description></item>
    /// <item><description><c>nil</c> is assignable to any nullable type (<c>T?</c>).</description></item>
    /// <item><description>A non-nullable <c>T</c> is assignable to its nullable counterpart <c>T?</c>.</description></item>
    /// <item><description>A nullable <c>T?</c> is NOT assignable to non-nullable <c>T</c> (requires explicit unwrap via <c>??</c>).</description></item>
    /// </list>
    /// </summary>
    private static bool TypesAreAssignable(GrobType from, GrobType to) {
        if (from == GrobType.Error || to == GrobType.Error) return true; // Error is universal
        if (from == to) return true;
        if (from == GrobType.Int && to == GrobType.Float) return true; // only implicit widening
        // nil is assignable to any nullable type.
        if (from == GrobType.Nil && GrobTypeHelpers.IsNullable(to)) return true;
        // T is assignable to T? (non-null value into nullable slot).
        if (GrobTypeHelpers.ToNullable(from) == to) return true;
        return false;
    }

    /// <summary>
    /// Function-type-aware overload of <see cref="TypesAreAssignable(GrobType,GrobType)"/>.
    /// When both <paramref name="from"/> and <paramref name="to"/> are function types
    /// (<see cref="GrobType.Function"/> or <see cref="GrobType.NullableFunction"/>),
    /// structural identity of the descriptors is required (D-326, invariant assignability).
    /// Nullable widening (<c>fn(): T</c> assignable to <c>(fn(): T)?</c>) is accepted
    /// when the descriptors match. Falls back to the plain overload for all other type pairs.
    /// </summary>
    private static bool TypesAreAssignable(
        GrobType from, GrobType to,
        FunctionTypeDescriptor? fromDesc, FunctionTypeDescriptor? toDesc) {
        if (from == GrobType.Error || to == GrobType.Error) return true;
        bool fromIsFunction = from == GrobType.Function || from == GrobType.NullableFunction;
        bool toIsFunction = to == GrobType.Function || to == GrobType.NullableFunction;
        if (fromIsFunction && toIsFunction)
            return TypesAreAssignable(from, to) &&
                   fromDesc is not null && toDesc is not null &&
                   DescriptorsAreAssignable(fromDesc, toDesc);
        return TypesAreAssignable(from, to);
    }

    /// <summary>
    /// Structural assignability for function-type descriptors (D-326, invariant
    /// assignability). Arity must match exactly; each parameter and the return type
    /// must be assignable position by position, descending into nested function
    /// descriptors. A <see cref="GrobType.Unknown"/> on either side at any position is
    /// permissive — this is what lets an inferred lambda (whose parameter and body types
    /// are <see cref="GrobType.Unknown"/> in v1) bind to a concrete <c>fn(int): int</c>
    /// annotation, while two fully-concrete descriptors are still compared invariantly.
    /// </summary>
    private static bool DescriptorsAreAssignable(FunctionTypeDescriptor from, FunctionTypeDescriptor to) {
        if (from.ParameterTypes.Count != to.ParameterTypes.Count) return false;

        for (int i = 0; i < from.ParameterTypes.Count; i++) {
            if (!PositionAssignable(
                    from.ParameterTypes[i], to.ParameterTypes[i],
                    DescriptorAt(from.ParameterDescriptors, i), DescriptorAt(to.ParameterDescriptors, i))) {
                return false;
            }
        }

        return PositionAssignable(from.ReturnType, to.ReturnType, from.ReturnDescriptor, to.ReturnDescriptor);
    }

    private static FunctionTypeDescriptor? DescriptorAt(IReadOnlyList<FunctionTypeDescriptor?> list, int index) =>
        index < list.Count ? list[index] : null;

    private static bool PositionAssignable(
        GrobType from, GrobType to, FunctionTypeDescriptor? fromDesc, FunctionTypeDescriptor? toDesc) {
        // Unknown on either side is permissive (inferred lambda parameter/body types).
        if (from == GrobType.Unknown || to == GrobType.Unknown) return true;
        bool fromIsFunction = from == GrobType.Function || from == GrobType.NullableFunction;
        bool toIsFunction = to == GrobType.Function || to == GrobType.NullableFunction;
        if (fromIsFunction && toIsFunction)
            return TypesAreAssignable(from, to) &&
                   fromDesc is not null && toDesc is not null &&
                   DescriptorsAreAssignable(fromDesc, toDesc);
        return TypesAreAssignable(from, to);
    }

    /// <summary>
    /// Returns <see langword="true"/> when an array value described by
    /// <paramref name="from"/> can be used where an array of element descriptor
    /// <paramref name="to"/> is expected (D-351). Element types are invariant — unlike
    /// scalar assignability, no <c>int → float</c> widening — because an array is a
    /// reference to shared, mutable storage whose elements keep their original runtime
    /// representation; a value read back out under a widened static type would
    /// misrepresent its actual <c>GrobValueKind</c>. Either descriptor missing (the
    /// element type could not be determined — an empty literal with no annotation, a
    /// value that flowed through <see cref="GrobType.Unknown"/>) stays permissive,
    /// mirroring how an <see cref="GrobType.Unknown"/> scalar is already treated
    /// elsewhere. Recurses into the nested descriptor for a <c>T[][]</c> element.
    /// </summary>
    private static bool ArrayElementAssignable(ArrayTypeDescriptor? from, ArrayTypeDescriptor? to) {
        if (from is null || to is null) return true;
        if (from.ElementKind == GrobType.Unknown || to.ElementKind == GrobType.Unknown) return true;
        if (from.ElementKind != to.ElementKind) return false;
        if (to.ElementNamedTypeName is not null && from.ElementNamedTypeName != to.ElementNamedTypeName) return false;
        if (to.ElementKind is GrobType.Array or GrobType.NullableArray) {
            return ArrayElementAssignable(from.ElementArrayDescriptor, to.ElementArrayDescriptor);
        }
        return true;
    }

    /// <summary>
    /// Returns <see cref="ErrorCatalog.E0104"/> when a nullable value is assigned
    /// to a non-nullable target, otherwise <see cref="ErrorCatalog.E0001"/>.
    /// </summary>
    private static ErrorDescriptor PickAssignabilityError(GrobType from, GrobType to) =>
        GrobTypeHelpers.IsNullable(from) && !GrobTypeHelpers.IsNullable(to)
            ? ErrorCatalog.E0104
            : ErrorCatalog.E0001;

    /// <summary>
    /// Returns <see langword="true"/> when both operand types are numeric
    /// (<c>int</c> or <c>float</c> or mixed).
    /// </summary>
    private static bool BothNumeric(GrobType left, GrobType right) =>
        (left == GrobType.Int || left == GrobType.Float) &&
        (right == GrobType.Int || right == GrobType.Float);

    /// <summary>
    /// True when either operand is the <c>nil</c> literal. The <c>nil</c> literal is
    /// comparable against any operand under <c>==</c>/<c>!=</c> (§20: <c>x == nil</c>
    /// resolves to bool — <c>false</c> when <c>x</c> is non-nil). This is the canonical
    /// nil check (<c>x == nil</c> / <c>x != nil</c>, either order) that flow-sensitive
    /// narrowing keys off, and it also holds for a value already narrowed to its
    /// non-nullable type inside an enclosing guard. <c>nil == nil</c> is already
    /// accepted by the same-type rule before this is reached.
    /// </summary>
    private static bool IsNilComparison(GrobType left, GrobType right) =>
        left == GrobType.Nil || right == GrobType.Nil;

    private static bool IsComparisonOperator(BinaryOperator op) => op switch {
        BinaryOperator.Equal or BinaryOperator.NotEqual or
        BinaryOperator.Less or BinaryOperator.LessEqual or
        BinaryOperator.Greater or BinaryOperator.GreaterEqual => true,
        _ => false,
    };

    /// <summary>Looks up <paramref name="name"/> in the scope stack, inner-most first.</summary>
    private Symbol? LookupSymbol(string name) {
        foreach (Dictionary<string, Symbol> scope in _scopes) {
            if (scope.TryGetValue(name, out Symbol? symbol)) return symbol;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Reserved identifiers (D-320, generalised from D-282).
    //
    // 'formatAs' and 'select' lex as ordinary identifiers and stay legal as member
    // names after '.', but they may not be bound by user code — a field, parameter,
    // local or function name. Both names consult this single set so the rule cannot
    // diverge. 'formatAs' additionally carries a bare-member rule (D-282); 'select'
    // does not — it is an ordinary method.
    // -----------------------------------------------------------------------
    private static readonly HashSet<string> _reservedIdentifiers =
        new(StringComparer.Ordinal) { "formatAs", "select" };

    /// <summary>
    /// Emits <see cref="ErrorCatalog.E1103"/> when <paramref name="name"/> is a
    /// reserved identifier used as a binding name. Called at every binding-declaration
    /// site — local, function name, parameter and type field.
    /// </summary>
    private void CheckReservedBindingName(string name, SourceRange range) {
        if (_reservedIdentifiers.Contains(name)) {
            EmitError(ErrorCatalog.E1103,
                $"'{name}' is a reserved identifier and cannot be used as a binding name. "
              + "Rename the binding.",
                range);
        }
    }

    /// <summary>
    /// Pass-1 registration of a top-level value binding as a provisional placeholder
    /// (D-321, D-324). Skips when the name is already bound by any symbol — a
    /// non-provisional one (an earlier-finalised binding), an fn/type provisional, or
    /// an earlier value-binding provisional — so the first declaration stays
    /// authoritative. A later duplicate must not overwrite the first: phase 1.5 resolves
    /// the first binding's type and pass 2 reports the duplicate as E1102 at the later
    /// declaration. Overwriting would let a forward reference resolve against the wrong
    /// (later) type and emit a bogus cascade before the E1102.
    /// </summary>
    private void RegisterProvisionalValueBinding(string name, SourceLocation declaredAt, AstNode declarationNode) {
        if (_scopes.Peek().ContainsKey(name)) return;
        RegisterSymbol(name, GrobType.Unknown, declaredAt, declarationNode, provisional: true);
    }

    /// <summary>
    /// Pass-1 registration of a top-level <c>type</c> declaration as a provisional
    /// placeholder (D-324), mirroring <see cref="RegisterProvisionalValueBinding"/>.
    /// Skips when the name already carries a REAL (non-provisional) symbol — a
    /// Sprint 7 exception-hierarchy type (<see cref="RegisterExceptionHierarchy"/>)
    /// — so pass 2's <see cref="FinalizeTopLevelBinding"/> still sees that real
    /// entry and reports E1102 at the user's declaration, exactly as it already
    /// does for two colliding user <c>type</c> declarations. Without this guard
    /// this loop would unconditionally overwrite the real symbol with a
    /// provisional one referencing the user's node, and the collision would never
    /// be reported — confirmed empirically: prior to this guard, the equivalent
    /// unconditional overwrite already let a user <c>fn print() { }</c> silently
    /// shadow the built-in with zero diagnostics (a pre-existing gap this guard
    /// does not extend to <c>FnDecl</c>; that is a separate, unrelated fix).
    /// </summary>
    private void RegisterProvisionalTypeBinding(string name, SourceLocation declaredAt, AstNode declarationNode) {
        if (_scopes.Peek().TryGetValue(name, out Symbol? existing) && !existing.Provisional) return;
        RegisterSymbol(name, GrobType.Unknown, declaredAt, declarationNode, provisional: true);
    }

    /// <summary>
    /// Bundles a symbol's optional type-identity metadata — the function-type descriptor
    /// and/or named-struct-type name(s) that travel alongside a symbol's bare <see
    /// cref="GrobType"/> tag — into a single <see cref="RegisterSymbol"/> parameter. Keeps
    /// the parameter count under the analyser bar (S107) as the set of identity channels
    /// has grown (D-326 function descriptors, Sprint 6 struct names, Sprint 8 Increment
    /// E's array-element struct name).
    /// </summary>
    private readonly record struct SymbolTypeIdentity(
        FunctionTypeDescriptor? FunctionDescriptor = null,
        string? NamedStructTypeName = null,
        ArrayTypeDescriptor? ArrayDescriptor = null);

    private void RegisterSymbol(string name, GrobType type, SourceLocation declaredAt, AstNode declarationNode,
                               bool provisional = false, SymbolTypeIdentity typeIdentity = default) {
        _scopes.Peek()[name] = new Symbol {
            Name = name,
            Type = type,
            DeclaredAt = declaredAt,
            DeclarationNode = declarationNode,
            Provisional = provisional,
            FunctionDescriptor = typeIdentity.FunctionDescriptor,
            NamedStructTypeName = typeIdentity.NamedStructTypeName,
            ArrayDescriptor = typeIdentity.ArrayDescriptor,
        };
    }

    /// <summary>
    /// Resolves <paramref name="typeRef"/>'s named-user-type identity when it denotes a
    /// registered <c>type</c> declaration or a <see cref="NamedTypeRegistry"/> entry
    /// (D-356) — the name-only counterpart of <see cref="ResolveSignatureType"/>'s
    /// registry/<c>TypeDecl</c> arms, kept
    /// separate rather than threaded through that method's widely-consumed return tuple
    /// (Sprint 8 Increment E, <c>formatAs</c>). Also used by
    /// <see cref="ResolveArrayElementDescriptor"/> (D-351) to resolve a <c>T[]</c>
    /// element's named-type identity for <see cref="Symbol.ArrayDescriptor"/> — nested
    /// arrays/function types have no direct name here and resolve their own kind instead.
    /// </summary>
    private string? TryGetNamedStructTypeName(TypeRef typeRef) {
        if (typeRef is ArrayTypeRef or FunctionTypeRef) return null;
        if (ResolveTypeRef(typeRef) != GrobType.Unknown) return null;
        if (NamedTypeRegistry.TryGet(typeRef.Name, out _)) return typeRef.Name;
        return LookupSymbol(typeRef.Name)?.DeclarationNode is TypeDecl ? typeRef.Name : null;
    }

    /// <summary>
    /// Finalises a pass-1 provisional top-level entry as a real binding (D-324). When
    /// a real (non-provisional) binding already exists for <paramref name="name"/>,
    /// emits <see cref="ErrorCatalog.E1102"/> at <paramref name="range"/> and leaves the
    /// existing binding in place. Otherwise registers the symbol as real. The note in the
    /// message carries the prior declaration's line so the user can locate both
    /// declarations.
    /// </summary>
    private void FinalizeTopLevelBinding(
        string name, GrobType type, SourceLocation declaredAt, AstNode declarationNode, SourceRange range,
        FunctionTypeDescriptor? functionDescriptor = null, ArrayTypeDescriptor? arrayDescriptor = null) {
        // Sprint 8 Increment E: 'formatAs' is both a reserved identifier (E1103, D-320) and
        // a pre-registered NamespaceDecl symbol (D-342) — the first reserved identifier to
        // be a namespace ('select' is reserved but not a namespace). Skipping the collision
        // check here avoids a redundant E1102 alongside E1103 for e.g. 'fn formatAs() {}';
        // the reserved-name diagnostic alone already fully explains the error.
        if (!_reservedIdentifiers.Contains(name) &&
                _scopes.Peek().TryGetValue(name, out Symbol? existing) && !existing.Provisional) {
            EmitError(ErrorCatalog.E1102,
                $"'{name}' is already declared in this scope (first declared at line {existing.DeclaredAt.Line}).",
                range);
            return;
        }
        RegisterSymbol(name, type, declaredAt, declarationNode,
            typeIdentity: new(functionDescriptor, ArrayDescriptor: arrayDescriptor));
    }

    /// <summary>
    /// Updates the type of an existing provisional symbol while keeping
    /// <see cref="Symbol.Provisional"/> <see langword="true"/>. Called by phase 1.5
    /// after the type of a top-level value binding has been inferred in dependency
    /// order (D-323). The symbol remains provisional so that pass 2's E1102 guard
    /// still permits re-registration when it finalises the binding.
    /// No-ops when the existing symbol is non-provisional (e.g. an fn or type
    /// declaration whose name is re-used by a value binding — pass 2 handles E1102).
    /// </summary>
    private void UpdateProvisionalType(string name, GrobType type, FunctionTypeDescriptor? functionDescriptor = null) {
        if (!_scopes.Peek().TryGetValue(name, out Symbol? existing)) return;
        if (!existing.Provisional) return;
        RegisterSymbol(name, type, existing.DeclaredAt, existing.DeclarationNode,
            provisional: true, typeIdentity: new(FunctionDescriptor: functionDescriptor));
    }

    /// <summary>Emits an error diagnostic and returns <see cref="GrobType.Error"/>.</summary>
    private GrobType EmitErrorAndReturn(ErrorDescriptor descriptor, string message, SourceRange range) {
        _diagnostics.Add(Diagnostic.Of(descriptor, range, message));
        return GrobType.Error;
    }

    private void EmitError(ErrorDescriptor descriptor, string message, SourceRange range) {
        _diagnostics.Add(Diagnostic.Of(descriptor, range, message));
    }

    private static string TypeName(GrobType type) => type switch {
        GrobType.Int => "int",
        GrobType.Float => "float",
        GrobType.String => "string",
        GrobType.Bool => "bool",
        GrobType.Nil => "nil",
        GrobType.Error => "<error>",
        GrobType.NullableInt => "int?",
        GrobType.NullableFloat => "float?",
        GrobType.NullableString => "string?",
        GrobType.NullableBool => "bool?",
        GrobType.Array => "array",
        GrobType.Map => "map",
        GrobType.Function => "fn",
        GrobType.NullableFunction => "fn?",
        GrobType.NullableArray => "array?",
        GrobType.Struct => "struct",
        GrobType.NullableStruct => "struct?",
        GrobType.AnonStruct => "struct",
        GrobType.NullableAnonStruct => "struct?",
        _ => "unknown",
    };

    private static string OperatorSymbol(BinaryOperator op) => op switch {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.LessEqual => "<=",
        BinaryOperator.Greater => ">",
        BinaryOperator.GreaterEqual => ">=",
        BinaryOperator.And => "&&",
        BinaryOperator.Or => "||",
        BinaryOperator.NilCoalesce => "??",
        _ => "?",
    };
}
