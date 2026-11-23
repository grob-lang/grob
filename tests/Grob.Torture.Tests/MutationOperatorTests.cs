using Grob.Torture.Mutation;
using Grob.Torture.Mutation.Operators;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// Each operator's edit on a small source (prompt §4; gate report §1.6): every candidate the
/// operator lists is applied to the source, and the tests assert the programs that result.
/// </summary>
public sealed class MutationOperatorTests {
    private static List<string> Mutants(IMutationOperator mutationOperator, string source) {
        MutationContext context = MutationContext.TryCreate(source) ?? throw new InvalidOperationException("the source does not parse");
        return [.. mutationOperator.Candidates(context).Select(edits => SourceEdit.Apply(source, edits))];
    }

    // ---- 1. remove a type annotation --------------------------------------------------------

    [Fact]
    public void RemoveAnnotation_Binding_DropsTheAnnotation() {
        Assert.Equal(["x := 1\n"], Mutants(new RemoveAnnotation(), "x: int := 1\n"));
    }

    [Fact]
    public void RemoveAnnotation_Parameters_DropsEachInTurnAndLeavesTheReturnType() {
        List<string> mutants = Mutants(new RemoveAnnotation(), "fn f(a: int, b: string): int {\n    return a\n}\n");

        Assert.Equal(
            ["fn f(a, b: string): int {\n    return a\n}\n", "fn f(a: int, b): int {\n    return a\n}\n"],
            mutants);
    }

    [Fact]
    public void RemoveAnnotation_Field_DropsTheFieldsType() {
        Assert.Equal(["type P {\n    name\n}\n"], Mutants(new RemoveAnnotation(), "type P {\n    name: string\n}\n"));
    }

    [Fact]
    public void RemoveAnnotation_GenericType_DropsAllOfIt() {
        Assert.Equal(["m := map<string, int>{}\n"], Mutants(new RemoveAnnotation(), "m: map<string, int> := map<string, int>{}\n"));
    }

    [Fact]
    public void RemoveAnnotation_NoAnnotation_HasNoCandidate() {
        Assert.Empty(Mutants(new RemoveAnnotation(), "x := 1\n"));
    }

    // ---- 2. replace a value with a literal of another type ----------------------------------

    [Fact]
    public void ReplaceValue_Literal_GivesEachOtherLiteralAndNil() {
        Assert.Equal(
            ["x := 1\n", "x := 1.5\n", "x := \"s\"\n", "x := true\n", "x := nil\n"],
            Mutants(new ReplaceValue(), "x := 5\n"));
    }

    [Fact]
    public void ReplaceValue_ExpressionInitialiser_IsReplacedWhole() {
        // R-58 reaches the VM through the expression form: a float binding's expression becomes an int.
        Assert.Contains("f := -2.5\nrabs: float := 1\n", Mutants(new ReplaceValue(), "f := -2.5\nrabs: float := f.abs()\n"));
    }

    [Fact]
    public void ReplaceValue_LambdaArgument_IsReplacedWhole() {
        // R-60 reaches the VM through a function argument that is not a function.
        Assert.Contains("xs := [1, 2]\nys := xs.filter(1)\n", Mutants(new ReplaceValue(), "xs := [1, 2]\nys := xs.filter(x => x > 1)\n"));
    }

    [Fact]
    public void ReplaceValue_ReturnedValue_IsReplaced() {
        Assert.Contains("fn f(): int {\n    return nil\n}\n", Mutants(new ReplaceValue(), "fn f(): int {\n    return 7\n}\n"));
    }

    [Fact]
    public void ReplaceValue_FieldValue_IsReplaced() {
        Assert.Contains("p := #{ a: nil, b: 2 }\n", Mutants(new ReplaceValue(), "p := #{ a: 1, b: 2 }\n"));
    }

    // ---- 3. make a declared type nullable ---------------------------------------------------

    [Fact]
    public void MakeNullable_Binding_AddsAQuestionMark() {
        Assert.Equal(["x: int? := 1\n"], Mutants(new MakeNullable(), "x: int := 1\n"));
    }

    [Fact]
    public void MakeNullable_ReturnType_AddsAQuestionMark() {
        Assert.Contains("fn f(): int? {\n    return 1\n}\n", Mutants(new MakeNullable(), "fn f(): int {\n    return 1\n}\n"));
    }

