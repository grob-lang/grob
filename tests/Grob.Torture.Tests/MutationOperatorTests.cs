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

    // A binary operand keeps its grouping when it moves, so the swap exchanges the operands and
    // nothing else; the inner binary is swapped as a candidate of its own.
    [Fact]
    public void SwapOperands_BinaryLeftOperand_IsParenthesised() {
        Assert.Equal(["x := 3 - (1 - 2)\n", "x := 2 - 1 - 3\n"], Mutants(new SwapOperands(), "x := 1 - 2 - 3\n"));
    }

    [Fact]
    public void SwapOperands_BinaryRightOperand_IsParenthesised() {
        Assert.Equal(["x := (2 * 3) - 1\n", "x := 1 - 3 * 2\n"], Mutants(new SwapOperands(), "x := 1 - 2 * 3\n"));
    }

    [Fact]
    public void SwapOperands_GroupedOperand_IsNotParenthesisedAgain() {
        Assert.Equal(["x := (2 - 3) - 1\n", "x := 1 - (3 - 2)\n"], Mutants(new SwapOperands(), "x := 1 - (2 - 3)\n"));
    }

    // A range's bounds are its own children, not a binary's, so a binary bound ends before the
    // range's '..' or 'step' and the swap leaves the range as it was.
    [Fact]
    public void SwapOperands_BinaryRangeStart_EndsBeforeTheRangeOperator() {
        Assert.Equal(
            ["for i in 2 + 1..4 {\n    print(i)\n}\n"],
            Mutants(new SwapOperands(), "for i in 1 + 2..4 {\n    print(i)\n}\n"));
    }

    [Fact]
    public void SwapOperands_BinaryRangeEndBeforeAStep_EndsBeforeTheStep() {
        Assert.Equal(
            ["for i in 0..2 + 1 step 3 {\n    print(i)\n}\n"],
            Mutants(new SwapOperands(), "for i in 0..1 + 2 step 3 {\n    print(i)\n}\n"));
    }

    [Fact]
    public void SwapOperands_BinaryRangeStep_RunsToTheEndOfTheRange() {
        Assert.Equal(
            ["for i in 0..9 step 2 + 1 {\n    print(i)\n}\n"],
            Mutants(new SwapOperands(), "for i in 0..9 step 1 + 2 {\n    print(i)\n}\n"));
    }

    [Fact]
    public void SwapOperands_LoopIterable_StopsBeforeTheLoopBody() {
        Assert.Equal(
            ["for x in b + a {\n    print(x)\n}\n"],
            Mutants(new SwapOperands(), "for x in a + b {\n    print(x)\n}\n"));
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

    // The tree is the parser's, with no scopes, so a name declared more than once is resolved only
    // when every declaration of it gives the same type.
    [Fact]
    public void NullableReceiver_NameDeclaredWithTwoTypes_HasNoCandidate() {
        Assert.Empty(Mutants(new NullableReceiver(),
            "fn f(x: int): string {\n    return x.toString()\n}\nfn g(x: string): string {\n    return x.upper()\n}\n"));
    }

    [Fact]
    public void NullableReceiver_NameDeclaredTwiceWithOneType_IsStillACandidate() {
        List<string> mutants = Mutants(new NullableReceiver(),
            "fn f(x: int): string {\n    return x.toString()\n}\nfn g(x: int): string {\n    return x.toString()\n}\n");

        Assert.Equal(2, mutants.Count);
        Assert.All(mutants, m => Assert.Contains("mn: int? := x\n", m, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("const s := \"cd\"\n")]
    [InlineData("readonly s := \"cd\"\n")]
    [InlineData("fn s(): int {\n    return 1\n}\n")]
    [InlineData("try {\n    g()\n} catch (s: IoError) {\n    g()\n}\n")]
    public void NullableReceiver_NameAlsoDeclaredWithNoTypeToRead_HasNoCandidate(string otherDeclaration) {
        Assert.Empty(Mutants(new NullableReceiver(), otherDeclaration + "s := \"ab\"\nprint(s.upper())\n"));
    }

    // The binding goes before the statement, where a lambda's parameter is not in scope, so a
    // name a lambda declares is never given a type, even beside an outer binding of that type.
    [Fact]
    public void NullableReceiver_NameALambdaParameterDeclares_HasNoCandidate() {
        Assert.Empty(Mutants(new NullableReceiver(),
            "s := \"ab\"\nf := (s: string) => s.upper()\nprint(s.upper())\n"));
    }

    [Fact]
    public void NullableReceiver_NameAlsoALoopVariable_HasNoCandidate() {
        Assert.Empty(Mutants(new NullableReceiver(),
            "n: int := 1\nfor n in [\"a\"] {\n    print(n.upper())\n}\nprint(n.toString())\n"));
    }

    // ---- all operators ----------------------------------------------------------------------

    [Fact]
    public void Operators_AreTwelveWithDistinctNames() {
        Assert.Equal(12, Mutator.Operators.Count);
        Assert.Equal(12, Mutator.Operators.Select(o => o.Name).Distinct().Count());
    }
}
