using System.Text;

using Grob.Compiler.Ast;

using Xunit;

using static Grob.Compiler.Tests.AstTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Smoke test for a minimal pretty-printer built on top of
/// <see cref="AstVisitor{T}"/>. The printer itself is defined here in the
/// test project — Sprint 1 only needs proof that the visitor surface can
/// drive a non-trivial consumer end-to-end.
/// </summary>
public class AstPrettyPrinterTests {
    [Fact]
    public void Print_BinaryAddOfTwoIdentifiers_ProducesInfixForm() {
        BinaryExpr expr = new(R, BinaryOperator.Add, Id("a"), Id("b"));
        Assert.Equal("(a + b)", new PrettyPrinter().Print(expr));
    }

    [Fact]
    public void Print_SmallFnDecl_IncludesSignatureAndBody() {
        FnDecl fn = new(
            R,
            "add",
            [
                new Parameter(R, "a", new TypeRef(R, "Int", [], false), null),
                new Parameter(R, "b", new TypeRef(R, "Int", [], false), null),
            ],
            new TypeRef(R, "Int", [], false),
            new BlockStmt(R, [
                new ReturnStmt(R, new BinaryExpr(R, BinaryOperator.Add, Id("a"), Id("b"))),
            ]));

        string output = new PrettyPrinter().Print(fn);

        Assert.Contains("fn add(a: Int, b: Int): Int", output);
        Assert.Contains("return (a + b)", output);
    }

    [Fact]
    public void Print_ErrorExpr_RendersErrorMarker() {
        ErrorExpr err = new(R, ErrDiag("E0042"));
        Assert.Equal("<error:E0042>", new PrettyPrinter().Print(err));
    }

    /// <summary>
    /// Minimal pretty-printer covering only the node kinds exercised by the
    /// smoke tests. Demonstrates that every visit hook — including the three
    /// abstract error hooks — is reachable through the public visitor API.
    /// </summary>
    private sealed class PrettyPrinter : AstVisitor<string> {
        private int _indent;

        public string Print(AstNode node) => node.Accept(this);

        public override string VisitIntLiteral(IntLiteralExpr node) => node.Value.ToString();
        public override string VisitIdentifier(IdentifierExpr node) => node.Name;

        public override string VisitBinary(BinaryExpr node) {
            string op = node.Operator switch {
                BinaryOperator.Add => "+",
                BinaryOperator.Subtract => "-",
                BinaryOperator.Multiply => "*",
                BinaryOperator.Divide => "/",
                _ => "?",
            };
            return $"({node.Left.Accept(this)} {op} {node.Right.Accept(this)})";
        }

        public override string VisitReturn(ReturnStmt node) =>
            node.Value is null ? "return" : $"return {node.Value.Accept(this)}";

        public override string VisitBlock(BlockStmt node) {
            StringBuilder sb = new();
            sb.Append("{\n");
            _indent++;
            foreach (Statement s in node.Statements) {
                sb.Append(new string(' ', _indent * 2));
                sb.Append(s.Accept(this));
                sb.Append('\n');
            }
            _indent--;
            sb.Append(new string(' ', _indent * 2));
            sb.Append('}');
            return sb.ToString();
        }

        public override string VisitFnDecl(FnDecl node) {
            StringBuilder sb = new();
            sb.Append("fn ").Append(node.Name).Append('(');
            for (int i = 0; i < node.Parameters.Count; i++) {
                if (i > 0) sb.Append(", ");
                Parameter p = node.Parameters[i];
                sb.Append(p.Name);
                if (p.Type is not null) sb.Append(": ").Append(p.Type.Name);
            }
            sb.Append("): ").Append(node.ReturnType.Name).Append(' ');
            sb.Append(node.Body.Accept(this));
            return sb.ToString();
        }

        public override string VisitErrorExpr(ErrorExpr node) => $"<error:{node.Diagnostic.Code}>";
        public override string VisitErrorStmt(ErrorStmt node) => $"<error:{node.Diagnostic.Code}>";
        public override string VisitErrorDecl(ErrorDecl node) => $"<error:{node.Diagnostic.Code}>";
    }
}
