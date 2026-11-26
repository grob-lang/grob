using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// An <c>int</c> the checker accepts where a <c>float</c> is expected is widened with
/// <see cref="OpCode.IntToFloat"/> in every position that takes one (R-58, D-460): a binding, an
/// assignment, a return, a struct field and its default, a parameter default, an argument to a
/// function value, an array literal's elements, a map literal's values, an array or map method's
/// argument and a switch's value pattern. Without the opcode the value reaches the first
/// <c>float</c> operation as an <c>int</c> and faults, or, in a comparison, quietly compares
/// unequal. Each case pins the exact opcodes of the chunk that holds the position.
/// </summary>
public sealed class IntToFloatWideningTests {
    private static readonly HashSet<OpCode> OneByteOperand = [
        OpCode.Constant, OpCode.PopN, OpCode.GetLocal, OpCode.SetLocal, OpCode.GetGlobal,
        OpCode.SetGlobal, OpCode.DefineGlobal, OpCode.GetUpvalue, OpCode.SetUpvalue,
        OpCode.GetProperty, OpCode.SetProperty, OpCode.NewArray, OpCode.Call, OpCode.NewStruct,
        OpCode.NewAnonStruct, OpCode.IncrementInt, OpCode.DecrementInt, OpCode.IncrementFloat,
        OpCode.DecrementFloat, OpCode.BuildString, OpCode.TryBegin, OpCode.Import, OpCode.NewMap,
    ];

    private static readonly HashSet<OpCode> TwoByteOperand = [
        OpCode.ConstantLong, OpCode.Jump, OpCode.JumpIfFalse, OpCode.JumpIfTrue, OpCode.Loop,
    ];

    private static Chunk Compile(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    private static List<(OpCode Op, int Operand)> Decode(Chunk chunk) {
        var result = new List<(OpCode, int)>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            int operand = 0;
            if (TwoByteOperand.Contains(op)) {
                operand = (chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1);
                offset += 2;
            } else if (OneByteOperand.Contains(op)) {
                operand = chunk.ReadByte(offset++);
            }
            result.Add((op, operand));
        }
        return result;
    }

    private static List<OpCode> Ops(Chunk chunk) => Decode(chunk).Select(i => i.Op).ToList();

    /// <summary>The opcodes of the script's own chunk.</summary>
    private static List<OpCode> Script(string source) => Ops(Compile(source));

    /// <summary>The opcodes of the body of the declared function <paramref name="name"/>.</summary>
    private static List<OpCode> Function(string source, string name) {
        Chunk script = Compile(source);
        foreach ((OpCode op, int operand) in Decode(script)) {
            if (op is not (OpCode.Constant or OpCode.ConstantLong)) continue;
            if (script.ReadConstant(operand).TryAsFunction(out GrobFunction? function)
                && function is BytecodeFunction bytecode && bytecode.Name == name) {
                return Ops(bytecode.Bytecode);
            }
        }
        throw new Xunit.Sdk.XunitException($"No function constant named '{name}' in the script chunk.");
    }

    private static int Widenings(List<OpCode> ops) => ops.Count(op => op == OpCode.IntToFloat);

    // -----------------------------------------------------------------------
    // A binding
    // -----------------------------------------------------------------------

