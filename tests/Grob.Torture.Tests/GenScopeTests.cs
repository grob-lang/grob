using Grob.Torture.Generation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The scope rules that keep a generated program well typed and finite: what a statement may
/// assign, and where a <c>break</c>, a <c>continue</c> and a <c>return</c> may go.
/// </summary>
public sealed class GenScopeTests {
    private static readonly GenVariable Count = new("count", GenType.Int, Assignable: true);
    private static readonly GenVariable Text = new("text", GenType.String, Assignable: true);
    private static readonly GenVariable Fixed = new("fixed", GenType.Int, Assignable: false);

    private static GenScope TopLevel() {
        var scope = new GenScope(returnType: null, visibleFunctions: 2);
        scope.Declare(Count);
        scope.Declare(Text);
        scope.Declare(Fixed);
        return scope;
    }

    [Fact]
    public void Assignable_TopLevel_IsEveryAssignableBinding() {
        Assert.Equal([Count, Text], TopLevel().Assignable);
    }

    [Fact]
    public void Assignable_InsideALoop_LeavesOuterStringsAlone() {
        GenScope scope = TopLevel();
        scope.Push(FrameKind.Loop);
        var inner = new GenVariable("inner", GenType.String, Assignable: true);
        scope.Declare(inner);

        Assert.Equal([Count, inner], scope.Assignable);
    }

    [Fact]
    public void Assignable_InsideALambda_IsOnlyItsOwnBindings() {
        GenScope scope = TopLevel();
        scope.Push(FrameKind.Lambda);
        var local = new GenVariable("local", GenType.Int, Assignable: true);
        scope.Declare(local);

        Assert.Equal([local], scope.Assignable);
        Assert.True(scope.InLambda);
    }

    [Fact]
    public void Pop_ClosesAFrame_AndForgetsItsBindings() {
        GenScope scope = TopLevel();
        scope.Push(FrameKind.Block);
        scope.Declare(new GenVariable("gone", GenType.Bool, Assignable: true));

        scope.Pop();

        Assert.Equal([Count, Text, Fixed], scope.Readable);
        Assert.Equal([Count, Fixed], scope.OfType(GenType.Int));
    }

    [Fact]
    public void DirectlyInLoop_ThroughAnIfBlock_IsTrue() {
        GenScope scope = TopLevel();
        scope.Push(FrameKind.Loop);
        scope.Push(FrameKind.Block);

        Assert.True(scope.DirectlyInLoop);
        Assert.True(scope.InLoop);
    }

    // By name: FrameKind is internal, so a theory cannot take it as a parameter.
    [Theory]
    [InlineData(nameof(FrameKind.Select))]
    [InlineData(nameof(FrameKind.Try))]
    [InlineData(nameof(FrameKind.Finally))]
    [InlineData(nameof(FrameKind.Lambda))]
    public void DirectlyInLoop_ThroughAnotherFrame_IsFalse(string between) {
        GenScope scope = TopLevel();
        scope.Push(FrameKind.Loop);
        scope.Push(Enum.Parse<FrameKind>(between));

        Assert.False(scope.DirectlyInLoop);
    }

    [Fact]
    public void InLoop_LoopOutsideALambda_IsFalseInsideIt() {
        GenScope scope = TopLevel();
        scope.Push(FrameKind.Loop);
        scope.Push(FrameKind.Lambda);

        Assert.False(scope.InLoop);
    }

    [Fact]
    public void InFinally_InsideAFinallyBlock_IsTrue() {
        GenScope scope = TopLevel();
        Assert.False(scope.InFinally);

        scope.Push(FrameKind.Finally);

        Assert.True(scope.InFinally);
    }

    [Fact]
    public void Constructor_CarriesTheReturnTypeAndTheVisibleFunctions() {
        var scope = new GenScope(GenType.Bool, visibleFunctions: 3);

        Assert.Equal(GenType.Bool, scope.ReturnType);
        Assert.Equal(3, scope.VisibleFunctions);
    }
}
