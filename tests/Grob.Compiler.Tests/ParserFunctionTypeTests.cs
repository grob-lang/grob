using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>Parser tests for function-type references (D-326).</summary>
public class ParserFunctionTypeTests {
    // ------------------------------------------------------------------
    // Well-formed function types
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionTypeRef_NoParams_ReturnsInt() {
        CompilationUnit unit = ParseOk("fn f(): fn(): int { return nil }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef fnType = Assert.IsType<FunctionTypeRef>(fn.ReturnType);
        Assert.Empty(fnType.ParameterTypes);
        Assert.Equal("int", fnType.ReturnType.Name);
        Assert.False(fnType.IsNullable);
        Assert.False(fnType.ReturnType.IsNullable);
    }

    [Fact]
    public void FunctionTypeRef_TwoParams_ReturnsBool() {
        CompilationUnit unit = ParseOk("fn f(action: fn(int, string): bool): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef paramType = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.Equal(2, paramType.ParameterTypes.Count);
        Assert.Equal("int", paramType.ParameterTypes[0].Name);
        Assert.Equal("string", paramType.ParameterTypes[1].Name);
        Assert.Equal("bool", paramType.ReturnType.Name);
        Assert.False(paramType.IsNullable);
    }

    [Fact]
    public void FunctionTypeRef_Nested_FnParamReturnsBool() {
        // fn(fn(int): bool): void
        CompilationUnit unit = ParseOk("fn f(pred: fn(fn(int): bool): void): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef outer = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.Single(outer.ParameterTypes);
        FunctionTypeRef inner = Assert.IsType<FunctionTypeRef>(outer.ParameterTypes[0]);
        Assert.Single(inner.ParameterTypes);
        Assert.Equal("int", inner.ParameterTypes[0].Name);
        Assert.Equal("bool", inner.ReturnType.Name);
        Assert.Equal("void", outer.ReturnType.Name);
    }

    [Fact]
    public void FunctionTypeRef_NullableReturnType_SufixOnReturn() {
        // fn(): int? — ? binds to the return type, not the function itself
        CompilationUnit unit = ParseOk("fn f(): fn(): int? { return nil }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef fnType = Assert.IsType<FunctionTypeRef>(fn.ReturnType);
        Assert.False(fnType.IsNullable);
        Assert.True(fnType.ReturnType.IsNullable);
        Assert.Equal("int", fnType.ReturnType.Name);
    }

    [Fact]
    public void FunctionTypeRef_NullableFunction_ParenSuffix() {
        // (fn(): int)? — ? on the outer paren makes the function itself nullable
        CompilationUnit unit = ParseOk("fn f(action: (fn(): int)?): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef paramType = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.True(paramType.IsNullable);
        Assert.Empty(paramType.ParameterTypes);
        Assert.Equal("int", paramType.ReturnType.Name);
        Assert.False(paramType.ReturnType.IsNullable);
    }

    [Fact]
    public void MakeCounter_ReturnAnnotation_Parses() {
        // The canonical D-326 example: fn returning fn(): int.
        CompilationUnit unit = ParseOk("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef returnType = Assert.IsType<FunctionTypeRef>(fn.ReturnType);
        Assert.Empty(returnType.ParameterTypes);
        Assert.Equal("int", returnType.ReturnType.Name);
    }

    // ------------------------------------------------------------------
    // Malformed function types
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionTypeRef_Malformed_MissingReturnType_IsE2001() {
        // "fn(): " with no return type — the parser expects a TypeRef after ':'
        // but finds '{'. E2001 lands at the '{' (column 15 in "fn f(): fn(): {").
        (_, DiagnosticBag bag) = Parse("fn f(): fn(): { return nil }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(15, d.Range.Start.Column);
    }
}
