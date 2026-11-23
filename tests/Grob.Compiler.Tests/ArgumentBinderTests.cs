using Grob.Compiler.Ast;
using Grob.Compiler.Binding;
using Grob.Core;
using Grob.Runtime;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Unit tests for <see cref="ArgumentBinder"/>, the one D-113 binding routine shared by
/// script functions and natives (D-446 Decision 1). The binder reports problems by kind
/// and argument index and emits no diagnostic, so these tests drive it through a plain
/// parameter view; the two production views are covered at the end.
/// </summary>
public sealed class ArgumentBinderTests {
    private static readonly SourceRange R = SourceRange.Unknown;

    /// <summary>A parameter view built from (name, has-default) pairs.</summary>
    internal readonly struct Params((string Name, bool HasDefault)[] items) : IBindableParameters {
        public int Count => items.Length;
        public string NameAt(int index) => items[index].Name;
        public bool HasDefaultAt(int index) => items[index].HasDefault;
    }

    // m(a, b = 1, c = 2)
    private static readonly Params ABC = new([("a", false), ("b", true), ("c", true)]);

    private static CallArgument Pos(int value) => new(R, null, new IntLiteralExpr(R, value));
    private static CallArgument Named(string name, int value) => new(R, name, new IntLiteralExpr(R, value));