    [Fact]
    public void MakeNullable_AlreadyNullable_HasNoCandidate() {
        Assert.Empty(Mutants(new MakeNullable(), "x: int? := 1\n"));
    }

    // ---- 4. move an operand into an inferred lambda -----------------------------------------

    [Fact]
    public void LambdaOperand_Binary_MovesTheLeftOperandIntoTheLambda() {
        Assert.Equal(["x := ((mv) => mv + 2)(1)\n"], Mutants(new LambdaOperand(), "x := 1 + 2\n"));
    }

    [Fact]
    public void LambdaOperand_NestedBinary_GivesOneCandidatePerBinary() {
        List<string> mutants = Mutants(new LambdaOperand(), "x := 1 + 2 * 3\n");

        Assert.Equal(2, mutants.Count);
        Assert.Contains("x := ((mv) => mv + 2 * 3)(1)\n", mutants);
        Assert.Contains("x := 1 + ((mv) => mv * 3)(2)\n", mutants);
    }

    // ---- 5. swap a binary operator's operands -----------------------------------------------

    [Fact]
    public void SwapOperands_Binary_SwapsTheOperands() {
        Assert.Equal(["x := 2 - 1\n"], Mutants(new SwapOperands(), "x := 1 - 2\n"));
    }

    [Fact]
    public void SwapOperands_StringConcatenation_SwapsTheOperands() {
        Assert.Equal(["s := \"b\" + \"a\"\n"], Mutants(new SwapOperands(), "s := \"a\" + \"b\"\n"));
    }

    // ---- 6. replace an identifier with another in scope -------------------------------------

    [Fact]
    public void SwapIdentifier_Use_TakesEachOtherDeclaredName() {
        List<string> mutants = Mutants(new SwapIdentifier(), "a := 1\nb := 2\nprint(a)\n");

        // The callee `print` is an identifier too.
        Assert.Equal(["a := 1\nb := 2\na(a)\n", "a := 1\nb := 2\nb(a)\n", "a := 1\nb := 2\nprint(b)\n"], mutants);
    }

    [Fact]
    public void SwapIdentifier_ParameterAndFunctionNames_AreDeclaredNames() {
        List<string> mutants = Mutants(new SwapIdentifier(), "fn f(n: int): int {\n    return n\n}\nx := f(1)\n");

        Assert.Contains("fn f(n: int): int {\n    return f\n}\nx := f(1)\n", mutants);
        Assert.Contains("fn f(n: int): int {\n    return n\n}\nx := n(1)\n", mutants);
    }

    // ---- 7. route a value through an array element or an anonymous struct field -------------

    [Fact]
    public void RouteValue_Value_IsReadBackFromAnArrayAndAStruct() {
        Assert.Equal(["x := [5][0]\n", "x := #{ v: 5 }.v\n"], Mutants(new RouteValue(), "x := 5\n"));
    }

    // ---- 8. swap ?. and . ; remove a nil-check guard ---------------------------------------

    [Fact]
    public void ChangeOptionalChain_Dot_BecomesQuestionDot() {
        Assert.Equal(["n := s?.length\n"], Mutants(new ChangeOptionalChain(), "n := s.length\n"));
    }

    [Fact]
    public void ChangeOptionalChain_QuestionDot_BecomesDot() {
        Assert.Equal(["n := s.length\n"], Mutants(new ChangeOptionalChain(), "n := s?.length\n"));
    }

    [Fact]
    public void ChangeOptionalChain_NilGuard_IsRemovedAndItsBodyKept() {
        List<string> mutants = Mutants(new ChangeOptionalChain(), "if (x != nil) {\n    print(x)\n}\n");

        Assert.Equal(["\n    print(x)\n\n"], mutants);
    }

    [Fact]
    public void ChangeOptionalChain_GuardWithAnElse_IsNotRemoved() {
        List<string> mutants = Mutants(new ChangeOptionalChain(), "if (x != nil) {\n    print(1)\n} else {\n    print(2)\n}\n");

        Assert.Empty(mutants);
    }

    // ---- 9. delete a statement --------------------------------------------------------------

    [Fact]
    public void DeleteStatement_TopLevel_DeletesEachInTurn() {
        Assert.Equal(["\nb := 2\n", "a := 1\n\n"], Mutants(new DeleteStatement(), "a := 1\nb := 2\n"));
    }

