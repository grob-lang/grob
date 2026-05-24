using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.AstTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Verifies that <see cref="AstWalker"/> recurses through children for the
/// composite node shapes used most often in early compiler passes.
/// </summary>
public class AstWalkerTests {
    /// <summary>Records every identifier name the walker encounters, in visit order.</summary>
    private sealed class IdentifierCollector : AstWalker {
        public List<string> Names { get; } = [];

        public override Unit VisitIdentifier(IdentifierExpr node) {
            Names.Add(node.Name);
            return default;
        }

        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    [Fact]
    public void Walker_RecursesIntoBinaryOperands() {
        BinaryExpr expr = new(R, BinaryOperator.Add, Id("a"), Id("b"));
        IdentifierCollector c = new();
        c.Visit(expr);
        Assert.Equal(["a", "b"], c.Names);
    }

    [Fact]
    public void Walker_RecursesIntoBlockStatements() {
        BlockStmt block = new(R, [
            new ExpressionStmt(R, Id("a")),
            new ExpressionStmt(R, Id("b")),
            new ExpressionStmt(R, Id("c")),
        ]);
        IdentifierCollector collector = new();
        collector.Visit(block);
        Assert.Equal(["a", "b", "c"], collector.Names);
    }

    [Fact]
    public void Walker_RecursesIntoFnDeclBodyAndParameterDefaults() {
        Parameter pa = new(R, "x", new TypeRef(R, "Int", [], false), Id("dflt"));
        FnDecl fn = new(
            R,
            "f",
            [pa],
            new TypeRef(R, "Int", [], false),
            new BlockStmt(R, [new ReturnStmt(R, Id("body"))]));

        IdentifierCollector c = new();
        c.Visit(fn);
        Assert.Equal(["dflt", "body"], c.Names);
    }

    [Fact]
    public void Walker_RecursesIntoIfElseChain() {
        IfStmt nested = new(
            R,
            Id("c2"),
            new BlockStmt(R, [new ExpressionStmt(R, Id("t2"))]),
            new BlockStmt(R, [new ExpressionStmt(R, Id("e2"))]));
        IfStmt outer = new(
            R,
            Id("c1"),
            new BlockStmt(R, [new ExpressionStmt(R, Id("t1"))]),
            nested);

        IdentifierCollector c = new();
        c.Visit(outer);
        Assert.Equal(["c1", "t1", "c2", "t2", "e2"], c.Names);
    }

    [Fact]
    public void Walker_RecursesIntoCallCalleeAndArguments() {
        CallExpr call = new(R, Id("f"), [
            new CallArgument(R, null, Id("x")),
            new CallArgument(R, "n", Id("y")),
        ]);

        IdentifierCollector c = new();
        c.Visit(call);
        Assert.Equal(["f", "x", "y"], c.Names);
    }

    [Fact]
    public void Walker_RecursesIntoInterpolatedStringExpressionParts() {
        InterpolatedStringExpr s = new(R, [
            new StringTextPart(R, "hello "),
            new StringExpressionPart(R, Id("name")),
            new StringTextPart(R, "!"),
        ]);

        IdentifierCollector c = new();
        c.Visit(s);
        Assert.Equal(["name"], c.Names);
    }

    [Fact]
    public void Walker_DefaultErrorHook_DoesNotRecurseFurther() {
        ErrorExpr err = new(R, ErrDiag());
        IdentifierCollector c = new();
        c.Visit(err);
        Assert.Empty(c.Names);
    }
}