    [Fact]
    public void Bind_AllPositional_FillsLeadingSlotsInSourceOrder() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Pos(2), Pos(3)], ABC);

        Assert.Empty(binding.Problems);
        Assert.Equal([0, 1, 2], binding.SlotArgument);
        Assert.Equal(3, binding.PositionalCount);
        Assert.False(binding.TooManyPositionals);
        Assert.False(binding.MissingRequired);
    }

    [Fact]
    public void Bind_OmittedDefaults_LeavesSlotsUnbound() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1)], ABC);

        Assert.Empty(binding.Problems);
        Assert.Equal([0, ArgumentBinder.Unbound, ArgumentBinder.Unbound], binding.SlotArgument);
        Assert.False(binding.MissingRequired);
    }

    [Fact]
    public void Bind_NamedSkipsDefault_BindsIntoItsParameter() {
        // m(a, c: x) reaches the callee as m(a, <b default>, x).
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Named("c", 9)], ABC);

        Assert.Empty(binding.Problems);
        Assert.Equal([0, ArgumentBinder.Unbound, 1], binding.SlotArgument);
        Assert.Equal(1, binding.PositionalCount);
    }

    [Fact]
    public void Bind_NamedInEitherOrder_BindsTheSameSlots() {
        ArgumentBinding forward = ArgumentBinder.Bind([Pos(1), Named("b", 5), Named("c", 9)], ABC);
        ArgumentBinding reverse = ArgumentBinder.Bind([Pos(1), Named("c", 9), Named("b", 5)], ABC);

        Assert.Equal([0, 1, 2], forward.SlotArgument);
        Assert.Equal([0, 2, 1], reverse.SlotArgument);
    }

    [Fact]
    public void Bind_PositionalAfterNamed_ReportsAtThatArgument() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Named("b", 5), Pos(3)], ABC);

        BindingProblem problem = Assert.Single(binding.Problems);
        Assert.Equal(BindingProblemKind.PositionalAfterNamed, problem.Kind);
        Assert.Equal(2, problem.ArgumentIndex);
        Assert.Equal(1, binding.PositionalCount);
    }

    [Fact]
    public void Bind_NamedRequiredParameter_ReportsNamesRequired() {
        ArgumentBinding binding = ArgumentBinder.Bind([Named("a", 1)], ABC);

        BindingProblem problem = Assert.Single(binding.Problems);
        Assert.Equal(BindingProblemKind.NamesRequired, problem.Kind);
        Assert.Equal(0, problem.ArgumentIndex);
        Assert.True(binding.MissingRequired);
    }

    [Fact]
    public void Bind_NamedTwice_ReportsDuplicateAtTheSecond() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Named("b", 5), Named("b", 6)], ABC);

        BindingProblem problem = Assert.Single(binding.Problems);
        Assert.Equal(BindingProblemKind.Duplicate, problem.Kind);
        Assert.Equal(2, problem.ArgumentIndex);
        Assert.Equal([0, 1, ArgumentBinder.Unbound], binding.SlotArgument);
    }

    [Fact]
    public void Bind_NamedAlreadyFilledPositionally_ReportsDuplicate() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Pos(2), Named("b", 5)], ABC);

        BindingProblem problem = Assert.Single(binding.Problems);
        Assert.Equal(BindingProblemKind.Duplicate, problem.Kind);
        Assert.Equal(2, problem.ArgumentIndex);
    }

    [Fact]
    public void Bind_UnknownName_ReportsUnknown() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Named("z", 5)], ABC);

        BindingProblem problem = Assert.Single(binding.Problems);
        Assert.Equal(BindingProblemKind.Unknown, problem.Kind);
        Assert.Equal(1, problem.ArgumentIndex);
    }

    [Fact]
    public void Bind_SeveralProblems_ReportsEachInArgumentOrder() {
        ArgumentBinding binding = ArgumentBinder.Bind(
            [Named("z", 1), Named("a", 2), Pos(3), Named("b", 4), Named("b", 5)], ABC);

        Assert.Equal(
            [
                new BindingProblem(BindingProblemKind.Unknown, 0),
                new BindingProblem(BindingProblemKind.NamesRequired, 1),
                new BindingProblem(BindingProblemKind.PositionalAfterNamed, 2),
                new BindingProblem(BindingProblemKind.Duplicate, 4),
            ],
            binding.Problems);
    }

    [Fact]
    public void Bind_TooManyPositionals_CountsThemAll() {
        ArgumentBinding binding = ArgumentBinder.Bind([Pos(1), Pos(2), Pos(3), Pos(4)], ABC);

        Assert.Empty(binding.Problems);
        Assert.Equal(4, binding.PositionalCount);
        Assert.True(binding.TooManyPositionals);
        Assert.Equal([0, 1, 2], binding.SlotArgument);
    }

    [Fact]
    public void Bind_NoArguments_MissesTheRequiredParameter() {
        ArgumentBinding binding = ArgumentBinder.Bind([], ABC);

        Assert.Empty(binding.Problems);
        Assert.True(binding.MissingRequired);
        Assert.False(binding.TooManyPositionals);
    }

    // -----------------------------------------------------------------------
    // The two production views.
    // -----------------------------------------------------------------------

    [Fact]
    public void ScriptParameters_ReadsNamesAndDefaultsFromTheDeclaration() {
        DiagnosticBag bag = new();
        CompilationUnit unit = Parser.Parse(Lexer.Scan("fn f(a: int, b: int = 1): int {\n    return a\n}\n", bag), bag);
        Assert.False(bag.HasErrors);
        FnDecl fn = Assert.IsType<FnDecl>(Assert.Single(unit.TopLevel));
        var view = new ScriptParameters(fn);

        Assert.Equal(2, view.Count);
        Assert.Equal("a", view.NameAt(0));
        Assert.False(view.HasDefaultAt(0));
        Assert.Equal("b", view.NameAt(1));
        Assert.True(view.HasDefaultAt(1));
    }

    [Fact]
    public void SignatureParameters_ReadsNamesAndDefaultsFromTheSignature() {
        var signature = new FunctionSignature("ns.m",
            [
                new SignatureParameter("value", SignatureType.Of(GrobType.String)),
                new SignatureParameter("pattern", SignatureType.Of(GrobType.String), defaultValue: GrobValue.FromString("")),
            ],
            SignatureType.Of(GrobType.String));
        var view = new SignatureParameters(signature);

        Assert.Equal(2, view.Count);
        Assert.Equal("value", view.NameAt(0));
        Assert.False(view.HasDefaultAt(0));
        Assert.Equal("pattern", view.NameAt(1));
        Assert.True(view.HasDefaultAt(1));
    }

    [Fact]
    public void SignatureParameters_VariadicTail_IsOneParameterWithNoDefault() {
        // D-446 Decision 4: positionals past the fixed prefix fill the tail, so naming the
        // tail is E0009 and no further rule is needed.
        var signature = new FunctionSignature("ns.join",
            [new SignatureParameter("parts", SignatureType.Of(GrobType.String), isVariadic: true)],
            SignatureType.Of(GrobType.String));

        ArgumentBinding binding = ArgumentBinder.Bind([Named("parts", 1)], new SignatureParameters(signature));

        Assert.Equal(BindingProblemKind.NamesRequired, Assert.Single(binding.Problems).Kind);
    }
}
