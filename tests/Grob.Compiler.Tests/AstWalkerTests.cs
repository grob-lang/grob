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

    [Fact]
    public void Walker_RecursesIntoEveryCompositeNodeKind() {
        TypeRef intType = new(R, "Int", [], false);

        // Expression composites: Unary, Grouping, Ternary, ArrayLiteral, Index, MemberAccess,
        // NumericRange (with step), Lambda (block body + parameter default).
        Expression expressionTree = new BinaryExpr(R, BinaryOperator.Add,
            new UnaryExpr(R, UnaryOperator.Negate, Id("u")),
            new GroupingExpr(R,
                new TernaryExpr(R, Id("tc"),
                    new ArrayLiteralExpr(R, [Id("ae1"), Id("ae2")]),
                    new IndexExpr(R, Id("ix-t"), Id("ix-i")))));

        Statement memberAndRange = new ExpressionStmt(R,
            new BinaryExpr(R, BinaryOperator.Add,
                new MemberAccessExpr(R, Id("ma-t"), "m"),
                new NumericRangeExpr(R, Id("rs"), Id("re"), Id("rstep"))));

        Statement lambdaStmt = new ExpressionStmt(R,
            new LambdaExpr(R,
                [new Parameter(R, "p", intType, Id("lp-default"))],
                new LambdaBlockBody(new BlockStmt(R, [new ExpressionStmt(R, Id("lb"))]))));

        // Statement composites: VarDecl, Assignment, CompoundAssignment, Increment, While, ForIn,
        // Select (cases + default), Return (value), Try (catches + finally).
        Statement varStmt = new VarDeclStmt(R, "v", null, Id("vi"));
        Statement assignStmt = new AssignmentStmt(R, Id("a-t"), Id("a-v"));
        Statement compoundStmt = new CompoundAssignmentStmt(R, Id("ca-t"),
            CompoundAssignmentOperator.PlusAssign, Id("ca-v"));
        Statement incStmt = new IncrementStmt(R, Id("inc-t"), IncrementKind.Increment);
        Statement whileStmt = new WhileStmt(R, Id("wc"),
            new BlockStmt(R, [new ExpressionStmt(R, Id("wb"))]));
        Statement forInStmt = new ForInStmt(R, ["x"], Id("fi"),
            new BlockStmt(R, [new ExpressionStmt(R, Id("fb"))]));
        Statement selectStmt = new SelectStmt(R, Id("ss"),
            [new CaseClause(R, [Id("sp1"), Id("sp2")],
                new BlockStmt(R, [new ExpressionStmt(R, Id("sb"))]))],
            new BlockStmt(R, [new ExpressionStmt(R, Id("sd"))]));
        Statement returnStmt = new ReturnStmt(R, Id("rv"));
        Statement tryStmt = new TryStmt(R,
            new BlockStmt(R, [new ExpressionStmt(R, Id("tb"))]),
            [new CatchClause(R, intType, "e",
                new BlockStmt(R, [new ExpressionStmt(R, Id("tc-b"))]))],
            new BlockStmt(R, [new ExpressionStmt(R, Id("tf"))]));

        // Leaf nodes (no walker override — exercise the AstVisitor<T> default delegators).
        Statement leafStmts = new BlockStmt(R, [
            new ExpressionStmt(R, new IntLiteralExpr(R, 1)),
            new ExpressionStmt(R, new FloatLiteralExpr(R, 1.5)),
            new ExpressionStmt(R, new StringLiteralExpr(R, "s")),
            new ExpressionStmt(R, new RawStringLiteralExpr(R, "s")),
            new ExpressionStmt(R, new RegexLiteralExpr(R, "p", "")),
            new ExpressionStmt(R, new BoolLiteralExpr(R, true)),
            new ExpressionStmt(R, new NilLiteralExpr(R)),
            new ExpressionStmt(R,
                new InterpolatedStringExpr(R, [
                    new StringTextPart(R, "hi "),
                    new StringExpressionPart(R, Id("is")),
                ])),
            new BreakStmt(R),
            new ContinueStmt(R),
        ]);

        // Declaration composites: TypeDecl (TypeField with default), ParamBlockDecl, ConstDecl,
        // ReadonlyDecl, ImportDecl (leaf).
        BlockStmt body = new(R, [
            new ExpressionStmt(R, expressionTree),
            memberAndRange,
            lambdaStmt,
            varStmt,
            assignStmt,
            compoundStmt,
            incStmt,
            whileStmt,
            forInStmt,
            selectStmt,
            returnStmt,
            tryStmt,
            leafStmts,
        ]);

        FnDecl fn = new(R, "f",
            [new Parameter(R, "fp", intType, Id("fp-default"))],
            intType, body);

        TypeDecl typeDecl = new(R, "T",
            [new TypeField(R, "tf", intType, Id("tf-default"))]);

        ParamBlockDecl paramBlock = new(R,
            [new Parameter(R, "pb", intType, Id("pb-default"))]);

        ConstDecl constDecl = new(R, "K", null, Id("ck"));
        ReadonlyDecl readonlyDecl = new(R, "R", null, Id("rk"));

        IdentifierCollector c = new();
        c.Visit(fn);
        c.Visit(typeDecl);
        c.Visit(paramBlock);
        c.Visit(constDecl);
        c.Visit(readonlyDecl);
        // Leaf declaration:
        c.Visit(new ImportDecl(R, "m", null));

        Assert.Equal([
            "fp-default",
            "u", "tc", "ae1", "ae2", "ix-t", "ix-i",
            "ma-t", "rs", "re", "rstep",
            "lp-default", "lb",
            "vi",
            "a-t", "a-v",
            "ca-t", "ca-v",
            "inc-t",
            "wc", "wb",
            "fi", "fb",
            "ss", "sp1", "sp2", "sb", "sd",
            "rv",
            "tb", "tc-b", "tf",
            "is",
            "tf-default",
            "pb-default",
            "ck",
            "rk",
        ], c.Names);
    }

    [Fact]
    public void Walker_NumericRange_WithoutStep_DoesNotThrow() {
        NumericRangeExpr range = new(R, Id("s"), Id("e"), null);
        IdentifierCollector c = new();
        c.Visit(range);
        Assert.Equal(["s", "e"], c.Names);
    }

    [Fact]
    public void Walker_Return_WithoutValue_DoesNotRecurse() {
        IdentifierCollector c = new();
        c.Visit(new ReturnStmt(R, null));
        Assert.Empty(c.Names);
    }

    [Fact]
    public void Walker_Try_WithoutFinally_StillRecursesCatches() {
        TryStmt t = new(R,
            new BlockStmt(R, [new ExpressionStmt(R, Id("b"))]),
            [new CatchClause(R, new TypeRef(R, "E", [], false), null,
                new BlockStmt(R, [new ExpressionStmt(R, Id("cb"))]))],
            null);
        IdentifierCollector c = new();
        c.Visit(t);
        Assert.Equal(["b", "cb"], c.Names);
    }

    [Fact]
    public void Walker_Lambda_WithExpressionBody_RecursesIntoExpression() {
        LambdaExpr lam = new(R, [],
            new LambdaExpressionBody(Id("lx")));
        IdentifierCollector c = new();
        c.Visit(lam);
        Assert.Equal(["lx"], c.Names);
    }

    [Fact]
    public void Walker_Select_WithoutDefault_StillRecursesCases() {
        SelectStmt s = new(R, Id("subj"),
            [new CaseClause(R, [Id("p")],
                new BlockStmt(R, [new ExpressionStmt(R, Id("b"))]))],
            null);
        IdentifierCollector c = new();
        c.Visit(s);
        Assert.Equal(["subj", "p", "b"], c.Names);
    }

    [Fact]
    public void Walker_If_WithoutElse_DoesNotThrow() {
        IfStmt i = new(R, Id("c"),
            new BlockStmt(R, [new ExpressionStmt(R, Id("t"))]),
            null);
        IdentifierCollector col = new();
        col.Visit(i);
        Assert.Equal(["c", "t"], col.Names);
    }
}
