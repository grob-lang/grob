using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

public class ParserDeclarationTests {
    [Fact]
    public void Fn_NoParams_ReturnsInt() {
        CompilationUnit unit = ParseOk("fn f(): int { return 1 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal("f", fn.Name);
        Assert.Empty(fn.Parameters);
        Assert.Equal("int", fn.ReturnType.Name);
    }

    [Fact]
    public void Fn_TwoParams_AnnotatedReturn() {
        CompilationUnit unit = ParseOk("fn add(a: int, b: int): int { return a + b }\n");
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("int", fn.Parameters[0].Type!.Name);
    }

    [Fact]
    public void Fn_DefaultParameter() {
        CompilationUnit unit = ParseOk("fn f(n: int = 5): int { return n }\n");
        FnDecl fn = Single<FnDecl>(unit);
        Assert.NotNull(fn.Parameters[0].DefaultValue);
    }

    [Fact]
    public void Type_WithFields() {
        CompilationUnit unit = ParseOk(
            "type Point {\nx: int\ny: int = 0\n}\n");
        TypeDecl t = Single<TypeDecl>(unit);
        Assert.Equal("Point", t.Name);
        Assert.Equal(2, t.Fields.Count);
        Assert.NotNull(t.Fields[1].DefaultValue);
    }

    [Fact]
    public void Import_Plain() {
        CompilationUnit unit = ParseOk("import io\n");
        ImportDecl i = Single<ImportDecl>(unit);
        Assert.Equal("io", i.ModulePath);
        Assert.Null(i.Alias);
    }

    [Fact]
    public void Import_Dotted_WithAlias() {
        CompilationUnit unit = ParseOk("import std.io as Io\n");
        ImportDecl i = Single<ImportDecl>(unit);
        Assert.Equal("std.io", i.ModulePath);
        Assert.Equal("Io", i.Alias);
    }

    [Fact]
    public void Const_TopLevel() {
        CompilationUnit unit = ParseOk("const PI := 3.14\n");
        ConstDecl c = Single<ConstDecl>(unit);
        Assert.Equal("PI", c.Name);
        Assert.IsType<FloatLiteralExpr>(c.Value);
    }

    [Fact]
    public void Readonly_TopLevel_WithAnnotation() {
        CompilationUnit unit = ParseOk("readonly NAME: string := \"sam\"\n");
        ReadonlyDecl r = Single<ReadonlyDecl>(unit);
        Assert.Equal("string", r.AnnotatedType!.Name);
    }

    /// <summary>
    /// D-410: the braceless per-line form is canonical. A decorator stack is
    /// parsed and skipped (not yet captured into the AST — Sprint 10) and does
    /// not disturb the resulting <see cref="ParamDecl"/>.
    /// </summary>
    [Fact]
    public void Param_Decorated_ParsesToSingleDeclaration() {
        CompilationUnit unit = ParseOk("@allowed(\"a\", \"b\")\nparam mode: string\n");
        ParamDecl p = Single<ParamDecl>(unit);
        Assert.Equal("mode", p.Name);
        Assert.Equal("string", p.Type.Name);
        Assert.Null(p.DefaultValue);
    }

    /// <summary>D-410: <c>param { ... }</c> is retired and no longer parses.</summary>
    [Fact]
    public void ParamBlock_NoLongerParses() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param {\nmode: string\n}\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected parameter name after 'param'", d.Message);
        Assert.NotNull(unit);
    }

    [Fact]
    public void TypeRef_Generic_Nullable() {
        CompilationUnit unit = ParseOk("fn f(xs: Array<int>?): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        TypeRef t = fn.Parameters[0].Type!;
        Assert.Equal("Array", t.Name);
        Assert.Single(t.TypeArguments);
        Assert.Equal("int", t.TypeArguments[0].Name);
        Assert.True(t.IsNullable);
    }
}
