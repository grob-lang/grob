using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>TypeChecker tests for function-type annotations and structural assignability (D-326).</summary>
public sealed class TypeCheckerFunctionTypeTests {
    // -----------------------------------------------------------------------
    // Helper — mirrors TypeCheckerTests.TypeCheckSource
    // -----------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag, SharedCatalogue.Stdlib);
        checker.Check(unit);
        return (unit, bag);
    }

    // -----------------------------------------------------------------------
    // Structural assignability — variable declaration binding
    // -----------------------------------------------------------------------

    /// <summary><c>x: fn(): int := () => 1</c> — identical signature is assignable; no error.</summary>
    [Fact]
    public void FunctionType_IdenticalSignature_IsAssignable() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(): int := () => 1\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// <c>readonly f: fn(): int := () => 1</c> — a readonly binding with a function-type
    /// annotation must carry the lambda descriptor through binding, so an identical
    /// signature is assignable; no spurious error (Fix G).
    /// </summary>
    [Fact]
    public void ReadonlyFunctionType_IdenticalSignature_IsAssignable() {
        (_, DiagnosticBag bag) = TypeCheckSource("readonly f: fn(): int := () => 1\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// <c>readonly f: fn(): int = () => "s"</c> — return-type mismatch on a readonly
    /// binding still emits E0001 at the lambda (Fix G keeps the negative path).
    /// </summary>
    [Fact]
    public void ReadonlyFunctionType_DifferentReturnType_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("readonly f: fn(): int := () => \"s\"\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(26, d.Range.Start.Column);
    }

    /// <summary><c>x: fn(): int := () => "s"</c> — return-type mismatch emits E0001 at the lambda.</summary>
    [Fact]
    public void FunctionType_DifferentReturnType_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(): int := () => \"s\"\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(17, d.Range.Start.Column);
    }

    /// <summary><c>x: fn(int): int := () => 1</c> — arity mismatch emits E0001 at the lambda.</summary>
    [Fact]
    public void FunctionType_DifferentArity_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(int): int := () => 1\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(20, d.Range.Start.Column);
    }

    /// <summary><c>x: (fn(): int)? := () => 1</c> — nullable widening is assignable; no error.</summary>
    [Fact]
    public void FunctionType_NullableWidening_IsAssignable() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: (fn(): int)? := () => 1\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Full pipeline — makeCounter canonical example
    // -----------------------------------------------------------------------

    /// <summary>The full <c>makeCounter</c> function with return type <c>fn(): int</c> type-checks without errors.</summary>
    [Fact]
    public void MakeCounter_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Higher-order parameter annotation
    // -----------------------------------------------------------------------

    /// <summary>A function with a <c>fn(int): int</c> parameter annotation type-checks without errors.</summary>
    [Fact]
    public void HigherOrderParam_FnAnnotation_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn applyOne(action: fn(int): int): void { }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// A higher-order parameter default whose lambda signature does not match the
    /// parameter's function type emits E0004 — the default must be checked with the
    /// descriptor-aware assignability rule (Fix H).
    /// </summary>
    [Fact]
    public void HigherOrderParamDefault_WrongReturnType_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(action: fn(): int = () => \"s\"): void { }\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(26, d.Range.Start.Column);
    }

    /// <summary>
    /// A higher-order parameter default whose lambda signature matches the parameter's
    /// function type type-checks without error (Fix H keeps the positive path).
    /// </summary>
    [Fact]
    public void HigherOrderParamDefault_MatchingSignature_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(action: fn(): int = () => 1): void { }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// Passing a lambda whose signature does not match a function-type parameter emits
    /// E0004 — the argument must be checked with the descriptor-aware rule, not merely
    /// as fn-to-fn (D-326; Fix J).
    /// </summary>
    [Fact]
    public void HigherOrderArgument_WrongSignature_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn applyOne(action: fn(int): int): void { }
            applyOne(() => "s")
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    /// <summary>
    /// Passing a lambda whose signature matches a function-type parameter type-checks
    /// without error (Fix J keeps the positive path).
    /// </summary>
    [Fact]
    public void HigherOrderArgument_MatchingSignature_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn applyOne(action: fn(int): int): void { }
            applyOne((n) => n + 1)
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Call-result function type flows to a binding annotation (Fix I)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>c: fn(): int := makeCounter()</c> where <c>makeCounter</c> returns <c>fn(): int</c>
    /// must type-check: the call's result type and descriptor flow to the annotated
    /// binding, so the matching annotation is assignable (Fix I).
    /// </summary>
    [Fact]
    public void CallReturningFunctionType_MatchingAnnotation_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            readonly c: fn(): int := makeCounter()
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// <c>c: fn(): string := makeCounter()</c> where <c>makeCounter</c> returns
    /// <c>fn(): int</c> must emit E0001: the call's descriptor does not match the
    /// annotation (Fix I keeps the negative path).
    /// </summary>
    [Fact]
    public void CallReturningFunctionType_MismatchedAnnotation_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            readonly c: fn(): string := makeCounter()
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(8, d.Range.Start.Line);
        Assert.Equal(29, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Return-type mismatch (E0005)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>fn f(): fn(): int { return () => "s" }</c> — the lambda's body type (string)
    /// does not match the declared return type (<c>fn(): int</c>); emits E0005 at the lambda.
    /// </summary>
    [Fact]
    public void FunctionType_ReturnMismatch_IsE0005() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(): fn(): int { return () => \"s\" }\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0005", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(28, d.Range.Start.Column);
    }

    /// <summary>
    /// Returning a bound variable whose function type does not match the declared return
    /// type emits E0005 — a non-lambda return value is checked against the symbol's
    /// descriptor, not merely fn-to-fn (D-326; Fix K).
    /// </summary>
    [Fact]
    public void FunctionType_ReturnBoundVariableMismatch_IsE0005() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn f(): fn(): int {
                g: fn(): string := () => "s"
                return g
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0005", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(12, d.Range.Start.Column);
    }

    /// <summary>
    /// Returning a bound variable whose function type matches the declared return type
    /// type-checks without error (Fix K keeps the positive path).
    /// </summary>
    [Fact]
    public void FunctionType_ReturnBoundVariableMatch_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn f(): fn(): int {
                g: fn(): int := () => 1
                return g
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Descriptor-to-descriptor (annotation-to-annotation) structural identity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Binding a <c>fn(int): int</c> variable to a <c>fn(string): int</c> annotation emits
    /// E0001 — a concrete parameter-type mismatch fails invariant structural assignability.
    /// </summary>
    [Fact]
    public void FunctionDescriptors_ParameterTypeMismatch_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            g: fn(int): int := (n) => n + 1
            f: fn(string): int := g
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);
    }

    /// <summary>
    /// Binding a <c>fn(fn(): int): int</c> variable to a <c>fn(fn(): string): int</c>
    /// annotation emits E0001 — a nested parameter descriptor mismatch fails invariant
    /// structural assignability through recursion.
    /// </summary>
    [Fact]
    public void FunctionDescriptors_NestedParameterMismatch_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            g: fn(fn(): int): int := (p) => 0
            f: fn(fn(): string): int := g
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(29, d.Range.Start.Column);
    }

    /// <summary>
    /// Binding a non-function value (<c>5</c>) to a function-type annotation emits E0001 —
    /// the descriptor-aware overload falls back to the plain rule when one side is not a
    /// function type.
    /// </summary>
    [Fact]
    public void FunctionAnnotation_NonFunctionValue_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(): int := 5\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(17, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Nested function-type structural identity (D-326, Fix F)
    //
    // A FunctionTypeDescriptor for a function-typed parameter or return must carry
    // the nested descriptor, otherwise two differently-shaped nested function types
    // (fn(fn(): int): int vs fn(fn(): string): int) both collapse to a flat
    // [GrobType.Function] parameter and compare equal. These tests assert the
    // descriptor's structural-equality contract directly.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two descriptors that differ only in a nested parameter descriptor's return type
    /// must compare unequal — a flat <c>GrobType.Function</c> parameter would collapse them.
    /// </summary>
    [Fact]
    public void NestedFunctionType_DifferentInnerReturn_DescriptorsNotEqual() {
        FunctionTypeDescriptor innerReturnsInt = new([], GrobType.Int);
        FunctionTypeDescriptor innerReturnsString = new([], GrobType.String);

        FunctionTypeDescriptor a = new(
            [GrobType.Function], GrobType.Int, [innerReturnsInt], null);
        FunctionTypeDescriptor b = new(
            [GrobType.Function], GrobType.Int, [innerReturnsString], null);

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Two descriptors with identical nested shape compare equal and hash equally, so the
    /// same nested annotation on both sides of an assignment is assignable.
    /// </summary>
    [Fact]
    public void NestedFunctionType_SameShape_DescriptorsEqual() {
        FunctionTypeDescriptor innerA = new([GrobType.Int], GrobType.Bool);
        FunctionTypeDescriptor innerB = new([GrobType.Int], GrobType.Bool);

        FunctionTypeDescriptor a = new(
            [GrobType.Function], GrobType.Int, [innerA], null);
        FunctionTypeDescriptor b = new(
            [GrobType.Function], GrobType.Int, [innerB], null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// A differing nested return descriptor also breaks equality.
    /// </summary>
    [Fact]
    public void NestedFunctionType_DifferentReturnDescriptor_DescriptorsNotEqual() {
        FunctionTypeDescriptor returnsIntReturner = new([], GrobType.Int);
        FunctionTypeDescriptor returnsStringReturner = new([], GrobType.String);

        FunctionTypeDescriptor a = new(
            [], GrobType.Function, [], returnsIntReturner);
        FunctionTypeDescriptor b = new(
            [], GrobType.Function, [], returnsStringReturner);

        Assert.NotEqual(a, b);
    }

    // -----------------------------------------------------------------------
    // GetHashCode alignment with relaxed equality (PR #95 review fix)
    //
    // ParameterDescriptorsEqual treats [] and [null] as the same (no function-typed
    // positions in both cases). GetHashCode must strip trailing nulls before hashing
    // so equal descriptors always produce the same hash code.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A descriptor built with the 2-arg constructor (ParameterDescriptors = []) equals
    /// one built with the 4-arg constructor using [null] (same logical shape: no nested
    /// descriptors). Their hash codes must also be equal so Dictionary/HashSet lookups work.
    /// </summary>
    [Fact]
    public void NestedFunctionType_EmptyVsNullDescriptorList_HashCodeEqual() {
        FunctionTypeDescriptor twoArg = new([GrobType.Int], GrobType.Bool);
        FunctionTypeDescriptor fourArgNullSlot = new([GrobType.Int], GrobType.Bool, [null], null);
        Assert.Equal(twoArg, fourArgNullSlot);
        Assert.Equal(twoArg.GetHashCode(), fourArgNullSlot.GetHashCode());
    }

    // -----------------------------------------------------------------------
    // Nullable function type assignability (PR #95 review fix — outside-diff)
    //
    // TypesAreAssignable (4-arg) was bypassing the base nullable/non-nullable
    // function kind check, so (fn(): int)? could be assigned to fn(): int when
    // descriptors matched.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A nullable function value cannot be assigned to a non-nullable function annotation
    /// (fn()? → fn() is narrowing and must emit E0104 — the nullable-to-non-nullable code).
    /// </summary>
    [Fact]
    public void NullableFunctionType_AssignedToNonNullable_IsE0104() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            x: (fn(): int)? := () => 1
            y: fn(): int := x
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0104", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(17, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Forward-reference function-type binding (PR #95 review fix — UpdateProvisionalType)
    //
    // Phase 1.5 updates provisional entries without carrying the function descriptor,
    // so when pass 2 processes a binding referencing a later binding (source order),
    // the later binding has no descriptor yet and assignability fails.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A top-level value binding referencing a later-declared binding of the same function
    /// type must type-check without error: phase 1.5 must carry the descriptor so the
    /// forward reference is resolved correctly.
    /// </summary>
    [Fact]
    public void FunctionType_ForwardReferenceBinding_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            f: fn(): int := g
            g: fn(): int := () => 1
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Grouped-lambda parameter default (PR #95 review fix — ExpressionDescriptor)
    //
    // CheckParameterDefaults was only calling _lambdaDescriptors for direct LambdaExpr
    // defaults. A grouped lambda ((lambda)) resolves to null descriptor, triggering
    // spurious E0004 even when the signature matches.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A parameter default supplied as a grouped lambda with a matching signature must
    /// type-check without error — the descriptor helper must recurse through the grouping.
    /// </summary>
    [Fact]
    public void HigherOrderParamDefault_GroupedLambda_MatchingSignature_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(action: fn(): int = (() => 1)): void { }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Calls through a function-typed parameter
    //
    // A parameter's symbol records the enclosing FnDecl as its declaration node
    // (Parameter is not an AstNode), so the call arm used to take `f(x)` inside
    // `applyTo` for a call to applyTo itself and bind the arguments against
    // applyTo's parameters. The call is to a function value and takes the
    // function-value path, as a lambda held in a binding does.
    // -----------------------------------------------------------------------

    [Fact]
    public void CallThroughFunctionTypedParameter_TypeChecksWithoutBindingToEnclosingFunction() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn applyTo(f: fn(int): int, x: int): int {
                return f(x)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_ShadowingTopLevelFunction_CallsTheParameter() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pair(a: int, b: int): int {
                return a + b
            }
            fn useIt(pair: fn(int): int): int {
                return pair(1)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_NamedAsItsOwnFunction_CallsTheParameter() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn twice(twice: fn(int): int): int {
                return twice(1)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ResolvedFunction_DirectCall_IsTheDeclaredFunction() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn add(a: int, b: int): int {
                return a + b
            }
            x := add(1, 2)
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));

        FnDecl add = Assert.Single(unit.TopLevel.OfType<FnDecl>());
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Same(add, call.ResolvedFunction);
    }

    [Fact]
    public void ResolvedFunction_CallThroughFunctionTypedParameter_IsNull() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn applyTo(f: fn(int): int, x: int): int {
                return f(x)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));

        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Null(call.ResolvedFunction);
    }

    /// <summary>
    /// A function type carries no parameter names (D-326), so a named argument through a
    /// function-typed parameter has nothing to bind against. It is refused with E0011 at
    /// each named argument (D-447 Decision 2), never passed in source order.
    /// </summary>
    [Fact]
    public void CallThroughFunctionTypedParameter_NamedArguments_AreE0011AtEachArgument() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(f: fn(int, int): int): int {
                return f(right: 1, left: 10)
            }
            """);
        Assert.Collection(bag.Errors,
            d => AssertFunctionValueNamedArgument(d, line: 2, column: 14),
            d => AssertFunctionValueNamedArgument(d, line: 2, column: 24));
    }

    [Fact]
    public void CallThroughFunctionValueBinding_NamedArgument_IsE0011() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            sub := (a: int, b: int) => a - b
            x := sub(10, b: 1)
            """);
        AssertFunctionValueNamedArgument(Assert.Single(bag.Errors), line: 2, column: 14);
    }

    [Fact]
    public void CallThroughFunctionTypedStructField_NamedArgument_IsE0011() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int, int): int
            }
            bx := Box { op: (a: int, b: int) => a - b }
            x := bx.op(10, b: 1)
            """);
        AssertFunctionValueNamedArgument(Assert.Single(bag.Errors), line: 5, column: 16);
    }

    // -----------------------------------------------------------------------
    // Argument count (E0003) and types (E0004) through a function value
    //
    // Before the parameter fix every call through a function-typed parameter was
    // rejected against the enclosing function; after it the call takes the
    // function-value path, which must check the arguments against the function
    // type or a wrong call compiles and runs on the wrong slots.
    // -----------------------------------------------------------------------

    [Fact]
    public void CallThroughFunctionTypedParameter_TooManyArguments_IsE0003() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(h: fn(int): int): int {
                return h(3, 100)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0003", d.Code);
        Assert.Equal("Function value 'h' expects 1 argument, but 2 were supplied.", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(12, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionValueBinding_TooFewArguments_IsE0003() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            g := (a: int, b: int) => a + b
            x := g(5)
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0003", d.Code);
        Assert.Equal("Function value 'g' expects 2 arguments, but 1 was supplied.", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughCallResult_WrongCount_NamesNoValue() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn make(): fn(int): int {
                return (n: int) => n
            }
            x := make()()
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0003", d.Code);
        Assert.Equal("The function value expects 1 argument, but 0 were supplied.", d.Message);
        Assert.Equal(4, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_WrongArgumentType_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(h: fn(int): int): int {
                return h("x")
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal("Argument 1 to function value 'h' has type 'string', which is not assignable to parameter type 'int'.", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_FunctionArgumentOfWrongShape_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(k: fn(fn(int): int): int): int {
                return k(() => 1)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_FunctionArgumentOfRightShape_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(k: fn(fn(int): int): int): int {
                return k((n: int) => n + 1)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// A lambda that returns a lambda carries the returned lambda's shape, so it matches
    /// a parameter that declares that nested return shape: an expression body, a block's
    /// final expression and a block's final <c>return</c> alike.
    /// </summary>
    [Theory]
    [InlineData("k(() => () => 1)")]
    [InlineData("k(() => {\n        f := () => 1\n        f\n    })")]
    [InlineData("k(() => {\n        return () => 1\n    })")]
    public void CallThroughFunctionTypedParameter_LambdaReturningLambda_MatchesNestedShape(string call) {
        (_, DiagnosticBag bag) = TypeCheckSource($$"""
            fn run(k: fn(fn(): fn(): int): int): int {
                return {{call}}
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// A block lambda whose early <c>return</c> yields a different function shape from
    /// its final value has no single nested return shape, so it does not match a binding
    /// that declares one: otherwise <c>pick(true)() + 1</c> would type as <c>int</c> and
    /// add to a string at run time.
    /// </summary>
    [Fact]
    public void LambdaBinding_EarlyReturnOfDifferentShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            pick: fn(): fn(): int := () => {
                if (flag) {
                    return () => "s"
                }
                return () => 1
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(26, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Reassigning a function-typed binding or parameter (D-326)
    //
    // A call through a parameter is checked against the parameter's function type, so
    // reassigning the parameter must keep that type: otherwise f = () => "s" followed by
    // f(1) types as int and yields a string.
    // -----------------------------------------------------------------------

    [Fact]
    public void Reassignment_FunctionTypedParameter_WrongShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(f: fn(int): int): int {
                f = () => "s"
                return f(1)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void Reassignment_FunctionTypedBinding_WrongShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            g: fn(int): int := (x: int) => x + 1
            g = () => "s"
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(5, d.Range.Start.Column);
    }

    [Fact]
    public void Reassignment_FunctionTypedParameter_CompatibleShape_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(f: fn(int): int, g: fn(int): int): int {
                f = (x) => x + 1
                f = g
                return f(1)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// An array element's function shape is not tracked, so it cannot be shown to match
    /// the parameter's function type and is rejected, as a declaration's initialiser is;
    /// accepting it would leave a later call trusting a shape nothing checked.
    /// </summary>
    [Fact]
    public void Reassignment_FunctionTypedParameter_FromUntrackedArrayElement_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(f: fn(int): int): int {
                ops := [(a: int) => a + 1]
                f = ops[0]
                return f(1)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void Reassignment_UntrackedFunctionBinding_AcceptsAnyFunction() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            ops := [(a: int) => a + 1]
            f := ops[0]
            f = () => "s"
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void FieldAssignment_FunctionTypedField_WrongShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(): int
            }
            bx := Box { op: () => 1 }
            bx.op = () => "s"
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void FieldAssignment_FunctionTypedField_UntrackedValue_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(): int
            }
            bx := Box { op: () => 1 }
            ops := [() => 2]
            bx.op = ops[0]
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(6, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void FieldAssignment_FunctionTypedField_SameShape_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(): int
            }
            bx := Box { op: () => 1 }
            bx.op = () => 2
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void FieldAssignment_AnonStructFunctionField_ComparesKindOnly() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            bx := #{ op: () => 1 }
            bx.op = () => "s"
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Reassignment_FunctionTypedParameter_FromStructField_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): int
            }
            fn apply(f: fn(int): int, bx: Box): int {
                f = bx.op
                return f(1)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void LambdaBinding_EarlyReturnAlreadyInError_IsNotReportedAgain() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            pick: fn(): fn(): int := () => {
                if (flag) {
                    return nope
                }
                () => 1
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E1001", d.Code);
        Assert.Equal(4, d.Range.Start.Line);
        Assert.Equal(16, d.Range.Start.Column);
    }

    [Fact]
    public void LambdaBinding_EarlyReturnOfSameShape_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            pick: fn(): fn(): int := () => {
                if (flag) {
                    return () => 2
                }
                () => 1
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_LambdaEarlyReturnOfNonFunction_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(k: fn(fn(): fn(): int): int, flag: bool): int {
                return k(() => {
                    if (flag) {
                        return 5
                    }
                    () => 1
                })
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_LambdaReturningWrongLambda_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(k: fn(fn(): fn(): int): int): int {
                return k(() => () => "s")
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_ArgumentAlreadyInError_IsNotReportedAgain() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(h: fn(int): int): int {
                return h(nope)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E1001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_UnknownArgument_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn apply(h: fn(int): int): int {
                return h(((x) => x)(1))
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// A lambda's parameter annotations are not read yet (R-47), so a lambda held in a
    /// binding has a function type whose parameters are Unknown: the count is checked,
    /// an argument's type is not.
    /// </summary>
    [Fact]
    public void CallThroughLambdaBinding_ParametersUntyped_ChecksCountOnly() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            g := (a: int) => a
            x := g("s")
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CallThroughFunctionValue_NamedArgumentAndWrongCount_IsE0011Only() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            sub := (a: int, b: int) => a - b
            x := sub(b: 1)
            """);
        AssertFunctionValueNamedArgument(Assert.Single(bag.Errors), line: 2, column: 10);
    }

    [Fact]
    public void CallThroughArrayElement_NamedArgument_IsE0011() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            ops := [(a: int, b: int) => a - b]
            x := ops[0](10, b: 1)
            """);
        AssertFunctionValueNamedArgument(Assert.Single(bag.Errors), line: 2, column: 17);
    }

    [Fact]
    public void CallThroughUndefinedName_NamedArgument_IsNotReportedTwice() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := nope(10, b: 1)\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E1001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void CallOnStructNamingNoField_NamedArgument_IsE1002Only() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int, int): int
            }
            bx := Box { op: (a: int, b: int) => a - b }
            x := bx.nope(10, b: 1)
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E1002", d.Code);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedAnonStructField_NamedArgument_IsE0011() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            bx := #{ op: (a: int, b: int) => a - b }
            x := bx.op(10, b: 1)
            """);
        AssertFunctionValueNamedArgument(Assert.Single(bag.Errors), line: 2, column: 16);
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_ReturningFunctionType_FlowsItsDescriptor() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn outer(make: fn(): fn(): int): int {
                g: fn(): int := make()
                return g()
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CallThroughFunctionTypedParameter_ReturningMismatchedFunctionType_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn outer(make: fn(): fn(): int): int {
                g: fn(): string := make()
                return 0
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(24, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Calling a value that is not a function (E0017)
    //
    // A callee whose type is known and is not a function type would otherwise
    // reach the VM, which faults with an internal error.
    // -----------------------------------------------------------------------

    [Fact]
    public void CallThroughNonFunctionParameter_IsE0017() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(n: int): int {
                return n()
            }
            """);
        AssertNotCallable(Assert.Single(bag.Errors), "'n' has type 'int'", line: 2, column: 12);
    }

    [Fact]
    public void CallThroughNonFunctionBinding_IsE0017() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\ny := x()\n");
        AssertNotCallable(Assert.Single(bag.Errors), "'x' has type 'int'", line: 2, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionCallResult_IsE0017() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn g(): string {
                return "s"
            }
            y := g()()
            """);
        AssertNotCallable(Assert.Single(bag.Errors), "The called value has type 'string'", line: 4, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionStructField_IsE0017() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                count: int
            }
            bx := Box { count: 1 }
            y := bx.count()
            """);
        AssertNotCallable(Assert.Single(bag.Errors), "Field 'count' has type 'int'", line: 5, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionAnonStructField_IsE0017() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            bx := #{ count: 1 }
            y := bx.count()
            """);
        AssertNotCallable(Assert.Single(bag.Errors), "Field 'count' has type 'int'", line: 2, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionBinding_NamedArgument_IsE0017Only() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\ny := x(a: 1)\n");
        AssertNotCallable(Assert.Single(bag.Errors), "'x' has type 'int'", line: 2, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionBinding_ArgumentErrorsStillReported() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\ny := x(nope)\n");
        List<Diagnostic> errors = [.. bag.Errors];
        Assert.Equal(2, errors.Count);
        Assert.Equal("E1001", errors[0].Code);
        Assert.Equal(2, errors[0].Range.Start.Line);
        Assert.Equal(8, errors[0].Range.Start.Column);
        AssertNotCallable(errors[1], "'x' has type 'int'", line: 2, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionResult_UsedAsOperand_IsNotReportedAgain() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\ny := x() + 1\n");
        AssertNotCallable(Assert.Single(bag.Errors), "'x' has type 'int'", line: 2, column: 6);
    }

    [Fact]
    public void CallThroughNonFunctionResult_CalledAgainAsArgument_IsNotReportedAgain() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn take(x: int): int {
                return x
            }
            fn run(n: int): int {
                return take(n()())
            }
            """);
        AssertNotCallable(Assert.Single(bag.Errors), "'n' has type 'int'", line: 5, column: 17);
    }

    [Fact]
    public void CallThroughUnknownCallee_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("g := (f) => f()\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CallThroughNullableFunctionParameter_IsNotE0017() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(f: (fn(): int)?): void {
                print(f())
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // A struct field's declared function type
    //
    // A field declared with a function type keeps that type when read, so it
    // is compared by shape wherever a tracked function value is (D-326).
    // -----------------------------------------------------------------------

    [Fact]
    public void Reassignment_FunctionTypedParameter_FromStructFieldOfWrongShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): string
            }
            fn apply(f: fn(int): int, bx: Box): int {
                f = bx.op
                return f(1)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void Declaration_FromStructFieldOfSameShape_IsAccepted() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): int
            }
            bx := Box { op: (a: int) => a + 1 }
            g: fn(int): int := bx.op
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Declaration_FromStructFieldOfWrongShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): int
            }
            bx := Box { op: (a: int) => a + 1 }
            g: fn(string): int := bx.op
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedStructField_WrongArgumentType_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): int
            }
            bx := Box { op: (a: int) => a + 1 }
            y := bx.op("s")
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal("Argument 1 to function value 'op' has type 'string', which is not assignable to parameter type 'int'.", d.Message);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(12, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedStructField_WrongCount_IsE0003() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): int
            }
            bx := Box { op: (a: int) => a + 1 }
            y := bx.op(1, 2)
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0003", d.Code);
        Assert.Equal("Function value 'op' expects 1 argument, but 2 were supplied.", d.Message);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void CallThroughFunctionTypedStructField_ResolvesReturnType() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            type Box {
                op: fn(int): int
            }
            bx := Box { op: (a: int) => a + 1 }
            s: string := bx.op(1)
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal("Cannot assign value of type 'int' to binding of type 'string'.", d.Message);
        Assert.Equal(5, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Combining function shapes (D-448 Decision 7)
    //
    // Where one value may come from several places — a lambda's return paths,
    // the arms of a ternary, a switch expression or '??' — compatible shapes
    // combine: an inferred lambda's Unknown positions take the concrete type of
    // another path, and a nil path makes the result a nullable function.
    // Conflicting concrete shapes still do not combine.
    // -----------------------------------------------------------------------

    [Fact]
    public void LambdaBinding_TrackedReturnAndInferredTail_CombineIntoDeclaredShape() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            fn outer(f: fn(int): int): void {
                t: fn(): fn(int): int := () => {
                    if (flag) {
                        return f
                    }
                    (x) => 1
                }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Reassignment_TrackedReturnAndInferredTail_CombineIntoDeclaredShape() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn install(flag: bool, f: fn(int): int, target: fn(): fn(int): int): void {
                target = () => {
                    if (flag) {
                        return f
                    }
                    (x) => 1
                }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void InferredLambdaBinding_CombinedReturnShape_ChecksCallsThroughIt() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            fn outer(f: fn(int): int): void {
                t := () => {
                    if (flag) {
                        return f
                    }
                    (x) => 1
                }
                print(t()("s"))
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(9, d.Range.Start.Line);
        Assert.Equal(15, d.Range.Start.Column);
    }

    [Fact]
    public void LambdaBinding_NilEarlyReturn_MatchesNullableFunctionResult() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            fn outer(f: fn(int): int): void {
                t: fn(): (fn(int): int)? := () => {
                    if (flag) {
                        return nil
                    }
                    f
                }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void LambdaBinding_NilEarlyReturn_DoesNotMatchNonNullableFunctionResult() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            fn outer(f: fn(int): int): void {
                t: fn(): fn(int): int := () => {
                    if (flag) {
                        return nil
                    }
                    f
                }
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(30, d.Range.Start.Column);
    }

    [Fact]
    public void LambdaBinding_ReturnsOnlyInsideIf_DoesNotMatchNonNullableFunctionResult() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            fn outer(f: fn(int): int): void {
                t: fn(): fn(int): int := () => {
                    if (flag) {
                        return f
                    }
                }
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(30, d.Range.Start.Column);
    }

    [Fact]
    public void Ternary_InferredAndTrackedArms_CombineShapes() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(flag: bool, f: fn(int): int): void {
                g := flag ? (x) => 1 : f
                print(g("s"))
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(13, d.Range.Start.Column);
    }

    [Fact]
    public void Ternary_InferredAndTrackedArms_BindToDeclaredShape() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(flag: bool, f: fn(int): int): void {
                h: fn(int): int := flag ? f : (x) => 1
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// An arm whose shape is not tracked has nothing to combine with, so the arms are not
    /// held to agree (D-404's permissive-on-missing rule).
    /// </summary>
    [Theory]
    [InlineData("flag ? f : bx.op")]
    [InlineData("flag ? bx.op : f")]
    public void Ternary_UntrackedArm_IsAccepted(string ternary) {
        (_, DiagnosticBag bag) = TypeCheckSource($$"""
            fn pick(flag: bool, f: fn(int): int): void {
                bx := #{ op: (x) => "s" }
                g := {{ternary}}
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Ternary_ConflictingConcreteArms_IsE0002() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(flag: bool, f: fn(int): int): void {
                g := flag ? f : (x) => "s"
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0002", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    [Fact]
    public void SwitchExpression_InferredAndTrackedArms_CombineShapes() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(n: int, f: fn(int): int): void {
                g := n switch { 1 => (x) => 1, _ => f }
                print(g("s"))
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(13, d.Range.Start.Column);
    }

    /// <summary>
    /// Combining is not transitive: each neighbouring pair of these arms combines through
    /// the inferred middle arm, but the first and last conflict, so every arm is combined
    /// into one shape rather than checked pair by pair.
    /// </summary>
    [Fact]
    public void SwitchExpression_ArmsCombinePairwiseButNotOverall_IsE0002() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(n: int, f: fn(int): int, s: fn(string): int): void {
                g := n switch { 1 => f, 2 => (x) => 0, _ => s }
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0002", d.Code);
        Assert.Equal("Switch arms must produce the same type: function signatures do not match.", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    /// <summary>
    /// The same holds for returns: an inferred middle arm's Unknown return keeps the first
    /// arm's <c>int</c> as its bound, which conflicts with the last arm's <c>string</c>.
    /// </summary>
    [Fact]
    public void SwitchExpression_ReturnsCombinePairwiseButNotOverall_IsE0002() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(n: int, f: fn(int): int, s: fn(int): string): void {
                g := n switch { 1 => f, 2 => (x) => x, _ => s }
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0002", d.Code);
        Assert.Equal("Switch arms must produce the same type: function signatures do not match.", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    // An arm's concrete return stays a bound on the combined shape (PR #262 review): the
    // inferred arm's Unknown return does not let an annotation that conflicts with the
    // concrete arm through, at a binding, an argument or a block lambda's result.

    [Fact]
    public void Ternary_InferredArmWithUnknownReturn_BindsToMatchingDeclaredShape() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(flag: bool, f: fn(int): int): void {
                h: fn(int): int := flag ? f : (x) => x
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Ternary_InferredArmWithUnknownReturn_ConflictingDeclaredReturn_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(flag: bool, f: fn(int): int): void {
                g: fn(int): string := flag ? f : (x) => x
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(27, d.Range.Start.Column);
    }

    [Fact]
    public void Ternary_InferredArmWithUnknownReturn_ConflictingParameterReturn_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn take(h: fn(int): string): void { print(h(1)) }
            fn pick(flag: bool, f: fn(int): int): void {
                take(flag ? f : (x) => x)
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    /// <summary>
    /// A binding inferred from a combined shape keeps the bound as its declared shape, so
    /// a reassignment whose return conflicts with the bound is refused, as it is against a
    /// concrete return (D-448 Decision 3), and a later annotation cannot read a stale bound.
    /// </summary>
    [Fact]
    public void CombinedBinding_ReassignedWithConflictingReturn_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(flag: bool, f: fn(int): int): void {
                g := flag ? f : (x) => x
                g = (x) => "s"
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void CombinedBinding_ReassignedWithConflictingBound_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn run(flag: bool, f: fn(int): int, s: fn(int): string): void {
                g := flag ? f : (x) => x
                g = flag ? s : (x) => x
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Theory]
    [InlineData("(x: int) => 7")]
    [InlineData("(x) => x")]
    [InlineData("f")]
    public void CombinedBinding_ReassignedWithMatchingOrUnknownReturn_IsAccepted(string replacement) {
        (_, DiagnosticBag bag) = TypeCheckSource($$"""
            fn run(flag: bool, f: fn(int): int): void {
                g := flag ? f : (x) => x
                g = {{replacement}}
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void LambdaBinding_TrackedReturnAndUnknownReturnTail_ConflictingDeclaredShape_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            fn outer(f: fn(int): int): void {
                t: fn(): fn(int): string := () => {
                    if (flag) {
                        return f
                    }
                    (x) => x
                }
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(33, d.Range.Start.Column);
    }

    [Fact]
    public void LambdaBinding_IfElseReturnsOnBothPaths_MatchesNonNullableFunctionResult() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            t: fn(): fn(): int := () => {
                if (flag) {
                    return () => 1
                } else {
                    return () => 2
                }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void LambdaBinding_IfBranchFallsThrough_DoesNotMatchNonNullableFunctionResult() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            t: fn(): fn(): int := () => {
                if (flag) {
                    print("no value")
                } else {
                    return () => 2
                }
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);
    }

    [Theory]
    [InlineData("try { return () => 1 } catch (e: IoError) { return () => 2 }")]
    [InlineData("try { return () => 1 } finally { print(2) }")]
    public void LambdaBinding_TryThatAlwaysReturns_MatchesNonNullableFunctionResult(string tryStmt) {
        (_, DiagnosticBag bag) = TypeCheckSource($$"""
            t: fn(): fn(): int := () => {
                {{tryStmt}}
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Theory]
    [InlineData("try { return () => 1 } catch (e: IoError) { print(2) }")]
    [InlineData("try { print(1) } catch (e: IoError) { return () => 2 }")]
    public void LambdaBinding_TryThatCanFallThrough_DoesNotMatchNonNullableFunctionResult(string tryStmt) {
        (_, DiagnosticBag bag) = TypeCheckSource($$"""
            t: fn(): fn(): int := () => {
                {{tryStmt}}
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);
    }

    /// <summary>
    /// The inferred arm's Unknown return is not shown to be the tracked arm's
    /// <c>string</c>, so a call through the combined value is not typed <c>string</c> and no
    /// string opcode is chosen for a value that may be an <c>int</c>.
    /// </summary>
    [Fact]
    public void Ternary_InferredArmWithUnknownReturn_LeavesCallResultUnknown() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(flag: bool, f: fn(int): string): void {
                g := flag ? f : (x) => x
                print(g(1))
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        CallExpr call = CollectCalls(unit).Single(c => c.Callee is IdentifierExpr { Name: "g" });
        Assert.Equal(GrobType.Unknown, call.ResolvedReturnType);
    }

    [Fact]
    public void NilCoalesce_InferredFallback_CombinesWithTrackedShape() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn pick(f: (fn(int): int)?): void {
                h: fn(int): int := f ?? ((x) => 1)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    private static void AssertNotCallable(Diagnostic d, string subject, int line, int column) {
        Assert.Equal("E0017", d.Code);
        Assert.Equal($"{subject}, which is not a function, so it cannot be called.", d.Message);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
    }

    private static void AssertFunctionValueNamedArgument(Diagnostic d, int line, int column) {
        Assert.Equal("E0011", d.Code);
        Assert.Equal("A function value does not accept named arguments; pass them positionally.", d.Message);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
    }

    private static List<CallExpr> CollectCalls(CompilationUnit unit) {
        var collector = new CallCollector();
        collector.Visit(unit);
        return collector.Calls;
    }

    private sealed class CallCollector : AstWalker {
        public List<CallExpr> Calls { get; } = [];
        public override Unit VisitCall(CallExpr node) {
            Calls.Add(node);
            return base.VisitCall(node);
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }
}
