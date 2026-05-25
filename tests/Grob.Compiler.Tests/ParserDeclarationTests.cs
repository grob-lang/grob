using Grob.Compiler.Ast;

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

    [Fact]
    public void ParamBlock_WithDecorators() {
        CompilationUnit unit = ParseOk(
            "param {\n@allowed(\"a\", \"b\")\nmode: string\nlen: int = 0\n}\n");
        ParamBlockDecl p = Single<ParamBlockDecl>(unit);
        Assert.Equal(2, p.Parameters.Count);
        Assert.Equal("mode", p.Parameters[0].Name);
        Assert.Equal("string", p.Parameters[0].Type!.Name);
        Assert.NotNull(p.Parameters[1].DefaultValue);
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