    [Fact]
    public void DeleteStatement_Return_IsDeleted() {
        Assert.Contains("fn f(): int {\n    \n}\n", Mutants(new DeleteStatement(), "fn f(): int {\n    return 1\n}\n"));
    }

    [Fact]
    public void DeleteStatement_Declaration_IsDeletedWithItsBody() {
        Assert.Contains("\nx := 1\n", Mutants(new DeleteStatement(), "fn f(): int {\n    return 1\n}\nx := 1\n"));
    }

    // ---- 10. add, remove or name an argument ------------------------------------------------

    [Fact]
    public void EditArgument_Call_RemovesEachArgumentWithItsComma() {
        List<string> mutants = Mutants(new EditArgument(), "g(1, 2)\n");

        Assert.Contains("g(2)\n", mutants);
        Assert.Contains("g(1)\n", mutants);
    }

    [Fact]
    public void EditArgument_Call_AddsAnArgument() {
        Assert.Contains("g(1, 2, 1)\n", Mutants(new EditArgument(), "g(1, 2)\n"));
    }

    [Fact]
    public void EditArgument_CallWithNoArguments_GainsOne() {
        Assert.Equal(["g(1)\n"], Mutants(new EditArgument(), "g()\n"));
    }

    [Fact]
    public void EditArgument_DeclaredFunction_NamesAnArgumentWithAParameterName() {
        List<string> mutants = Mutants(new EditArgument(), "fn f(a: int, b: int): int {\n    return a\n}\nx := f(1, 2)\n");

        Assert.Contains("fn f(a: int, b: int): int {\n    return a\n}\nx := f(a: 1, 2)\n", mutants);
        Assert.Contains("fn f(a: int, b: int): int {\n    return a\n}\nx := f(1, b: 2)\n", mutants);
    }

    [Fact]
    public void EditArgument_NamedArgument_IsNotNamedAgain() {
        List<string> mutants = Mutants(new EditArgument(), "fn f(a: int): int {\n    return a\n}\nx := f(a: 1)\n");

        Assert.DoesNotContain(mutants, m => m.Contains("a: a:", StringComparison.Ordinal));
    }

    // ---- 11. make a constant operand zero or the integer minimum ---------------------------

    [Fact]
    public void ConstEdge_Constant_BecomesTheSmallestInteger() {
        Assert.Contains("const x := -9223372036854775807 - 1\n", Mutants(new ConstEdge(), "const x := 10 / 2\n"));
    }

    [Fact]
    public void ConstEdge_IntegerOperand_BecomesZero() {
        List<string> mutants = Mutants(new ConstEdge(), "const x := 10 / 2\n");

        Assert.Contains("const x := 0 / 2\n", mutants);
        Assert.Contains("const x := 10 / 0\n", mutants);
    }

    [Fact]
    public void ConstEdge_NotAConstant_HasNoCandidate() {
        Assert.Empty(Mutants(new ConstEdge(), "x := 10 / 2\n"));
    }

    // ---- 12. wrap a callee's receiver in a nullable binding --------------------------------

    [Fact]
    public void NullableReceiver_LiteralInitialisedReceiver_IsBoundNullableAndCalledOptionally() {
        Assert.Equal(
            ["s := \"ab\"\nmn: string? := s\nprint(mn?.upper())\n"],
            Mutants(new NullableReceiver(), "s := \"ab\"\nprint(s.upper())\n"));
    }

    [Fact]
    public void NullableReceiver_AnnotatedReceiver_UsesItsAnnotation() {
        Assert.Equal(
            ["n: int := 5\nmn: int? := n\nprint(mn?.toString())\n"],
            Mutants(new NullableReceiver(), "n: int := 5\nprint(n.toString())\n"));
    }

    [Fact]
    public void NullableReceiver_ReceiverOfUnknownType_HasNoCandidate() {
        Assert.Empty(Mutants(new NullableReceiver(), "s := f()\nprint(s.upper())\n"));
    }

    [Fact]
    public void NullableReceiver_AlreadyOptional_HasNoCandidate() {
        Assert.Empty(Mutants(new NullableReceiver(), "s := \"ab\"\nprint(s?.upper())\n"));
    }

    // ---- all operators ----------------------------------------------------------------------

    [Fact]
    public void Operators_AreTwelveWithDistinctNames() {
        Assert.Equal(12, Mutator.Operators.Count);
        Assert.Equal(12, Mutator.Operators.Select(o => o.Name).Distinct().Count());
    }
}