    [Fact]
    public void GlobalBinding_IntLiteralToFloat_IsWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.DefineGlobal, OpCode.Return],
            Script("x: float := 1"));

    [Fact]
    public void GlobalBinding_IntVariableToFloat_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.IntToFloat, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("n := 2\nx: float := n"));

    [Fact]
    public void LocalBinding_IntLiteralToFloat_IsWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.PopN, OpCode.Nil, OpCode.Return],
            Function("fn f(): void {\n    y: float := 3\n}", "f"));

    [Fact]
    public void ReadonlyBinding_IntLiteralToFloat_IsWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.DefineGlobal, OpCode.Return],
            Script("readonly r: float := 1"));

    [Fact]
    public void ConstBinding_IntLiteralToFloat_FoldsToAFloatConstant() {
        // A const emits no code of its own: every reference inlines the value, so the
        // widening is a compile-time conversion of the folded constant, not an opcode.
        Chunk chunk = Compile("const c: float := 3\nx := c");

        Assert.Equal([OpCode.Constant, OpCode.DefineGlobal, OpCode.Return], Ops(chunk));
        GrobValue inlined = chunk.ReadConstant(Decode(chunk)[0].Operand);
        Assert.True(inlined.IsFloat, $"expected the inlined const to be a float, was {inlined.Kind}");
        Assert.Equal(3.0, inlined.AsFloat());
    }

    [Fact]
    public void Binding_FloatInitialiser_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.DefineGlobal, OpCode.Return],
            Script("x: float := 1.5"));

    [Fact]
    public void Binding_IntInitialiserToIntAnnotation_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.DefineGlobal, OpCode.Return],
            Script("x: int := 1"));

    [Fact]
    public void Binding_MixedArithmeticInitialiser_IsWidenedOnce() {
        // 1 + 2.5 is already a float: the operand is widened inside the addition and the
        // binding adds nothing to it.
        List<OpCode> ops = Script("x: float := 1 + 2.5");

        Assert.Equal(1, Widenings(ops));
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.AddFloat, OpCode.DefineGlobal, OpCode.Return],
            ops);
    }

    [Fact]
    public void Binding_IntTernaryToFloat_IsWidenedAfterTheTernary() {
        // Both arms are int, so the ternary is int and the binding widens its result once.
        List<OpCode> ops = Script("c := true\nx: float := c ? 1 : 2");

        Assert.Equal(1, Widenings(ops));
        int widen = ops.IndexOf(OpCode.IntToFloat);
        Assert.Equal(OpCode.DefineGlobal, ops[widen + 1]);
    }

    // -----------------------------------------------------------------------
    // An assignment
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_GlobalFloat_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.Constant, OpCode.IntToFloat, OpCode.SetGlobal,
                OpCode.Return,
            ],
            Script("x := 1.5\nx = 1"));

    [Fact]
    public void Assignment_LocalFloat_IsWidened() {
        List<OpCode> ops = Function("fn f(): void {\n    y := 1.5\n    y = 3\n}", "f");

        Assert.Equal(
            [
                OpCode.Constant,
                OpCode.Constant, OpCode.IntToFloat, OpCode.SetLocal,
                OpCode.PopN, OpCode.Nil, OpCode.Return,
            ],
            ops);
    }

    [Fact]
    public void Assignment_StructFloatField_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.SetProperty,
                OpCode.Return,
            ],
            Script("type S {\n    f: float\n}\ns := S { f: 1.5 }\ns.f = 1"));

    [Fact]
    public void Assignment_FloatArrayElement_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.SetIndex,
                OpCode.Return,
            ],
            Script("xs: float[] := [1.5]\nxs[0] = 1"));

    [Fact]
    public void Assignment_IntToIntBinding_IsNotWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.Constant, OpCode.SetGlobal,
                OpCode.Return,
            ],
            Script("x := 1\nx = 2"));

    [Fact]
    public void CompoundAssignment_IntIntoFloatBinding_IsStillWidenedOnce() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.AddFloat, OpCode.SetGlobal,
                OpCode.Return,
            ],
            Script("x := 1.5\nx += 1"));

    // -----------------------------------------------------------------------
    // A return
    // -----------------------------------------------------------------------

    [Fact]
    public void Return_IntLiteralFromFloatFunction_IsWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.Return, OpCode.Nil, OpCode.Return],
            Function("fn f(): float { return 1 }", "f"));

    [Fact]
    public void Return_IntParameterFromFloatFunction_IsWidened() =>
        Assert.Equal(
            [OpCode.GetLocal, OpCode.IntToFloat, OpCode.Return, OpCode.Nil, OpCode.Return],
            Function("fn f(n: int): float { return n }", "f"));

    [Fact]
    public void Return_IntFromIntFunction_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.Return, OpCode.Nil, OpCode.Return],
            Function("fn f(): int { return 1 }", "f"));

    [Fact]
    public void Return_FloatFromFloatFunction_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.Return, OpCode.Nil, OpCode.Return],
            Function("fn f(): float { return 1.5 }", "f"));

    [Fact]
    public void Return_InsideALambdaInsideAFloatFunction_IsNotWidenedByTheEnclosingReturnType() {
        // The lambda is its own function with no declared return type: the enclosing fn's
        // `float` must not leak into it. The lambda's body is read where it is called.
        List<OpCode> ops = Function("fn f(): float {\n    g := () => 1\n    return 2.5\n}", "f");

        Assert.Equal(0, Widenings(ops));
    }

    // -----------------------------------------------------------------------
    // A struct field
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_IntForFloatField_IsWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.NewStruct, OpCode.DefineGlobal, OpCode.Return],
            Script("type S {\n    f: float\n}\ns := S { f: 1 }"));

    [Fact]
    public void StructConstruction_IntFieldDefaultForFloatField_IsWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.IntToFloat, OpCode.NewStruct, OpCode.DefineGlobal, OpCode.Return],
            Script("type S {\n    f: float = 1\n}\ns := S {}"));

    [Fact]
    public void StructConstruction_OnlyTheFloatFieldIsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.Constant,
                OpCode.NewStruct, OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("type S {\n    a: int\n    f: float\n    b: int = 3\n}\ns := S { a: 1, f: 2 }"));

    [Fact]
    public void StructConstruction_FloatForFloatField_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.NewStruct, OpCode.DefineGlobal, OpCode.Return],
            Script("type S {\n    f: float\n}\ns := S { f: 1.5 }"));

    // -----------------------------------------------------------------------
    // A parameter default
    // -----------------------------------------------------------------------

    [Fact]
    public void ParameterDefault_IntForFloatParameter_IsWidenedAtTheCallSite() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.Call, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("fn g(x: float = 1): float { return x }\ny := g()"));

    [Fact]
    public void ParameterDefault_SuppliedArgument_IsWidenedAsBefore() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.Call, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("fn g(x: float = 1): float { return x }\ny := g(2)"));

    [Fact]
    public void ParameterDefault_IntForIntParameter_IsNotWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.Call, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("fn g(x: int = 1): int { return x }\ny := g()"));

    [Fact]
    public void ParameterDefault_FilledByNameAroundASkippedDefault_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.Call,
                OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("fn g(a: float = 1, b: int = 2): float { return a }\ny := g(b: 5)"));

    // -----------------------------------------------------------------------
    // An argument to a function value
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionValueCall_ThroughAParameter_WidensAnIntArgument() =>
        Assert.Equal(
            [
                OpCode.GetLocal, OpCode.Constant, OpCode.IntToFloat, OpCode.Call,
                OpCode.Return, OpCode.Nil, OpCode.Return,
            ],
            Function("fn apply(h: fn(float): float): float { return h(2) }", "apply"));

    [Fact]
    public void FunctionValueCall_ThroughATypedBinding_WidensAnIntArgument() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.Call, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("k: fn(float): float := a => a * 2.0\nr := k(3)"));

    [Fact]
    public void FunctionValueCall_ThroughAStructField_WidensAnIntArgument() {
        List<OpCode> ops = Script(
            "type H {\n    g: fn(float): float\n}\nh := H { g: a => a * 2.0 }\nr := h.g(4)");

        int call = ops.LastIndexOf(OpCode.Call);
        Assert.Equal([OpCode.Constant, OpCode.IntToFloat, OpCode.Call], ops.GetRange(call - 2, 3));
    }

    [Fact]
    public void FunctionValueCall_ThroughACallResult_WidensAnIntArgument() {
        List<OpCode> ops = Script(
            "fn mk(): fn(float): float {\n    return a => a * 2.0\n}\nr := mk()(3)");

        int call = ops.LastIndexOf(OpCode.Call);
        Assert.Equal([OpCode.Constant, OpCode.IntToFloat, OpCode.Call], ops.GetRange(call - 2, 3));
    }

    [Fact]
    public void FunctionValueCall_WithAFloatArgument_IsNotWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.Call, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("k: fn(float): float := a => a * 2.0\nr := k(3.5)"));

    [Fact]
    public void FunctionValueCall_WithIntParameter_IsNotWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.Call, OpCode.DefineGlobal,
                OpCode.Return,
            ],
            Script("k: fn(int): int := a => a + 1\nr := k(3)"));

    [Fact]
    public void FunctionValueCall_WidensOnlyTheFloatParameterOfTwo() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.Call,
                OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("k: fn(int, float): float := (a, b) => b * 2.0\nr := k(3, 4)"));

    [Fact]
    public void FunctionValueCall_WidensEveryFloatParameterGivenAnInt() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.IntToFloat,
                OpCode.Call, OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("k: fn(float, float): float := (a, b) => a * b * 2.0\nr := k(1, 2)"));

    // -----------------------------------------------------------------------
    // An array literal's elements
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayLiteral_IntBesideAFloat_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.NewArray,
                OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("xs: float[] := [1, 2.5]"));

    [Fact]
    public void ArrayLiteral_FloatThenInt_WidensTheLaterElement() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.NewArray,
                OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("zs := [2.5, 1]"));

    [Fact]
    public void ArrayLiteral_AllInts_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal, OpCode.Return],
            Script("xs := [1, 2]"));

    [Fact]
    public void ArrayLiteral_AllFloats_IsNotWidened() =>
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal, OpCode.Return],
            Script("xs := [1.5, 2.5]"));

    [Fact]
    public void ArrayLiteral_NestedArrays_WidensEachInnerLiteral() {
        List<OpCode> ops = Script("rows: float[][] := [[1, 2.5], [3, 4.5]]");

        Assert.Equal(2, Widenings(ops));
        Assert.Equal(
            [
                OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.NewArray,
                OpCode.Constant, OpCode.IntToFloat, OpCode.Constant, OpCode.NewArray,
                OpCode.NewArray, OpCode.DefineGlobal, OpCode.Return,
            ],
            ops);
    }

    // -----------------------------------------------------------------------
    // What the checker still refuses: nothing here accepts more than it did
    // -----------------------------------------------------------------------

    private static Diagnostic SingleError(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return Assert.Single(bag.Errors);
    }

    [Theory]
    [InlineData("x: float? := 1", 1, 14)]
    [InlineData("x: float? := nil\nx = 1", 2, 5)]
    public void NullableFloat_GivenAnInt_IsStillRefused(string source, int line, int column) {
        // T widens to T?, and int widens to float, but int does not reach float? in one step.
        Diagnostic diagnostic = SingleError(source);

        Assert.Equal("E0001", diagnostic.Code);
        Assert.Equal(line, diagnostic.Range.Start.Line);
        Assert.Equal(column, diagnostic.Range.Start.Column);
    }

    [Fact]
    public void FloatArray_GivenIntElements_IsStillRefused() {
        // A whole int array is not a float array: the elements keep their representation (D-351).
        Diagnostic diagnostic = SingleError("xs: float[] := [1, 2]");

        Assert.Equal("E0001", diagnostic.Code);
        Assert.Equal(1, diagnostic.Range.Start.Line);
        Assert.Equal(16, diagnostic.Range.Start.Column);
    }

    [Fact]
    public void IntBinding_GivenAFloat_IsStillRefused() {
        Diagnostic diagnostic = SingleError("x: int := 1.5");

        Assert.Equal("E0001", diagnostic.Code);
        Assert.Equal(1, diagnostic.Range.Start.Line);
        Assert.Equal(11, diagnostic.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // A map literal's values
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_IntValueForFloatMap_IsWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.NewMap,
                OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("m := map<string, float>{ \"a\": 1 }"));

    [Fact]
    public void MapLiteral_WithNoValueTypeArgument_IsNotWidened() =>
        // `map<string>` names no value type: the checker leaves it permissive, so there is
        // no float for an int to be widened to.
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.NewMap, OpCode.DefineGlobal, OpCode.Return],
            Script("m := map<string>{ \"a\": 1 }"));

    [Fact]
    public void MapLiteral_OnlyTheFloatMapsValuesAreWidened() =>
        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.NewMap,
                OpCode.DefineGlobal, OpCode.Return,
            ],
            Script("m := map<string, int>{ \"a\": 1 }"));

    // -----------------------------------------------------------------------
    // An array or map method's argument
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("fs: float[] := [1.5]\nfs.append(2)")]
    [InlineData("fs: float[] := [1.5]\nfs.insert(0, 2)")]
    [InlineData("fs: float[] := [1.5]\nb := fs.contains(2)")]
    [InlineData("m := map<string, float>{ \"a\": 1.5 }\nm.set(\"b\", 2)")]
    public void CollectionMethodCall_IntForTheElementOrValue_IsWidenedBeforeTheCall(string source) {
        List<(OpCode Op, int Operand)> instructions = Decode(Compile(source));

        int widen = instructions.FindIndex(i => i.Op == OpCode.IntToFloat);
        Assert.True(widen > 0, "expected an IntToFloat");
        Assert.Equal(OpCode.Constant, instructions[widen - 1].Op);
        Assert.Equal(OpCode.Call, instructions[widen + 1].Op);
        Assert.Equal(1, instructions.Count(i => i.Op == OpCode.IntToFloat));
    }

    [Fact]
    public void ArrayInsert_WidensTheValueAndNotTheIndex() {
        List<(OpCode Op, int Operand)> instructions = Decode(Compile("fs: float[] := [1.5]\nfs.insert(0, 2)"));

        int call = instructions.FindLastIndex(i => i.Op == OpCode.Call);
        Assert.Equal(2, instructions[call].Operand);
        Assert.Equal(
            [OpCode.Constant, OpCode.Constant, OpCode.IntToFloat, OpCode.Call],
            instructions.GetRange(call - 3, 4).Select(i => i.Op));
    }

    [Theory]
    [InlineData("fs: float[] := [1.5]\nfs.append(2.5)")]
    [InlineData("ns: int[] := [1]\nns.append(2)")]
    [InlineData("m := map<string, int>{ \"a\": 1 }\nm.set(\"b\", 2)")]
    public void CollectionMethodCall_MatchingElementType_IsNotWidened(string source) =>
        Assert.Equal(0, Widenings(Script(source)));

    // -----------------------------------------------------------------------
    // A switch's value pattern
    // -----------------------------------------------------------------------

    [Fact]
    public void SwitchValuePattern_IntPatternOnAFloatSubject_IsWidened() {
        List<OpCode> ops = Script("x := 2.0\nr := x switch {\n    1 => \"one\",\n    _ => \"other\"\n}");

        int equal = ops.IndexOf(OpCode.Equal);
        Assert.Equal([OpCode.Constant, OpCode.IntToFloat, OpCode.Equal], ops.GetRange(equal - 2, 3));
        Assert.Equal(1, Widenings(ops));
    }

    [Fact]
    public void SwitchValuePattern_IntPatternOnAnIntSubject_IsNotWidened() {
        List<OpCode> ops = Script("x := 2\nr := x switch {\n    1 => \"one\",\n    _ => \"other\"\n}");

        Assert.Equal(0, Widenings(ops));
    }

    [Fact]
    public void SwitchValuePattern_IntPatternOnANullableFloatSubject_IsWidened() {
        List<OpCode> ops = Script("x: float? := 2.0\nr := x switch {\n    1 => \"one\",\n    _ => \"other\"\n}");

        int equal = ops.IndexOf(OpCode.Equal);
        Assert.Equal([OpCode.Constant, OpCode.IntToFloat, OpCode.Equal], ops.GetRange(equal - 2, 3));
    }

    [Fact]
    public void SwitchValuePattern_FloatPatternOnAFloatSubject_IsNotWidened() {
        List<OpCode> ops = Script("x := 2.0\nr := x switch {\n    1.5 => \"a\",\n    _ => \"other\"\n}");

        Assert.Equal(0, Widenings(ops));
    }
}
